using Offsets;
using squad_dma.Source.Squad.Features;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;

namespace squad_dma
{
    /// <summary>
    /// Class containing Game instance.
    /// </summary>
    public class Game
    {
        #region Fields
        private readonly ulong _squadBase;
        private volatile bool _inGame = false;
        private RegistredActors _actors;
        private UActor _localUPlayer;
        private ulong _gameWorld;
        private ulong _gameInstance;
        private ulong _localPlayer;
        private ulong _playerController;
        private Vector3 _absoluteLocation;
        private string _currentLevel = string.Empty;
        private DateTime _lastTeamCheck = DateTime.MinValue;
        private const int TeamCheckInterval = 1000;

        private GameTickets _gameTickets;
        private PlayerStats _gameStats;
        private DebugVehicles _debugVehicles;
        private DebugTeam _debugTeam;
        private LocalSoldier _localSoldier;

        //FOV & Recoil thing
        private ulong _currentWeaponPtr = 0;
        private ulong _lastNoRecoilWeaponPtr = 0;

        private bool _isAimingDownSights;
        private bool _hasPipScope;
        private float _currentFOV;
        private int _magnificationIndex;
        private bool _isFiring = false;
        #endregion

        #region Properties
        public bool InGame => _inGame;
        public string MapName => _currentLevel;
        public UActor LocalPlayer => _localUPlayer;
        public ReadOnlyDictionary<ulong, UActor> Actors => _actors?.Actors;
        public Vector3 AbsoluteLocation => _absoluteLocation;
        public Dictionary<int, int> TeamTickets => _gameTickets.GetTickets();
        public GameTickets GameTickets => _gameTickets;
        public PlayerStats GameStats => _gameStats;
        public bool IsAimingDownSights => _isAimingDownSights;
        public bool HasPipScope => _hasPipScope;
        public float CurrentFOV => _currentFOV;
        public bool IsFiring => _isFiring;

        #endregion

        #region Constructor
        public Game(ulong squadBase)
        {
            _squadBase = squadBase;
            _gameTickets = null;
            _gameStats = null;
        }
        #endregion
        /*

               #region GameLoop
               /// <summary>
               /// Main Game Loop executed by Memory Worker Thread.
               /// </summary>
               public void GameLoop()
               {
                   try
                   {
                       if (!this._inGame)
                       {
                           this._vehiclesLogged = false;
                           throw new GameEnded("Game has ended!");
                       }

                       UpdateLocalPlayerInfo();
                       this._actors.UpdateList();
                       this._actors.UpdateAllPlayers();
                       // LogTeamInfo();
                       //ApplyNoSpread();
                       //ApplyNoRecoil();
                       if (Program.Config.NoRecoil)
                       {
                           ApplyNoRecoilNoSpread();
                       }
                       if (Program.Config.NoSway)
                       {
                           ApplyNoSway();
                       }
                       if (Program.Config.NoCameraShake)
                       {
                           ApplyNoCameraShake();
                       }
                   }
                   catch (DMAShutdown)
                   {
                       HandleDMAShutdown();
                   }
                   catch (GameEnded e)
                   {
                       HandleGameEnded(e);
                   }
                   catch (Exception ex)
                   {
                       HandleUnexpectedException(ex);
                   }
               }
               #endregion
        */
        #region Public Methods
        public void SetInstantSeatSwitch() => _debugVehicles?.SetInstantSeatSwitch();
        public void LogVehicles(bool force = false) => _debugVehicles?.LogVehicles(force);
        public void VehicleTeam() => _debugVehicles?.VehicleTeam();
        public void LogTeamInfo() => _debugTeam?.LogTeamInfo();
        public void SetSuppression(bool enable) => _localSoldier?.SetSuppression(enable);
        public void SetInteractionDistances(bool enable) => _localSoldier?.SetInteractionDistances(enable);
        public void SetShootingInMainBase(bool enable) => _localSoldier?.SetShootingInMainBase(enable);
        public void SetSpeedHack(bool enable) => _localSoldier?.SetSpeedHack(enable);
        public void SetAirStuck(bool enable) => _localSoldier?.SetAirStuck(enable);
        public void SetHideActor(bool enable) => _localSoldier?.SetHideActor(enable);
        public void WaitForGame()
        {
            while (true)
            {
                try
                {
                    if (!Memory.GetModuleBase())
                    {
                        throw new GameNotRunningException("Process terminated during wait");
                    }

                    if (GetGameWorld() && GetGameInstance() && GetCurrentLevel() && InitActors() && GetLocalPlayer())
                    {
                        if (!Memory.GetModuleBase())
                        {
                            throw new GameNotRunningException("Process terminated during initialization");
                        }

                        Thread.Sleep(1000);
                        Program.Log("Game has started!!");
                        this._inGame = true;
                        Memory.GameStatus = GameStatus.InGame;
                        
                        _gameTickets = new GameTickets(_gameWorld, _localUPlayer);
                        _gameStats = new PlayerStats(_playerController);
                        
                        InitializeManagers();
                        
                        return;
                    }
                }
                catch (GameNotRunningException)
                {
                    throw; // Propagate up to break out of wait loop
                }
                Thread.Sleep(500);
            }
        }

        public void GameLoop()
        {
            try
            {
                if (!this._inGame)
                {
                    throw new GameEnded("Game has ended!");
                }
                UpdateLocalPlayerInfo(); // Updates team/squad and camera cache
                ProcessPlayerInfo(); // Updates weapon, ADS, FOV, etc. for ESP
                this._actors.UpdateList(); // Updates actor list
                this._actors.UpdateAllPlayers(); // Updates all player data

                if (Program.Config.NoRecoil)
                {
                    ApplyNoRecoilNoSpread();
                }
                if (Program.Config.NoSway)
                {
                    ApplyNoSway();
                }
                if (Program.Config.NoCameraShake)
                {
                    ApplyNoCameraShake();
                }

                //LogTeamInfo();
                if (DateTime.Now.Second % 3 == 0) // Adjust as needed
                {
                    //VehicleTeam();
                }
            }
            catch (DMAShutdown)
            {
                HandleDMAShutdown();
            }
            catch (GameEnded e)
            {
                HandleGameEnded(e);
            }
            catch (Exception ex)
            {
                HandleUnexpectedException(ex);
            }
        }
        #endregion

        #region Private Methods
        private void InitializeManagers()
        {
            _debugVehicles = new DebugVehicles(_playerController, _inGame, _actors);
            _debugTeam = new DebugTeam(_inGame, _localUPlayer, _actors?.Actors);
            _localSoldier = new LocalSoldier(_playerController, _inGame, _actors);
        }

        private bool TryExecute(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch { return false; }
        }

        private void HandleDMAShutdown()
        {
            Program.Log("DMA shutdown");
            this._inGame = false;
        }
        private void HandleGameEnded(GameEnded e)
        {
            Program.Log("Game has ended!");
            this._inGame = false;
            Memory.GameStatus = GameStatus.Menu;
            Memory.Restart();
        }

        private void HandleUnexpectedException(Exception ex)
        {
            Program.Log($"CRITICAL ERROR - Game ended due to unhandled exception: {ex}");
            this._inGame = false;
        }

        private bool GetGameWorld() => TryExecute(() => _gameWorld = Memory.ReadPtr(_squadBase + Offsets.GameObjects.GWorld));

        private bool GetGameInstance() => TryExecute(() => _gameInstance = Memory.ReadPtr(_gameWorld + Offsets.World.OwningGameInstance));

        private bool GetCurrentLevel() => TryExecute(() => {
            var currentLayer = Memory.ReadPtr(_gameInstance + Offsets.GameInstance.CurrentLayer);
            var currentLevelIdPtr = currentLayer + Offsets.SQLayer.LevelID;
            var currentLevelId = Memory.ReadValue<uint>(currentLevelIdPtr);
            _currentLevel = Memory.GetNamesById([currentLevelId])[currentLevelId];
            Program.Log("Current level is " + _currentLevel);
        });

        private bool InitActors() => TryExecute(() => {
            var persistentLevel = Memory.ReadPtr(_gameWorld + Offsets.World.PersistentLevel);
            _actors = new RegistredActors(persistentLevel);
        });

        private bool GetLocalPlayer() => TryExecute(() => {
            var localPlayers = Memory.ReadPtr(_gameInstance + Offsets.GameInstance.LocalPlayers);
            _localPlayer = Memory.ReadPtr(localPlayers);
            _localUPlayer = new UActor(_localPlayer);
            _localUPlayer.Team = Team.Unknown;
            GetPlayerController();
        });

        private bool GetPlayerController() => TryExecute(() => _playerController = Memory.ReadPtr(_localPlayer + Offsets.UPlayer.PlayerController));

        private bool UpdateLocalPlayerInfo()
        {
            try
            {
                if ((DateTime.Now - _lastTeamCheck).TotalMilliseconds > TeamCheckInterval)
                {
                    _lastTeamCheck = DateTime.Now;

                    try
                    {
                        ulong playerState = Memory.ReadPtr(_playerController + Offsets.Controller.PlayerState);
                        ulong squadState = Memory.ReadPtr(_playerController + Offsets.PlayerController.SquadState);

                        if (playerState == 0 || squadState == 0)
                            return false;

                        int teamId = Memory.ReadValue<int>(playerState + Offsets.ASQPlayerState.TeamID);
                        int squadId = Memory.ReadValue<int>(squadState + Offsets.ASQSquadState.SquadId);

                        if (_localUPlayer.TeamID != teamId || _localUPlayer.SquadID != squadId)
                        {
                            _localUPlayer.TeamID = teamId;
                            _localUPlayer.SquadID = squadId;
                        }
                    }
                    catch { return false; }
                }
                //ProcessPlayerInfo();
                GetCameraCache();
                return true;
            }
            catch
            {
                return false;
            }
        }
        private bool GetCameraCache()
        {
            try
            {
                var cameraInfoScatterMap = new ScatterReadMap(1);
                var cameraManagerRound = cameraInfoScatterMap.AddRound();
                var cameraInfoRound = cameraInfoScatterMap.AddRound();

                var cameraManagerPtr = cameraManagerRound.AddEntry<ulong>(0, 0, _playerController + Offsets.PlayerController.PlayerCameraManager);
                cameraManagerRound.AddEntry<int>(0, 11, _gameWorld + Offsets.World.WorldOrigin);
                cameraManagerRound.AddEntry<int>(0, 12, _gameWorld + Offsets.World.WorldOrigin + 0x4);
                cameraManagerRound.AddEntry<int>(0, 13, _gameWorld + Offsets.World.WorldOrigin + 0x8);
                cameraInfoRound.AddEntry<Vector3>(0, 1, cameraManagerPtr, null, Offsets.Camera.CameraLocation);
                cameraInfoRound.AddEntry<Vector3>(0, 2, cameraManagerPtr, null, Offsets.Camera.CameraRotation);

                cameraInfoScatterMap.Execute();

                if (!cameraInfoScatterMap.Results[0][1].TryGetResult<Vector3>(out var location))
                {
                    return false;
                }
                if (!cameraInfoScatterMap.Results[0][2].TryGetResult<Vector3>(out var rotation))
                {
                    return false;
                }
                if (cameraInfoScatterMap.Results[0][11].TryGetResult<int>(out var absoluteX)
                && cameraInfoScatterMap.Results[0][12].TryGetResult<int>(out var absoluteY)
                && cameraInfoScatterMap.Results[0][13].TryGetResult<int>(out var absoluteZ))
                {
                    _absoluteLocation = new Vector3(absoluteX, absoluteY, absoluteZ);
                }
                _localUPlayer.Position = location;
                _localUPlayer.Rotation = new Vector2(rotation.Y, rotation.X);
                _localUPlayer.Rotation3D = rotation;
                return true;
            }
            catch { return false; }
        }


        /// <summary>
        /// Processes and updates player information from game memory.
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        private bool ProcessPlayerInfo()
    {
        var scatterMap = new ScatterReadMap(1);
        ulong pawnPtr = ReadPawnPointer();
        if (pawnPtr == 0)
        {
            ResetPlayerStateToDefault();
            return true;
        }

        string pawnClassName = Memory.GetActorClassName(pawnPtr);
        bool isInVehicle = !pawnClassName.Contains("BP_Soldier");
        float cameraFOV = ReadCameraFOV();

        if (isInVehicle)
        {
            _currentFOV = cameraFOV;
            _isAimingDownSights = false;
            _hasPipScope = false;
            return true;
        }

        return UpdateOnFootPlayerInfo(scatterMap, pawnPtr, cameraFOV);
    }

    /// <summary>
    /// Reads the pawn pointer from memory using offset.
    /// </summary>
    /// <returns>The pawn pointer value</returns>
    private ulong ReadPawnPointer()
    {
        return Memory.ReadPtr(_playerController + Offsets.PlayerController.AcknowledgedPawn);
    }

    /// <summary>
    /// Reads the camera FOV from memory using offset.
    /// </summary>
    /// <returns>The camera FOV value</returns>

    /// <summary>
    /// Applies no-recoil effect by zeroing out recoil-related memory values.
    /// </summary>
    /// <summary>
    /// Applies no-recoil and no-sway effects by zeroing out relevant memory values.
    /// </summary>
    private readonly List<IScatterWriteEntry> _noRecoilAnimEntries = new List<IScatterWriteEntry>
{
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeapRecoilRelLoc, 0f),     // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeapRecoilRelLoc + 4, 0f), // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeapRecoilRelLoc + 8, 0f), // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MoveRecoilFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.RecoilCanRelease, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalRecoilSigma, 0f),     // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalRecoilSigma + 4, 0f), // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalRecoilSigma + 8, 0f), // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalRecoilMean, 0f),      // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalRecoilMean + 4, 0f),  // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalRecoilMean + 8, 0f),  // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.StandRecoilMean, 0f),      // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.StandRecoilMean + 4, 0f),  // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.StandRecoilMean + 8, 0f),  // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.StandRecoilSigma, 0f),     // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.StandRecoilSigma + 4, 0f), // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.StandRecoilSigma + 8, 0f), // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.CrouchRecoilMean, 0f),     // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.CrouchRecoilMean + 4, 0f), // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.CrouchRecoilMean + 8, 0f), // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.CrouchRecoilSigma, 0f),    // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.CrouchRecoilSigma + 4, 0f),// Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.CrouchRecoilSigma + 8, 0f),// Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneRecoilMean, 0f),      // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneRecoilMean + 4, 0f),  // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneRecoilMean + 8, 0f),  // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneRecoilSigma, 0f),     // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneRecoilSigma + 4, 0f), // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneRecoilSigma + 8, 0f), // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneTransitionRecoilMean, 0f), // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneTransitionRecoilMean + 4, 0f), // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneTransitionRecoilMean + 8, 0f), // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneTransitionRecoilSigma, 0f), // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneTransitionRecoilSigma + 4, 0f), // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneTransitionRecoilSigma + 8, 0f), // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeaponPunch, 0f),          // Pitch
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeaponPunch + 4, 0f),      // Yaw
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeaponPunch + 8, 0f),      // Roll
};

    private readonly List<IScatterWriteEntry> _noRecoilWeaponEntries = new List<IScatterWriteEntry>
{
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.RecoilCameraOffsetFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.RecoilWeaponRelLocFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.AddMoveRecoil, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MaxMoveRecoilFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandRecoilMean, 0f),      // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandRecoilMean + 4, 0f),  // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandRecoilMean + 8, 0f),  // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandRecoilSigma, 0f),     // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandRecoilSigma + 4, 0f), // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandRecoilSigma + 8, 0f), // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandAdsRecoilMean, 0f),   // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandAdsRecoilMean + 4, 0f),// Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandAdsRecoilMean + 8, 0f),// Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandAdsRecoilSigma, 0f),  // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandAdsRecoilSigma + 4, 0f),// Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandAdsRecoilSigma + 8, 0f),// Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchRecoilMean, 0f),     // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchRecoilMean + 4, 0f), // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchRecoilMean + 8, 0f), // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchRecoilSigma, 0f),    // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchRecoilSigma + 4, 0f),// Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchRecoilSigma + 8, 0f),// Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchAdsRecoilMean, 0f),  // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchAdsRecoilMean + 4, 0f),// Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchAdsRecoilMean + 8, 0f),// Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchAdsRecoilSigma, 0f), // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchAdsRecoilSigma + 4, 0f),// Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchAdsRecoilSigma + 8, 0f),// Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneRecoilMean, 0f),      // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneRecoilMean + 4, 0f),  // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneRecoilMean + 8, 0f),  // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneRecoilSigma, 0f),     // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneRecoilSigma + 4, 0f), // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneRecoilSigma + 8, 0f), // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneAdsRecoilMean, 0f),   // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneAdsRecoilMean + 4, 0f),// Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneAdsRecoilMean + 8, 0f),// Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneAdsRecoilSigma, 0f),  // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneAdsRecoilSigma + 4, 0f),// Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneAdsRecoilSigma + 8, 0f),// Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneTransitionRecoilMean, 0f), // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneTransitionRecoilMean + 4, 0f), // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneTransitionRecoilMean + 8, 0f), // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneTransitionRecoilSigma, 0f), // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneTransitionRecoilSigma + 4, 0f), // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneTransitionRecoilSigma + 8, 0f), // Z
};

    private readonly List<IScatterWriteEntry> _noSwayAnimEntries = new List<IScatterWriteEntry>
{
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MoveSwayFactorMultiplier, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SuppressSwayFactorMultiplier, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeaponPunchSwayCombinedRotator, 0f), // Pitch
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeaponPunchSwayCombinedRotator + 4, 0f), // Yaw
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeaponPunchSwayCombinedRotator + 8, 0f), // Roll
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.UnclampedTotalSway, 0f),
    //new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.LocationOffsetMultiplier, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.UnclampedTotalSway, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.TotalSway, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.Sway, 0f), // Pitch
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.Sway + 4, 0f), // Yaw
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.Sway + 8, 0f), // Roll
    //new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.LocationOffset, 0f), // X
    //new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.LocationOffset + 4, 0f), // Y
    //new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.LocationOffset + 8, 0f), // Z
    //new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.LocationOffsetMultiplier, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.UnclampedTotalSway, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.TotalSway, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.Sway, 0f), // Pitch
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.Sway + 4, 0f), // Yaw
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.Sway + 8, 0f), // Roll
    //new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.LocationOffset, 0f), // X
   // new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.LocationOffset + 4, 0f), // Y
   // new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.LocationOffset + 8, 0f), // Z
};

    private readonly List<IScatterWriteEntry> _noSwayWeaponEntries = new List<IScatterWriteEntry>
{
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.AddMoveSway, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MaxMoveSwayFactor, 0f),
   // new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.LocationOffsetMultiplier, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.UnclampedTotalSway, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.TotalSway, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.Sway, 0f), // Pitch
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.Sway + 4, 0f), // Yaw
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.Sway + 8, 0f), // Roll
    //new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.LocationOffset, 0f), // X
    //new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.LocationOffset + 4, 0f), // Y
    //new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.LocationOffset + 8, 0f), // Z
    //new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.LocationOffsetMultiplier, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.UnclampedTotalSway, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.TotalSway, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.Sway, 0f), // Pitch
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.Sway + 4, 0f), // Yaw
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.Sway + 8, 0f), // Roll
    //new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.LocationOffset, 0f), // X
   // new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.LocationOffset + 4, 0f), // Y
    //new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.LocationOffset + 8, 0f), // Z
};

    private readonly List<IScatterWriteEntry> _noSpreadAnimEntries = new List<IScatterWriteEntry>
{
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MoveDeviationFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ShotDeviationFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalDeviation, 0f),      // X
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalDeviation + 4, 0f),  // Y
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalDeviation + 8, 0f),  // Z
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalDeviation + 12, 0f), // W
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.AddMoveDeviation, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MoveDeviationFactorRelease, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MaxMoveDeviationFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinMoveDeviationFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FullStaminaDeviationFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.LowStaminaDeviationFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.AddShotDeviationFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.AddShotDeviationFactorAds, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ShotDeviationFactorRelease, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinShotDeviationFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MaxShotDeviationFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinProneAdsDeviation, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinProneDeviation, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinCrouchAdsDeviation, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinCrouchDeviation, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinStandAdsDeviation, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinStandDeviation, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinProneTransitionDeviation, 0f),
};

    private readonly List<IScatterWriteEntry> _noSpreadWeaponEntries = new List<IScatterWriteEntry>
{
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinShotDeviationFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MaxShotDeviationFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.AddShotDeviationFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.AddShotDeviationFactorAds, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ShotDeviationFactorRelease, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.LowStaminaDeviationFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.FullStaminaDeviationFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MoveDeviationFactorRelease, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.AddMoveDeviation, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MaxMoveDeviationFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinMoveDeviationFactor, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinProneAdsDeviation, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinProneDeviation, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinCrouchAdsDeviation, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinCrouchDeviation, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinStandAdsDeviation, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinStandDeviation, 0f),
    new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinProneTransitionDeviation, 0f),
};
    // Helper method to get common base pointers
    private bool GetBasePointers(out ulong animInstancePtr, out ulong weaponStaticInfoPtr)
{
    animInstancePtr = 0;
    weaponStaticInfoPtr = 0;

    ulong pawnPtr = Memory.ReadPtr(_playerController + Offsets.PlayerController.AcknowledgedPawn);
    if (pawnPtr == 0) return false;

    ulong inventoryPtr = Memory.ReadPtr(pawnPtr + Offsets.ASQSoldier.InventoryComponent);
    if (inventoryPtr == 0) return false;

    ulong weaponPtr = Memory.ReadPtr(inventoryPtr + Offsets.USQPawnInventoryComponent.CurrentWeapon);
    if (weaponPtr == 0) return false;

    animInstancePtr = Memory.ReadPtr(pawnPtr + Offsets.ASQSoldier.CachedAnimInstance1p);
    weaponStaticInfoPtr = Memory.ReadPtr(weaponPtr + Offsets.ASQWeapon.WeaponStaticInfo);

    return animInstancePtr != 0 && weaponStaticInfoPtr != 0;
}

    // Helper method to update scatter entry addresses
    private IScatterWriteEntry UpdateEntryAddress(IScatterWriteEntry entry, ulong baseAddress)
    {
        if (entry is ScatterWriteDataEntry<float> floatEntry)
        {
            return new ScatterWriteDataEntry<float>(baseAddress + (ulong)floatEntry.Address, floatEntry.Data);
        }
        return entry;
    }

        public void ApplyNoRecoilNoSpread()
        {
            try
            {
                ulong pawnPtr = Memory.ReadPtr(_playerController + Offsets.PlayerController.AcknowledgedPawn);
                if (pawnPtr == 0)
                {
                    _lastNoRecoilWeaponPtr = 0;
                    Program.Log("No-recoil/no-spread skipped: No acknowledged pawn.");
                    return;
                }

                // Check if the player is in a vehicle
                string pawnClassName = Memory.GetActorClassName(pawnPtr);
                bool isInVehicle = !pawnClassName.Contains("BP_Soldier");
                if (isInVehicle)
                {
                    _lastNoRecoilWeaponPtr = 0;
                    // Program.Log("No-recoil/no-spread skipped: Player is in a vehicle.");
                    return;
                }

                if (_currentWeaponPtr == 0)
                {
                    _lastNoRecoilWeaponPtr = 0;
                    // Program.Log("No-recoil/no-spread skipped: No current weapon detected.");
                    return;
                }

                if (!GetBasePointers(out ulong animInstancePtr, out ulong weaponStaticInfoPtr))
                {
                    Program.Log("No-recoil/no-spread skipped: Failed to get base pointers.");
                    return;
                }

                var scatterEntries = new List<IScatterWriteEntry>();

                if (_currentWeaponPtr != _lastNoRecoilWeaponPtr || _lastNoRecoilWeaponPtr == 0)
                {
                    scatterEntries.AddRange(_noRecoilAnimEntries.Select(e => UpdateEntryAddress(e, animInstancePtr)));
                    scatterEntries.AddRange(_noRecoilWeaponEntries.Select(e => UpdateEntryAddress(e, weaponStaticInfoPtr)));
                    scatterEntries.AddRange(_noSpreadAnimEntries.Select(e => UpdateEntryAddress(e, animInstancePtr)));
                    scatterEntries.AddRange(_noSpreadWeaponEntries.Select(e => UpdateEntryAddress(e, weaponStaticInfoPtr)));
                    _lastNoRecoilWeaponPtr = _currentWeaponPtr;
                    Program.Log($"No-recoil & no-spread applied for weapon 0x{_currentWeaponPtr:X}");
                }

                if (scatterEntries.Count > 0)
                {
                    Memory.WriteScatter(scatterEntries);
                }
            }
            catch { }
        }
        // Applies no-sway (active when aiming)
        public void ApplyNoSway()
        {
            if (!_isAimingDownSights) return; // Only apply when ADS

            try
            {
                if (!GetBasePointers(out ulong animInstancePtr, out ulong weaponStaticInfoPtr)) return;

                var scatterEntries = new List<IScatterWriteEntry>();
                scatterEntries.AddRange(_noSwayAnimEntries.Select(e => UpdateEntryAddress(e, animInstancePtr)));
                scatterEntries.AddRange(_noSwayWeaponEntries.Select(e => UpdateEntryAddress(e, weaponStaticInfoPtr)));

                if (scatterEntries.Count > 0)
                {
                    Memory.WriteScatter(scatterEntries);
                    //Program.Log("No-sway applied successfully.");
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Failed to apply no-sway: {ex.Message}");
            }
        }

        // no-spread
        public void ApplyNoSpread()
        {
            //if (!_isFiring) return; // Uncomment to only apply when firing

            try
            {
                if (!GetBasePointers(out ulong animInstancePtr, out ulong weaponStaticInfoPtr)) return;

                var scatterEntries = new List<IScatterWriteEntry>();
                scatterEntries.AddRange(_noSpreadAnimEntries.Select(e => UpdateEntryAddress(e, animInstancePtr)));
                scatterEntries.AddRange(_noSpreadWeaponEntries.Select(e => UpdateEntryAddress(e, weaponStaticInfoPtr)));

                if (scatterEntries.Count > 0)
                {
                    Memory.WriteScatter(scatterEntries);
                    //Program.Log("No-spread applied successfully.");
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Failed to apply no-spread: {ex.Message}");
            }
        }

        // no-shake
        public void ApplyNoCameraShake()
        {
            if (!_isFiring) return; // Only apply when firing

            try
            {
                var scatterEntries = new List<IScatterWriteEntry>();

                // Handle camera shake suppression
                ulong cameraManagerPtr = Memory.ReadPtr(_playerController + Offsets.PlayerController.PlayerCameraManager);
                if (cameraManagerPtr == 0) return;
                ulong cameraShakeModPtr = Memory.ReadPtr(cameraManagerPtr + Offsets.Camera.CachedCameraShakeMod);
                if (cameraShakeModPtr != 0)
                {
                    ulong activeShakesDataPtr = Memory.ReadPtr(cameraShakeModPtr + Offsets.UCameraModifier_CameraShake.ActiveShakes);
                    if (activeShakesDataPtr != 0)
                    {
                        int activeShakesCount = Memory.ReadValue<int>(cameraShakeModPtr + Offsets.UCameraModifier_CameraShake.ActiveShakes + 0x8);
                        if (activeShakesCount > 0)
                        {
                            const int shakeInfoSize = 0x18;
                            for (int i = 0; i < activeShakesCount; i++)
                            {
                                ulong shakeBasePtr = Memory.ReadPtr(activeShakesDataPtr + (uint)(i * shakeInfoSize));
                                if (shakeBasePtr != 0)
                                {
                                    scatterEntries.Add(new ScatterWriteDataEntry<float>(shakeBasePtr + Offsets.UCameraShakeBase.ShakeScale, 0f));
                                }
                            }
                        }
                    }
                }

                if (scatterEntries.Count > 0)
                {
                    Memory.WriteScatter(scatterEntries);
                    //Program.Log("No-shake applied successfully.");
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Failed to apply no-shake: {ex.Message}");
            }
        }

        private float ReadCameraFOV()
        {
            ulong cameraManagerPtr = Memory.ReadPtr(_playerController + Offsets.PlayerController.PlayerCameraManager);
            return Memory.ReadValue<float>(cameraManagerPtr + Offsets.Camera.CameraFov);
        }

        /// <summary>
        /// Updates player info for on-foot scenarios.
        /// </summary>
        /// <param name="scatterMap">The scatter read map for batch memory reading</param>
        /// <param name="pawnPtr">Pointer to the pawn</param>
        /// <param name="cameraFOV">Base camera FOV</param>
        /// <returns>True if successful</returns>
        private bool UpdateOnFootPlayerInfo(ScatterReadMap scatterMap, ulong pawnPtr, float cameraFOV)
        {
            ulong inventoryPtr = Memory.ReadPtr(pawnPtr + Offsets.ASQSoldier.InventoryComponent);
            if (inventoryPtr == 0)
            {
                _isAimingDownSights = false;
                _hasPipScope = false;
                _currentFOV = cameraFOV;
                return true;
            }

            var round1 = scatterMap.AddRound();
            var weaponPtrEntry = round1.AddEntry<ulong>(0, 0, inventoryPtr + Offsets.USQPawnInventoryComponent.CurrentWeapon);
            scatterMap.Execute();

            if (!scatterMap.Results[0][0].TryGetResult<ulong>(out ulong weaponPtr) || weaponPtr == 0)
            {
                _isAimingDownSights = false;
                _hasPipScope = false;
                _currentFOV = cameraFOV;
                return true;
            }

            return UpdateWeaponInfo(scatterMap, weaponPtr, cameraFOV);
        }

        /// <summary>
        /// Updates weapon-specific information including ADS, scope, and FOV.
        /// </summary>
        /// <param name="scatterMap">The scatter read map</param>
        /// <param name="weaponPtr">Pointer to the current weapon</param>
        /// <param name="cameraFOV">Base camera FOV</param>
        /// <returns>True if successful</returns>
        private bool UpdateWeaponInfo(ScatterReadMap scatterMap, ulong weaponPtr, float cameraFOV)
        {
            _currentWeaponPtr = weaponPtr; // Update current weapon pointer

            var round2 = scatterMap.AddRound();
            round2.AddEntry<byte>(0, 1, weaponPtr + Offsets.ASQWeapon.bAimingDownSights);
            round2.AddEntry<ulong>(0, 2, weaponPtr + Offsets.ASQWeapon.CachedPipScope);
            round2.AddEntry<float>(0, 3, weaponPtr + Offsets.ASQWeapon.CurrentFOV);
            round2.AddEntry<byte>(0, 4, weaponPtr + Offsets.ASQWeapon.CurrentState);
            scatterMap.Execute();

            _isAimingDownSights = scatterMap.Results[0][1].TryGetResult<byte>(out byte ads) && ads == 1;
            _hasPipScope = scatterMap.Results[0][2].TryGetResult<ulong>(out ulong pipScopePtr) && pipScopePtr != 0;
            float weaponFOV = scatterMap.Results[0][3].TryGetResult<float>(out float currFOV) && currFOV > 5f && currFOV < 180f ? currFOV : cameraFOV;
            _isFiring = scatterMap.Results[0][4].TryGetResult<byte>(out byte firing) && firing == 1;

            float finalFOV = cameraFOV; // Default to camera FOV
            if (_isAimingDownSights)
            {
                finalFOV = weaponFOV; // Set to ADS FOV initially
                if (_hasPipScope && pipScopePtr != 0)
                {
                    UpdateScopeMagnification(pipScopePtr, weaponFOV, ref finalFOV); // Adjust for magnification
                }
            }

            // Assign the final FOV only once
            _currentFOV = finalFOV;

            //Program.Log($"ADS: {_isAimingDownSights}, PipScope: {_hasPipScope}, Firing: {_isFiring}, WeaponPtr: 0x{_currentWeaponPtr:X}, FOV: {_currentFOV}, WeaponFOV: {weaponFOV}, CameraFOV: {cameraFOV}");

            return true;
        }

        /// <summary>
        /// Updates scope magnification and adjusts FOV accordingly.
        /// </summary>
        /// <param name="pipScopePtr">The pipScopePtr adress</param>
        /// <param name="pipScopePtr">Pointer to the pip scope</param>
        /// <param name="weaponFOV">Base weapon FOV</param>
        private void UpdateScopeMagnification(ulong pipScopePtr, float weaponFOV, ref float fov)
        {
            // Directly read the CurrentMagnificationLevel using ReadValue
            int magnificationIdx = Memory.ReadValue<int>(pipScopePtr + Offsets.USQPipScopeCaptureComponent.CurrentMagnificationLevel);

            // Validate and assign the magnification index
            _magnificationIndex = (magnificationIdx >= 0 && magnificationIdx < 3) ? magnificationIdx : 0;
            //Program.Log($"ADS: {_isAimingDownSights}, PipScope: {_hasPipScope}, FOV: {_currentFOV}, WeaponFOV: {weaponFOV}, CameraFOV: {cameraFOV}");
            // Determine magnification factor based on index
            float magnification = _magnificationIndex switch
            {
                0 => Program.Config.FirstScopeMagnification,  // 1st scope Magnification
                1 => Program.Config.SecondScopeMagnification, // 2nd scope Magnification
                2 => Program.Config.ThirdScopeMagnification,  // 3rd scope Magnification
                _ => 1f                               // Default (no magnification)
            };

            if (magnification > 1f)
            {
                fov = GetZoomedFOV(magnification, weaponFOV);
            }
        }

        //Zoomed FOV Calculation :

        float GetZoomedFOV(float MagnificationDesired, float DefaultFOV)
        {
            float defaultFOVRad = DefaultFOV * 0.00872664626f; // Conversion degrés -> radians (π / 360)
            float zoomedHalfFOVRad = (float)Math.Atan(Math.Tan(defaultFOVRad) / MagnificationDesired);
            return 2.0f * zoomedHalfFOVRad * 57.295779513f; // Conversion radians -> degrés (180 / π)
        }
        /// <summary>
        /// Resets player state variables to their default values.
        /// </summary>
        private void ResetPlayerStateToDefault()
        {
            _isAimingDownSights = false;
            _hasPipScope = false;
            _isFiring = false;
            _currentFOV = 90f;
            _currentWeaponPtr = 0;
            _lastNoRecoilWeaponPtr = 0;
        }

        #endregion
    }

    #region Exceptions
    public class GameNotRunningException : Exception
    {
        public GameNotRunningException() { }
        public GameNotRunningException(string message) : base(message) { }
        public GameNotRunningException(string message, Exception inner) : base(message, inner) { }
    }

    public class GameEnded : Exception
    {
        public GameEnded() { }
        public GameEnded(string message) : base(message) { }
        public GameEnded(string message, Exception inner) : base(message, inner) { }
    }
    #endregion
}
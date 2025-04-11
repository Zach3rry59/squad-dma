using Offsets;
using squad_dma.Source.Squad.Features;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;

namespace squad_dma
{
    public class Game : IDisposable
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
        private ulong _currentWeaponPtr;

        private GameTickets _gameTickets;
        private PlayerStats _gameStats;
        private DebugVehicles _debugVehicles;
        private DebugTeam _debugTeam;
        private LocalSoldier _localSoldier;

        public struct SoldierState
        {
            public ulong PawnPtr;
            public ulong WeaponPtr;
            public ulong InventoryPtr;
            public bool IsAimingDownSights;
            public bool IsFiring;
            public float CameraFOV;
            public bool HasPipScope;
            public float CurrentFOV;
        }

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
        public Dictionary<int, int> TeamTickets => _gameTickets?.GetTickets();
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
        public void DisableCollision(bool disable) => _localSoldier?.DisableCollision(disable);
        public void SetQuickZoom(bool enable) => _localSoldier?.SetQuickZoom(enable);
        public void SetRapidFire(bool enable) => _localSoldier?.SetRapidFire(enable);
        public void SetInfiniteAmmo(bool enable) => _localSoldier?.SetInfiniteAmmo(enable);
        public void SetQuickSwap(bool enable) => _localSoldier?.SetQuickSwap(enable);
        public void SetNoRecoil(bool enable) => _localSoldier?.SetNoRecoil(enable);
        public void SetNoSway(bool enable) => _localSoldier?.SetNoSway(enable);
        public void SetNoCameraShake(bool enable) => _localSoldier?.SetNoCameraShake(enable);
        public void ReadCurrentWeapons(bool includeOtherPlayers = false) => _localSoldier?.ReadCurrentWeapons(includeOtherPlayers);

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
                    throw;
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
                try
                {
                    SoldierState state = ProcessSoldierInfo();
                    _localSoldier?.UpdateSoldierState(state);
                }
                catch
                { }
                  
                this._actors.UpdateList();
                this._actors.UpdateAllPlayers();

                // Example: Log team info every 10 seconds
                if (DateTime.Now.Second % 10 == 0)
                {
                    // Uncomment if needed: LogTeamInfo();
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
            //_debugVehicles = new DebugVehicles(_playerController, _inGame, _actors);
            //_debugTeam = new DebugTeam(_inGame, _localUPlayer, _actors?.Actors);
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
            _localSoldier?.Dispose();
            _localSoldier = null;
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

        private bool GetCurrentLevel() => TryExecute(() =>
        {
            var currentLayer = Memory.ReadPtr(_gameInstance + Offsets.GameInstance.CurrentLayer);
            var currentLevelIdPtr = currentLayer + Offsets.SQLayer.LevelID;
            var currentLevelId = Memory.ReadValue<uint>(currentLevelIdPtr);
            _currentLevel = Memory.GetNamesById([currentLevelId])[currentLevelId];
            Program.Log("Current level is " + _currentLevel);
        });

        private bool InitActors() => TryExecute(() =>
        {
            var persistentLevel = Memory.ReadPtr(_gameWorld + Offsets.World.PersistentLevel);
            _actors = new RegistredActors(persistentLevel);
        });

        private bool GetLocalPlayer() => TryExecute(() =>
        {
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

        private SoldierState ProcessSoldierInfo()
        {
            SoldierState state = new SoldierState();
            state.PawnPtr = ReadPawnPointer();
            if (state.PawnPtr == 0)
            {
                ResetPlayerStateToDefault();
                return state;
            }

            string pawnClassName = Memory.GetActorClassName(state.PawnPtr);
            bool isInVehicle = !pawnClassName.Contains("BP_Soldier");
            state.CameraFOV = ReadCameraFOV();

            if (isInVehicle)
            {
                _currentFOV = state.CameraFOV;
                state.CurrentFOV = state.CameraFOV;
                return state;
            }

            var scatterMap = new ScatterReadMap(1);
            state.InventoryPtr = Memory.ReadPtr(state.PawnPtr + Offsets.ASQSoldier.InventoryComponent);
            if (state.InventoryPtr == 0)
            {
                state.CurrentFOV = state.CameraFOV;
                return state;
            }

            var round1 = scatterMap.AddRound();
            var weaponPtrEntry = round1.AddEntry<ulong>(0, 0, state.InventoryPtr + Offsets.USQPawnInventoryComponent.CurrentWeapon);
            scatterMap.Execute();

            if (!scatterMap.Results[0][0].TryGetResult<ulong>(out state.WeaponPtr) || state.WeaponPtr == 0)
            {
                state.CurrentFOV = state.CameraFOV;
                return state;
            }

            var round2 = scatterMap.AddRound();
            round2.AddEntry<byte>(0, 1, state.WeaponPtr + Offsets.ASQWeapon.bAimingDownSights);
            round2.AddEntry<ulong>(0, 2, state.WeaponPtr + Offsets.ASQWeapon.CachedPipScope);
            round2.AddEntry<float>(0, 3, state.WeaponPtr + Offsets.ASQWeapon.CurrentFOV);
            round2.AddEntry<byte>(0, 4, state.WeaponPtr + Offsets.ASQWeapon.CurrentState);
            scatterMap.Execute();

            state.IsAimingDownSights = scatterMap.Results[0][1].TryGetResult<byte>(out byte ads) && ads == 1;
            state.HasPipScope = scatterMap.Results[0][2].TryGetResult<ulong>(out ulong pipScopePtr) && pipScopePtr != 0;
            float weaponFOV = scatterMap.Results[0][3].TryGetResult<float>(out float currFOV) && currFOV > 5f && currFOV < 180f ? currFOV : state.CameraFOV;
            state.IsFiring = scatterMap.Results[0][4].TryGetResult<byte>(out byte firing) && firing == 1;

            state.CurrentFOV = state.CameraFOV;
            if (state.IsAimingDownSights && state.HasPipScope && pipScopePtr != 0)
            {
                UpdateScopeMagnification(pipScopePtr, weaponFOV, ref state.CurrentFOV);
            }
            else if (state.IsAimingDownSights)
            {
                state.CurrentFOV = weaponFOV;
            }

            _isAimingDownSights = state.IsAimingDownSights;
            _hasPipScope = state.HasPipScope;
            _currentFOV = state.CurrentFOV;
            _isFiring = state.IsFiring;
            _currentWeaponPtr = state.WeaponPtr;

            return state;
        }

        private ulong ReadPawnPointer()
        {
            if (_playerController == 0)
                return 0;

            try
            {
                return Memory.ReadPtr(_playerController + Offsets.PlayerController.AcknowledgedPawn);
            }
            catch
            {
                return 0;
            }
        }

        private float ReadCameraFOV()
        {
            ulong cameraManagerPtr = Memory.ReadPtr(_playerController + Offsets.PlayerController.PlayerCameraManager);
            return Memory.ReadValue<float>(cameraManagerPtr + Offsets.Camera.CameraFov);
        }

        private void UpdateScopeMagnification(ulong pipScopePtr, float weaponFOV, ref float fov)
        {
            int magnificationIdx = Memory.ReadValue<int>(pipScopePtr + Offsets.USQPipScopeCaptureComponent.CurrentMagnificationLevel);
            _magnificationIndex = (magnificationIdx >= 0 && magnificationIdx < 3) ? magnificationIdx : 0;

            float magnification = _magnificationIndex switch
            {
                0 => Program.Config.FirstScopeMagnification,
                1 => Program.Config.SecondScopeMagnification,
                2 => Program.Config.ThirdScopeMagnification,
                _ => 1f
            };

            if (magnification > 1f)
            {
                fov = GetZoomedFOV(magnification, weaponFOV);
            }
        }

        public float GetZoomedFOV(float magnificationDesired, float defaultFOV)
        {
            const float DegToRad = 0.01745329252f; // π / 180 for degrees to radians
            const float RadToDeg = 57.295779513f;  // 180 / π for radians to degrees

            float defaultFOVRad = defaultFOV * DegToRad; // Convert full FOV to radians
            float tanHalfFOV = (float)Math.Tan(defaultFOVRad / 2.0f); // Tangent of half the default FOV
            float zoomedHalfFOVRad = (float)Math.Atan(tanHalfFOV / magnificationDesired); // Half the zoomed FOV
            float zoomedFOV = 2.0f * zoomedHalfFOVRad * RadToDeg; // Full FOV in degrees

            return zoomedFOV;
        }
        private void ResetPlayerStateToDefault()
        {
            _isAimingDownSights = false;
            _hasPipScope = false;
            _isFiring = false;
            _currentFOV = 90f;
            _currentWeaponPtr = 0;
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            _localSoldier?.Dispose();
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
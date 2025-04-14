using squad_dma.Source.Squad.Features;
using squad_dma.Source.Squad.Debug;
using System.Collections.ObjectModel;
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
        private Vector3D _absoluteLocation;
        private string _currentLevel = string.Empty;
        private DateTime _lastTeamCheck = DateTime.MinValue;
        private const int TeamCheckInterval = 1000;

        // Ajout des champs pour les informations du joueur local
        private ulong _currentWeaponPtr;
        private bool _isAimingDownSights;
        private bool _hasPipScope;
        private double _currentFOV; // Changé en double pour UE5
        private int _magnificationIndex;
        private bool _isFiring = false;

        private Source.Squad.Manager _soldierManager;

        private GameTickets _gameTickets;
        private PlayerStats _gameStats;
        private DebugVehicles _debugVehicles;
        private DebugTeam _debugTeam;
        private DebugSoldier _debugSoldier;
        #endregion

        #region Properties
        public bool InGame => _inGame;
        public string MapName => _currentLevel;
        public UActor LocalPlayer => _localUPlayer;
        public ReadOnlyDictionary<ulong, UActor> Actors => _actors?.Actors;
        public Vector3D AbsoluteLocation => _absoluteLocation;
        public Dictionary<int, int> TeamTickets => _gameTickets.GetTickets();
        public GameTickets GameTickets => _gameTickets;
        public PlayerStats GameStats => _gameStats;
        // Ajout des propriétés publiques pour l'ESP
        public bool IsAimingDownSights => _isAimingDownSights;
        public bool HasPipScope => _hasPipScope;
        public double CurrentFOV => _currentFOV; // Changé en double pour UE5
        public bool IsFiring => _isFiring;
        public int MagnificationIndex => _magnificationIndex;
        #endregion

        #region Constructor
        public Game(ulong squadBase)
        {
            _squadBase = squadBase;
            _gameTickets = null;
            _gameStats = null;
            _currentFOV = 90.0; // Valeur par défaut
        }
        #endregion

        #region Public Methods
        public void SetInstantSeatSwitch() => _debugVehicles?.SetInstantSeatSwitch();
        public void LogVehicles(bool force = false) => _debugVehicles?.LogVehicles(force);
        public void VehicleTeam() => _debugVehicles?.VehicleTeam();
        public void LogTeamInfo() => _debugTeam?.LogTeamInfo();
        public void SetSuppression(bool enable) => _soldierManager?.SetSuppression(enable);
        public void SetInteractionDistances(bool enable) => _soldierManager?.SetInteractionDistances(enable);
        public void SetShootingInMainBase(bool enable) => _soldierManager?.SetShootingInMainBase(enable);
        public void SetSpeedHack(bool enable) => _soldierManager?.SetSpeedHack(enable);
        public void SetAirStuck(bool enable) => _soldierManager?.SetAirStuck(enable);
        public void SetHideActor(bool enable) => _soldierManager?.SetHideActor(enable);
        public void DisableCollision(bool disable) => _soldierManager?.DisableCollision(disable);
        public void SetQuickZoom(bool enable) => _soldierManager?.SetQuickZoom(enable);
        public void SetRapidFire(bool enable) => _soldierManager?.SetRapidFire(enable);
        public void SetInfiniteAmmo(bool enable) => _soldierManager?.SetInfiniteAmmo(enable);
        public void SetQuickSwap(bool enable) => _soldierManager?.SetQuickSwap(enable);
        public void ReadCurrentWeapons(bool includeOtherPlayers = false) => _debugSoldier?.ReadCurrentWeapons(includeOtherPlayers);
        public void LogCurrentValues() => _debugSoldier?.LogCurrentValues();

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

                UpdateLocalPlayerInfo();
                this._actors.UpdateList();
                this._actors.UpdateAllPlayers();
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
            _soldierManager = new Source.Squad.Manager(_playerController, _inGame, _actors);

            _debugVehicles = new DebugVehicles(_playerController, _inGame, _actors);
            _debugTeam = new DebugTeam(_inGame, _localUPlayer, _actors?.Actors);
            _debugSoldier = new DebugSoldier(_playerController, _inGame);
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

        private bool GetGameWorld() =>
            TryExecute(() => _gameWorld = Memory.ReadPtr(_squadBase + Offsets.GameObjects.GWorld));

        private bool GetGameInstance() =>
            TryExecute(() => _gameInstance = Memory.ReadPtr(_gameWorld + Offsets.World.OwningGameInstance));

        private bool GetCurrentLevel() =>
            TryExecute(() =>
            {
                var currentLayer = Memory.ReadPtr(_gameInstance + Offsets.GameInstance.CurrentLayer);
                var currentLevelIdPtr = currentLayer + Offsets.SQLayer.LevelID;
                var currentLevelId = Memory.ReadValue<uint>(currentLevelIdPtr);
                _currentLevel = Memory.GetNamesById([currentLevelId])[currentLevelId];
                Program.Log($"Current level is {_currentLevel}");
            });

        private bool InitActors() =>
            TryExecute(() =>
            {
                var persistentLevel = Memory.ReadPtr(_gameWorld + Offsets.World.PersistentLevel);
                _actors = new RegistredActors(persistentLevel);
            });

        private bool GetLocalPlayer() =>
            TryExecute(() =>
            {
                var localPlayers = Memory.ReadPtr(_gameInstance + Offsets.GameInstance.LocalPlayers);
                _localPlayer = Memory.ReadPtr(localPlayers);
                _localUPlayer = new UActor(_localPlayer);
                _localUPlayer.Team = Team.Unknown;
                GetPlayerController();
            });

        private bool GetPlayerController() =>
            TryExecute(() =>
            {
                _playerController = Memory.ReadPtr(_localPlayer + Offsets.UPlayer.PlayerController);
                Program.Log($"PlayerController: {_playerController:X}");
            });

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
                        ulong squadState = Memory.ReadPtr(_playerController + Offsets.SQPlayerController.SquadState);

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
                ProcessPlayerInfo();
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
                if (_playerController == 0 || _gameWorld == 0)
                    return false;

                var cameraInfoScatterMap = new ScatterReadMap(1);
                var cameraManagerRound = cameraInfoScatterMap.AddRound();
                var cameraInfoRound = cameraInfoScatterMap.AddRound();

                var cameraManagerPtr = Memory.ReadPtr(_playerController + Offsets.PlayerController.PlayerCameraManager);
                if (cameraManagerPtr == 0)
                    return false;

                var viewTargetPtr = cameraManagerPtr + Offsets.PlayerCameraManager.ViewTarget;
                var povPtr = viewTargetPtr + Offsets.FTViewTarget.POV;
                var worldOriginPtr = _gameWorld + Offsets.World.WorldOrigin;

                // World Origin (FVector)
                cameraManagerRound.AddEntry<double>(0, 11, worldOriginPtr);      // World Origin X
                cameraManagerRound.AddEntry<double>(0, 12, worldOriginPtr + 0x8);  // World Origin Y
                cameraManagerRound.AddEntry<double>(0, 13, worldOriginPtr + 0x10); // World Origin Z

                // FMinimalViewInfo structure
                // Location (0x0)
                cameraInfoRound.AddEntry<double>(0, 14, povPtr + 0x0);  // X
                cameraInfoRound.AddEntry<double>(0, 15, povPtr + 0x8);  // Y
                cameraInfoRound.AddEntry<double>(0, 16, povPtr + 0x10); // Z

                // Rotation (0x18)
                cameraInfoRound.AddEntry<double>(0, 17, povPtr + 0x18); // Pitch
                cameraInfoRound.AddEntry<double>(0, 18, povPtr + 0x20); // Yaw
                cameraInfoRound.AddEntry<double>(0, 19, povPtr + 0x28); // Roll

                // FOV (0x30)
                cameraInfoRound.AddEntry<float>(0, 20, povPtr + 0x30); // FOV

                cameraInfoScatterMap.Execute();

                if (cameraInfoScatterMap.Results[0][11].TryGetResult<double>(out var absoluteX) &&
                    cameraInfoScatterMap.Results[0][12].TryGetResult<double>(out var absoluteY) &&
                    cameraInfoScatterMap.Results[0][13].TryGetResult<double>(out var absoluteZ))
                {
                    _absoluteLocation = new Vector3D(absoluteX, absoluteY, absoluteZ);
                }
                else
                {
                    return false;
                }

                if (cameraInfoScatterMap.Results[0][14].TryGetResult<double>(out var x) &&
                    cameraInfoScatterMap.Results[0][15].TryGetResult<double>(out var y) &&
                    cameraInfoScatterMap.Results[0][16].TryGetResult<double>(out var z))
                {
                    _localUPlayer.Position = new Vector3D(
                        x + _absoluteLocation.X,
                        y + _absoluteLocation.Y,
                        z + _absoluteLocation.Z
                    );
                }
                else
                {
                    return false;
                }

                if (cameraInfoScatterMap.Results[0][17].TryGetResult<double>(out var rotX) &&
                    cameraInfoScatterMap.Results[0][18].TryGetResult<double>(out var rotY) &&
                    cameraInfoScatterMap.Results[0][19].TryGetResult<double>(out var rotZ))
                {
                    var rotation = new Vector3D(rotX, rotY, rotZ);
                    _localUPlayer.Rotation = new Vector2D(rotation.Y, rotation.X);
                    _localUPlayer.Rotation3D = rotation;
                }
                else
                {
                    return false;
                }

                if (cameraInfoScatterMap.Results[0][20].TryGetResult<float>(out var cameraFOV))
                {
                    _currentFOV = cameraFOV;
                }
                else
                {
                    _currentFOV = 90.0;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

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

            if (isInVehicle)
            {
                _isAimingDownSights = false;
                _hasPipScope = false;
                return true;
            }

            return UpdateOnFootPlayerInfo(scatterMap, pawnPtr);
        }

        private double ReadCameraFOV()
        {
            try
            {
                ulong cameraManagerPtr = Memory.ReadPtr(_playerController + Offsets.PlayerController.PlayerCameraManager);
                if (cameraManagerPtr == 0)
                    return 90.0;

                var viewTargetPtr = cameraManagerPtr + Offsets.PlayerCameraManager.ViewTarget;
                var povPtr = viewTargetPtr + Offsets.FTViewTarget.POV; 

                float cameraFOV = Memory.ReadValue<float>(povPtr + 0x30); // FOV
                return cameraFOV;
            }
            catch
            {
                return 90.0;
            }
        }

        /// <summary>
        /// Updates player info for on-foot scenarios.
        /// </summary>
        /// <param name="scatterMap">The scatter read map for batch memory reading</param>
        /// <param name="pawnPtr">Pointer to the pawn</param>
        /// <returns>True if successful</returns>
        private bool UpdateOnFootPlayerInfo(ScatterReadMap scatterMap, ulong pawnPtr)
        {
            ulong inventoryPtr = Memory.ReadPtr(pawnPtr + Offsets.ASQSoldier.InventoryComponent);
            if (inventoryPtr == 0)
            {
                _isAimingDownSights = false;
                _hasPipScope = false;
                return true;
            }

            var round1 = scatterMap.AddRound();
            var weaponPtrEntry = round1.AddEntry<ulong>(0, 0, inventoryPtr + Offsets.USQPawnInventoryComponent.CurrentWeapon);
            scatterMap.Execute();

            if (!scatterMap.Results[0][0].TryGetResult<ulong>(out ulong weaponPtr) || weaponPtr == 0)
            {
                _isAimingDownSights = false;
                _hasPipScope = false;
                return true;
            }

            return UpdateWeaponInfo(scatterMap, weaponPtr);
        }

        /// <summary>
        /// Updates weapon-specific information including ADS, scope, and FOV.
        /// </summary>
        /// <param name="scatterMap">The scatter read map</param>
        /// <param name="weaponPtr">Pointer to the current weapon</param>
        /// <returns>True if successful</returns>
        private bool UpdateWeaponInfo(ScatterReadMap scatterMap, ulong weaponPtr)
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
            float weaponFOV = scatterMap.Results[0][3].TryGetResult<float>(out float currFOV) && currFOV > 5f && currFOV < 180f ? currFOV : (float)_currentFOV;
            _isFiring = scatterMap.Results[0][4].TryGetResult<byte>(out byte firing) && firing == 1;

            double finalFOV = _currentFOV; // Default to camera FOV
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

            // Program.Log($"ADS: {_isAimingDownSights}, PipScope: {_hasPipScope}, Firing: {_isFiring}, WeaponPtr: 0x{_currentWeaponPtr:X}, FOV: {_currentFOV}, WeaponFOV: {weaponFOV}, CameraFOV: {_currentFOV}");

            return true;
        }

        /// <summary>
        /// Updates scope magnification and adjusts FOV accordingly.
        /// </summary>
        /// <param name="pipScopePtr">Pointer to the pip scope</param>
        /// <param name="weaponFOV">Base weapon FOV</param>
        /// <param name="fov">Reference to the FOV to adjust</param>
        private void UpdateScopeMagnification(ulong pipScopePtr, float weaponFOV, ref double fov)
        {
            // Directly read the CurrentMagnificationLevel using ReadValue
            int magnificationIdx = Memory.ReadValue<int>(pipScopePtr + Offsets.USQPipScopeCaptureComponent.CurrentMagnificationLevel); // Vérifier cet offset pour UE5

            // Validate and assign the magnification index
            _magnificationIndex = (magnificationIdx >= 0 && magnificationIdx < 3) ? magnificationIdx : 0;

            // Determine magnification factor based on index
            float magnification = _magnificationIndex switch
            {
                0 => Program.Config.FirstScopeMagnification,  // 1st scope Magnification
                1 => Program.Config.SecondScopeMagnification, // 2nd scope Magnification
                2 => Program.Config.ThirdScopeMagnification,  // 3rd scope Magnification
                _ => 1f                                       // Default (no magnification)
            };

            if (magnification > 1f)
            {
                fov = GetZoomedFOV(magnification, weaponFOV);
            }
        }

        /// <summary>
        /// Calculates the zoomed FOV based on magnification.
        /// </summary>
        /// <param name="magnificationDesired">Desired magnification level</param>
        /// <param name="defaultFOV">Default FOV</param>
        /// <returns>Adjusted FOV</returns>
        private double GetZoomedFOV(float magnificationDesired, float defaultFOV)
        {
            double defaultFOVRad = defaultFOV * 0.00872664626; // Conversion degrés -> radians (π / 360)
            double zoomedHalfFOVRad = Math.Atan(Math.Tan(defaultFOVRad) / magnificationDesired);
            return 2.0 * zoomedHalfFOVRad * 57.295779513; // Conversion radians -> degrés (180 / π)
        }

        /// <summary>
        /// Resets player state variables to their default values.
        /// </summary>
        private void ResetPlayerStateToDefault()
        {
            _isAimingDownSights = false;
            _hasPipScope = false;
            _isFiring = false;
            _currentFOV = 90.0;
            _currentWeaponPtr = 0;
        }

        private ulong ReadPawnPointer()
        {
            return Memory.ReadPtr(_playerController + Offsets.PlayerController.AcknowledgedPawn);
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
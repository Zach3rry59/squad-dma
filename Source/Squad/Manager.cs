using Offsets;
using squad_dma.Source.Squad.Debug;
using squad_dma.Source.Squad.Features;

namespace squad_dma.Source.Squad
{
    public class Manager : IDisposable
    {
        public readonly ulong _playerController;
        public readonly bool _inGame;
        private CancellationTokenSource _cancellationTokenSource;
        
        protected ulong _cachedPlayerState = 0;
        protected ulong _cachedSoldierActor = 0;
        protected ulong _cachedInventoryComponent = 0;
        protected ulong _cachedCurrentWeapon = 0;
        protected ulong _cachedCharacterMovement = 0;
        protected DateTime _lastPointerUpdate = DateTime.MinValue;
        
        // Modules
        private Suppression _suppression;
        private InteractionDistances _interactionDistances;
        private ShootingInMainBase _shootingInMainBase;
        private SpeedHack _speedHack;
        private Collision _collision;
        private AirStuck _airStuck;
        private HideActor _hideActor;
        private QuickZoom _quickZoom;
        private RapidFire _rapidFire;
        private InfiniteAmmo _infiniteAmmo;
        private QuickSwap _quickSwap;

        /// <summary>
        /// Constructor for feature classes that inherit from Manager
        /// </summary>
        /// <param name="playerController">The player controller address</param>
        /// <param name="inGame">Whether the game is currently active</param>
        public Manager(ulong playerController, bool inGame)
        {
            _playerController = playerController;
            _inGame = inGame;
            _cancellationTokenSource = null; // Features don't need this
            
            UpdateCachedPointers();
        }
        
        public Manager(ulong playerController, bool inGame, RegistredActors actors)
        {
            _playerController = playerController;
            _inGame = inGame;
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Initialize cached pointers
            UpdateCachedPointers();
            
            // Initialize all feature modules
            InitializeFeatures();
            
            // Start a timer to apply features
            StartFeatureTimer();
        }
        
        /// <summary>
        /// Updates cached pointers to avoid redundant memory reads
        /// </summary>
        protected void UpdateCachedPointers()
        {
            try
            {
                if ((DateTime.Now - _lastPointerUpdate).TotalMilliseconds < 500 && 
                    _cachedPlayerState != 0 && _cachedSoldierActor != 0)
                    return;
                    
                if (!_inGame || _playerController == 0) return;
                
                _cachedPlayerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                if (_cachedPlayerState == 0) return;
                
                _cachedSoldierActor = Memory.ReadPtr(_cachedPlayerState + ASQPlayerState.Soldier);
                if (_cachedSoldierActor == 0) return;
                
                _cachedInventoryComponent = Memory.ReadPtr(_cachedSoldierActor + ASQSoldier.InventoryComponent);
                if (_cachedInventoryComponent != 0)
                {
                    _cachedCurrentWeapon = Memory.ReadPtr(_cachedInventoryComponent + USQPawnInventoryComponent.CurrentWeapon);
                }
                
                _cachedCharacterMovement = Memory.ReadPtr(_cachedSoldierActor + Character.CharacterMovement);
                
                _lastPointerUpdate = DateTime.Now;
            }
            catch
            {
                // Reset pointers on error
                _cachedPlayerState = 0;
                _cachedSoldierActor = 0;
                _cachedInventoryComponent = 0;
                _cachedCurrentWeapon = 0;
                _cachedCharacterMovement = 0;
            }
        }
        
        private void InitializeFeatures()
        {
            _suppression = new Suppression(_playerController, _inGame);
            _interactionDistances = new InteractionDistances(_playerController, _inGame);
            _shootingInMainBase = new ShootingInMainBase(_playerController, _inGame);
            _speedHack = new SpeedHack(_playerController, _inGame);
            _collision = new Collision(_playerController, _inGame);
            _airStuck = new AirStuck(_playerController, _inGame, _collision);
            _hideActor = new HideActor(_playerController, _inGame);
            _quickZoom = new QuickZoom(_playerController, _inGame);
            _rapidFire = new RapidFire(_playerController, _inGame);
            _infiniteAmmo = new InfiniteAmmo(_playerController, _inGame);
            _quickSwap = new QuickSwap(_playerController, _inGame);
        }
        
        /// <summary>
        /// Checks if the local player is valid (has a valid player state and soldier actor)
        /// </summary>
        /// <returns>True if local player is valid, false otherwise</returns>
        public bool IsLocalPlayerValid()
        {
            try
            {
                if (!_inGame || _playerController == 0) return false;
                
                ulong playerState = _cachedPlayerState != 0 ? _cachedPlayerState : Memory.ReadPtr(_playerController + Controller.PlayerState);
                if (playerState == 0) return false;
                
                ulong soldierActor = _cachedSoldierActor != 0 ? _cachedSoldierActor : Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return false;
                
                return true;
            }
            catch
            { return false; }
        }
        
        // Start a timer to apply features every second
        // Simple fix for when the Localplayer respawns
        // Need to change it to a better solution
        private void StartFeatureTimer()
        {
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        UpdateCachedPointers();

                        if (_suppression._isSuppressionEnabled)
                            _suppression.Apply();
                        if (_interactionDistances._isInteractionDistancesEnabled)
                            _interactionDistances.Apply();
                        if (_shootingInMainBase._isShootingInMainBaseEnabled)
                            _shootingInMainBase.Apply();
                        if (_speedHack._isSpeedHackEnabled)
                            _speedHack.Apply();
                        if (_airStuck._isAirStuckEnabled)
                            _airStuck.Apply();
                        
                        // Handle DisableCollision, ensuring it's disabled if AirStuck is disabled
                        if (!_airStuck._isAirStuckEnabled && _collision.IsCollisionDisabled)
                        {
                            _collision.SetEnabled(false);
                        }
                        
                        if (_collision.IsCollisionDisabled)
                            _collision.Apply();
                        if (_hideActor._isHideActorEnabled)
                            _hideActor.Apply();
                        if (_rapidFire._isRapidFireEnabled)
                            _rapidFire.Apply();
                        if (_infiniteAmmo._isInfiniteAmmoEnabled)
                            _infiniteAmmo.Apply();
                        if (_quickSwap._isQuickSwapEnabled)
                            _quickSwap.Apply();
                    }
                    catch { /* Silently fail */ }
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);
        }
        
        /// <summary>
        /// Apply method to be implemented by features
        /// </summary>
        public virtual void Apply() { }
        
        #region Feature Control Methods
        
        public void SetSuppression(bool enable)
        {
            _suppression.SetEnabled(enable);
        }
        
        public void SetInteractionDistances(bool enable)
        {
            _interactionDistances.SetEnabled(enable);
        }
        
        public void SetShootingInMainBase(bool enable)
        {
            _shootingInMainBase.SetEnabled(enable);
        }
        
        public void SetSpeedHack(bool enable)
        {
            _speedHack.SetEnabled(enable);
        }
        
        public void SetAirStuck(bool enable)
        {
            _airStuck.SetEnabled(enable);
            
            // If AirStuck is disabled, also disable collision
            if (!enable && _collision.IsCollisionDisabled)
            {
                _collision.SetEnabled(false);
            }
        }
        
        public void SetQuickZoom(bool enable)
        {
            _quickZoom.SetEnabled(enable);
        }
        
        public void SetHideActor(bool enable)
        {
            _hideActor.SetEnabled(enable);
        }
        
        public void DisableCollision(bool disable)
        {
            // Only allow enabling if AirStuck is enabled
            if (disable && !_airStuck._isAirStuckEnabled)
            {
                return;
            }
            
            _collision.SetEnabled(disable);
        }
        
        public void SetRapidFire(bool enable)
        {
            _rapidFire.SetEnabled(enable);
        }
        
        public void SetInfiniteAmmo(bool enable)
        {
            _infiniteAmmo.SetEnabled(enable);
        }
        
        public void SetQuickSwap(bool enable)
        {
            _quickSwap.SetEnabled(enable);
        }
        
        public void Dispose()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
        }
        #endregion
    }
}
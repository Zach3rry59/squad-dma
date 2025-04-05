using Offsets;

namespace squad_dma.Source.Squad.Features
{
    public class LocalSoldier
    {
        private readonly ulong _playerController;
        private readonly bool _inGame;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isSuppressionEnabled = false;
        private bool _isInteractionDistancesEnabled = false;
        private bool _isShootingInMainBaseEnabled = false;
        private bool _isSpeedHackEnabled = false;
        private bool _isAirStuckEnabled = false;
        private bool _isHideActorEnabled = false;
        private bool _isQuickZoomEnabled = false;
        private bool _isRapidFireEnabled = false;
        private bool _isInfiniteAmmoEnabled = false;
        private bool _isQuickSwapEnabled = false;
        private bool _isCollisionDisabled = false;

        // Movement modes
        private enum EMovementMode : byte
        {
            MOVE_None = 0,
            MOVE_Walking = 1,
            MOVE_NavWalking = 2,
            MOVE_Falling = 3,
            MOVE_Swimming = 4,
            MOVE_Flying = 5,
            MOVE_Custom = 6,
            MOVE_MAX = 7
        }

        // Original Values to restore
        private float _originalUseInteractDistance;
        private float _originalInteractableRadiusMultiplier;
        private float _originalUnderSuppressionPercentage;
        private float _originalMaxSuppressionPercentage;
        private float _originalSuppressionMultiplier;
        private float _originalTimeBetweenShots = 0.0f;
        private float _originalTimeBetweenSingleShots = 0.0f;
        private float _originalFov;
        private float _originalTimeDilation = 0.0f;
        
        // InstantReload original values
        private byte _originalInfiniteAmmo = 0;
        private byte _originalInfiniteMags = 0;
        private byte _originalCreateProjectileOnServer = 0;

        // AirStuck original values
        private byte _originalMovementMode = 0;
        private byte _originalReplicatedMovementMode = 0;
        private byte _originalReplicateMovement = 0;
        private float _originalMaxFlySpeed = 0.0f;
        private float _originalMaxCustomMovementSpeed = 0.0f;
        private float _originalMaxAcceleration = 0.0f;
        
        // HideActor original values
        private byte _originalHideActorReplicateMovement = 0;
        private byte _originalHidden = 0;

        // DisableCollision original values
        private byte _originalCollisionEnabled = 0;

        // New values for fast weapon swap
        private float _originalEquipDuration = 0.0f;
        private float _originalUnequipDuration = 0.0f;
        private float _originalCachedEquipDuration = 0.0f;
        private float _originalCachedUnequipDuration = 0.0f;

        // ShootingInMainBase original values
        private bool _originalUsableInMainBase = false;

        public LocalSoldier(ulong playerController, bool inGame, RegistredActors actors)
        {
            _playerController = playerController;
            _inGame = inGame;
            _cancellationTokenSource = new CancellationTokenSource();
            StartFeatureTimer();
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
                        if (_isSuppressionEnabled)
                            ApplySuppression();
                        if (_isInteractionDistancesEnabled)
                            ApplyInteractionDistances();
                        if (_isShootingInMainBaseEnabled || _originalUsableInMainBase != false)
                            ApplyShootingInMainBase();
                        if (_isSpeedHackEnabled || _originalTimeDilation != 0.0f)
                            ApplySpeedHack();
                        if (_isAirStuckEnabled || _originalMovementMode != 0)
                            ApplyAirStuck();
                        
                        // Handle DisableCollision, ensuring it's disabled if AirStuck is disabled
                        if (!_isAirStuckEnabled && _isCollisionDisabled)
                        {
                            _isCollisionDisabled = false;
                        }
                        
                        if (_isCollisionDisabled || _originalCollisionEnabled != 0)
                            DisableCollision(_isCollisionDisabled);
                            
                        if (_isHideActorEnabled || _originalHideActorReplicateMovement != 0)
                            HideActor();
                        if (_isRapidFireEnabled || _originalTimeBetweenShots != 0.0f)
                            ApplyRapidFire();
                        if (_isInfiniteAmmoEnabled || _originalInfiniteAmmo != 0)
                            ApplyInfiniteAmmo();
                        if (_isQuickSwapEnabled || _originalEquipDuration != 0.0f)
                            ApplyQuickSwap();
                    }
                    catch { /* Silently fail */ }
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Checks if the local player is valid (has a valid player state and soldier actor)
        /// </summary>
        /// <returns>True if local player is valid, false otherwise</returns>
        private bool IsLocalPlayerValid()
        {
            if (!_inGame || _playerController == 0) return false;
            
            ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
            if (playerState == 0) return false;

            ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
            if (soldierActor == 0) return false;
            
            return true;
        }

        public void SetSuppression(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isSuppressionEnabled = enable;
            ApplySuppression();
        }

        private void ApplySuppression()
        {
            try
            {
                if (!IsLocalPlayerValid()) return;
                
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;

                if (_isSuppressionEnabled)
                {
                    // Store original values when first enabled
                    if (_originalUnderSuppressionPercentage == 0.0f)
                    {
                        _originalUnderSuppressionPercentage = Memory.ReadValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage);
                        _originalMaxSuppressionPercentage = Memory.ReadValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage);
                        _originalSuppressionMultiplier = Memory.ReadValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier);
                    }

                    // Apply suppression settings
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage, 0.0f);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage, 0.0f);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier, 0.0f);
                }
                else
                {
                    // Only restore if we have saved original values
                    if (_originalUnderSuppressionPercentage != 0.0f || _originalMaxSuppressionPercentage != 0.0f || _originalSuppressionMultiplier != 0.0f)
                {
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage, _originalUnderSuppressionPercentage);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage, _originalMaxSuppressionPercentage);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier, _originalSuppressionMultiplier);
                        
                        // Reset stored values
                        _originalUnderSuppressionPercentage = 0.0f;
                        _originalMaxSuppressionPercentage = 0.0f;
                        _originalSuppressionMultiplier = 0.0f;
                    }
                }
                Program.Log(Program.Config.DisableSuppression? "Suppression enabled" : "Suppression disabled");
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting suppression: {ex.Message}");
            }
        }

        public void SetInteractionDistances(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isInteractionDistancesEnabled = enable;
            ApplyInteractionDistances();
        }

        private void ApplyInteractionDistances()
        {
            try
            {
                if (!IsLocalPlayerValid()) return;
                
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;

                if (_isInteractionDistancesEnabled)
                {
                    // Store original values when first enabled
                    if (_originalUseInteractDistance == 0.0f)
                    {
                        _originalUseInteractDistance = Memory.ReadValue<float>(soldierActor + ASQSoldier.UseInteractDistance);
                        _originalInteractableRadiusMultiplier = Memory.ReadValue<float>(soldierActor + ASQSoldier.InteractableRadiusMultiplier);
                    }

                    // Apply interaction distances settings
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UseInteractDistance, 5000.0f);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.InteractableRadiusMultiplier, 70.0f);
                }
                else
                {
                    // Only restore if we have saved original values
                    if (_originalUseInteractDistance != 0.0f || _originalInteractableRadiusMultiplier != 0.0f)
                {
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UseInteractDistance, _originalUseInteractDistance);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.InteractableRadiusMultiplier, _originalInteractableRadiusMultiplier);
                        
                        // Reset stored values
                        _originalUseInteractDistance = 0.0f;
                        _originalInteractableRadiusMultiplier = 0.0f;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting interaction distances: {ex.Message}");
            }
        }

        public void SetShootingInMainBase(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isShootingInMainBaseEnabled = enable;
            ApplyShootingInMainBase();
        }

        private void ApplyShootingInMainBase()
        {
            try
            {
                // Don't attempt any memory operations if the feature is disabled
                // and we don't have original values to restore
                if (!_isShootingInMainBaseEnabled && !_originalUsableInMainBase)
                    return;
                
                if (!IsLocalPlayerValid()) return;
                
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;

                ulong inventoryComponent = Memory.ReadPtr(soldierActor + ASQSoldier.InventoryComponent);
                if (inventoryComponent == 0) return;

                ulong currentItemStaticInfo = Memory.ReadPtr(inventoryComponent + ASQSoldier.CurrentItemStaticInfo);
                if (currentItemStaticInfo == 0) return;

                if (_isShootingInMainBaseEnabled)
                {
                    // Store original value when first enabled
                    if (!_originalUsableInMainBase)
                    {
                        _originalUsableInMainBase = Memory.ReadValue<bool>(currentItemStaticInfo + ASQSoldier.bUsableInMainBase);
                    }

                    // Apply setting
                    Memory.WriteValue<bool>(currentItemStaticInfo + ASQSoldier.bUsableInMainBase, true);
                }
                else
                {
                    // Only restore if we have saved original value
                    if (_originalUsableInMainBase != false)
                    {
                        // Restore original value
                        Memory.WriteValue<bool>(currentItemStaticInfo + ASQSoldier.bUsableInMainBase, _originalUsableInMainBase);
                        
                        // Reset stored value
                        _originalUsableInMainBase = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting shooting in main base: {ex.Message}");
            }
        }

        public void SetSpeedHack(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isSpeedHackEnabled = enable;
            ApplySpeedHack();
        }

        private void ApplySpeedHack()
        {
            try
            {
                // Don't attempt any memory operations if the feature is disabled
                // and we don't have original values to restore
                if (!_isSpeedHackEnabled && _originalTimeDilation == 0.0f)
                    return;
                
                if (!IsLocalPlayerValid()) return;
                
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;

                if (_isSpeedHackEnabled)
                {
                    // Store original value when first enabled
                    if (_originalTimeDilation == 0.0f)
                    {
                        _originalTimeDilation = Memory.ReadValue<float>(soldierActor + Actor.CustomTimeDilation);
                    }

                    // Apply speed hack setting
                    Memory.WriteValue<float>(soldierActor + Actor.CustomTimeDilation, 4.0f);
                }
                else
                {
                    // Only restore if we have saved original value
                    if (_originalTimeDilation != 0.0f)
                    {
                        // Restore original value
                        Memory.WriteValue<float>(soldierActor + Actor.CustomTimeDilation, _originalTimeDilation);
                        
                        // Reset stored value
                        _originalTimeDilation = 0.0f;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting time dilation: {ex.Message}");
            }
        }

        public void SetAirStuck(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isAirStuckEnabled = enable;
            ApplyAirStuck();
        }

        // Die to fully reset
        private void ApplyAirStuck()
        {
            try
            {
                // Don't attempt any memory operations if the feature is disabled
                // and we don't have original values to restore
                if (!_isAirStuckEnabled && _originalMovementMode == 0)
                    return;
                
                if (!IsLocalPlayerValid()) return;
                
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;

                ulong characterMovement = Memory.ReadPtr(soldierActor + Character.CharacterMovement);
                if (characterMovement == 0) return;

                if (_isAirStuckEnabled)
                {
                    // Store original values when first enabled
                    if (_originalMovementMode == 0)
                    {
                        _originalMovementMode = Memory.ReadValue<byte>(characterMovement + CharacterMovementComponent.MovementMode);
                        _originalReplicatedMovementMode = Memory.ReadValue<byte>(characterMovement + Character.ReplicatedMovementMode);
                        _originalReplicateMovement = Memory.ReadValue<byte>(soldierActor + Actor.bReplicateMovement);
                        _originalMaxFlySpeed = Memory.ReadValue<float>(characterMovement + CharacterMovementComponent.MaxFlySpeed);
                        _originalMaxCustomMovementSpeed = Memory.ReadValue<float>(characterMovement + CharacterMovementComponent.MaxCustomMovementSpeed);
                        _originalMaxAcceleration = Memory.ReadValue<float>(characterMovement + CharacterMovementComponent.MaxAcceleration);
                        
                    }

                    // Apply air stuck settings
                    Memory.WriteValue<byte>(characterMovement + CharacterMovementComponent.MovementMode, (byte)EMovementMode.MOVE_Flying);
                    Memory.WriteValue<byte>(characterMovement + Character.ReplicatedMovementMode, (byte)EMovementMode.MOVE_Flying);
                    Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, 0);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxFlySpeed, 4000.0f);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxCustomMovementSpeed, 4000.0f);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxAcceleration, 4000.0f);
                }
                else
                {
                    // Only restore if we have saved original values
                    if (_originalMovementMode != 0)
                    {
                        // Restore original values
                        Memory.WriteValue<byte>(characterMovement + CharacterMovementComponent.MovementMode, _originalMovementMode);
                        Memory.WriteValue<byte>(characterMovement + Character.ReplicatedMovementMode, _originalReplicatedMovementMode);
                        Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, _originalReplicateMovement);
                        Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxFlySpeed, _originalMaxFlySpeed);
                        Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxCustomMovementSpeed, _originalMaxCustomMovementSpeed);
                        Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxAcceleration, _originalMaxAcceleration);
                                                
                        // Reset stored values
                        _originalMovementMode = 0;
                        _originalReplicatedMovementMode = 0;
                        _originalReplicateMovement = 0;
                        _originalMaxFlySpeed = 0.0f;
                        _originalMaxCustomMovementSpeed = 0.0f;
                        _originalMaxAcceleration = 0.0f;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting air stuck: {ex.Message}");
            }
        }

        public void SetQuickZoom(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isQuickZoomEnabled = enable;
            ApplyQuickZoom();
        }

        public void ApplyQuickZoom()
        {
            try
            {
                if (!IsLocalPlayerValid()) return;
                
                ulong cameraManager = Memory.ReadPtr(_playerController + PlayerController.PlayerCameraManager);
                if (cameraManager == 0) return;
                
                if (_isQuickZoomEnabled)
                {
                    _originalFov = Memory.ReadValue<float>(cameraManager + PlayerCameraManager.DefaultFOV);
                    Memory.WriteValue<float>(cameraManager + PlayerCameraManager.DefaultFOV, 20.0f);
                }
                else
                {
                    Memory.WriteValue<float>(cameraManager + PlayerCameraManager.DefaultFOV, _originalFov);
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting Quick Zoom: {ex.Message}");
            }
        }
        public void SetHideActor(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isHideActorEnabled = enable;
            HideActor();
        }

        private void HideActor()
        {
            try
            {
                if (!_isHideActorEnabled && _originalHideActorReplicateMovement == 0)
                    return;
                
                if (!IsLocalPlayerValid()) return;
                
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;

                if (_isHideActorEnabled)
                {
                    // Store original values when first enabled
                    if (_originalHideActorReplicateMovement == 0)
                    {
                        _originalHideActorReplicateMovement = Memory.ReadValue<byte>(soldierActor + Actor.bReplicateMovement);
                        _originalHidden = Memory.ReadValue<byte>(soldierActor + Actor.bHidden);
                    }

                    // Apply hide actor settings
                    Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, 0);
                    Memory.WriteValue<byte>(soldierActor + Actor.bHidden, 1);
                }
                else
                {
                    // Only restore if we have saved original values
                    if (_originalHideActorReplicateMovement != 0)
                    {
                        // Restore original values
                        Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, _originalHideActorReplicateMovement);
                        Memory.WriteValue<byte>(soldierActor + Actor.bHidden, _originalHidden);
                        
                        // Reset stored values
                        _originalHideActorReplicateMovement = 0;
                        _originalHidden = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting hide actor: {ex.Message}");
            }
        }

        /// <summary>
        /// Enables or disables collision for the soldier actor
        /// </summary>
        /// <param name="disable">True to disable collision, false to restore normal collision</param>
        public void DisableCollision(bool disable)
        {
            if (!IsLocalPlayerValid()) return;
            
            // Only allow enabling if AirStuck is enabled
            if (disable && !_isAirStuckEnabled)
            {
                // If AirStuck is disabled, ensure DisableCollision is also disabled
                _isCollisionDisabled = false;
                return;
            }
            
            _isCollisionDisabled = disable;
            
            try
            {
                if (!IsLocalPlayerValid()) return;
                
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;

                ulong rootComponent = Memory.ReadPtr(soldierActor + Actor.RootComponent);
                if (rootComponent == 0) return;

                // Access the FBodyInstance struct which is at offset 0x2c8 in UPrimitiveComponent
                // CollisionEnabled is at offset 0x20 in FBodyInstance
                ulong bodyInstanceAddr = rootComponent + 0x2c8;
                
                if (disable)
                {
                    // Store original value if we haven't already
                    if (_originalCollisionEnabled == 0)
                    {
                        _originalCollisionEnabled = Memory.ReadValue<byte>(bodyInstanceAddr + 0x20);
                    }
                    
                    // Set to NoCollision (0)
                    Memory.WriteValue<byte>(bodyInstanceAddr + 0x20, 0);
                }
                else if (_originalCollisionEnabled != 0)
                {
                    // Restore original value
                    Memory.WriteValue<byte>(bodyInstanceAddr + 0x20, _originalCollisionEnabled);
                    _originalCollisionEnabled = 0;
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error {(disable ? "disabling" : "enabling")} collision: {ex.Message}");
            }
        }

        public void SetRapidFire(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isRapidFireEnabled = enable;
            ApplyRapidFire();
        }

        private void ApplyRapidFire()
        {
            try
            {
                // Don't attempt any memory operations if the feature is disabled
                // and we don't have original values to restore
                if (!_isRapidFireEnabled && _originalTimeBetweenShots == 0.0f)
                    return;
                
                if (!IsLocalPlayerValid()) return;
                
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;

                ulong inventoryComponent = Memory.ReadPtr(soldierActor + ASQSoldier.InventoryComponent);
                if (inventoryComponent == 0) return;

                ulong currentWeapon = Memory.ReadPtr(inventoryComponent + USQPawnInventoryComponent.CurrentWeapon);
                if (currentWeapon == 0) return;
                
                ulong weaponConfigOffset = currentWeapon + ASQWeapon.WeaponConfig;

                if (_isRapidFireEnabled)
                {
                    // Store original values when first enabled
                    if (_originalTimeBetweenShots == 0.0f)
                    {
                        _originalTimeBetweenShots = Memory.ReadValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenShots);
                        _originalTimeBetweenSingleShots = Memory.ReadValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenSingleShots);
                    }

                    // Apply rapid fire settings
                    Memory.WriteValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenShots, 0.01f);
                    Memory.WriteValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenSingleShots, 0.01f);
                }
                else
                {
                    // Only restore if we have saved original values
                    if (_originalTimeBetweenShots != 0.0f)
                    {
                        // Restore original values
                        Memory.WriteValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenShots, _originalTimeBetweenShots);
                        Memory.WriteValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenSingleShots, _originalTimeBetweenSingleShots);
                        
                        // Reset stored values
                        _originalTimeBetweenShots = 0.0f;
                        _originalTimeBetweenSingleShots = 0.0f;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting rapid fire: {ex.Message}");
            }
        }

        public void SetInfiniteAmmo(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isInfiniteAmmoEnabled = enable;
            ApplyInfiniteAmmo();
        }

        private void ApplyInfiniteAmmo()
        {
            try
            {
                // Don't attempt any memory operations if the feature is disabled
                // and we don't have original values to restore
                if (!_isInfiniteAmmoEnabled && _originalInfiniteAmmo == 0)
                    return;
                
                if (!IsLocalPlayerValid()) return;
                
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;

                ulong inventoryComponent = Memory.ReadPtr(soldierActor + ASQSoldier.InventoryComponent);
                if (inventoryComponent == 0) return;

                ulong currentWeapon = Memory.ReadPtr(inventoryComponent + USQPawnInventoryComponent.CurrentWeapon);
                if (currentWeapon == 0) return;
                
                ulong weaponConfigOffset = currentWeapon + ASQWeapon.WeaponConfig;

                if (_isInfiniteAmmoEnabled)
                {
                    // Store original values when first enabled
                    if (_originalInfiniteAmmo == 0)
                    {
                        _originalInfiniteAmmo = Memory.ReadValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteAmmo);
                        _originalInfiniteMags = Memory.ReadValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteMags);
                        _originalCreateProjectileOnServer = Memory.ReadValue<byte>(weaponConfigOffset + FSQWeaponData.bCreateProjectileOnServer);
                    }

                    // Apply infinite ammo settings
                    Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteAmmo, 1);
                    Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteMags, 1);
                    Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bCreateProjectileOnServer, 1);
                }
                else
                {
                    // Only restore if we have saved original values
                    if (_originalInfiniteAmmo != 0)
                    {
                        // Restore original values
                        Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteAmmo, _originalInfiniteAmmo);
                        Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteMags, _originalInfiniteMags);
                        Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bCreateProjectileOnServer, _originalCreateProjectileOnServer);
                                                
                        // Reset stored values
                        _originalInfiniteAmmo = 0;
                        _originalInfiniteMags = 0;
                        _originalCreateProjectileOnServer = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting infinite ammo: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets whether quick weapon swapping is enabled
        /// </summary>
        /// <param name="enable">Whether to enable quick weapon swapping</param>
        public void SetQuickSwap(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isQuickSwapEnabled = enable;
            ApplyQuickSwap();
        }

        /// <summary>
        /// Applies or removes quick weapon swapping
        /// </summary>
        private void ApplyQuickSwap()
        {
            try
            {
                // Don't attempt any memory operations if the feature is disabled
                // and we don't have original values to restore
                if (!_isQuickSwapEnabled && _originalEquipDuration == 0.0f)
                    return;
                
                if (!IsLocalPlayerValid()) return;
                
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;
                
                ulong inventoryComponent = Memory.ReadPtr(soldierActor + ASQSoldier.InventoryComponent);
                if (inventoryComponent == 0) return;
                
                ulong currentWeapon = Memory.ReadPtr(inventoryComponent + USQPawnInventoryComponent.CurrentWeapon);
                if (currentWeapon == 0) return;
                
                if (_isQuickSwapEnabled)
                {
                    // Store original values when first enabled
                    if (_originalEquipDuration == 0.0f)
                    {
                        _originalEquipDuration = Memory.ReadValue<float>(currentWeapon + ASQEquipableItem.EquipDuration);
                        _originalUnequipDuration = Memory.ReadValue<float>(currentWeapon + ASQEquipableItem.UnequipDuration);
                        _originalCachedEquipDuration = Memory.ReadValue<float>(currentWeapon + ASQEquipableItem.CachedEquipDuration);
                        _originalCachedUnequipDuration = Memory.ReadValue<float>(currentWeapon + ASQEquipableItem.CachedUnequipDuration);
                    }
                    
                    const float FAST_SWAP_VALUE = 0.01f;
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.EquipDuration, FAST_SWAP_VALUE);
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.UnequipDuration, FAST_SWAP_VALUE);
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.CachedEquipDuration, FAST_SWAP_VALUE);
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.CachedUnequipDuration, FAST_SWAP_VALUE);
                }
                else
                {
                    // Only restore if we have saved original values
                    if (_originalEquipDuration != 0.0f)
                    {
                        // Restore original values
                        Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.EquipDuration, _originalEquipDuration);
                        Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.UnequipDuration, _originalUnequipDuration);
                        Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.CachedEquipDuration, _originalCachedEquipDuration);
                        Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.CachedUnequipDuration, _originalCachedUnequipDuration);
                        
                        // Reset stored values
                        _originalEquipDuration = 0.0f;
                        _originalUnequipDuration = 0.0f;
                        _originalCachedEquipDuration = 0.0f;
                        _originalCachedUnequipDuration = 0.0f;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting quick swap: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
        
        /// <summary>
        /// Reads and logs just the essential weapon name information
        /// </summary>
        /// <param name="soldierActor">The soldier actor address</param>
        /// <param name="label">Label for logging</param>
        private void ReadWeaponInfo(ulong soldierActor, string label)
        {
            try
            {
                // Get inventory component
                ulong inventoryComponent = Memory.ReadPtr(soldierActor + ASQSoldier.InventoryComponent);
                if (inventoryComponent == 0) return;
                
                // Get current weapon
                ulong currentWeapon = Memory.ReadPtr(inventoryComponent + USQPawnInventoryComponent.CurrentWeapon);
                if (currentWeapon == 0) return;
                
                Program.Log($"{label} Weapon:");
                
                // Get weapon object name 
                try
                {
                    int nameIndex = Memory.ReadValue<int>(currentWeapon + 0x18);
                    Dictionary<uint, string> names = Memory.GetNamesById(new List<uint> { (uint)nameIndex });
                    
                    if (names.ContainsKey((uint)nameIndex))
                    {
                        string weaponName = names[(uint)nameIndex];
                        Program.Log($"  - Object: {weaponName}");
                    }
                }
                catch { /* Silently fail */ }
                
                // Get static info name
                try
                {
                    ulong itemStaticInfo = Memory.ReadPtr(currentWeapon + ASQEquipableItem.ItemStaticInfo);
                    
                    if (itemStaticInfo != 0)
                    {
                        int staticInfoNameIndex = Memory.ReadValue<int>(itemStaticInfo + 0x18);
                        Dictionary<uint, string> names = Memory.GetNamesById(new List<uint> { (uint)staticInfoNameIndex });
                        
                        if (names.ContainsKey((uint)staticInfoNameIndex))
                        {
                            string infoName = names[(uint)staticInfoNameIndex];
                            Program.Log($"  - Static: {infoName}");
                        }
                    }
                }
                catch { /* Silently fail */ }
            }
            catch { /* Silently fail */ }
        }
        
        /// <summary>
        /// Reads and logs the current weapon of the local player and optionally other players
        /// </summary>
        /// <param name="includeOtherPlayers">Whether to include other players' weapons in the log</param>
        public void ReadCurrentWeapons(bool includeOtherPlayers = false)
        {
            try
            {
                if (!IsLocalPlayerValid()) return;
                
                Program.Log("=== READING CURRENT WEAPONS ===");
                
                // Get local player's weapon
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;
                
                ReadWeaponInfo(soldierActor, "Local Player");
                
                // If requested, try to find a few other players by team
                if (includeOtherPlayers)
                {
                    // This is a simplified approach that doesn't rely on traversing the player array
                    int localTeamId = Memory.ReadValue<int>(playerState + ASQPlayerState.TeamID);
                    Program.Log($"Local player is on team: {localTeamId}");
                }
                
                Program.Log("=============================");
            }
            catch { /* Silently fail */ }
        }
    }
} 
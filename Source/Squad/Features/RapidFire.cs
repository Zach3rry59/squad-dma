using Offsets;

namespace squad_dma.Source.Squad.Features
{
    public class RapidFire : Manager
    {
        public bool _isRapidFireEnabled = false;
        
        // Original values for soldier weapon
        private float _soldierOriginalTimeBetweenShots = 0.0f;
        private float _soldierOriginalTimeBetweenSingleShots = 0.0f;
        
        // Original values for vehicle weapon
        private float _vehicleOriginalTimeBetweenShots = 0.0f;
        private float _vehicleOriginalTimeBetweenSingleShots = 0.0f;
        
        public RapidFire(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isRapidFireEnabled = enable;
            Apply();
        }
        
        public override void Apply()
        {
            try
            {
                if (!IsLocalPlayerValid()) return;
                
                // First try to apply to soldier weapon
                UpdateCachedPointers();
                ulong currentWeapon = _cachedCurrentWeapon;
                if (currentWeapon != 0)
                {
                    Apply(currentWeapon, ref _soldierOriginalTimeBetweenShots, ref _soldierOriginalTimeBetweenSingleShots);
                }

                // Then check if player is in a vehicle and apply to vehicle weapon
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                if (playerState != 0)
                {
                    ulong currentSeat = Memory.ReadPtr(playerState + ASQPlayerState.CurrentSeat);
                    if (currentSeat != 0)
                    {
                        ulong seatPawn = Memory.ReadPtr(currentSeat + USQVehicleSeatComponent.SeatPawn);
                        if (seatPawn != 0)
                        {
                            ulong vehicleInventory = Memory.ReadPtr(seatPawn + ASQVehicleSeat.VehicleInventory);
                            if (vehicleInventory != 0)
                            {
                                ulong vehicleWeapon = Memory.ReadPtr(vehicleInventory + USQPawnInventoryComponent.CurrentWeapon);
                                if (vehicleWeapon != 0)
                                {
                                    Apply(vehicleWeapon, ref _vehicleOriginalTimeBetweenShots, ref _vehicleOriginalTimeBetweenSingleShots);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting rapid fire: {ex.Message}");
            }
        }

        private void Apply(ulong weapon, ref float originalTimeBetweenShots, ref float originalTimeBetweenSingleShots)
        {
            ulong weaponConfigOffset = weapon + ASQWeapon.WeaponConfig;

            if (_isRapidFireEnabled)
            {
                // Store original values when first enabled
                if (originalTimeBetweenShots == 0.0f)
                {
                    originalTimeBetweenShots = Memory.ReadValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenShots);
                    originalTimeBetweenSingleShots = Memory.ReadValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenSingleShots);
                }

                // Apply rapid fire settings
                Memory.WriteValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenShots, 0.01f);
                Memory.WriteValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenSingleShots, 0.01f);
            }
            else
            {
                // Only restore if we have saved original values
                if (originalTimeBetweenShots != 0.0f)
                {
                    // Restore original values
                    Memory.WriteValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenShots, originalTimeBetweenShots);
                    Memory.WriteValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenSingleShots, originalTimeBetweenSingleShots);
                    
                    // Reset stored values
                    originalTimeBetweenShots = 0.0f;
                    originalTimeBetweenSingleShots = 0.0f;
                }
            }
        }
    }
} 
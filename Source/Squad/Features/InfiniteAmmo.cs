using Offsets;

namespace squad_dma.Source.Squad.Features
{
    public class InfiniteAmmo : Manager
    {
        public bool _isInfiniteAmmoEnabled = false;
        
        public InfiniteAmmo(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isInfiniteAmmoEnabled = enable;
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
                    Apply(currentWeapon);
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
                                    Apply(vehicleWeapon);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting infinite ammo: {ex.Message}");
            }
        }

        private void Apply(ulong weapon)
        {
            ulong weaponConfigOffset = weapon + ASQWeapon.WeaponConfig;

            if (_isInfiniteAmmoEnabled)
            {
                Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteAmmo, 1);
                Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteMags, 1);
            }
            else
            {
                Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteAmmo, 0);
                Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteMags, 0);
            }
        }
    }
} 
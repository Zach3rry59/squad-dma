using Offsets;

namespace squad_dma.Source.Squad.Features
{
    public class ForceFullAuto : Manager
    {
        public bool _isForceFullAutoEnabled = false;
        
        private int[] _originalFireModes = null;
        private bool _originalManualBolt = false;
        
        public ForceFullAuto(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isForceFullAutoEnabled = enable;
            Apply();
        }

        // FireModes :
        // 1 = Single Fire
        // 3 = Burst
        // -1 = Full Auto
        
        public override void Apply()
        {
            try
            {
                if (!_isForceFullAutoEnabled && _originalFireModes == null)
                    return;
                
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
                Program.Log($"Error setting force full auto: {ex.Message}");
            }
        }

        private void Apply(ulong weapon)
        {
            ulong weaponConfig = weapon + ASQWeapon.WeaponConfig;
            ulong fireModesArray = weaponConfig + FSQWeaponData.FireModes;
            ulong fireModesData = Memory.ReadPtr(fireModesArray);
            if (fireModesData == 0) return;
            
            int fireModeCount = Memory.ReadValue<int>(fireModesArray + 0x8);
            if (fireModeCount <= 0) return;
            
            ulong itemStaticInfo = Memory.ReadPtr(weapon + ASQEquipableItem.ItemStaticInfo);
            if (itemStaticInfo == 0) return;

            if (_isForceFullAutoEnabled)
            {
                if (_originalFireModes == null)
                {
                    _originalFireModes = new int[fireModeCount];
                    for (int i = 0; i < fireModeCount; i++)
                    {
                        ulong fireModeAddress = fireModesData + ((ulong)i * 4);
                        _originalFireModes[i] = Memory.ReadValue<int>(fireModeAddress);
                    }
                    _originalManualBolt = Memory.ReadValue<bool>(itemStaticInfo + USQWeaponStaticInfo.bRequiresManualBolt);
                }

                for (int i = 0; i < fireModeCount; i++)
                {
                    ulong fireModeAddress = fireModesData + ((ulong)i * 4);
                    Memory.WriteValue(fireModeAddress, -1);
                }
                
                Memory.WriteValue(weapon + ASQWeapon.CurrentFireMode, 0);
                
                Memory.WriteValue(itemStaticInfo + USQWeaponStaticInfo.bRequiresManualBolt, false);
            }
            else
            {
                // Restore original values or use defaults if not found
                if (_originalFireModes != null)
                {
                    for (int i = 0; i < fireModeCount && i < _originalFireModes.Length; i++)
                    {
                        ulong fireModeAddress = fireModesData + ((ulong)i * 4);
                        Memory.WriteValue(fireModeAddress, _originalFireModes[i]);
                    }
                    
                    Memory.WriteValue(itemStaticInfo + USQWeaponStaticInfo.bRequiresManualBolt, _originalManualBolt);
                }
                else
                {
                    for (int i = 0; i < fireModeCount; i++)
                    {
                        ulong fireModeAddress = fireModesData + ((ulong)i * 4);
                        Memory.WriteValue(fireModeAddress, 1);
                    }
                    Memory.WriteValue(itemStaticInfo + USQWeaponStaticInfo.bRequiresManualBolt, true); 
                }
                
                _originalFireModes = null;
                _originalManualBolt = false;
            }
        }
    }
} 
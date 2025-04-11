using Offsets;

namespace squad_dma.Source.Squad.Features
{
    public class RapidFire : Manager
    {
        public bool _isRapidFireEnabled = false;
        
        // Original Values to restore
        private float _originalTimeBetweenShots = 0.0f;
        private float _originalTimeBetweenSingleShots = 0.0f;
        
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
                // Don't attempt any memory operations if the feature is disabled
                // and we don't have original values to restore
                if (!_isRapidFireEnabled && _originalTimeBetweenShots == 0.0f)
                    return;
                
                if (!IsLocalPlayerValid()) return;
                
                UpdateCachedPointers();
                ulong currentWeapon = _cachedCurrentWeapon;
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
    }
} 
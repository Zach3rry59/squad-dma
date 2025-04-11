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
                
                UpdateCachedPointers();
                ulong currentWeapon = _cachedCurrentWeapon;
                if (currentWeapon == 0) return;
                
                ulong weaponConfigOffset = currentWeapon + ASQWeapon.WeaponConfig;

                if (_isInfiniteAmmoEnabled)
                {
                    Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteAmmo, 1);
                    Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteMags, 1);
                    Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bCreateProjectileOnServer, 1);
                }
                else
                {
                    Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteAmmo, 0);
                    Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteMags, 0);
                    Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bCreateProjectileOnServer, 0);
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting infinite ammo: {ex.Message}");
            }
        }
    }
} 
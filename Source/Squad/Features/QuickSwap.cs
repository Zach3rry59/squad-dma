using Offsets;

namespace squad_dma.Source.Squad.Features
{
    public class QuickSwap : Manager
    {
        public bool _isQuickSwapEnabled = false;
        
        public QuickSwap(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isQuickSwapEnabled = enable;
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
                
                if (_isQuickSwapEnabled)
                {
                    const float FAST_SWAP_VALUE = 0.01f;
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.EquipDuration, FAST_SWAP_VALUE);
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.UnequipDuration, FAST_SWAP_VALUE);
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.CachedEquipDuration, FAST_SWAP_VALUE);
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.CachedUnequipDuration, FAST_SWAP_VALUE);
                }
                else
                {
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.EquipDuration, 1.2f);
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.UnequipDuration, 1.067f);
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.CachedEquipDuration, 1.2f);
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.CachedUnequipDuration, 1.067f);
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting quick swap: {ex.Message}");
            }
        }
    }
} 
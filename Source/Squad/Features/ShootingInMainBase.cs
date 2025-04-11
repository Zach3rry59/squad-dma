using Offsets;

namespace squad_dma.Source.Squad.Features
{
    public class ShootingInMainBase : Manager
    {
        public bool _isShootingInMainBaseEnabled = false;
        
        public ShootingInMainBase(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isShootingInMainBaseEnabled = enable;
            Apply();
        }
        
        public override void Apply()
        {
            try
            {
                if (!IsLocalPlayerValid()) return;
                
                UpdateCachedPointers();
                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0) return;

                ulong inventoryComponent = _cachedInventoryComponent;
                if (inventoryComponent == 0) return;

                ulong currentItemStaticInfo = Memory.ReadPtr(inventoryComponent + ASQSoldier.CurrentItemStaticInfo);
                if (currentItemStaticInfo == 0) return;

                if (_isShootingInMainBaseEnabled)
                {
                    Memory.WriteValue<bool>(currentItemStaticInfo + ASQSoldier.bUsableInMainBase, true);
                }
                else
                {
                    Memory.WriteValue<bool>(currentItemStaticInfo + ASQSoldier.bUsableInMainBase, false);
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting shooting in main base: {ex.Message}");
            }
        }
    }
} 
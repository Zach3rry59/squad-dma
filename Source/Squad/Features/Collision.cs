using Offsets;
using System.Security.AccessControl;

namespace squad_dma.Source.Squad.Features
{
    public class Collision : Manager
    {
        private new bool _isCollisionDisabled = false;
        
        public Collision(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        public bool IsCollisionDisabled => _isCollisionDisabled;

        private enum ECollisionEnabled : byte
        {
            NoCollision = 0, 
            QueryOnly = 1, 
            PhysicsOnly = 2, 
            QueryAndPhysics = 3, 
            ECollisionEnabled_MAX = 4
        };

        public void SetEnabled(bool disable)
        {
            if (!IsLocalPlayerValid()) return;
            _isCollisionDisabled = disable;
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

                ulong rootComponent = Memory.ReadPtr(soldierActor + Actor.RootComponent);
                if (rootComponent == 0) return;

                ulong bodyInstanceAddr = rootComponent + UPrimitiveComponent.BodyInstance;
                
                if (_isCollisionDisabled)
                {                  
                    // Set to NoCollision (0)
                    Memory.WriteValue<byte>(bodyInstanceAddr + FBodyInstance.CollisionEnabled, 0);
                }
                else
                {
                    // Set to QueryOnly (1)
                    Memory.WriteValue<byte>(bodyInstanceAddr + FBodyInstance.CollisionEnabled, 1);
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error {(_isCollisionDisabled ? "disabling" : "enabling")} collision: {ex.Message}");
            }
        }
    }
} 
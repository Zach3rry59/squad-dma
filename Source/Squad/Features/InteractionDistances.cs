using Offsets;

namespace squad_dma.Source.Squad.Features
{
    public class InteractionDistances : Manager
    {
        public bool _isInteractionDistancesEnabled = false;
        
        public InteractionDistances(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isInteractionDistancesEnabled = enable;
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

                if (_isInteractionDistancesEnabled)
                {
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UseInteractDistance, 5000.0f);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.InteractableRadiusMultiplier, 70.0f);
                }
                else
                {
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UseInteractDistance, 220);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.InteractableRadiusMultiplier, 1.2f);
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting interaction distances: {ex.Message}");
            }
        }
    }
} 
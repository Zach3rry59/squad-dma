using Offsets;

namespace squad_dma.Source.Squad.Features
{
    public class Suppression : Manager
    {
        public bool _isSuppressionEnabled = false;
        
        public Suppression(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
                
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isSuppressionEnabled = enable;
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

                if (_isSuppressionEnabled)
                {
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage, 0.0f);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage, 0.0f);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier, 0.0f);

                    Memory.WriteValue(soldierActor + ASQSoldier.bIsCameraRecoilActive, false);
                }
                else
                {
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage, 0);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage, -1);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier, 1);

                    Memory.WriteValue(soldierActor + ASQSoldier.bIsCameraRecoilActive, true);
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting suppression: {ex.Message}");
            }
        }
    }
} 
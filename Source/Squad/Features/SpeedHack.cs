using Offsets;

namespace squad_dma.Source.Squad.Features
{
    public class SpeedHack : Manager
    {
        public bool _isSpeedHackEnabled = false;
        
        public SpeedHack(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isSpeedHackEnabled = enable;
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

                if (_isSpeedHackEnabled)
                {
                    Memory.WriteValue<float>(soldierActor + Actor.CustomTimeDilation, 4.0f);
                }
                else
                {
                    Memory.WriteValue<float>(soldierActor + Actor.CustomTimeDilation, 1);
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting time dilation: {ex.Message}");
            }
        }
    }
} 
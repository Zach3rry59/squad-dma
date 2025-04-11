using Offsets;

namespace squad_dma.Source.Squad.Features
{
    public class AirStuck : Manager
    {
        public bool _isAirStuckEnabled = false;
        private Collision _collision;
        
        public AirStuck(ulong playerController, bool inGame, Collision collisionMod)
            : base(playerController, inGame)
        {
            _collision = collisionMod;
        }

        private enum EMovementMode : byte
        {
            MOVE_None = 0,
            MOVE_Walking = 1,
            MOVE_NavWalking = 2,
            MOVE_Falling = 3,
            MOVE_Swimming = 4,
            MOVE_Flying = 5,
            MOVE_Custom = 6,
            MOVE_MAX = 7
        }

        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isAirStuckEnabled = enable;
            
            // Ensure collision is disabled when AirStuck is disabled
            if (!_isAirStuckEnabled && _collision.IsCollisionDisabled)
            {
                _collision.SetEnabled(false);
            }
            
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

                ulong characterMovement = _cachedCharacterMovement;
                if (characterMovement == 0) return;

                if (_isAirStuckEnabled)
                {
                    Memory.WriteValue<byte>(characterMovement + CharacterMovementComponent.MovementMode, (byte)EMovementMode.MOVE_Flying);
                    Memory.WriteValue<byte>(characterMovement + Character.ReplicatedMovementMode, (byte)EMovementMode.MOVE_Flying);
                    Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, 0);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxFlySpeed, 2000.0f);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxCustomMovementSpeed, 2000.0f);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxAcceleration, 2000.0f);
                }
                else
                {
                    Memory.WriteValue<byte>(characterMovement + CharacterMovementComponent.MovementMode, (byte)EMovementMode.MOVE_Walking);
                    Memory.WriteValue<byte>(characterMovement + Character.ReplicatedMovementMode, (byte)EMovementMode.MOVE_Walking);
                    Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, 16);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxFlySpeed, 200);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxCustomMovementSpeed, 600);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxAcceleration, 500);
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting air stuck: {ex.Message}");
            }
        }
    }
} 
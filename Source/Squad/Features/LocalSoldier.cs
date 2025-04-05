using Offsets;

namespace squad_dma.Source.Squad.Features
{
    public class LocalSoldier
    {
        private readonly ulong _playerController;
        private readonly bool _inGame;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isSuppressionEnabled = false;
        private bool _isInteractionDistancesEnabled = false;
        private bool _isShootingInMainBaseEnabled = false;
        private bool _isSpeedHackEnabled = false;
        private bool _isAirStuckEnabled = false;
        private bool _isHideActorEnabled = false;

        // Movement modes
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

        // Original Values to restore
        private float _originalUseInteractDistance;
        private float _originalInteractableRadiusMultiplier;
        private float _originalUnderSuppressionPercentage;
        private float _originalMaxSuppressionPercentage;
        private float _originalSuppressionMultiplier;

        public LocalSoldier(ulong playerController, bool inGame, RegistredActors actors)
        {
            _playerController = playerController;
            _inGame = inGame;
            _cancellationTokenSource = new CancellationTokenSource();
            StartFeatureTimer();
        }

        // Start a timer to apply features every second
        // Simple fix for when the Localplayer respawns
        // Need to change it to a better solution
        private void StartFeatureTimer()
        {
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (_isSuppressionEnabled)
                            ApplySuppression();
                        if (_isInteractionDistancesEnabled)
                            ApplyInteractionDistances();
                        if (_isShootingInMainBaseEnabled)
                            ApplyShootingInMainBase();
                        if (_isSpeedHackEnabled)
                            ApplySpeedHack();
                        if (_isAirStuckEnabled)
                            ApplyAirStuck();
                        if (_isHideActorEnabled)
                            HideActor();
                    }
                    catch { /* Silently fail */ }
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);
        }

        public void SetSuppression(bool enable)
        {
            if (!_inGame || _playerController == 0) return;
            _isSuppressionEnabled = enable;
            ApplySuppression();
        }

        private void ApplySuppression()
        {
            try
            {
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                if (playerState == 0) return;

                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;

                if (_isSuppressionEnabled)
                {
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage, 0.0f);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage, 0.0f);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier, 0.0f);
                }
                else
                {
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage, _originalUnderSuppressionPercentage);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage, _originalMaxSuppressionPercentage);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier, _originalSuppressionMultiplier);
                }
            }
            catch { /* Silently fail */ }
        }

        public void SetInteractionDistances(bool enable)
        {
            if (!_inGame || _playerController == 0) return;
            _isInteractionDistancesEnabled = enable;
            ApplyInteractionDistances();
        }

        private void ApplyInteractionDistances()
        {
            try
            {
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                if (playerState == 0) return;

                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;

                if (_isInteractionDistancesEnabled)
                {
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UseInteractDistance, 5000.0f);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.InteractableRadiusMultiplier, 70.0f);
                }
                else
                {
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UseInteractDistance, _originalUseInteractDistance);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.InteractableRadiusMultiplier, _originalInteractableRadiusMultiplier);
                }
            }
            catch { /* Silently fail */ }
        }

        public void SetShootingInMainBase(bool enable)
        {
            if (!_inGame || _playerController == 0) return;
            _isShootingInMainBaseEnabled = enable;
            ApplyShootingInMainBase();
        }

        private void ApplyShootingInMainBase()
        {
            try
            {
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                if (playerState == 0) return;

                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;

                ulong inventoryComponent = Memory.ReadPtr(soldierActor + ASQSoldier.InventoryComponent);
                if (inventoryComponent == 0) return;

                ulong currentItemStaticInfo = Memory.ReadPtr(inventoryComponent + ASQSoldier.CurrentItemStaticInfo);
                if (currentItemStaticInfo == 0) return;

                Memory.WriteValue<bool>(currentItemStaticInfo + ASQSoldier.bUsableInMainBase, _isShootingInMainBaseEnabled);
            }
            catch { /* Silently fail */ }
        }

        public void SetSpeedHack(bool enable)
        {
            if (!_inGame || _playerController == 0) return;
            _isSpeedHackEnabled = enable;
            ApplySpeedHack();
        }

        private void ApplySpeedHack()
        {
            try
            {
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                if (playerState == 0) return;

                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;

                Memory.WriteValue<float>(soldierActor + Actor.CustomTimeDilation, _isSpeedHackEnabled ? 4.0f : 1.0f);
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting time dilation: {ex.Message}");
            }
        }

        public void SetAirStuck(bool enable)
        {
            if (!_inGame || _playerController == 0) return;
            _isAirStuckEnabled = enable;
            ApplyAirStuck();
        }

        // Die to fully reset
        private void ApplyAirStuck()
        {
            try
            {
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                if (playerState == 0) return;

                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;

                ulong characterMovement = Memory.ReadPtr(soldierActor + Character.CharacterMovement);
                if (characterMovement == 0) return;

                if (_isAirStuckEnabled)
                {
                    Memory.WriteValue<byte>(characterMovement + CharacterMovementComponent.MovementMode, (byte)EMovementMode.MOVE_Flying);
                    Memory.WriteValue<byte>(characterMovement + Character.ReplicatedMovementMode, (byte)EMovementMode.MOVE_Flying);
                    Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, 0);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxFlySpeed, 4000.0f);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxCustomMovementSpeed, 4000.0f);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxAcceleration, 4000.0f);
                }
                else
                {
                    Memory.WriteValue<byte>(characterMovement + CharacterMovementComponent.MovementMode, (byte)EMovementMode.MOVE_Walking);
                    Memory.WriteValue<byte>(characterMovement + Character.ReplicatedMovementMode, (byte)EMovementMode.MOVE_Walking);
                    Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, 1);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxFlySpeed, 200.0f);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxCustomMovementSpeed, 600.0f);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxAcceleration, 500.0f);
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting air stuck: {ex.Message}");
            }
        }

        public void SetHideActor(bool enable)
        {
            if (!_inGame || _playerController == 0) return;
            _isHideActorEnabled = enable;
            HideActor();
        }

        private void HideActor()
        {
            try
            {
                ulong playerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                if (playerState == 0) return;

                ulong soldierActor = Memory.ReadPtr(playerState + ASQPlayerState.Soldier);
                if (soldierActor == 0) return;

                if (_isHideActorEnabled)
                {
                    Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, 0);
                    Memory.WriteValue<byte>(soldierActor + Actor.bHidden, 1);
                }
                else
                {
                    Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, 1);
                    Memory.WriteValue<byte>(soldierActor + Actor.bHidden, 0);
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting : {ex.Message}");
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
} 
using Offsets;

namespace squad_dma.Source.Squad.Features
{
    public class QuickZoom : Manager
    {
        private bool _isQuickZoomEnabled = false;
        private float _originalFov = 0.0f;
        
        public QuickZoom(ulong playerController, bool inGame)
            : base(playerController, inGame)
        {
        }
        
        public void SetEnabled(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isQuickZoomEnabled = enable;
            Apply();
        }
        
        public override void Apply()
        {
            try
            {
                if (!IsLocalPlayerValid()) return;
                
                UpdateCachedPointers();
                ulong cameraManager = Memory.ReadPtr(_playerController + PlayerController.PlayerCameraManager);
                if (cameraManager == 0) return;
                
                if (_isQuickZoomEnabled)
                {
                    _originalFov = Memory.ReadValue<float>(cameraManager + PlayerCameraManager.DefaultFOV);
                    Memory.WriteValue<float>(cameraManager + PlayerCameraManager.DefaultFOV, 20.0f);
                }
                else if (_originalFov != 0.0f)
                {
                    Memory.WriteValue<float>(cameraManager + PlayerCameraManager.DefaultFOV, _originalFov);
                    _originalFov = 0.0f;
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting Quick Zoom: {ex.Message}");
            }
        }
    }
} 
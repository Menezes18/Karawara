using UnityEngine;
using UnityEngine.InputSystem;

namespace System.MiniMap
{
    [CreateAssetMenu(menuName = "Menezes/MiniMap/Input Handler Legacy")]
    public class InputBaseMiniMapInputLegacy : InputBaseMiniMap
    {
        public Key screenModeKey = Key.M;
        public Key zoomInKey = Key.I;
        public Key zoomOutKey = Key.I;

        public override void Init()
        {
           
        }

        public override bool IsInputDown(MiniMapInput key)
        {
#if ENABLE_INPUT_SYSTEM
            switch (key)
            {
                case MiniMapInput.ZoomIn:
                    return IsKeyPressed(zoomInKey);
                case MiniMapInput.ZoomOut:
                    return IsKeyPressed(zoomOutKey);
                case MiniMapInput.ScreenMode:
                    return IsKeyPressed(screenModeKey);
                default:
                    return false;
            }
#else
            return false;
#endif
        }

        
        private bool IsKeyPressed(Key keyCode)
        {
            
            return Keyboard.current[keyCode].wasPressedThisFrame;
        }

        
    }
}
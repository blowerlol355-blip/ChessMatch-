using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Detector global de toques en pantalla y clics izquierdos.
    /// Reproduce un blip suave mediante RetroAudioManager.PlayTap() salvo cuando ya se disparó
    /// un sonido específico de pieza, carta o botón uGUI en el mismo fotograma.
    /// </summary>
    public class GlobalTouchSound : MonoBehaviour
    {
        public static GlobalTouchSound Instance { get; private set; }

        private int _pendingTapFrame = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (DetectPointerPressThisFrame())
            {
                _pendingTapFrame = Time.frameCount;
            }
        }

        private void LateUpdate()
        {
            if (_pendingTapFrame == Time.frameCount)
            {
                _pendingTapFrame = -1;
                RetroAudioManager.Instance?.PlayTapIfNoPieceSoundThisFrame();
            }
        }

        private static bool DetectPointerPressThisFrame()
        {
            try
            {
                if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
                {
                    return true;
                }

                if (Input.GetMouseButtonDown(0))
                {
                    return true;
                }
            }
            catch (System.InvalidOperationException)
            {
                // Ignorar si el proyecto utiliza únicamente el New Input System
            }

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Touchscreen.current != null &&
                UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                return true;
            }

            if (UnityEngine.InputSystem.Mouse.current != null &&
                UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return false;
        }
    }
}


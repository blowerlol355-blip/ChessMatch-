using UnityEngine;
using UnityEngine.UI;

namespace ChessBalatro
{
    /// <summary>
    /// Componente auxiliar que configura y mantiene automáticamente el CanvasScaler en
    /// resolución de referencia móvil 9:16 Portrait (1080x1920) con Match Width/Height equilibrado (0.5f).
    /// Asegura que la interfaz, botones y textos se escalen proporcionalmente en teléfonos y tablets.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Canvas))]
    public class MobileCanvasScalerAdapter : MonoBehaviour
    {
        [Header("Resolución de Referencia Móvil (9:16)")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080f, 1920f);
        [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 0.5f;

        private CanvasScaler _scaler;

        private void Awake()
        {
            ApplyMobileScaling();
        }

        private void OnEnable()
        {
            ApplyMobileScaling();
        }

        /// <summary>
        /// Aplica la configuración estándar de escala móvil.
        /// </summary>
        [ContextMenu("Apply 9:16 Mobile Preset")]
        public void ApplyMobileScaling()
        {
            if (_scaler == null) _scaler = GetComponent<CanvasScaler>();
            if (_scaler == null) _scaler = gameObject.AddComponent<CanvasScaler>();

            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = referenceResolution;
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _scaler.matchWidthOrHeight = matchWidthOrHeight;
        }

        private void Reset()
        {
            ApplyMobileScaling();
        }
    }
}

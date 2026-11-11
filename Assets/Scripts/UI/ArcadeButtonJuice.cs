using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChessBalatro
{
    /// <summary>
    /// Componente de Game Feel y Juice estilo Arcade de 32-bits para botones uGUI.
    /// Incorpora:
    /// - Escala elástica y elevación de 2px en Hover / Selección.
    /// - Contracción rápida a 0.94f en Press simulando el pulsador de una máquina recreativa.
    /// - Pulso rítmico continuo ("respiración" de brillo y escala) en botones destacados.
    /// - Entrada escalonada (Staggered Entrance) en cascada al abrir paneles.
    /// </summary>
    public class ArcadeButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Configuración Arcade")]
        [Tooltip("Escala objetivo al pasar el puntero o enfocar el botón.")]
        [SerializeField] private float hoverScale = 1.06f;

        [Tooltip("Escala de contracción al presionar el botón físico.")]
        [SerializeField] private float pressScale = 0.94f;

        [Tooltip("Desplazamiento vertical hacia arriba en Hover.")]
        [SerializeField] private float hoverYShift = 3f;

        [Tooltip("Velocidad de interpolación elástica.")]
        [SerializeField] private float animationSpeed = 16f;

        [Header("Efecto de Pulso Rítmico (Featured Button)")]
        [Tooltip("Activa la respiración continua de escala y brillo (ej. 'JUGAR', '¡COMENZAR PARTIDA!').")]
        [SerializeField] private bool isFeaturedButton = false;
        [SerializeField] private float pulseFrequency = 2.4f;
        [SerializeField] private float pulseAmplitude = 0.025f;

        [Header("Entrada Escalonada (Staggered Entrance)")]
        [SerializeField] private bool animateEntranceOnEnable = true;
        [SerializeField] private float entranceDelay = 0f;
        [SerializeField] private float entranceDuration = 0.35f;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Outline _outline;
        private Image _buttonImage;

        private Vector3 _originalLocalPos;
        private Vector3 _targetScale = Vector3.one;
        private Vector3 _targetPos;
        private Color _originalOutlineColor = Color.white;
        private bool _isHovered = false;
        private bool _isPressed = false;
        private bool _isEntranceRunning = false;

        public bool IsFeaturedButton
        {
            get => isFeaturedButton;
            set => isFeaturedButton = value;
        }

        public float EntranceDelay
        {
            get => entranceDelay;
            set => entranceDelay = value;
        }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _outline = GetComponent<Outline>();
            _buttonImage = GetComponent<Image>();

            if (_rectTransform != null)
            {
                _originalLocalPos = _rectTransform.localPosition;
                _targetPos = _originalLocalPos;
            }

            if (_outline != null)
            {
                _originalOutlineColor = _outline.effectColor;
            }
        }

        private void OnEnable()
        {
            if (_rectTransform != null && _originalLocalPos == Vector3.zero && _rectTransform.localPosition != Vector3.zero)
            {
                _originalLocalPos = _rectTransform.localPosition;
            }

            _isHovered = false;
            _isPressed = false;
            _targetScale = Vector3.one;
            _targetPos = _originalLocalPos;

            if (animateEntranceOnEnable)
            {
                PlayStaggeredEntrance(entranceDelay);
            }
        }

        private void Update()
        {
            if (_isEntranceRunning) return;

            // 1. Pulso rítmico continuo si es botón destacado
            Vector3 desiredScale = _targetScale;
            if (isFeaturedButton && !_isPressed)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * pulseAmplitude;
                desiredScale *= pulse;

                if (_outline != null)
                {
                    float glow = 0.7f + 0.3f * Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f);
                    Color glowCol = new Color(1f, 0.85f, 0.25f, 1f) * glow;
                    glowCol.a = 1f;
                    _outline.effectColor = glowCol;
                }
            }

            // 2. Interpolación elástica hacia escala y posición deseadas
            if (_rectTransform != null)
            {
                _rectTransform.localScale = Vector3.Lerp(_rectTransform.localScale, desiredScale, Time.unscaledDeltaTime * animationSpeed);
                _rectTransform.localPosition = Vector3.Lerp(_rectTransform.localPosition, _targetPos, Time.unscaledDeltaTime * animationSpeed);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            _targetScale = Vector3.one * hoverScale;
            _targetPos = _originalLocalPos + new Vector3(0, hoverYShift, 0);

            if (_outline != null)
            {
                _outline.effectColor = Color.white;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            _isPressed = false;
            _targetScale = Vector3.one;
            _targetPos = _originalLocalPos;

            if (_outline != null)
            {
                _outline.effectColor = _originalOutlineColor;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            _targetScale = Vector3.one * pressScale;
            _targetPos = _originalLocalPos - new Vector3(0, 1.5f, 0);

            Selectable selectable = GetComponent<Selectable>();
            if (selectable == null || selectable.IsInteractable())
            {
                RetroAudioManager.Instance?.PlayTap();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            _targetScale = _isHovered ? Vector3.one * hoverScale : Vector3.one;
            _targetPos = _isHovered ? _originalLocalPos + new Vector3(0, hoverYShift, 0) : _originalLocalPos;
        }

        /// <summary>
        /// Dispara la animación de entrada en cascada desde abajo con retraso individual.
        /// </summary>
        public void PlayStaggeredEntrance(float delay = 0f)
        {
            StopAllCoroutines();
            StartCoroutine(StaggeredEntranceRoutine(delay));
        }

        private IEnumerator StaggeredEntranceRoutine(float delay)
        {
            _isEntranceRunning = true;

            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            _canvasGroup.alpha = 0f;
            if (_rectTransform != null)
            {
                _rectTransform.localScale = Vector3.one * 0.75f;
                _rectTransform.localPosition = _originalLocalPos - new Vector3(0, 24f, 0);
            }

            if (delay > 0.001f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            float elapsed = 0f;
            Vector3 startPos = _originalLocalPos - new Vector3(0, 24f, 0);

            while (elapsed < entranceDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / entranceDuration);
                // Elastic ease out
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                float overshoot = Mathf.Sin(t * Mathf.PI * 0.9f) * 0.12f;

                if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Lerp(0f, 1f, t * 1.5f);
                if (_rectTransform != null)
                {
                    _rectTransform.localPosition = Vector3.Lerp(startPos, _originalLocalPos, ease);
                    _rectTransform.localScale = Vector3.one * (Mathf.Lerp(0.75f, 1f, ease) + overshoot);
                }

                yield return null;
            }

            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            if (_rectTransform != null)
            {
                _rectTransform.localPosition = _originalLocalPos;
                _rectTransform.localScale = Vector3.one;
            }

            _targetPos = _originalLocalPos;
            _targetScale = Vector3.one;
            _isEntranceRunning = false;
        }

        /// <summary>
        /// Utilidad para inyectar y configurar ArcadeButtonJuice en todos los botones hijos de un contenedor.
        /// </summary>
        public static void AutoApplyToButtons(GameObject container, float delayStep = 0.05f)
        {
            if (container == null) return;

            Button[] buttons = container.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button btn = buttons[i];
                ArcadeButtonJuice juice = btn.GetComponent<ArcadeButtonJuice>();
                if (juice == null) juice = btn.gameObject.AddComponent<ArcadeButtonJuice>();

                juice.EntranceDelay = i * delayStep;
                juice.animateEntranceOnEnable = true;

                // Si es el botón JUGAR o COMENZAR, marcar como botón destacado
                string btnName = btn.gameObject.name.ToLowerInvariant();
                if (btnName.Contains("play") || btnName.Contains("jugar") || btnName.Contains("start") || btnName.Contains("comenzar"))
                {
                    juice.IsFeaturedButton = true;
                }
            }
        }
    }
}


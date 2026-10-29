using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChessBalatro
{
    /// <summary>
    /// Representación visual y física de una carta estilo Balatro en un Canvas de Unity uGUI.
    /// Gestiona animaciones de hover, pulsación táctil, contorno brillante (glow) al activarse,
    /// consumo de coste y comunicación con CardManager y BoardInputHandler.
    /// </summary>
    public class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Datos de la Carta")]
        [SerializeField] private CardData cardData;

        [Header("Componentes Visuales")]
        [SerializeField] private Image cardBackgroundImage;
        [SerializeField] private Image cardHeaderImage;
        [SerializeField] private Image artworkImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text costText;
        [SerializeField] private Text rarityText;
        [SerializeField] private Outline glowOutline;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Configuración de Animación")]
        [SerializeField] private float hoverYOffset = 18f;
        [SerializeField] private float hoverScale = 1.06f;
        [SerializeField] private float activeScale = 1.10f;
        [SerializeField] private float animationSpeed = 12f;

        [Header("Animación Pop-In (Draw)")]
        [Tooltip("Duración total de la animación de aparición al robar la carta.")]
        [SerializeField] private float drawAnimDuration = 0.55f;
        [Tooltip("Escala máxima durante el overshoot elástico (ej. 1.18 para un 18% extra).")]
        [SerializeField] private float drawOvershoot = 1.18f;

        [Header("Tilt Flotante Inactivo (estilo Balatro)")]
        [Tooltip("Activa el bamboleo sinusoidal cuando la carta está en la mano sin hover.")]
        [SerializeField] private bool enableIdleTilt = true;
        [SerializeField] private float idleTiltFrequency = 0.8f;   // Hz
        [SerializeField] private float idleTiltAmplitude = 2.5f;   // grados

        [Header("Ilustración de la Carta (Sprite completo)")]
        [Tooltip("Sprite de ilustración completa, asignado por código o desde el Inspector.")]
        [SerializeField] private Sprite illustrationSprite;

        // Estado interno
        public CardData CardData => cardData;
        public ICardEffect Effect => _currentEffect;
        public bool IsActivated => _isActivated;
        public bool IsInteractable { get; private set; } = true;

        private ICardEffect _currentEffect;
        private bool _isActivated = false;
        private bool _isHovered = false;
        private bool _isPressed = false;
        private bool _isDrawAnimationRunning = false;

        // Tilt flotante
        private float _idleTiltPhaseOffset;   // offsets aleatorios para que las cartas no se muevan sincronizadas

        private Vector3 _originalLocalPos;
        private Vector3 _targetLocalPos;
        private Vector3 _targetScale = Vector3.one;
        private float   _targetZRotation = 0f;
        private Coroutine _glowCoroutine;
        private RectTransform _rectTransform;

        // Referencia opcional al gestor de cartas
        private CardManager _cardManager;

        public event Action<CardView> OnCardClicked;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null) _rectTransform = gameObject.AddComponent<RectTransform>();

            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (glowOutline == null) glowOutline = GetComponent<Outline>();
            if (glowOutline == null)
            {
                glowOutline = gameObject.AddComponent<Outline>();
                glowOutline.effectDistance = new Vector2(4f, -4f);
                glowOutline.enabled = false;
            }

            _originalLocalPos = transform.localPosition;
            _targetLocalPos = _originalLocalPos;

            // Offset aleatorio para que el tilt no esté sincronizado entre cartas de la mano
            _idleTiltPhaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        private void Start()
        {
            if (_cardManager == null) _cardManager = FindAnyObjectByType<CardManager>();

            if (_originalLocalPos == Vector3.zero && transform.localPosition != Vector3.zero)
            {
                _originalLocalPos = transform.localPosition;
                _targetLocalPos = _originalLocalPos;
            }

            if (cardData != null && _currentEffect == null)
            {
                Initialize(cardData, _cardManager);
            }
        }

        private void Update()
        {
            if (_isDrawAnimationRunning) return;   // la corutina controla transform directamente

            // Interpolación suave de posición y escala (sensación orgánica tipo Balatro)
            transform.localPosition = Vector3.Lerp(transform.localPosition, _targetLocalPos, Time.deltaTime * animationSpeed);
            transform.localScale    = Vector3.Lerp(transform.localScale,    _targetScale,    Time.deltaTime * animationSpeed);

            // Tilt flotante sinusoidal (idle, estilo Balatro)
            if (enableIdleTilt && !_isHovered && !_isActivated && !_isPressed)
            {
                float tilt = Mathf.Sin(Time.time * idleTiltFrequency * Mathf.PI * 2f + _idleTiltPhaseOffset) * idleTiltAmplitude;
                _targetZRotation = tilt;
            }
            else
            {
                _targetZRotation = 0f;
            }

            float currentZ = transform.localEulerAngles.z;
            // Convertir el ángulo de Euler a un valor en el rango [-180, 180] para Lerp correcto
            if (currentZ > 180f) currentZ -= 360f;
            float newZ = Mathf.Lerp(currentZ, _targetZRotation, Time.deltaTime * animationSpeed);
            transform.localEulerAngles = new Vector3(0f, 0f, newZ);
        }

        /// <summary>
        /// Inicializa la carta con sus datos, actualizando textos, colores según rareza y enlace al gestor.
        /// </summary>
        public void Initialize(CardData data, CardManager manager = null)
        {
            cardData = data;
            _cardManager = manager;

            if (data == null) return;

            _currentEffect = data.GetOrCreateEffect();

            // Construcción procedural de elementos si no existen en el GameObject
            EnsureVisualComponents();

            // Asignación de datos a los textos
            if (titleText != null) titleText.text = data.CardName;
            if (descriptionText != null) descriptionText.text = data.Description;
            if (costText != null) costText.text = $"${data.Cost}";
            if (rarityText != null) rarityText.text = GetRarityLabel(data.Rarity);

            // Sprite de arte: prioridad al sprite de ilustración completa si está asignado
            Sprite effectiveSprite = illustrationSprite != null ? illustrationSprite : data.CardSprite;

            if (artworkImage != null)
            {
                artworkImage.preserveAspect = true;
                if (effectiveSprite != null)
                {
                    artworkImage.sprite = effectiveSprite;
                    artworkImage.color  = Color.white;
                }
                else
                {
                    // Procedural fallback icon
                    artworkImage.color = GetRarityAccentColor(data.Rarity);
                }
            }

            // Aplicar paleta de colores según rareza (estilo Balatro)
            ApplyRarityStyling(data.Rarity);
        }

        /// <summary>
        /// Asigna un Sprite de ilustración completa a la carta en tiempo de ejecución.
        /// Llamar antes o después de Initialize; sobrescribe el CardData.CardSprite si ambos existen.
        /// </summary>
        public void SetIllustrationSprite(Sprite sprite)
        {
            illustrationSprite = sprite;
            if (artworkImage != null && sprite != null)
            {
                artworkImage.sprite = sprite;
                artworkImage.color  = Color.white;
            }
        }

        // ── Animación Pop-In / Draw ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Dispara la animación de aparición al "robar" la carta:
        /// escala desde 0 hasta 1 con un overshoot elástico, luego se asienta.
        /// Llamar justo después de Initialize() cuando la carta entra a la mano.
        /// </summary>
        public void PlayDrawAnimation()
        {
            StartCoroutine(DrawAnimationRoutine());
        }

        private IEnumerator DrawAnimationRoutine()
        {
            _isDrawAnimationRunning = true;

            // Asegurarse de que la carta es invisible al inicio
            transform.localScale = Vector3.zero;
            if (canvasGroup != null) canvasGroup.alpha = 0f;

            float elapsed  = 0f;
            float phaseA   = drawAnimDuration * 0.55f;   // fase de escala principal
            float phaseB   = drawAnimDuration * 0.25f;   // overshoot
            float phaseC   = drawAnimDuration * 0.20f;   // asentamiento

            // Fase A: 0 → overshoot (ease-out cúbico + fade in)
            while (elapsed < phaseA)
            {
                elapsed += Time.deltaTime;
                float t     = Mathf.Clamp01(elapsed / phaseA);
                float ease  = 1f - Mathf.Pow(1f - t, 3f);   // ease-out cubic
                float scale = Mathf.Lerp(0f, drawOvershoot, ease);
                transform.localScale = Vector3.one * scale;
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(0f, 1f, t * 2f);
                yield return null;
            }

            if (canvasGroup != null) canvasGroup.alpha = 1f;

            // Fase B: overshoot → ligeramente por encima de 1.0 (rebote elástico)
            elapsed = 0f;
            while (elapsed < phaseB)
            {
                elapsed += Time.deltaTime;
                float t     = Mathf.Clamp01(elapsed / phaseB);
                float scale = Mathf.Lerp(drawOvershoot, 1.04f, t);
                transform.localScale = Vector3.one * scale;
                yield return null;
            }

            // Fase C: asentamiento suave a 1.0
            elapsed = 0f;
            while (elapsed < phaseC)
            {
                elapsed += Time.deltaTime;
                float t     = Mathf.Clamp01(elapsed / phaseC);
                float scale = Mathf.Lerp(1.04f, 1f, Mathf.SmoothStep(0f, 1f, t));
                transform.localScale = Vector3.one * scale;
                yield return null;
            }

            transform.localScale = Vector3.one;
            _isDrawAnimationRunning = false;
        }

        // ── Jugar Carta → Efecto + Animación de consumo ───────────────────────────────────────────

        /// <summary>
        /// Ejecuta el efecto de la carta sobre el tablero y lanza la animación de desvanecimiento
        /// hacia arriba (estilo Balatro). Si el efecto es <see cref="PetrosianDefenseEffect"/>,
        /// llama a ApplyEffect directamente para activar el Escudo.
        /// </summary>
        /// <param name="board">Estado del tablero actual (puede ser null; se intentará obtener de la escena).</param>
        public void PlayCard(BoardState board = null)
        {
            if (!IsInteractable) return;

            // Resolver board desde la escena si no se pasó
            if (board == null)
            {
                BoardView bv = UnityEngine.Object.FindAnyObjectByType<BoardView>();
                board = bv?.BoardState;
            }

            // Aplicar efecto de la carta
            if (_currentEffect != null && board != null)
            {
                _currentEffect.ApplyEffect(board);
            }

            // Lanzar animación de desvanecimiento hacia arriba
            StartCoroutine(PlayCardConsumeRoutine());
        }

        private IEnumerator PlayCardConsumeRoutine()
        {
            SetInteractable(false);

            float elapsed    = 0f;
            float duration   = 0.45f;
            Vector3 startPos = transform.localPosition;
            Vector3 endPos   = startPos + new Vector3(0f, 80f, 0f);   // sube 80 unidades de canvas
            Vector3 startScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 2f);   // ease-out quad
                transform.localPosition = Vector3.Lerp(startPos, endPos, ease);
                transform.localScale    = Vector3.Lerp(startScale, Vector3.zero, t * t);
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(1f, 0f, ease);
                yield return null;
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Asigna la posición base local (útil cuando el CardHandView organiza las cartas en abanico o fila).
        /// </summary>
        public void SetBasePosition(Vector3 localPos)
        {
            _originalLocalPos = localPos;
            if (!_isHovered && !_isActivated)
            {
                _targetLocalPos = localPos;
            }
        }

        /// <summary>
        /// Activa o desactiva visualmente el estado de la carta con contorno brillante y escala aumentada.
        /// </summary>
        public void SetActivated(bool activated)
        {
            _isActivated = activated;

            if (_isActivated)
            {
                _targetScale = Vector3.one * activeScale;
                _targetLocalPos = _originalLocalPos + new Vector3(0, hoverYOffset * 1.4f, 0);

                if (glowOutline != null)
                {
                    glowOutline.enabled = true;
                    if (_glowCoroutine != null) StopCoroutine(_glowCoroutine);
                    _glowCoroutine = StartCoroutine(PulseGlowRoutine());
                }
            }
            else
            {
                if (_glowCoroutine != null)
                {
                    StopCoroutine(_glowCoroutine);
                    _glowCoroutine = null;
                }

                if (glowOutline != null)
                {
                    glowOutline.enabled = false;
                }

                UpdateTargetTransform();
            }
        }

        public void SetInteractable(bool interactable)
        {
            IsInteractable = interactable;
            if (canvasGroup != null)
            {
                canvasGroup.interactable = interactable;
                canvasGroup.alpha = interactable ? 1.0f : 0.65f;
            }
        }

        /// <summary>
        /// Se dispara al hacer clic/toque sobre la carta física en pantalla.
        /// Notifica al CardManager para validar coste, aplicar el efecto sobre el tablero
        /// y refrescar las casillas legales en BoardInputHandler.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsInteractable) return;

            // Punch scale instantáneo al tocar
            transform.localScale = Vector3.one * (activeScale * 1.08f);

            OnCardClicked?.Invoke(this);

            if (_cardManager == null)
            {
                _cardManager = FindAnyObjectByType<CardManager>();
            }

            if (_cardManager != null)
            {
                _cardManager.TryToggleCardActivation(this);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            _isHovered = true;
            UpdateTargetTransform();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            _isPressed = false;
            UpdateTargetTransform();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            _isPressed = true;
            _targetScale = Vector3.one * 0.98f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            UpdateTargetTransform();
        }

        /// <summary>
        /// Animación cuando la carta se consume tras realizar la jugada de ajedrez.
        /// </summary>
        public void OnTurnEndAfterUsed()
        {
            StartCoroutine(ConsumeRoutine());
        }

        private IEnumerator ConsumeRoutine()
        {
            SetInteractable(false);
            float elapsed = 0f;
            float duration = 0.35f;
            Vector3 startScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t * t);
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }

            gameObject.SetActive(false);
        }

        private void UpdateTargetTransform()
        {
            if (_isPressed)
            {
                _targetScale = Vector3.one * 0.98f;
            }
            else if (_isActivated)
            {
                _targetScale = Vector3.one * activeScale;
                _targetLocalPos = _originalLocalPos + new Vector3(0, hoverYOffset * 1.4f, 0);
            }
            else if (_isHovered)
            {
                _targetScale = Vector3.one * hoverScale;
                _targetLocalPos = _originalLocalPos + new Vector3(0, hoverYOffset, 0);
            }
            else
            {
                _targetScale = Vector3.one;
                _targetLocalPos = _originalLocalPos;
            }
        }

        /// <summary>
        /// Corutina que hace parpadear/pulsar el contorno brillante con estética Balatro.
        /// </summary>
        private IEnumerator PulseGlowRoutine()
        {
            Color baseColor = cardData != null ? GetRarityAccentColor(cardData.Rarity) : new Color(0.98f, 0.85f, 0.25f, 1f);
            baseColor.a = 1f;

            while (_isActivated && glowOutline != null)
            {
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 6f);
                Color currentColor = baseColor * pulse;
                currentColor.a = 0.9f + 0.1f * pulse;
                glowOutline.effectColor = currentColor;

                float dist = 4f + 2f * pulse;
                glowOutline.effectDistance = new Vector2(dist, -dist);
                yield return null;
            }
        }

        #region Estilización y Construcción Visual Procedural

        private string GetRarityLabel(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return "COMÚN";
                case CardRarity.Uncommon: return "INFRECUENTE";
                case CardRarity.Rare: return "RARA";
                case CardRarity.Legendary: return "LEGENDARIA";
                default: return "CARTA";
            }
        }

        private Color GetRarityAccentColor(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common:
                    return new Color(0.24f, 0.55f, 0.95f, 1.0f); // Azul Balatro
                case CardRarity.Uncommon:
                    return new Color(0.12f, 0.75f, 0.45f, 1.0f); // Verde Esmeralda
                case CardRarity.Rare:
                    return new Color(0.95f, 0.28f, 0.28f, 1.0f); // Rojo Rubí
                case CardRarity.Legendary:
                    return new Color(0.72f, 0.32f, 0.98f, 1.0f); // Púrpura Místico
                default:
                    return Color.white;
            }
        }

        private void ApplyRarityStyling(CardRarity rarity)
        {
            Color accent = GetRarityAccentColor(rarity);

            if (cardHeaderImage != null)
            {
                cardHeaderImage.color = accent;
            }

            if (rarityText != null)
            {
                rarityText.color = accent;
            }

            if (glowOutline != null)
            {
                glowOutline.effectColor = accent;
            }
        }

        /// <summary>
        /// Garantiza que la carta cuente con todos los elementos gráficos uGUI necesarios
        /// incluso si se crea desde código en tiempo de ejecución sin prefab previo.
        /// </summary>
        private void EnsureVisualComponents()
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform.sizeDelta == Vector2.zero)
            {
                _rectTransform.sizeDelta = new Vector2(140f, 200f);
            }

            // Fondo de la carta
            if (cardBackgroundImage == null)
            {
                cardBackgroundImage = GetComponent<Image>();
                if (cardBackgroundImage == null) cardBackgroundImage = gameObject.AddComponent<Image>();
                cardBackgroundImage.color = new Color(0.12f, 0.14f, 0.18f, 0.96f); // Fondo pizarra oscuro
            }
            cardBackgroundImage.raycastTarget = true;

            // Encabezado
            if (cardHeaderImage == null)
            {
                Transform headerTr = transform.Find("Header");
                if (headerTr == null)
                {
                    GameObject headerObj = new GameObject("Header");
                    headerObj.transform.SetParent(transform, false);
                    RectTransform rt = headerObj.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0f, 0.78f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    cardHeaderImage = headerObj.AddComponent<Image>();
                    cardHeaderImage.color = new Color(0.24f, 0.55f, 0.95f, 1f);
                }
                else
                {
                    cardHeaderImage = headerTr.GetComponent<Image>();
                }
            }

            // Título
            if (titleText == null)
            {
                Transform tTr = transform.Find("TitleText");
                if (tTr == null)
                {
                    GameObject tObj = new GameObject("TitleText");
                    tObj.transform.SetParent(transform, false);
                    RectTransform rt = tObj.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.04f, 0.78f);
                    rt.anchorMax = new Vector2(0.72f, 1f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    titleText = tObj.AddComponent<Text>();
                    titleText.font = GetDefaultUiFont();
                    titleText.fontSize = 12;
                    titleText.fontStyle = FontStyle.Bold;
                    titleText.color = Color.white;
                    titleText.alignment = TextAnchor.MiddleLeft;
                }
                else
                {
                    titleText = tTr.GetComponent<Text>();
                }
            }

            // Texto de Coste
            if (costText == null)
            {
                Transform cTr = transform.Find("CostText");
                if (cTr == null)
                {
                    GameObject cObj = new GameObject("CostText");
                    cObj.transform.SetParent(transform, false);
                    RectTransform rt = cObj.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.72f, 0.78f);
                    rt.anchorMax = new Vector2(0.96f, 1f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    costText = cObj.AddComponent<Text>();
                    costText.font = GetDefaultUiFont();
                    costText.fontSize = 13;
                    costText.fontStyle = FontStyle.Bold;
                    costText.color = new Color(1f, 0.88f, 0.25f, 1f); // Dorado brillante
                    costText.alignment = TextAnchor.MiddleRight;
                }
                else
                {
                    costText = cTr.GetComponent<Text>();
                }
            }

            // Área de Ilustración
            if (artworkImage == null)
            {
                Transform aTr = transform.Find("Artwork");
                if (aTr == null)
                {
                    GameObject aObj = new GameObject("Artwork");
                    aObj.transform.SetParent(transform, false);
                    RectTransform rt = aObj.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.15f, 0.44f);
                    rt.anchorMax = new Vector2(0.85f, 0.74f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    artworkImage = aObj.AddComponent<Image>();
                    artworkImage.color = new Color(0.24f, 0.55f, 0.95f, 0.8f);
                }
                else
                {
                    artworkImage = aTr.GetComponent<Image>();
                }
            }

            // Descripción
            if (descriptionText == null)
            {
                Transform dTr = transform.Find("DescriptionText");
                if (dTr == null)
                {
                    GameObject dObj = new GameObject("DescriptionText");
                    dObj.transform.SetParent(transform, false);
                    RectTransform rt = dObj.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.08f, 0.12f);
                    rt.anchorMax = new Vector2(0.92f, 0.42f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    descriptionText = dObj.AddComponent<Text>();
                    descriptionText.font = GetDefaultUiFont();
                    descriptionText.fontSize = 10;
                    descriptionText.color = new Color(0.88f, 0.90f, 0.94f, 1f);
                    descriptionText.alignment = TextAnchor.UpperLeft;
                    descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
                }
                else
                {
                    descriptionText = dTr.GetComponent<Text>();
                }
            }

            // Rareza (Badge inferior)
            if (rarityText == null)
            {
                Transform rTr = transform.Find("RarityText");
                if (rTr == null)
                {
                    GameObject rObj = new GameObject("RarityText");
                    rObj.transform.SetParent(transform, false);
                    RectTransform rt = rObj.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.06f, 0.02f);
                    rt.anchorMax = new Vector2(0.94f, 0.11f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    rarityText = rObj.AddComponent<Text>();
                    rarityText.font = GetDefaultUiFont();
                    rarityText.fontSize = 9;
                    rarityText.fontStyle = FontStyle.Bold;
                    rarityText.alignment = TextAnchor.MiddleCenter;
                }
                else
                {
                    rarityText = rTr.GetComponent<Text>();
                }
            }
        }

        private static Font _cachedDefaultFont;

        private static Font GetDefaultUiFont()
        {
            if (_cachedDefaultFont != null) return _cachedDefaultFont;

            try
            {
                _cachedDefaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch
            {
                try
                {
                    _cachedDefaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch
                {
                    _cachedDefaultFont = Resources.Load<Font>("Fonts/LegacyRuntime");
                }
            }

            return _cachedDefaultFont;
        }

        #endregion
    }
}

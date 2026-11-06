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
    /// abanico cinético, consumo de coste y comunicación con CardManager y BoardInputHandler.
    /// </summary>
    public class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Datos de la Carta")]
        [SerializeField] private CardData cardData;

        [Header("Dimensiones y Escala")]
        [Tooltip("Dimensiones físicas de la carta en píxeles de Canvas.")]
        [SerializeField] private Vector2 cardSize = new Vector2(240f, 340f);

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
        [SerializeField] private float hoverYOffset = 30f;
        [SerializeField] private float hoverXOffset = -35f; // Desplazamiento hacia la izquierda en disposición vertical
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
        public bool IsDisappeared => _isDisappeared;
        public Vector2 CardSize => cardSize;

        private ICardEffect _currentEffect;
        private bool _isActivated = false;
        private bool _isHovered = false;
        private bool _isPressed = false;
        private bool _isDrawAnimationRunning = false;
        private bool _isVerticalLayout = false;
        private bool _isDisappeared = false;
        private Coroutine _selectDisappearCoroutine;
        private Coroutine _burnoutCoroutine;

        public void SetVerticalLayout(bool isVertical)
        {
            _isVerticalLayout = isVertical;
        }

        // Tilt flotante y abanico
        private float _idleTiltPhaseOffset;   // offsets aleatorios para que las cartas no se muevan sincronizadas
        private float _baseRotationZ = 0f;    // ángulo base asignado por el abanico del CardHandView

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
                glowOutline.effectDistance = new Vector2(5f, -5f);
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
            if (_isDrawAnimationRunning || _isDisappeared) return; // la corutina controla transform directamente

            // Interpolación suave de posición y escala
            transform.localPosition = Vector3.Lerp(transform.localPosition, _targetLocalPos, Time.deltaTime * animationSpeed);
            transform.localScale    = Vector3.Lerp(transform.localScale,    _targetScale,    Time.deltaTime * animationSpeed);

            // Tilt flotante sinusoidal (idle, estilo Balatro)
            if (enableIdleTilt && !_isHovered && !_isActivated && !_isPressed)
            {
                float tilt = Mathf.Sin(Time.time * idleTiltFrequency * Mathf.PI * 2f + _idleTiltPhaseOffset) * idleTiltAmplitude;
                _targetZRotation = _baseRotationZ + tilt;
            }
            else if (_isHovered || _isActivated)
            {
                // Al interactuar se endereza a 0° para máxima legibilidad
                _targetZRotation = 0f;
            }
            else
            {
                _targetZRotation = _baseRotationZ;
            }

            float currentZ = transform.localEulerAngles.z;
            if (currentZ > 180f) currentZ -= 360f;
            float newZ = Mathf.Lerp(currentZ, _targetZRotation, Time.deltaTime * animationSpeed);
            transform.localEulerAngles = new Vector3(0f, 0f, newZ);
        }

        /// <summary>
        /// Aplica las dimensiones físicas ampliadas a la carta y sus elementos hijos.
        /// </summary>
        public void ApplyCardSize(Vector2 size)
        {
            cardSize = size;
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform != null)
            {
                _rectTransform.sizeDelta = size;
            }
        }

        /// <summary>
        /// Asigna el ángulo base de inclinación en el abanico cinético de la mano.
        /// </summary>
        public void SetBaseRotationAngle(float angle)
        {
            _baseRotationZ = angle;
            if (!_isHovered && !_isActivated)
            {
                _targetZRotation = angle;
            }
        }

        /// <summary>
        /// Inicializa la carta con sus datos, actualizando textos legibles, colores según rareza y enlace al gestor.
        /// </summary>
        public void Initialize(CardData data, CardManager manager = null)
        {
            cardData = data;
            _cardManager = manager;

            if (data == null) return;

            _currentEffect = data.GetOrCreateEffect();

            // Construcción procedural ampliada de elementos visuales
            EnsureVisualComponents();

            // Textos legibles y completos (sin truncamientos)
            if (titleText != null)
            {
                string displayTitle = data.CardName;
                if (displayTitle.Contains("Tigrán Petrosián"))
                {
                    displayTitle = "Tigrán Petrosián";
                }
                titleText.text = displayTitle;
            }
            if (descriptionText != null) descriptionText.text = data.Description;
            if (costText != null) costText.text = $"${data.Cost}";
            if (rarityText != null) rarityText.text = GetRarityLabel(data.Rarity);

            // Sprite de arte: prioridad a ilustración completa -> CardSprite -> Sprite de ficha de ajedrez fallback
            Sprite effectiveSprite = illustrationSprite != null ? illustrationSprite : data.CardSprite;
            if (effectiveSprite == null)
            {
                effectiveSprite = ResolvePieceFallbackSprite(data.CardName);
            }

            if (artworkImage != null)
            {
                artworkImage.preserveAspect = true;
                if (effectiveSprite != null)
                {
                    artworkImage.sprite = effectiveSprite;
                    artworkImage.color  = Color.white;
                    artworkImage.enabled = true;
                }
                else
                {
                    artworkImage.sprite = null;
                    artworkImage.color = GetRarityAccentColor(data.Rarity);
                }
            }

            // Aplicar paleta de colores según rareza (estilo Balatro)
            ApplyRarityStyling(data.Rarity);
        }

        /// <summary>
        /// Busca de forma inteligente un sprite de pieza de ajedrez adecuado si la carta no incluye ilustración propia.
        /// </summary>
        private Sprite ResolvePieceFallbackSprite(string cardName)
        {
            if (string.IsNullOrEmpty(cardName)) return null;

            string lower = cardName.ToLowerInvariant();
            if (lower.Contains("petrosián") || lower.Contains("petrosian") || lower.Contains("tigrán") || lower.Contains("tigran"))
            {
                Sprite petro = PetrosianCardData.LoadPetrosianSprite();
                if (petro != null) return petro;
            }

            PieceType targetType = PieceType.None;
            if (lower.Contains("peón") || lower.Contains("peon") || lower.Contains("pawn"))
            {
                targetType = PieceType.Pawn;
            }
            else if (lower.Contains("caball") || lower.Contains("knight") || lower.Contains("salto"))
            {
                targetType = PieceType.Knight;
            }
            else if (lower.Contains("alfil") || lower.Contains("bishop"))
            {
                targetType = PieceType.Bishop;
            }
            else if (lower.Contains("torre") || lower.Contains("rook"))
            {
                targetType = PieceType.Rook;
            }
            else if (lower.Contains("dama") || lower.Contains("reina") || lower.Contains("queen"))
            {
                targetType = PieceType.Queen;
            }
            else if (lower.Contains("rey") || lower.Contains("king"))
            {
                targetType = PieceType.King;
            }

            if (targetType != PieceType.None)
            {
                if (PieceSkinManager.Instance != null && PieceSkinManager.Instance.ActiveTheme != null)
                {
                    Sprite s = PieceSkinManager.Instance.ActiveTheme.GetSprite(targetType, PieceColor.White);
                    if (s != null) return s;
                }

                BoardView bv = FindAnyObjectByType<BoardView>();
                if (bv != null && bv.PieceTheme != null)
                {
                    Sprite s = bv.PieceTheme.GetSprite(targetType, PieceColor.White);
                    if (s != null) return s;
                }

                string pieceFileName = targetType switch
                {
                    PieceType.Pawn => "white_pawn",
                    PieceType.Knight => "white_knight",
                    PieceType.Bishop => "white_bishop",
                    PieceType.Rook => "white_rook",
                    PieceType.Queen => "white_queen",
                    PieceType.King => "white_king",
                    _ => null
                };

                if (!string.IsNullOrEmpty(pieceFileName))
                {
                    Sprite s = Resources.Load<Sprite>($"Pieces/{pieceFileName}");
                    if (s != null) return s;
                }
            }

            return null;
        }

        /// <summary>
        /// Asigna un Sprite de ilustración completa a la carta en tiempo de ejecución.
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

        public void PlayDrawAnimation()
        {
            StartCoroutine(DrawAnimationRoutine());
        }

        private IEnumerator DrawAnimationRoutine()
        {
            _isDrawAnimationRunning = true;

            transform.localScale = Vector3.zero;
            if (canvasGroup != null) canvasGroup.alpha = 0f;

            float elapsed  = 0f;
            float phaseA   = drawAnimDuration * 0.55f;
            float phaseB   = drawAnimDuration * 0.25f;
            float phaseC   = drawAnimDuration * 0.20f;

            while (elapsed < phaseA)
            {
                elapsed += Time.deltaTime;
                float t     = Mathf.Clamp01(elapsed / phaseA);
                float ease  = 1f - Mathf.Pow(1f - t, 3f);
                float scale = Mathf.Lerp(0f, drawOvershoot, ease);
                transform.localScale = Vector3.one * scale;
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(0f, 1f, t * 2f);
                yield return null;
            }

            if (canvasGroup != null) canvasGroup.alpha = 1f;

            elapsed = 0f;
            while (elapsed < phaseB)
            {
                elapsed += Time.deltaTime;
                float t     = Mathf.Clamp01(elapsed / phaseB);
                float scale = Mathf.Lerp(drawOvershoot, 1.04f, t);
                transform.localScale = Vector3.one * scale;
                yield return null;
            }

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

        public void PlayCard(BoardState board = null)
        {
            if (!IsInteractable) return;

            if (board == null)
            {
                BoardView bv = UnityEngine.Object.FindAnyObjectByType<BoardView>();
                board = bv?.BoardState;
            }

            if (_currentEffect != null && board != null)
            {
                _currentEffect.ApplyEffect(board);
            }

            StartCoroutine(PlayCardConsumeRoutine());
        }

        private IEnumerator PlayCardConsumeRoutine()
        {
            SetInteractable(false);

            float elapsed    = 0f;
            float duration   = 0.45f;
            Vector3 startPos = transform.localPosition;
            Vector3 endPos   = startPos + new Vector3(0f, 90f, 0f);
            Vector3 startScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 2f);
                transform.localPosition = Vector3.Lerp(startPos, endPos, ease);
                transform.localScale    = Vector3.Lerp(startScale, Vector3.zero, t * t);
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(1f, 0f, ease);
                yield return null;
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Asigna la posición base local calculada por CardHandView.
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
                _targetLocalPos = _originalLocalPos + new Vector3(0, hoverYOffset * 1.35f, 0);

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

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsInteractable) return;

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
            // Al hacer hover, elevar la carta a la cima de la jerarquía para que destaque
            transform.SetAsLastSibling();
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

        public void OnTurnEndAfterUsed()
        {
            PlayDestroyBurnoutAnimation();
        }

        /// <summary>
        /// Animación de selección: la carta da un punch elástico de escala y sale proyectada hacia el centro/tablero
        /// desvaneciéndose para dejar el tablero 100% despejado y visible para la jugada.
        /// </summary>
        public void PlaySelectAndDisappearAnimation()
        {
            if (_selectDisappearCoroutine != null) StopCoroutine(_selectDisappearCoroutine);
            if (_burnoutCoroutine != null) StopCoroutine(_burnoutCoroutine);

            _selectDisappearCoroutine = StartCoroutine(SelectAndDisappearRoutine());
        }

        private IEnumerator SelectAndDisappearRoutine()
        {
            _isDisappeared = true;
            SetInteractable(false);
            _isDrawAnimationRunning = false;

            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.TriggerHapticFeedback();
            }

            Vector3 startPos = transform.localPosition;
            Vector3 targetPos = startPos + new Vector3(_isVerticalLayout ? -180f : 0f, _isVerticalLayout ? 40f : 160f, 0f);
            Vector3 startScale = transform.localScale;
            Vector3 peakScale = startScale * 1.18f;
            float startRot = transform.localEulerAngles.z;
            if (startRot > 180f) startRot -= 360f;
            float targetRot = -10f;

            float duration = 0.35f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                Vector3 curScale;
                if (t < 0.28f)
                {
                    curScale = Vector3.Lerp(startScale, peakScale, t / 0.28f);
                }
                else
                {
                    curScale = Vector3.Lerp(peakScale, Vector3.one * 0.35f, (t - 0.28f) / 0.72f);
                }

                transform.localPosition = Vector3.Lerp(startPos, targetPos, ease);
                transform.localScale = curScale;
                transform.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(startRot, targetRot, ease));

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, Mathf.Pow(t, 2.2f));
                }

                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
            }
            _selectDisappearCoroutine = null;
        }

        /// <summary>
        /// Animación de retorno a la mano: si el jugador desactiva o cancela la carta antes de mover,
        /// regresa volando a su posición original en la mano con fade-in y curvatura suave.
        /// </summary>
        public void PlayReturnToHandAnimation()
        {
            if (_selectDisappearCoroutine != null)
            {
                StopCoroutine(_selectDisappearCoroutine);
                _selectDisappearCoroutine = null;
            }

            StartCoroutine(ReturnToHandRoutine());
        }

        private IEnumerator ReturnToHandRoutine()
        {
            _isDisappeared = false;

            Vector3 flyFromPos = _originalLocalPos + new Vector3(_isVerticalLayout ? -150f : 0f, _isVerticalLayout ? 40f : 140f, 0f);
            transform.localPosition = flyFromPos;
            transform.localScale = Vector3.one * 0.5f;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = true;
            }

            float duration = 0.30f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                transform.localPosition = Vector3.Lerp(flyFromPos, _originalLocalPos, ease);
                transform.localScale = Vector3.Lerp(Vector3.one * 0.5f, Vector3.one, ease);
                transform.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-10f, _baseRotationZ, ease));

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(0f, 1f, ease);
                }

                yield return null;
            }

            transform.localPosition = _originalLocalPos;
            transform.localScale = Vector3.one;
            transform.localEulerAngles = new Vector3(0f, 0f, _baseRotationZ);
            SetInteractable(true);
        }

        /// <summary>
        /// Animación de destrucción definitiva (Burnout / Ash Disintegrate):
        /// Una vez que la carta se ha usado en un movimiento real, aparece en el centro calcinada,
        /// con sello '¡CONSUMIDA!', tiembla violentamente y se destruye el GameObject para siempre.
        /// </summary>
        public void PlayDestroyBurnoutAnimation()
        {
            if (_selectDisappearCoroutine != null)
            {
                StopCoroutine(_selectDisappearCoroutine);
                _selectDisappearCoroutine = null;
            }

            if (_burnoutCoroutine != null) StopCoroutine(_burnoutCoroutine);
            _burnoutCoroutine = StartCoroutine(DestroyBurnoutRoutine());
        }

        private IEnumerator DestroyBurnoutRoutine()
        {
            _isDisappeared = true;
            SetInteractable(false);

            // Centrar la carta en el canvas sobre el tablero
            transform.SetAsLastSibling();
            Vector3 centerPos = Vector3.zero;
            transform.localPosition = centerPos;
            transform.localScale = Vector3.one * 1.15f;
            transform.localEulerAngles = new Vector3(0f, 0f, -6f);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = false;
            }

            // Aplicar acabado de carta calcinada/agotada
            ApplyBurnedLook();

            // Sello visual "¡CONSUMIDA!"
            GameObject stamp = EnsureBurnedStampOverlay();
            if (stamp != null) stamp.SetActive(true);

            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.TriggerHapticFeedback();
            }

            // 1. Fase de Jitter / Temblor violento de fallo de energía
            float shakeDuration = 0.40f;
            float elapsed = 0f;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float jX = Mathf.Sin(Time.time * 85f) * 7f;
                float jY = Mathf.Cos(Time.time * 70f) * 5f;
                transform.localPosition = centerPos + new Vector3(jX, jY, 0f);
                yield return null;
            }

            // 2. Fase de Colapso / Desintegración
            float collapseDuration = 0.32f;
            elapsed = 0f;
            Vector3 currentScale = transform.localScale;

            while (elapsed < collapseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / collapseDuration);
                float ease = t * t;

                transform.localScale = Vector3.Lerp(currentScale, Vector3.zero, ease);
                transform.localEulerAngles = new Vector3(0f, 0f, -6f - (t * 35f));

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, ease);
                }

                yield return null;
            }

            // Destrucción física definitiva
            Destroy(gameObject);
        }

        private void ApplyBurnedLook()
        {
            if (cardBackgroundImage != null)
            {
                cardBackgroundImage.color = new Color(0.12f, 0.13f, 0.16f, 0.98f);
            }
            if (cardHeaderImage != null)
            {
                cardHeaderImage.color = new Color(0.36f, 0.12f, 0.12f, 1f);
            }
            if (artworkImage != null)
            {
                artworkImage.color = new Color(0.35f, 0.35f, 0.38f, 0.80f);
            }
            if (titleText != null) titleText.color = new Color(0.70f, 0.65f, 0.65f, 0.85f);
            if (costText != null) costText.color = new Color(0.85f, 0.25f, 0.25f, 0.90f);
            if (descriptionText != null) descriptionText.color = new Color(0.55f, 0.55f, 0.58f, 0.70f);

            if (glowOutline != null)
            {
                glowOutline.enabled = true;
                glowOutline.effectColor = new Color(0.92f, 0.22f, 0.12f, 0.90f);
                glowOutline.effectDistance = new Vector2(6f, -6f);
            }
        }

        private GameObject EnsureBurnedStampOverlay()
        {
            Transform existing = transform.Find("BurnedStampOverlay");
            if (existing != null) return existing.gameObject;

            GameObject stampObj = new GameObject("BurnedStampOverlay");
            stampObj.transform.SetParent(transform, false);

            RectTransform rt = stampObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.04f, 0.36f);
            rt.anchorMax = new Vector2(0.96f, 0.64f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localEulerAngles = new Vector3(0f, 0f, -8f);

            Image stampBg = stampObj.AddComponent<Image>();
            stampBg.color = new Color(0.68f, 0.10f, 0.10f, 0.94f);

            Outline outline = stampObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.02f, 0.02f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);

            GameObject textObj = new GameObject("StampText");
            textObj.transform.SetParent(stampObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            Text stampText = textObj.AddComponent<Text>();
            stampText.font = ChessFontProvider.SerifBold;
            stampText.fontSize = 21;
            stampText.fontStyle = FontStyle.Bold;
            stampText.alignment = TextAnchor.MiddleCenter;
            stampText.color = new Color(1f, 0.94f, 0.55f, 1f);
            stampText.text = "¡CONSUMIDA!";

            return stampObj;
        }

        private void UpdateTargetTransform()
        {
            if (_isDisappeared) return;

            Vector3 offset = _isVerticalLayout
                ? new Vector3(hoverXOffset, 0, 0)
                : new Vector3(0, hoverYOffset, 0);

            if (_isPressed)
            {
                _targetScale = Vector3.one * 0.98f;
                _targetLocalPos = _originalLocalPos + (_isVerticalLayout ? new Vector3(hoverXOffset * 0.35f, 0, 0) : Vector3.zero);
            }
            else if (_isActivated)
            {
                _targetScale = Vector3.one * activeScale;
                _targetLocalPos = _originalLocalPos + (offset * 1.35f);
            }
            else if (_isHovered)
            {
                _targetScale = Vector3.one * hoverScale;
                _targetLocalPos = _originalLocalPos + offset;
            }
            else
            {
                _targetScale = Vector3.one;
                _targetLocalPos = _originalLocalPos;
            }
        }

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

                float dist = 4.5f + 2.5f * pulse;
                glowOutline.effectDistance = new Vector2(dist, -dist);
                yield return null;
            }
        }

        #region Estilización y Construcción Visual Procedural Ampliada

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
        /// con jerarquía amplia, fuentes legibles, ajuste automático de texto e ilustración de gran formato.
        /// </summary>
        private void EnsureVisualComponents()
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            _rectTransform.sizeDelta = cardSize;

            // 1. Fondo de la carta
            if (cardBackgroundImage == null)
            {
                cardBackgroundImage = GetComponent<Image>();
                if (cardBackgroundImage == null) cardBackgroundImage = gameObject.AddComponent<Image>();
                cardBackgroundImage.color = new Color(0.10f, 0.12f, 0.17f, 0.98f); // Fondo pizarra oscuro
            }
            cardBackgroundImage.raycastTarget = true;

            // 2. Encabezado de color según rareza
            if (cardHeaderImage == null)
            {
                Transform headerTr = transform.Find("Header");
                if (headerTr == null)
                {
                    GameObject headerObj = new GameObject("Header");
                    headerObj.transform.SetParent(transform, false);
                    cardHeaderImage = headerObj.AddComponent<Image>();
                    cardHeaderImage.color = new Color(0.24f, 0.55f, 0.95f, 1f);
                }
                else
                {
                    cardHeaderImage = headerTr.GetComponent<Image>();
                }
            }
            if (cardHeaderImage != null)
            {
                RectTransform rt = cardHeaderImage.rectTransform;
                rt.anchorMin = new Vector2(0f, 0.78f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            // 3. Título de la Carta (con BestFit y ajuste multilínea para nombres largos)
            if (titleText == null)
            {
                Transform tTr = transform.Find("TitleText");
                if (tTr == null)
                {
                    GameObject tObj = new GameObject("TitleText");
                    tObj.transform.SetParent(transform, false);
                    titleText = tObj.AddComponent<Text>();
                }
                else
                {
                    titleText = tTr.GetComponent<Text>();
                }
            }
            if (titleText != null)
            {
                RectTransform rt = titleText.rectTransform;
                rt.anchorMin = new Vector2(0.04f, 0.78f);
                rt.anchorMax = new Vector2(0.74f, 0.99f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                titleText.font = ChessFontProvider.SerifBold;
                titleText.fontSize = 15;
                titleText.fontStyle = FontStyle.Bold;
                titleText.color = Color.white;
                titleText.alignment = TextAnchor.MiddleLeft;
                titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
                titleText.verticalOverflow = VerticalWrapMode.Truncate;
                titleText.resizeTextForBestFit = true;
                titleText.resizeTextMinSize = 9;
                titleText.resizeTextMaxSize = 15;
                titleText.lineSpacing = 0.92f;
            }

            // 4. Texto de Coste ($)
            if (costText == null)
            {
                Transform cTr = transform.Find("CostText");
                if (cTr == null)
                {
                    GameObject cObj = new GameObject("CostText");
                    cObj.transform.SetParent(transform, false);
                    costText = cObj.AddComponent<Text>();
                }
                else
                {
                    costText = cTr.GetComponent<Text>();
                }
            }
            if (costText != null)
            {
                RectTransform rt = costText.rectTransform;
                rt.anchorMin = new Vector2(0.74f, 0.78f);
                rt.anchorMax = new Vector2(0.96f, 0.99f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                costText.font = ChessFontProvider.SerifBold;
                costText.fontSize = 20;
                costText.fontStyle = FontStyle.Bold;
                costText.color = new Color(1f, 0.88f, 0.25f, 1f); // Dorado brillante
                costText.alignment = TextAnchor.MiddleRight;
            }

            // 5. Marco y Área de Ilustración de Gran Formato
            if (artworkImage == null)
            {
                Transform artContainerTr = transform.Find("ArtworkContainer");
                if (artContainerTr == null)
                {
                    GameObject containerObj = new GameObject("ArtworkContainer");
                    containerObj.transform.SetParent(transform, false);
                    RectTransform rt = containerObj.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.06f, 0.44f);
                    rt.anchorMax = new Vector2(0.94f, 0.80f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    Image bgImg = containerObj.AddComponent<Image>();
                    bgImg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f); // Marco pizarra oscuro

                    GameObject spriteObj = new GameObject("ArtworkSprite");
                    spriteObj.transform.SetParent(containerObj.transform, false);
                    RectTransform sRt = spriteObj.AddComponent<RectTransform>();
                    sRt.anchorMin = new Vector2(0.04f, 0.04f);
                    sRt.anchorMax = new Vector2(0.96f, 0.96f);
                    sRt.offsetMin = Vector2.zero;
                    sRt.offsetMax = Vector2.zero;

                    artworkImage = spriteObj.AddComponent<Image>();
                    artworkImage.preserveAspect = true;
                }
                else
                {
                    Transform spriteTr = artContainerTr.Find("ArtworkSprite");
                    artworkImage = spriteTr != null ? spriteTr.GetComponent<Image>() : artContainerTr.GetComponent<Image>();
                }
            }

            // 6. Texto de Descripción del Efecto (Centrado, legible, tamaño 13)
            if (descriptionText == null)
            {
                Transform dTr = transform.Find("DescriptionText");
                if (dTr == null)
                {
                    GameObject dObj = new GameObject("DescriptionText");
                    dObj.transform.SetParent(transform, false);
                    RectTransform rt = dObj.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.06f, 0.12f);
                    rt.anchorMax = new Vector2(0.94f, 0.43f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    descriptionText = dObj.AddComponent<Text>();
                    descriptionText.font = ChessFontProvider.SerifRegular;
                    descriptionText.fontSize = 13;
                    descriptionText.color = new Color(0.90f, 0.92f, 0.96f, 1f);
                    descriptionText.alignment = TextAnchor.MiddleCenter;
                    descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
                    descriptionText.resizeTextForBestFit = true;
                    descriptionText.resizeTextMinSize = 11;
                    descriptionText.resizeTextMaxSize = 15;
                    descriptionText.lineSpacing = 1.05f;
                }
                else
                {
                    descriptionText = dTr.GetComponent<Text>();
                    descriptionText.font = ChessFontProvider.SerifRegular;
                    descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    descriptionText.resizeTextForBestFit = true;
                    descriptionText.resizeTextMinSize = 11;
                    descriptionText.resizeTextMaxSize = 15;
                }
            }

            // 7. Rareza (Badge inferior centrado)
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
                    rarityText.font = ChessFontProvider.SerifBold;
                    rarityText.fontSize = 12;
                    rarityText.fontStyle = FontStyle.Bold;
                    rarityText.alignment = TextAnchor.MiddleCenter;
                }
                else
                {
                    rarityText = rTr.GetComponent<Text>();
                    rarityText.font = ChessFontProvider.SerifBold;
                }
            }
        }

        private static Font GetDefaultUiFont(bool bold = true)
        {
            return ChessFontProvider.GetFont(bold);
        }

        #endregion
    }
}

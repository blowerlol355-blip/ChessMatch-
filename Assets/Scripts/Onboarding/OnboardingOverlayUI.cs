using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChessBalatro
{
    /// <summary>
    /// Capa visual superior (Canvas sortingOrder = 999 + soporte híbrido para menús OnGUI)
    /// del Tutorial Guiado de Primera Vez:
    /// - Dimmer oscuro semitransparente con recorte dinámico (Spotlight) sobre RectTransform, casilla o rect OnGUI.
    /// - Puntero/flecha estilo pixel art dorado (<see cref="PointerArrow"/>) con rebote continuo <c>Mathf.Sin(Time.time * 6f) * 12f</c>.
    /// - Caja de diálogo arcade con retrato pixel art de "Maestro Guía", efecto typewriter y botón "Siguiente ►".
    /// - Botón discreto "Saltar Tutorial ⏭" en la esquina superior derecha.
    /// </summary>
    public class OnboardingOverlayUI : MonoBehaviour
    {
        public static OnboardingOverlayUI Instance { get; private set; }

        private const int OverlaySortingOrder = 999;

        private OnboardingStepPresentation _presentation;
        private bool _isVisible = false;
        private bool _isTyping = false;
        private string _fullBodyText = string.Empty;
        private string _visibleBodyText = string.Empty;
        private Coroutine _typewriterCoroutine;

        // Geometría actual calculada en coordenadas de pantalla Top-Left
        private bool _hasSpotlight = false;
        private Rect _spotlightRect;
        private Rect _dialogueRect;
        private Rect _skipButtonRect;
        private Rect _continueButtonRect;
        private Rect _arrowRect;
        private float _arrowImguiAngle = 0f;
        private float _arrowUguiAngle = 0f;
        private Vector2 _arrowBaseCenter;
        private Vector2 _arrowDirToTarget;
        private bool _mirrorOnGui = false;
        private float _pulsePhase = 0f;

        // Texturas pixel art procedurales (FilterMode.Point)
        private Texture2D _pointerArrowTex;
        private Sprite _pointerArrowSprite;
        private Texture2D _mentorPortraitTex;
        private Sprite _mentorPortraitSprite;
        private Texture2D _dimmerTex;
        private Texture2D _dialogueBgTex;
        private Texture2D _skipBgTex;
        private Texture2D _goldBtnTex;

        // Jerarquía Canvas uGUI (sortingOrder = 999)
        private Canvas _overlayCanvas;
        private CanvasGroup _canvasGroup;
        private RectTransform _canvasRootRect;
        private RectTransform _dimTopRect;
        private RectTransform _dimBottomRect;
        private RectTransform _dimLeftRect;
        private RectTransform _dimRightRect;
        private Image _dimTopImg;
        private Image _dimBottomImg;
        private Image _dimLeftImg;
        private Image _dimRightImg;

        private RectTransform _spotTopEdge;
        private RectTransform _spotBottomEdge;
        private RectTransform _spotLeftEdge;
        private RectTransform _spotRightEdge;
        private Image[] _spotEdgeImages;

        private RectTransform _pointerArrowRect;
        private Image _pointerArrowImage;

        private RectTransform _dialoguePanelRect;
        private Image _mentorPortraitImage;
        private Text _badgeText;
        private Text _titleText;
        private Text _bodyText;
        private Text _actionHintText;
        private Button _continueButton;
        private Text _continueButtonText;
        private Button _dialogueTapSkipTypingBtn;

        private RectTransform _skipTutorialRect;
        private Button _skipTutorialButton;

        // Estilos OnGUI cacheados para compatibilidad con pantallas IMGUI (Menú Principal y Draft Modal)
        private bool _guiStylesReady = false;
        private int _lastGuiWidth = 0;
        private int _lastGuiHeight = 0;
        private GUIStyle _guiBadgeStyle;
        private GUIStyle _guiTitleStyle;
        private GUIStyle _guiBodyStyle;
        private GUIStyle _guiHintStyle;
        private GUIStyle _guiContinueBtnStyle;
        private GUIStyle _guiSkipBtnStyle;
        private GUIStyle _guiMentorTagStyle;

        public Canvas OverlayCanvas => _overlayCanvas;
        public RectTransform PointerArrow => _pointerArrowRect;
        public bool IsVisible => _isVisible;

        public static bool IsPointerOverBlockingOverlay()
        {
            if (Instance == null || !Instance._isVisible || !OnboardingManager.IsOnboardingActive) return false;
            if (Event.current == null) return false;
            Vector2 mp = Event.current.mousePosition;
            return Instance._dialogueRect.Contains(mp) || Instance._skipButtonRect.Contains(mp);
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            CreateProceduralPixelArtTextures();
            EnsureCanvasInitialized();
            HideImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (_overlayCanvas != null)
            {
                Destroy(_overlayCanvas.gameObject);
            }

            DestroyProceduralAssets();
        }

        public void PresentStep(OnboardingStepPresentation presentation)
        {
            _presentation = presentation;
            _isVisible = presentation.Step != OnboardingStep.None;

            if (!_isVisible)
            {
                HideImmediate();
                return;
            }

            EnsureCanvasInitialized();

            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
            }

            _fullBodyText = presentation.Body ?? string.Empty;
            _visibleBodyText = string.Empty;
            _isTyping = !string.IsNullOrEmpty(_fullBodyText);

            UpdateStaticTexts();
            RefreshLayoutForCurrentFrame();

            if (_isTyping)
            {
                _typewriterCoroutine = StartCoroutine(TypewriterRoutine(_fullBodyText));
            }
        }

        public void HideImmediate()
        {
            _isVisible = false;
            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
                _typewriterCoroutine = null;
            }
            _isTyping = false;

            if (_overlayCanvas != null)
            {
                _overlayCanvas.gameObject.SetActive(false);
            }
        }

        private void CompleteTypewriterImmediately()
        {
            if (!_isTyping) return;

            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
                _typewriterCoroutine = null;
            }

            _isTyping = false;
            _visibleBodyText = _fullBodyText;
            if (_bodyText != null)
            {
                _bodyText.text = _visibleBodyText;
            }
        }

        private IEnumerator TypewriterRoutine(string fullText)
        {
            _isTyping = true;
            _visibleBodyText = string.Empty;

            const float charsPerSecond = 54f;
            float charAccumulator = 0f;
            int shownChars = 0;

            while (shownChars < fullText.Length)
            {
                charAccumulator += Time.unscaledDeltaTime * charsPerSecond;
                int targetChars = Mathf.Min(fullText.Length, Mathf.FloorToInt(charAccumulator));
                if (targetChars > shownChars)
                {
                    shownChars = targetChars;
                    _visibleBodyText = fullText.Substring(0, shownChars);
                    if (_bodyText != null)
                    {
                        _bodyText.text = _visibleBodyText;
                    }
                }
                yield return null;
            }

            _visibleBodyText = fullText;
            if (_bodyText != null)
            {
                _bodyText.text = fullText;
            }
            _isTyping = false;
            _typewriterCoroutine = null;
        }

        private void Update()
        {
            if (!_isVisible || !OnboardingManager.IsOnboardingActive)
            {
                if (_overlayCanvas != null && _overlayCanvas.gameObject.activeSelf)
                {
                    _overlayCanvas.gameObject.SetActive(false);
                }
                return;
            }

            _pulsePhase = Time.unscaledTime;
            RefreshLayoutForCurrentFrame();
        }

        private void RefreshLayoutForCurrentFrame()
        {
            if (_overlayCanvas == null) EnsureCanvasInitialized();

            if (!_overlayCanvas.gameObject.activeSelf)
            {
                _overlayCanvas.gameObject.SetActive(true);
            }

            _mirrorOnGui = DetermineIfFullscreenImGuiIsActive();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = _mirrorOnGui ? 0f : 1f;
                _canvasGroup.blocksRaycasts = !_mirrorOnGui;
                _canvasGroup.interactable = !_mirrorOnGui;
            }

            ComputeGeometryForCurrentPhase();
            SyncUguiElementsToComputedGeometry();
        }

        private bool DetermineIfFullscreenImGuiIsActive()
        {
            if (MainMenuManager.Instance != null && MainMenuManager.Instance.IsRenderingOnGui)
            {
                return true;
            }

            if (RewardDraftModalUI.Instance != null && RewardDraftModalUI.Instance.IsModalOpen)
            {
                return true;
            }

            return false;
        }

        #region Cálculo de Geometría (Spotlight, PointerArrow, DialogueBox)

        private void ComputeGeometryForCurrentPhase()
        {
            float screenW = Mathf.Max(1f, Screen.width);
            float screenH = Mathf.Max(1f, Screen.height);
            float scale = ChessFontProvider.GetResponsiveScale();

            float safeLeft = Mathf.Max(0f, Screen.safeArea.xMin);
            float safeRight = Mathf.Max(0f, screenW - Screen.safeArea.xMax);
            float safeTop = Mathf.Max(0f, screenH - Screen.safeArea.yMax);
            float safeBottom = Mathf.Max(0f, Screen.safeArea.yMin);

            // 1. Botón discreto superior derecho ("Saltar Tutorial ⏭")
            float skipW = Mathf.Clamp(172f * scale, 148f, 236f);
            float skipH = Mathf.Clamp(34f * scale, 30f, 46f);
            _skipButtonRect = new Rect(screenW - safeRight - skipW - 14f, safeTop + 10f, skipW, skipH);

            // 2. Spotlight target en coordenadas Top-Left de pantalla
            _hasSpotlight = TryResolveTargetScreenRect(out Rect rawTargetRect);
            if (_hasSpotlight)
            {
                float pad = (_presentation.SubPhase == OnboardingSubPhase.BoardSelectPiece ||
                             _presentation.SubPhase == OnboardingSubPhase.BoardMovePiece ||
                             _presentation.SubPhase == OnboardingSubPhase.CaptureEnemyPiece)
                    ? Mathf.Clamp(6f * scale, 4f, 10f)
                    : Mathf.Clamp(8f * scale, 6f, 14f);

                _spotlightRect = new Rect(
                    Mathf.Clamp(rawTargetRect.x - pad, 0f, screenW - 8f),
                    Mathf.Clamp(rawTargetRect.y - pad, 0f, screenH - 8f),
                    Mathf.Min(rawTargetRect.width + pad * 2f, screenW),
                    Mathf.Min(rawTargetRect.height + pad * 2f, screenH)
                );
            }
            else
            {
                _spotlightRect = new Rect(0, 0, 0, 0);
            }

            // 3. PointerArrow posicionamiento + ángulo hacia el objetivo + rebote continuo
            float arrowSize = Mathf.Clamp(44f * scale, 36f, 64f);
            float bounceOffset = Mathf.Sin(Time.time * 6f) * 12f;

            if (_hasSpotlight)
            {
                ComputeBestArrowPlacement(_spotlightRect, screenW, screenH, safeLeft, safeRight, safeTop, safeBottom, arrowSize, scale);
                Vector2 animCenter = _arrowBaseCenter + _arrowDirToTarget * (bounceOffset * (scale * 0.85f));
                _arrowRect = new Rect(
                    animCenter.x - arrowSize * 0.5f,
                    animCenter.y - arrowSize * 0.5f,
                    arrowSize,
                    arrowSize
                );
            }
            else
            {
                _arrowRect = Rect.zero;
            }

            // 4. Caja de diálogo arcade de "Maestro Guía" sin solapar el Spotlight ni el tablero guiado
            float dlgW = Mathf.Min(screenW - safeLeft - safeRight - 24f, 670f * scale);
            float dlgH = Mathf.Clamp(212f * scale, 188f, 295f);
            _dialogueRect = ComputeBestDialogueRect(dlgW, dlgH, screenW, screenH, safeLeft, safeRight, safeTop, safeBottom, scale);

            float btnW = Mathf.Clamp(146f * scale, 120f, 200f);
            float btnH = Mathf.Clamp(38f * scale, 34f, 52f);
            _continueButtonRect = new Rect(
                _dialogueRect.xMax - btnW - (14f * scale),
                _dialogueRect.yMax - btnH - (12f * scale),
                btnW,
                btnH
            );
        }

        private bool TryResolveTargetScreenRect(out Rect result)
        {
            result = Rect.zero;
            OnboardingManager mgr = OnboardingManager.Instance;

            switch (_presentation.SubPhase)
            {
                case OnboardingSubPhase.MenuProfileIntro:
                    if (PlayerProfileBadgeUI.Instance != null && PlayerProfileBadgeUI.Instance.LastBadgeScreenRect.width > 10f)
                    {
                        result = PlayerProfileBadgeUI.Instance.LastBadgeScreenRect;
                        return true;
                    }
                    break;

                case OnboardingSubPhase.MenuArcadeIntro:
                    if (MainMenuManager.Instance != null && MainMenuManager.Instance.LastArcadeButtonRect.width > 10f)
                    {
                        result = MainMenuManager.Instance.LastArcadeButtonRect;
                        return true;
                    }
                    break;

                case OnboardingSubPhase.MenuEducationIntro:
                    if (MainMenuManager.Instance != null && MainMenuManager.Instance.LastEducationButtonRect.width > 10f)
                    {
                        result = MainMenuManager.Instance.LastEducationButtonRect;
                        return true;
                    }
                    break;

                case OnboardingSubPhase.MenuSkinsOptionsIntro:
                    if (MainMenuManager.Instance != null && MainMenuManager.Instance.LastSkinsAndOptionsRect.width > 10f)
                    {
                        result = MainMenuManager.Instance.LastSkinsAndOptionsRect;
                        return true;
                    }
                    break;

                case OnboardingSubPhase.MenuPlayButton:
                    if (MainMenuManager.Instance != null && MainMenuManager.Instance.LastPlayButtonRect.width > 10f)
                    {
                        result = MainMenuManager.Instance.LastPlayButtonRect;
                        return true;
                    }
                    break;

                case OnboardingSubPhase.SetupGrandMaster:
                    if (MainMenuManager.Instance != null && MainMenuManager.Instance.LastCardSelectorRect.width > 10f)
                    {
                        result = MainMenuManager.Instance.LastCardSelectorRect;
                        return true;
                    }
                    break;

                case OnboardingSubPhase.SetupStartMatch:
                    if (MainMenuManager.Instance != null && MainMenuManager.Instance.LastStartMatchButtonRect.width > 10f)
                    {
                        result = MainMenuManager.Instance.LastStartMatchButtonRect;
                        return true;
                    }
                    break;

                case OnboardingSubPhase.BoardSelectPiece:
                    return TryProjectBoardCoordToScreenRect(OnboardingLogic.GuidedKnightStart, out result);

                case OnboardingSubPhase.BoardMovePiece:
                    return TryProjectBoardCoordToScreenRect(OnboardingLogic.GuidedKnightMid, out result);

                case OnboardingSubPhase.CaptureEnemyPiece:
                    return TryProjectBoardCoordToScreenRect(OnboardingLogic.GuidedRookTarget, out result);

                case OnboardingSubPhase.ExplainCaptureHud:
                    if (RewardDraftModalUI.Instance != null && RewardDraftModalUI.Instance.LastHudBarRect.width > 10f)
                    {
                        result = RewardDraftModalUI.Instance.LastHudBarRect;
                        return true;
                    }
                    break;

                case OnboardingSubPhase.SelectDraftCard:
                    if (RewardDraftModalUI.Instance != null)
                    {
                        if (RewardDraftModalUI.Instance.LastCardsAreaRect.width > 10f)
                        {
                            result = RewardDraftModalUI.Instance.LastCardsAreaRect;
                            return true;
                        }
                        if (RewardDraftModalUI.Instance.LastFirstCardRect.width > 10f)
                        {
                            result = RewardDraftModalUI.Instance.LastFirstCardRect;
                            return true;
                        }
                    }
                    break;

                case OnboardingSubPhase.ActivateBottomCard:
                    if (mgr != null)
                    {
                        CardView cv = mgr.GetPrimaryBottomCardView();
                        if (cv != null && TryProjectRectTransformToScreenRect(cv.GetComponent<RectTransform>(), cv.CardSize, out result))
                        {
                            return true;
                        }
                    }
                    break;

                case OnboardingSubPhase.CompletionMessage:
                    return false;
            }

            return false;
        }

        private bool TryProjectBoardCoordToScreenRect(BoardCoord coord, out Rect screenRect)
        {
            screenRect = Rect.zero;
            BoardView bv = OnboardingManager.Instance != null
                ? OnboardingManager.Instance.GetBoardView()
                : FindAnyObjectByType<BoardView>();
            if (bv == null) return false;

            Camera cam = Camera.main;
            if (cam == null) return false;

            Vector3 worldCenter = bv.CoordToWorldPosition(coord);
            float half = bv.TileSize * 0.5f;

            Vector3 bl = cam.WorldToScreenPoint(new Vector3(worldCenter.x - half, worldCenter.y - half, worldCenter.z));
            Vector3 tr = cam.WorldToScreenPoint(new Vector3(worldCenter.x + half, worldCenter.y + half, worldCenter.z));

            float xMin = Mathf.Min(bl.x, tr.x);
            float xMax = Mathf.Max(bl.x, tr.x);
            float yTop = Screen.height - Mathf.Max(bl.y, tr.y);
            float yBottom = Screen.height - Mathf.Min(bl.y, tr.y);

            screenRect = Rect.MinMaxRect(xMin, yTop, xMax, yBottom);
            return screenRect.width > 2f && screenRect.height > 2f;
        }

        private bool TryProjectRectTransformToScreenRect(RectTransform rt, Vector2 fallbackCanvasSize, out Rect screenRect)
        {
            screenRect = Rect.zero;
            if (rt == null) return false;

            Canvas parentCanvas = rt.GetComponentInParent<Canvas>();
            Camera uiCam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? parentCanvas.worldCamera
                : null;

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(uiCam, corners[i]);
                if (sp.x < minX) minX = sp.x;
                if (sp.x > maxX) maxX = sp.x;
                if (sp.y < minY) minY = sp.y;
                if (sp.y > maxY) maxY = sp.y;
            }

            float w = maxX - minX;
            float h = maxY - minY;

            // Si la carta recién robada aún está en escala 0 del PlayDrawAnimation, usar su tamaño base esperado
            float minExpectedW = Mathf.Max(40f, fallbackCanvasSize.x * 0.4f);
            float minExpectedH = Mathf.Max(60f, fallbackCanvasSize.y * 0.4f);
            if (w < minExpectedW || h < minExpectedH)
            {
                Vector2 centerScreen = RectTransformUtility.WorldToScreenPoint(uiCam, rt.position);
                float canvasScaleFactor = parentCanvas != null ? parentCanvas.scaleFactor : ChessFontProvider.GetResponsiveScale();
                w = fallbackCanvasSize.x * canvasScaleFactor;
                h = fallbackCanvasSize.y * canvasScaleFactor;
                minX = centerScreen.x - w * 0.5f;
                maxX = centerScreen.x + w * 0.5f;
                minY = centerScreen.y - h * 0.5f;
                maxY = centerScreen.y + h * 0.5f;
            }

            screenRect = Rect.MinMaxRect(minX, Screen.height - maxY, maxX, Screen.height - minY);
            return screenRect.width > 4f && screenRect.height > 4f;
        }

        private void ComputeBestArrowPlacement(
            Rect spot,
            float screenW,
            float screenH,
            float safeLeft,
            float safeRight,
            float safeTop,
            float safeBottom,
            float arrowSize,
            float scale)
        {
            float clearance = (arrowSize * 0.5f) + (18f * scale);

            // Preferir apuntar hacia la primera tarjeta concreta durante Paso 5 si conocemos su Rect
            Rect focusRect = spot;
            if (_presentation.SubPhase == OnboardingSubPhase.SelectDraftCard &&
                RewardDraftModalUI.Instance != null &&
                RewardDraftModalUI.Instance.LastFirstCardRect.width > 10f)
            {
                focusRect = RewardDraftModalUI.Instance.LastFirstCardRect;
            }

            float spaceAbove = focusRect.y - (safeTop + 42f * scale);
            float spaceBelow = (screenH - safeBottom) - focusRect.yMax;
            float spaceLeft = focusRect.x - safeLeft;
            float spaceRight = (screenW - safeRight) - focusRect.xMax;

            // En Barra HUD superior (Paso 4b) siempre colocar la flecha justo debajo apuntando hacia arriba
            if (_presentation.SubPhase == OnboardingSubPhase.ExplainCaptureHud && spaceBelow >= clearance)
            {
                _arrowBaseCenter = new Vector2(focusRect.center.x, focusRect.yMax + clearance);
                _arrowDirToTarget = Vector2.down; // en screen top-left Y apunta hacia arriba (-Y)
                _arrowImguiAngle = 180f;
                _arrowUguiAngle = 180f;
                return;
            }

            // En Carta inferior (Paso 6a) colocar encima apuntando hacia abajo
            if (_presentation.SubPhase == OnboardingSubPhase.ActivateBottomCard && spaceAbove >= clearance)
            {
                _arrowBaseCenter = new Vector2(focusRect.center.x, focusRect.y - clearance);
                _arrowDirToTarget = Vector2.up; // hacia abajo (+Y top-left) -> invertido abajo
                _arrowImguiAngle = 0f;
                _arrowUguiAngle = 0f;
                return;
            }

            // Por defecto: encima si hay espacio, si no izquierda, abajo o derecha
            if (spaceAbove >= clearance + 10f)
            {
                _arrowBaseCenter = new Vector2(Mathf.Clamp(focusRect.center.x, arrowSize, screenW - arrowSize), focusRect.y - clearance);
                _arrowDirToTarget = new Vector2(0f, 1f);
                _arrowImguiAngle = 0f;
                _arrowUguiAngle = 0f;
            }
            else if (spaceLeft >= clearance + 10f)
            {
                _arrowBaseCenter = new Vector2(focusRect.x - clearance, Mathf.Clamp(focusRect.center.y, arrowSize, screenH - arrowSize));
                _arrowDirToTarget = new Vector2(1f, 0f);
                _arrowImguiAngle = -90f;
                _arrowUguiAngle = 90f;
            }
            else if (spaceBelow >= clearance + 10f)
            {
                _arrowBaseCenter = new Vector2(Mathf.Clamp(focusRect.center.x, arrowSize, screenW - arrowSize), focusRect.yMax + clearance);
                _arrowDirToTarget = new Vector2(0f, -1f);
                _arrowImguiAngle = 180f;
                _arrowUguiAngle = 180f;
            }
            else
            {
                _arrowBaseCenter = new Vector2(Mathf.Min(screenW - arrowSize, focusRect.xMax + clearance), Mathf.Clamp(focusRect.center.y, arrowSize, screenH - arrowSize));
                _arrowDirToTarget = new Vector2(-1f, 0f);
                _arrowImguiAngle = 90f;
                _arrowUguiAngle = -90f;
            }
        }

        private Rect ComputeBestDialogueRect(
            float dlgW,
            float dlgH,
            float screenW,
            float screenH,
            float safeLeft,
            float safeRight,
            float safeTop,
            float safeBottom,
            float scale)
        {
            float centerX = Mathf.Clamp((screenW - dlgW) * 0.5f, safeLeft + 12f, screenW - safeRight - dlgW - 12f);

            // Pasos en pantalla de tablero o carta inferior: ubicar diálogo sin tapar las casillas clave ni la carta inferior
            if (!_hasSpotlight)
            {
                return new Rect(centerX, (screenH - dlgH) * 0.5f, dlgW, dlgH);
            }

            // En Paso 6a (carta abajo) -> colocar diálogo arriba con margen bajo Skip
            if (_presentation.SubPhase == OnboardingSubPhase.ActivateBottomCard)
            {
                float topBandY = safeTop + (54f * scale);
                return new Rect(centerX, topBandY, dlgW, dlgH);
            }

            // En Paso 5 (Draft Modal abierto) -> colocar en banda inferior reservada debajo del Spotlight
            if (_presentation.SubPhase == OnboardingSubPhase.SelectDraftCard)
            {
                float bottomY = Mathf.Max(safeTop + 56f * scale, screenH - safeBottom - dlgH - (14f * scale));
                return new Rect(centerX, bottomY, dlgW, dlgH);
            }

            // Para tablero (Pasos 3 y 4a): despejar completamente las casillas b1/c3/d5 dejando diálogo arriba o abajo
            bool isBoardStep = _presentation.SubPhase == OnboardingSubPhase.BoardSelectPiece ||
                               _presentation.SubPhase == OnboardingSubPhase.BoardMovePiece ||
                               _presentation.SubPhase == OnboardingSubPhase.CaptureEnemyPiece;
            if (isBoardStep)
            {
                bool isTargetInLowerHalf = _spotlightRect.center.y > screenH * 0.48f;
                float topY = safeTop + (54f * scale);
                float botY = screenH - safeBottom - dlgH - (14f * scale);

                // Elegir la banda opuesta a la casilla resaltada, siempre evitando solapar el spotlight
                Rect preferred = isTargetInLowerHalf
                    ? new Rect(centerX, topY, dlgW, dlgH)
                    : new Rect(centerX, botY, dlgW, dlgH);
                Rect expandedSpot = ExpandRect(_spotlightRect, 54f * scale);
                if (!preferred.Overlaps(expandedSpot))
                {
                    return preferred;
                }

                Rect alt = isTargetInLowerHalf
                    ? new Rect(centerX, botY, dlgW, dlgH)
                    : new Rect(centerX, topY, dlgW, dlgH);
                if (!alt.Overlaps(expandedSpot))
                {
                    return alt;
                }
            }

            // Candidatos generales: debajo de spotlight (+flecha), arriba de spotlight, pie inferior, cabecera superior
            float arrowMargin = 62f * scale;
            Rect candBottomScreen = new Rect(centerX, screenH - safeBottom - dlgH - (16f * scale), dlgW, dlgH);
            Rect candBelowSpot = new Rect(centerX, _spotlightRect.yMax + arrowMargin, dlgW, dlgH);
            Rect candTopScreen = new Rect(centerX, safeTop + (56f * scale), dlgW, dlgH);
            Rect candAboveSpot = new Rect(centerX, _spotlightRect.y - dlgH - arrowMargin, dlgW, dlgH);

            Rect[] candidates = { candBottomScreen, candBelowSpot, candTopScreen, candAboveSpot };
            Rect avoidSpot = ExpandRect(_spotlightRect, 20f * scale);
            Rect avoidSkip = ExpandRect(_skipButtonRect, 12f * scale);

            Rect bestRect = candBottomScreen;
            float lowestOverlap = float.MaxValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                Rect c = candidates[i];
                if (c.y < safeTop + 8f || c.yMax > screenH - safeBottom - 8f) continue;

                float penalty = OverlapArea(c, avoidSpot) + OverlapArea(c, avoidSkip);
                if (_arrowRect.width > 1f)
                {
                    penalty += OverlapArea(c, ExpandRect(_arrowRect, 8f));
                }

                if (penalty < lowestOverlap)
                {
                    lowestOverlap = penalty;
                    bestRect = c;
                    if (penalty <= 0.1f) break;
                }
            }

            bestRect.y = Mathf.Clamp(bestRect.y, safeTop + 10f, screenH - safeBottom - dlgH - 10f);
            return bestRect;
        }

        private static Rect ExpandRect(Rect r, float amount)
        {
            return new Rect(r.x - amount, r.y - amount, r.width + amount * 2f, r.height + amount * 2f);
        }

        private static float OverlapArea(Rect a, Rect b)
        {
            float xOverlap = Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin));
            float yOverlap = Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));
            return xOverlap * yOverlap;
        }

        #endregion

        #region Construcción y Sincronización del Canvas uGUI (sortingOrder = 999)

        private void EnsureCanvasInitialized()
        {
            if (_overlayCanvas != null) return;

            EnsureEventSystemExists();

            GameObject canvasObj = new GameObject("Onboarding_Canvas_999");
            canvasObj.transform.SetParent(transform, false);

            _overlayCanvas = canvasObj.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = OverlaySortingOrder;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            _canvasGroup = canvasObj.AddComponent<CanvasGroup>();

            _canvasRootRect = canvasObj.GetComponent<RectTransform>();

            // 1. 4 paneles Dimmer para formar el hueco Spotlight
            Color dimColor = new Color(0.02f, 0.03f, 0.07f, 0.74f);
            _dimTopRect = CreateFullAnchorImage("Dimmer_Top", canvasObj.transform, dimColor, true, out _dimTopImg);
            _dimBottomRect = CreateFullAnchorImage("Dimmer_Bottom", canvasObj.transform, dimColor, true, out _dimBottomImg);
            _dimLeftRect = CreateFullAnchorImage("Dimmer_Left", canvasObj.transform, dimColor, true, out _dimLeftImg);
            _dimRightRect = CreateFullAnchorImage("Dimmer_Right", canvasObj.transform, dimColor, true, out _dimRightImg);

            // 2. Marco dorado del hueco Spotlight (raycastTarget = false)
            Color goldCol = new Color(1.0f, 0.84f, 0.16f, 1f);
            _spotEdgeImages = new Image[4];
            _spotTopEdge = CreateFullAnchorImage("SpotFrame_Top", canvasObj.transform, goldCol, false, out _spotEdgeImages[0]);
            _spotBottomEdge = CreateFullAnchorImage("SpotFrame_Bottom", canvasObj.transform, goldCol, false, out _spotEdgeImages[1]);
            _spotLeftEdge = CreateFullAnchorImage("SpotFrame_Left", canvasObj.transform, goldCol, false, out _spotEdgeImages[2]);
            _spotRightEdge = CreateFullAnchorImage("SpotFrame_Right", canvasObj.transform, goldCol, false, out _spotEdgeImages[3]);

            // 3. Puntero Pixel Art Dorado (PointerArrow)
            GameObject arrowObj = new GameObject("PointerArrow");
            arrowObj.transform.SetParent(canvasObj.transform, false);
            _pointerArrowRect = arrowObj.AddComponent<RectTransform>();
            _pointerArrowImage = arrowObj.AddComponent<Image>();
            _pointerArrowImage.sprite = _pointerArrowSprite;
            _pointerArrowImage.preserveAspect = true;
            _pointerArrowImage.raycastTarget = false;

            // 4. Caja de Diálogo Arcade ("Maestro Guía")
            BuildUguiDialogueBox(canvasObj.transform);

            // 5. Botón Discreto "Saltar Tutorial ⏭" en esquina superior derecha
            BuildUguiSkipButton(canvasObj.transform);
        }

        private RectTransform CreateFullAnchorImage(string name, Transform parent, Color color, bool raycastTarget, out Image img)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycastTarget;
            return rt;
        }

        private void BuildUguiDialogueBox(Transform parent)
        {
            GameObject boxObj = new GameObject("DialogueBox");
            boxObj.transform.SetParent(parent, false);
            _dialoguePanelRect = boxObj.AddComponent<RectTransform>();

            Image bg = boxObj.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.14f, 0.96f);
            bg.raycastTarget = true;

            Outline border = boxObj.AddComponent<Outline>();
            border.effectColor = new Color(0.95f, 0.78f, 0.22f, 1f);
            border.effectDistance = new Vector2(3f, -3f);

            // Tap sobre el diálogo acelera typewriter si aún está escribiendo
            _dialogueTapSkipTypingBtn = boxObj.AddComponent<Button>();
            _dialogueTapSkipTypingBtn.transition = Selectable.Transition.None;
            _dialogueTapSkipTypingBtn.onClick.AddListener(CompleteTypewriterImmediately);

            // Retrato Maestro Guía (izquierda)
            GameObject portraitFrame = new GameObject("MaestroPortraitFrame");
            portraitFrame.transform.SetParent(boxObj.transform, false);
            RectTransform pfRt = portraitFrame.AddComponent<RectTransform>();
            pfRt.anchorMin = new Vector2(0f, 1f);
            pfRt.anchorMax = new Vector2(0f, 1f);
            pfRt.pivot = new Vector2(0f, 1f);
            pfRt.anchoredPosition = new Vector2(20f, -20f);
            pfRt.sizeDelta = new Vector2(126f, 126f);

            Image pfBg = portraitFrame.AddComponent<Image>();
            pfBg.color = new Color(0.11f, 0.15f, 0.24f, 1f);
            pfBg.raycastTarget = false;
            Outline pfBorder = portraitFrame.AddComponent<Outline>();
            pfBorder.effectColor = new Color(0.95f, 0.78f, 0.22f, 1f);
            pfBorder.effectDistance = new Vector2(2.5f, -2.5f);

            GameObject portraitObj = new GameObject("MaestroPortrait");
            portraitObj.transform.SetParent(portraitFrame.transform, false);
            RectTransform pRt = portraitObj.AddComponent<RectTransform>();
            pRt.anchorMin = Vector2.zero;
            pRt.anchorMax = Vector2.one;
            pRt.offsetMin = new Vector2(4f, 4f);
            pRt.offsetMax = new Vector2(-4f, -4f);

            _mentorPortraitImage = portraitObj.AddComponent<Image>();
            _mentorPortraitImage.sprite = _mentorPortraitSprite;
            _mentorPortraitImage.preserveAspect = true;
            _mentorPortraitImage.raycastTarget = false;

            // Etiqueta bajo retrato
            Text portraitLabel = CreateUguiText("MaestroLabel", boxObj.transform, ChessFontProvider.SerifBold, 17, TextAnchor.MiddleCenter, new Color(0.98f, 0.86f, 0.34f));
            RectTransform plRt = portraitLabel.GetComponent<RectTransform>();
            plRt.anchorMin = new Vector2(0f, 1f);
            plRt.anchorMax = new Vector2(0f, 1f);
            plRt.pivot = new Vector2(0f, 1f);
            plRt.anchoredPosition = new Vector2(14f, -152f);
            plRt.sizeDelta = new Vector2(138f, 26f);
            portraitLabel.text = "MAESTRO GUÍA";

            // Textos a la derecha del retrato
            _badgeText = CreateUguiText("StepBadge", boxObj.transform, ChessFontProvider.SerifBold, 20, TextAnchor.MiddleLeft, new Color(0.45f, 0.88f, 1f));
            SetLeftStretchTop(_badgeText.rectTransform, 166f, 20f, -16f, 28f);

            _titleText = CreateUguiText("StepTitle", boxObj.transform, ChessFontProvider.SerifBold, 28, TextAnchor.MiddleLeft, new Color(1f, 0.90f, 0.38f));
            SetLeftStretchTop(_titleText.rectTransform, 166f, 20f, -46f, 36f);

            _bodyText = CreateUguiText("StepBody", boxObj.transform, ChessFontProvider.SerifRegular, 24, TextAnchor.UpperLeft, new Color(0.92f, 0.94f, 0.98f));
            _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform bodyRt = _bodyText.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(166f, 66f);
            bodyRt.offsetMax = new Vector2(-20f, -86f);

            _actionHintText = CreateUguiText("ActionHint", boxObj.transform, ChessFontProvider.SerifBold, 22, TextAnchor.MiddleLeft, new Color(0.40f, 0.96f, 0.68f));
            RectTransform hintRt = _actionHintText.rectTransform;
            hintRt.anchorMin = new Vector2(0f, 0f);
            hintRt.anchorMax = new Vector2(1f, 0f);
            hintRt.pivot = new Vector2(0f, 0f);
            hintRt.offsetMin = new Vector2(166f, 14f);
            hintRt.offsetMax = new Vector2(-20f, 54f);

            // Botón "Siguiente ►"
            GameObject btnObj = new GameObject("ContinueButton");
            btnObj.transform.SetParent(boxObj.transform, false);
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 0f);
            btnRt.anchorMax = new Vector2(1f, 0f);
            btnRt.pivot = new Vector2(1f, 0f);
            btnRt.anchoredPosition = new Vector2(-18f, 14f);
            btnRt.sizeDelta = new Vector2(210f, 48f);

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = new Color(0.88f, 0.68f, 0.16f, 1f);
            Outline btnOutline = btnObj.AddComponent<Outline>();
            btnOutline.effectColor = new Color(1f, 0.95f, 0.65f, 1f);
            btnOutline.effectDistance = new Vector2(2f, -2f);

            _continueButton = btnObj.AddComponent<Button>();
            _continueButton.onClick.AddListener(HandleContinueClicked);

            _continueButtonText = CreateUguiText("ContinueLabel", btnObj.transform, ChessFontProvider.SerifBold, 22, TextAnchor.MiddleCenter, new Color(0.08f, 0.06f, 0.02f));
            _continueButtonText.rectTransform.anchorMin = Vector2.zero;
            _continueButtonText.rectTransform.anchorMax = Vector2.one;
            _continueButtonText.rectTransform.offsetMin = Vector2.zero;
            _continueButtonText.rectTransform.offsetMax = Vector2.zero;
        }

        private void BuildUguiSkipButton(Transform parent)
        {
            GameObject skipObj = new GameObject("SkipTutorialButton");
            skipObj.transform.SetParent(parent, false);
            _skipTutorialRect = skipObj.AddComponent<RectTransform>();

            Image bg = skipObj.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.11f, 0.18f, 0.90f);

            Outline outline = skipObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.70f, 0.26f, 0.90f);
            outline.effectDistance = new Vector2(2f, -2f);

            _skipTutorialButton = skipObj.AddComponent<Button>();
            _skipTutorialButton.onClick.AddListener(HandleSkipClicked);

            Text skipTxt = CreateUguiText("SkipLabel", skipObj.transform, ChessFontProvider.SerifBold, 19, TextAnchor.MiddleCenter, new Color(0.96f, 0.90f, 0.64f));
            skipTxt.text = "Saltar Tutorial ⏭";
            skipTxt.rectTransform.anchorMin = Vector2.zero;
            skipTxt.rectTransform.anchorMax = Vector2.one;
            skipTxt.rectTransform.offsetMin = new Vector2(8f, 2f);
            skipTxt.rectTransform.offsetMax = new Vector2(-8f, -2f);
        }

        private static Text CreateUguiText(string name, Transform parent, Font font, int fontSize, TextAnchor alignment, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            Text t = go.AddComponent<Text>();
            t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = color;
            t.raycastTarget = false;
            return t;
        }

        private static void SetLeftStretchTop(RectTransform rt, float left, float right, float topOffset, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, topOffset - height);
            rt.offsetMax = new Vector2(-right, topOffset);
        }

        private void UpdateStaticTexts()
        {
            if (_badgeText != null) _badgeText.text = _presentation.BadgeText;
            if (_titleText != null) _titleText.text = _presentation.Title;
            if (_bodyText != null) _bodyText.text = _visibleBodyText;

            bool showContinue = !_presentation.IsInteractive;
            if (_continueButton != null)
            {
                _continueButton.gameObject.SetActive(showContinue);
            }
            if (_continueButtonText != null)
            {
                _continueButtonText.text = string.IsNullOrEmpty(_presentation.ContinueButtonLabel)
                    ? "Siguiente ►"
                    : _presentation.ContinueButtonLabel;
            }
            if (_actionHintText != null)
            {
                _actionHintText.gameObject.SetActive(_presentation.IsInteractive);
                _actionHintText.text = string.IsNullOrEmpty(_presentation.ActionHintText)
                    ? string.Empty
                    : $"✦ {_presentation.ActionHintText}";
            }
        }

        private void SyncUguiElementsToComputedGeometry()
        {
            float sw = Mathf.Max(1f, Screen.width);
            float sh = Mathf.Max(1f, Screen.height);

            // 1. 4 paneles Dimmer (anclas normalizadas)
            if (_hasSpotlight && _spotlightRect.width > 2f && _spotlightRect.height > 2f)
            {
                float nxMin = Mathf.Clamp01(_spotlightRect.xMin / sw);
                float nxMax = Mathf.Clamp01(_spotlightRect.xMax / sw);
                float nyMax = Mathf.Clamp01(1f - (_spotlightRect.yMin / sh));
                float nyMin = Mathf.Clamp01(1f - (_spotlightRect.yMax / sh));

                SetNormalizedRect(_dimTopRect, 0f, nyMax, 1f, 1f);
                SetNormalizedRect(_dimBottomRect, 0f, 0f, 1f, nyMin);
                SetNormalizedRect(_dimLeftRect, 0f, nyMin, nxMin, nyMax);
                SetNormalizedRect(_dimRightRect, nxMax, nyMin, 1f, nyMax);

                _dimTopImg.enabled = nyMax < 0.999f;
                _dimBottomImg.enabled = nyMin > 0.001f;
                _dimLeftImg.enabled = nxMin > 0.001f && nyMax > nyMin;
                _dimRightImg.enabled = nxMax < 0.999f && nyMax > nyMin;

                float bw = Mathf.Clamp(3f / sw, 0.0015f, 0.006f);
                float bh = Mathf.Clamp(3f / sh, 0.0015f, 0.006f);
                SetNormalizedRect(_spotTopEdge, nxMin, Mathf.Max(0f, nyMax - bh), nxMax, nyMax);
                SetNormalizedRect(_spotBottomEdge, nxMin, nyMin, nxMax, Mathf.Min(1f, nyMin + bh));
                SetNormalizedRect(_spotLeftEdge, nxMin, nyMin, Mathf.Min(1f, nxMin + bw), nyMax);
                SetNormalizedRect(_spotRightEdge, Mathf.Max(0f, nxMax - bw), nyMin, nxMax, nyMax);

                float pulseAlpha = 0.72f + 0.28f * Mathf.Sin(_pulsePhase * 5f);
                Color frameCol = new Color(1.0f, 0.85f, 0.18f, pulseAlpha);
                for (int i = 0; i < _spotEdgeImages.Length; i++)
                {
                    _spotEdgeImages[i].enabled = true;
                    _spotEdgeImages[i].color = frameCol;
                }
            }
            else
            {
                // Pantalla completa atenuada cuando no hay spotlight específico (ej. mensaje final)
                SetNormalizedRect(_dimTopRect, 0f, 0f, 1f, 1f);
                _dimTopImg.enabled = true;
                _dimBottomImg.enabled = false;
                _dimLeftImg.enabled = false;
                _dimRightImg.enabled = false;
                for (int i = 0; i < _spotEdgeImages.Length; i++)
                {
                    _spotEdgeImages[i].enabled = false;
                }
            }

            // 2. PointerArrow
            if (_pointerArrowRect != null)
            {
                bool showArrow = _hasSpotlight && _arrowRect.width > 2f;
                _pointerArrowImage.enabled = showArrow;
                if (showArrow)
                {
                    SetNormalizedRectFromScreenTopLeft(_pointerArrowRect, _arrowRect, sw, sh);
                    _pointerArrowRect.localEulerAngles = new Vector3(0f, 0f, _arrowUguiAngle);
                }
            }

            // 3. DialogueBox & Skip Button
            SetNormalizedRectFromScreenTopLeft(_dialoguePanelRect, _dialogueRect, sw, sh);
            SetNormalizedRectFromScreenTopLeft(_skipTutorialRect, _skipButtonRect, sw, sh);

            // Actualizar visibilidad interactiva según estado
            if (_bodyText != null) _bodyText.text = _visibleBodyText;
        }

        private static void SetNormalizedRectFromScreenTopLeft(RectTransform rt, Rect screenTopLeft, float sw, float sh)
        {
            if (rt == null) return;
            float nxMin = Mathf.Clamp01(screenTopLeft.xMin / sw);
            float nxMax = Mathf.Clamp01(screenTopLeft.xMax / sw);
            float nyMax = Mathf.Clamp01(1f - (screenTopLeft.yMin / sh));
            float nyMin = Mathf.Clamp01(1f - (screenTopLeft.yMax / sh));
            SetNormalizedRect(rt, nxMin, nyMin, nxMax, nyMax);
        }

        private static void SetNormalizedRect(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void HandleContinueClicked()
        {
            if (_isTyping)
            {
                CompleteTypewriterImmediately();
                return;
            }
            OnboardingManager.Instance?.AdvanceInformativeStep();
        }

        private void HandleSkipClicked()
        {
            OnboardingManager.Instance?.SkipOnboarding();
        }

        private static void EnsureEventSystemExists()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                esObj.AddComponent<StandaloneInputModule>();
#endif
            }
        }

        #endregion

        #region Renderizado Híbrido OnGUI (Menús IMGUI y Draft Modal)

        private void OnGUI()
        {
            if (!_isVisible || !OnboardingManager.IsOnboardingActive || !_mirrorOnGui) return;

            GUI.depth = -2000;
            InitGuiStylesIfNeeded();

            float sw = Screen.width;
            float sh = Screen.height;
            float scale = ChessFontProvider.GetResponsiveScale();

            Color prevCol = GUI.color;

            // 1. 4 paneles Dimmer alrededor del Spotlight
            if (_hasSpotlight && _spotlightRect.width > 2f && _spotlightRect.height > 2f)
            {
                if (_spotlightRect.yMin > 0f)
                    GUI.DrawTexture(new Rect(0f, 0f, sw, _spotlightRect.yMin), _dimmerTex);
                if (_spotlightRect.yMax < sh)
                    GUI.DrawTexture(new Rect(0f, _spotlightRect.yMax, sw, sh - _spotlightRect.yMax), _dimmerTex);
                if (_spotlightRect.xMin > 0f)
                    GUI.DrawTexture(new Rect(0f, _spotlightRect.yMin, _spotlightRect.xMin, _spotlightRect.height), _dimmerTex);
                if (_spotlightRect.xMax < sw)
                    GUI.DrawTexture(new Rect(_spotlightRect.xMax, _spotlightRect.yMin, sw - _spotlightRect.xMax, _spotlightRect.height), _dimmerTex);

                // Borde dorado pulsante del hueco
                float pulseAlpha = 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 5f);
                GUI.color = new Color(1.0f, 0.85f, 0.18f, pulseAlpha);
                DrawGuiBorder(_spotlightRect, 3f);
                GUI.color = Color.white;
            }
            else
            {
                GUI.DrawTexture(new Rect(0f, 0f, sw, sh), _dimmerTex);
            }

            // 2. Caja de Diálogo Arcade "Maestro Guía"
            DrawGuiDialogueBox(scale);

            // 3. Flecha PointerArrow pixel art dorada con rebote
            if (_hasSpotlight && _arrowRect.width > 2f && _pointerArrowTex != null)
            {
                Matrix4x4 oldMatrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(_arrowImguiAngle, _arrowRect.center);
                GUI.DrawTexture(_arrowRect, _pointerArrowTex, ScaleMode.StretchToFill);
                GUI.matrix = oldMatrix;
            }

            // 4. Botón discreto superior derecho "Saltar Tutorial ⏭"
            GUI.color = Color.white;
            GUI.DrawTexture(_skipButtonRect, _skipBgTex);
            GUI.color = new Color(0.88f, 0.72f, 0.25f, 0.95f);
            DrawGuiBorder(_skipButtonRect, 2f);
            GUI.color = Color.white;
            if (GUI.Button(_skipButtonRect, "Saltar Tutorial ⏭", _guiSkipBtnStyle))
            {
                HandleSkipClicked();
            }

            // Bloquear clics perdidos fuera del hueco Spotlight, Diálogo y Botón Saltar cuando hay Overlay IMGUI activo
            Event ev = Event.current;
            if (ev != null && (ev.type == EventType.MouseDown || ev.type == EventType.MouseUp) && ev.button == 0)
            {
                Vector2 mp = ev.mousePosition;
                bool inAllowedZone = _dialogueRect.Contains(mp) ||
                                     _skipButtonRect.Contains(mp) ||
                                     (_hasSpotlight && _spotlightRect.Contains(mp) && _presentation.IsInteractive);
                if (!inAllowedZone)
                {
                    ev.Use();
                }
            }

            GUI.color = prevCol;
        }

        private void DrawGuiDialogueBox(float scale)
        {
            // Sombra dura 32-bits
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.90f);
            GUI.DrawTexture(new Rect(_dialogueRect.x + 4, _dialogueRect.y + 4, _dialogueRect.width, _dialogueRect.height), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.DrawTexture(_dialogueRect, _dialogueBgTex);

            GUI.color = new Color(0.95f, 0.78f, 0.22f, 1f);
            DrawGuiBorder(_dialogueRect, 3f);
            GUI.color = Color.white;

            float pad = 12f * scale;
            float portraitSize = Mathf.Clamp(78f * scale, 64f, 108f);
            Rect portraitBox = new Rect(_dialogueRect.x + pad, _dialogueRect.y + pad, portraitSize, portraitSize);

            GUI.color = new Color(0.11f, 0.15f, 0.24f, 1f);
            GUI.DrawTexture(portraitBox, Texture2D.whiteTexture);
            GUI.color = new Color(0.95f, 0.78f, 0.22f, 1f);
            DrawGuiBorder(portraitBox, 2f);
            GUI.color = Color.white;

            if (_mentorPortraitTex != null)
            {
                Rect innerP = new Rect(portraitBox.x + 3, portraitBox.y + 3, portraitBox.width - 6, portraitBox.height - 6);
                GUI.DrawTexture(innerP, _mentorPortraitTex, ScaleMode.StretchToFill);
            }

            Rect mentorTagRect = new Rect(portraitBox.x - 4f, portraitBox.yMax + (4f * scale), portraitBox.width + 8f, 18f * scale);
            GUI.Label(mentorTagRect, "MAESTRO GUÍA", _guiMentorTagStyle);

            float textX = portraitBox.xMax + (14f * scale);
            float textW = _dialogueRect.xMax - textX - pad;
            float curY = _dialogueRect.y + (10f * scale);

            GUI.Label(new Rect(textX, curY, textW, 20f * scale), _presentation.BadgeText, _guiBadgeStyle);
            curY += 20f * scale;

            GUI.Label(new Rect(textX, curY, textW, 26f * scale), _presentation.Title, _guiTitleStyle);
            curY += 28f * scale;

            float bottomStripH = 42f * scale;
            float bodyH = Mathf.Max(42f, _dialogueRect.yMax - curY - bottomStripH - (8f * scale));
            Rect bodyRect = new Rect(textX, curY, textW, bodyH);
            GUI.Label(bodyRect, _visibleBodyText, _guiBodyStyle);

            if (!_presentation.IsInteractive)
            {
                GUI.DrawTexture(_continueButtonRect, _goldBtnTex);
                GUI.color = new Color(1f, 0.94f, 0.56f, 1f);
                DrawGuiBorder(_continueButtonRect, 2f);
                GUI.color = Color.white;

                string btnLabel = string.IsNullOrEmpty(_presentation.ContinueButtonLabel)
                    ? "Siguiente ►"
                    : _presentation.ContinueButtonLabel;

                if (GUI.Button(_continueButtonRect, btnLabel, _guiContinueBtnStyle))
                {
                    HandleContinueClicked();
                }
            }
            else if (!string.IsNullOrEmpty(_presentation.ActionHintText))
            {
                Rect hintRect = new Rect(textX, _dialogueRect.yMax - (36f * scale), textW, 26f * scale);
                GUI.Label(hintRect, $"✦ {_presentation.ActionHintText}", _guiHintStyle);
            }

            // Tocar el diálogo completa el typewriter si seguía animando
            if (_isTyping &&
                Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                _dialogueRect.Contains(Event.current.mousePosition) &&
                !_continueButtonRect.Contains(Event.current.mousePosition))
            {
                CompleteTypewriterImmediately();
                Event.current.Use();
            }
        }

        private void InitGuiStylesIfNeeded()
        {
            if (_guiStylesReady && _lastGuiWidth == Screen.width && _lastGuiHeight == Screen.height) return;

            _lastGuiWidth = Screen.width;
            _lastGuiHeight = Screen.height;

            float scale = ChessFontProvider.GetResponsiveScale();
            Font bold = ChessFontProvider.SerifBold;
            Font regular = ChessFontProvider.SerifRegular;

            _guiBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.RoundToInt(12 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.45f, 0.88f, 1f, 1f) }
            };

            _guiTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.RoundToInt(18 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.90f, 0.38f, 1f) }
            };

            _guiBodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = regular,
                fontSize = Mathf.RoundToInt(14 * scale),
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.92f, 0.94f, 0.98f, 1f) }
            };

            _guiHintStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.RoundToInt(13 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.40f, 0.96f, 0.68f, 1f) }
            };

            _guiContinueBtnStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.RoundToInt(14 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.08f, 0.06f, 0.02f, 1f) }
            };

            _guiSkipBtnStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.RoundToInt(12 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.96f, 0.90f, 0.64f, 1f) }
            };

            _guiMentorTagStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.RoundToInt(10 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.98f, 0.86f, 0.34f, 1f) }
            };

            _guiStylesReady = true;
        }

        private static void DrawGuiBorder(Rect rect, float thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        }

        #endregion

        #region Arte Pixel Art Procedural (FilterMode.Point)

        private void CreateProceduralPixelArtTextures()
        {
            _dimmerTex = MakeSolidTexture(new Color(0.02f, 0.03f, 0.07f, 0.74f));
            _dialogueBgTex = MakeSolidTexture(new Color(0.06f, 0.08f, 0.14f, 0.96f));
            _skipBgTex = MakeSolidTexture(new Color(0.08f, 0.11f, 0.18f, 0.90f));
            _goldBtnTex = MakeSolidTexture(new Color(0.92f, 0.72f, 0.18f, 1.0f));

            _pointerArrowTex = GeneratePointerArrowTexture();
            _pointerArrowSprite = Sprite.Create(
                _pointerArrowTex,
                new Rect(0, 0, _pointerArrowTex.width, _pointerArrowTex.height),
                new Vector2(0.5f, 0.5f)
            );

            _mentorPortraitTex = GenerateMentorPortraitTexture();
            _mentorPortraitSprite = Sprite.Create(
                _mentorPortraitTex,
                new Rect(0, 0, _mentorPortraitTex.width, _mentorPortraitTex.height),
                new Vector2(0.5f, 0.5f)
            );
        }

        private static Texture2D GeneratePointerArrowTexture()
        {
            // Flecha pixel art dorada 16x16 apuntando hacia abajo (punta inferior)
            string[] pattern =
            {
                "......OOOO......",
                ".....OHGGHO.....",
                ".....OHGGGO.....",
                ".....OHGGAO.....",
                ".....OHGGAO.....",
                "..OOOOHGGAOOOO..",
                ".OHHHHGGGAAAAAO.",
                ".OHHGGGGGGGGAAO.",
                "..OHGGGGGGGAAO..",
                "...OHGGGGGAAO...",
                "....OHGGGAAO....",
                ".....OHGAAO.....",
                "......OGAO......",
                ".......OO.......",
                ".......OO.......",
                "................"
            };

            return BakePatternTexture(pattern, ch => ch switch
            {
                'O' => new Color(0.10f, 0.06f, 0.02f, 1f),
                'H' => new Color(1.00f, 0.97f, 0.65f, 1f),
                'G' => new Color(1.00f, 0.84f, 0.14f, 1f),
                'A' => new Color(0.80f, 0.54f, 0.06f, 1f),
                _ => new Color(0f, 0f, 0f, 0f)
            });
        }

        private static Texture2D GenerateMentorPortraitTexture()
        {
            // Retrato arcade 16x16 de "Maestro Guía" (Gran Maestro sabio con corona dorada y barba blanca)
            string[] pattern =
            {
                "BBBB..GGGG..BBBB",
                "BBBB.GYYYYG.BBBB",
                "BBBBBOGGGGO.BBBB",
                "BBBB.OHHHHO.BBBB",
                "BBB.OHSSSSHO.BBB",
                "BBB.OSSSSSSO.BBB",
                "BBB.OSEBBESO.BBB",
                "BBB.OSSSSSSO.BBB",
                "BB..OWWWWWWO..BB",
                "BB..OWWWWWWO..BB",
                "BBB..OWWWWO..BBB",
                "BB.OOBBWWBBOO.BB",
                "B.OBBGGWWGGBBO.B",
                ".OBBBBBWWBBBBBO.",
                ".OBBBBBBBBBBBBO.",
                "BBBBBBBBBBBBBBBB"
            };

            return BakePatternTexture(pattern, ch => ch switch
            {
                'B' => new Color(0.10f, 0.15f, 0.28f, 1f), // Fondo y túnica zafiro
                'O' => new Color(0.06f, 0.05f, 0.08f, 1f), // Contorno 32-bits
                'G' => new Color(0.95f, 0.76f, 0.16f, 1f), // Oro
                'Y' => new Color(1.00f, 0.94f, 0.58f, 1f), // Brillo dorado
                'H' => new Color(0.82f, 0.85f, 0.92f, 1f), // Cabello plateado
                'S' => new Color(0.96f, 0.78f, 0.64f, 1f), // Piel
                'E' => new Color(0.12f, 0.18f, 0.30f, 1f), // Ojos
                'W' => new Color(0.95f, 0.96f, 0.99f, 1f), // Barba y cuello
                _ => new Color(0.10f, 0.15f, 0.28f, 1f)
            });
        }

        private static Texture2D BakePatternTexture(string[] pattern, Func<char, Color> palette)
        {
            int h = pattern.Length;
            int w = pattern[0].Length;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[w * h];
            for (int py = 0; py < h; py++)
            {
                int texY = (h - 1) - py;
                string row = pattern[py];
                for (int px = 0; px < w; px++)
                {
                    pixels[texY * w + px] = palette(row[px]);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeSolidTexture(Color col)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color[] cols = { col, col, col, col };
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private void DestroyProceduralAssets()
        {
            if (_pointerArrowSprite != null) Destroy(_pointerArrowSprite);
            if (_pointerArrowTex != null) Destroy(_pointerArrowTex);
            if (_mentorPortraitSprite != null) Destroy(_mentorPortraitSprite);
            if (_mentorPortraitTex != null) Destroy(_mentorPortraitTex);
            if (_dimmerTex != null) Destroy(_dimmerTex);
            if (_dialogueBgTex != null) Destroy(_dialogueBgTex);
            if (_skipBgTex != null) Destroy(_skipBgTex);
            if (_goldBtnTex != null) Destroy(_goldBtnTex);
        }

        #endregion
    }
}

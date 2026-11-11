using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Interfaz visual de Economía de Capturas y Selector de Recompensas (Draft Modal):
    /// 1. Instancia texto flotante pixel art dorado sobre la casilla capturada ("+1 PT", "+3 PTS", "+9 PTS!").
    /// 2. Renderiza el contador y barra en HUD: "PUNTOS DE CAPTURA: X / 4 (Mejora) | X / 10 (Maestro)".
    /// 3. Muestra un Modal emergente con animación pop-in y 2-3 cartas elegibles de Mejoras Menores o Gran Maestro.
    /// </summary>
    public class RewardDraftModalUI : MonoBehaviour
    {
        public static RewardDraftModalUI Instance { get; private set; }

        [Header("Configuración Visual")]
        [SerializeField] private float floatingTextDuration = 1.05f;
        [SerializeField] private float floatingRiseTiles = 0.90f;
        [SerializeField] private float modalPopDuration = 0.28f;

        private CaptureRewardManager _rewardManager;
        private BoardView _boardView;
        private DifficultySelectorUI _difficultySelectorUI;

        // Estado del Modal de Draft
        private bool _isModalOpen = false;
        private UpgradeType _currentRewardType = UpgradeType.Minor_Perk;
        private List<CardData> _draftCards = new List<CardData>();
        private string _draftReason = string.Empty;
        private float _popElapsed = 0f;
        private int _hoveredCardIndex = -1;
        private Vector2 _portraitScroll = Vector2.zero;

        public bool IsModalOpen => _isModalOpen;
        public Rect LastHudBarRect { get; private set; }
        public Rect LastCardsAreaRect { get; private set; }
        public Rect LastFirstCardRect { get; private set; }

        // Suavizado visual de barras HUD
        private float _displayMinorFill = 0f;
        private float _displayMasterFill = 0f;

        // Caché de estilos y texturas OnGUI
        private Texture2D _darkOverlayTex;
        private Texture2D _panelBgTex;
        private Texture2D _hudBgTex;
        private Texture2D _cardBgTex;
        private Texture2D _cardHoverBgTex;
        private Texture2D _barTrackTex;

        private GUIStyle _hudLabelStyle;
        private GUIStyle _modalBadgeStyle;
        private GUIStyle _modalTitleStyle;
        private GUIStyle _modalSubtitleStyle;
        private GUIStyle _cardTitleStyle;
        private GUIStyle _cardDescStyle;
        private GUIStyle _cardCostStyle;
        private GUIStyle _cardRarityStyle;
        private GUIStyle _pickBtnStyle;
        private GUIStyle _tempFitStyle;
        private Font _cachedPopupFont;
        private int _lastStyledWidth = 0;

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
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void Start()
        {
            ResolveReferences();
            Subscribe();
        }

        private void Update()
        {
            if (_rewardManager == null)
            {
                ResolveReferences();
                if (_rewardManager != null) Subscribe();
            }

            if (_isModalOpen)
            {
                _popElapsed += Time.unscaledDeltaTime;
            }

            if (_rewardManager != null)
            {
                float targetMinor = Mathf.Clamp01((float)_rewardManager.MinorCyclePoints / CaptureRewardManager.MinorUpgradeThreshold);
                float targetMaster = Mathf.Clamp01((float)_rewardManager.MasterCyclePoints / CaptureRewardManager.MasterCardThreshold);
                _displayMinorFill = Mathf.Lerp(_displayMinorFill, targetMinor, Time.unscaledDeltaTime * 10f);
                _displayMasterFill = Mathf.Lerp(_displayMasterFill, targetMaster, Time.unscaledDeltaTime * 10f);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            Unsubscribe();
            DestroyCachedTextures();
        }

        private void ResolveReferences()
        {
            if (_boardView == null) _boardView = GetComponent<BoardView>() ?? FindAnyObjectByType<BoardView>();
            if (_rewardManager == null) _rewardManager = CaptureRewardManager.Instance ?? GetComponent<CaptureRewardManager>() ?? FindAnyObjectByType<CaptureRewardManager>();
            if (_difficultySelectorUI == null) _difficultySelectorUI = FindAnyObjectByType<DifficultySelectorUI>();
        }

        private void Subscribe()
        {
            if (_rewardManager == null) return;

            _rewardManager.OnPointsPopupRequested -= HandlePointsPopupRequested;
            _rewardManager.OnPointsPopupRequested += HandlePointsPopupRequested;

            _rewardManager.OnCustomPopupRequested -= HandleCustomPopupRequested;
            _rewardManager.OnCustomPopupRequested += HandleCustomPopupRequested;

            _rewardManager.OnDraftRequested -= HandleDraftRequested;
            _rewardManager.OnDraftRequested += HandleDraftRequested;
        }

        private void Unsubscribe()
        {
            if (_rewardManager != null)
            {
                _rewardManager.OnPointsPopupRequested -= HandlePointsPopupRequested;
                _rewardManager.OnCustomPopupRequested -= HandleCustomPopupRequested;
                _rewardManager.OnDraftRequested -= HandleDraftRequested;
            }
        }

        public void ForceClose()
        {
            _isModalOpen = false;
            _draftCards.Clear();
            _hoveredCardIndex = -1;
            _popElapsed = 0f;
            LastCardsAreaRect = Rect.zero;
            LastFirstCardRect = Rect.zero;
        }

        private void HandleDraftRequested(UpgradeType rewardType, List<CardData> cards, string reason)
        {
            _currentRewardType = rewardType;
            _draftCards = cards != null ? new List<CardData>(cards) : new List<CardData>();
            _draftReason = reason ?? string.Empty;
            _popElapsed = 0f;
            _hoveredCardIndex = -1;
            _portraitScroll = Vector2.zero;
            _isModalOpen = _draftCards.Count > 0;
        }

        #region Texto Flotante Pixel Art sobre Casilla

        private void HandlePointsPopupRequested(Vector3 worldPosition, int points, PieceType capturedType, bool deliveredCheck)
        {
            string label = PieceValueTable.FormatPopupText(points, capturedType, includeCheckTag: deliveredCheck);
            if (string.IsNullOrEmpty(label)) return;

            StartCoroutine(SpawnAndAnimatePopupRoutine(worldPosition, label, capturedType == PieceType.Queen));
        }

        private void HandleCustomPopupRequested(Vector3 worldPosition, string label, bool isMajorHighlight)
        {
            if (string.IsNullOrEmpty(label)) return;
            StartCoroutine(SpawnAndAnimatePopupRoutine(worldPosition, label, isMajorHighlight));
        }

        private IEnumerator SpawnAndAnimatePopupRoutine(Vector3 worldPos, string text, bool isMajorHighlight)
        {
            if (_boardView == null) _boardView = FindAnyObjectByType<BoardView>();
            float tileSize = _boardView != null ? _boardView.TileSize : 1.0f;

            GameObject popupObj = new GameObject("CapturePointsPopup");
            Vector3 startPos = new Vector3(worldPos.x, worldPos.y + (0.18f * tileSize), -0.6f);
            popupObj.transform.position = startPos;

            if (_cachedPopupFont == null)
            {
                _cachedPopupFont = Resources.Load<Font>("Fonts/ChessSerif-Bold");
            }

            // Texto principal dorado estilo pixel-arcade
            TextMesh mainMesh = popupObj.AddComponent<TextMesh>();
            mainMesh.text = text;
            mainMesh.alignment = TextAlignment.Center;
            mainMesh.anchor = TextAnchor.MiddleCenter;
            mainMesh.fontSize = 64;
            mainMesh.fontStyle = FontStyle.Bold;
            mainMesh.characterSize = (isMajorHighlight ? 0.048f : 0.040f) * tileSize;

            Color goldColor = isMajorHighlight
                ? new Color(1.0f, 0.92f, 0.25f, 1.0f)
                : new Color(1.0f, 0.84f, 0.14f, 1.0f);
            mainMesh.color = goldColor;

            MeshRenderer mainRenderer = popupObj.GetComponent<MeshRenderer>();
            if (_cachedPopupFont != null)
            {
                mainMesh.font = _cachedPopupFont;
                if (_cachedPopupFont.material != null && mainRenderer != null)
                {
                    mainRenderer.sharedMaterial = _cachedPopupFont.material;
                }
            }
            if (mainRenderer != null) mainRenderer.sortingOrder = 40;

            // Sombra oscura pixel-art detrás para contraste perfecto sobre casillas claras u oscuras
            GameObject shadowObj = new GameObject("Shadow");
            shadowObj.transform.SetParent(popupObj.transform, false);
            shadowObj.transform.localPosition = new Vector3(0.038f * tileSize, -0.038f * tileSize, 0.02f);

            TextMesh shadowMesh = shadowObj.AddComponent<TextMesh>();
            shadowMesh.text = text;
            shadowMesh.alignment = TextAlignment.Center;
            shadowMesh.anchor = TextAnchor.MiddleCenter;
            shadowMesh.fontSize = 64;
            shadowMesh.fontStyle = FontStyle.Bold;
            shadowMesh.characterSize = mainMesh.characterSize;
            Color shadowColor = new Color(0.05f, 0.05f, 0.09f, 0.92f);
            shadowMesh.color = shadowColor;

            MeshRenderer shadowRenderer = shadowObj.GetComponent<MeshRenderer>();
            if (_cachedPopupFont != null)
            {
                shadowMesh.font = _cachedPopupFont;
                if (_cachedPopupFont.material != null && shadowRenderer != null)
                {
                    shadowRenderer.sharedMaterial = _cachedPopupFont.material;
                }
            }
            if (shadowRenderer != null) shadowRenderer.sortingOrder = 39;

            float duration = Mathf.Max(0.4f, floatingTextDuration);
            float elapsed = 0f;
            Vector3 endPos = startPos + new Vector3(0f, floatingRiseTiles * tileSize, 0f);

            while (elapsed < duration)
            {
                if (popupObj == null) yield break;

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Pop inicial rápido de escala (1.35 -> 1.0)
                float scalePop = t < 0.16f
                    ? Mathf.Lerp(1.36f, 1.0f, t / 0.16f)
                    : 1.0f;
                popupObj.transform.localScale = Vector3.one * scalePop;

                // Desplazamiento vertical suave (ease-out)
                float riseEase = 1f - Mathf.Pow(1f - t, 3f);
                popupObj.transform.position = Vector3.Lerp(startPos, endPos, riseEase);

                // Mantener sólido al inicio y desvanecer en la segunda mitad
                float alpha = t < 0.48f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.48f) / 0.52f);
                mainMesh.color = new Color(goldColor.r, goldColor.g, goldColor.b, alpha);
                shadowMesh.color = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * alpha);

                yield return null;
            }

            if (popupObj != null)
            {
                Destroy(popupObj);
            }
        }

        #endregion

        #region Renderizado OnGUI (HUD Bar + Draft Modal)

        private void OnGUI()
        {
            if (TutorialController.IsEducationalModeActive || GameManager.SelectedGameMode == GameMode.Education || GameManager.SelectedGameMode == GameMode.Arcade || ArcadeSurvivorController.IsArcadeModeActive) return;
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameState.Playing &&
                !_isModalOpen)
            {
                return;
            }

            if (_rewardManager == null)
            {
                _rewardManager = CaptureRewardManager.Instance ?? FindAnyObjectByType<CaptureRewardManager>();
            }
            if (_rewardManager == null || !_rewardManager.IsEconomyActive) return;

            InitStylesIfNeeded();

            // 1. Barra / Contador de Puntos de Captura en HUD
            if (GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.Playing)
            {
                DrawCaptureHudBar();
            }

            // 2. Modal emergente de Draft (Mejora Menor o Gran Maestro)
            if (_isModalOpen)
            {
                DrawDraftModal();
            }
        }

        private void DrawCaptureHudBar()
        {
            float scale = ChessFontProvider.GetResponsiveScale();
            float safeLeft = Mathf.Max(0f, Screen.safeArea.xMin);
            float safeRight = Mathf.Max(0f, Screen.width - Screen.safeArea.xMax);
            float safeTop = Mathf.Max(0f, Screen.height - Screen.safeArea.yMax);

            bool inOnboarding = OnboardingManager.IsOnboardingGameplayActive;
            bool hasClock = !inOnboarding && GameManager.CurrentMatchConfig != null && GameManager.CurrentMatchConfig.HasTimeLimit;
            bool hasPauseBtn = !inOnboarding && MainMenuController.IsFloatingPauseButtonVisible;
            bool hasDifficultyUI = !inOnboarding && _difficultySelectorUI != null && _difficultySelectorUI.enabled && _difficultySelectorUI.ShowOnScreenUI;

            float leftX = safeLeft + 14f;
            float barH = Mathf.Clamp(46f * scale, 42f, 54f);
            float barY = safeTop + 12f;
            float skipReserved = OnboardingManager.IsOnboardingActive
                ? (Mathf.Clamp(172f * scale, 148f, 236f) + 28f)
                : 0f;
            float pauseReserved = Mathf.Max(hasPauseBtn ? 76f : 14f, skipReserved);
            float diffReserved = hasDifficultyUI ? (Mathf.Min(260f, Screen.width - 32f) + 26f) : Mathf.Max(14f, skipReserved);

            float maxAvailableW;
            if (!hasClock)
            {
                // Fila superior izquierda (yMax <= safeTop + 66f), libre por encima del panel de Dificultad (safeTop + 70f)
                maxAvailableW = Mathf.Max(220f, Screen.width - leftX - safeRight - pauseReserved);
            }
            else
            {
                float clockLeftX = (Screen.width - 340f) * 0.5f;
                float spaceLeftOfClock = clockLeftX - leftX - 10f;

                if (spaceLeftOfClock >= 320f)
                {
                    // Cabe a la izquierda del reloj centrado sin solaparse
                    maxAvailableW = spaceLeftOfClock;
                }
                else
                {
                    // Bajo el reloj centrado (y = safeTop + 68f); comprobar espacio a la izquierda de Dificultad IA
                    float spaceLeftOfDiff = Screen.width - leftX - safeRight - diffReserved;
                    if (!hasDifficultyUI || spaceLeftOfDiff >= 340f)
                    {
                        barY = safeTop + 68f;
                        maxAvailableW = Mathf.Max(220f, spaceLeftOfDiff);
                    }
                    else
                    {
                        // Pantalla muy estrecha con reloj + selector de dificultad activos
                        barY = safeTop + 162f;
                        maxAvailableW = Mathf.Max(220f, Screen.width - leftX - safeRight - 14f);
                    }
                }
            }

            float barW = Mathf.Min(540f * scale, maxAvailableW);

            Rect hudRect = new Rect(leftX, barY, barW, barH);
            if (Event.current.type == EventType.Repaint && hudRect.width > 4f)
            {
                LastHudBarRect = hudRect;
            }

            Color oldColor = GUI.color;
            // Sombra dura arcade
            GUI.color = new Color(0.02f, 0.03f, 0.06f, 0.88f);
            GUI.DrawTexture(new Rect(hudRect.x + 2, hudRect.y + 2, hudRect.width, hudRect.height), Texture2D.whiteTexture);

            // Fondo
            GUI.color = Color.white;
            GUI.DrawTexture(hudRect, _hudBgTex);

            // Borde dorado si está a punto de desbloquear recompensa, acero arcade en estado normal
            bool nearThreshold = _rewardManager.MinorCyclePoints >= 3 || _rewardManager.MasterCyclePoints >= 8;
            Color borderColor = nearThreshold
                ? new Color(0.95f, 0.78f, 0.22f, 1f)
                : new Color(0.28f, 0.36f, 0.50f, 1f);
            GUI.color = borderColor;
            DrawBorder2px(hudRect);
            GUI.color = Color.white;

            // Texto oficial requerido
            string hudText = $"PUNTOS DE CAPTURA: {_rewardManager.MinorCyclePoints} / {CaptureRewardManager.MinorUpgradeThreshold} (Mejora) | {_rewardManager.MasterCyclePoints} / {CaptureRewardManager.MasterCardThreshold} (Maestro)";
            GUIStyle fittedLabel = GetFittedStyle(_hudLabelStyle, hudText, barW - 18f);
            Rect labelRect = new Rect(hudRect.x + 9f, hudRect.y + (3f * scale), barW - 18f, barH * 0.54f);
            GUI.Label(labelRect, hudText, fittedLabel);

            // Barras de progreso dobles (Mejora / Maestro)
            float barsY = hudRect.y + (barH * 0.63f);
            float barsH = Mathf.Max(6f, 7f * scale);
            float gap = 10f * scale;
            float innerW = barW - 18f;
            float leftTrackW = (innerW - gap) * 0.44f;
            float rightTrackW = innerW - gap - leftTrackW;

            Rect minorTrack = new Rect(hudRect.x + 9f, barsY, leftTrackW, barsH);
            Rect masterTrack = new Rect(minorTrack.xMax + gap, barsY, rightTrackW, barsH);

            GUI.DrawTexture(minorTrack, _barTrackTex);
            GUI.DrawTexture(masterTrack, _barTrackTex);

            if (_displayMinorFill > 0.005f)
            {
                GUI.color = new Color(0.22f, 0.84f, 0.52f, 1f);
                GUI.DrawTexture(new Rect(minorTrack.x, minorTrack.y, minorTrack.width * _displayMinorFill, minorTrack.height), Texture2D.whiteTexture);
            }

            if (_displayMasterFill > 0.005f)
            {
                GUI.color = new Color(0.98f, 0.80f, 0.20f, 1f);
                GUI.DrawTexture(new Rect(masterTrack.x, masterTrack.y, masterTrack.width * _displayMasterFill, masterTrack.height), Texture2D.whiteTexture);
            }

            GUI.color = oldColor;
        }

        private void DrawDraftModal()
        {
            float scale = ChessFontProvider.GetResponsiveScale();

            // Animación pop-in con ligero overshoot elástico
            float t = Mathf.Clamp01(_popElapsed / Mathf.Max(0.05f, modalPopDuration));
            float easeOut = 1f - Mathf.Pow(1f - t, 3f);
            float overshoot = Mathf.Sin(t * Mathf.PI) * 0.05f;
            float popScale = Mathf.Lerp(0.72f, 1.0f, easeOut) + overshoot;
            float alpha = Mathf.Clamp01(t * 2.2f);

            // Overlay oscuro
            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _darkOverlayTex);

            float safeLeft = Mathf.Max(0f, Screen.safeArea.xMin);
            float safeRight = Mathf.Max(0f, Screen.width - Screen.safeArea.xMax);
            float safeTop = Mathf.Max(0f, Screen.height - Screen.safeArea.yMax);
            float safeBottom = Mathf.Max(0f, Screen.safeArea.yMin);

            float reservedBottom = OnboardingManager.IsOnboardingActive
                ? (Mathf.Clamp(195f * scale, 172f, 275f) + (22f * scale))
                : 0f;

            float usableW = Screen.width - safeLeft - safeRight - (24f * scale);
            float usableH = Mathf.Max(260f, Screen.height - safeTop - safeBottom - reservedBottom - (24f * scale));

            int cardCount = Mathf.Max(1, _draftCards.Count);
            bool horizontalCards = usableW >= (cardCount * 185f) && usableW > (usableH * 0.72f);

            float targetModalW = horizontalCards
                ? Mathf.Min(usableW, Mathf.Max(640f, cardCount * 280f * scale))
                : Mathf.Min(usableW, 660f * scale);
            float targetModalH = horizontalCards
                ? Mathf.Min(usableH, 540f * scale)
                : Mathf.Min(usableH, 860f * scale);

            float modalW = targetModalW * popScale;
            float modalH = targetModalH * popScale;
            float modalX = safeLeft + ((Screen.width - safeLeft - safeRight - modalW) * 0.5f);
            float modalY = safeTop + ((Screen.height - safeTop - safeBottom - reservedBottom - modalH) * 0.5f);

            Rect modalRect = new Rect(modalX, modalY, modalW, modalH);

            bool isMaster = _currentRewardType == UpgradeType.Major_Master;
            Color accentColor = isMaster
                ? new Color(0.98f, 0.80f, 0.22f, alpha)
                : new Color(0.32f, 0.85f, 0.62f, alpha);

            // Sombra del panel
            GUI.color = new Color(0f, 0f, 0f, 0.75f * alpha);
            GUI.DrawTexture(new Rect(modalRect.x + 5, modalRect.y + 5, modalRect.width, modalRect.height), Texture2D.whiteTexture);

            // Fondo del panel
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(modalRect, _panelBgTex);

            // Borde dorado/esmeralda de 3px
            GUI.color = accentColor;
            DrawBorderThickness(modalRect, 3f);

            // Cabecera del Modal
            float padX = 22f * scale * popScale;
            float padY = 18f * scale * popScale;
            float contentW = modalW - (padX * 2f);
            float curY = modalY + padY;

            string badgeStr = isMaster
                ? "[ ★ DRAFT DE GRAN MAESTRO ★ ]"
                : "[ ⚡ DRAFT DE MEJORA MENOR ⚡ ]";
            GUI.color = accentColor;
            GUI.Label(new Rect(modalX + padX, curY, contentW, 24f * popScale), badgeStr, _modalBadgeStyle);
            curY += 26f * popScale;

            GUI.color = new Color(1f, 1f, 1f, alpha);
            string titleStr = isMaster ? "Elige una Carta Legendaria" : "Elige una Mejora Táctica";
            GUI.Label(new Rect(modalX + padX, curY, contentW, 34f * popScale), titleStr, _modalTitleStyle);
            curY += 36f * popScale;

            string reasonStr = string.IsNullOrEmpty(_draftReason)
                ? "Se añadirá directamente a tu mano para esta partida."
                : $"{_draftReason} Añade una carta a tu mano.";
            GUI.Label(new Rect(modalX + padX, curY, contentW, 24f * popScale), reasonStr, _modalSubtitleStyle);
            curY += 32f * popScale;

            // Zona de tarjetas
            float cardsAreaH = modalRect.yMax - curY - (18f * popScale);
            Rect cardsAreaRect = new Rect(modalX + padX, curY, contentW, Mathf.Max(140f, cardsAreaH));
            if (Event.current.type == EventType.Repaint && cardsAreaRect.width > 4f)
            {
                LastCardsAreaRect = cardsAreaRect;
            }

            bool canInteract = _popElapsed >= 0.12f;
            CardData pendingSelectedCard = null;

            if (horizontalCards)
            {
                float cardGap = 16f * scale * popScale;
                float cardW = (cardsAreaRect.width - cardGap * (cardCount - 1)) / cardCount;
                float cardH = Mathf.Min(cardsAreaRect.height, 395f * scale * popScale);
                float startY = cardsAreaRect.y + ((cardsAreaRect.height - cardH) * 0.5f);

                for (int i = 0; i < _draftCards.Count; i++)
                {
                    Rect cRect = new Rect(cardsAreaRect.x + i * (cardW + cardGap), startY, cardW, cardH);
                    if (DrawDraftCardVertical(i, _draftCards[i], cRect, canInteract, accentColor, alpha))
                    {
                        pendingSelectedCard = _draftCards[i];
                    }
                }
            }
            else
            {
                // Layout apilado vertical en pantallas móviles estrechas con scroll seguro
                float cardGap = 12f * scale;
                float cardH = Mathf.Min(205f * scale, Mathf.Max(145f, (cardsAreaRect.height - (cardCount - 1) * cardGap) / cardCount));
                float totalContentH = cardCount * cardH + Mathf.Max(0, cardCount - 1) * cardGap;

                Rect viewRect = new Rect(0f, 0f, cardsAreaRect.width - (totalContentH > cardsAreaRect.height ? 18f : 0f), totalContentH);
                _portraitScroll = GUI.BeginScrollView(cardsAreaRect, _portraitScroll, viewRect, false, totalContentH > cardsAreaRect.height);

                for (int i = 0; i < _draftCards.Count; i++)
                {
                    Rect cRect = new Rect(0f, i * (cardH + cardGap), viewRect.width, cardH);
                    if (DrawDraftCardHorizontalBanner(i, _draftCards[i], cRect, canInteract, accentColor, alpha))
                    {
                        pendingSelectedCard = _draftCards[i];
                    }
                }

                GUI.EndScrollView();
            }

            GUI.color = prevColor;

            if (pendingSelectedCard != null)
            {
                _rewardManager.SelectRewardCard(pendingSelectedCard);
            }
        }

        private bool DrawDraftCardVertical(int index, CardData data, Rect rect, bool canInteract, Color themeAccent, float alpha)
        {
            if (data == null) return false;

            if (index == 0 && Event.current.type == EventType.Repaint && rect.width > 4f)
            {
                Vector2 tl = GUIUtility.GUIToScreenPoint(rect.position);
                LastFirstCardRect = new Rect(tl.x, tl.y, rect.width, rect.height);
            }

            bool isHovered = rect.Contains(Event.current.mousePosition);
            if (isHovered) _hoveredCardIndex = index;

            Color rarityColor = data.HasCustomAuraColor
                ? new Color(data.AuraColor.r, data.AuraColor.g, data.AuraColor.b, alpha)
                : GetRarityColor(data.Rarity, alpha);
            Color borderColor = isHovered ? new Color(1f, 0.94f, 0.45f, alpha) : rarityColor;

            // Sombra de carta
            GUI.color = new Color(0f, 0f, 0f, 0.65f * alpha);
            GUI.DrawTexture(new Rect(rect.x + 3, rect.y + 3, rect.width, rect.height), Texture2D.whiteTexture);

            // Fondo de carta
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(rect, isHovered ? _cardHoverBgTex : _cardBgTex);

            // Borde según aura/rareza
            GUI.color = borderColor;
            DrawBorderThickness(rect, isHovered ? 3f : 2f);

            float pad = 12f;
            float innerW = rect.width - (pad * 2f);
            float y = rect.y + 10f;

            // Fila superior: Subtítulo/Rareza y Coste
            string topBadge = !string.IsNullOrEmpty(data.Subtitle) ? data.Subtitle.ToUpperInvariant() : GetRarityLabel(data.Rarity);
            GUI.color = rarityColor;
            GUI.Label(new Rect(rect.x + pad, y, innerW * 0.72f, 20f), topBadge, _cardRarityStyle);
            GUI.color = new Color(0.98f, 0.92f, 0.35f, alpha);
            GUI.Label(new Rect(rect.x + pad, y, innerW, 20f), $"${data.Cost}", _cardCostStyle);
            y += 24f;

            // Ilustración / Sprite de carta
            float artH = Mathf.Clamp(rect.height * 0.34f, 76f, 148f);
            Rect artRect = new Rect(rect.x + pad, y, innerW, artH);
            DrawCardSprite(artRect, MasterCardDatabase.ResolveSprite(data), rarityColor, alpha);
            y += artH + 8f;

            // Título de carta
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUIStyle fittedTitle = GetFittedStyle(_cardTitleStyle, data.CardName, innerW);
            GUI.Label(new Rect(rect.x + pad, y, innerW, 42f), data.CardName, fittedTitle);
            y += 42f;

            // Descripción
            float btnH = 40f;
            float descH = Mathf.Max(42f, rect.yMax - y - btnH - 16f);
            GUI.Label(new Rect(rect.x + pad, y, innerW, descH), data.Description, _cardDescStyle);

            // Botón Elegir
            Rect btnRect = new Rect(rect.x + pad, rect.yMax - btnH - 10f, innerW, btnH);
            GUI.color = isHovered ? new Color(1f, 0.95f, 0.6f, alpha) : new Color(1f, 1f, 1f, alpha);
            DrawDraftButton(btnRect, "★ ELEGIR CARTA", themeAccent, alpha);

            bool clicked = false;
            bool effectiveCanInteract = canInteract && OnboardingManager.IsImGuiButtonAllowed("draft_card");
            bool prevEnabled = GUI.enabled;
            GUI.enabled = effectiveCanInteract;
            if (GUI.Button(btnRect, GUIContent.none, GUIStyle.none) ||
                (Event.current.type == EventType.MouseUp && Event.current.button == 0 && rect.Contains(Event.current.mousePosition)))
            {
                if (effectiveCanInteract)
                {
                    Event.current.Use();
                    clicked = true;
                }
            }
            GUI.enabled = prevEnabled;
            return clicked;
        }

        private bool DrawDraftCardHorizontalBanner(int index, CardData data, Rect rect, bool canInteract, Color themeAccent, float alpha)
        {
            if (data == null) return false;

            if (index == 0 && Event.current.type == EventType.Repaint && rect.width > 4f)
            {
                Vector2 tl = GUIUtility.GUIToScreenPoint(rect.position);
                Rect rawScreen = new Rect(tl.x, tl.y, rect.width, rect.height);
                if (LastCardsAreaRect.width > 4f)
                {
                    float yMin = Mathf.Max(rawScreen.yMin, LastCardsAreaRect.yMin);
                    float yMax = Mathf.Min(rawScreen.yMax, LastCardsAreaRect.yMax);
                    if (yMax > yMin + 8f)
                    {
                        rawScreen = Rect.MinMaxRect(rawScreen.xMin, yMin, rawScreen.xMax, yMax);
                    }
                }
                LastFirstCardRect = rawScreen;
            }

            bool isHovered = rect.Contains(Event.current.mousePosition);
            if (isHovered) _hoveredCardIndex = index;

            Color rarityColor = data.HasCustomAuraColor
                ? new Color(data.AuraColor.r, data.AuraColor.g, data.AuraColor.b, alpha)
                : GetRarityColor(data.Rarity, alpha);
            Color borderColor = isHovered ? new Color(1f, 0.94f, 0.45f, alpha) : rarityColor;

            GUI.color = new Color(0f, 0f, 0f, 0.60f * alpha);
            GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(rect, isHovered ? _cardHoverBgTex : _cardBgTex);

            GUI.color = borderColor;
            DrawBorderThickness(rect, isHovered ? 3f : 2f);

            float pad = 12f;
            float artSize = Mathf.Min(rect.height - pad * 2f, 108f);
            Rect artRect = new Rect(rect.x + pad, rect.y + (rect.height - artSize) * 0.5f, artSize, artSize);
            DrawCardSprite(artRect, MasterCardDatabase.ResolveSprite(data), rarityColor, alpha);

            float textX = artRect.xMax + 12f;
            float textW = rect.xMax - textX - pad;
            float y = rect.y + pad;

            string topBadge = !string.IsNullOrEmpty(data.Subtitle) ? data.Subtitle.ToUpperInvariant() : GetRarityLabel(data.Rarity);
            GUI.color = rarityColor;
            GUI.Label(new Rect(textX, y, textW * 0.72f, 18f), topBadge, _cardRarityStyle);
            GUI.color = new Color(0.98f, 0.92f, 0.35f, alpha);
            GUI.Label(new Rect(textX, y, textW, 18f), $"${data.Cost}", _cardCostStyle);
            y += 20f;

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUIStyle fittedTitle = GetFittedStyle(_cardTitleStyle, data.CardName, textW);
            GUI.Label(new Rect(textX, y, textW, 24f), data.CardName, fittedTitle);
            y += 26f;

            float btnH = 34f;
            float btnW = Mathf.Min(textW, 175f);
            float descH = Mathf.Max(34f, rect.yMax - y - btnH - 12f);
            GUI.Label(new Rect(textX, y, textW, descH), data.Description, _cardDescStyle);

            Rect btnRect = new Rect(rect.xMax - btnW - pad, rect.yMax - btnH - 8f, btnW, btnH);
            DrawDraftButton(btnRect, "★ ELEGIR CARTA", themeAccent, alpha);

            bool clicked = false;
            bool effectiveCanInteract = canInteract && OnboardingManager.IsImGuiButtonAllowed("draft_card");
            bool prevEnabled = GUI.enabled;
            GUI.enabled = effectiveCanInteract;
            if (GUI.Button(btnRect, GUIContent.none, GUIStyle.none) ||
                (Event.current.type == EventType.MouseUp && Event.current.button == 0 && rect.Contains(Event.current.mousePosition)))
            {
                if (effectiveCanInteract)
                {
                    Event.current.Use();
                    clicked = true;
                }
            }
            GUI.enabled = prevEnabled;
            return clicked;
        }

        private void DrawCardSprite(Rect targetRect, Sprite sprite, Color tintFallback, float alpha)
        {
            Color prev = GUI.color;
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.9f * alpha);
            GUI.DrawTexture(targetRect, Texture2D.whiteTexture);

            if (sprite != null && sprite.texture != null)
            {
                Texture2D tex = sprite.texture;
                Rect texRect = sprite.textureRect;
                Rect uv = new Rect(
                    texRect.x / tex.width,
                    texRect.y / tex.height,
                    texRect.width / tex.width,
                    texRect.height / tex.height
                );

                // Mantener relación de aspecto centrada dentro de targetRect
                float spriteAspect = texRect.width / Mathf.Max(1f, texRect.height);
                float rectAspect = targetRect.width / Mathf.Max(1f, targetRect.height);
                Rect drawRect = targetRect;
                if (spriteAspect > rectAspect)
                {
                    float h = targetRect.width / spriteAspect;
                    drawRect = new Rect(targetRect.x, targetRect.y + (targetRect.height - h) * 0.5f, targetRect.width, h);
                }
                else
                {
                    float w = targetRect.height * spriteAspect;
                    drawRect = new Rect(targetRect.x + (targetRect.width - w) * 0.5f, targetRect.y, w, targetRect.height);
                }

                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTextureWithTexCoords(drawRect, tex, uv, true);
            }
            else
            {
                GUI.color = tintFallback;
                GUI.Label(targetRect, "★", _modalTitleStyle);
            }

            GUI.color = prev;
        }

        private void DrawDraftButton(Rect rect, string label, Color accent, float alpha)
        {
            Color prev = GUI.color;
            GUI.color = new Color(accent.r * 0.85f, accent.g * 0.85f, accent.b * 0.85f, alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = new Color(1f, 0.95f, 0.65f, alpha);
            DrawBorderThickness(rect, 2f);

            GUI.color = new Color(0.06f, 0.08f, 0.12f, alpha);
            GUI.Label(rect, label, _pickBtnStyle);
            GUI.color = prev;
        }

        #endregion

        #region Helpers y Estilos

        private void InitStylesIfNeeded()
        {
            if (_hudLabelStyle != null && _lastStyledWidth == Screen.width) return;
            _lastStyledWidth = Screen.width;

            if (_darkOverlayTex == null)
            {
                _darkOverlayTex = MakeColorTex(new Color(0.02f, 0.03f, 0.06f, 0.82f));
                _panelBgTex = MakeColorTex(new Color(0.07f, 0.09f, 0.14f, 0.98f));
                _hudBgTex = MakeColorTex(new Color(0.06f, 0.08f, 0.13f, 0.94f));
                _cardBgTex = MakeColorTex(new Color(0.10f, 0.13f, 0.20f, 0.98f));
                _cardHoverBgTex = MakeColorTex(new Color(0.15f, 0.19f, 0.29f, 1.00f));
                _barTrackTex = MakeColorTex(new Color(0.14f, 0.17f, 0.24f, 1.00f));
            }

            float scale = ChessFontProvider.GetResponsiveScale();
            Font bold = ChessFontProvider.SerifBold;
            Font regular = ChessFontProvider.SerifRegular;

            _hudLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.RoundToInt(13 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.96f, 0.94f, 0.84f, 1f) }
            };

            _modalBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.RoundToInt(13 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _modalTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.RoundToInt(24 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.98f, 0.88f, 0.35f, 1f) }
            };

            _modalSubtitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = regular,
                fontSize = Mathf.RoundToInt(14 * scale),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.82f, 0.87f, 0.95f, 1f) }
            };

            _cardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.RoundToInt(16 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(1f, 0.94f, 0.62f, 1f) }
            };

            _cardDescStyle = new GUIStyle(GUI.skin.label)
            {
                font = regular,
                fontSize = Mathf.RoundToInt(13 * scale),
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.88f, 0.91f, 0.96f, 1f) }
            };

            _cardCostStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.RoundToInt(15 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.98f, 0.92f, 0.35f, 1f) }
            };

            _cardRarityStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.RoundToInt(11 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _pickBtnStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.RoundToInt(13 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.06f, 0.08f, 0.12f, 1f) }
            };
        }

        private GUIStyle GetFittedStyle(GUIStyle baseStyle, string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return baseStyle;
            if (_tempFitStyle == null) _tempFitStyle = new GUIStyle(baseStyle);

            float measuredW = baseStyle.CalcSize(new GUIContent(text)).x;
            float usableW = Mathf.Max(20f, maxWidth - 4f);
            if (measuredW <= usableW) return baseStyle;

            _tempFitStyle.font = baseStyle.font;
            _tempFitStyle.fontStyle = baseStyle.fontStyle;
            _tempFitStyle.alignment = baseStyle.alignment;
            _tempFitStyle.normal = baseStyle.normal;
            _tempFitStyle.wordWrap = baseStyle.wordWrap;

            float ratio = usableW / measuredW;
            _tempFitStyle.fontSize = Mathf.Max(10, Mathf.FloorToInt(baseStyle.fontSize * ratio));
            return _tempFitStyle;
        }

        private static Color GetRarityColor(CardRarity rarity, float alpha) => rarity switch
        {
            CardRarity.Legendary => new Color(1.00f, 0.80f, 0.22f, alpha),
            CardRarity.Rare      => new Color(0.32f, 0.68f, 1.00f, alpha),
            CardRarity.Uncommon  => new Color(0.24f, 0.85f, 0.56f, alpha),
            _                    => new Color(0.65f, 0.72f, 0.82f, alpha)
        };

        private static string GetRarityLabel(CardRarity rarity) => rarity switch
        {
            CardRarity.Legendary => "GRAN MAESTRO",
            CardRarity.Rare      => "RARA",
            CardRarity.Uncommon  => "POCO COMÚN",
            _                    => "COMÚN"
        };

        private static void DrawBorder2px(Rect rect) => DrawBorderThickness(rect, 2f);

        private static void DrawBorderThickness(Rect rect, float t)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - t, rect.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, t, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - t, rect.y, t, rect.height), Texture2D.whiteTexture);
        }

        private static Texture2D MakeColorTex(Color c)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color[] cols = { c, c, c, c };
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private void DestroyCachedTextures()
        {
            if (_darkOverlayTex != null) Destroy(_darkOverlayTex);
            if (_panelBgTex != null) Destroy(_panelBgTex);
            if (_hudBgTex != null) Destroy(_hudBgTex);
            if (_cardBgTex != null) Destroy(_cardBgTex);
            if (_cardHoverBgTex != null) Destroy(_cardHoverBgTex);
            if (_barTrackTex != null) Destroy(_barTrackTex);
        }

        #endregion
    }
}

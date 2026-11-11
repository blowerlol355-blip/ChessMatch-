using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChessBalatro
{
    /// <summary>
    /// Interfaz de usuario completa para el Modo Educativo.
    /// Incluye:
    /// 1. Selector de Lecciones agrupado en 3 categorías visuales por código de color
    ///    (Básico = Verde, Intermedio = Azul/Dorado, Difícil = Rojo/Violeta).
    /// 2. HUD interactivo superior durante la lección con instrucciones paso a paso,
    ///    contador de jugadas, indicador de objetivo y resalte táctico de pieza/carta sugerida.
    /// 3. Barra inferior centrada con botones arcade acotados de navegación.
    /// 4. Modales arcade de Victoria y Reintento.
    /// Funciona de forma automática y responsive (9:16 móvil 1080x1920 y escritorio) con encuadre completo del tablero.
    /// </summary>
    public class TutorialUI : MonoBehaviour
    {
        public static TutorialUI Instance { get; private set; }

        [Header("Referencias de Control")]
        [SerializeField] private TutorialController controller;
        [SerializeField] private BoardView boardView;

        [Header("Paneles uGUI Opcionales")]
        [SerializeField] private GameObject selectorRootPanel;
        [SerializeField] private GameObject hudRootPanel;
        [SerializeField] private GameObject modalRootPanel;

        // Estado de pantalla
        private bool _showSelector = false;
        private bool _showFeedbackModal = false;
        private string _modalTitle = "";
        private string _modalMessage = "";
        private bool _modalIsVictory = false;
        private Vector2 _selectorScrollPos = Vector2.zero;

        public bool IsSelectorOpen => _showSelector;
        public float ActiveNavBarTopY { get; private set; } = -1f;

        // Estilos OnGUI Procedurales
        private bool _stylesInitialized = false;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _selectorTitleStyle;
        private GUIStyle _selectorSubtitleStyle;
        private GUIStyle _primaryButtonStyle;
        private GUIStyle _secondaryButtonStyle;
        private GUIStyle _disabledButtonStyle;
        private GUIStyle _tempFitStyle;

        private Texture2D _modalBgTex;
        private Texture2D _primaryBtnTex;
        private Texture2D _secondaryBtnTex;
        private Texture2D _disabledBtnTex;
        private Texture2D _clearBtnTex;

        // Ajuste de cámara para tablero completo durante lección
        private float _originalCamOrthoSize = -1f;
        private Vector3 _originalCamPosition;
        private bool _cameraAdjusted = false;

        // Ajuste temporal de mano de cartas en modo Portrait para no cubrir casillas del tablero
        private HandAlignment _savedHandAlignment = HandAlignment.RightSide;
        private float _savedHandBottomOffset = 25f;
        private bool _handLayoutAdjusted = false;

        // Paleta de colores temáticos por nivel de dificultad
        private readonly Color _colorBasicoBadge = new Color(0.20f, 0.72f, 0.36f, 1f);     // Verde esmeralda
        private readonly Color _colorIntermedioBadge = new Color(0.22f, 0.58f, 0.96f, 1f); // Azul real
        private readonly Color _colorDificilBadge = new Color(0.90f, 0.25f, 0.28f, 1f);    // Rojo carmesí

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            BoardView.EnsureCanvasScalersConfigured();
            ResolveReferences();
        }

        private void Start()
        {
            ResolveReferences();
            SubscribeControllerEvents();

            Camera cam = Camera.main;
            if (cam != null && _originalCamOrthoSize < 0f)
            {
                _originalCamOrthoSize = cam.orthographicSize;
                _originalCamPosition = cam.transform.position;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeControllerEvents();
            RestoreCardHandAlignment();
            RestoreCameraIfNeeded();
            DestroyCachedTextures();
        }

        private void ResolveReferences()
        {
            if (controller == null) controller = FindAnyObjectByType<TutorialController>();
            if (controller == null)
            {
                controller = gameObject.AddComponent<TutorialController>();
            }

            if (boardView == null) boardView = FindAnyObjectByType<BoardView>();
        }

        private void SubscribeControllerEvents()
        {
            if (controller != null)
            {
                controller.OnLessonStarted += HandleLessonStarted;
                controller.OnLessonPassed  += HandleLessonPassed;
                controller.OnLessonFailed  += HandleLessonFailed;
            }
        }

        private void UnsubscribeControllerEvents()
        {
            if (controller != null)
            {
                controller.OnLessonStarted -= HandleLessonStarted;
                controller.OnLessonPassed  -= HandleLessonPassed;
                controller.OnLessonFailed  -= HandleLessonFailed;
            }
        }

        private void Update()
        {
            // Resaltar periódicamente la pieza sugerida en el tablero si la lección está activa
            if (controller != null && controller.IsLessonActive && !_showSelector && !_showFeedbackModal)
            {
                TutorialLessonData lesson = controller.CurrentLesson;
                if (lesson != null && lesson.suggestedPieceCoord.HasValue && boardView != null)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
                    Color highlightColor = Color.Lerp(new Color(1f, 0.85f, 0.20f, 0.50f), new Color(1f, 0.95f, 0.40f, 0.85f), pulse);
                    boardView.HighlightSquare(lesson.suggestedPieceCoord.Value, highlightColor);
                }
            }
        }

        #region Handlers de Eventos

        private void HandleLessonStarted(TutorialLessonData lesson)
        {
            _showSelector = false;
            _showFeedbackModal = false;

            FindAnyObjectByType<GameHUDView>()?.ClearBanner();

            if (selectorRootPanel != null) selectorRootPanel.SetActive(false);
            if (hudRootPanel != null) hudRootPanel.SetActive(true);
            if (modalRootPanel != null) modalRootPanel.SetActive(false);
        }

        private void HandleLessonPassed(TutorialLessonData lesson)
        {
            _showFeedbackModal = true;
            _modalIsVictory = true;
            _modalTitle = "¡LECCIÓN SUPERADA!";
            _modalMessage = $"¡Excelente ejecución táctica! Has dominado: <b>{lesson.title}</b>.\n{lesson.tacticalTip}";

            FindAnyObjectByType<GameHUDView>()?.ClearBanner();

            if (modalRootPanel != null) modalRootPanel.SetActive(true);
        }

        private void HandleLessonFailed(TutorialLessonData lesson)
        {
            _showFeedbackModal = true;
            _modalIsVictory = false;
            _modalTitle = "MOVIMIENTOS AGOTADOS";
            _modalMessage = $"Se han agotado las jugadas permitidas para este puzle.\nConsejo clave: {lesson.tacticalTip}\n¡Vuelve a intentarlo!";

            FindAnyObjectByType<GameHUDView>()?.ClearBanner();

            if (modalRootPanel != null) modalRootPanel.SetActive(true);
        }

        public void OpenLessonSelector()
        {
            _showSelector = true;
            _showFeedbackModal = false;
            ActiveNavBarTopY = -1f;

            RestoreCardHandAlignment();
            RestoreCameraIfNeeded();
            FindAnyObjectByType<GameHUDView>()?.ClearBanner();

            if (selectorRootPanel != null) selectorRootPanel.SetActive(true);
            if (hudRootPanel != null) hudRootPanel.SetActive(false);
            if (modalRootPanel != null) modalRootPanel.SetActive(false);
        }

        public void CloseLessonSelector()
        {
            _showSelector = false;
            if (selectorRootPanel != null) selectorRootPanel.SetActive(false);
            if (hudRootPanel != null) hudRootPanel.SetActive(true);
        }

        public void NotifyExitedTutorial()
        {
            _showSelector = false;
            _showFeedbackModal = false;
            ActiveNavBarTopY = -1f;

            if (selectorRootPanel != null) selectorRootPanel.SetActive(false);
            if (hudRootPanel != null) hudRootPanel.SetActive(false);
            if (modalRootPanel != null) modalRootPanel.SetActive(false);

            RestoreCardHandAlignment();
            RestoreCameraIfNeeded();
        }

        #endregion

        #region Renderizado OnGUI Procedural Responsive (1080x1920 Móvil & Escritorio)

        private void OnGUI()
        {
            if (controller == null) return;

            InitStylesIfNeeded();

            float screenW = Screen.width;
            float screenH = Screen.height;
            bool isLandscape = screenW > (screenH * 1.1f);

            // Referencia 1080x1920 en Portrait (scale == 1.0f exacto en 1080x1920)
            // En Landscape usa alto de 1080 para mantener proporción y legibilidad
            float scale = !isLandscape
                ? Mathf.Clamp(Mathf.Min(screenW / 1080f, screenH / 1920f), 0.45f, 1.5f)
                : Mathf.Clamp(screenH / 1080f, 0.58f, 1.25f);

            // Tipografía legible en móvil (+25% a +40% respecto al diseño base, adaptada a escala)
            _badgeStyle.fontSize           = Mathf.Max(14, Mathf.RoundToInt(21f * scale));
            _titleStyle.fontSize           = Mathf.Max(18, Mathf.RoundToInt(30f * scale));
            _bodyStyle.fontSize            = Mathf.Max(14, Mathf.RoundToInt(19f * scale));
            _subtitleStyle.fontSize        = Mathf.Max(14, Mathf.RoundToInt(21f * scale));

            _selectorTitleStyle.fontSize    = Mathf.Max(16, Mathf.RoundToInt(22f * scale));
            _selectorSubtitleStyle.fontSize = Mathf.Max(13, Mathf.RoundToInt(18f * scale));

            _primaryButtonStyle.fontSize   = Mathf.Max(14, Mathf.RoundToInt(20f * scale));
            _secondaryButtonStyle.fontSize = Mathf.Max(13, Mathf.RoundToInt(19f * scale));
            _disabledButtonStyle.fontSize  = Mathf.Max(12, Mathf.RoundToInt(17f * scale));

            // 1. Selector de Lecciones (Pantalla Completa)
            if (_showSelector)
            {
                controller.HideStandardHUD();
                RestoreCardHandAlignment();
                RestoreCameraIfNeeded();
                ActiveNavBarTopY = -1f;
                RenderLessonSelectorGUI(screenW, screenH, scale);
                return;
            }

            // 2. HUD Durante la Lección Activa
            if (controller.IsLessonActive)
            {
                controller.HideStandardHUD();
                RenderLessonHudGUI(screenW, screenH, scale, isLandscape);
            }
            else
            {
                controller.RestoreStandardHUD();
                RestoreCardHandAlignment();
                RestoreCameraIfNeeded();
                ActiveNavBarTopY = -1f;
            }

            // 3. Modal de Feedback (Victoria o Derrota)
            if (_showFeedbackModal)
            {
                RenderFeedbackModalGUI(screenW, screenH, scale);
            }
        }

        /// <summary>
        /// Dibuja la pantalla de selección de lecciones con 3 bloques temáticos por dificultad y scroll seguro.
        /// </summary>
        private void RenderLessonSelectorGUI(float screenW, float screenH, float scale)
        {
            GUI.depth = -50;
            GUI.DrawTexture(new Rect(0, 0, screenW, screenH), _modalBgTex);

            float safeTop = Mathf.Max(0f, Screen.height - Screen.safeArea.yMax);
            float safeBottom = Mathf.Max(0f, Screen.safeArea.yMin);
            float safeLeft = Mathf.Max(0f, Screen.safeArea.xMin);
            float safeRight = Mathf.Max(0f, Screen.width - Screen.safeArea.xMax);
            float horizSafe = Mathf.Max(safeLeft, safeRight);

            float marginH = horizSafe + (16f * scale);
            float marginTop = safeTop + (16f * scale);
            float marginBottom = safeBottom + (16f * scale);

            float panelW = Mathf.Min(screenW - (marginH * 2f), 980f * scale);
            float panelH = screenH - marginTop - marginBottom;
            float panelX = (screenW - panelW) * 0.5f;
            float panelY = marginTop;

            DrawArcadePanel(new Rect(panelX, panelY, panelW, panelH), new Color(0.08f, 0.10f, 0.15f, 0.98f), new Color(0.25f, 0.35f, 0.50f, 1f));

            float padTop = 24f * scale;
            float padBottom = 20f * scale;
            float padSide = 30f * scale;
            float contentX = panelX + padSide;
            float contentW = Mathf.Max(120f, panelW - (padSide * 2f));
            float curY = panelY + padTop;

            string mainTitle = "★ MODO EDUCATIVO: ACADEMIA DE AJEDREZ ★";
            _titleStyle.alignment = TextAnchor.MiddleCenter;
            float mainTitleH = Mathf.Max(36f * scale, _titleStyle.CalcHeight(new GUIContent(mainTitle), contentW) + (4f * scale));
            GUI.Label(new Rect(contentX, curY, contentW, mainTitleH), mainTitle, _titleStyle);
            curY += mainTitleH + (8f * scale);

            string subTitle = "Aprende desde los fundamentos geométricos hasta tácticas de Gran Maestro";
            _subtitleStyle.alignment = TextAnchor.MiddleCenter;
            float subTitleH = Mathf.Max(26f * scale, _subtitleStyle.CalcHeight(new GUIContent(subTitle), contentW) + (4f * scale));
            GUI.Label(new Rect(contentX, curY, contentW, subTitleH), subTitle, _subtitleStyle);
            curY += subTitleH + (14f * scale);

            float exitBtnH = Mathf.Max(60f * scale, 46f);
            float exitBtnY = panelY + panelH - padBottom - exitBtnH;

            // Zona scrollable para los 3 bloques de lecciones (evita cortes en pantallas cortas Landscape)
            float scrollViewY = curY;
            float scrollViewH = Mathf.Max(100f, exitBtnY - scrollViewY - (14f * scale));
            float scrollInnerW = contentW - (18f * scale);
            float totalContentH = CalculateSelectorContentHeight(scrollInnerW, scale);

            Rect viewRect = new Rect(contentX, scrollViewY, contentW, scrollViewH);
            Rect contentRect = new Rect(0f, 0f, scrollInnerW, Mathf.Max(totalContentH, scrollViewH));
            _selectorScrollPos = GUI.BeginScrollView(viewRect, _selectorScrollPos, contentRect, false, totalContentH > scrollViewH);

            var lessons = controller.Lessons;
            float scrollCurY = 0f;
            if (lessons != null && lessons.Count >= 6)
            {
                scrollCurY = RenderDifficultyGroup(0f, scrollCurY, scrollInnerW, scale, "NIVEL BÁSICO: MOVIMIENTO FUNDAMENTAL", _colorBasicoBadge, 1, 3);
                scrollCurY += 14f * scale;

                scrollCurY = RenderDifficultyGroup(0f, scrollCurY, scrollInnerW, scale, "NIVEL INTERMEDIO: DEFENSA Y CARTAS MENORES", _colorIntermedioBadge, 4, 5);
                scrollCurY += 14f * scale;

                scrollCurY = RenderDifficultyGroup(0f, scrollCurY, scrollInnerW, scale, "NIVEL DIFÍCIL: MAESTRÍA ROGUELIKE (PETROSIÁN)", _colorDificilBadge, 6, 6);
            }

            GUI.EndScrollView();

            if (DrawArcadeButton(new Rect(contentX, exitBtnY, contentW, exitBtnH), "← VOLVER AL MENÚ PRINCIPAL", _secondaryButtonStyle, false, true))
            {
                controller.ExitTutorial();
            }
        }

        private float CalculateSelectorContentHeight(float w, float scale)
        {
            var lessons = controller.Lessons;
            if (lessons == null || lessons.Count < 6) return 100f;

            float total = 0f;
            total += MeasureDifficultyGroupHeight(w, scale, 1, 3) + (14f * scale);
            total += MeasureDifficultyGroupHeight(w, scale, 4, 5) + (14f * scale);
            total += MeasureDifficultyGroupHeight(w, scale, 6, 6) + (8f * scale);
            return total;
        }

        private float MeasureDifficultyGroupHeight(float w, float scale, int startId, int endId)
        {
            float badgeH = 36f * scale;
            float h = badgeH + (8f * scale);
            float textW = w * 0.68f - (14f * scale);

            for (int id = startId; id <= endId; id++)
            {
                TutorialLessonData lesson = controller.GetLessonById(id);
                if (lesson == null) continue;

                float tH = _selectorTitleStyle.CalcHeight(new GUIContent($"{id}. {lesson.title}"), textW) + (4f * scale);
                float sH = _selectorSubtitleStyle.CalcHeight(new GUIContent(lesson.subtitle), textW) + (4f * scale);
                float cardH = Mathf.Max(82f * scale, tH + sH + (24f * scale));
                h += cardH + (8f * scale);
            }

            return h;
        }

        private float RenderDifficultyGroup(float x, float y, float w, float scale, string groupHeader, Color groupColor, int startId, int endId)
        {
            float badgeH = 36f * scale;
            DrawArcadePanel(new Rect(x, y, w, badgeH), groupColor * 0.35f, groupColor);

            Color old = GUI.color;
            GUI.color = groupColor;
            _selectorTitleStyle.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(x + (12f * scale), y, w - (24f * scale), badgeH), $"■ {groupHeader}", _selectorTitleStyle);
            GUI.color = old;
            y += badgeH + (8f * scale);

            for (int id = startId; id <= endId; id++)
            {
                TutorialLessonData lesson = controller.GetLessonById(id);
                if (lesson == null) continue;

                bool completed = controller.IsLessonCompleted(id);
                bool unlocked = controller.IsLessonUnlocked(id);

                float leftPad = 14f * scale;
                float textW = w * 0.68f - leftPad;
                string titlePrefix = completed ? "✓ " : (unlocked ? $"{id}. " : "🔒 ");
                string titleLine = $"{titlePrefix}{lesson.title}";

                float titleH = Mathf.Max(26f * scale, _selectorTitleStyle.CalcHeight(new GUIContent(titleLine), textW) + (4f * scale));
                float subH = Mathf.Max(22f * scale, _selectorSubtitleStyle.CalcHeight(new GUIContent(lesson.subtitle), textW) + (4f * scale));
                float cardH = Mathf.Max(82f * scale, titleH + subH + (24f * scale));

                Rect cardRect = new Rect(x, y, w, cardH);
                Color borderColor = completed ? new Color(0.35f, 0.78f, 0.42f, 1f) : (unlocked ? groupColor : new Color(0.25f, 0.28f, 0.35f, 0.85f));
                Color bgCardColor = unlocked ? new Color(0.12f, 0.15f, 0.22f, 0.96f) : new Color(0.08f, 0.09f, 0.12f, 0.92f);
                DrawArcadePanel(cardRect, bgCardColor, borderColor);

                _selectorTitleStyle.alignment = TextAnchor.MiddleLeft;
                GUI.Label(new Rect(x + leftPad, y + (10f * scale), textW, titleH), titleLine, _selectorTitleStyle);

                _selectorSubtitleStyle.alignment = TextAnchor.UpperLeft;
                GUI.Label(new Rect(x + leftPad, y + (10f * scale) + titleH + (2f * scale), textW, subH), lesson.subtitle, _selectorSubtitleStyle);

                float btnW = w * 0.27f;
                float btnX = x + w - btnW - (12f * scale);
                float actionBtnH = Mathf.Max(52f * scale, cardH - (24f * scale));
                float btnY = y + (cardH - actionBtnH) * 0.5f;

                if (unlocked)
                {
                    string btnLabel = completed ? "REPETIR ↻" : "JUGAR ▶";
                    GUIStyle styleToUse = completed ? _secondaryButtonStyle : _primaryButtonStyle;

                    if (DrawArcadeButton(new Rect(btnX, btnY, btnW, actionBtnH), btnLabel, styleToUse, !completed, true))
                    {
                        controller.LoadLesson(id);
                    }
                }
                else
                {
                    DrawArcadeButton(new Rect(btnX, btnY, btnW, actionBtnH), "BLOQUEADA 🔒", _disabledButtonStyle, false, false);
                }

                y += cardH + (8f * scale);
            }

            return y;
        }

        /// <summary>
        /// Dibuja el HUD educativo unificado:
        /// 1. Panel superior de lección con Padding (Top: 24px, Bottom: 20px, Left/Right: 30px),
        ///    "[ LECCIÓN X/6 ]" alineado encima del título centrado y fuentes aumentadas (+25%+).
        /// 2. Barra inferior acotada y centrada con márgenes laterales e inferiores de 16px,
        ///    altura mínima de 60px (referencia 1080x1920) y bordes estilo arcade de 2px.
        /// 3. Encuadre automático de cámara para visualizar siempre el tablero 8x8 completo y su marco dorado.
        /// </summary>
        private void RenderLessonHudGUI(float screenW, float screenH, float scale, bool isLandscape)
        {
            TutorialLessonData lesson = controller.CurrentLesson;
            if (lesson == null) return;

            GUI.depth = 10;

            Color diffColor = GetDifficultyColor(lesson.difficulty);
            float safeTop = Mathf.Max(0f, Screen.height - Screen.safeArea.yMax);
            float safeBottom = Mathf.Max(0f, Screen.safeArea.yMin);
            float safeLeft = Mathf.Max(0f, Screen.safeArea.xMin);
            float safeRight = Mathf.Max(0f, Screen.width - Screen.safeArea.xMax);
            float horizSafe = Mathf.Max(safeLeft, safeRight);

            // =========================================================================
            // 1. PANEL SUPERIOR DE LECCIÓN (BANNER EDUCATIVO)
            // =========================================================================
            float bannerMarginSide = horizSafe + (16f * scale);
            float bannerMarginTop = safeTop + (16f * scale);
            float maxBannerW = isLandscape
                ? Mathf.Clamp(screenW * 0.72f, 660f, 1140f * scale)
                : (screenW - (bannerMarginSide * 2f));
            float bannerW = Mathf.Min(screenW - (bannerMarginSide * 2f), maxBannerW);
            float bannerX = (screenW - bannerW) * 0.5f;
            float bannerY = bannerMarginTop;

            // Padding interno exacto en referencia 1080x1920: Top 24px, Bottom 20px, Left/Right 30px
            float padTop = 24f * scale;
            float padBottom = 20f * scale;
            float padSide = 30f * scale;

            float bContentX = bannerX + padSide;
            float bContentW = Mathf.Max(100f, bannerW - (padSide * 2f));

            string badgeText = $"[ LECCIÓN {lesson.lessonId}/6 · {GetDifficultyDisplayName(lesson.difficulty)} ]";
            _badgeStyle.alignment = TextAnchor.MiddleCenter;
            float badgeH = Mathf.Max(26f * scale, _badgeStyle.CalcHeight(new GUIContent(badgeText), bContentW) + (4f * scale));

            _titleStyle.alignment = TextAnchor.MiddleCenter;
            float titleH = Mathf.Max(34f * scale, _titleStyle.CalcHeight(new GUIContent(lesson.title), bContentW) + (4f * scale));

            _bodyStyle.alignment = TextAnchor.UpperCenter;
            float bodyH = Mathf.Max(40f * scale, _bodyStyle.CalcHeight(new GUIContent(lesson.instructionText), bContentW) + (4f * scale));

            int remainingMoves = Mathf.Max(0, lesson.maxMoves - controller.MovesMadeInLesson);
            string movesStr = $"Jugadas Restantes: <b>{remainingMoves} / {lesson.maxMoves}</b>";
            string reqStr = lesson.requiresCard ? $"  |  Carta Requerida: <b>{lesson.requiredCardName}</b>" : "";
            string statusText = $"{movesStr}{reqStr}";
            _subtitleStyle.alignment = TextAnchor.MiddleCenter;
            float statusH = Mathf.Max(26f * scale, _subtitleStyle.CalcHeight(new GUIContent(statusText), bContentW) + (4f * scale));

            float gapBadgeToTitle = 8f * scale;
            float gapTitleToBody  = 10f * scale;
            float gapBodyToStatus = 10f * scale;

            float bannerH = padTop + badgeH + gapBadgeToTitle + titleH + gapTitleToBody + bodyH + gapBodyToStatus + statusH + padBottom;

            DrawArcadePanel(new Rect(bannerX, bannerY, bannerW, bannerH), new Color(0.08f, 0.11f, 0.16f, 0.96f), diffColor);

            float curY = bannerY + padTop;

            Color oldGuiColor = GUI.color;
            GUI.color = diffColor;
            GUI.Label(new Rect(bContentX, curY, bContentW, badgeH), badgeText, _badgeStyle);
            GUI.color = oldGuiColor;
            curY += badgeH + gapBadgeToTitle;

            GUI.Label(new Rect(bContentX, curY, bContentW, titleH), lesson.title, _titleStyle);
            curY += titleH + gapTitleToBody;

            GUI.Label(new Rect(bContentX, curY, bContentW, bodyH), lesson.instructionText, _bodyStyle);
            curY += bodyH + gapBodyToStatus;

            GUI.Label(new Rect(bContentX, curY, bContentW, statusH), statusText, _subtitleStyle);

            // =========================================================================
            // 2. BARRA INFERIOR DE NAVEGACIÓN (CENTRADA, MÁRGENES 16px, BOTONES >= 60px)
            // =========================================================================
            float navMarginSide = horizSafe + (16f * scale);
            float navMarginBottom = safeBottom + (16f * scale);
            float maxNavW = isLandscape
                ? Mathf.Clamp(screenW * 0.72f, 660f, 1140f * scale)
                : (screenW - (navMarginSide * 2f));
            float navW = Mathf.Min(screenW - (navMarginSide * 2f), maxNavW);
            float navX = (screenW - navW) * 0.5f;

            float btnH = Mathf.Max(60f * scale, 44f); // Mínimo 60px en referencia 1080x1920
            float navPadV = 10f * scale;
            float navPadH = 12f * scale;
            float navH = btnH + (navPadV * 2f);
            float navY = screenH - navH - navMarginBottom;
            ActiveNavBarTopY = navY;

            DrawArcadePanel(new Rect(navX, navY, navW, navH), new Color(0.08f, 0.10f, 0.15f, 0.96f), new Color(0.24f, 0.32f, 0.44f, 1f));

            float btnGap = 8f * scale;
            float availableW = navW - (navPadH * 2f) - (btnGap * 4f);
            float btnW = availableW / 5f;
            float btnY = navY + navPadV;
            float curBtnX = navX + navPadH;

            bool canPrev = lesson.lessonId > 1;
            if (DrawArcadeButton(new Rect(curBtnX, btnY, btnW, btnH), "◄ Ant", _secondaryButtonStyle, false, canPrev))
            {
                controller.PreviousLesson();
            }
            curBtnX += btnW + btnGap;

            if (DrawArcadeButton(new Rect(curBtnX, btnY, btnW, btnH), "↻ Reintentar", _secondaryButtonStyle, false, true))
            {
                controller.RestartCurrentLesson();
            }
            curBtnX += btnW + btnGap;

            bool canNext = lesson.lessonId < 6 && controller.IsLessonUnlocked(lesson.lessonId + 1);
            if (DrawArcadeButton(new Rect(curBtnX, btnY, btnW, btnH), "Sig ►", _primaryButtonStyle, true, canNext))
            {
                controller.AdvanceToNextLesson();
            }
            curBtnX += btnW + btnGap;

            if (DrawArcadeButton(new Rect(curBtnX, btnY, btnW, btnH), "☰ Lecciones", _secondaryButtonStyle, false, true))
            {
                OpenLessonSelector();
            }
            curBtnX += btnW + btnGap;

            if (DrawArcadeButton(new Rect(curBtnX, btnY, btnW, btnH), "✕ Salir", _secondaryButtonStyle, false, true))
            {
                controller.ExitTutorial();
            }

            // =========================================================================
            // 3. MANO DE CARTAS EN LECCIONES 5/6 + ENCUADRE DEL TABLERO COMPLETO
            // =========================================================================
            float cardReservedScreenH = 0f;
            CardManager cm = CardManager.Instance != null ? CardManager.Instance : FindAnyObjectByType<CardManager>();
            if (cm != null && cm.HandView != null)
            {
                if (lesson.requiresCard && !isLandscape)
                {
                    ConfigureCardHandForPortraitTutorial(cm.HandView, navY, scale);
                    Canvas parentCanvas = cm.HandView.GetComponentInParent<Canvas>();
                    float canvasScaleFactor = (parentCanvas != null && parentCanvas.scaleFactor > 0.01f) ? parentCanvas.scaleFactor : scale;
                    cardReservedScreenH = (cm.HandView.CardDimensions.y * canvasScaleFactor) + (14f * scale);
                }
                else
                {
                    RestoreCardHandAlignment();
                }
            }

            float pillReservedH = (cm != null && cm.ActiveCardViews != null && cm.ActiveCardViews.Count > 0)
                ? 62f
                : 0f;

            float boardMargin = 14f * scale;
            float topBoundary = bannerY + bannerH + boardMargin;
            float bottomBoundary = navY - boardMargin - Mathf.Max(cardReservedScreenH, pillReservedH);
            float freeH = Mathf.Max(bottomBoundary - topBoundary, 120f);
            float freeW = Mathf.Max(screenW - (horizSafe * 2f) - (24f * scale), 120f);

            float boardWorldSpan = boardView != null ? boardView.FrameWorldSpan : 8.85f;
            float neededOrthoH = (boardWorldSpan * 0.5f) * (screenH / freeH);
            float neededOrthoW = (boardWorldSpan * 0.5f) * (screenH / freeW);
            float finalOrthoSize = Mathf.Max(neededOrthoH, neededOrthoW) * 1.03f;

            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                if (_originalCamOrthoSize < 0f)
                {
                    _originalCamOrthoSize = cam.orthographicSize;
                    _originalCamPosition = cam.transform.position;
                }

                cam.orthographicSize = finalOrthoSize;

                Vector3 boardCenter = boardView != null ? boardView.transform.position : Vector3.zero;
                float freeCenterY = (topBoundary + bottomBoundary) * 0.5f;
                float screenCenterY = screenH * 0.5f;
                float pixelOffsetY = freeCenterY - screenCenterY;
                float worldUnitsPerPixel = (finalOrthoSize * 2f) / screenH;
                float camY = boardCenter.y + (pixelOffsetY * worldUnitsPerPixel);

                cam.transform.position = new Vector3(boardCenter.x, camY, _originalCamPosition.z);
                _cameraAdjusted = true;
            }
        }

        private void ConfigureCardHandForPortraitTutorial(CardHandView hand, float navTopY, float scale)
        {
            if (hand == null) return;

            if (!_handLayoutAdjusted)
            {
                _savedHandAlignment = hand.Alignment;
                _savedHandBottomOffset = hand.BottomOffset;
                _handLayoutAdjusted = true;
            }

            Canvas parentCanvas = hand.GetComponentInParent<Canvas>();
            float screenToCanvas = 1f;
            if (parentCanvas != null)
            {
                RectTransform canvasRect = parentCanvas.transform as RectTransform;
                if (canvasRect != null && canvasRect.rect.height > 10f)
                {
                    screenToCanvas = canvasRect.rect.height / Screen.height;
                }
            }

            float desiredBottomCanvas = ((Screen.height - navTopY) + (10f * scale)) * screenToCanvas;
            if (hand.Alignment != HandAlignment.BottomCenter)
            {
                hand.Alignment = HandAlignment.BottomCenter;
            }
            hand.SetBottomOffset(desiredBottomCanvas);
        }

        private void RestoreCardHandAlignment()
        {
            if (!_handLayoutAdjusted) return;

            CardManager cm = CardManager.Instance != null ? CardManager.Instance : FindAnyObjectByType<CardManager>();
            if (cm != null && cm.HandView != null)
            {
                cm.HandView.Alignment = _savedHandAlignment;
                cm.HandView.SetBottomOffset(_savedHandBottomOffset);
            }

            _handLayoutAdjusted = false;
        }

        /// <summary>
        /// Dibuja el modal de retroalimentación (Victoria o Derrota) con cálculo dinámico de altura.
        /// </summary>
        private void RenderFeedbackModalGUI(float screenW, float screenH, float scale)
        {
            GUI.depth = -100;
            GUI.DrawTexture(new Rect(0, 0, screenW, screenH), _modalBgTex);

            float modalW = Mathf.Min(screenW * 0.90f, 780f * scale);
            float padTop = 26f * scale;
            float padBottom = 24f * scale;
            float padSide = 30f * scale;
            float mW = Mathf.Max(140f, modalW - (padSide * 2f));

            _titleStyle.alignment = TextAnchor.MiddleCenter;
            float titleH = Mathf.Max(34f * scale, _titleStyle.CalcHeight(new GUIContent(_modalTitle), mW) + (4f * scale));

            _bodyStyle.alignment = TextAnchor.UpperCenter;
            float msgH = Mathf.Max(60f * scale, _bodyStyle.CalcHeight(new GUIContent(_modalMessage), mW) + (8f * scale));

            float mBtnH = Mathf.Max(60f * scale, 46f);
            float gap1 = 14f * scale;
            float gap2 = 22f * scale;

            float modalH = padTop + titleH + gap1 + msgH + gap2 + mBtnH + padBottom;
            float modalX = (screenW - modalW) * 0.5f;
            float modalY = (screenH - modalH) * 0.5f;

            Color accentCol = _modalIsVictory ? new Color(0.35f, 0.85f, 0.40f, 1f) : new Color(0.90f, 0.25f, 0.25f, 1f);
            DrawArcadePanel(new Rect(modalX, modalY, modalW, modalH), new Color(0.07f, 0.09f, 0.13f, 0.98f), accentCol);

            float mX = modalX + padSide;
            float mY = modalY + padTop;

            Color oldColor = GUI.color;
            GUI.color = accentCol;
            GUI.Label(new Rect(mX, mY, mW, titleH), _modalTitle, _titleStyle);
            GUI.color = oldColor;
            mY += titleH + gap1;

            GUI.Label(new Rect(mX, mY, mW, msgH), _modalMessage, _bodyStyle);
            mY += msgH + gap2;

            float mBtnW = (mW - (14f * scale)) / 2f;

            if (_modalIsVictory)
            {
                if (DrawArcadeButton(new Rect(mX, mY, mBtnW, mBtnH), "Reintentar ↻", _secondaryButtonStyle, false, true))
                {
                    _showFeedbackModal = false;
                    controller.RestartCurrentLesson();
                }

                bool hasNext = controller.CurrentLessonId < 6;
                string nextLabel = hasNext ? "Siguiente Lección ►" : "Menú de Lecciones ☰";
                if (DrawArcadeButton(new Rect(mX + mBtnW + (14f * scale), mY, mBtnW, mBtnH), nextLabel, _primaryButtonStyle, true, true))
                {
                    _showFeedbackModal = false;
                    if (hasNext)
                    {
                        controller.AdvanceToNextLesson();
                    }
                    else
                    {
                        OpenLessonSelector();
                    }
                }
            }
            else
            {
                if (DrawArcadeButton(new Rect(mX, mY, mBtnW, mBtnH), "Reintentar Lección ↻", _primaryButtonStyle, true, true))
                {
                    _showFeedbackModal = false;
                    controller.RestartCurrentLesson();
                }

                if (DrawArcadeButton(new Rect(mX + mBtnW + (14f * scale), mY, mBtnW, mBtnH), "Menú de Lecciones ☰", _secondaryButtonStyle, false, true))
                {
                    _showFeedbackModal = false;
                    OpenLessonSelector();
                }
            }
        }

        /// <summary>
        /// Restaura la cámara a su posición y tamaño originales.
        /// </summary>
        private void RestoreCameraIfNeeded()
        {
            if (!_cameraAdjusted || _originalCamOrthoSize < 0f) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            cam.orthographicSize = _originalCamOrthoSize;
            cam.transform.position = _originalCamPosition;
            _cameraAdjusted = false;
        }

        private Color GetDifficultyColor(LessonDifficulty difficulty)
        {
            switch (difficulty)
            {
                case LessonDifficulty.Basico:     return _colorBasicoBadge;
                case LessonDifficulty.Intermedio: return _colorIntermedioBadge;
                case LessonDifficulty.Dificil:    return _colorDificilBadge;
                default:                          return Color.white;
            }
        }

        private string GetDifficultyDisplayName(LessonDifficulty difficulty)
        {
            switch (difficulty)
            {
                case LessonDifficulty.Basico:     return "BÁSICO";
                case LessonDifficulty.Intermedio: return "INTERMEDIO";
                case LessonDifficulty.Dificil:    return "DIFÍCIL";
                default:                          return difficulty.ToString().ToUpperInvariant();
            }
        }

        private void DrawArcadePanel(Rect rect, Color bgCol, Color borderCol)
        {
            Color old = GUI.color;

            // Sombra dura arcade de 3px
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(rect.x + 3, rect.y + 3, rect.width, rect.height), Texture2D.whiteTexture);

            // Fondo
            GUI.color = bgCol;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            // Borde duro estilo arcade de 2px
            GUI.color = borderCol;
            Draw2pxBorder(rect);

            GUI.color = old;
        }

        private GUIStyle GetFittedButtonStyle(GUIStyle baseStyle, string text, float maxWidth)
        {
            if (_tempFitStyle == null)
            {
                _tempFitStyle = new GUIStyle(baseStyle);
            }

            GUIContent content = new GUIContent(text);
            float measuredW = baseStyle.CalcSize(content).x;
            float usableW = Mathf.Max(20f, maxWidth - 10f);
            if (measuredW <= usableW)
            {
                return baseStyle;
            }

            // Reducir progresivamente solo en resoluciones muy estrechas para evitar recorte lateral
            _tempFitStyle.font = baseStyle.font;
            _tempFitStyle.fontStyle = baseStyle.fontStyle;
            _tempFitStyle.alignment = baseStyle.alignment;
            _tempFitStyle.padding = baseStyle.padding;
            _tempFitStyle.normal = baseStyle.normal;
            _tempFitStyle.hover = baseStyle.hover;
            _tempFitStyle.active = baseStyle.active;

            float ratio = usableW / measuredW;
            _tempFitStyle.fontSize = Mathf.Max(11, Mathf.FloorToInt(baseStyle.fontSize * ratio));
            return _tempFitStyle;
        }

        private bool DrawArcadeButton(Rect rect, string text, GUIStyle textStyle, bool isPrimary, bool isEnabled = true)
        {
            Color oldCol = GUI.color;

            if (!isEnabled)
            {
                GUI.enabled = false;
                // Sombra suave
                GUI.color = new Color(0f, 0f, 0f, 0.40f);
                GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), Texture2D.whiteTexture);
                // Fondo inactivo
                GUI.color = new Color(0.12f, 0.14f, 0.18f, 0.70f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                // Borde 2px
                GUI.color = new Color(0.22f, 0.26f, 0.32f, 0.60f);
                Draw2pxBorder(rect);

                GUI.color = oldCol;
                GUIStyle fittedDisabled = GetFittedButtonStyle(_disabledButtonStyle, text, rect.width);
                GUI.Label(rect, text, fittedDisabled);
                GUI.enabled = true;
                return false;
            }

            bool isHover = rect.Contains(Event.current.mousePosition);

            Color bgCol = isPrimary
                ? (isHover ? new Color(0.96f, 0.76f, 0.22f, 1f) : new Color(0.88f, 0.65f, 0.15f, 1f))
                : (isHover ? new Color(0.28f, 0.34f, 0.46f, 1f) : new Color(0.20f, 0.25f, 0.34f, 1f));

            Color borderCol = isPrimary
                ? (isHover ? Color.white : new Color(1f, 0.88f, 0.40f, 1f))
                : (isHover ? Color.white : new Color(0.38f, 0.48f, 0.62f, 1f));

            // Sombra dura arcade de 2px
            GUI.color = new Color(0f, 0f, 0f, 0.60f);
            GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), Texture2D.whiteTexture);

            // Fondo
            GUI.color = bgCol;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            // Borde duro arcade de 2px
            GUI.color = borderCol;
            Draw2pxBorder(rect);

            GUI.color = oldCol;

            GUIStyle fittedStyle = GetFittedButtonStyle(textStyle, text, rect.width);
            return GUI.Button(rect, text, fittedStyle);
        }

        private void Draw2pxBorder(Rect rect)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2, rect.width, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 2, rect.y, 2, rect.height), Texture2D.whiteTexture);
        }

        #endregion

        #region Inicialización de Estilos y Texturas

        private void InitStylesIfNeeded()
        {
            if (_stylesInitialized) return;

            Font boldFont = ChessFontProvider.SerifBold;
            Font regularFont = ChessFontProvider.SerifRegular;

            _modalBgTex = MakeColorTex(new Color(0.02f, 0.03f, 0.05f, 0.88f));
            _primaryBtnTex = MakeColorTex(new Color(0.85f, 0.65f, 0.15f, 1f));
            _secondaryBtnTex = MakeColorTex(new Color(0.24f, 0.28f, 0.36f, 1f));
            _disabledBtnTex = MakeColorTex(new Color(0.18f, 0.20f, 0.24f, 0.7f));
            _clearBtnTex = MakeColorTex(Color.clear);

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = boldFont,
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                richText = true,
                padding = new RectOffset(0, 0, 2, 2),
                normal = { textColor = new Color(0.96f, 0.86f, 0.38f, 1f) }
            };

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = boldFont,
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                richText = true,
                padding = new RectOffset(0, 0, 2, 2),
                normal = { textColor = new Color(0.90f, 0.94f, 0.98f, 1f) }
            };

            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = regularFont,
                fontSize = 19,
                wordWrap = true,
                richText = true,
                alignment = TextAnchor.UpperCenter,
                padding = new RectOffset(0, 0, 2, 2),
                normal = { textColor = new Color(0.88f, 0.92f, 0.97f, 1f) }
            };

            _badgeStyle = new GUIStyle(GUI.skin.label)
            {
                font = boldFont,
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                richText = true,
                padding = new RectOffset(0, 0, 2, 2),
                normal = { textColor = Color.white }
            };

            _selectorTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = boldFont,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                richText = true,
                padding = new RectOffset(0, 0, 2, 2),
                normal = { textColor = Color.white }
            };

            _selectorSubtitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = regularFont,
                fontSize = 18,
                wordWrap = true,
                richText = true,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(0, 0, 2, 2),
                normal = { textColor = new Color(0.78f, 0.84f, 0.92f, 1f) }
            };

            _primaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                font = boldFont,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                padding = new RectOffset(4, 4, 2, 2),
                normal = { background = _clearBtnTex, textColor = new Color(0.10f, 0.06f, 0.01f, 1f) },
                hover = { background = _clearBtnTex, textColor = new Color(0.05f, 0.03f, 0.00f, 1f) },
                active = { background = _clearBtnTex, textColor = new Color(0.05f, 0.03f, 0.00f, 1f) }
            };

            _secondaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                font = boldFont,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                padding = new RectOffset(4, 4, 2, 2),
                normal = { background = _clearBtnTex, textColor = Color.white },
                hover = { background = _clearBtnTex, textColor = Color.white },
                active = { background = _clearBtnTex, textColor = new Color(0.90f, 0.90f, 0.90f, 1f) }
            };

            _disabledButtonStyle = new GUIStyle(GUI.skin.button)
            {
                font = regularFont,
                fontSize = 17,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                padding = new RectOffset(4, 4, 2, 2),
                normal = { background = _clearBtnTex, textColor = new Color(0.55f, 0.58f, 0.65f, 1f) }
            };

            _stylesInitialized = true;
        }

        private Texture2D MakeColorTex(Color col)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color[] cols = new Color[4] { col, col, col, col };
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private void DestroyCachedTextures()
        {
            if (_modalBgTex != null) Destroy(_modalBgTex);
            if (_primaryBtnTex != null) Destroy(_primaryBtnTex);
            if (_secondaryBtnTex != null) Destroy(_secondaryBtnTex);
            if (_disabledBtnTex != null) Destroy(_disabledBtnTex);
            if (_clearBtnTex != null) Destroy(_clearBtnTex);
        }

        #endregion
    }
}

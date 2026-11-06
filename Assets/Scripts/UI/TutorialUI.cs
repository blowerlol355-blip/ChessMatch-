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
    /// 3. Modales arcade de Victoria y Reintento.
    /// Funciona de forma automática y responsive (9:16 y escritorio) tanto con uGUI como con OnGUI.
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

        // Estilos OnGUI Procedurales
        private bool _stylesInitialized = false;
        private GUIStyle _cardBoxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _primaryButtonStyle;
        private GUIStyle _secondaryButtonStyle;
        private GUIStyle _disabledButtonStyle;
        private GUIStyle _modalBoxStyle;

        private Texture2D _panelTex;
        private Texture2D _modalBgTex;
        private Texture2D _cardTex;
        private Texture2D _primaryBtnTex;
        private Texture2D _secondaryBtnTex;
        private Texture2D _disabledBtnTex;

        // Paleta de colores temáticos por nivel de dificultad
        private readonly Color _colorBasicoBadge = new Color(0.20f, 0.65f, 0.32f, 1f);     // Verde esmeralda
        private readonly Color _colorIntermedioBadge = new Color(0.18f, 0.52f, 0.92f, 1f); // Azul real
        private readonly Color _colorDificilBadge = new Color(0.85f, 0.22f, 0.25f, 1f);    // Rojo carmesí

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

            ResolveReferences();
        }

        private void Start()
        {
            ResolveReferences();
            SubscribeControllerEvents();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeControllerEvents();
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
                    // Pulso dorado sobre la casilla sugerida
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

            if (modalRootPanel != null) modalRootPanel.SetActive(true);
        }

        private void HandleLessonFailed(TutorialLessonData lesson)
        {
            _showFeedbackModal = true;
            _modalIsVictory = false;
            _modalTitle = "MOVIMIENTOS AGOTADOS";
            _modalMessage = $"Se han agotado las jugadas permitidas para este puzle.\nConsejo clave: {lesson.tacticalTip}\n¡Vuelve a intentarlo!";

            if (modalRootPanel != null) modalRootPanel.SetActive(true);
        }

        public void OpenLessonSelector()
        {
            _showSelector = true;
            _showFeedbackModal = false;

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

        #endregion

        #region Renderizado OnGUI Procedural Responsive (9:16 Mobile & Desktop)

        private void OnGUI()
        {
            if (controller == null) return;

            InitStylesIfNeeded();

            float screenW = Screen.width;
            float screenH = Screen.height;
            float scale = Mathf.Min(screenW / 1080f, screenH / 1920f);
            if (scale <= 0.05f) scale = 1f;

            // 1. Selector de Lecciones (Pantalla Completa)
            if (_showSelector)
            {
                RenderLessonSelectorGUI(screenW, screenH, scale);
                return;
            }

            // 2. HUD Durante la Lección Activa
            if (controller.IsLessonActive)
            {
                RenderLessonHudGUI(screenW, screenH, scale);
            }

            // 3. Modal de Feedback (Victoria o Derrota)
            if (_showFeedbackModal)
            {
                RenderFeedbackModalGUI(screenW, screenH, scale);
            }
        }

        /// <summary>
        /// Dibuja la pantalla de selección de lecciones con 3 bloques temáticos por dificultad.
        /// </summary>
        private void RenderLessonSelectorGUI(float screenW, float screenH, float scale)
        {
            GUI.depth = -50;
            // Fondo oscuro semitransparente con viñeta
            GUI.DrawTexture(new Rect(0, 0, screenW, screenH), _modalBgTex);

            float panelW = Mathf.Min(screenW * 0.94f, 960f * scale);
            float panelH = screenH * 0.94f;
            float panelX = (screenW - panelW) * 0.5f;
            float panelY = (screenH - panelH) * 0.5f;

            DrawArcadePanel(new Rect(panelX, panelY, panelW, panelH), new Color(0.08f, 0.10f, 0.15f, 0.98f), new Color(0.25f, 0.35f, 0.50f, 1f));

            float contentX = panelX + (20 * scale);
            float contentW = panelW - (40 * scale);
            float curY = panelY + (20 * scale);

            // Título Principal
            GUI.Label(new Rect(contentX, curY, contentW, 45 * scale), "★ MODO EDUCATIVO: ACADEMIA DE AJEDREZ ★", _titleStyle);
            curY += 50 * scale;

            GUI.Label(new Rect(contentX, curY, contentW, 25 * scale), "Aprende desde los fundamentos geométricos hasta tácticas de Gran Maestro", _subtitleStyle);
            curY += 35 * scale;

            // Renderizar los 3 grupos de dificultad
            var lessons = controller.Lessons;
            if (lessons != null && lessons.Count >= 6)
            {
                // BLOQUE 1: BÁSICO (Verde) - Lecciones 1, 2 y 3
                curY = RenderDifficultyGroup(contentX, curY, contentW, scale, "NIVEL BÁSICO: MOVIMIENTO FUNDAMENTAL", _colorBasicoBadge, 1, 3);
                curY += 15 * scale;

                // BLOQUE 2: INTERMEDIO (Azul/Dorado) - Lecciones 4 y 5
                curY = RenderDifficultyGroup(contentX, curY, contentW, scale, "NIVEL INTERMEDIO: DEFENSA Y CARTAS MENORES", _colorIntermedioBadge, 4, 5);
                curY += 15 * scale;

                // BLOQUE 3: DIFÍCIL (Rojo/Violeta) - Lección 6
                curY = RenderDifficultyGroup(contentX, curY, contentW, scale, "NIVEL DIFÍCIL: MAESTRÍA ROGUELIKE (PETROSIÁN)", _colorDificilBadge, 6, 6);
                curY += 20 * scale;
            }

            // Botón Salir al Menú Principal
            float btnH = 55 * scale;
            if (GUI.Button(new Rect(contentX, panelY + panelH - btnH - (15 * scale), contentW, btnH), "← VOLVER AL MENÚ PRINCIPAL", _secondaryButtonStyle))
            {
                controller.ExitTutorial();
            }
        }

        private float RenderDifficultyGroup(float x, float y, float w, float scale, string groupHeader, Color groupColor, int startId, int endId)
        {
            // Encabezado de categoría con badge de color
            float badgeH = 32 * scale;
            DrawArcadePanel(new Rect(x, y, w, badgeH), groupColor * 0.35f, groupColor);
            GUI.color = groupColor;
            GUI.Label(new Rect(x + (10 * scale), y + (4 * scale), w, badgeH), $"■ {groupHeader}", _subtitleStyle);
            GUI.color = Color.white;
            y += badgeH + (8 * scale);

            for (int id = startId; id <= endId; id++)
            {
                TutorialLessonData lesson = controller.GetLessonById(id);
                if (lesson == null) continue;

                bool completed = controller.IsLessonCompleted(id);
                bool unlocked = controller.IsLessonUnlocked(id);

                float cardH = 68 * scale;
                Rect cardRect = new Rect(x, y, w, cardH);

                Color borderColor = completed ? new Color(0.35f, 0.75f, 0.40f, 1f) : (unlocked ? groupColor : new Color(0.25f, 0.28f, 0.35f, 0.8f));
                Color bgCardColor = unlocked ? new Color(0.12f, 0.15f, 0.22f, 0.95f) : new Color(0.08f, 0.09f, 0.12f, 0.90f);

                DrawArcadePanel(cardRect, bgCardColor, borderColor);

                // Columna izquierda: Número y Título
                float textW = w * 0.68f;
                string titlePrefix = completed ? "✓ " : (unlocked ? $"{id}. " : "🔒 ");
                GUI.Label(new Rect(x + (12 * scale), y + (8 * scale), textW, 26 * scale), $"{titlePrefix}{lesson.title}", _badgeStyle);
                GUI.Label(new Rect(x + (12 * scale), y + (34 * scale), textW, 24 * scale), lesson.subtitle, _bodyStyle);

                // Columna derecha: Botón de Acción
                float btnW = w * 0.28f;
                float btnX = x + w - btnW - (10 * scale);
                float btnY = y + (10 * scale);
                float actionBtnH = cardH - (20 * scale);

                if (unlocked)
                {
                    string btnLabel = completed ? "REPETIR ↻" : "JUGAR ▶";
                    GUIStyle styleToUse = completed ? _secondaryButtonStyle : _primaryButtonStyle;

                    if (GUI.Button(new Rect(btnX, btnY, btnW, actionBtnH), btnLabel, styleToUse))
                    {
                        controller.LoadLesson(id);
                    }
                }
                else
                {
                    GUI.enabled = false;
                    GUI.Button(new Rect(btnX, btnY, btnW, actionBtnH), "BLOQUEADA 🔒", _disabledButtonStyle);
                    GUI.enabled = true;
                }

                y += cardH + (6 * scale);
            }

            return y;
        }

        /// <summary>
        /// Dibuja el HUD interactivo durante la lección activa (panel superior de guía y barra inferior).
        /// </summary>
        private void RenderLessonHudGUI(float screenW, float screenH, float scale)
        {
            TutorialLessonData lesson = controller.CurrentLesson;
            if (lesson == null) return;

            GUI.depth = 10;

            // 1. Tarjeta Superior Guía (Instrucciones de la lección)
            float bannerW = screenW * 0.94f;
            float bannerX = (screenW - bannerW) * 0.5f;
            float bannerY = screenH * 0.015f;
            float bannerH = 210f * scale;

            Color diffColor = GetDifficultyColor(lesson.difficulty);
            DrawArcadePanel(new Rect(bannerX, bannerY, bannerW, bannerH), new Color(0.08f, 0.11f, 0.16f, 0.96f), diffColor);

            float bContentX = bannerX + (15 * scale);
            float bContentW = bannerW - (30 * scale);
            float bCurY = bannerY + (10 * scale);

            // Badge de Dificultad y Lección
            string badgeText = $"[ LECCIÓN {lesson.lessonId}/6 · {lesson.difficulty.ToString().ToUpper()} ]";
            GUI.color = diffColor;
            GUI.Label(new Rect(bContentX, bCurY, bContentW, 24 * scale), badgeText, _badgeStyle);
            GUI.color = Color.white;
            bCurY += 26 * scale;

            // Título
            GUI.Label(new Rect(bContentX, bCurY, bContentW, 30 * scale), lesson.title, _titleStyle);
            bCurY += 32 * scale;

            // Instrucción Paso a Paso
            GUI.Label(new Rect(bContentX, bCurY, bContentW, 60 * scale), lesson.instructionText, _bodyStyle);
            bCurY += 62 * scale;

            // Fila de Estado: Contador de Jugadas y Objetivo
            int remainingMoves = Mathf.Max(0, lesson.maxMoves - controller.MovesMadeInLesson);
            string movesStr = $"Jugadas Restantes: <b>{remainingMoves} / {lesson.maxMoves}</b>";
            string reqStr = lesson.requiresCard ? $" | Carta Requerida: <b>{lesson.requiredCardName}</b>" : "";
            GUI.Label(new Rect(bContentX, bCurY, bContentW, 26 * scale), $"{movesStr}{reqStr}", _subtitleStyle);

            // 2. Barra Inferior de Controles y Navegación (Ubicada a la derecha del panel de historial, sin sobreponerse)
            float histWidth = Mathf.Min(250f * scale, screenW * 0.38f);
            float navX = (16f * scale) + histWidth + (12f * scale);
            float navW = screenW - navX - (16f * scale);
            float navH = 65f * scale;
            float navY = screenH - navH - (16f * scale);

            DrawArcadePanel(new Rect(navX, navY, navW, navH), new Color(0.08f, 0.10f, 0.15f, 0.96f), new Color(0.24f, 0.32f, 0.44f, 1f));

            float btnGap = 6 * scale;
            float btnW = (navW - (20 * scale) - (btnGap * 4)) / 5f;
            float btnY = navY + (12 * scale);
            float btnH = navH - (24 * scale);
            float curBtnX = navX + (10 * scale);

            // Botón 1: Lección Anterior
            GUI.enabled = lesson.lessonId > 1;
            if (GUI.Button(new Rect(curBtnX, btnY, btnW, btnH), "◄ Ant", _secondaryButtonStyle))
            {
                controller.PreviousLesson();
            }
            GUI.enabled = true;
            curBtnX += btnW + btnGap;

            // Botón 2: Reintentar
            if (GUI.Button(new Rect(curBtnX, btnY, btnW, btnH), "↻ Reintentar", _secondaryButtonStyle))
            {
                controller.RestartCurrentLesson();
            }
            curBtnX += btnW + btnGap;

            // Botón 3: Siguiente Lección
            GUI.enabled = lesson.lessonId < 6 && controller.IsLessonUnlocked(lesson.lessonId + 1);
            if (GUI.Button(new Rect(curBtnX, btnY, btnW, btnH), "Sig ►", _primaryButtonStyle))
            {
                controller.AdvanceToNextLesson();
            }
            GUI.enabled = true;
            curBtnX += btnW + btnGap;

            // Botón 4: Lista de Lecciones
            if (GUI.Button(new Rect(curBtnX, btnY, btnW, btnH), "☰ Lecciones", _secondaryButtonStyle))
            {
                OpenLessonSelector();
            }
            curBtnX += btnW + btnGap;

            // Botón 5: Salir al Menú
            if (GUI.Button(new Rect(curBtnX, btnY, btnW, btnH), "✕ Salir", _secondaryButtonStyle))
            {
                controller.ExitTutorial();
            }
        }

        /// <summary>
        /// Dibuja el modal de retroalimentación (Victoria o Derrota) tras una jugada decisiva.
        /// </summary>
        private void RenderFeedbackModalGUI(float screenW, float screenH, float scale)
        {
            GUI.depth = -100;
            GUI.DrawTexture(new Rect(0, 0, screenW, screenH), _modalBgTex);

            float modalW = Mathf.Min(screenW * 0.88f, 750f * scale);
            float modalH = 340f * scale;
            float modalX = (screenW - modalW) * 0.5f;
            float modalY = (screenH - modalH) * 0.5f;

            Color accentCol = _modalIsVictory ? new Color(0.35f, 0.85f, 0.40f, 1f) : new Color(0.90f, 0.25f, 0.25f, 1f);
            DrawArcadePanel(new Rect(modalX, modalY, modalW, modalH), new Color(0.07f, 0.09f, 0.13f, 0.98f), accentCol);

            float mX = modalX + (25 * scale);
            float mW = modalW - (50 * scale);
            float mY = modalY + (25 * scale);

            GUI.color = accentCol;
            GUI.Label(new Rect(mX, mY, mW, 36 * scale), _modalTitle, _titleStyle);
            GUI.color = Color.white;
            mY += 45 * scale;

            GUI.Label(new Rect(mX, mY, mW, 110 * scale), _modalMessage, _bodyStyle);
            mY += 125 * scale;

            float mBtnH = 55 * scale;
            float mBtnW = (mW - (15 * scale)) / 2f;

            if (_modalIsVictory)
            {
                if (GUI.Button(new Rect(mX, mY, mBtnW, mBtnH), "Reintentar ↻", _secondaryButtonStyle))
                {
                    _showFeedbackModal = false;
                    controller.RestartCurrentLesson();
                }

                bool hasNext = controller.CurrentLessonId < 6;
                string nextLabel = hasNext ? "Siguiente Lección ►" : "Menú de Lecciones ☰";
                if (GUI.Button(new Rect(mX + mBtnW + (15 * scale), mY, mBtnW, mBtnH), nextLabel, _primaryButtonStyle))
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
                if (GUI.Button(new Rect(mX, mY, mBtnW, mBtnH), "Reintentar Lección ↻", _primaryButtonStyle))
                {
                    _showFeedbackModal = false;
                    controller.RestartCurrentLesson();
                }

                if (GUI.Button(new Rect(mX + mBtnW + (15 * scale), mY, mBtnW, mBtnH), "Menú de Lecciones ☰", _secondaryButtonStyle))
                {
                    _showFeedbackModal = false;
                    OpenLessonSelector();
                }
            }
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

        private void DrawArcadePanel(Rect rect, Color bgCol, Color borderCol)
        {
            Color old = GUI.color;

            // Sombra dura arcade de 3px
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(rect.x + 3, rect.y + 3, rect.width, rect.height), Texture2D.whiteTexture);

            // Fondo
            GUI.color = bgCol;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            // Borde duro de 2px
            GUI.color = borderCol;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2, rect.width, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 2, rect.y, 2, rect.height), Texture2D.whiteTexture);

            GUI.color = old;
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

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = boldFont,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.96f, 0.86f, 0.38f, 1f) }
            };

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = boldFont,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.85f, 0.90f, 0.95f, 1f) }
            };

            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = regularFont,
                fontSize = 14,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.82f, 0.86f, 0.92f, 1f) }
            };

            _badgeStyle = new GUIStyle(GUI.skin.label)
            {
                font = boldFont,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _primaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                font = boldFont,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _primaryBtnTex, textColor = new Color(0.12f, 0.08f, 0.02f, 1f) }
            };

            _secondaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                font = boldFont,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _secondaryBtnTex, textColor = Color.white }
            };

            _disabledButtonStyle = new GUIStyle(GUI.skin.button)
            {
                font = regularFont,
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _disabledBtnTex, textColor = new Color(0.55f, 0.58f, 0.65f, 1f) }
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
        }

        #endregion
    }
}


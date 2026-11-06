using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChessBalatro
{
    /// <summary>
    /// Definición de una lección individual del Modo Educativo.
    /// </summary>
    [Serializable]
    public class TutorialLesson
    {
        public int lessonNumber;
        public string title;
        public string subtitle;
        [TextArea(3, 6)] public string instructions;
        [TextArea(2, 4)] public string petrosianTip;
        public int maxMoves;
        public int requiredCaptures;
        public bool isDefenseCheckLesson;

        // Delegados de configuración y validación específica
        public Action<BoardState> setupBoardAction;
        public Func<Move, bool> moveRuleFilter;
        public Func<BoardState, bool> customWinCondition;
    }

    /// <summary>
    /// Controlador central del Modo Educativo (Tutorial).
    /// Diseñado para enseñar ajedrez desde cero mediante 5 lecciones interactivas progresivas.
    /// Inyecta estados de tablero reducidos en <see cref="BoardView"/>, restringe movimientos
    /// que no correspondan a la regla enseñada a través de <see cref="BoardInputHandler"/>
    /// y proporciona retroalimentación táctica e interfaz visual responsive (9:16).
    /// </summary>
    public class TutorialLessonManager : MonoBehaviour
    {
        public static TutorialLessonManager Instance { get; private set; }

        [Header("Referencias de Tablero")]
        [SerializeField] private BoardView boardView;
        [SerializeField] private BoardInputHandler inputHandler;

        [Header("Estado de la Lección")]
        [SerializeField] private int currentLessonIndex = 0; // 0..4 (Lecciones 1..5)
        [SerializeField] private int movesMadeInLesson = 0;
        [SerializeField] private int remainingTargets = 0;
        [SerializeField] private bool isLessonActive = false;
        [SerializeField] private bool isLessonCompleted = false;
        [SerializeField] private bool isLessonFailed = false;

        [Header("UI Canvas uGUI (Opcional)")]
        [SerializeField] private GameObject tutorialRootPanel;
        [SerializeField] private Text lessonHeaderBadgeText;
        [SerializeField] private Text lessonTitleText;
        [SerializeField] private Text lessonSubtitleText;
        [SerializeField] private Text lessonInstructionText;
        [SerializeField] private Text lessonTipText;
        [SerializeField] private Text movesCounterText;
        [SerializeField] private Text targetsCounterText;
        [SerializeField] private GameObject feedbackModalPanel;
        [SerializeField] private Text feedbackTitleText;
        [SerializeField] private Text feedbackMessageText;
        [SerializeField] private Button nextLessonButton;
        [SerializeField] private Button retryLessonButton;
        [SerializeField] private Button prevLessonButton;
        [SerializeField] private Button exitTutorialButton;

        public int CurrentLessonIndex => currentLessonIndex;
        public bool IsLessonActive => isLessonActive;
        public bool IsLessonCompleted => isLessonCompleted;

        private readonly List<TutorialLesson> _lessons = new List<TutorialLesson>();
        private TutorialLesson CurrentLesson => (_lessons != null && currentLessonIndex >= 0 && currentLessonIndex < _lessons.Count)
            ? _lessons[currentLessonIndex]
            : null;

        // Estilos para OnGUI fallback
        private bool _onGuiStylesReady = false;
        private GUIStyle _topBannerStyle;
        private GUIStyle _cardBoxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _instructionStyle;
        private GUIStyle _tipBoxStyle;
        private GUIStyle _hudBadgeStyle;
        private GUIStyle _modalOverlayStyle;
        private GUIStyle _modalBoxStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _secondaryButtonStyle;
        private Texture2D _bannerTex;
        private Texture2D _cardTex;
        private Texture2D _tipTex;
        private Texture2D _btnTex;
        private Texture2D _modalTex;

        public event Action<TutorialLesson> OnLessonStarted;
        public event Action<TutorialLesson> OnLessonPassed;
        public event Action<TutorialLesson> OnLessonFailedEvent;

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

            if (GetComponent<TutorialController>() == null && FindAnyObjectByType<TutorialController>() == null)
            {
                gameObject.AddComponent<TutorialController>();
            }
            if (GetComponent<TutorialUI>() == null && FindAnyObjectByType<TutorialUI>() == null)
            {
                gameObject.AddComponent<TutorialUI>();
            }

            BuildLessonsDatabase();
            SetupUguiButtons();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            UnsubscribeBoardEvents();
            DestroyCachedTextures();
        }

        private void ResolveReferences()
        {
            if (boardView == null) boardView = FindAnyObjectByType<BoardView>();
            if (inputHandler == null && boardView != null) inputHandler = boardView.InputHandler;
            if (inputHandler == null) inputHandler = FindAnyObjectByType<BoardInputHandler>();
        }

        private void SetupUguiButtons()
        {
            if (nextLessonButton != null)
                nextLessonButton.onClick.AddListener(AdvanceToNextLesson);

            if (retryLessonButton != null)
                retryLessonButton.onClick.AddListener(RestartCurrentLesson);

            if (prevLessonButton != null)
                prevLessonButton.onClick.AddListener(GoToPreviousLesson);

            if (exitTutorialButton != null)
                exitTutorialButton.onClick.AddListener(ExitTutorialToMenu);
        }

        #region Definición de las 5 Lecciones Interactivas

        private void BuildLessonsDatabase()
        {
            _lessons.Clear();

            // =========================================================================
            // LECCIÓN 1: La Torre y el Alfil (Líneas rectas y diagonales)
            // =========================================================================
            var l1 = new TutorialLesson
            {
                lessonNumber = 1,
                title = "Lección 1: La Torre y el Alfil",
                subtitle = "Líneas rectas (ortogonales) y diagonales",
                instructions = "La Torre domina filas y columnas en línea recta. El Alfil se desliza en diagonal respetando siempre su color de casilla. ¡Captura los dos peones negros objetivo!",
                petrosianTip = "«Las piezas colocadas en líneas y diagonales abiertas multiplican su potencia geométrica y controlan el tablero.»",
                maxMoves = 2,
                requiredCaptures = 2,
                isDefenseCheckLesson = false,
                setupBoardAction = (board) =>
                {
                    board.Clear();
                    // Torre blanca en d1, Alfil blanco en c1, Rey blanco protegido en a1
                    board[3, 0] = new Piece(PieceType.Rook, PieceColor.White);   // d1
                    board[2, 0] = new Piece(PieceType.Bishop, PieceColor.White); // c1
                    board[0, 0] = new Piece(PieceType.King, PieceColor.White);   // a1

                    // Objetivos: Peón en d5 (recto para Torre) y Peón en f4 (diagonal c1->f4 para Alfil)
                    board[3, 4] = new Piece(PieceType.Pawn, PieceColor.Black);   // d5
                    board[5, 3] = new Piece(PieceType.Pawn, PieceColor.Black);   // f4

                    board.CurrentTurn = PieceColor.White;
                },
                moveRuleFilter = (move) =>
                {
                    // Solo permitir movimientos de Torre o Alfil
                    if (move.PieceMoved == null) return false;
                    return move.PieceMoved.Type == PieceType.Rook || move.PieceMoved.Type == PieceType.Bishop;
                }
            };
            _lessons.Add(l1);

            // =========================================================================
            // LECCIÓN 2: El Caballo (El salto en 'L')
            // =========================================================================
            var l2 = new TutorialLesson
            {
                lessonNumber = 2,
                title = "Lección 2: El Caballo",
                subtitle = "El salto en 'L' y superación de obstáculos",
                instructions = "El Caballo salta en 'L' (2 casillas en una dirección y 1 en perpendicular). ¡Es la única pieza que puede saltar sobre otras! Salta la barrera y captura los dos objetivos.",
                petrosianTip = "«El caballo es el mejor saltador y bloqueador. En posiciones cerradas supera ampliamente a las piezas lineales.»",
                maxMoves = 3,
                requiredCaptures = 2,
                isDefenseCheckLesson = false,
                setupBoardAction = (board) =>
                {
                    board.Clear();
                    // Caballo blanco en b1, Rey en a1
                    board[1, 0] = new Piece(PieceType.Knight, PieceColor.White); // b1
                    board[0, 0] = new Piece(PieceType.King, PieceColor.White);   // a1

                    // Barrera de peones propios demostrando que el caballo salta por encima
                    board[0, 1] = new Piece(PieceType.Pawn, PieceColor.White);   // a2
                    board[1, 1] = new Piece(PieceType.Pawn, PieceColor.White);   // b2
                    board[2, 1] = new Piece(PieceType.Pawn, PieceColor.White);   // c2

                    // Objetivos negros: en c3 (primer salto b1->c3) y e4 (segundo salto c3->e4)
                    board[2, 2] = new Piece(PieceType.Pawn, PieceColor.Black);   // c3
                    board[4, 3] = new Piece(PieceType.Pawn, PieceColor.Black);   // e4

                    board.CurrentTurn = PieceColor.White;
                },
                moveRuleFilter = (move) =>
                {
                    // Bloquear cualquier pieza que no sea el Caballo
                    return move.PieceMoved != null && move.PieceMoved.Type == PieceType.Knight;
                }
            };
            _lessons.Add(l2);

            // =========================================================================
            // LECCIÓN 3: El Peón (Avance simple, doble y captura diagonal)
            // =========================================================================
            var l3 = new TutorialLesson
            {
                lessonNumber = 3,
                title = "Lección 3: El Peón",
                subtitle = "Avance simple, doble inicial y captura diagonal",
                instructions = "El Peón avanza recto, pero en su casilla inicial puede dar un salto doble (2 casillas). Solo captura en diagonal hacia adelante. ¡Avanza dos casillas y luego captura al objetivo!",
                petrosianTip = "«Los peones son el alma del ajedrez; su estructura define dónde atacar y cómo defenderse sólidamente.»",
                maxMoves = 2,
                requiredCaptures = 1,
                isDefenseCheckLesson = false,
                setupBoardAction = (board) =>
                {
                    board.Clear();
                    // Peón blanco en e2 (casilla inicial), Rey en a1
                    board[4, 1] = new Piece(PieceType.Pawn, PieceColor.White);   // e2
                    board[0, 0] = new Piece(PieceType.King, PieceColor.White);   // a1

                    // Objetivo negro colocado en d5 para ser capturado en diagonal tras el avance e2->e4
                    board[3, 4] = new Piece(PieceType.Pawn, PieceColor.Black);   // d5

                    board.CurrentTurn = PieceColor.White;
                },
                moveRuleFilter = (move) =>
                {
                    // Solo permitir movimientos de Peón
                    return move.PieceMoved != null && move.PieceMoved.Type == PieceType.Pawn;
                }
            };
            _lessons.Add(l3);

            // =========================================================================
            // LECCIÓN 4: La Dama y el Rey
            // =========================================================================
            var l4 = new TutorialLesson
            {
                lessonNumber = 4,
                title = "Lección 4: La Dama y el Rey",
                subtitle = "El poder absoluto de la Dama y la seguridad del Rey",
                instructions = "La Dama combina el poder de Torre y Alfil en cualquier dirección. El Rey se mueve en cualquier dirección pero solo una casilla a la vez. ¡Usa la Dama para abatir la torre lejana y el Rey para tomar el peón adyacente!",
                petrosianTip = "«Una Dama poderosa sin un Rey seguro es una trampa. La verdadera maestría reside en la armonía de ambas piezas.»",
                maxMoves = 2,
                requiredCaptures = 2,
                isDefenseCheckLesson = false,
                setupBoardAction = (board) =>
                {
                    board.Clear();
                    // Dama en d1, Rey en e1
                    board[3, 0] = new Piece(PieceType.Queen, PieceColor.White);  // d1
                    board[4, 0] = new Piece(PieceType.King, PieceColor.White);   // e1

                    // Objetivos: Torre negra en d7 (largo alcance para Dama) y Peón en e2 (adyacente para Rey)
                    board[3, 6] = new Piece(PieceType.Rook, PieceColor.Black);   // d7
                    board[4, 1] = new Piece(PieceType.Pawn, PieceColor.Black);   // e2

                    board.CurrentTurn = PieceColor.White;
                },
                moveRuleFilter = (move) =>
                {
                    return move.PieceMoved != null && (move.PieceMoved.Type == PieceType.Queen || move.PieceMoved.Type == PieceType.King);
                }
            };
            _lessons.Add(l4);

            // =========================================================================
            // LECCIÓN 5: ¿Qué es el Jaque y cómo defenderse? (El Escudo de Petrosián)
            // =========================================================================
            var l5 = new TutorialLesson
            {
                lessonNumber = 5,
                title = "Lección 5: El Jaque y la Defensa",
                subtitle = "Defensa profiláctica y el 'Escudo de Petrosián'",
                instructions = "¡Tu Rey está en Jaque por la Torre en e8! Las 3 formas de defenderse: 1. Mover el Rey, 2. Interponer una pieza bloqueando la línea de ataque ('Escudo de Petrosián'), o 3. Capturar la pieza atacante. ¡Interpón la Torre blanca en e2 o mueve el Rey para salvarlo!",
                petrosianTip = "«Tigrán Petrosián enseñó que la defensa impenetrable consiste en neutralizar las amenazas antes de que maduren. ¡Erige tu escudo defensivo!»",
                maxMoves = 1,
                requiredCaptures = 0,
                isDefenseCheckLesson = true,
                setupBoardAction = (board) =>
                {
                    board.Clear();
                    // Rey blanco en e1 (bajo jaque vertical desde e8)
                    board[4, 0] = new Piece(PieceType.King, PieceColor.White);   // e1
                    // Torre blanca en a2 lista para interponerse en e2 (Escudo)
                    board[0, 1] = new Piece(PieceType.Rook, PieceColor.White);   // a2

                    // Torre negra en e8 atacando al Rey blanco en e1
                    board[4, 7] = new Piece(PieceType.Rook, PieceColor.Black);   // e8
                    // Rey negro seguro en h8
                    board[7, 7] = new Piece(PieceType.King, PieceColor.Black);   // h8

                    board.CurrentTurn = PieceColor.White;
                },
                moveRuleFilter = (move) =>
                {
                    // Cualquier jugada legal que anule el jaque está permitida
                    return true;
                },
                customWinCondition = (board) =>
                {
                    // Victoria si las blancas ya no están en jaque
                    return !board.IsInCheck(PieceColor.White);
                }
            };
            _lessons.Add(l5);
        }

        #endregion

        #region Flujo y Ejecución de Lecciones

        /// <summary>
        /// Inicia el Modo Educativo comenzando por la lección especificada (por defecto la primera).
        /// </summary>
        public void StartEducationMode(int lessonIndex = 0)
        {
            if (TutorialController.Instance != null)
            {
                TutorialController.Instance.StartEducationMode(lessonIndex + 1);
                return;
            }

            ResolveReferences();
            currentLessonIndex = Mathf.Clamp(lessonIndex, 0, _lessons.Count - 1);
            isLessonActive = true;

            if (GameManager.Instance != null)
            {
                GameManager.SelectedGameMode = GameMode.Education;
                GameManager.Instance.SetState(GameState.Playing);
            }

            // Desactivar IA rival durante las lecciones
            if (inputHandler != null)
            {
                inputHandler.EnableAI = false;
            }

            LoadLesson(currentLessonIndex);
        }

        /// <summary>
        /// Carga y configura el estado inicial del tablero para la lección activa.
        /// </summary>
        public void LoadLesson(int index)
        {
            ResolveReferences();
            if (index < 0 || index >= _lessons.Count) return;

            currentLessonIndex = index;
            TutorialLesson lesson = CurrentLesson;

            movesMadeInLesson = 0;
            isLessonCompleted = false;
            isLessonFailed = false;
            isLessonActive = true;

            // 1. Crear nuevo BoardState reducido
            BoardState lessonBoard = new BoardState();
            lesson.setupBoardAction?.Invoke(lessonBoard);

            // 2. Contar piezas objetivo iniciales (negras no-rey)
            remainingTargets = CountTargetPieces(lessonBoard);

            // 3. Inyectar en BoardView
            if (boardView != null)
            {
                boardView.LoadCustomPosition(lessonBoard);
            }

            // 4. Inyectar filtro de movimientos en BoardInputHandler
            if (inputHandler != null)
            {
                inputHandler.EnableAI = false;
                inputHandler.SetMoveFilter(lesson.moveRuleFilter);
            }

            // 5. Suscribir a jugadas
            SubscribeBoardEvents();

            // 6. Actualizar UI
            UpdateUguiElements();
            if (tutorialRootPanel != null) tutorialRootPanel.SetActive(true);
            if (feedbackModalPanel != null) feedbackModalPanel.SetActive(false);

            Debug.Log($"<color=cyan><b>[TutorialLessonManager] Iniciando: {lesson.title}</b></color>");
            OnLessonStarted?.Invoke(lesson);
        }

        private void SubscribeBoardEvents()
        {
            if (boardView != null)
            {
                boardView.OnMoveExecuted -= HandleMoveExecuted;
                boardView.OnMoveExecuted += HandleMoveExecuted;
            }
        }

        private void UnsubscribeBoardEvents()
        {
            if (boardView != null)
            {
                boardView.OnMoveExecuted -= HandleMoveExecuted;
            }
        }

        private void HandleMoveExecuted(Move move)
        {
            if (!isLessonActive || isLessonCompleted || isLessonFailed) return;

            movesMadeInLesson++;
            TutorialLesson lesson = CurrentLesson;
            if (lesson == null || boardView == null || boardView.BoardState == null) return;

            BoardState board = boardView.BoardState;

            // Comprobar condición de victoria según tipo de lección
            if (lesson.isDefenseCheckLesson)
            {
                bool defPassed = lesson.customWinCondition != null ? lesson.customWinCondition(board) : !board.IsInCheck(PieceColor.White);
                if (defPassed)
                {
                    CompleteLesson();
                    return;
                }
            }
            else
            {
                remainingTargets = CountTargetPieces(board);
                if (remainingTargets <= 0)
                {
                    CompleteLesson();
                    return;
                }
            }

            // Comprobar si se agotaron los movimientos disponibles
            if (movesMadeInLesson >= lesson.maxMoves)
            {
                FailLesson();
                return;
            }

            // Si aún no termina, asegurar que el turno continúe en Blancas (para que el estudiante siga jugando)
            board.CurrentTurn = PieceColor.White;
            UpdateUguiElements();
        }

        private int CountTargetPieces(BoardState board)
        {
            if (board == null) return 0;
            int count = 0;
            for (int f = 0; f < BoardState.BoardSize; f++)
            {
                for (int r = 0; r < BoardState.BoardSize; r++)
                {
                    Piece p = board[f, r];
                    if (p != null && p.Color == PieceColor.Black && p.Type != PieceType.King)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private void CompleteLesson()
        {
            isLessonCompleted = true;
            isLessonFailed = false;

            if (inputHandler != null)
            {
                inputHandler.ClearMoveFilter();
            }

            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.TriggerHapticFeedback();
            }

            ShowFeedbackModal(
                "¡LECCIÓN SUPERADA!",
                $"¡Excelente trabajo! Has completado con éxito la {CurrentLesson?.title}. Has demostrado dominio táctico de esta regla.",
                isVictory: true
            );

            Debug.Log($"<color=green><b>[TutorialLessonManager] Lección superada con éxito ({CurrentLesson?.title})</b></color>");
            OnLessonPassed?.Invoke(CurrentLesson);
        }

        private void FailLesson()
        {
            isLessonFailed = true;
            isLessonCompleted = false;

            if (inputHandler != null)
            {
                inputHandler.ClearMoveFilter();
            }

            ShowFeedbackModal(
                "MOVIMIENTOS AGOTADOS",
                "No has alcanzado el objetivo en el número de jugadas permitido. Repasa la regla explicada y vuelve a intentarlo.",
                isVictory: false
            );

            Debug.Log($"<color=orange><b>[TutorialLessonManager] Lección fallida: movimientos agotados ({CurrentLesson?.title})</b></color>");
            OnLessonFailedEvent?.Invoke(CurrentLesson);
        }

        public void RestartCurrentLesson()
        {
            LoadLesson(currentLessonIndex);
        }

        public void AdvanceToNextLesson()
        {
            if (currentLessonIndex < _lessons.Count - 1)
            {
                LoadLesson(currentLessonIndex + 1);
            }
            else
            {
                // Todas las lecciones completadas
                ShowFeedbackModal(
                    "¡GRADUACIÓN DE AJEDREZ!",
                    "¡Felicitaciones! Has completado todas las lecciones del Modo Educativo. Estás listo para desafiar a los Grandes Maestros en el Modo Roguelike.",
                    isVictory: true
                );
            }
        }

        public void GoToPreviousLesson()
        {
            if (currentLessonIndex > 0)
            {
                LoadLesson(currentLessonIndex - 1);
            }
        }

        public void ExitTutorialToMenu()
        {
            isLessonActive = false;
            UnsubscribeBoardEvents();

            if (inputHandler != null)
            {
                inputHandler.ClearMoveFilter();
                inputHandler.EnableAI = true;
            }

            if (tutorialRootPanel != null) tutorialRootPanel.SetActive(false);
            if (feedbackModalPanel != null) feedbackModalPanel.SetActive(false);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToMenu();
            }
        }

        #endregion

        #region Actualización de UI uGUI

        private void UpdateUguiElements()
        {
            TutorialLesson lesson = CurrentLesson;
            if (lesson == null) return;

            if (lessonHeaderBadgeText != null)
                lessonHeaderBadgeText.text = $"LECCIÓN {lesson.lessonNumber} DE {_lessons.Count}";

            if (lessonTitleText != null)
                lessonTitleText.text = lesson.title;

            if (lessonSubtitleText != null)
                lessonSubtitleText.text = lesson.subtitle;

            if (lessonInstructionText != null)
                lessonInstructionText.text = lesson.instructions;

            if (lessonTipText != null)
                lessonTipText.text = lesson.petrosianTip;

            if (movesCounterText != null)
                movesCounterText.text = $"Jugadas: {movesMadeInLesson} / {lesson.maxMoves}";

            if (targetsCounterText != null)
            {
                if (lesson.isDefenseCheckLesson)
                    targetsCounterText.text = "Objetivo: ¡Anular el Jaque!";
                else
                    targetsCounterText.text = $"Objetivos restantes: {remainingTargets}";
            }

            if (prevLessonButton != null)
                prevLessonButton.interactable = currentLessonIndex > 0;

            if (nextLessonButton != null)
                nextLessonButton.interactable = isLessonCompleted;
        }

        private void ShowFeedbackModal(string title, string message, bool isVictory)
        {
            if (feedbackModalPanel != null)
            {
                feedbackModalPanel.SetActive(true);
                if (feedbackTitleText != null) feedbackTitleText.text = title;
                if (feedbackMessageText != null) feedbackMessageText.text = message;
            }
            UpdateUguiElements();
        }

        #endregion

        #region OnGUI Fallback Responsive Móvil (9:16 Portrait)

        private void OnGUI()
        {
            if (TutorialUI.Instance != null) return;
            if (!isLessonActive) return;

            // Si los paneles uGUI están activos y asignados, no renderizar fallback duplicado
            if (tutorialRootPanel != null && tutorialRootPanel.activeInHierarchy) return;

            InitOnGuiStyles();

            float screenW = Screen.width;
            float screenH = Screen.height;
            float scale = Mathf.Min(screenW / 1080f, screenH / 1920f);
            if (scale <= 0.05f) scale = 1f;

            TutorialLesson lesson = CurrentLesson;
            if (lesson == null) return;

            // 1. Tarjeta Superior Guía (Instrucciones de la lección)
            float bannerW = screenW * 0.94f;
            float bannerX = (screenW - bannerW) * 0.5f;
            float bannerY = screenH * 0.02f;
            float bannerH = 260f * scale;

            GUILayout.BeginArea(new Rect(bannerX, bannerY, bannerW, bannerH), _cardBoxStyle);
            {
                GUILayout.Label($"LECCIÓN {lesson.lessonNumber} DE {_lessons.Count}", _hudBadgeStyle);
                GUILayout.Label(lesson.title, _titleStyle);
                GUILayout.Label(lesson.subtitle, _subtitleStyle);
                GUILayout.Space(6 * scale);
                GUILayout.Label(lesson.instructions, _instructionStyle);
                GUILayout.Space(6 * scale);

                // Contador de progreso
                string progressStr = lesson.isDefenseCheckLesson
                    ? $"[OBJETIVO: ANULAR EL JAQUE] | Jugadas: {movesMadeInLesson}/{lesson.maxMoves}"
                    : $"Objetivos restantes: {remainingTargets} | Jugadas: {movesMadeInLesson}/{lesson.maxMoves}";
                GUILayout.Label(progressStr, _hudBadgeStyle);
            }
            GUILayout.EndArea();

            // 2. Barra Inferior con Consejo de Petrosián y Botones de Control
            float botH = 210f * scale;
            float botY = screenH - botH - (screenH * 0.02f);

            GUILayout.BeginArea(new Rect(bannerX, botY, bannerW, botH), _cardBoxStyle);
            {
                GUILayout.Label($"Consejo de Tigrán Petrosián:", _subtitleStyle);
                GUILayout.Label(lesson.petrosianTip, _instructionStyle);
                GUILayout.Space(8 * scale);

                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("◄ Anterior", _secondaryButtonStyle, GUILayout.Height(50 * scale)))
                    {
                        GoToPreviousLesson();
                    }

                    if (GUILayout.Button("Reintentar ↻", _secondaryButtonStyle, GUILayout.Height(50 * scale)))
                    {
                        RestartCurrentLesson();
                    }

                    GUI.enabled = currentLessonIndex < _lessons.Count - 1;
                    if (GUILayout.Button("Siguiente ►", _buttonStyle, GUILayout.Height(50 * scale)))
                    {
                        AdvanceToNextLesson();
                    }
                    GUI.enabled = true;

                    if (GUILayout.Button("Salir ✕", _secondaryButtonStyle, GUILayout.Height(50 * scale)))
                    {
                        ExitTutorialToMenu();
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();

            // 3. Modal de Victoria o Fallo
            if (isLessonCompleted || isLessonFailed)
            {
                GUI.depth = -100;
                GUI.DrawTexture(new Rect(0, 0, screenW, screenH), _modalTex);

                float modalW = screenW * 0.88f;
                float modalH = 340f * scale;
                float modalX = (screenW - modalW) * 0.5f;
                float modalY = (screenH - modalH) * 0.5f;

                GUILayout.BeginArea(new Rect(modalX, modalY, modalW, modalH), _modalBoxStyle);
                {
                    string mTitle = isLessonCompleted ? "¡LECCIÓN SUPERADA!" : "MOVIMIENTOS AGOTADOS";
                    string mColor = isLessonCompleted ? "#4CAF50" : "#E53935";
                    GUILayout.Label($"<color={mColor}><b>{mTitle}</b></color>", _titleStyle);
                    GUILayout.Space(10 * scale);

                    string mDesc = isLessonCompleted
                        ? $"Has demostrado dominio de {lesson.title}. ¡Excelente ejecución táctica!"
                        : "Se han agotado los movimientos permitidos sin completar el objetivo. Repasa la regla e inténtalo de nuevo.";
                    GUILayout.Label(mDesc, _instructionStyle);
                    GUILayout.Space(20 * scale);

                    GUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button("Reintentar ↻", _secondaryButtonStyle, GUILayout.Height(55 * scale)))
                        {
                            RestartCurrentLesson();
                        }

                        if (isLessonCompleted)
                        {
                            if (currentLessonIndex < _lessons.Count - 1)
                            {
                                if (GUILayout.Button("Siguiente Lección ►", _buttonStyle, GUILayout.Height(55 * scale)))
                                {
                                    AdvanceToNextLesson();
                                }
                            }
                            else
                            {
                                if (GUILayout.Button("¡Graduación! Menú Principal", _buttonStyle, GUILayout.Height(55 * scale)))
                                {
                                    ExitTutorialToMenu();
                                }
                            }
                        }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndArea();
            }
        }

        private void InitOnGuiStyles()
        {
            if (_onGuiStylesReady) return;

            float scale = ChessFontProvider.GetResponsiveScale();

            _cardTex = MakeSolidColorTex(2, 2, new Color(0.10f, 0.12f, 0.16f, 0.94f));
            _modalTex = MakeSolidColorTex(2, 2, new Color(0f, 0f, 0f, 0.82f));
            _btnTex = MakeSolidColorTex(2, 2, new Color(0.85f, 0.65f, 0.15f, 1f));
            _tipTex = MakeSolidColorTex(2, 2, new Color(0.18f, 0.22f, 0.28f, 0.95f));

            Font boldSerif = ChessFontProvider.SerifBold;
            Font regularSerif = ChessFontProvider.SerifRegular;

            _cardBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _cardTex },
                padding = new RectOffset((int)(20 * scale), (int)(20 * scale), (int)(15 * scale), (int)(15 * scale))
            };

            _modalBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _tipTex },
                padding = new RectOffset((int)(28 * scale), (int)(28 * scale), (int)(25 * scale), (int)(25 * scale))
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = boldSerif,
                fontSize = Mathf.RoundToInt(28 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.96f, 0.86f, 0.40f, 1f) }
            };

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = regularSerif,
                fontSize = Mathf.RoundToInt(20 * scale),
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.80f, 0.85f, 0.90f, 1f) }
            };

            _instructionStyle = new GUIStyle(GUI.skin.label)
            {
                font = regularSerif,
                fontSize = Mathf.RoundToInt(18 * scale),
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.92f, 0.93f, 0.95f, 1f) }
            };

            _hudBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                font = boldSerif,
                fontSize = Mathf.RoundToInt(17 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.35f, 0.85f, 0.45f, 1f) }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = boldSerif,
                fontSize = Mathf.RoundToInt(20 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _btnTex, textColor = new Color(0.10f, 0.08f, 0.02f, 1f) }
            };

            _secondaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                font = boldSerif,
                fontSize = Mathf.RoundToInt(18 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _onGuiStylesReady = true;
        }

        private Texture2D MakeSolidColorTex(int width, int height, Color color)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void DestroyCachedTextures()
        {
            if (_cardTex != null) Destroy(_cardTex);
            if (_modalTex != null) Destroy(_modalTex);
            if (_btnTex != null) Destroy(_btnTex);
            if (_tipTex != null) Destroy(_tipTex);
        }

        #endregion
    }
}


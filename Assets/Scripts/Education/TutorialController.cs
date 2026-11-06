using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Gestor central del Modo Educativo estructurado en 6 lecciones progresivas
    /// en 3 niveles de dificultad (3 Básicas, 2 Intermedias y 1 Difícil).
    /// Controla la inyección de posiciones, validación paso a paso de jugadas,
    /// uso de cartas y guardado de progreso en PlayerPrefs.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        public static TutorialController Instance { get; private set; }

        private const string PREF_KEY_COMPLETED = "Tutorial_Lesson_Completed_";
        private const string PREF_KEY_MAX_UNLOCKED = "Tutorial_MaxUnlockedLesson";

        [Header("Referencias de Escena")]
        [SerializeField] private BoardView boardView;
        [SerializeField] private BoardInputHandler inputHandler;
        [SerializeField] private CardManager cardManager;

        [Header("Estado de la Lección Actual")]
        [SerializeField] private int currentLessonId = 1; // 1 a 6
        [SerializeField] private int movesMadeInLesson = 0;
        [SerializeField] private bool isLessonActive = false;
        [SerializeField] private bool isLessonCompleted = false;
        [SerializeField] private bool isLessonFailed = false;
        [SerializeField] private bool cardWasUsedInLesson = false;

        private readonly List<TutorialLessonData> _lessons = new List<TutorialLessonData>();

        public IReadOnlyList<TutorialLessonData> Lessons => _lessons;
        public TutorialLessonData CurrentLesson => GetLessonById(currentLessonId);
        public int CurrentLessonId => currentLessonId;
        public int MovesMadeInLesson => movesMadeInLesson;
        public bool IsLessonActive => isLessonActive;
        public bool IsCurrentLessonCompleted => isLessonCompleted;
        public bool IsLessonFailed => isLessonFailed;
        public bool CardWasUsedInLesson => cardWasUsedInLesson;

        // Eventos para UI y sistemas externos
        public event Action<TutorialLessonData> OnLessonStarted;
        public event Action<TutorialLessonData> OnLessonPassed;
        public event Action<TutorialLessonData> OnLessonFailed;
        public event Action<int, int> OnMovesUpdated; // movesMade, maxMoves

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
            BuildLessonsCatalog();
        }

        private void Start()
        {
            ResolveReferences();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeEvents();
            PetrosianDefenseEffect.ForceReset();
        }

        private void ResolveReferences()
        {
            if (boardView == null) boardView = FindAnyObjectByType<BoardView>();
            if (inputHandler == null && boardView != null) inputHandler = boardView.InputHandler;
            if (inputHandler == null) inputHandler = FindAnyObjectByType<BoardInputHandler>();
            if (cardManager == null) cardManager = FindAnyObjectByType<CardManager>();
        }

        #region Definición de las 6 Lecciones Exactas

        private void BuildLessonsCatalog()
        {
            _lessons.Clear();

            // =========================================================================
            // LECCIÓN 1 (BÁSICA): "Torre y Alfil: Líneas y Diagonales"
            // =========================================================================
            var l1 = new TutorialLessonData
            {
                lessonId = 1,
                title = "Torre y Alfil: Líneas y Diagonales",
                subtitle = "Dominio de columnas, filas y diagonales",
                difficulty = LessonDifficulty.Basico,
                instructionText = "La Torre domina filas y columnas en línea recta. El Alfil se desliza en diagonal respetando siempre su color de casilla. ¡Captura los dos peones negros objetivo!",
                tacticalTip = "«Las piezas colocadas en líneas abiertas multiplican su potencia geométrica y controlan el tablero.»",
                winCondition = WinConditionType.CaptureAllTargets,
                maxMoves = 2,
                requiresCard = false,
                suggestedPieceCoord = new BoardCoord(3, 0), // Torre en d1
                initialPieces = new List<InitialPiecePlacement>
                {
                    new InitialPiecePlacement(PieceType.King,   PieceColor.White, 0, 0), // a1
                    new InitialPiecePlacement(PieceType.Rook,   PieceColor.White, 3, 0), // d1
                    new InitialPiecePlacement(PieceType.Bishop, PieceColor.White, 2, 0), // c1
                    new InitialPiecePlacement(PieceType.Pawn,   PieceColor.Black, 3, 4), // d5 (objetivo Torre)
                    new InitialPiecePlacement(PieceType.Pawn,   PieceColor.Black, 5, 3)  // f4 (objetivo Alfil)
                },
                moveRuleFilter = (move) =>
                {
                    if (move.PieceMoved == null) return false;
                    return move.PieceMoved.Type == PieceType.Rook || move.PieceMoved.Type == PieceType.Bishop;
                }
            };
            _lessons.Add(l1);

            // =========================================================================
            // LECCIÓN 2 (BÁSICA): "Caballo y Peón: El Salto en L y Avance"
            // =========================================================================
            var l2 = new TutorialLessonData
            {
                lessonId = 2,
                title = "Caballo y Peón: El Salto en L y Avance",
                subtitle = "Salto sobre obstáculos y ataque diagonal",
                difficulty = LessonDifficulty.Basico,
                instructionText = "El Caballo salta en 'L' (2 casillas en una dirección y 1 perpendicular) siendo la única pieza que supera obstáculos. El Peón avanza recto (con opción de doble paso inicial) y captura en diagonal hacia adelante. ¡Supera la barrera y captura los 2 objetivos!",
                tacticalTip = "«El caballo es el mejor saltador y bloqueador. En posiciones cerradas supera ampliamente a las piezas lineales.»",
                winCondition = WinConditionType.CaptureAllTargets,
                maxMoves = 3,
                requiresCard = false,
                suggestedPieceCoord = new BoardCoord(1, 0), // Caballo en b1
                initialPieces = new List<InitialPiecePlacement>
                {
                    new InitialPiecePlacement(PieceType.King,   PieceColor.White, 7, 0), // h1
                    new InitialPiecePlacement(PieceType.Knight, PieceColor.White, 1, 0), // b1
                    new InitialPiecePlacement(PieceType.Pawn,   PieceColor.White, 4, 1), // e2
                    new InitialPiecePlacement(PieceType.Pawn,   PieceColor.White, 0, 1), // a2 (barrera)
                    new InitialPiecePlacement(PieceType.Pawn,   PieceColor.White, 1, 1), // b2 (barrera)
                    new InitialPiecePlacement(PieceType.Pawn,   PieceColor.White, 2, 1), // c2 (barrera)
                    new InitialPiecePlacement(PieceType.Pawn,   PieceColor.Black, 2, 2), // c3 (objetivo Caballo)
                    new InitialPiecePlacement(PieceType.Pawn,   PieceColor.Black, 3, 3)  // d4 (objetivo Peón tras e2->e4)
                },
                moveRuleFilter = (move) =>
                {
                    if (move.PieceMoved == null) return false;
                    return move.PieceMoved.Type == PieceType.Knight || move.PieceMoved.Type == PieceType.Pawn;
                }
            };
            _lessons.Add(l2);

            // =========================================================================
            // LECCIÓN 3 (BÁSICA): "Dama y Rey: Tu Primer Jaque"
            // =========================================================================
            var l3 = new TutorialLessonData
            {
                lessonId = 3,
                title = "Dama y Rey: Tu Primer Jaque",
                subtitle = "El ataque directo y la iniciativa de la Dama",
                difficulty = LessonDifficulty.Basico,
                instructionText = "La Dama es la pieza más poderosa: combina el poder de Torre y Alfil en cualquier dirección. Desplaza tu Dama a la casilla h5 para poner al Rey negro bajo Jaque directo a lo largo de la diagonal e8-h5.",
                tacticalTip = "«Dar jaque obliga al rival a reaccionar de inmediato, otorgándote la iniciativa del combate.»",
                winCondition = WinConditionType.DeliverCheck,
                maxMoves = 1,
                requiresCard = false,
                suggestedPieceCoord = new BoardCoord(3, 0), // Dama en d1
                targetCoord = new BoardCoord(7, 4),         // h5
                initialPieces = new List<InitialPiecePlacement>
                {
                    new InitialPiecePlacement(PieceType.King,  PieceColor.White, 4, 0), // e1
                    new InitialPiecePlacement(PieceType.Queen, PieceColor.White, 3, 0), // d1
                    new InitialPiecePlacement(PieceType.King,  PieceColor.Black, 4, 7), // e8
                    new InitialPiecePlacement(PieceType.Pawn,  PieceColor.Black, 3, 6), // d7
                    new InitialPiecePlacement(PieceType.Pawn,  PieceColor.Black, 5, 6)  // f7
                },
                moveRuleFilter = (move) =>
                {
                    return move.PieceMoved != null && move.PieceMoved.Type == PieceType.Queen;
                }
            };
            _lessons.Add(l3);

            // =========================================================================
            // LECCIÓN 4 (INTERMEDIA): "Defensa Real: Escapar del Jaque"
            // =========================================================================
            var l4 = new TutorialLessonData
            {
                lessonId = 4,
                title = "Defensa Real: Escapar del Jaque",
                subtitle = "Interposición, escape y neutralización de amenazas",
                difficulty = LessonDifficulty.Intermedio,
                instructionText = "¡Tu Rey blanco en e1 está en Jaque por la Torre negra en e8! Existen 3 formas de defenderse: 1. Mover el Rey, 2. Interponer una pieza en la línea de ataque ('Escudo defensivo'), o 3. Capturar la pieza atacante. ¡Interpón la Torre blanca en e2 o aparta al Rey para salvarlo!",
                tacticalTip = "«La profilaxis y la defensa sólida consisten en frustrar el plan del rival antes de que se desarrolle.»",
                winCondition = WinConditionType.DeliverCheck,
                maxMoves = 1,
                requiresCard = false,
                suggestedPieceCoord = new BoardCoord(0, 1), // Torre en a2
                initialPieces = new List<InitialPiecePlacement>
                {
                    new InitialPiecePlacement(PieceType.King,   PieceColor.White, 4, 0), // e1 (en jaque frontal)
                    new InitialPiecePlacement(PieceType.Rook,   PieceColor.White, 0, 1), // a2 (puede interponerse en e2)
                    new InitialPiecePlacement(PieceType.Bishop, PieceColor.White, 2, 0), // c1
                    new InitialPiecePlacement(PieceType.Rook,   PieceColor.Black, 4, 7), // e8 (atacando e1)
                    new InitialPiecePlacement(PieceType.King,   PieceColor.Black, 7, 7)  // h8
                },
                moveRuleFilter = (move) => true,
                customWinValidator = (board) => !board.IsInCheck(PieceColor.White)
            };
            _lessons.Add(l4);

            // =========================================================================
            // LECCIÓN 5 (INTERMEDIA): "Poder Arcade: Uso de Cartas Menores"
            // =========================================================================
            var l5 = new TutorialLessonData
            {
                lessonId = 5,
                title = "Poder Arcade: Uso de Cartas Menores",
                subtitle = "Mejora de movimiento y captura fuera de rango",
                difficulty = LessonDifficulty.Intermedio,
                instructionText = "Tu Caballo blanco en d4 necesita alcanzar y capturar a la Torre enemiga en d5, pero las reglas clásicas impiden un movimiento recto. ¡Haz clic en la carta 'Salto de Caballería' en tu mano para activarla y realiza el avance ortogonal para capturar al objetivo!",
                tacticalTip = "«En Chess Match, las cartas modifican las leyes del ajedrez. Úsalas para sorprender y abrir caminos imposibles.»",
                winCondition = WinConditionType.UseCardAndWin,
                maxMoves = 1,
                requiresCard = true,
                requiredCardName = "Salto de Caballería",
                cardFactory = () =>
                {
                    var card = ScriptableObject.CreateInstance<CardData>();
                    card.Initialize(
                        "Salto de Caballería",
                        "Los Caballos pueden desplazarse 1 casilla en cruz ortogonal además de su salto tradicional en L.",
                        2,
                        CardRarity.Rare,
                        new KnightBoostEffect()
                    );
                    card.CardSprite = Resources.Load<Sprite>("Pieces/white_knight");
                    return card;
                },
                suggestedPieceCoord = new BoardCoord(3, 3), // Caballo en d4
                targetCoord = new BoardCoord(3, 4),         // Torre en d5
                initialPieces = new List<InitialPiecePlacement>
                {
                    new InitialPiecePlacement(PieceType.King,   PieceColor.White, 0, 0), // a1
                    new InitialPiecePlacement(PieceType.Knight, PieceColor.White, 3, 3), // d4
                    new InitialPiecePlacement(PieceType.Rook,   PieceColor.Black, 3, 4), // d5 (objetivo a 1 paso ortogonal)
                    new InitialPiecePlacement(PieceType.King,   PieceColor.Black, 7, 7)  // h8
                },
                moveRuleFilter = (move) => true,
                customWinValidator = (board) =>
                {
                    // Victoria si la Torre en d5 fue capturada (pieza en d5 ahora es el caballo blanco)
                    Piece p = board[3, 4];
                    return p != null && p.Color == PieceColor.White && p.Type == PieceType.Knight;
                }
            };
            _lessons.Add(l5);

            // =========================================================================
            // LECCIÓN 6 (DIFÍCIL): "Gran Maestro: El Escudo de Petrosián"
            // =========================================================================
            var l6 = new TutorialLessonData
            {
                lessonId = 6,
                title = "Gran Maestro: El Escudo de Petrosián",
                subtitle = "Neutralización de jaque y jaque mate fulminante",
                difficulty = LessonDifficulty.Dificil,
                instructionText = "¡Tu Rey blanco en e1 está bajo Jaque por el Alfil negro en a5, bloqueando tus ataques! Activa la carta legendaria de Tigrán Petrosián: su Escudo de Hierro neutralizará el jaque instantáneamente, liberando a tu Dama en h5 para ejecutar el Jaque Mate decisivo en f7.",
                tacticalTip = "«Tigrán Petrosián fue apodado 'El Tigre de Hierro' por su impenetrable solidez defensiva que asfixiaba a sus rivales.»",
                winCondition = WinConditionType.UseCardAndWin,
                maxMoves = 1,
                requiresCard = true,
                requiredCardName = "Tigrán Petrosián - El Tigre de Hierro",
                cardFactory = () => PetrosianCardData.CreateRuntimeInstance(),
                suggestedPieceCoord = new BoardCoord(7, 4), // Dama en h5
                targetCoord = new BoardCoord(5, 6),         // f7
                initialPieces = new List<InitialPiecePlacement>
                {
                    new InitialPiecePlacement(PieceType.King,   PieceColor.White, 4, 0), // e1 (en jaque por Alfil a5)
                    new InitialPiecePlacement(PieceType.Queen,  PieceColor.White, 7, 4), // h5 (lista para mate en f7)
                    new InitialPiecePlacement(PieceType.Bishop, PieceColor.White, 2, 3), // c4 (protege f7)
                    new InitialPiecePlacement(PieceType.Bishop, PieceColor.Black, 0, 4), // a5 (atacando e1 en diagonal a5-e1)
                    new InitialPiecePlacement(PieceType.King,   PieceColor.Black, 5, 7), // f8 (rey negro)
                    new InitialPiecePlacement(PieceType.Pawn,   PieceColor.Black, 6, 6), // g7
                    new InitialPiecePlacement(PieceType.Pawn,   PieceColor.Black, 7, 6)  // h7
                },
                moveRuleFilter = (move) => true,
                customWinValidator = (board) =>
                {
                    // Victoria si se dio Jaque Mate al rey negro
                    return MoveValidator.IsCheckmate(board, PieceColor.Black) ||
                           (board.History != null && board.History.LastMove.HasValue && board.History.LastMove.Value.isCheckmate);
                }
            };
            _lessons.Add(l6);
        }

        #endregion

        #region Control y Ejecución de Lecciones

        public TutorialLessonData GetLessonById(int id)
        {
            return _lessons.Find(l => l.lessonId == id);
        }

        public void StartEducationMode(int lessonId = 1)
        {
            ResolveReferences();
            LoadLesson(lessonId);
        }

        public void LoadLesson(int lessonId)
        {
            ResolveReferences();
            TutorialLessonData lesson = GetLessonById(lessonId);
            if (lesson == null)
            {
                Debug.LogWarning($"[TutorialController] No se encontró la lección con ID {lessonId}.");
                return;
            }

            currentLessonId = lessonId;
            movesMadeInLesson = 0;
            isLessonCompleted = false;
            isLessonFailed = false;
            isLessonActive = true;
            cardWasUsedInLesson = false;

            // Resetear escudo de Petrosián
            PetrosianDefenseEffect.ForceReset();

            if (GameManager.Instance != null)
            {
                GameManager.SelectedGameMode = GameMode.Education;
                GameManager.Instance.SetState(GameState.Playing);
            }

            // Desactivar IA para puzzles
            if (inputHandler != null)
            {
                inputHandler.EnableAI = false;
                inputHandler.SetMoveFilter(lesson.moveRuleFilter);
            }

            // 1. Configurar tablero con piezas de la lección
            BoardState lessonBoard = new BoardState();
            lesson.ApplyToBoard(lessonBoard);

            if (boardView != null)
            {
                boardView.LoadCustomPosition(lessonBoard);
            }

            // 2. Configurar mano de cartas según requiera la lección
            SetupCardsForLesson(lesson);

            // 3. Suscribir a eventos de jugadas
            SubscribeEvents();

            Debug.Log($"<color=cyan><b>[TutorialController] Iniciando Lección {lesson.lessonId}: {lesson.title}</b></color>");
            OnLessonStarted?.Invoke(lesson);
            OnMovesUpdated?.Invoke(movesMadeInLesson, lesson.maxMoves);
        }

        private void SetupCardsForLesson(TutorialLessonData lesson)
        {
            if (cardManager == null) cardManager = FindAnyObjectByType<CardManager>();
            if (cardManager == null) return;

            cardManager.ClearTurnEffects();
            cardManager.HandView?.ClearHand();

            if (lesson.requiresCard && lesson.cardFactory != null)
            {
                CardData card = lesson.cardFactory.Invoke();
                if (card != null && cardManager.HandView != null)
                {
                    cardManager.HandView.AddCardToHand(card, cardManager);
                    Debug.Log($"<color=lime>[TutorialController] Carta otorgada para la lección: <b>{card.CardName}</b></color>");
                }
            }
        }

        private void SubscribeEvents()
        {
            UnsubscribeEvents();

            if (boardView != null)
            {
                boardView.OnMoveExecuted += HandleMoveExecuted;
            }

            if (cardManager != null)
            {
                cardManager.OnCardActivated += HandleCardActivated;
            }
        }

        private void UnsubscribeEvents()
        {
            if (boardView != null)
            {
                boardView.OnMoveExecuted -= HandleMoveExecuted;
            }

            if (cardManager != null)
            {
                cardManager.OnCardActivated -= HandleCardActivated;
            }
        }

        private void HandleCardActivated(CardView cardView)
        {
            cardWasUsedInLesson = true;
            Debug.Log($"<color=yellow>[TutorialController] Carta utilizada en la lección: {cardView.CardData?.CardName}</color>");
        }

        private void HandleMoveExecuted(Move move)
        {
            if (!isLessonActive || isLessonCompleted || isLessonFailed) return;

            movesMadeInLesson++;
            TutorialLessonData lesson = CurrentLesson;
            if (lesson == null || boardView == null || boardView.BoardState == null) return;

            BoardState board = boardView.BoardState;
            OnMovesUpdated?.Invoke(movesMadeInLesson, lesson.maxMoves);

            // Evaluar condición de victoria
            bool won = CheckWinCondition(lesson, board);

            if (won)
            {
                CompleteLesson(lesson);
                return;
            }

            // Comprobar si se agotaron los movimientos permitidos
            if (movesMadeInLesson >= lesson.maxMoves)
            {
                FailLesson(lesson);
                return;
            }

            // Si aún quedan jugadas, mantener turno en Blancas para el estudiante
            board.CurrentTurn = PieceColor.White;
        }

        private bool CheckWinCondition(TutorialLessonData lesson, BoardState board)
        {
            if (board == null) return false;

            // 1. Validador personalizado si existe
            if (lesson.customWinValidator != null)
            {
                if (lesson.requiresCard && !cardWasUsedInLesson && lesson.winCondition == WinConditionType.UseCardAndWin)
                {
                    // Si requiere carta y no se ha usado, no cumple UseCardAndWin
                    return false;
                }
                return lesson.customWinValidator.Invoke(board);
            }

            // 2. Validadores según WinConditionType
            switch (lesson.winCondition)
            {
                case WinConditionType.CaptureAllTargets:
                    int targets = CountTargetPieces(board);
                    return targets <= 0;

                case WinConditionType.DeliverCheck:
                    return board.IsInCheck(PieceColor.Black) || !board.IsInCheck(PieceColor.White);

                case WinConditionType.DeliverCheckmate:
                    return MoveValidator.IsCheckmate(board, PieceColor.Black);

                case WinConditionType.UseCardAndWin:
                    if (!cardWasUsedInLesson) return false;
                    return CountTargetPieces(board) <= 0 || MoveValidator.IsCheckmate(board, PieceColor.Black);

                default:
                    return false;
            }
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

        private void CompleteLesson(TutorialLessonData lesson)
        {
            isLessonCompleted = true;
            isLessonFailed = false;

            if (inputHandler != null)
            {
                inputHandler.ClearMoveFilter();
            }

            // Guardar progreso en PlayerPrefs
            SaveLessonProgress(lesson.lessonId);

            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.TriggerHapticFeedback();
            }

            Debug.Log($"<color=green><b>[TutorialController] ¡Lección {lesson.lessonId} Superada!</b></color>");
            OnLessonPassed?.Invoke(lesson);
        }

        private void FailLesson(TutorialLessonData lesson)
        {
            isLessonFailed = true;
            isLessonCompleted = false;

            if (inputHandler != null)
            {
                inputHandler.ClearMoveFilter();
            }

            Debug.Log($"<color=orange><b>[TutorialController] Lección {lesson.lessonId} Fallida: Movimientos agotados.</b></color>");
            OnLessonFailed?.Invoke(lesson);
        }

        public void RestartCurrentLesson()
        {
            LoadLesson(currentLessonId);
        }

        public void AdvanceToNextLesson()
        {
            if (currentLessonId < _lessons.Count)
            {
                LoadLesson(currentLessonId + 1);
            }
            else
            {
                Debug.Log("<color=gold><b>[TutorialController] ¡Todas las 6 lecciones completadas con éxito!</b></color>");
            }
        }

        public void PreviousLesson()
        {
            if (currentLessonId > 1)
            {
                LoadLesson(currentLessonId - 1);
            }
        }

        public void ExitTutorial()
        {
            isLessonActive = false;
            UnsubscribeEvents();

            if (inputHandler != null)
            {
                inputHandler.ClearMoveFilter();
                inputHandler.EnableAI = true;
            }

            PetrosianDefenseEffect.ForceReset();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToMenu();
            }
        }

        #endregion

        #region Persistencia de Progreso (PlayerPrefs)

        public bool IsLessonCompleted(int lessonId)
        {
            return PlayerPrefs.GetInt(PREF_KEY_COMPLETED + lessonId, 0) == 1;
        }

        public bool IsLessonUnlocked(int lessonId)
        {
            if (lessonId == 1) return true; // Lección 1 siempre desbloqueada
            int maxUnlocked = PlayerPrefs.GetInt(PREF_KEY_MAX_UNLOCKED, 1);
            return lessonId <= maxUnlocked || IsLessonCompleted(lessonId - 1);
        }

        private void SaveLessonProgress(int completedLessonId)
        {
            PlayerPrefs.SetInt(PREF_KEY_COMPLETED + completedLessonId, 1);
            int currentMax = PlayerPrefs.GetInt(PREF_KEY_MAX_UNLOCKED, 1);
            if (completedLessonId + 1 > currentMax)
            {
                PlayerPrefs.SetInt(PREF_KEY_MAX_UNLOCKED, completedLessonId + 1);
            }
            PlayerPrefs.Save();
        }

        #endregion
    }
}

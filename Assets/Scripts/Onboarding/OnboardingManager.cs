using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessBalatro
{
    /// <summary>
    /// Pasos principales del Tutorial Guiado de Primera Vez (Interactive Onboarding System).
    /// </summary>
    public enum OnboardingStep
    {
        None = 0,
        MenuOverview = 1,   // Paso 1: Perfil del jugador y botón JUGAR (qué es Chess Match)
        MatchSetup = 2,     // Paso 2: Selector de Gran Maestro (Tigrán Petrosián) y opciones de partida
        BoardMovement = 3,  // Paso 3: Tablero guiado — mover pieza aliada a casilla válida
        CaptureEconomy = 4, // Paso 4: Capturar pieza enemiga vulnerable y revisar PUNTOS DE CAPTURA
        DraftCards = 5,     // Paso 5: Modal de Draft — Mejoras Menores (4 pts) vs Cartas de Jugador (10 pts)
        ActivateCard = 6    // Paso 6: Activar carta equipada abajo en HUD y mensaje final
    }

    /// <summary>
    /// Subfases granulares dentro de los 6 pasos para alternar entre explicaciones ("Siguiente ►")
    /// y acciones interactivas obligatorias sobre el botón, casilla o carta señalada.
    /// </summary>
    public enum OnboardingSubPhase
    {
        None = 0,

        // Paso 1 (Recorrido completo del Menú y sus modos)
        MenuProfileIntro = 10,
        MenuPlayButton = 11,
        MenuArcadeIntro = 12,
        MenuEducationIntro = 13,
        MenuSkinsOptionsIntro = 14,

        // Paso 2
        SetupGrandMaster = 20,
        SetupStartMatch = 21,

        // Paso 3
        BoardSelectPiece = 30,
        BoardMovePiece = 31,

        // Paso 4
        CaptureEnemyPiece = 40,
        ExplainCaptureHud = 41,

        // Paso 5
        SelectDraftCard = 50,

        // Paso 6
        ActivateBottomCard = 60,
        CompletionMessage = 61
    }

    /// <summary>
    /// Descriptor de contenido visual y reglas de bloqueo para la capa <see cref="OnboardingOverlayUI"/>.
    /// </summary>
    public struct OnboardingStepPresentation
    {
        public OnboardingStep Step;
        public OnboardingSubPhase SubPhase;
        public string BadgeText;
        public string Title;
        public string Body;
        public bool IsInteractive;
        public string ContinueButtonLabel;
        public string ActionHintText;
    }

    /// <summary>
    /// Lógica pura C# (testeable fuera de Unity) para las coordenadas guiadas, reglas de filtro
    /// y transiciones deterministas de los 6 pasos del onboarding.
    /// </summary>
    public static class OnboardingLogic
    {
        public const string CompletedPrefKey = "HasCompletedOnboarding";
        public const int TotalSteps = 6;

        // Posición guiada determinista:
        // Rey blanco e1, Peón blanco a2 (para garantizar Peón Acelerado+ activable),
        // Caballo blanco b1 -> c3 (Paso 3) -> captura Torre negra en d5 (+5 PTS, dispara Minor Draft >= 4) (Paso 4),
        // Rey negro h8 + Peón negro g7 para mantener tablero legal y libre de jaques/ahogados accidentales.
        public static readonly BoardCoord GuidedWhiteKing = new BoardCoord(4, 0);   // e1
        public static readonly BoardCoord GuidedWhitePawn = new BoardCoord(0, 1);   // a2
        public static readonly BoardCoord GuidedKnightStart = new BoardCoord(1, 0); // b1
        public static readonly BoardCoord GuidedKnightMid = new BoardCoord(2, 2);   // c3
        public static readonly BoardCoord GuidedRookTarget = new BoardCoord(3, 4);  // d5 (5 pts)
        public static readonly BoardCoord GuidedBlackKing = new BoardCoord(7, 7);   // h8
        public static readonly BoardCoord GuidedBlackPawn = new BoardCoord(6, 6);   // g7

        public static int GetStepNumber(OnboardingStep step)
        {
            return (int)step;
        }

        public static OnboardingStep GetStepForSubPhase(OnboardingSubPhase subPhase)
        {
            return subPhase switch
            {
                OnboardingSubPhase.MenuProfileIntro
                or OnboardingSubPhase.MenuArcadeIntro
                or OnboardingSubPhase.MenuEducationIntro
                or OnboardingSubPhase.MenuSkinsOptionsIntro
                or OnboardingSubPhase.MenuPlayButton => OnboardingStep.MenuOverview,
                OnboardingSubPhase.SetupGrandMaster or OnboardingSubPhase.SetupStartMatch => OnboardingStep.MatchSetup,
                OnboardingSubPhase.BoardSelectPiece or OnboardingSubPhase.BoardMovePiece => OnboardingStep.BoardMovement,
                OnboardingSubPhase.CaptureEnemyPiece or OnboardingSubPhase.ExplainCaptureHud => OnboardingStep.CaptureEconomy,
                OnboardingSubPhase.SelectDraftCard => OnboardingStep.DraftCards,
                OnboardingSubPhase.ActivateBottomCard or OnboardingSubPhase.CompletionMessage => OnboardingStep.ActivateCard,
                _ => OnboardingStep.None
            };
        }

        public static OnboardingSubPhase GetNextOnContinue(OnboardingSubPhase current)
        {
            return current switch
            {
                OnboardingSubPhase.MenuProfileIntro => OnboardingSubPhase.MenuArcadeIntro,
                OnboardingSubPhase.MenuArcadeIntro => OnboardingSubPhase.MenuEducationIntro,
                OnboardingSubPhase.MenuEducationIntro => OnboardingSubPhase.MenuSkinsOptionsIntro,
                OnboardingSubPhase.MenuSkinsOptionsIntro => OnboardingSubPhase.MenuPlayButton,
                OnboardingSubPhase.SetupGrandMaster => OnboardingSubPhase.SetupStartMatch,
                OnboardingSubPhase.ExplainCaptureHud => OnboardingSubPhase.SelectDraftCard,
                OnboardingSubPhase.CompletionMessage => OnboardingSubPhase.None,
                _ => current
            };
        }

        public static BoardState CreateGuidedBoardState()
        {
            BoardState state = new BoardState();
            state.Clear();
            state.WhiteCanCastleKingside = false;
            state.WhiteCanCastleQueenside = false;
            state.BlackCanCastleKingside = false;
            state.BlackCanCastleQueenside = false;
            state.CurrentTurn = PieceColor.White;
            state.FullmoveNumber = 1;

            state[GuidedWhiteKing] = new Piece(PieceType.King, PieceColor.White);
            state[GuidedWhitePawn] = new Piece(PieceType.Pawn, PieceColor.White);
            state[GuidedKnightStart] = new Piece(PieceType.Knight, PieceColor.White);

            state[GuidedRookTarget] = new Piece(PieceType.Rook, PieceColor.Black);
            state[GuidedBlackPawn] = new Piece(PieceType.Pawn, PieceColor.Black);
            state[GuidedBlackKing] = new Piece(PieceType.King, PieceColor.Black);

            return state;
        }

        public static bool IsGuidedMoveAllowed(OnboardingSubPhase subPhase, Move move)
        {
            if (move == null) return false;

            switch (subPhase)
            {
                case OnboardingSubPhase.BoardSelectPiece:
                case OnboardingSubPhase.BoardMovePiece:
                    return move.From == GuidedKnightStart && move.To == GuidedKnightMid;

                case OnboardingSubPhase.CaptureEnemyPiece:
                    return move.From == GuidedKnightMid && move.To == GuidedRookTarget;

                default:
                    return false;
            }
        }

        public static bool IsBoardSquareClickAllowed(OnboardingSubPhase subPhase, BoardCoord coord)
        {
            switch (subPhase)
            {
                case OnboardingSubPhase.BoardSelectPiece:
                    return coord == GuidedKnightStart;

                case OnboardingSubPhase.BoardMovePiece:
                    return coord == GuidedKnightMid || coord == GuidedKnightStart;

                case OnboardingSubPhase.CaptureEnemyPiece:
                    return coord == GuidedRookTarget || coord == GuidedKnightMid;

                default:
                    return false;
            }
        }

        public static OnboardingStepPresentation BuildPresentation(OnboardingSubPhase subPhase, string selectedCardName = null)
        {
            switch (subPhase)
            {
                case OnboardingSubPhase.MenuProfileIntro:
                    return new OnboardingStepPresentation
                    {
                        Step = OnboardingStep.MenuOverview,
                        SubPhase = subPhase,
                        BadgeText = "PASO 1 / 6 · BIENVENIDO",
                        Title = "Maestro Guía · ¿Qué es Chess Match?",
                        Body = "¡Bienvenido a Chess Match! Combina ajedrez táctico con cartas de poder, un Modo Arcade Survivors y una Academia de 6 lecciones. Arriba a la derecha ves tu Perfil, Rango y ELO.",
                        IsInteractive = false,
                        ContinueButtonLabel = "Siguiente ►"
                    };

                case OnboardingSubPhase.MenuArcadeIntro:
                    return new OnboardingStepPresentation
                    {
                        Step = OnboardingStep.MenuOverview,
                        SubPhase = subPhase,
                        BadgeText = "PASO 1 / 6 · MODO ARCADE",
                        Title = "Modo Arcade (Chess Survivors)",
                        Body = "¡Asedio de 15 minutos sin barras de HP! Toda captura es de 1 movimiento: mueve al Rey Blanco, protege tus 3 Escudos Reales, invoca aliados físicos, activa sinergias, vive expansiones 8×8→8×20 cada 3:30 y vence al Coloso Real.",
                        IsInteractive = false,
                        ContinueButtonLabel = "Siguiente ►"
                    };

                case OnboardingSubPhase.MenuEducationIntro:
                    return new OnboardingStepPresentation
                    {
                        Step = OnboardingStep.MenuOverview,
                        SubPhase = subPhase,
                        BadgeText = "PASO 1 / 6 · ACADEMIA",
                        Title = "Modo Educativo (6 Lecciones)",
                        Body = "¿Quieres dominar piezas o estrategias desde cero? La Academia incluye 6 lecciones progresivas: Torre, Alfil, Caballo, Peón, Dama, Defensa de Jaque, Cartas Menores y Tigrán Petrosián.",
                        IsInteractive = false,
                        ContinueButtonLabel = "Siguiente ►"
                    };

                case OnboardingSubPhase.MenuSkinsOptionsIntro:
                    return new OnboardingStepPresentation
                    {
                        Step = OnboardingStep.MenuOverview,
                        SubPhase = subPhase,
                        BadgeText = "PASO 1 / 6 · PERSONALIZACIÓN",
                        Title = "Aspectos, Opciones y Centro de Tutoriales",
                        Body = "Personaliza las piezas en ASPECTOS, ajusta Música/SFX y hápticos en OPCIONES, o abre en cualquier momento TUTORIAL DEL JUEGO para consultar las guías de todo el juego.",
                        IsInteractive = false,
                        ContinueButtonLabel = "Siguiente ►"
                    };

                case OnboardingSubPhase.MenuPlayButton:
                    return new OnboardingStepPresentation
                    {
                        Step = OnboardingStep.MenuOverview,
                        SubPhase = subPhase,
                        BadgeText = "PASO 1 / 6 · MODO CLÁSICO",
                        Title = "Pulsa el Botón JUGAR",
                        Body = "Ahora toca el botón dorado 'JUGAR' para elegir tu Gran Maestro y preparar tu primera batalla táctica guiada en el tablero.",
                        IsInteractive = true,
                        ActionHintText = "Toca 'JUGAR' para continuar"
                    };

                case OnboardingSubPhase.SetupGrandMaster:
                    return new OnboardingStepPresentation
                    {
                        Step = OnboardingStep.MatchSetup,
                        SubPhase = subPhase,
                        BadgeText = "PASO 2 / 6 · GRANDES MAESTROS",
                        Title = "Elige entre 4 Leyendas y Ajustes",
                        Body = "Escoge tu Gran Maestro: Petrosián (anula Jaques), Tal (sacrifica para 2 turnos), Polgár (salta 1 pieza) o Kaspárov (presión e intimidación), además de bando, reloj e IA.",
                        IsInteractive = false,
                        ContinueButtonLabel = "Siguiente ►"
                    };

                case OnboardingSubPhase.SetupStartMatch:
                    return new OnboardingStepPresentation
                    {
                        Step = OnboardingStep.MatchSetup,
                        SubPhase = subPhase,
                        BadgeText = "PASO 2 / 6 · IR AL TABLERO",
                        Title = "Inicia la Partida Guiada",
                        Body = "Ahora pulsa '¡COMENZAR PARTIDA!' para saltar al tablero y practicar tu primera jugada táctica.",
                        IsInteractive = true,
                        ActionHintText = "Toca '¡COMENZAR PARTIDA!'"
                    };

                case OnboardingSubPhase.BoardSelectPiece:
                    return new OnboardingStepPresentation
                    {
                        Step = OnboardingStep.BoardMovement,
                        SubPhase = subPhase,
                        BadgeText = "PASO 3 / 6 · MOVER PIEZAS",
                        Title = "Selecciona tu Caballo (b1)",
                        Body = "Cada pieza conserva sus reglas clásicas del ajedrez. Toca tu Caballo blanco en b1 para iluminar sus saltos en 'L'.",
                        IsInteractive = true,
                        ActionHintText = "Toca el Caballo en b1"
                    };

                case OnboardingSubPhase.BoardMovePiece:
                    return new OnboardingStepPresentation
                    {
                        Step = OnboardingStep.BoardMovement,
                        SubPhase = subPhase,
                        BadgeText = "PASO 3 / 6 · CASILLA VÁLIDA",
                        Title = "Desplázate a c3",
                        Body = "¡Excelente! Los puntos verdes señalan las casillas legales disponibles. Toca c3 para completar el movimiento.",
                        IsInteractive = true,
                        ActionHintText = "Toca la casilla marcada c3"
                    };

                case OnboardingSubPhase.CaptureEnemyPiece:
                    return new OnboardingStepPresentation
                    {
                        Step = OnboardingStep.CaptureEconomy,
                        SubPhase = subPhase,
                        BadgeText = "PASO 4 / 6 · CAPTURAR PIEZA",
                        Title = "Captura la Torre Negra (d5)",
                        Body = "¡Tu Caballo amenaza la Torre negra en d5! Cada captura suma puntos para desbloquear cartas. Toca d5 para capturarla.",
                        IsInteractive = true,
                        ActionHintText = "Captura la Torre en d5"
                    };

                case OnboardingSubPhase.ExplainCaptureHud:
                    return new OnboardingStepPresentation
                    {
                        Step = OnboardingStep.CaptureEconomy,
                        SubPhase = subPhase,
                        BadgeText = "PASO 4 / 6 · ECONOMÍA",
                        Title = "Puntos de Captura (+5 PTS)",
                        Body = "¡Bien hecho! Arriba ves tu barra de PUNTOS DE CAPTURA. Valores: Peón=1, Caballo/Alfil=3, Torre=5 y Dama=9. Con 4 pts obtienes una Mejora Menor y con 10 pts (o comiendo la Dama) ¡una Carta de Gran Maestro!",
                        IsInteractive = false,
                        ContinueButtonLabel = "Siguiente ►"
                    };

                case OnboardingSubPhase.SelectDraftCard:
                    return new OnboardingStepPresentation
                    {
                        Step = OnboardingStep.DraftCards,
                        SubPhase = subPhase,
                        BadgeText = "PASO 5 / 6 · DRAFT DE CARTAS",
                        Title = "Elige tu Recompensa",
                        Body = "¡Superaste los 4 puntos! Aquí puedes elegir Mejoras Menores (4 pts) para potenciar tus piezas o Cartas de Gran Maestro (10 pts) con poderes legendarios. Escoge una carta para añadirla a tu mano.",
                        IsInteractive = true,
                        ActionHintText = "Toca una carta para elegirla"
                    };

                case OnboardingSubPhase.ActivateBottomCard:
                    string cardLabel = string.IsNullOrEmpty(selectedCardName) ? "tu carta" : $"'{selectedCardName}'";
                    return new OnboardingStepPresentation
                    {
                        Step = OnboardingStep.ActivateCard,
                        SubPhase = subPhase,
                        BadgeText = "PASO 6 / 6 · USAR CARTA",
                        Title = "Activa tu Carta Equipada",
                        Body = $"Tu carta {cardLabel} ya está equipada en la parte inferior. Pulsa sobre ella antes de mover para activar su poder especial sobre el tablero.",
                        IsInteractive = true,
                        ActionHintText = "Pulsa la carta inferior para activarla"
                    };

                case OnboardingSubPhase.CompletionMessage:
                    return new OnboardingStepPresentation
                    {
                        Step = OnboardingStep.ActivateCard,
                        SubPhase = subPhase,
                        BadgeText = "¡TUTORIAL COMPLETADO! (6 / 6)",
                        Title = "¡Estás listo para competir!",
                        Body = "¡Estás listo para competir! Recuerda: captura piezas para ganar puntos, desbloquea cartas y revisa 'TUTORIAL DEL JUEGO' cuando quieras guías del Modo Arcade, Academia y Maestros.",
                        IsInteractive = false,
                        ContinueButtonLabel = "¡A JUGAR! ►"
                    };

                default:
                    return default;
            }
        }
    }

    /// <summary>
    /// Singleton persistente encargado del "Tutorial Guiado de Primera Vez" (Interactive Onboarding).
    /// Verifica <see cref="OnboardingLogic.CompletedPrefKey"/> al iniciar, orquesta los 6 pasos entre escenas,
    /// permite repetirlo desde el Menú y ofrece el botón "Saltar Tutorial ⏭" en cualquier momento.
    /// </summary>
    public class OnboardingManager : MonoBehaviour
    {
        public const string CompletedPrefKey = OnboardingLogic.CompletedPrefKey;

        private static OnboardingManager _instance;
        private static bool _isQuitting = false;

        public static OnboardingManager Instance => _instance;

        public static bool HasCompletedOnboarding => PlayerPrefs.GetInt(CompletedPrefKey, 0) == 1;
        public static bool IsOnboardingActive => _instance != null && _instance._isOnboardingActive;
        public static bool IsOnboardingGameplayActive =>
            IsOnboardingActive && (int)_instance._currentStep >= (int)OnboardingStep.BoardMovement;

        public static bool ShouldDeferDraftModal =>
            IsOnboardingActive && _instance._subPhase == OnboardingSubPhase.CaptureEnemyPiece;

        public static bool ShouldBlockBoardInteraction =>
            TutorialHubUI.IsHubOpen ||
            (IsOnboardingActive &&
             _instance._subPhase != OnboardingSubPhase.BoardSelectPiece &&
             _instance._subPhase != OnboardingSubPhase.BoardMovePiece &&
             _instance._subPhase != OnboardingSubPhase.CaptureEnemyPiece);

        public OnboardingStep CurrentStep => _currentStep;
        public OnboardingSubPhase CurrentSubPhase => _subPhase;

        private bool _isOnboardingActive = false;
        private OnboardingStep _currentStep = OnboardingStep.None;
        private OnboardingSubPhase _subPhase = OnboardingSubPhase.None;
        private bool _hasCheckedFirstLaunch = false;
        private bool _pendingStartOnMenuLoad = false;
        private bool _pendingGameplayInitOnSceneLoad = false;
        private bool _hasSavedPreviousHandAlignment = false;
        private HandAlignment _previousHandAlignment = HandAlignment.RightSide;
        private float _previousHandBottomOffset = 25f;
        private CardData _selectedOnboardingCard;
        private string _selectedCardName = string.Empty;

        private BoardView _boardView;
        private BoardInputHandler _inputHandler;
        private CardManager _cardManager;
        private CaptureRewardManager _rewardManager;
        private OnboardingOverlayUI _overlayUI;

        public event Action<OnboardingStep, OnboardingSubPhase> OnStepChanged;
        public event Action OnOnboardingCompletedOrSkipped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeOnLaunch()
        {
            if (!_isQuitting)
            {
                EnsureInstance();
            }
        }

        public static OnboardingManager EnsureInstance()
        {
            if (_isQuitting || !Application.isPlaying) return _instance;

            if (_instance == null)
            {
                OnboardingManager existing = FindAnyObjectByType<OnboardingManager>(FindObjectsInactive.Include);
                if (existing != null)
                {
                    _instance = existing;
                    if (!existing.gameObject.activeSelf) existing.gameObject.SetActive(true);
                }
                else
                {
                    GameObject go = new GameObject("OnboardingManager");
                    _instance = go.AddComponent<OnboardingManager>();
                }
            }

            _instance.MakePersistent();
            _instance.EnsureOverlayUI();
            return _instance;
        }

        private void MakePersistent()
        {
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);
        }

        private OnboardingOverlayUI EnsureOverlayUI()
        {
            if (_overlayUI == null)
            {
                _overlayUI = GetComponent<OnboardingOverlayUI>() ?? FindAnyObjectByType<OnboardingOverlayUI>(FindObjectsInactive.Include);
                if (_overlayUI == null)
                {
                    _overlayUI = gameObject.AddComponent<OnboardingOverlayUI>();
                }
            }
            return _overlayUI;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            MakePersistent();
            EnsureOverlayUI();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            if (_hasCheckedFirstLaunch) return;
            _hasCheckedFirstLaunch = true;

            if (PlayerPrefs.GetInt(CompletedPrefKey, 0) == 0 && !_isOnboardingActive)
            {
                // No interrumpir si se entró directamente al Modo Educativo o Modo Arcade
                if (GameManager.SelectedGameMode == GameMode.Education ||
                    GameManager.SelectedGameMode == GameMode.Arcade ||
                    TutorialController.IsEducationalModeActive)
                {
                    return;
                }

                StartOnboarding();
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeGameplayEvents();
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            UnsubscribeGameplayEvents();
        }

        /// <summary>
        /// Abre el Centro Completo de Tutoriales ("Todos los Tutoriales del Juego"),
        /// donde el jugador puede explorar las guías de Menú, Clásico Roguelike,
        /// Modo Arcade (Survivors), Academia Educativa, Grandes Maestros, Cartas, Aspectos y Opciones.
        /// Llamado al pulsar el botón "🎓 TUTORIAL DEL JUEGO".
        /// </summary>
        public void OpenTutorialHub(TutorialHubCategory initialCategory = TutorialHubCategory.Overview)
        {
            CancelActiveOnboardingWithoutStartingMatch();
            TutorialHubUI.EnsureInstance().Open(initialCategory);
        }

        /// <summary>
        /// Cancela un onboarding activo sin forzar el inicio de una partida clásica.
        /// </summary>
        public void CancelActiveOnboardingWithoutStartingMatch()
        {
            if (!_isOnboardingActive) return;

            CleanupOnboardingRuntimeState();
            _overlayUI?.HideImmediate();
        }

        /// <summary>
        /// Inicia (o reinicia desde el Paso 1) el flujo del Tutorial Guiado de Primera Vez.
        /// Recorre el Menú Principal completo (Perfil, Arcade, Academia, Aspectos/Opciones, JUGAR),
        /// la selección de Gran Maestro y la partida táctica guiada sobre el tablero.
        /// </summary>
        public void StartOnboarding()
        {
            if (TutorialHubUI.Instance != null && TutorialHubUI.Instance.IsOpen)
            {
                TutorialHubUI.Instance.Close();
            }

            EnsureOverlayUI();
            UnsubscribeGameplayEvents();

            _selectedOnboardingCard = null;
            _selectedCardName = string.Empty;
            _hasSavedPreviousHandAlignment = false;
            _pendingGameplayInitOnSceneLoad = false;

            if (TutorialController.Instance != null)
            {
                TutorialController.Instance.DeactivateForStandardMatch();
            }
            GameManager.SelectedGameMode = GameMode.ClassicRoguelike;

            bool isMenuScene = MainMenuManager.Instance != null ||
                               SceneManager.GetActiveScene().name == "MainMenuScene";

            if (isMenuScene && MainMenuManager.Instance != null)
            {
                _pendingStartOnMenuLoad = false;
                BeginAtMenuStep1();
                return;
            }

            if (Application.CanStreamedLevelBeLoaded("MainMenuScene"))
            {
                _isOnboardingActive = true;
                _pendingStartOnMenuLoad = true;
                SetPhase(OnboardingStep.MenuOverview, OnboardingSubPhase.MenuProfileIntro);
                SceneManager.LoadSceneAsync("MainMenuScene");
                return;
            }

            // Fallback seguro si se prueba en una escena aislada sin MainMenuScene en BuildSettings
            _isOnboardingActive = true;
            BeginGuidedGameplayDirect();
        }

        private void BeginAtMenuStep1()
        {
            _isOnboardingActive = true;
            if (MainMenuManager.Instance != null)
            {
                MainMenuManager.Instance.SwitchPanel(MainMenuManager.MenuPanel.Main);
            }
            SetPhase(OnboardingStep.MenuOverview, OnboardingSubPhase.MenuProfileIntro);
            Debug.Log("<color=#FFD700><b>[OnboardingManager]</b> Tutorial Guiado iniciado en Menú Principal (Paso 1/6).</color>");
        }

        private void BeginGuidedGameplayDirect()
        {
            _isOnboardingActive = true;
            SetPhase(OnboardingStep.BoardMovement, OnboardingSubPhase.BoardSelectPiece);
            StartCoroutine(SetupGuidedBoardCoroutine());
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_pendingStartOnMenuLoad && (scene.name == "MainMenuScene" || MainMenuManager.Instance != null))
            {
                _pendingStartOnMenuLoad = false;
                StartCoroutine(DelayedBeginMenuRoutine());
                return;
            }

            if (_isOnboardingActive && (_pendingGameplayInitOnSceneLoad || scene.name == "SampleScene"))
            {
                _pendingGameplayInitOnSceneLoad = false;
                InitializeGuidedGameplayScene();
            }
        }

        private IEnumerator DelayedBeginMenuRoutine()
        {
            yield return null;
            BeginAtMenuStep1();
        }

        /// <summary>
        /// Llamado cuando el jugador avanza manualmente con "Siguiente ►" o "¡A JUGAR! ►".
        /// </summary>
        public void AdvanceInformativeStep()
        {
            if (!_isOnboardingActive) return;

            RetroAudioManager.Instance?.PlayTap();

            if (_subPhase == OnboardingSubPhase.CompletionMessage)
            {
                CompleteOnboardingAndStartMatch();
                return;
            }

            OnboardingSubPhase next = OnboardingLogic.GetNextOnContinue(_subPhase);
            if (next == _subPhase || next == OnboardingSubPhase.None) return;

            OnboardingStep nextStep = OnboardingLogic.GetStepForSubPhase(next);
            SetPhase(nextStep, next);

            if (next == OnboardingSubPhase.SelectDraftCard)
            {
                TriggerOnboardingDraftModal();
            }
        }

        /// <summary>
        /// Omite inmediatamente el tutorial guiado y persiste HasCompletedOnboarding = 1.
        /// </summary>
        public void SkipOnboarding()
        {
            if (!_isOnboardingActive) return;

            RetroAudioManager.Instance?.PlayTap();
            MarkOnboardingCompleted();
            CleanupOnboardingRuntimeState();

            _overlayUI?.HideImmediate();
            OnOnboardingCompletedOrSkipped?.Invoke();

            Debug.Log("<color=yellow><b>[OnboardingManager]</b> Tutorial omitido por el jugador. HasCompletedOnboarding = 1.</color>");

            if (SceneManager.GetActiveScene().name == "SampleScene" || MainMenuManager.Instance == null)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.SelectedGameMode = GameMode.ClassicRoguelike;
                    GameManager.Instance.StartNewGame();
                }
            }
        }

        private void CompleteOnboardingAndStartMatch()
        {
            MarkOnboardingCompleted();
            CleanupOnboardingRuntimeState();

            _overlayUI?.HideImmediate();
            OnOnboardingCompletedOrSkipped?.Invoke();

            Debug.Log("<color=lime><b>[OnboardingManager]</b> Tutorial Guiado completado con éxito. HasCompletedOnboarding = 1.</color>");

            if (GameManager.Instance != null)
            {
                GameManager.SelectedGameMode = GameMode.ClassicRoguelike;
                GameManager.Instance.StartNewGame();
            }
        }

        public static void MarkOnboardingCompleted()
        {
            PlayerPrefs.SetInt(CompletedPrefKey, 1);
            PlayerPrefs.Save();
        }

        public static void ResetOnboardingProgress()
        {
            PlayerPrefs.SetInt(CompletedPrefKey, 0);
            PlayerPrefs.Save();
        }

        private void CleanupOnboardingRuntimeState()
        {
            _isOnboardingActive = false;
            _pendingStartOnMenuLoad = false;
            _pendingGameplayInitOnSceneLoad = false;
            _currentStep = OnboardingStep.None;
            _subPhase = OnboardingSubPhase.None;

            UnsubscribeGameplayEvents();

            if (_inputHandler != null)
            {
                _inputHandler.ClearMoveFilter();
                _inputHandler.IsRewardDraftBlocking = false;
                _inputHandler.EnableAI = true;
            }

            if (_cardManager != null && _cardManager.HandView != null && _hasSavedPreviousHandAlignment)
            {
                _cardManager.HandView.Alignment = _previousHandAlignment;
                _cardManager.HandView.SetBottomOffset(_previousHandBottomOffset);
                _hasSavedPreviousHandAlignment = false;
            }
        }

        private void SetPhase(OnboardingStep step, OnboardingSubPhase subPhase)
        {
            _currentStep = step;
            _subPhase = subPhase;

            OnboardingStepPresentation presentation = OnboardingLogic.BuildPresentation(subPhase, _selectedCardName);
            EnsureOverlayUI().PresentStep(presentation);

            OnStepChanged?.Invoke(step, subPhase);
        }

        #region Notificaciones de Menú Principal (Pasos 1 y 2)

        public void NotifyMenuPlayClicked()
        {
            if (!_isOnboardingActive) return;

            if (_subPhase == OnboardingSubPhase.MenuPlayButton)
            {
                MainMenuManager.Instance?.SelectMasterCardIndex(0); // Mostrar a Tigrán Petrosián por defecto
                SetPhase(OnboardingStep.MatchSetup, OnboardingSubPhase.SetupGrandMaster);
            }
        }

        public void NotifyQuickMatchStartClicked()
        {
            if (!_isOnboardingActive) return;

            if (_subPhase == OnboardingSubPhase.SetupStartMatch || _currentStep == OnboardingStep.MatchSetup)
            {
                if (GameManager.CurrentMatchConfig == null)
                {
                    GameManager.CurrentMatchConfig = new MatchConfig();
                }
                // En la secuencia guiada del tablero el jugador controla las Blancas
                GameManager.CurrentMatchConfig.playerColor = PieceColor.White;

                _pendingGameplayInitOnSceneLoad = true;
                SetPhase(OnboardingStep.BoardMovement, OnboardingSubPhase.BoardSelectPiece);
            }
        }

        public static bool IsImGuiButtonAllowed(string buttonTag)
        {
            if (TutorialHubUI.IsHubOpen) return false;
            if (!IsOnboardingActive) return true;
            if (buttonTag == "skip_tutorial" || buttonTag == "onboarding_continue") return true;
            if (OnboardingOverlayUI.IsPointerOverBlockingOverlay()) return false;

            OnboardingSubPhase phase = _instance._subPhase;
            return phase switch
            {
                OnboardingSubPhase.MenuPlayButton => buttonTag == "menu_play",
                OnboardingSubPhase.SetupStartMatch => buttonTag == "setup_start_match",
                OnboardingSubPhase.SelectDraftCard => buttonTag == "draft_card",
                _ => false
            };
        }

        #endregion

        #region Tablero Guiado, Captura, Draft y Carta (Pasos 3 a 6)

        public void InitializeGuidedGameplayScene()
        {
            if (!_isOnboardingActive) return;
            StopCoroutine(nameof(SetupGuidedBoardCoroutine));
            StartCoroutine(SetupGuidedBoardCoroutine());
        }

        private IEnumerator SetupGuidedBoardCoroutine()
        {
            yield return null; // Esperar a que todos los Start() de SampleScene finalicen su resolución

            if (!_isOnboardingActive) yield break;

            ResolveGameplayReferences();
            SubscribeGameplayEvents();

            if (_boardView == null)
            {
                Debug.LogWarning("[OnboardingManager] No se encontró BoardView en escena.");
                yield break;
            }

            if (TutorialController.Instance != null)
            {
                TutorialController.Instance.DeactivateForStandardMatch();
            }
            GameManager.SelectedGameMode = GameMode.ClassicRoguelike;

            if (GameManager.CurrentMatchConfig != null)
            {
                GameManager.CurrentMatchConfig.playerColor = PieceColor.White;
            }

            PetrosianDefenseEffect.ForceReset();

            if (_inputHandler != null)
            {
                _inputHandler.CancelPendingMoves();
                _inputHandler.EnableAI = false;
                _inputHandler.AIColor = PieceColor.Black;
                _inputHandler.SetMoveFilter(FilterGuidedBoardMove);
            }

            _rewardManager?.ResetMatchProgress();

            if (_cardManager != null)
            {
                _cardManager.ClearTurnEffects();
                _cardManager.EnsureCanvasAndHandView();
                if (_cardManager.HandView != null)
                {
                    if (!_hasSavedPreviousHandAlignment)
                    {
                        _previousHandAlignment = _cardManager.HandView.Alignment;
                        _previousHandBottomOffset = _cardManager.HandView.BottomOffset;
                        _hasSavedPreviousHandAlignment = true;
                    }
                    _cardManager.HandView.ClearHand();
                }
                _cardManager.AddEnergy(Mathf.Max(0, 10 - _cardManager.PlayerEnergy));
            }

            BoardState guidedState = OnboardingLogic.CreateGuidedBoardState();
            _boardView.LoadCustomPosition(guidedState);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameState.Playing);
            }

            if ((int)_currentStep < (int)OnboardingStep.BoardMovement)
            {
                SetPhase(OnboardingStep.BoardMovement, OnboardingSubPhase.BoardSelectPiece);
            }
            else
            {
                SetPhase(_currentStep, _subPhase);
            }

            Debug.Log("<color=cyan><b>[OnboardingManager]</b> Tablero guiado listo (b1->c3, luego c3->d5 captura Torre).</color>");
        }

        private void ResolveGameplayReferences()
        {
            if (_boardView == null) _boardView = FindAnyObjectByType<BoardView>();
            if (_inputHandler == null && _boardView != null) _inputHandler = _boardView.InputHandler;
            if (_inputHandler == null) _inputHandler = FindAnyObjectByType<BoardInputHandler>();
            if (_cardManager == null) _cardManager = CardManager.Instance ?? FindAnyObjectByType<CardManager>();
            if (_rewardManager == null) _rewardManager = CaptureRewardManager.Instance ?? FindAnyObjectByType<CaptureRewardManager>();
        }

        private void SubscribeGameplayEvents()
        {
            if (_inputHandler != null)
            {
                _inputHandler.OnPieceSelected -= HandlePieceSelected;
                _inputHandler.OnPieceSelected += HandlePieceSelected;
                _inputHandler.OnSelectionCleared -= HandleSelectionCleared;
                _inputHandler.OnSelectionCleared += HandleSelectionCleared;
                _inputHandler.OnMoveCompleted -= HandleMoveCompleted;
                _inputHandler.OnMoveCompleted += HandleMoveCompleted;
            }

            if (_rewardManager != null)
            {
                _rewardManager.OnRewardCardSelected -= HandleDraftCardSelected;
                _rewardManager.OnRewardCardSelected += HandleDraftCardSelected;
            }

            if (_cardManager != null)
            {
                _cardManager.OnCardActivated -= HandleCardActivated;
                _cardManager.OnCardActivated += HandleCardActivated;
            }
        }

        private void UnsubscribeGameplayEvents()
        {
            if (_inputHandler != null)
            {
                _inputHandler.OnPieceSelected -= HandlePieceSelected;
                _inputHandler.OnSelectionCleared -= HandleSelectionCleared;
                _inputHandler.OnMoveCompleted -= HandleMoveCompleted;
            }

            if (_rewardManager != null)
            {
                _rewardManager.OnRewardCardSelected -= HandleDraftCardSelected;
            }

            if (_cardManager != null)
            {
                _cardManager.OnCardActivated -= HandleCardActivated;
            }
        }

        private bool FilterGuidedBoardMove(Move move)
        {
            if (!_isOnboardingActive) return true;
            return OnboardingLogic.IsGuidedMoveAllowed(_subPhase, move);
        }

        public static bool IsBoardSquareClickAllowed(BoardCoord coord)
        {
            if (TutorialHubUI.IsHubOpen) return false;
            if (!IsOnboardingActive) return true;
            return OnboardingLogic.IsBoardSquareClickAllowed(_instance._subPhase, coord);
        }

        public static bool IsCardClickAllowed(CardView cardView)
        {
            if (TutorialHubUI.IsHubOpen) return false;
            if (!IsOnboardingActive) return true;
            return _instance._subPhase == OnboardingSubPhase.ActivateBottomCard && cardView != null;
        }

        private void HandlePieceSelected(BoardCoord coord)
        {
            if (!_isOnboardingActive) return;

            if (_subPhase == OnboardingSubPhase.BoardSelectPiece && coord == OnboardingLogic.GuidedKnightStart)
            {
                SetPhase(OnboardingStep.BoardMovement, OnboardingSubPhase.BoardMovePiece);
            }
        }

        private void HandleSelectionCleared()
        {
            if (!_isOnboardingActive) return;

            if (_subPhase == OnboardingSubPhase.BoardMovePiece)
            {
                SetPhase(OnboardingStep.BoardMovement, OnboardingSubPhase.BoardSelectPiece);
            }
            else if (_subPhase == OnboardingSubPhase.CaptureEnemyPiece && _inputHandler != null)
            {
                // Mantener seleccionado el Caballo en c3 para resaltar d5 en rojo con claridad
                StartCoroutine(ReselectCaptureKnightNextFrame());
            }
        }

        private IEnumerator ReselectCaptureKnightNextFrame()
        {
            yield return null;
            if (_isOnboardingActive && _subPhase == OnboardingSubPhase.CaptureEnemyPiece && _inputHandler != null &&
                _boardView != null && _boardView.BoardState != null &&
                _boardView.BoardState[OnboardingLogic.GuidedKnightMid] != null)
            {
                _boardView.BoardState.CurrentTurn = PieceColor.White;
                _inputHandler.SelectPiece(OnboardingLogic.GuidedKnightMid, playAudio: false);
            }
        }

        private void HandleMoveCompleted(Move move)
        {
            if (!_isOnboardingActive || move == null) return;

            if (_subPhase == OnboardingSubPhase.BoardMovePiece &&
                move.From == OnboardingLogic.GuidedKnightStart &&
                move.To == OnboardingLogic.GuidedKnightMid)
            {
                StartCoroutine(PrepareCaptureStepRoutine());
                return;
            }

            if (_subPhase == OnboardingSubPhase.CaptureEnemyPiece &&
                move.From == OnboardingLogic.GuidedKnightMid &&
                move.To == OnboardingLogic.GuidedRookTarget)
            {
                if (_boardView != null && _boardView.BoardState != null)
                {
                    _boardView.BoardState.CurrentTurn = PieceColor.White;
                }

                SetPhase(OnboardingStep.CaptureEconomy, OnboardingSubPhase.ExplainCaptureHud);
            }
        }

        private IEnumerator PrepareCaptureStepRoutine()
        {
            yield return null;
            if (!_isOnboardingActive) yield break;

            if (_boardView != null && _boardView.BoardState != null)
            {
                _boardView.BoardState.CurrentTurn = PieceColor.White;
            }

            SetPhase(OnboardingStep.CaptureEconomy, OnboardingSubPhase.CaptureEnemyPiece);

            if (_inputHandler != null)
            {
                _inputHandler.SelectPiece(OnboardingLogic.GuidedKnightMid, playAudio: false);
            }
        }

        private void TriggerOnboardingDraftModal()
        {
            ResolveGameplayReferences();
            if (_rewardManager != null)
            {
                if (!_rewardManager.FlushDeferredOnboardingDraft())
                {
                    _rewardManager.OpenOnboardingDraftFallback(
                        UpgradeType.Minor_Perk,
                        "¡Superaste 4 Puntos de Captura!"
                    );
                }
            }
        }

        private void HandleDraftCardSelected(CardData chosenCard)
        {
            if (!_isOnboardingActive || _subPhase != OnboardingSubPhase.SelectDraftCard) return;

            _selectedOnboardingCard = chosenCard;
            _selectedCardName = chosenCard != null ? chosenCard.CardName : "tu mejora";

            StartCoroutine(PrepareBottomCardActivationRoutine());
        }

        private IEnumerator PrepareBottomCardActivationRoutine()
        {
            yield return null;
            if (!_isOnboardingActive) yield break;

            ResolveGameplayReferences();

            if (_boardView != null && _boardView.BoardState != null)
            {
                _boardView.BoardState.CurrentTurn = PieceColor.White;
            }

            if (_cardManager != null)
            {
                int neededEnergy = _selectedOnboardingCard != null ? Mathf.Max(10, _selectedOnboardingCard.Cost) : 10;
                if (_cardManager.PlayerEnergy < neededEnergy)
                {
                    _cardManager.AddEnergy(neededEnergy - _cardManager.PlayerEnergy);
                }

                _cardManager.EnsureCanvasAndHandView();
                if (_cardManager.HandView != null)
                {
                    if (!_hasSavedPreviousHandAlignment)
                    {
                        _previousHandAlignment = _cardManager.HandView.Alignment;
                        _previousHandBottomOffset = _cardManager.HandView.BottomOffset;
                        _hasSavedPreviousHandAlignment = true;
                    }
                    _cardManager.HandView.Alignment = HandAlignment.BottomCenter;
                    _cardManager.HandView.SetBottomOffset(28f);
                }
            }

            SetPhase(OnboardingStep.ActivateCard, OnboardingSubPhase.ActivateBottomCard);
        }

        private void HandleCardActivated(CardView activatedView)
        {
            if (!_isOnboardingActive || _subPhase != OnboardingSubPhase.ActivateBottomCard) return;

            MarkOnboardingCompleted();
            SetPhase(OnboardingStep.ActivateCard, OnboardingSubPhase.CompletionMessage);
        }

        #endregion

        #region Helpers de Target para Overlay

        public CardView GetPrimaryBottomCardView()
        {
            if (_cardManager == null) _cardManager = CardManager.Instance ?? FindAnyObjectByType<CardManager>();
            if (_cardManager == null || _cardManager.HandView == null || _cardManager.HandView.CardsInHand == null)
            {
                return null;
            }

            var cards = _cardManager.HandView.CardsInHand;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null && !cards[i].IsDisappeared && !cards[i].IsActivated)
                {
                    return cards[i];
                }
            }

            return cards.Count > 0 ? cards[0] : null;
        }

        public BoardView GetBoardView()
        {
            if (_boardView == null) _boardView = FindAnyObjectByType<BoardView>();
            return _boardView;
        }

        #endregion
    }
}

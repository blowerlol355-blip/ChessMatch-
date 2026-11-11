using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Estados globales del ciclo de juego (Game Loop).
    /// </summary>
    public enum GameState
    {
        MainMenu,
        MasterSelecting,   // Pantalla de selección de Gran Maestro (draft pre-partida)
        Playing,
        Paused,
        GameOver
    }

    /// <summary>
    /// Modos de juego disponibles en ChessMatch.
    /// </summary>
    public enum GameMode
    {
        ClassicRoguelike, // Partida estándar con cartas y selección de Grandes Maestros
        Education,        // Secuencia interactiva de aprendizaje de ajedrez desde cero
        Arcade            // Modo rápido / desafíos (Próximamente)
    }

    /// <summary>
    /// Configuración de partida rápida seleccionada por el jugador antes de entrar al tablero.
    /// </summary>
    [Serializable]
    public class MatchConfig
    {
        public CardData startingCard;
        public string startingMasterName;
        public PieceColor playerColor = PieceColor.White;
        public float timeLimitSeconds = 0f; // 0 = Sin límite, 180 = 3 min, 300 = 5 min, 600 = 10 min
        public AIDifficulty aiDifficulty = AIDifficulty.Medium;

        public bool HasTimeLimit => timeLimitSeconds > 0.1f;
    }

    /// <summary>
    /// Singleton central que coordina el estado global del juego (Game Loop),
    /// la persistencia de dificultad con PlayerPrefs, transiciones de pantallas y limpieza
    /// de memoria en reinicios sin fugas de GameObjects o eventos duplicados.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public static GameMode SelectedGameMode = GameMode.ClassicRoguelike;
        public static bool PendingOpenEducationSelector = false;
        public static int PendingEducationLessonIndex = 0;
        public GameMode CurrentGameMode => SelectedGameMode;

        // Configuración y reloj de partida rápida
        public static MatchConfig CurrentMatchConfig = new MatchConfig();
        public static float WhiteRemainingTime { get; set; } = 0f;
        public static float BlackRemainingTime { get; set; } = 0f;
        public static bool IsTimerRunning => CurrentMatchConfig != null && CurrentMatchConfig.HasTimeLimit && Instance != null && Instance.CurrentState == GameState.Playing;

        private const string DifficultyPrefKey = "ChessMatch_AIDifficulty";

        [Header("Subsistemas")]
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private BoardView boardView;
        [SerializeField] private BoardInputHandler inputHandler;
        [SerializeField] private CardManager cardManager;
        [SerializeField] private MainMenuController menuController;

        [Header("Estado Global")]
        [SerializeField] private GameState currentState = GameState.MainMenu;
        [SerializeField] private AIDifficulty selectedDifficulty = AIDifficulty.Medium;

        public GameState CurrentState => currentState;
        public AIDifficulty SelectedDifficulty => selectedDifficulty;

        /// <summary>
        /// Carta de Gran Maestro elegida durante el draft inicial.
        /// Disponible durante toda la partida hasta que se reinicie.
        /// </summary>
        public CardData ActiveMasterCard { get; set; }

        public void SetActiveMasterCard(CardData card)
        {
            ActiveMasterCard = card;
        }

        public event Action<GameState>      OnGameStateChanged;
        public event Action<AIDifficulty>   OnDifficultyChanged;
        public event Action<bool, int>      OnGameOver;            // (isVictory, totalTurns)
        public event Action<CardData>       OnMasterCardSelected;  // Notifica qué Gran Maestro fue elegido

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

            LoadDifficultyPreference();
            ResolveReferences();
        }

        private void Start()
        {
            ResolveReferences();
            ApplyDifficultyToSubsystems(selectedDifficulty);

            // Ir directamente a jugar al tablero sin menús intermedios
            StartNewGame();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            UnsubscribeEvents();
        }

        private void ResolveReferences()
        {
            if (boardManager == null) boardManager = FindAnyObjectByType<BoardManager>();
            if (boardView == null && boardManager != null) boardView = boardManager.BoardView;
            if (boardView == null) boardView = FindAnyObjectByType<BoardView>();

            if (inputHandler == null && boardView != null) inputHandler = boardView.InputHandler;
            if (inputHandler == null) inputHandler = FindAnyObjectByType<BoardInputHandler>();

            if (cardManager == null && boardManager != null) cardManager = boardManager.CardManager;
            if (cardManager == null) cardManager = FindAnyObjectByType<CardManager>();

            if (menuController == null) menuController = FindAnyObjectByType<MainMenuController>();

            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (boardView != null)
            {
                boardView.OnGameInitialized -= HandleGameInitialized;
                boardView.OnGameInitialized += HandleGameInitialized;

                if (boardView.BoardState?.History != null)
                {
                    SubscribeToHistory(boardView.BoardState.History);
                }
            }
        }

        private void UnsubscribeEvents()
        {
            if (boardView != null)
            {
                boardView.OnGameInitialized -= HandleGameInitialized;
                if (boardView.BoardState?.History != null)
                {
                    boardView.BoardState.History.OnCheckmate -= HandleCheckmate;
                    boardView.BoardState.History.OnStalemate -= HandleStalemate;
                }
            }
        }

        private void HandleGameInitialized(BoardState state)
        {
            if (state?.History != null)
            {
                SubscribeToHistory(state.History);
            }
        }

        private void SubscribeToHistory(MoveHistory history)
        {
            history.OnCheckmate -= HandleCheckmate;
            history.OnCheckmate += HandleCheckmate;
            history.OnStalemate -= HandleStalemate;
            history.OnStalemate += HandleStalemate;
        }

        private void HandleCheckmate(PieceColor matedColor)
        {
            if (currentState != GameState.Playing) return;

            PieceColor humanColor = CurrentMatchConfig != null ? CurrentMatchConfig.playerColor : PieceColor.White;
            bool isVictory = (matedColor != humanColor);
            int totalTurns = boardView?.BoardState != null ? boardView.BoardState.FullmoveNumber : 1;

            SetState(GameState.GameOver);
            OnGameOver?.Invoke(isVictory, totalTurns);
            Debug.Log($"<color={(isVictory ? "lime" : "red")}><b>[GameManager] Fin de partida: {(isVictory ? "¡VICTORIA!" : "¡DERROTA!")}</b> en {totalTurns} turnos.</color>");
        }

        private void HandleStalemate(PieceColor stalematedColor)
        {
            if (currentState != GameState.Playing) return;

            int totalTurns = boardView?.BoardState != null ? boardView.BoardState.FullmoveNumber : 1;
            SetState(GameState.GameOver);
            OnGameOver?.Invoke(false, totalTurns);
            Debug.Log("<color=cyan><b>[GameManager] Fin de partida: ¡TABLAS POR REY AHOGADO!</b></color>");
        }

        #region Máquina de Estados y Transiciones

        public void SetState(GameState newState)
        {
            if (currentState == newState) return;

            currentState = newState;
            OnGameStateChanged?.Invoke(currentState);

            // Bloquear o habilitar interacción de piezas según el estado
            if (inputHandler != null)
            {
                if (currentState != GameState.Playing)
                {
                    inputHandler.Deselect();
                }
            }

            Debug.Log($"<color=white><b>[GameManager]</b> Estado cambiado a: <b>{currentState}</b></color>");
        }

        /// <summary>
        /// Inicia el flujo de selección de Gran Maestro (draft pre-partida).
        /// Cambia el estado a <see cref="GameState.MasterSelecting"/> para que
        /// <see cref="MasterSelectionUI"/> pueda mostrarse y el tablero permanezca oculto.
        /// </summary>
        public void BeginMasterSelection()
        {
            Debug.Log("<color=cyan><b>[GameManager] Iniciando selección de Gran Maestro...</b></color>");
            ResolveReferences();

            MasterSelectionUI masterUI = FindAnyObjectByType<MasterSelectionUI>();
            if (masterUI == null)
            {
                GameObject targetObj = boardView != null ? boardView.gameObject : gameObject;
                masterUI = targetObj.AddComponent<MasterSelectionUI>();
            }

            SetState(GameState.MasterSelecting);

            if (masterUI != null)
            {
                masterUI.Show();
            }
            else
            {
                // Fallback de seguridad si no hay UI
                StartNewGame();
            }
        }

        /// <summary>
        /// Confirma la carta de Gran Maestro elegida y arranca la partida.
        /// Llamado por <see cref="MasterSelectionUI"/> al presionar "Confirmar y Comenzar Ronda".
        /// </summary>
        /// <param name="masterCard">Carta de Gran Maestro seleccionada por el jugador.</param>
        public void ConfirmMasterSelection(CardData masterCard)
        {
            ActiveMasterCard = masterCard;
            OnMasterCardSelected?.Invoke(masterCard);

            Debug.Log($"<color=#FFD700><b>[GameManager] Gran Maestro confirmado: {masterCard?.CardName ?? "(ninguno)"}</b></color>");

            // Arrancar la partida directamente
            StartNewGame();
        }

        /// <summary>
        /// Inicia el Modo Arcade ("Asedio de 15 Minutos / Chess Survivors").
        /// </summary>
        public void StartArcadeMode()
        {
            Debug.Log("<color=#FFD700><b>[GameManager] Iniciando Modo Arcade (Chess Survivors)...</b></color>");
            ResolveReferences();
            SelectedGameMode = GameMode.Arcade;

            if (TutorialController.Instance != null)
            {
                TutorialController.Instance.DeactivateForStandardMatch();
            }

            PetrosianDefenseEffect.ForceReset();
            (CaptureRewardManager.Instance ?? FindAnyObjectByType<CaptureRewardManager>())?.ResetMatchProgress();

            if (inputHandler != null)
            {
                inputHandler.CancelPendingMoves();
                inputHandler.EnableAI = false;
                inputHandler.enabled = false;
            }

            if (cardManager != null)
            {
                cardManager.ClearTurnEffects();
            }

            ArcadeSurvivorController arcade = GetComponent<ArcadeSurvivorController>() ?? FindAnyObjectByType<ArcadeSurvivorController>(FindObjectsInactive.Include);
            if (arcade == null)
            {
                GameObject target = boardView != null ? boardView.gameObject : gameObject;
                arcade = target.AddComponent<ArcadeSurvivorController>();
            }

            SetState(GameState.Playing);
            arcade.StartArcadeRun();
        }

        /// <summary>
        /// Inicia el Modo Educativo cargando la secuencia de lecciones interactivas.
        /// </summary>
        public void StartEducationMode(int lessonIndex = 0)
        {
            Debug.Log($"<color=cyan><b>[GameManager] Iniciando Modo Educativo (Lección {lessonIndex + 1})...</b></color>");
            ResolveReferences();
            SelectedGameMode = GameMode.Education;
            SetState(GameState.Playing);

            inputHandler?.CancelPendingMoves();
            (CaptureRewardManager.Instance ?? FindAnyObjectByType<CaptureRewardManager>())?.ResetMatchProgress();

            if (cardManager != null)
            {
                cardManager.ClearTurnEffects();
            }

            TutorialController controller = FindAnyObjectByType<TutorialController>();
            if (controller == null)
            {
                GameObject target = boardView != null ? boardView.gameObject : gameObject;
                controller = target.AddComponent<TutorialController>();
            }

            TutorialUI ui = FindAnyObjectByType<TutorialUI>();
            if (ui == null)
            {
                GameObject target = boardView != null ? boardView.gameObject : gameObject;
                ui = target.AddComponent<TutorialUI>();
            }

            if (controller != null)
            {
                controller.StartEducationMode(lessonIndex + 1);
                if (PendingOpenEducationSelector)
                {
                    PendingOpenEducationSelector = false;
                    ui?.OpenLessonSelector();
                }
                return;
            }
            PendingOpenEducationSelector = false;

            TutorialLessonManager tutorial = FindAnyObjectByType<TutorialLessonManager>();
            if (tutorial == null)
            {
                GameObject target = boardView != null ? boardView.gameObject : gameObject;
                tutorial = target.AddComponent<TutorialLessonManager>();
            }

            if (tutorial != null)
            {
                tutorial.StartEducationMode(lessonIndex);
            }
        }

        /// <summary>
        /// Provee la lista por defecto de las 4 cartas de Gran Maestro (Major_Master) para configurar partida y drafts.
        /// 1. Tigrán Petrosián - El Tigre de Hierro ("Jaque Anulado")
        /// 2. Mijaíl Tal - El Mago de Riga ("Bosque Oscuro")
        /// 3. Judit Polgár - Reina de la Tormenta ("Ataque Implacable")
        /// 4. Garry Kaspárov - El Ogro de Bakú ("Presión Absoluta")
        /// </summary>
        public static List<CardData> GetAvailableStartingCards()
        {
            return MasterCardDatabase.CreateDefaultMasterCards();
        }

        private void Update()
        {
            if (IsTimerRunning && boardView != null && boardView.BoardState != null)
            {
                if (boardView.BoardState.CurrentTurn == PieceColor.White)
                {
                    WhiteRemainingTime -= Time.deltaTime;
                    if (WhiteRemainingTime <= 0f)
                    {
                        WhiteRemainingTime = 0f;
                        HandleTimeout(PieceColor.White);
                    }
                }
                else
                {
                    BlackRemainingTime -= Time.deltaTime;
                    if (BlackRemainingTime <= 0f)
                    {
                        BlackRemainingTime = 0f;
                        HandleTimeout(PieceColor.Black);
                    }
                }
            }
        }

        private void HandleTimeout(PieceColor outOfTimeColor)
        {
            Debug.Log($"<color=red><b>¡TIEMPO AGOTADO!</b> El bando {outOfTimeColor} se ha quedado sin tiempo.</color>");
            bool isPlayerVictory = CurrentMatchConfig != null && outOfTimeColor != CurrentMatchConfig.playerColor;
            SetState(GameState.GameOver);
            OnGameOver?.Invoke(isPlayerVictory, boardView != null && boardView.BoardState != null ? boardView.BoardState.FullmoveNumber : 0);
        }

        /// <summary>
        /// Inicia una nueva partida desde el Menú Principal:
        /// Genera el tablero 8x8 limpio, distribuye piezas, mano de cartas y pasa a estado Playing.
        /// Aplica la configuración rápida (carta activa, color de bando, reloj y dificultad de IA).
        /// </summary>
        public void StartNewGame()
        {
            if (OnboardingManager.IsOnboardingGameplayActive)
            {
                ResolveReferences();
                SetState(GameState.Playing);
                OnboardingManager.Instance?.InitializeGuidedGameplayScene();
                return;
            }

            if (SelectedGameMode == GameMode.Arcade)
            {
                StartArcadeMode();
                return;
            }

            if (SelectedGameMode == GameMode.Education)
            {
                ArcadeSurvivorController.Instance?.CleanupArcadeSession();
                if (inputHandler != null) inputHandler.enabled = true;
                int startLessonIdx = Mathf.Max(0, PendingEducationLessonIndex);
                PendingEducationLessonIndex = 0;
                StartEducationMode(startLessonIdx);
                return;
            }

            ArcadeSurvivorController.Instance?.CleanupArcadeSession();
            if (inputHandler != null) inputHandler.enabled = true;

            Debug.Log("<color=green><b>[GameManager] Iniciando nueva partida (StartNewGame)...</b></color>");
            ResolveReferences();

            if (TutorialController.Instance != null)
            {
                TutorialController.Instance.DeactivateForStandardMatch();
            }
            BoardView.EnsureCanvasScalersConfigured();

            // Sincronizar configuraciones rápidas
            if (CurrentMatchConfig != null)
            {
                selectedDifficulty = CurrentMatchConfig.aiDifficulty;
                ActiveMasterCard = (CurrentMatchConfig.startingCard != null || !string.IsNullOrEmpty(CurrentMatchConfig.startingMasterName))
                    ? MasterCardDatabase.CloneCardWithFreshEffect(CurrentMatchConfig.startingCard, CurrentMatchConfig.startingMasterName)
                    : null;

                if (CurrentMatchConfig.HasTimeLimit)
                {
                    WhiteRemainingTime = CurrentMatchConfig.timeLimitSeconds;
                    BlackRemainingTime = CurrentMatchConfig.timeLimitSeconds;
                }
                else
                {
                    WhiteRemainingTime = 0f;
                    BlackRemainingTime = 0f;
                }
            }
            ApplyDifficultyToSubsystems(selectedDifficulty);

            // Transicionar de inmediato a Playing para que la UI responda
            SetState(GameState.Playing);

            try
            {
                // Limpieza estricta de piezas, corutinas pendientes, economía y cartas previas para evitar memory leaks
                PetrosianDefenseEffect.ForceReset();
                inputHandler?.CancelPendingMoves();
                (CaptureRewardManager.Instance ?? FindAnyObjectByType<CaptureRewardManager>())?.ResetMatchProgress();

                if (cardManager != null)
                {
                    cardManager.ClearTurnEffects();
                }

                if (boardView != null)
                {
                    boardView.InitializeGame();
                }

                // Configurar bando del jugador e IA
                if (inputHandler != null && CurrentMatchConfig != null)
                {
                    inputHandler.EnableAI = true;
                    inputHandler.AIColor = CurrentMatchConfig.playerColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
                    inputHandler.AIDifficulty = CurrentMatchConfig.aiDifficulty;

                    // Si el jugador eligió Negras, la IA juega con Blancas y abre la partida
                    if (CurrentMatchConfig.playerColor == PieceColor.Black)
                    {
                        inputHandler.TriggerAITurn();
                    }
                }

                if (cardManager != null)
                {
                    cardManager.SetupInitialHand();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Error al inicializar subsistemas en StartNewGame: {ex}");
            }
        }

        /// <summary>
        /// Reinicia la partida en curso manteniendo la dificultad actual.
        /// </summary>
        public void RestartGame()
        {
            if (SelectedGameMode == GameMode.Arcade)
            {
                StartArcadeMode();
                return;
            }

            ResolveReferences();

            PetrosianDefenseEffect.ForceReset();
            inputHandler?.CancelPendingMoves();
            (CaptureRewardManager.Instance ?? FindAnyObjectByType<CaptureRewardManager>())?.ResetMatchProgress();

            if (cardManager != null)
            {
                cardManager.ClearTurnEffects();
            }

            if (ActiveMasterCard != null)
            {
                ActiveMasterCard = MasterCardDatabase.CloneCardWithFreshEffect(ActiveMasterCard, ActiveMasterCard.CardName);
            }
            else if (CurrentMatchConfig != null && (CurrentMatchConfig.startingCard != null || !string.IsNullOrEmpty(CurrentMatchConfig.startingMasterName)))
            {
                ActiveMasterCard = MasterCardDatabase.CloneCardWithFreshEffect(CurrentMatchConfig.startingCard, CurrentMatchConfig.startingMasterName);
            }

            if (CurrentMatchConfig != null && CurrentMatchConfig.HasTimeLimit)
            {
                WhiteRemainingTime = CurrentMatchConfig.timeLimitSeconds;
                BlackRemainingTime = CurrentMatchConfig.timeLimitSeconds;
            }

            if (boardView != null)
            {
                boardView.InitializeGame();
            }

            if (inputHandler != null && CurrentMatchConfig != null)
            {
                inputHandler.EnableAI = true;
                inputHandler.AIColor = CurrentMatchConfig.playerColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
                inputHandler.AIDifficulty = CurrentMatchConfig.aiDifficulty;
                if (CurrentMatchConfig.playerColor == PieceColor.Black)
                {
                    inputHandler.TriggerAITurn();
                }
            }

            if (cardManager != null)
            {
                cardManager.SetupInitialHand();
            }

            SetState(GameState.Playing);
        }

        /// <summary>
        /// Vuelve al Menú Principal limpiando el estado de la partida y liberando memoria.
        /// </summary>
        public void ReturnToMenu()
        {
            ResolveReferences();

            ArcadeSurvivorController.Instance?.CleanupArcadeSession();
            if (inputHandler != null) inputHandler.enabled = true;
            PetrosianDefenseEffect.ForceReset();
            inputHandler?.CancelPendingMoves();
            (CaptureRewardManager.Instance ?? FindAnyObjectByType<CaptureRewardManager>())?.ResetMatchProgress();

            if (TutorialController.Instance != null)
            {
                TutorialController.Instance.DeactivateForStandardMatch();
            }

            if (cardManager != null)
            {
                cardManager.ClearTurnEffects();
            }

            if (inputHandler != null)
            {
                inputHandler.Deselect();
            }

            // Si existe la escena MainMenuScene, volver asíncronamente a ella
            if (Application.CanStreamedLevelBeLoaded("MainMenuScene") &&
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenuScene")
            {
                UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("MainMenuScene");
                return;
            }

            // Opcional: restaurar el tablero inicial para que luzca de fondo en el menú
            if (boardView != null)
            {
                boardView.InitializeGame();
            }

            SetState(GameState.MainMenu);
        }

        /// <summary>
        /// Pausa la partida activa.
        /// </summary>
        public void PauseGame()
        {
            if (currentState == GameState.Playing)
            {
                SetState(GameState.Paused);
            }
        }

        /// <summary>
        /// Reanuda la partida pausada.
        /// </summary>
        public void ResumeGame()
        {
            if (currentState == GameState.Paused)
            {
                SetState(GameState.Playing);
            }
        }

        /// <summary>
        /// Alterna entre Pausado y Jugando.
        /// </summary>
        public void TogglePause()
        {
            if (currentState == GameState.Playing) PauseGame();
            else if (currentState == GameState.Paused) ResumeGame();
        }

        /// <summary>
        /// Cierra la aplicación de forma segura.
        /// </summary>
        public void QuitGame()
        {
            Debug.Log("<color=grey>[GameManager] Saliendo de la aplicación...</color>");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion

        #region Gestión y Persistencia de Dificultad (PlayerPrefs)

        public void SetDifficulty(AIDifficulty difficulty)
        {
            selectedDifficulty = difficulty;
            SaveDifficultyPreference(difficulty);
            ApplyDifficultyToSubsystems(difficulty);

            OnDifficultyChanged?.Invoke(difficulty);
            Debug.Log($"<color=cyan>[GameManager] Dificultad IA guardada: <b>{difficulty}</b></color>");
        }

        public void SetDifficulty(int difficultyIndex)
        {
            if (difficultyIndex >= 0 && difficultyIndex <= 2)
            {
                SetDifficulty((AIDifficulty)difficultyIndex);
            }
        }

        private void ApplyDifficultyToSubsystems(AIDifficulty difficulty)
        {
            if (boardManager != null)
            {
                boardManager.AIDifficulty = difficulty;
            }

            if (inputHandler != null)
            {
                inputHandler.AIDifficulty = difficulty;
            }
        }

        private void LoadDifficultyPreference()
        {
            if (PlayerPrefs.HasKey(DifficultyPrefKey))
            {
                int saved = PlayerPrefs.GetInt(DifficultyPrefKey, (int)AIDifficulty.Medium);
                selectedDifficulty = (AIDifficulty)Mathf.Clamp(saved, 0, 2);
            }
            else
            {
                selectedDifficulty = AIDifficulty.Medium;
            }
        }

        private void SaveDifficultyPreference(AIDifficulty difficulty)
        {
            PlayerPrefs.SetInt(DifficultyPrefKey, (int)difficulty);
            PlayerPrefs.Save();
        }

        #endregion
    }
}

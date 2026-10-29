using System;
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
    /// Singleton central que coordina el estado global del juego (Game Loop),
    /// la persistencia de dificultad con PlayerPrefs, transiciones de pantallas y limpieza
    /// de memoria en reinicios sin fugas de GameObjects o eventos duplicados.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

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

            // Iniciar en MainMenu por defecto
            SetState(GameState.MainMenu);
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

            // En ajedrez estándar, el jugador controla Blancas y la IA Negras
            bool isVictory = (matedColor == PieceColor.Black);
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
        /// Inicia una nueva partida desde el Menú Principal:
        /// Genera el tablero 8x8 limpio, distribuye piezas, mano de cartas y pasa a estado Playing.
        /// </summary>
        public void StartNewGame()
        {
            Debug.Log("<color=green><b>[GameManager] Iniciando nueva partida (StartNewGame)...</b></color>");
            ResolveReferences();
            ApplyDifficultyToSubsystems(selectedDifficulty);

            // Transicionar de inmediato a Playing para que la UI responda
            SetState(GameState.Playing);

            try
            {
                // Limpieza estricta de piezas y cartas previas para evitar memory leaks
                if (cardManager != null)
                {
                    cardManager.ClearTurnEffects();
                }

                if (boardView != null)
                {
                    boardView.InitializeGame();
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
            ResolveReferences();

            if (cardManager != null)
            {
                cardManager.ClearTurnEffects();
            }

            if (boardView != null)
            {
                boardView.InitializeGame();
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

            if (cardManager != null)
            {
                cardManager.ClearTurnEffects();
            }

            if (inputHandler != null)
            {
                inputHandler.Deselect();
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

using System;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Datos detallados de una captura ocurrida en el tablero para economía de puntos y VFX/UI.
    /// </summary>
    public struct PieceCaptureEventData
    {
        public Piece CapturedPiece;
        public Piece CapturingPiece;
        public BoardCoord CaptureCoord;
        public Vector3 WorldPosition;
        public bool DeliveredCheck;
        public bool IsCheckmate;
        public Move Move;
    }

    /// <summary>
    /// Fachada y controlador de alto nivel que conecta el tablero de ajedrez (BoardView / BoardInputHandler)
    /// con el sistema de cartas tipo Balatro (CardManager).
    /// </summary>
    public class BoardManager : MonoBehaviour
    {
        [Header("Subsistemas")]
        [SerializeField] private BoardView boardView;
        [SerializeField] private BoardInputHandler inputHandler;
        [SerializeField] private CardManager cardManager;
        [SerializeField] private CaptureRewardManager captureRewardManager;

        [Header("Configuración de Inteligencia Artificial")]
        [SerializeField] private bool enableAI = true;
        [SerializeField] private AIDifficulty aiDifficulty = AIDifficulty.Medium;

        public BoardView BoardView => boardView;
        public BoardInputHandler InputHandler => inputHandler;
        public CardManager CardManager => cardManager;
        public CaptureRewardManager CaptureRewardManager => captureRewardManager;
        public BoardState BoardState => boardView != null ? boardView.BoardState : null;

        public bool EnableAI
        {
            get => inputHandler != null ? inputHandler.EnableAI : enableAI;
            set
            {
                enableAI = value;
                if (inputHandler != null) inputHandler.EnableAI = value;
            }
        }

        public AIDifficulty AIDifficulty
        {
            get => inputHandler != null ? inputHandler.AIDifficulty : aiDifficulty;
            set
            {
                aiDifficulty = value;
                if (inputHandler != null) inputHandler.AIDifficulty = value;
                OnDifficultyChanged?.Invoke(value);
            }
        }

        public event Action<AIDifficulty> OnDifficultyChanged;
        public event Action<PieceCaptureEventData> OnPieceCaptured;
        public event Action<Piece, BoardCoord, Vector3, bool> OnPlayerCheckDelivered;
        public event Action<int> OnExtraTurnsChanged;
        public event Action OnGameRestarted;

        private int _pendingExtraTurns = 0;
        public int PendingExtraTurns => BoardState != null ? BoardState.PendingExtraTurns : _pendingExtraTurns;

        /// <summary>
        /// Otorga turnos consecutivos adicionales al bando activo en la ronda actual.
        /// </summary>
        public void GrantExtraTurn(int count = 1)
        {
            if (count <= 0) return;

            if (BoardState != null)
            {
                BoardState.GrantExtraTurn(BoardState.CurrentTurn, count);
                _pendingExtraTurns = BoardState.PendingExtraTurns;
            }
            else
            {
                _pendingExtraTurns += count;
            }

            OnExtraTurnsChanged?.Invoke(PendingExtraTurns);
            Debug.Log($"<color=#C066FF><b>[BoardManager]</b> Turnos consecutivos otorgados (+{count}). Pendientes: {PendingExtraTurns}.</color>");
        }

        public void SetDifficulty(AIDifficulty newDifficulty)
        {
            AIDifficulty = newDifficulty;
        }

        public void SetDifficulty(int difficultyIndex)
        {
            if (difficultyIndex >= 0 && difficultyIndex <= 2)
            {
                AIDifficulty = (AIDifficulty)difficultyIndex;
            }
        }

        private void Awake()
        {
            RetroAudioManager.EnsureInstance();

            if (boardView == null) boardView = GetComponent<BoardView>();
            if (boardView == null) boardView = FindAnyObjectByType<BoardView>();

            if (inputHandler == null && boardView != null) inputHandler = boardView.InputHandler;
            if (inputHandler == null) inputHandler = FindAnyObjectByType<BoardInputHandler>();

            if (inputHandler != null)
            {
                inputHandler.BindBoardManager(this);
                inputHandler.EnableAI = enableAI;
                inputHandler.AIDifficulty = aiDifficulty;
            }

            if (cardManager == null) cardManager = GetComponent<CardManager>();
            if (cardManager == null) cardManager = FindAnyObjectByType<CardManager>();

            if (cardManager == null && boardView != null)
            {
                cardManager = boardView.gameObject.AddComponent<CardManager>();
            }

            if (cardManager != null)
            {
                if (inputHandler != null) cardManager.BindInputHandler(inputHandler);
                if (boardView != null) cardManager.BindBoardView(boardView);
            }

            EnsureEconomySubsystems();
        }

        private void Start()
        {
            if (boardView == null) boardView = FindAnyObjectByType<BoardView>();
            if (inputHandler == null && boardView != null) inputHandler = boardView.InputHandler;
            if (inputHandler != null) inputHandler.BindBoardManager(this);
            if (cardManager == null) cardManager = FindAnyObjectByType<CardManager>();

            if (cardManager != null)
            {
                if (inputHandler != null) cardManager.BindInputHandler(inputHandler);
                if (boardView != null) cardManager.BindBoardView(boardView);
            }

            EnsureEconomySubsystems();
        }

        private void EnsureEconomySubsystems()
        {
            if (captureRewardManager == null) captureRewardManager = GetComponent<CaptureRewardManager>();
            if (captureRewardManager == null) captureRewardManager = FindAnyObjectByType<CaptureRewardManager>();

            GameObject hostObj = boardView != null ? boardView.gameObject : gameObject;
            if (captureRewardManager == null)
            {
                captureRewardManager = hostObj.AddComponent<CaptureRewardManager>();
            }
            captureRewardManager.BindBoardManager(this);

            if (FindAnyObjectByType<RewardDraftModalUI>() == null)
            {
                hostObj.AddComponent<RewardDraftModalUI>();
            }
        }

        /// <summary>
        /// Notifica una captura ejecutada en el tablero a la economía de puntos y sistemas de recompensa.
        /// </summary>
        public void NotifyPieceCaptured(PieceCaptureEventData captureData)
        {
            OnPieceCaptured?.Invoke(captureData);
        }

        /// <summary>
        /// Notifica un Jaque válido sin captura para evaluar bonificaciones opcionales.
        /// </summary>
        public void NotifyCheckDelivered(Piece checkingPiece, BoardCoord checkedKingCoord, Vector3 checkedKingWorldPos, bool isCheckmate)
        {
            OnPlayerCheckDelivered?.Invoke(checkingPiece, checkedKingCoord, checkedKingWorldPos, isCheckmate);
        }

        /// <summary>
        /// Reinicia la partida y restablece el tablero y los efectos de cartas.
        /// </summary>
        [ContextMenu("Restart Game")]
        public void RestartGame()
        {
            _pendingExtraTurns = 0;
            PetrosianDefenseEffect.ForceReset();
            if (BoardState != null)
            {
                BoardState.PendingExtraTurns = 0;
                BoardState.ClearImmobilizedPieces();
                BoardState.ClearIntimidation();
            }
            inputHandler?.CancelPendingMoves();
            OnGameRestarted?.Invoke();
            if (cardManager != null) cardManager.ClearTurnEffects();
            if (boardView != null) boardView.InitializeGame();
        }

        /// <summary>
        /// Ejecuta las pruebas de lógica de ajedrez y cartas.
        /// </summary>
        [ContextMenu("Run All Core & Card Tests")]
        public void RunAllTests()
        {
            var results = CoreChessTests.RunAllTests();
            int passed = 0;
            foreach (var r in results)
            {
                if (r.Passed)
                {
                    passed++;
                    Debug.Log($"<color=green>[PASS]</color> {r.Name}");
                }
                else
                {
                    Debug.LogError($"<color=red>[FAIL]</color> {r.Name} -> {r.ErrorMessage}");
                }
            }
            Debug.Log($"<color={(passed == results.Count ? "lime" : "yellow")}><b>Resumen Pruebas: {passed}/{results.Count} superadas exitosamente.</b></color>");
        }
    }
}

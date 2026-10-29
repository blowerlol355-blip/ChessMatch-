using System;
using UnityEngine;

namespace ChessBalatro
{
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

        [Header("Configuración de Inteligencia Artificial")]
        [SerializeField] private bool enableAI = true;
        [SerializeField] private AIDifficulty aiDifficulty = AIDifficulty.Medium;

        public BoardView BoardView => boardView;
        public BoardInputHandler InputHandler => inputHandler;
        public CardManager CardManager => cardManager;
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
            if (boardView == null) boardView = GetComponent<BoardView>();
            if (boardView == null) boardView = FindAnyObjectByType<BoardView>();

            if (inputHandler == null && boardView != null) inputHandler = boardView.InputHandler;
            if (inputHandler == null) inputHandler = FindAnyObjectByType<BoardInputHandler>();

            if (inputHandler != null)
            {
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
        }

        private void Start()
        {
            if (boardView == null) boardView = FindAnyObjectByType<BoardView>();
            if (inputHandler == null && boardView != null) inputHandler = boardView.InputHandler;
            if (cardManager == null) cardManager = FindAnyObjectByType<CardManager>();

            if (cardManager != null)
            {
                if (inputHandler != null) cardManager.BindInputHandler(inputHandler);
                if (boardView != null) cardManager.BindBoardView(boardView);
            }
        }

        /// <summary>
        /// Reinicia la partida y restablece el tablero y los efectos de cartas.
        /// </summary>
        [ContextMenu("Restart Game")]
        public void RestartGame()
        {
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

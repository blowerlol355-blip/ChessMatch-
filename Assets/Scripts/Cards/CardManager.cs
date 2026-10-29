using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChessBalatro
{
    /// <summary>
    /// Gestor central de cartas tipo Balatro.
    /// Orquesta la mano de cartas, los fondos/energía del jugador, la activación de cartas previas a la jugada,
    /// la inyección de modificadores a MoveValidator y la limpieza de efectos al terminar el turno en BoardInputHandler.
    /// </summary>
    public class CardManager : MonoBehaviour
    {
        [Header("Referencias del Tablero")]
        [SerializeField] private BoardView boardView;
        [SerializeField] private BoardInputHandler inputHandler;

        [Header("Economía y Recursos")]
        [SerializeField] private int playerEnergy = 10;

        [Header("Cartas Iniciales de Prueba")]
        [SerializeField] private List<CardData> initialCards = new List<CardData>();
        [SerializeField] private bool autoSpawnTestCardsOnStart = true;

        [Header("Interfaz de Usuario")]
        [SerializeField] private CardHandView handView;
        [SerializeField] private Canvas targetCanvas;

        // Estado activo durante el turno
        private readonly List<CardView> _activeCardViews = new List<CardView>();
        private readonly List<ICardEffect> _activeEffects = new List<ICardEffect>();

        public int PlayerEnergy => playerEnergy;
        public IReadOnlyList<CardView> ActiveCardViews => _activeCardViews;
        public IReadOnlyList<ICardEffect> ActiveEffects => _activeEffects;

        public event Action<int> OnEnergyChanged;
        public event Action<CardView> OnCardActivated;
        public event Action<CardView> OnCardDeactivated;
        public event Action OnTurnEffectsCleared;

        private void Awake()
        {
            if (boardView == null) boardView = FindAnyObjectByType<BoardView>();
            if (inputHandler == null && boardView != null) inputHandler = boardView.InputHandler;
            if (inputHandler == null) inputHandler = FindAnyObjectByType<BoardInputHandler>();
        }

        private void Start()
        {
            if (boardView == null) boardView = FindAnyObjectByType<BoardView>();
            if (inputHandler == null && boardView != null) inputHandler = boardView.InputHandler;

            SubscribeToTurnEvents();
            EnsureCanvasAndHandView();

            if (autoSpawnTestCardsOnStart)
            {
                SetupInitialHand();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromTurnEvents();
        }

        private void SubscribeToTurnEvents()
        {
            if (inputHandler != null)
            {
                inputHandler.OnMoveCompleted -= HandleMoveCompleted;
                inputHandler.OnMoveCompleted += HandleMoveCompleted;
            }

            if (boardView != null)
            {
                boardView.OnMoveExecuted -= HandleMoveCompleted;
                boardView.OnMoveExecuted += HandleMoveCompleted;
            }
        }

        private void UnsubscribeFromTurnEvents()
        {
            if (inputHandler != null)
            {
                inputHandler.OnMoveCompleted -= HandleMoveCompleted;
            }

            if (boardView != null)
            {
                boardView.OnMoveExecuted -= HandleMoveCompleted;
            }
        }

        /// <summary>
        /// Vincula explícitamente el manejador de entrada para sincronizar la limpieza de efectos de cartas.
        /// </summary>
        public void BindInputHandler(BoardInputHandler handler)
        {
            if (inputHandler == handler && handler != null) return;
            if (inputHandler != null) inputHandler.OnMoveCompleted -= HandleMoveCompleted;
            inputHandler = handler;
            if (inputHandler != null) inputHandler.OnMoveCompleted += HandleMoveCompleted;
        }

        /// <summary>
        /// Vincula la vista del tablero para reaccionar a jugadas directas.
        /// </summary>
        public void BindBoardView(BoardView view)
        {
            if (boardView == view && view != null) return;
            if (boardView != null) boardView.OnMoveExecuted -= HandleMoveCompleted;
            boardView = view;
            if (boardView != null) boardView.OnMoveExecuted += HandleMoveCompleted;
        }

        /// <summary>
        /// Instancia o asigna el Canvas y el contenedor de mano CardHandView si no existen.
        /// </summary>
        private void EnsureCanvasAndHandView()
        {
            EnsureEventSystem();

            if (handView != null) return;

            handView = FindAnyObjectByType<CardHandView>();
            if (handView != null) return;

            if (targetCanvas == null)
            {
                GameObject existingCanvas = GameObject.Find("Cards_Canvas");
                if (existingCanvas != null) targetCanvas = existingCanvas.GetComponent<Canvas>();
            }

            if (targetCanvas == null) targetCanvas = FindAnyObjectByType<Canvas>();

            if (targetCanvas == null)
            {
                GameObject canvasObj = new GameObject("Cards_Canvas");
                targetCanvas = canvasObj.AddComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                targetCanvas.sortingOrder = 50; // Capa superior para garantizar visibilidad sobre el tablero
                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObj.AddComponent<GraphicRaycaster>();
            }
            else
            {
                // Si ya existe un Canvas, garantizar que el sorting order sea suficiente para renderizarse
                if (targetCanvas.sortingOrder < 30)
                {
                    targetCanvas.sortingOrder = 30;
                }
            }

            GameObject handObj = new GameObject("CardHandContainer");
            handObj.transform.SetParent(targetCanvas.transform, false);
            handView = handObj.AddComponent<CardHandView>();
            handView.ConfigureHandContainerRect();
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            }
        }

        /// <summary>
        /// Genera la mano inicial incorporando la carta de Gran Maestro (Petrosián) y cartas complementarias.
        /// </summary>
        public void SetupInitialHand()
        {
            if (handView == null) EnsureCanvasAndHandView();

            if (handView != null)
            {
                handView.ClearHand();
            }

            // 1. Carta de Gran Maestro / Tigrán Petrosián seleccionada
            CardData masterCard = GameManager.Instance != null ? GameManager.Instance.ActiveMasterCard : null;
            if (masterCard == null)
            {
                masterCard = PetrosianCardData.CreateRuntimeInstance();
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ActiveMasterCard = masterCard;
                }
            }
            handView.AddCardToHand(masterCard, this);

            // 2. Si el usuario configuró cartas adicionales en el Inspector, usarlas
            if (initialCards != null && initialCards.Count > 0)
            {
                for (int i = 0; i < initialCards.Count; i++)
                {
                    if (initialCards[i] != null && initialCards[i] != masterCard)
                    {
                        handView.AddCardToHand(initialCards[i], this);
                    }
                }
                return;
            }

            // 3. Cartas de prueba predeterminadas complementarias
            TurboPawnCardData turboPawn = TurboPawnCardData.CreateInstance();
            handView.AddCardToHand(turboPawn, this);

            CardData knightBoost = ScriptableObject.CreateInstance<CardData>();
            knightBoost.Initialize(
                "Salto de Caballería",
                "Los Caballos pueden desplazarse 1 casilla en cruz ortogonal además de su salto tradicional en L.",
                4,
                CardRarity.Rare,
                new KnightBoostEffect()
            );
            handView.AddCardToHand(knightBoost, this);
        }

        /// <summary>
        /// Alterna la activación de una carta (permite activarla antes del movimiento o deseleccionarla para recuperar el coste).
        /// </summary>
        public bool TryToggleCardActivation(CardView cardView)
        {
            if (cardView == null || cardView.CardData == null) return false;

            if (cardView.IsActivated)
            {
                DeactivateCard(cardView);
                return false;
            }
            else
            {
                return TryActivateCard(cardView);
            }
        }

        /// <summary>
        /// Intenta activar la carta: valida el coste de energía, valida el estado del tablero,
        /// consume el coste, aplica el efecto sobre el tablero e inmediatamente actualiza las casillas legales en el tablero.
        /// </summary>
        public bool TryActivateCard(CardView cardView)
        {
            if (cardView == null || cardView.CardData == null) return false;
            CardData data = cardView.CardData;

            // Requisito 4: Validar coste disponible
            if (playerEnergy < data.Cost)
            {
                Debug.LogWarning($"<color=orange>[CardManager] Energía insuficiente ({playerEnergy}/{data.Cost}) para activar '{data.CardName}'.</color>");
                return false;
            }

            BoardState board = boardView != null ? boardView.BoardState : null;
            if (board == null)
            {
                Debug.LogWarning("[CardManager] No hay BoardState activo en el tablero.");
                return false;
            }

            ICardEffect effect = cardView.Effect;
            if (effect != null && !effect.CanApply(board, board.CurrentTurn))
            {
                Debug.LogWarning($"<color=yellow>[CardManager] La carta '{data.CardName}' no se puede aplicar en este turno/estado.</color>");
                return false;
            }

            // Consumir el coste
            playerEnergy -= data.Cost;
            OnEnergyChanged?.Invoke(playerEnergy);

            // Inyectar y aplicar el efecto
            if (effect != null)
            {
                effect.ApplyEffect(board);

                if (!board.ActiveCardEffects.Contains(effect))
                    board.ActiveCardEffects.Add(effect);

                if (effect is IMoveModifier modifier && !board.Modifiers.Contains(modifier))
                    board.Modifiers.Add(modifier);

                _activeEffects.Add(effect);
            }

            _activeCardViews.Add(cardView);
            cardView.SetActivated(true);

            // Requisito 4: Actualizar inmediatamente los movimientos disponibles en el tablero
            if (inputHandler != null)
            {
                inputHandler.RefreshLegalMoves();
            }

            OnCardActivated?.Invoke(cardView);
            Debug.Log($"<color=lime><b>[CardManager]</b> Carta activada: <b>{data.CardName}</b> (Coste: ${data.Cost}, Energía restante: ${playerEnergy}). Movimientos actualizados.</color>");
            return true;
        }

        /// <summary>
        /// Desactiva una carta previamente activada, reembolsa su coste y limpia su efecto.
        /// </summary>
        public void DeactivateCard(CardView cardView)
        {
            if (cardView == null || !_activeCardViews.Contains(cardView)) return;

            CardData data = cardView.CardData;
            ICardEffect effect = cardView.Effect;
            BoardState board = boardView != null ? boardView.BoardState : null;

            // Reembolsar coste
            playerEnergy += data.Cost;
            OnEnergyChanged?.Invoke(playerEnergy);

            // Limpiar efecto del tablero
            if (effect != null)
            {
                effect.ResetEffect();
                if (board != null)
                {
                    board.ActiveCardEffects.Remove(effect);
                    if (effect is IMoveModifier modifier)
                        board.Modifiers.Remove(modifier);
                }
                _activeEffects.Remove(effect);
            }

            _activeCardViews.Remove(cardView);
            cardView.SetActivated(false);

            // Actualizar tablero de inmediato
            if (inputHandler != null)
            {
                inputHandler.RefreshLegalMoves();
            }

            OnCardDeactivated?.Invoke(cardView);
            Debug.Log($"<color=yellow>[CardManager] Carta desactivada: <b>{data.CardName}</b>. Coste reembolsado (${playerEnergy}).</color>");
        }

        /// <summary>
        /// Requisito 5: Se dispara al completar el movimiento de ajedrez.
        /// Limpia los efectos de la carta consumida y deja el tablero listo para el siguiente turno.
        /// </summary>
        private void HandleMoveCompleted(Move move)
        {
            ClearTurnEffects();
        }

        /// <summary>
        /// Limpia y restablece todos los efectos activos al terminar el movimiento del jugador.
        /// </summary>
        public void ClearTurnEffects()
        {
            if (_activeEffects.Count == 0 && _activeCardViews.Count == 0) return;

            BoardState board = boardView != null ? boardView.BoardState : null;

            // 1. Resetear y desvincular cada efecto
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                var effect = _activeEffects[i];
                if (effect != null)
                {
                    effect.ResetEffect();
                    if (board != null)
                    {
                        board.ActiveCardEffects.Remove(effect);
                        if (effect is IMoveModifier modifier)
                            board.Modifiers.Remove(modifier);
                    }
                }
            }
            _activeEffects.Clear();

            // 2. Animar consumo y retirada visual de la carta utilizada
            for (int i = 0; i < _activeCardViews.Count; i++)
            {
                var cv = _activeCardViews[i];
                if (cv != null)
                {
                    cv.SetActivated(false);
                    cv.OnTurnEndAfterUsed();
                    if (handView != null)
                    {
                        handView.RemoveCardFromHand(cv);
                    }
                }
            }
            _activeCardViews.Clear();

            // 3. Refrescar selección de casilla si fuera necesario
            if (inputHandler != null)
            {
                inputHandler.RefreshLegalMoves();
            }

            OnTurnEffectsCleared?.Invoke();
            Debug.Log("<color=grey><b>[CardManager]</b> Efectos de cartas del turno limpiados tras completar el movimiento de ajedrez.</color>");
        }

        /// <summary>
        /// Agrega energía/monedas al jugador.
        /// </summary>
        public void AddEnergy(int amount)
        {
            playerEnergy += amount;
            OnEnergyChanged?.Invoke(playerEnergy);
        }
    }
}

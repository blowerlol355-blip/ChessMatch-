using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Gestor central de la Economía de Puntos por Captura y Selector de Recompensas (Draft).
    /// Escucha las capturas del jugador en <see cref="BoardManager"/>, acumula puntos según
    /// <see cref="PieceValueTable"/>, solicita el texto flotante y dispara el Draft de Mejoras Menores
    /// (cada 4 pts) o Cartas de Gran Maestro (cada 10 pts o al capturar la Dama rival).
    /// </summary>
    public class CaptureRewardManager : MonoBehaviour
    {
        public static CaptureRewardManager Instance { get; private set; }

        public const int MinorUpgradeThreshold = 4;
        public const int MasterCardThreshold = 10;

        [Header("Referencias del Tablero y Cartas")]
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private BoardInputHandler inputHandler;
        [SerializeField] private CardManager cardManager;

        [Header("Configuración de Economía")]
        [Tooltip("Si está activo, entregar un Jaque válido otorga +1 punto adicional.")]
        [SerializeField] private bool enableCheckBonus = true;
        [SerializeField, Range(0f, 1f)] private float draftOpenDelay = 0.35f;

        [Header("Estado Actual (Solo Lectura / Debug)")]
        [SerializeField] private int currentCapturePoints = 0;
        [SerializeField] private int minorCyclePoints = 0;
        [SerializeField] private int masterCyclePoints = 0;

        [Header("Catálogo Opcional desde Inspector")]
        [SerializeField] private List<CardData> minorUpgradeCards = new List<CardData>();
        [SerializeField] private List<CardData> masterCards = new List<CardData>();

        private readonly HashSet<CardData> _runtimeDraftCards = new HashSet<CardData>();
        private Coroutine _pendingDraftCoroutine;
        private BoardManager _subscribedBoardManager;
        private bool _hasDeferredOnboardingDraft = false;
        private UpgradeType _deferredOnboardingType = UpgradeType.Minor_Perk;
        private string _deferredOnboardingReason = string.Empty;

        public int CurrentCapturePoints => currentCapturePoints;
        public int MinorCyclePoints => minorCyclePoints;
        public int MasterCyclePoints => masterCyclePoints;
        public bool EnableCheckBonus => enableCheckBonus;
        public bool IsDraftOpen { get; private set; } = false;
        public bool HasDeferredOnboardingDraft => _hasDeferredOnboardingDraft;

        /// <summary>
        /// Activo en partidas normales/roguelike y desactivado en Modo Educativo.
        /// </summary>
        public bool IsEconomyActive => IsEconomyEnabledForCurrentMode();

        public event Action<int, int, int> OnPointsUpdated;
        public event Action<Vector3, int, PieceType, bool> OnPointsPopupRequested;
        public event Action<Vector3, string, bool> OnCustomPopupRequested;
        public event Action<UpgradeType, List<CardData>, string> OnDraftRequested;
        public event Action<CardData> OnRewardCardSelected;

        public static bool IsEconomyEnabledForCurrentMode()
        {
            if (TutorialController.IsEducationalModeActive) return false;
            if (GameManager.SelectedGameMode == GameMode.Education) return false;
            if (GameManager.SelectedGameMode == GameMode.Arcade || ArcadeSurvivorController.IsArcadeModeActive) return false;
            return true;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeToBoardManager();
        }

        private void Start()
        {
            ResolveReferences();
            SubscribeToBoardManager();
            EnsureModalUIExists();
            OnPointsUpdated?.Invoke(currentCapturePoints, minorCyclePoints, masterCyclePoints);
        }

        private void OnDisable()
        {
            UnsubscribeFromBoardManager();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            UnsubscribeFromBoardManager();
            CleanupRuntimeDraftCards(exceptChosen: null);
        }

        public void BindBoardManager(BoardManager manager)
        {
            if (manager == null) return;
            if (boardManager == manager && _subscribedBoardManager == manager) return;

            boardManager = manager;
            SubscribeToBoardManager();
        }

        private void ResolveReferences()
        {
            if (boardManager == null) boardManager = GetComponent<BoardManager>();
            if (boardManager == null) boardManager = FindAnyObjectByType<BoardManager>();

            if (inputHandler == null && boardManager != null) inputHandler = boardManager.InputHandler;
            if (inputHandler == null) inputHandler = FindAnyObjectByType<BoardInputHandler>();

            if (cardManager == null && boardManager != null) cardManager = boardManager.CardManager;
            if (cardManager == null) cardManager = FindAnyObjectByType<CardManager>();
        }

        private void EnsureModalUIExists()
        {
            if (RewardDraftModalUI.Instance == null && FindAnyObjectByType<RewardDraftModalUI>() == null)
            {
                gameObject.AddComponent<RewardDraftModalUI>();
            }
        }

        private void SubscribeToBoardManager()
        {
            if (boardManager == null) return;
            if (_subscribedBoardManager == boardManager) return;

            UnsubscribeFromBoardManager();

            _subscribedBoardManager = boardManager;
            _subscribedBoardManager.OnPieceCaptured += HandlePieceCaptured;
            _subscribedBoardManager.OnPlayerCheckDelivered += HandlePlayerCheckDelivered;
            _subscribedBoardManager.OnGameRestarted += ResetMatchProgress;
        }

        private void UnsubscribeFromBoardManager()
        {
            if (_subscribedBoardManager != null)
            {
                _subscribedBoardManager.OnPieceCaptured -= HandlePieceCaptured;
                _subscribedBoardManager.OnPlayerCheckDelivered -= HandlePlayerCheckDelivered;
                _subscribedBoardManager.OnGameRestarted -= ResetMatchProgress;
                _subscribedBoardManager = null;
            }
        }

        /// <summary>
        /// Reinicia los contadores de puntos y cierra cualquier Draft pendiente al iniciar o reiniciar partida.
        /// </summary>
        public void ResetMatchProgress()
        {
            if (_pendingDraftCoroutine != null)
            {
                StopCoroutine(_pendingDraftCoroutine);
                _pendingDraftCoroutine = null;
            }

            currentCapturePoints = 0;
            minorCyclePoints = 0;
            masterCyclePoints = 0;
            IsDraftOpen = false;
            _hasDeferredOnboardingDraft = false;

            if (inputHandler != null)
            {
                inputHandler.IsRewardDraftBlocking = false;
            }

            CleanupRuntimeDraftCards(exceptChosen: null);
            RewardDraftModalUI.Instance?.ForceClose();
            OnPointsUpdated?.Invoke(currentCapturePoints, minorCyclePoints, masterCyclePoints);
        }

        private PieceColor ResolveHumanPlayerColor()
        {
            if (GameManager.CurrentMatchConfig != null)
            {
                return GameManager.CurrentMatchConfig.playerColor;
            }

            if (inputHandler != null && inputHandler.EnableAI)
            {
                return inputHandler.AIColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
            }

            return PieceColor.White;
        }

        private void HandlePieceCaptured(PieceCaptureEventData data)
        {
            if (!IsEconomyEnabledForCurrentMode()) return;
            if (data.CapturedPiece == null) return;

            PieceColor playerColor = ResolveHumanPlayerColor();
            if (data.CapturingPiece != null && data.CapturingPiece.Color != playerColor)
            {
                // Las capturas realizadas por la IA no otorgan puntos ni recompensas al jugador
                return;
            }

            if (data.CapturedPiece.Color == playerColor) return;

            int earnedPoints = PieceValueTable.CalculateEarnedPoints(
                data.CapturedPiece.Type,
                data.DeliveredCheck,
                enableCheckBonus
            );

            bool hasPolgarBonus = data.DeliveredCheck && HasActivePolgarBonus(playerColor);
            if (hasPolgarBonus)
            {
                earnedPoints += JuditPolgarEffect.CheckBonusPoints;
            }

            if (earnedPoints <= 0) return;

            // 1. Lanzar texto flotante dorado sobre la casilla de captura
            if (hasPolgarBonus)
            {
                OnCustomPopupRequested?.Invoke(data.WorldPosition, $"+{earnedPoints} PTS!\n¡ATAQUE POLGÁR!", true);
            }
            else
            {
                OnPointsPopupRequested?.Invoke(data.WorldPosition, earnedPoints, data.CapturedPiece.Type, data.DeliveredCheck);
            }

            // 2. Acumular puntos y evaluar umbrales
            bool isQueenCapture = data.CapturedPiece.Type == PieceType.Queen;
            AwardPointsAndCheckThresholds(earnedPoints, isQueenCapture, data.IsCheckmate);
        }

        private void HandlePlayerCheckDelivered(Piece checkingPiece, BoardCoord kingCoord, Vector3 kingWorldPos, bool isCheckmate)
        {
            if (!IsEconomyEnabledForCurrentMode()) return;

            PieceColor playerColor = ResolveHumanPlayerColor();
            if (checkingPiece != null && checkingPiece.Color != playerColor) return;

            bool hasPolgarBonus = HasActivePolgarBonus(playerColor);
            if (!enableCheckBonus && !hasPolgarBonus) return;

            int bonusPoints = (enableCheckBonus ? PieceValueTable.CheckBonusPoints : 0)
                            + (hasPolgarBonus ? JuditPolgarEffect.CheckBonusPoints : 0);

            if (bonusPoints <= 0) return;

            if (hasPolgarBonus)
            {
                OnCustomPopupRequested?.Invoke(kingWorldPos, $"+{bonusPoints} PTS!\n¡ATAQUE POLGÁR!", true);
            }
            else
            {
                OnPointsPopupRequested?.Invoke(kingWorldPos, bonusPoints, PieceType.None, true);
            }

            AwardPointsAndCheckThresholds(bonusPoints, isQueenCapture: false, isGameOver: isCheckmate);
        }

        private bool HasActivePolgarBonus(PieceColor playerColor)
        {
            BoardState state = boardManager != null ? boardManager.BoardState : null;
            return JuditPolgarEffect.HasActiveEffect(state, playerColor);
        }

        /// <summary>
        /// Suma puntos de captura adicionales directamente (p. ej. bonificaciones de cartas especiales o pruebas)
        /// y evalúa los umbrales de recompensa.
        /// </summary>
        public void AddBonusPoints(int points, Vector3 worldPos, string customPopupLabel = null, bool isGameOver = false)
        {
            if (!IsEconomyEnabledForCurrentMode() || points <= 0) return;

            if (!string.IsNullOrEmpty(customPopupLabel))
            {
                OnCustomPopupRequested?.Invoke(worldPos, customPopupLabel, points >= 3);
            }
            else
            {
                OnPointsPopupRequested?.Invoke(worldPos, points, PieceType.None, false);
            }

            AwardPointsAndCheckThresholds(points, isQueenCapture: false, isGameOver: isGameOver);
        }

        /// <summary>
        /// Solicita mostrar un texto flotante dorado personalizado sobre una casilla del tablero.
        /// </summary>
        public void NotifyCustomPopup(Vector3 worldPos, string customText, bool isMajorHighlight = false)
        {
            if (string.IsNullOrEmpty(customText)) return;
            OnCustomPopupRequested?.Invoke(worldPos, customText, isMajorHighlight);
        }

        private void AwardPointsAndCheckThresholds(int earnedPoints, bool isQueenCapture, bool isGameOver)
        {
            currentCapturePoints += earnedPoints;
            minorCyclePoints += earnedPoints;
            masterCyclePoints += earnedPoints;

            bool isMatchPlaying = !isGameOver &&
                                  (GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.Playing);

            if (isMatchPlaying)
            {
                bool reachedMaster = isQueenCapture || masterCyclePoints >= MasterCardThreshold;
                bool reachedMinor = minorCyclePoints >= MinorUpgradeThreshold;

                if (reachedMaster)
                {
                    masterCyclePoints = (isQueenCapture && masterCyclePoints < MasterCardThreshold)
                        ? 0
                        : (masterCyclePoints % MasterCardThreshold);
                    minorCyclePoints %= MinorUpgradeThreshold;

                    OnPointsUpdated?.Invoke(currentCapturePoints, minorCyclePoints, masterCyclePoints);

                    string reason = isQueenCapture
                        ? "¡Captura directa de Dama rival!"
                        : $"¡Alcanzados {MasterCardThreshold} puntos de captura!";
                    BeginDraftSequence(UpgradeType.Major_Master, reason);
                    return;
                }

                if (reachedMinor)
                {
                    minorCyclePoints %= MinorUpgradeThreshold;
                    OnPointsUpdated?.Invoke(currentCapturePoints, minorCyclePoints, masterCyclePoints);
                    BeginDraftSequence(UpgradeType.Minor_Perk, $"¡Alcanzados {MinorUpgradeThreshold} puntos de captura!");
                    return;
                }
            }

            OnPointsUpdated?.Invoke(currentCapturePoints, minorCyclePoints, masterCyclePoints);
        }

        private void BeginDraftSequence(UpgradeType rewardType, string reason)
        {
            if (!IsEconomyEnabledForCurrentMode()) return;

            if (OnboardingManager.ShouldDeferDraftModal)
            {
                _hasDeferredOnboardingDraft = true;
                _deferredOnboardingType = rewardType;
                _deferredOnboardingReason = reason ?? string.Empty;
                return;
            }

            EnsureModalUIExists();
            ResolveReferences();

            // Bloquear inmediatamente el turno de la IA y la entrada del tablero mientras se elige recompensa
            IsDraftOpen = true;
            if (inputHandler != null)
            {
                inputHandler.IsRewardDraftBlocking = true;
            }

            if (_pendingDraftCoroutine != null)
            {
                StopCoroutine(_pendingDraftCoroutine);
            }
            _pendingDraftCoroutine = StartCoroutine(OpenDraftAfterBeatRoutine(rewardType, reason));
        }

        /// <summary>
        /// Abre el modal de Draft que quedó diferido durante el Paso 4 del Tutorial Guiado.
        /// </summary>
        public bool FlushDeferredOnboardingDraft()
        {
            if (!_hasDeferredOnboardingDraft) return false;

            _hasDeferredOnboardingDraft = false;
            OpenOnboardingDraftFallback(_deferredOnboardingType, _deferredOnboardingReason);
            return true;
        }

        /// <summary>
        /// Abre inmediatamente un modal de Draft para el Tutorial Guiado sin retardo de corutina.
        /// </summary>
        public void OpenOnboardingDraftFallback(UpgradeType rewardType, string reason)
        {
            EnsureModalUIExists();
            ResolveReferences();

            List<CardData> draftOptions = BuildDraftOptions(rewardType);
            if (draftOptions.Count == 0) return;

            IsDraftOpen = true;
            if (inputHandler != null)
            {
                inputHandler.IsRewardDraftBlocking = true;
            }

            OnDraftRequested?.Invoke(rewardType, draftOptions, reason ?? string.Empty);
        }

        private IEnumerator OpenDraftAfterBeatRoutine(UpgradeType rewardType, string reason)
        {
            if (draftOpenDelay > 0.01f)
            {
                yield return new WaitForSecondsRealtime(draftOpenDelay);
            }

            _pendingDraftCoroutine = null;

            if (!IsDraftOpen ||
                !IsEconomyEnabledForCurrentMode() ||
                (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing))
            {
                IsDraftOpen = false;
                if (inputHandler != null) inputHandler.IsRewardDraftBlocking = false;
                yield break;
            }

            List<CardData> draftOptions = BuildDraftOptions(rewardType);
            if (draftOptions.Count == 0)
            {
                Debug.LogWarning("[CaptureRewardManager] No había cartas disponibles para el Draft; reanudando partida.");
                IsDraftOpen = false;
                if (inputHandler != null) inputHandler.IsRewardDraftBlocking = false;
                yield break;
            }

            Debug.Log($"<color=#FFD700><b>[CaptureRewardManager]</b> Abriendo Draft ({rewardType}): {reason} ({draftOptions.Count} opciones).</color>");
            OnDraftRequested?.Invoke(rewardType, draftOptions, reason);
        }

        /// <summary>
        /// Selecciona una carta del Draft: la incorpora a la mano activa del jugador,
        /// reproduce el sonido de recompensa en RetroAudioManager y reanuda la partida / turno de IA.
        /// </summary>
        public void SelectRewardCard(CardData chosenCard)
        {
            if (chosenCard != null)
            {
                ResolveReferences();

                if (cardManager == null) cardManager = CardManager.Instance ?? FindAnyObjectByType<CardManager>();
                if (cardManager != null)
                {
                    cardManager.EnsureCanvasAndHandView();
                    if (cardManager.HandView != null)
                    {
                        cardManager.HandView.AddCardToHand(chosenCard, cardManager);
                    }
                }

                RetroAudioManager.Instance?.PlayRewardSound();
                OnRewardCardSelected?.Invoke(chosenCard);
                Debug.Log($"<color=lime><b>[CaptureRewardManager]</b> Recompensa añadida a la mano: <b>{chosenCard.CardName}</b>.</color>");
            }

            CleanupRuntimeDraftCards(exceptChosen: chosenCard);
            RewardDraftModalUI.Instance?.ForceClose();

            IsDraftOpen = false;
            if (inputHandler != null)
            {
                inputHandler.IsRewardDraftBlocking = false;
            }
        }

        private void CleanupRuntimeDraftCards(CardData exceptChosen)
        {
            if (_runtimeDraftCards.Count == 0) return;

            foreach (CardData tempCard in _runtimeDraftCards)
            {
                if (tempCard != null && tempCard != exceptChosen)
                {
                    Destroy(tempCard);
                }
            }
            _runtimeDraftCards.Clear();
        }

        #region Factoría de Catálogo de Cartas (Mejoras Menores y Grandes Maestros)

        public List<CardData> BuildDraftOptions(UpgradeType rewardType)
        {
            CleanupRuntimeDraftCards(exceptChosen: null);

            List<CardData> pool = rewardType == UpgradeType.Major_Master
                ? BuildMasterCardPool()
                : BuildMinorUpgradePool();

            ShuffleList(pool);

            int maxChoices = Mathf.Min(3, pool.Count);
            List<CardData> selected = new List<CardData>(maxChoices);
            for (int i = 0; i < maxChoices; i++)
            {
                selected.Add(pool[i]);
            }

            // Destruir sobrantes de este pool no incluidos en selected si no se usarán
            for (int i = maxChoices; i < pool.Count; i++)
            {
                CardData unused = pool[i];
                if (unused != null && _runtimeDraftCards.Remove(unused))
                {
                    Destroy(unused);
                }
            }

            return selected;
        }

        private List<CardData> BuildMinorUpgradePool()
        {
            var pool = new List<CardData>();

            // Incluir primero cartas configuradas opcionalmente desde el Inspector (clonadas para aislar estado)
            if (minorUpgradeCards != null)
            {
                for (int i = 0; i < minorUpgradeCards.Count; i++)
                {
                    if (minorUpgradeCards[i] != null)
                    {
                        pool.Add(CloneCardDataForRuntime(minorUpgradeCards[i], UpgradeType.Minor_Perk));
                    }
                }
            }

            // 1. AcceleratedPawnEffect
            CardData accelPawn = ScriptableObject.CreateInstance<CardData>();
            accelPawn.Initialize(
                "Peón Acelerado+",
                "Tus Peones pueden avanzar 2 casillas al frente desde cualquier fila y desplazarse 1 paso diagonal libre.",
                2,
                CardRarity.Common,
                new AcceleratedPawnEffect(),
                Resources.Load<Sprite>("Pieces/white_pawn"),
                UpgradeType.Minor_Perk
            );
            _runtimeDraftCards.Add(accelPawn);
            pool.Add(accelPawn);

            // 2. RoyalTrenchEffect
            CardData royalTrench = ScriptableObject.CreateInstance<CardData>();
            royalTrench.Initialize(
                "Trinchera Real",
                "Tu Rey puede huir hasta 2 casillas ortogonales y tus Peones defensivos se deslizan 1 paso lateral para cubrirle.",
                3,
                CardRarity.Uncommon,
                new RoyalTrenchEffect(),
                Resources.Load<Sprite>("Pieces/white_king"),
                UpgradeType.Minor_Perk
            );
            _runtimeDraftCards.Add(royalTrench);
            pool.Add(royalTrench);

            // 3. TacticalBoostEffect
            CardData tacticalBoost = ScriptableObject.CreateInstance<CardData>();
            tacticalBoost.Initialize(
                "Impulso Táctico",
                "Alfiles ganan 1 paso ortogonal; Torres y Caballos ganan 1 paso corto en diagonal durante este turno.",
                3,
                CardRarity.Rare,
                new TacticalBoostEffect(),
                Resources.Load<Sprite>("Pieces/white_bishop"),
                UpgradeType.Minor_Perk
            );
            _runtimeDraftCards.Add(tacticalBoost);
            pool.Add(tacticalBoost);

            return pool;
        }

        private List<CardData> BuildMasterCardPool()
        {
            var pool = new List<CardData>();

            if (masterCards != null)
            {
                for (int i = 0; i < masterCards.Count; i++)
                {
                    if (masterCards[i] != null)
                    {
                        pool.Add(CloneCardDataForRuntime(masterCards[i], UpgradeType.Major_Master));
                    }
                }
            }

            // Registrar las 4 leyendas por defecto: Petrosián, Tal, Polgár y Kaspárov
            List<CardData> defaultMasters = MasterCardDatabase.CreateDefaultMasterCards();
            for (int i = 0; i < defaultMasters.Count; i++)
            {
                CardData master = defaultMasters[i];
                if (master != null)
                {
                    _runtimeDraftCards.Add(master);
                    pool.Add(master);
                }
            }

            return pool;
        }

        private CardData CloneCardDataForRuntime(CardData template, UpgradeType fallbackUpgradeType)
        {
            CardData clone = ScriptableObject.CreateInstance<CardData>();
            ICardEffect clonedEffect = null;

            ICardEffect sourceEffect = template.GetOrCreateEffect();
            if (sourceEffect != null)
            {
                try
                {
                    clonedEffect = Activator.CreateInstance(sourceEffect.GetType()) as ICardEffect;
                }
                catch
                {
                    clonedEffect = sourceEffect;
                }
            }

            clone.Initialize(
                template.CardName,
                template.Description,
                template.Cost,
                template.Rarity,
                clonedEffect,
                MasterCardDatabase.ResolveSprite(template),
                template.UpgradeType != UpgradeType.Minor_Perk ? template.UpgradeType : fallbackUpgradeType
            );
            if (template.HasCustomAuraColor || !string.IsNullOrEmpty(template.Subtitle))
            {
                clone.SetMasterVisuals(
                    MasterCardDatabase.GetCardSubtitle(template),
                    MasterCardDatabase.GetCardAuraColor(template)
                );
            }
            _runtimeDraftCards.Add(clone);
            return clone;
        }

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swapIdx = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[swapIdx]) = (list[swapIdx], list[i]);
            }
        }

        #endregion
    }
}


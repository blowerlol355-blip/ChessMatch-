using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChessBalatro
{
    /// <summary>
    /// Controlador principal del Modo Arcade ("Asedio de 15 Minutos / Chess Survivors") bajo Captura Pura de Ajedrez:
    /// - Sin barras de vida ni daño numérico: toda captura elimina al rival en 1 movimiento.
    /// - Rey Blanco con 3 Escudos Reales + Escudo de Hierro (si tiene a Tigrán Petrosián equipado).
    /// - Coordina Piezas Aliadas Físicas (SummonedAllyPieceManager), Sinergias (CardSynergySystem),
    ///   Expansión cada 3:30 (DynamicBoardExpander: 8x8 -> 8x11 -> 8x14 -> 8x17 -> 8x20)
    ///   y Jefe Final por Sellos de Jaque al minuto 15:00 (MegaBossPieceController).
    /// </summary>
    public class ArcadeSurvivorController : MonoBehaviour
    {
        public static ArcadeSurvivorController Instance { get; private set; }

        public static bool IsArcadeModeActive =>
            Instance != null &&
            Instance._isSessionActive &&
            GameManager.SelectedGameMode == GameMode.Arcade;

        [Header("Configuración de Arena y Rey")]
        [SerializeField] private float tileSize = 1.0f;

        private bool _isSessionActive = false;
        private bool _isPaused = false;
        private bool _isGameOver = false;
        private bool _bossTriggered = false;

        // Subsistemas
        private DynamicBoardExpander _boardExpander;
        private SummonedAllyPieceManager _allyManager;
        private CardSynergySystem _synergySystem;
        private EnemyWaveSpawner _enemySpawner;
        private MegaBossPieceController _bossController;
        private ArcadeLevelUpUI _levelUpUI;

        // Rey Blanco y Escudos
        private GameObject _kingObj;
        private SpriteRenderer _kingSpriteRenderer;
        private SpriteRenderer _kingShadowRenderer;
        private readonly SpriteRenderer[] _royalShieldPips = new SpriteRenderer[ArcadeLogic.BaseRoyalShields];
        private Transform _ironShieldOrbitRoot;
        private SpriteRenderer _ironShieldRenderer;

        private ArcadeCoord _kingCoord = new ArcadeCoord(3, 2);
        private Vector3 _kingVisualFrom;
        private Vector3 _kingVisualTarget;
        private float _kingSlideProgress = 1f;
        private float _moveCooldownTimer = 0f;
        private float _invulnerableTimer = 0f;
        private float _kingHitFlashTimer = 0f;

        // Estado de partida
        private int _royalShields = ArcadeLogic.BaseRoyalShields;
        private float _ironShieldRechargeTimer = 0f;
        private float _ironShieldOrbitAngle = 0f;

        private int _currentLevel = 1;
        private int _currentExp = 0;
        private int _expToNextLevel = 4;
        private int _pendingLevelUps = 0;
        private float _elapsedSeconds = 0f;
        private float _bossFightSeconds = 0f;
        private float _moveCooldownDuration = ArcadeLogic.MoveCooldownSeconds;
        private float _magnetPickupRange = 2.60f;

        private readonly HashSet<ArcadeMasterId> _ownedMasters = new HashSet<ArcadeMasterId>();
        private ArcadeRunStats _runStats = new ArcadeRunStats();

        // Banners
        private string _bannerTitle = string.Empty;
        private string _bannerSubtitle = string.Empty;
        private Color _bannerColor = Color.white;
        private float _bannerTimer = 0f;

        // Input (Tap, Swipe, Keyboard)
        private bool _isPointerTracking = false;
        private Vector2 _pointerDownScreenPos;
        private float _pointerDownTime = 0f;
        private ArcadeCoord _pointerDownCoord;
        private bool _pointerDownCoordValid = false;
        private bool _swipeConsumedThisDrag = false;
        private Rect _topHudScreenRect;
        private Rect _bottomStripScreenRect;

        // Cámara restaurable
        private bool _savedCameraState = false;
        private float _savedCamOrthoSize = 5f;
        private Vector3 _savedCamPosition = new Vector3(0f, 0f, -10f);
        private int _lastStyledWidth = 0;
        private int _lastStyledHeight = 0;

        // Sprites procedurales cacheados
        private Sprite _whiteSquareSprite;
        private Sprite _softDiscSprite;
        private Sprite _shieldEmblemSprite;
        private Font _cachedPopupFont;

        // Texturas y estilos OnGUI
        private Texture2D _darkHudBgTex;
        private Texture2D _goldFillTex;
        private Texture2D _crimsonFillTex;
        private Texture2D _emeraldFillTex;
        private Texture2D _cyanFillTex;
        private Texture2D _trackDarkTex;
        private Texture2D _btnPrimaryTex;
        private Texture2D _btnSecondaryTex;
        private Texture2D _btnDangerTex;
        private Texture2D _modalDimTex;

        private GUIStyle _clockStyle;
        private GUIStyle _headerLabelStyle;
        private GUIStyle _smallLabelStyle;
        private GUIStyle _weaponNameStyle;
        private GUIStyle _bannerTitleStyle;
        private GUIStyle _bannerSubStyle;
        private GUIStyle _modalTitleStyle;
        private GUIStyle _modalBodyStyle;
        private GUIStyle _primaryBtnStyle;
        private GUIStyle _secondaryBtnStyle;
        private GUIStyle _dangerBtnStyle;

        // Propiedades públicas
        public bool IsSessionActive => _isSessionActive;
        public bool IsPaused => _isPaused || (_levelUpUI != null && _levelUpUI.IsModalOpen) || TutorialHubUI.IsHubOpen;
        public bool IsGameOver => _isGameOver;
        public bool BossTriggered => _bossTriggered;
        public float TileSize => tileSize;
        public ArcadeCoord KingCoord => _kingCoord;
        public Vector3 KingWorldPosition => _kingObj != null ? _kingObj.transform.position : CoordToWorld(_kingCoord);
        public int RoyalShields => _royalShields;
        public bool HasPetrosianIronShield => _ownedMasters.Contains(ArcadeMasterId.Petrosian);
        public bool IsPetrosianIronShieldReady => HasPetrosianIronShield && _ironShieldRechargeTimer <= 0f;
        public int CurrentLevel => _currentLevel;
        public int CurrentExp => _currentExp;
        public int ExpToNextLevel => _expToNextLevel;
        public float ElapsedSeconds => _elapsedSeconds;
        public float MagnetPickupRange => _magnetPickupRange;
        public ArcadeRunStats RunStats => _runStats;

        public DynamicBoardExpander BoardExpander => _boardExpander;
        public SummonedAllyPieceManager AllyManager => _allyManager;
        public CardSynergySystem SynergySystem => _synergySystem;
        public EnemyWaveSpawner EnemySpawner => _enemySpawner;
        public MegaBossPieceController BossController => _bossController;
        public ArcadeLevelUpUI LevelUpUI => _levelUpUI;

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
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            RestoreTimeAndCamera();
            DestroyCachedTextures();
        }

        public void StartArcadeRun()
        {
            Time.timeScale = 1f;
            _isSessionActive = true;
            _isPaused = false;
            _isGameOver = false;
            _bossTriggered = false;

            HideStandardChessSceneVisuals();
            SaveInitialCameraState();
            EnsureSubsystems();
            ResetRunState();

            _boardExpander.ResetAndBuildInitialBoard();
            _synergySystem.ResetSynergies();
            _allyManager.ResetAllies();
            _enemySpawner.ResetSpawner();
            _bossController.ResetBoss();
            if (_levelUpUI != null) _levelUpUI.CloseModal();

            SpawnWhiteKing();
            _boardExpander.RecalculateCameraTargets(snapImmediately: true);

            // Desplegar 1 Peón Aliado inicial en el tablero 8x8 desde el segundo 0
            _allyManager.SummonAllyPiece(PieceType.Pawn, silent: true);

            // Si el jugador equipó una carta de Gran Maestro antes de entrar (ej. Tigrán Petrosián), activarla al inicio
            ApplyOptionalPresetMasterIfConfigured();
            _synergySystem.EvaluateAndSyncSynergies(showNotifications: false);

            GameBackdrop.EnsureBackdrop();
            ShowBanner("¡ASEDIO PURO DE AJEDREZ!", "3 Escudos Reales · Captura de 1 movimiento · Tablero expandible cada 3:30", new Color(0.96f, 0.80f, 0.28f), 3.4f);
            RetroAudioManager.EnsureInstance()?.PlayShieldActivate();
        }

        private void ApplyOptionalPresetMasterIfConfigured()
        {
            string cardName = GameManager.CurrentMatchConfig?.startingCard?.CardName;
            if (string.IsNullOrEmpty(cardName) && GameManager.Instance != null && GameManager.Instance.ActiveMasterCard != null)
            {
                cardName = GameManager.Instance.ActiveMasterCard.CardName;
            }
            if (string.IsNullOrEmpty(cardName)) return;

            string low = cardName.ToLowerInvariant();
            if (low.Contains("petrosi")) GrantMaster(ArcadeMasterId.Petrosian, showPopup: false);
            else if (low.Contains("tal")) GrantMaster(ArcadeMasterId.Tal, showPopup: false);
            else if (low.Contains("polg")) GrantMaster(ArcadeMasterId.Polgar, showPopup: false);
            else if (low.Contains("kasp")) GrantMaster(ArcadeMasterId.Kasparov, showPopup: false);
        }

        private void SaveInitialCameraState()
        {
            Camera cam = Camera.main;
            if (cam != null && !_savedCameraState)
            {
                _savedCamOrthoSize = cam.orthographicSize;
                _savedCamPosition = cam.transform.position;
                _savedCameraState = true;
            }
        }

        private void HideStandardChessSceneVisuals()
        {
            BoardView stdBoard = FindAnyObjectByType<BoardView>(FindObjectsInactive.Include);
            if (stdBoard != null)
            {
                stdBoard.ClearAll();
                Transform frame = stdBoard.transform.Find("BoardFrameContainer");
                if (frame != null) Destroy(frame.gameObject);
            }

            CardHandView handView = FindAnyObjectByType<CardHandView>(FindObjectsInactive.Include);
            if (handView != null)
            {
                handView.ClearHand();
                handView.gameObject.SetActive(false);
            }

            DifficultySelectorUI diffUI = FindAnyObjectByType<DifficultySelectorUI>(FindObjectsInactive.Include);
            if (diffUI != null) diffUI.SetVisible(false);

            GameHUDView hudView = FindAnyObjectByType<GameHUDView>(FindObjectsInactive.Include);
            if (hudView != null)
            {
                hudView.ClearBanner();
                hudView.SetMoveHistoryVisible(false);
                hudView.ShowOnScreenHUD = false;
            }
        }

        private void EnsureSubsystems()
        {
            if (_boardExpander == null)
            {
                _boardExpander = GetComponent<DynamicBoardExpander>() ?? FindAnyObjectByType<DynamicBoardExpander>();
                if (_boardExpander == null) _boardExpander = gameObject.AddComponent<DynamicBoardExpander>();
            }
            _boardExpander.InitializeExpander(this);

            if (_synergySystem == null)
            {
                _synergySystem = GetComponent<CardSynergySystem>() ?? FindAnyObjectByType<CardSynergySystem>();
                if (_synergySystem == null) _synergySystem = gameObject.AddComponent<CardSynergySystem>();
            }
            _synergySystem.InitializeSystem(this);

            if (_allyManager == null)
            {
                _allyManager = GetComponent<SummonedAllyPieceManager>() ?? FindAnyObjectByType<SummonedAllyPieceManager>();
                if (_allyManager == null) _allyManager = gameObject.AddComponent<SummonedAllyPieceManager>();
            }
            _allyManager.InitializeManager(this);

            if (_enemySpawner == null)
            {
                _enemySpawner = GetComponent<EnemyWaveSpawner>() ?? FindAnyObjectByType<EnemyWaveSpawner>();
                if (_enemySpawner == null) _enemySpawner = gameObject.AddComponent<EnemyWaveSpawner>();
            }
            _enemySpawner.InitializeSpawner(this);

            if (_bossController == null)
            {
                _bossController = GetComponent<MegaBossPieceController>() ?? FindAnyObjectByType<MegaBossPieceController>();
                if (_bossController == null) _bossController = gameObject.AddComponent<MegaBossPieceController>();
            }
            _bossController.InitializeBossController(this);

            if (_levelUpUI == null)
            {
                _levelUpUI = GetComponent<ArcadeLevelUpUI>() ?? FindAnyObjectByType<ArcadeLevelUpUI>();
                if (_levelUpUI == null) _levelUpUI = gameObject.AddComponent<ArcadeLevelUpUI>();
            }
            _levelUpUI.InitializeUI(this);
        }

        private void ResetRunState()
        {
            _royalShields = ArcadeLogic.BaseRoyalShields;
            _ironShieldRechargeTimer = 0f;
            _ironShieldOrbitAngle = 0f;

            _currentLevel = 1;
            _currentExp = 0;
            _expToNextLevel = ArcadeLogic.GetExpRequiredForNextLevel(1);
            _pendingLevelUps = 0;
            _elapsedSeconds = 0f;
            _bossFightSeconds = 0f;
            _moveCooldownDuration = ArcadeLogic.MoveCooldownSeconds;
            _moveCooldownTimer = 0f;
            _invulnerableTimer = 0f;
            _kingHitFlashTimer = 0f;
            _magnetPickupRange = 2.60f;

            _ownedMasters.Clear();
            _runStats = new ArcadeRunStats { CurrentBoardRows = ArcadeLogic.InitialBoardHeight };
        }

        public void CleanupArcadeSession()
        {
            _isSessionActive = false;
            _isPaused = false;
            _isGameOver = false;

            _enemySpawner?.ResetSpawner();
            _allyManager?.ResetAllies();
            _synergySystem?.ResetSynergies();
            _bossController?.ResetBoss();
            _levelUpUI?.CloseModal();
            _boardExpander?.ClearArenaVisuals();

            if (_kingObj != null)
            {
                Destroy(_kingObj);
                _kingObj = null;
            }

            RestoreTimeAndCamera();
        }

        private void RestoreTimeAndCamera()
        {
            Time.timeScale = 1f;
            if (_savedCameraState && Camera.main != null)
            {
                Camera.main.orthographicSize = _savedCamOrthoSize;
                Camera.main.transform.position = _savedCamPosition;
                _savedCameraState = false;
            }
        }

        public Vector3 CoordToWorld(ArcadeCoord c) => CoordToWorld(c.X, c.Y);

        public Vector3 CoordToWorld(int x, int y)
        {
            return _boardExpander != null ? _boardExpander.CoordToWorld(x, y) : new Vector3(x, y, 0f);
        }

        public bool WorldToCoord(Vector3 worldPos, out ArcadeCoord coord)
        {
            if (_boardExpander != null) return _boardExpander.WorldToCoord(worldPos, out coord);
            coord = default;
            return false;
        }

        public void SetTileTelegraph(ArcadeCoord coord, Color color, float duration)
        {
            _boardExpander?.SetTileTelegraph(coord, color, duration);
        }

        public void FlashTileColor(ArcadeCoord coord, Color color, float duration)
        {
            _boardExpander?.SetTileTelegraph(coord, color, duration);
        }

        #region Rey Blanco & Sistema de Escudos Reales + Escudo de Hierro Petrosián

        private void SpawnWhiteKing()
        {
            if (_kingObj != null) Destroy(_kingObj);

            _kingCoord = new ArcadeCoord(ArcadeLogic.BoardWidth / 2 - 1, 2);
            Vector3 startWorld = CoordToWorld(_kingCoord);
            _kingVisualFrom = startWorld;
            _kingVisualTarget = startWorld;
            _kingSlideProgress = 1f;

            _kingObj = new GameObject("PlayerWhiteKing");
            _kingObj.transform.position = startWorld;

            Sprite kingSprite = GetPieceSprite(PieceType.King, PieceColor.White);
            GameObject visualChild = new GameObject("KingSpriteVisual");
            visualChild.transform.SetParent(_kingObj.transform, false);
            _kingSpriteRenderer = visualChild.AddComponent<SpriteRenderer>();
            _kingSpriteRenderer.sortingOrder = 12;

            if (kingSprite != null)
            {
                _kingSpriteRenderer.sprite = kingSprite;
                _kingSpriteRenderer.color = Color.white;
                float uScale = ComputeUniformSpriteScale(kingSprite, 0.86f * tileSize);
                visualChild.transform.localScale = new Vector3(uScale, uScale, 1f);
            }
            else
            {
                Sprite disc = GetProceduralDiscSprite();
                _kingSpriteRenderer.sprite = disc;
                _kingSpriteRenderer.color = new Color(0.97f, 0.97f, 0.92f, 1f);
                float uScale = ComputeUniformSpriteScale(disc, 0.80f * tileSize);
                visualChild.transform.localScale = new Vector3(uScale, uScale, 1f);
            }

            // Sombra bajo el Rey Blanco
            GameObject shadowObj = new GameObject("Shadow");
            shadowObj.transform.SetParent(_kingObj.transform, false);
            shadowObj.transform.localPosition = new Vector3(0.05f, -0.07f, 0.05f);
            shadowObj.transform.localScale = new Vector3(0.75f * tileSize, 0.75f * tileSize, 1f);
            _kingShadowRenderer = shadowObj.AddComponent<SpriteRenderer>();
            _kingShadowRenderer.sprite = GetProceduralDiscSprite();
            _kingShadowRenderer.color = new Color(0f, 0f, 0f, 0.42f);
            _kingShadowRenderer.sortingOrder = 10;

            // 3 Escudos Reales encima del Rey Blanco (sin barra de HP)
            CreateRoyalShieldPipsVisual();

            // Contenedor de órbita del Escudo de Hierro (Tigrán Petrosián)
            GameObject orbitObj = new GameObject("PetrosianIronShieldRoot");
            orbitObj.transform.SetParent(_kingObj.transform, false);
            _ironShieldOrbitRoot = orbitObj.transform;
            RefreshIronShieldVisual();
        }

        private void CreateRoyalShieldPipsVisual()
        {
            GameObject root = new GameObject("KingRoyalShields");
            root.transform.SetParent(_kingObj.transform, false);
            root.transform.localPosition = new Vector3(0f, 0.56f * tileSize, -0.1f);

            Sprite shieldSpr = GetShieldEmblemSprite();
            float spacing = 0.28f * tileSize;

            for (int i = 0; i < ArcadeLogic.BaseRoyalShields; i++)
            {
                GameObject pip = new GameObject($"RoyalShieldPip_{i}");
                pip.transform.SetParent(root.transform, false);
                pip.transform.localPosition = new Vector3((i - 1) * spacing, 0f, 0f);
                SpriteRenderer sr = pip.AddComponent<SpriteRenderer>();
                sr.sprite = shieldSpr;
                sr.sortingOrder = 20;
                float uScale = ComputeUniformSpriteScale(shieldSpr, 0.25f * tileSize);
                pip.transform.localScale = new Vector3(uScale, uScale, 1f);
                _royalShieldPips[i] = sr;
            }

            UpdateRoyalShieldPipsVisual();
        }

        private void UpdateRoyalShieldPipsVisual()
        {
            for (int i = 0; i < _royalShieldPips.Length; i++)
            {
                SpriteRenderer sr = _royalShieldPips[i];
                if (sr == null) continue;

                bool intact = i < _royalShields;
                sr.color = intact
                    ? new Color(0.28f, 0.92f, 1.0f, 1.0f)
                    : new Color(0.22f, 0.24f, 0.28f, 0.35f);
            }
        }

        public void RefreshIronShieldVisual()
        {
            if (_ironShieldOrbitRoot == null) return;

            if (_ironShieldRenderer != null)
            {
                Destroy(_ironShieldRenderer.gameObject);
                _ironShieldRenderer = null;
            }

            if (!HasPetrosianIronShield) return;

            GameObject sObj = new GameObject("PetrosianIronShieldOrb");
            sObj.transform.SetParent(_ironShieldOrbitRoot, false);
            _ironShieldRenderer = sObj.AddComponent<SpriteRenderer>();
            Sprite shieldSpr = GetShieldEmblemSprite();
            _ironShieldRenderer.sprite = shieldSpr;
            _ironShieldRenderer.sortingOrder = 16;
            float uScale = ComputeUniformSpriteScale(shieldSpr, 0.44f * tileSize);
            sObj.transform.localScale = new Vector3(uScale, uScale, 1f);
        }

        private void UpdateIronShieldOrbit(float dt)
        {
            if (!HasPetrosianIronShield || _ironShieldRenderer == null) return;

            if (_ironShieldRechargeTimer > 0f)
            {
                _ironShieldRechargeTimer = Mathf.Max(0f, _ironShieldRechargeTimer - dt);
                if (_ironShieldRechargeTimer <= 0f)
                {
                    SpawnFloatingText(KingWorldPosition + Vector3.up * 0.55f, "¡ESCUDO HIERRO LISTO!", MasterCardDatabase.PetrosianAuraColor, false);
                }
            }

            _ironShieldOrbitAngle += dt * 3.0f;
            float radius = 0.72f * tileSize;
            _ironShieldRenderer.transform.localPosition = new Vector3(Mathf.Cos(_ironShieldOrbitAngle) * radius, Mathf.Sin(_ironShieldOrbitAngle) * radius, -0.04f);

            if (_ironShieldRechargeTimer <= 0f)
            {
                float shimmer = 0.86f + 0.14f * Mathf.Sin(Time.time * 8f);
                _ironShieldRenderer.color = new Color(1.0f * shimmer, 0.84f * shimmer, 0.24f, 1.0f);
            }
            else
            {
                _ironShieldRenderer.color = new Color(0.48f, 0.50f, 0.56f, 0.34f);
            }
        }

        public void HandleEnemyAttackOnKing(PieceType attackerType, bool isBossOrGuardAttack, out bool destroyAttacker)
        {
            destroyAttacker = false;
            if (!_isSessionActive || _isGameOver) return;
            if (_invulnerableTimer > 0f) return;

            // 1. Escudo de Hierro de Tigrán Petrosián (se activa antes de gastar Escudos Reales)
            if (IsPetrosianIronShieldReady)
            {
                _ironShieldRechargeTimer = 18.0f;
                _invulnerableTimer = 0.65f;
                _runStats.BlockedAttacksByShield++;
                destroyAttacker = !isBossOrGuardAttack;

                RetroAudioManager.Instance?.PlayShieldActivate();
                FlashTileColor(_kingCoord, MasterCardDatabase.PetrosianAuraColor, 0.40f);
                SpawnFloatingText(KingWorldPosition + Vector3.up * 0.5f, "¡ESCUDO DE HIERRO!", MasterCardDatabase.PetrosianAuraColor, true);
                return;
            }

            // 2. Gastar 1 de los 3 Escudos Reales del Rey Blanco
            if (_royalShields > 0)
            {
                _royalShields--;
                _invulnerableTimer = 0.75f;
                _kingHitFlashTimer = 0.22f;
                destroyAttacker = !isBossOrGuardAttack;
                UpdateRoyalShieldPipsVisual();

                RetroAudioManager.Instance?.PlayCheckAlert();
                FlashTileColor(_kingCoord, new Color(1f, 0.22f, 0.24f, 0.75f), 0.35f);
                SpawnFloatingText(KingWorldPosition + Vector3.up * 0.45f, $"-1 ESCUDO REAL ({_royalShields})", new Color(1f, 0.28f, 0.32f), true);

                if (_royalShields <= 0)
                {
                    EndRunDefeat();
                }
            }
            else
            {
                EndRunDefeat();
            }
        }

        public void RestoreRoyalShield(int count)
        {
            if (!_isSessionActive || _isGameOver) return;
            int prev = _royalShields;
            _royalShields = Mathf.Min(ArcadeLogic.BaseRoyalShields, _royalShields + count);
            UpdateRoyalShieldPipsVisual();

            if (_royalShields > prev)
            {
                RetroAudioManager.Instance?.PlayShieldActivate();
                SpawnFloatingText(KingWorldPosition + Vector3.up * 0.45f, "+1 ESCUDO REAL", new Color(0.28f, 0.94f, 1.0f), true);
                ShowBanner("¡ESCUDO REAL RESTAURADO!", $"Escudos Reales actuales: {_royalShields}/{ArcadeLogic.BaseRoyalShields}", new Color(0.28f, 0.92f, 1.0f), 2.3f);
            }
        }

        #endregion

        #region Bucle Principal y Control del Rey Blanco

        private void Update()
        {
            if (!_isSessionActive) return;

            if (Camera.main != null && (Screen.width != _lastStyledWidth || Screen.height != _lastStyledHeight))
            {
                _boardExpander?.RecalculateCameraTargets(snapImmediately: false);
            }

            if (!IsPaused && !_isGameOver && _pendingLevelUps > 0)
            {
                _pendingLevelUps--;
                _levelUpUI?.OpenLevelUpDraft(_currentLevel);
                return;
            }

            if (IsPaused || _isGameOver)
            {
                return;
            }

            float dt = Time.deltaTime;

            // Reloj de Supervivencia 15:00 (900s) + Expansiones exactas cada 210s (3:30)
            if (!_bossTriggered)
            {
                _elapsedSeconds += dt;
                _runStats.ElapsedSeconds = Mathf.Min(_elapsedSeconds, ArcadeLogic.SurvivalDurationSeconds);

                if (_elapsedSeconds >= ArcadeLogic.SurvivalDurationSeconds)
                {
                    _elapsedSeconds = ArcadeLogic.SurvivalDurationSeconds;
                    TriggerMegaBossPhase();
                }
            }
            else if (_bossController != null && _bossController.IsBossActive)
            {
                _bossFightSeconds += dt;
                _runStats.BossFightSeconds = _bossFightSeconds;
            }

            _boardExpander?.TickExpansion(_elapsedSeconds, dt);

            if (_bannerTimer > 0f) _bannerTimer -= dt;
            if (_moveCooldownTimer > 0f) _moveCooldownTimer = Mathf.Max(0f, _moveCooldownTimer - dt);
            if (_invulnerableTimer > 0f) _invulnerableTimer = Mathf.Max(0f, _invulnerableTimer - dt);

            UpdateKingVisualColorsAndSlide(dt);
            UpdateIronShieldOrbit(dt);
            HandlePlayerMovementInput();
        }

        private void UpdateKingVisualColorsAndSlide(float dt)
        {
            if (_kingHitFlashTimer > 0f)
            {
                _kingHitFlashTimer = Mathf.Max(0f, _kingHitFlashTimer - dt);
                if (_kingSpriteRenderer != null)
                {
                    _kingSpriteRenderer.color = _kingHitFlashTimer > 0f ? new Color(1f, 0.30f, 0.30f, 1f) : Color.white;
                }
            }
            else if (_invulnerableTimer > 0f && _kingSpriteRenderer != null)
            {
                float blink = Mathf.PingPong(Time.time * 18f, 1f) > 0.4f ? 1f : 0.45f;
                _kingSpriteRenderer.color = new Color(1f, 1f, 1f, blink);
            }
            else if (_kingSpriteRenderer != null)
            {
                _kingSpriteRenderer.color = Color.white;
            }

            if (_kingObj == null) return;
            if (_kingSlideProgress < 1f)
            {
                _kingSlideProgress = Mathf.Clamp01(_kingSlideProgress + dt * 9.5f);
                float ease = 1f - Mathf.Pow(1f - _kingSlideProgress, 3f);
                Vector3 pos = Vector3.Lerp(_kingVisualFrom, _kingVisualTarget, ease);
                float hop = Mathf.Sin(ease * Mathf.PI) * 0.14f * tileSize;
                _kingObj.transform.position = pos + new Vector3(0f, hop, 0f);
            }
            else
            {
                _kingObj.transform.position = _kingVisualTarget;
            }
        }

        [ContextMenu("Force Jump To Minute 15:00 Boss")]
        public void ForceJumpToBossPhase()
        {
            if (!_isSessionActive) return;
            _elapsedSeconds = ArcadeLogic.SurvivalDurationSeconds;
            _boardExpander?.TickExpansion(_elapsedSeconds, 0.02f);
            TriggerMegaBossPhase();
        }

        private void TriggerMegaBossPhase()
        {
            if (_bossTriggered) return;
            _bossTriggered = true;

            _enemySpawner?.ClearAllMinorEnemiesKeepGuards();
            RetroAudioManager.Instance?.PlayCheckAlert();

            ShowBanner(
                "⚠ ¡MINUTO 15:00! EL COLOSO REAL ⚠",
                "Rompe sus 3 Sellos de Jaque derrotando a la Guardia Real de élite",
                new Color(0.98f, 0.24f, 0.28f),
                4.2f);

            _bossController?.StartFinalBossBattle();
            _boardExpander?.RecalculateCameraTargets(snapImmediately: false);
        }

        private void HandlePlayerMovementInput()
        {
            ReadPointerSample(out bool downThisFrame, out bool held, out bool upThisFrame, out Vector2 screenPos);

            if (downThisFrame)
            {
                if (!IsPointerOverUi(screenPos))
                {
                    _isPointerTracking = true;
                    _pointerDownScreenPos = screenPos;
                    _pointerDownTime = Time.unscaledTime;
                    _swipeConsumedThisDrag = false;
                    _pointerDownCoordValid = TryScreenToCoord(screenPos, out _pointerDownCoord);
                }
            }

            if (_isPointerTracking && held && !_swipeConsumedThisDrag)
            {
                Vector2 delta = screenPos - _pointerDownScreenPos;
                float swipeThreshold = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) * 0.042f, 26f, 52f);
                ArcadeCoord dir = ArcadeLogic.ComputeSwipeStepDirection(delta.x, delta.y, swipeThreshold);
                if (dir.X != 0 || dir.Y != 0)
                {
                    _swipeConsumedThisDrag = true;
                    TryMoveKingByStep(dir.X, dir.Y);
                }
            }

            if (_isPointerTracking && upThisFrame)
            {
                if (!_swipeConsumedThisDrag && (Time.unscaledTime - _pointerDownTime) < 0.45f)
                {
                    bool releaseValid = TryScreenToCoord(screenPos, out ArcadeCoord clickedCoord);
                    if (releaseValid || _pointerDownCoordValid)
                    {
                        ArcadeCoord target = releaseValid ? clickedCoord : _pointerDownCoord;
                        int boardH = _boardExpander != null ? _boardExpander.CurrentRows : ArcadeLogic.InitialBoardHeight;
                        if (target.IsValidIn(boardH) && target != _kingCoord)
                        {
                            ArcadeCoord step = ArcadeLogic.ComputeKingStepDirection(_kingCoord, target);
                            TryMoveKingByStep(step.X, step.Y);
                        }
                    }
                }
                _isPointerTracking = false;
            }

            if (_moveCooldownTimer <= 0f)
            {
                ReadKeyboardDirection(out int kx, out int ky);
                if (kx != 0 || ky != 0)
                {
                    TryMoveKingByStep(kx, ky);
                }
            }
        }

        private bool TryMoveKingByStep(int dx, int dy)
        {
            if (_moveCooldownTimer > 0f) return false;
            if (dx == 0 && dy == 0) return false;

            int boardH = _boardExpander != null ? _boardExpander.CurrentRows : ArcadeLogic.InitialBoardHeight;
            ArcadeCoord next = new ArcadeCoord(_kingCoord.X + Mathf.Clamp(dx, -1, 1), _kingCoord.Y + Mathf.Clamp(dy, -1, 1));
            if (!next.IsValidIn(boardH)) return false;

            // Verificar colisión contra el cuerpo 2x2 del Jefe
            if (_bossController != null && _bossController.IsBossActive && _bossController.IsTileOccupiedByBoss(next.X, next.Y))
            {
                if (_bossController.IsVulnerableToDirectCapture)
                {
                    _kingCoord = next;
                    _kingVisualFrom = _kingObj != null ? _kingObj.transform.position : CoordToWorld(next);
                    _kingVisualTarget = CoordToWorld(next);
                    _kingSlideProgress = 0f;
                    _bossController.CaptureBossDefinitiveCheckmate("Rey Blanco");
                    return true;
                }
                else
                {
                    FlashTileColor(next, new Color(1f, 0.25f, 0.25f, 0.70f), 0.28f);
                    SpawnFloatingText(CoordToWorld(next) + Vector3.up * 0.45f, "¡SELLOS ACTIVOS!", new Color(1f, 0.45f, 0.25f), false);
                    return false;
                }
            }

            // Captura instantánea de 1 solo movimiento si hay una pieza enemiga en la casilla objetivo
            bool capturedEnemy = _enemySpawner != null && _enemySpawner.CaptureEnemyAt(next, capturedByAlly: false);

            // Si hay una pieza aliada ocupando la casilla destino, intercambiar posiciones limpiamente para evitar solapamientos
            if (_allyManager != null && _allyManager.IsOccupiedByActiveAlly(next))
            {
                _allyManager.SwapAllyFromSquare(next, _kingCoord);
            }

            _kingCoord = next;
            _kingVisualFrom = _kingObj != null ? _kingObj.transform.position : CoordToWorld(next);
            _kingVisualTarget = CoordToWorld(next);
            _kingSlideProgress = 0f;
            _moveCooldownTimer = _moveCooldownDuration;

            FlashTileColor(next, new Color(0.96f, 0.88f, 0.35f, 0.45f), 0.16f);
            if (!capturedEnemy)
            {
                RetroAudioManager.Instance?.PlayPieceMove();
            }
            return true;
        }

        private bool TryScreenToCoord(Vector2 screenPos, out ArcadeCoord coord)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                coord = default;
                return false;
            }

            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Mathf.Abs(cam.transform.position.z)));
            return WorldToCoord(world, out coord);
        }

        private bool IsPointerOverUi(Vector2 screenPos)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return true;
            }
            Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
            return _topHudScreenRect.Contains(guiPos) || _bottomStripScreenRect.Contains(guiPos);
        }

        private void ReadPointerSample(out bool down, out bool held, out bool up, out Vector2 pos)
        {
            down = false;
            held = false;
            up = false;
            pos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Touchscreen.current != null)
            {
                var touch = UnityEngine.InputSystem.Touchscreen.current.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                {
                    down = true;
                    pos = touch.position.ReadValue();
                    held = true;
                    return;
                }
                if (touch.press.isPressed)
                {
                    held = true;
                    pos = touch.position.ReadValue();
                    return;
                }
                if (touch.press.wasReleasedThisFrame)
                {
                    up = true;
                    pos = touch.position.ReadValue();
                    return;
                }
            }

            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                pos = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame) down = true;
                if (mouse.leftButton.isPressed) held = true;
                if (mouse.leftButton.wasReleasedThisFrame) up = true;
                if (down || held || up) return;
            }
#endif

            try
            {
                if (Input.touchCount > 0)
                {
                    Touch t = Input.GetTouch(0);
                    pos = t.position;
                    down = t.phase == TouchPhase.Began;
                    held = t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
                    up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                    return;
                }

                pos = Input.mousePosition;
                down = Input.GetMouseButtonDown(0);
                held = Input.GetMouseButton(0);
                up = Input.GetMouseButtonUp(0);
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void ReadKeyboardDirection(out int dx, out int dy)
        {
            dx = 0;
            dy = 0;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) dx -= 1;
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) dx += 1;
                if (kb.upArrowKey.isPressed || kb.wKey.isPressed) dy += 1;
                if (kb.downArrowKey.isPressed || kb.sKey.isPressed) dy -= 1;
                if (kb.qKey.isPressed) { dx = -1; dy = 1; }
                if (kb.eKey.isPressed) { dx = 1; dy = 1; }
                if (kb.zKey.isPressed) { dx = -1; dy = -1; }
                if (kb.cKey.isPressed) { dx = 1; dy = -1; }
                if (dx != 0 || dy != 0) return;
            }
#endif

            try
            {
                if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) dx -= 1;
                if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) dx += 1;
                if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) dy += 1;
                if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) dy -= 1;
            }
            catch (InvalidOperationException)
            {
            }
        }

        #endregion

        #region Progresión EXP, Cartas de Gran Maestro y Estado Final

        public void AddExp(int amount)
        {
            if (!_isSessionActive || _isGameOver || amount <= 0) return;

            _currentExp += amount;
            _runStats.TotalExpCollected += amount;
            SpawnFloatingText(KingWorldPosition + new Vector3(0.22f, 0.35f, 0f), $"+{amount} EXP", new Color(0.32f, 0.90f, 1f), false);

            while (_currentExp >= _expToNextLevel)
            {
                _currentExp -= _expToNextLevel;
                _currentLevel++;
                _runStats.FinalLevel = _currentLevel;
                _expToNextLevel = ArcadeLogic.GetExpRequiredForNextLevel(_currentLevel);
                _pendingLevelUps++;
            }

            if (_pendingLevelUps > 0 && _levelUpUI != null && !_levelUpUI.IsModalOpen)
            {
                _pendingLevelUps--;
                _levelUpUI.OpenLevelUpDraft(_currentLevel);
            }
        }

        public void RecordEnemyKill(PieceType pieceType, bool isGuard)
        {
            _runStats.RecordKill(pieceType, isGuard);
        }

        public bool HasMaster(ArcadeMasterId master) => _ownedMasters.Contains(master);

        public void GrantMaster(ArcadeMasterId master, bool showPopup = true)
        {
            if (!_ownedMasters.Add(master)) return;

            if (!_runStats.OwnedMasters.Contains(master))
            {
                _runStats.OwnedMasters.Add(master);
            }

            if (master == ArcadeMasterId.Petrosian)
            {
                _ironShieldRechargeTimer = 0f;
                RefreshIronShieldVisual();
            }

            _synergySystem?.EvaluateAndSyncSynergies(showNotifications: showPopup);

            if (showPopup)
            {
                switch (master)
                {
                    case ArcadeMasterId.Petrosian:
                        ShowBanner("¡TIGRÁN PETROSIÁN!", "Escudo de Hierro en Rey + Escudo Dorado en Peones/Torres", MasterCardDatabase.PetrosianAuraColor, 2.8f);
                        break;
                    case ArcadeMasterId.Tal:
                        ShowBanner("¡MIJAÍL TAL!", "Sinergia Caballo: Doble salto en L consecutivo al capturar", MasterCardDatabase.TalAuraColor, 2.8f);
                        break;
                    case ArcadeMasterId.Polgar:
                        ShowBanner("¡JUDIT POLGÁR!", "Sinergia Torre/Alfil/Dama: Barrido perforante en línea y diagonal", MasterCardDatabase.PolgarAuraColor, 2.8f);
                        break;
                    case ArcadeMasterId.Kasparov:
                        ShowBanner("¡GARRY KASPÁROV!", "Sobrecarga Blitz: Duplica (x2) la velocidad de turno aliada", MasterCardDatabase.KasparovAuraColor, 2.8f);
                        break;
                }
            }
        }

        public void UpgradeMagnetRange(float bonus)
        {
            _magnetPickupRange = Mathf.Min(9.5f, _magnetPickupRange + bonus);
            ShowBanner("¡IMÁN TÁCTICO!", $"Rango de recogida EXP ampliado ({_magnetPickupRange:F1} casillas)", new Color(0.32f, 0.88f, 1f), 2.1f);
        }

        public void HandleBossDefeated()
        {
            _runStats.BossDefeated = true;
            _runStats.FinalLevel = _currentLevel;
            _isGameOver = true;
            Time.timeScale = 0f;
            RetroAudioManager.Instance?.PlayRewardSound();
        }

        private void EndRunDefeat()
        {
            _runStats.BossDefeated = false;
            _runStats.FinalLevel = _currentLevel;
            _isGameOver = true;
            Time.timeScale = 0f;
        }

        public void ShowBanner(string title, string subtitle, Color color, float duration = 2.6f)
        {
            _bannerTitle = title;
            _bannerSubtitle = subtitle;
            _bannerColor = color;
            _bannerTimer = duration;
        }

        #endregion

        #region Sprites & Textos Flotantes

        public Sprite GetPieceSprite(PieceType type, PieceColor color)
        {
            if (PieceSkinManager.Instance != null && PieceSkinManager.Instance.ActiveTheme != null)
            {
                Sprite fromSkin = PieceSkinManager.Instance.ActiveTheme.GetSprite(type, color);
                if (fromSkin != null) return fromSkin;
            }

            string cName = color == PieceColor.White ? "white" : "black";
            string pName = type.ToString().ToLowerInvariant();
            return Resources.Load<Sprite>($"Pieces/{cName}_{pName}");
        }

        public static float ComputeUniformSpriteScale(Sprite sprite, float desiredWorldSpan)
        {
            if (sprite == null) return desiredWorldSpan;
            float maxBound = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            if (maxBound <= 0.0001f) return desiredWorldSpan;
            return desiredWorldSpan / maxBound;
        }

        public Sprite GetProceduralSquareSprite()
        {
            if (_whiteSquareSprite != null) return _whiteSquareSprite;
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            _whiteSquareSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            return _whiteSquareSprite;
        }

        public Sprite GetProceduralDiscSprite()
        {
            if (_softDiscSprite != null) return _softDiscSprite;
            const int sz = 32;
            Texture2D tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            Color[] cols = new Color[sz * sz];
            float r = (sz - 1) * 0.5f;
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float dx = (x - r) / r;
                    float dy = (y - r) / r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    cols[y * sz + x] = d <= 0.92f ? Color.white : (d <= 1.0f ? new Color(1f, 1f, 1f, (1f - d) / 0.08f) : Color.clear);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            _softDiscSprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz);
            return _softDiscSprite;
        }

        public Sprite GetShieldEmblemSprite()
        {
            if (_shieldEmblemSprite != null) return _shieldEmblemSprite;
            const int sz = 24;
            Texture2D tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color[] cols = new Color[sz * sz];
            for (int y = 0; y < sz; y++)
            {
                float ny = (y / (float)(sz - 1)) * 2f - 1f;
                for (int x = 0; x < sz; x++)
                {
                    float nx = Mathf.Abs((x / (float)(sz - 1)) * 2f - 1f);
                    float maxW = ny >= -0.1f ? 0.85f : Mathf.Lerp(0.85f, 0.05f, (-0.1f - ny) / 0.9f);
                    bool inside = nx <= maxW && ny <= 0.88f && ny >= -0.92f;
                    bool border = inside && (nx > maxW - 0.14f || ny > 0.72f || ny < -0.76f);
                    cols[y * sz + x] = !inside ? Color.clear : (border ? new Color(1f, 0.95f, 0.65f, 1f) : new Color(0.92f, 0.72f, 0.14f, 1f));
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            _shieldEmblemSprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz);
            return _shieldEmblemSprite;
        }

        public void SpawnFloatingText(Vector3 worldPos, string text, Color color, bool large = false)
        {
            if (!gameObject.activeInHierarchy || string.IsNullOrEmpty(text)) return;
            StartCoroutine(FloatingTextRoutine(worldPos, text, color, large));
        }

        private IEnumerator FloatingTextRoutine(Vector3 worldPos, string text, Color color, bool large)
        {
            if (_cachedPopupFont == null) _cachedPopupFont = ChessFontProvider.SerifBold;

            Vector3 jitter = new Vector3(UnityEngine.Random.Range(-0.16f, 0.16f), UnityEngine.Random.Range(-0.04f, 0.10f), -0.45f);
            Vector3 startPos = worldPos + jitter;

            GameObject obj = new GameObject("ArcadeCapturePopup");
            obj.transform.position = startPos;

            TextMesh mainMesh = obj.AddComponent<TextMesh>();
            mainMesh.text = text;
            mainMesh.alignment = TextAlignment.Center;
            mainMesh.anchor = TextAnchor.MiddleCenter;
            mainMesh.fontSize = large ? 56 : 46;
            mainMesh.fontStyle = FontStyle.Bold;
            mainMesh.characterSize = (large ? 0.068f : 0.052f) * tileSize;
            mainMesh.color = color;

            MeshRenderer mainMR = obj.GetComponent<MeshRenderer>();
            if (_cachedPopupFont != null)
            {
                mainMesh.font = _cachedPopupFont;
                if (_cachedPopupFont.material != null && mainMR != null) mainMR.sharedMaterial = _cachedPopupFont.material;
            }
            if (mainMR != null) mainMR.sortingOrder = 55;

            GameObject shadowObj = new GameObject("Shadow");
            shadowObj.transform.SetParent(obj.transform, false);
            shadowObj.transform.localPosition = new Vector3(0.032f, -0.032f, 0.02f);

            TextMesh shMesh = shadowObj.AddComponent<TextMesh>();
            shMesh.text = text;
            shMesh.alignment = TextAlignment.Center;
            shMesh.anchor = TextAnchor.MiddleCenter;
            shMesh.fontSize = mainMesh.fontSize;
            shMesh.fontStyle = FontStyle.Bold;
            shMesh.characterSize = mainMesh.characterSize;
            shMesh.color = new Color(0.04f, 0.04f, 0.08f, 0.92f);

            MeshRenderer shMR = shadowObj.GetComponent<MeshRenderer>();
            if (_cachedPopupFont != null)
            {
                shMesh.font = _cachedPopupFont;
                if (_cachedPopupFont.material != null && shMR != null) shMR.sharedMaterial = _cachedPopupFont.material;
            }
            if (shMR != null) shMR.sortingOrder = 54;

            float dur = large ? 0.85f : 0.65f;
            float elapsed = 0f;
            Vector3 endPos = startPos + new Vector3(0f, 0.70f * tileSize, 0f);

            while (elapsed < dur)
            {
                if (obj == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float pop = t < 0.18f ? Mathf.Lerp(1.32f, 1.0f, t / 0.18f) : 1.0f;
                obj.transform.localScale = Vector3.one * pop;
                obj.transform.position = Vector3.Lerp(startPos, endPos, 1f - Mathf.Pow(1f - t, 3f));

                float alpha = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) / 0.5f);
                mainMesh.color = new Color(color.r, color.g, color.b, alpha);
                shMesh.color = new Color(0.04f, 0.04f, 0.08f, 0.92f * alpha);
                yield return null;
            }

            if (obj != null) Destroy(obj);
        }

        #endregion

        #region HUD Escudos / Reloj / Tablero / Jefe por Sellos

        private void OnGUI()
        {
            if (!_isSessionActive) return;

            InitGuiStylesIfNeeded();

            float w = Screen.width;
            float h = Screen.height;
            float s = ChessFontProvider.GetResponsiveScale();
            float safeTop = Mathf.Max(0f, h - Screen.safeArea.yMax);

            // 1. HUD Superior: 3 Escudos Reales + Escudo de Hierro | Reloj 15:00 & Tablero 8xN | Pausa | Barra EXP
            float topBarH = 96f * s;
            Rect topHudRect = new Rect(10f * s, safeTop + 6f * s, w - 20f * s, topBarH);
            _topHudScreenRect = topHudRect;
            GUI.Box(topHudRect, GUIContent.none, new GUIStyle { normal = { background = _darkHudBgTex } });

            float leftW = Mathf.Min(topHudRect.width * 0.36f, 235f * s);
            string shieldPips = "";
            for (int i = 0; i < ArcadeLogic.BaseRoyalShields; i++)
            {
                shieldPips += i < _royalShields ? "🛡" : "✖";
            }
            GUI.color = _royalShields > 1 ? new Color(0.32f, 0.94f, 1f) : new Color(1f, 0.34f, 0.36f);
            GUI.Label(new Rect(topHudRect.x + 12f * s, topHudRect.y + 6f * s, leftW, 24f * s),
                $"ESCUDOS: {shieldPips} ({_royalShields}/3)", _headerLabelStyle);
            GUI.color = Color.white;

            if (HasPetrosianIronShield)
            {
                string ironStr = IsPetrosianIronShieldReady ? "★ HIERRO LISTO" : $"★ HIERRO: {_ironShieldRechargeTimer:F0}s";
                GUI.color = IsPetrosianIronShieldReady ? MasterCardDatabase.PetrosianAuraColor : new Color(0.65f, 0.70f, 0.78f);
                GUI.Label(new Rect(topHudRect.x + 12f * s, topHudRect.y + 30f * s, leftW, 20f * s), ironStr, _smallLabelStyle);
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(0.80f, 0.85f, 0.94f);
                GUI.Label(new Rect(topHudRect.x + 12f * s, topHudRect.y + 30f * s, leftW, 20f * s),
                    $"⚔ Capturas: {_runStats.DestroyedPieces}", _smallLabelStyle);
                GUI.color = Color.white;
            }

            // Centro: Reloj + Tablero actual (8x8 .. 8x20)
            float centerW = Mathf.Min(topHudRect.width * 0.44f, 300f * s);
            float centerX = topHudRect.x + (topHudRect.width - centerW) * 0.5f;
            string clockStr = ArcadeLogic.FormatClockMMSS(_elapsedSeconds);
            GUI.color = _bossTriggered ? new Color(1f, 0.30f, 0.30f) : Color.white;
            GUI.Label(new Rect(centerX, topHudRect.y + 2f * s, centerW, 34f * s), clockStr, _clockStyle);
            GUI.color = Color.white;

            int boardRows = _boardExpander != null ? _boardExpander.CurrentRows : ArcadeLogic.InitialBoardHeight;
            GUI.color = new Color(0.95f, 0.84f, 0.34f);
            GUI.Label(new Rect(centerX, topHudRect.y + 34f * s, centerW, 20f * s),
                $"TABLERO {ArcadeLogic.BoardWidth}x{boardRows} · {ArcadeLogic.GetWavePhaseTitle(_elapsedSeconds, _bossTriggered)}",
                new GUIStyle(_smallLabelStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
            GUI.color = Color.white;

            // Derecha: Pausa
            float pauseSize = 40f * s;
            Rect pauseRect = new Rect(topHudRect.xMax - pauseSize - 10f * s, topHudRect.y + 8f * s, pauseSize, pauseSize);
            if (!_isGameOver && (_levelUpUI == null || !_levelUpUI.IsModalOpen))
            {
                if (GUI.Button(pauseRect, _isPaused ? "▶" : "❚❚", _secondaryBtnStyle))
                {
                    TogglePause();
                }
            }

            // Barra EXP
            float expBarX = topHudRect.x + 12f * s;
            float expBarY = topHudRect.y + 62f * s;
            float expBarW = topHudRect.width - 24f * s;
            float expBarH = 24f * s;
            GUI.DrawTexture(new Rect(expBarX, expBarY, expBarW, expBarH), _trackDarkTex);
            float expRatio = Mathf.Clamp01((float)_currentExp / Mathf.Max(1, _expToNextLevel));
            GUI.DrawTexture(new Rect(expBarX, expBarY, expBarW * expRatio, expBarH), _cyanFillTex);
            GUI.Label(new Rect(expBarX, expBarY, expBarW, expBarH),
                $"NIVEL {_currentLevel}   ·   EXP {_currentExp} / {_expToNextLevel}   ·   CAPTURAS {_runStats.DestroyedPieces}",
                new GUIStyle(_smallLabelStyle) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }, fontStyle = FontStyle.Bold });

            // 2. Indicador del Jefe Final: 3 SELLOS DE JAQUE (sin barra de HP)
            float curY = topHudRect.yMax + 6f * s;
            if (_bossController != null && _bossController.IsBossActive)
            {
                float bossH = 44f * s;
                Rect bossRect = new Rect(topHudRect.x + 8f * s, curY, topHudRect.width - 16f * s, bossH);
                GUI.Box(bossRect, GUIContent.none, new GUIStyle { normal = { background = _darkHudBgTex } });

                bool vuln = _bossController.IsVulnerableToDirectCapture;
                int seals = _bossController.RemainingCheckSeals;
                string sealText = vuln
                    ? "👑 EL COLOSO REAL (2x2) — ★ ¡VULNERABLE! CAPTURA DE UN GOLPE ★"
                    : $"👑 EL COLOSO REAL — SELLOS DE JAQUE RESTANTES: {seals}/3  (Elimina a la Guardia Real)";

                GUI.color = vuln ? new Color(1f, 0.86f, 0.22f) : new Color(1f, 0.34f, 0.36f);
                GUI.Label(new Rect(bossRect.x + 8f * s, bossRect.y + 6f * s, bossRect.width - 16f * s, bossH - 12f * s),
                    sealText,
                    new GUIStyle(_smallLabelStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(13 * s) });
                GUI.color = Color.white;
                curY = bossRect.yMax + 6f * s;
            }

            // 3. Banner flotante
            if (_bannerTimer > 0f && !string.IsNullOrEmpty(_bannerTitle))
            {
                float bW = Mathf.Min(w * 0.90f, 540f * s);
                float bH = 60f * s;
                Rect bRect = new Rect((w - bW) * 0.5f, curY + 6f * s, bW, bH);
                GUI.Box(bRect, GUIContent.none, new GUIStyle { normal = { background = _darkHudBgTex } });
                GUI.color = _bannerColor;
                GUI.Label(new Rect(bRect.x + 8f, bRect.y + 6f * s, bRect.width - 16f, 26f * s), _bannerTitle, _bannerTitleStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(bRect.x + 8f, bRect.y + 32f * s, bRect.width - 16f, 22f * s), _bannerSubtitle, _bannerSubStyle);
            }

            // 4. Barra Inferior: Aliados Invocados + Sinergias Activas
            DrawBottomAlliesAndSynergiesHud(w, h, s);

            if (_isGameOver)
            {
                DrawEndRunStatsModal(w, h, s);
            }
            else if (_isPaused)
            {
                DrawPauseModal(w, h, s);
            }
        }

        private void DrawBottomAlliesAndSynergiesHud(float w, float h, float s)
        {
            float stripH = 68f * s;
            Rect stripRect = new Rect(10f * s, h - stripH - 8f * s, w - 20f * s, stripH);
            _bottomStripScreenRect = stripRect;
            GUI.Box(stripRect, GUIContent.none, new GUIStyle { normal = { background = _darkHudBgTex } });

            int pawns = _allyManager != null ? _allyManager.CountAlliesOfType(PieceType.Pawn) : 0;
            int knights = _allyManager != null ? _allyManager.CountAlliesOfType(PieceType.Knight) : 0;
            int bishops = _allyManager != null ? _allyManager.CountAlliesOfType(PieceType.Bishop) : 0;
            int rooks = _allyManager != null ? _allyManager.CountAlliesOfType(PieceType.Rook) : 0;
            int queens = _allyManager != null ? _allyManager.CountAlliesOfType(PieceType.Queen) : 0;

            string squadText = $"ALIADOS:  ♟{pawns}   ♞{knights}   ♝{bishops}   ♜{rooks}   ♛{queens}";
            GUI.color = new Color(0.42f, 0.96f, 0.78f);
            GUI.Label(new Rect(stripRect.x + 12f * s, stripRect.y + 6f * s, stripRect.width - 24f * s, 22f * s), squadText, _weaponNameStyle);
            GUI.color = Color.white;

            CardSynergyState syn = _synergySystem != null ? _synergySystem.CurrentState : default;
            string synLine =
                $"{(syn.PiercingCaptureSynergy ? "★" : "○")} Barrido Polgár   ·   " +
                $"{(syn.ChainLeapSynergy ? "★" : "○")} Doble Salto Tal   ·   " +
                $"{(syn.IronPhalanxSynergy ? "★" : "○")} Falange Petrosián   ·   " +
                $"{(syn.BlitzOverdriveSynergy ? "★" : "○")} Blitz Kaspárov";

            GUI.color = syn.ActiveCount > 0 ? new Color(1f, 0.85f, 0.28f) : new Color(0.68f, 0.74f, 0.82f);
            GUI.Label(new Rect(stripRect.x + 8f * s, stripRect.y + 32f * s, stripRect.width - 16f * s, 26f * s),
                synLine,
                new GUIStyle(_smallLabelStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
            GUI.color = Color.white;
        }

        private void DrawPauseModal(float w, float h, float s)
        {
            GUI.DrawTexture(new Rect(0, 0, w, h), _modalDimTex);

            float pW = Mathf.Min(w * 0.86f, 380f * s);
            float pH = 310f * s;
            Rect r = new Rect((w - pW) * 0.5f, (h - pH) * 0.5f, pW, pH);
            GUI.Box(r, GUIContent.none, new GUIStyle { normal = { background = _darkHudBgTex } });

            GUILayout.BeginArea(new Rect(r.x + 18f * s, r.y + 18f * s, r.width - 36f * s, r.height - 36f * s));
            GUILayout.Label("ASEDIO EN PAUSA", _modalTitleStyle);
            GUILayout.Space(10f * s);
            int rows = _boardExpander != null ? _boardExpander.CurrentRows : ArcadeLogic.InitialBoardHeight;
            GUILayout.Label($"Tiempo: {ArcadeLogic.FormatClockMMSS(_elapsedSeconds)} · Tablero {ArcadeLogic.BoardWidth}x{rows}\nCapturas: {_runStats.DestroyedPieces} · Escudos: {_royalShields}/3", _modalBodyStyle);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("▶  REANUDAR ASEDIO", _primaryBtnStyle, GUILayout.Height(44f * s))) TogglePause();
            GUILayout.Space(6f * s);
            if (GUILayout.Button("★  TUTORIALES / GUÍA ARCADE", _secondaryBtnStyle, GUILayout.Height(38f * s)))
            {
                OnboardingManager.EnsureInstance().OpenTutorialHub(TutorialHubCategory.Arcade);
            }
            GUILayout.Space(6f * s);
            if (GUILayout.Button("↻  REINICIAR ASEDIO", _secondaryBtnStyle, GUILayout.Height(38f * s))) StartArcadeRun();
            GUILayout.Space(6f * s);
            if (GUILayout.Button("⌂  MENÚ PRINCIPAL", _dangerBtnStyle, GUILayout.Height(36f * s))) ReturnToMainMenu();
            GUILayout.EndArea();
        }

        private void DrawEndRunStatsModal(float w, float h, float s)
        {
            GUI.DrawTexture(new Rect(0, 0, w, h), _modalDimTex);

            bool victory = _runStats.BossDefeated;
            float pW = Mathf.Min(w * 0.90f, 470f * s);
            float pH = Mathf.Min(h * 0.84f, 480f * s);
            Rect r = new Rect((w - pW) * 0.5f, (h - pH) * 0.5f, pW, pH);
            GUI.Box(r, GUIContent.none, new GUIStyle { normal = { background = _darkHudBgTex } });

            GUILayout.BeginArea(new Rect(r.x + 22f * s, r.y + 18f * s, r.width - 44f * s, r.height - 36f * s));

            GUI.color = victory ? new Color(1f, 0.85f, 0.25f) : new Color(0.98f, 0.30f, 0.34f);
            GUILayout.Label(victory ? "¡VICTORIA EN EL ASEDIO!" : "REY BLANCO CAPTURADO", _modalTitleStyle);
            GUI.color = Color.white;

            GUILayout.Space(4f * s);
            GUILayout.Label(victory
                ? "Jaque Mate Definitivo sobre El Coloso Real. Tus piezas dominaron el tablero."
                : $"Tus Escudos Reales se agotaron al minuto {ArcadeLogic.FormatClockMMSS(_elapsedSeconds)}. ¡Vuelve al asedio!",
                _bannerSubStyle);

            GUILayout.Space(12f * s);

            string synList = _runStats.UnlockedSynergyNames.Count > 0
                ? string.Join(", ", _runStats.UnlockedSynergyNames)
                : "Ninguna activa todavía";

            string mastersList = _runStats.OwnedMasters.Count > 0
                ? string.Join(", ", ConvertAllMasters(_runStats.OwnedMasters))
                : "Sin Grandes Maestros aún";

            string statsBody =
                $"<b>• Tiempo Supervivencia:</b> {ArcadeLogic.FormatClockMMSS(_elapsedSeconds)}  (Tablero {ArcadeLogic.BoardWidth}x{_runStats.CurrentBoardRows})\n" +
                $"<b>• Nivel Alcanzado:</b> Nivel {_runStats.FinalLevel}  (EXP: {_runStats.TotalExpCollected})\n" +
                $"<b>• Piezas Capturadas:</b> {_runStats.DestroyedPieces} totales  " +
                $"(♟{_runStats.PawnsKilled} · ♞{_runStats.KnightsKilled} · ♝{_runStats.BishopsKilled} · ♜{_runStats.RooksKilled} · ♛{_runStats.QueensKilled})\n" +
                $"<b>• Bloqueos por Escudos (Petrosián):</b> {_runStats.BlockedAttacksByShield}\n" +
                $"<b>• Reconstrucciones Aliadas:</b> {_runStats.AllyReconstructions}\n" +
                $"<b>• Sinergias Desatadas:</b> {synList}\n" +
                $"<b>• Grandes Maestros:</b> {mastersList}";

            GUILayout.Label(statsBody, _modalBodyStyle);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("⚔  JUGAR DE NUEVO", _primaryBtnStyle, GUILayout.Height(46f * s))) StartArcadeRun();
            GUILayout.Space(8f * s);
            if (GUILayout.Button("⌂  VOLVER AL MENÚ PRINCIPAL", _secondaryBtnStyle, GUILayout.Height(40f * s))) ReturnToMainMenu();
            GUILayout.EndArea();
        }

        private string[] ConvertAllMasters(List<ArcadeMasterId> list)
        {
            string[] arr = new string[list.Count];
            for (int i = 0; i < list.Count; i++) arr[i] = ArcadeLogic.GetMasterDisplayName(list[i]);
            return arr;
        }

        public void TogglePause()
        {
            if (_isGameOver) return;
            _isPaused = !_isPaused;
            Time.timeScale = _isPaused ? 0f : 1f;
            RetroAudioManager.Instance?.PlayTap();
        }

        public void ReturnToMainMenu()
        {
            CleanupArcadeSession();
            GameManager.SelectedGameMode = GameMode.ClassicRoguelike;
            if (GameManager.Instance != null) GameManager.Instance.ReturnToMenu();
        }

        private void InitGuiStylesIfNeeded()
        {
            if (_clockStyle != null && Screen.width == _lastStyledWidth && Screen.height == _lastStyledHeight) return;
            _lastStyledWidth = Screen.width;
            _lastStyledHeight = Screen.height;

            float s = ChessFontProvider.GetResponsiveScale();

            if (_darkHudBgTex == null) _darkHudBgTex = MakeSolidTex(new Color(0.06f, 0.08f, 0.12f, 0.92f));
            if (_goldFillTex == null) _goldFillTex = MakeSolidTex(new Color(0.95f, 0.78f, 0.22f, 1f));
            if (_crimsonFillTex == null) _crimsonFillTex = MakeSolidTex(new Color(0.86f, 0.18f, 0.24f, 1f));
            if (_emeraldFillTex == null) _emeraldFillTex = MakeSolidTex(new Color(0.20f, 0.84f, 0.44f, 1f));
            if (_cyanFillTex == null) _cyanFillTex = MakeSolidTex(new Color(0.20f, 0.76f, 0.96f, 1f));
            if (_trackDarkTex == null) _trackDarkTex = MakeSolidTex(new Color(0.14f, 0.17f, 0.24f, 0.95f));
            if (_btnPrimaryTex == null) _btnPrimaryTex = MakeSolidTex(new Color(0.78f, 0.20f, 0.26f, 1f));
            if (_btnSecondaryTex == null) _btnSecondaryTex = MakeSolidTex(new Color(0.16f, 0.21f, 0.30f, 1f));
            if (_btnDangerTex == null) _btnDangerTex = MakeSolidTex(new Color(0.45f, 0.12f, 0.16f, 1f));
            if (_modalDimTex == null) _modalDimTex = MakeSolidTex(new Color(0.02f, 0.03f, 0.06f, 0.76f));

            _clockStyle = new GUIStyle(GUI.skin.label)
            {
                font = ChessFontProvider.SerifBold,
                fontSize = Mathf.RoundToInt(27 * s),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _headerLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = ChessFontProvider.SerifBold,
                fontSize = Mathf.RoundToInt(14 * s),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.94f, 0.96f, 1f) }
            };

            _smallLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = ChessFontProvider.SerifRegular,
                fontSize = Mathf.RoundToInt(12 * s),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.84f, 0.88f, 0.94f) }
            };

            _weaponNameStyle = new GUIStyle(GUI.skin.label)
            {
                font = ChessFontProvider.SerifBold,
                fontSize = Mathf.RoundToInt(13 * s),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _bannerTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = ChessFontProvider.SerifBold,
                fontSize = Mathf.RoundToInt(18 * s),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _bannerSubStyle = new GUIStyle(GUI.skin.label)
            {
                font = ChessFontProvider.SerifRegular,
                fontSize = Mathf.RoundToInt(13 * s),
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.86f, 0.90f, 0.96f) }
            };

            _modalTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = ChessFontProvider.SerifBold,
                fontSize = Mathf.RoundToInt(24 * s),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _modalBodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = ChessFontProvider.SerifRegular,
                fontSize = Mathf.RoundToInt(13 * s),
                wordWrap = true,
                richText = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.90f, 0.93f, 0.98f) }
            };

            _primaryBtnStyle = new GUIStyle(GUI.skin.button)
            {
                font = ChessFontProvider.SerifBold,
                fontSize = Mathf.RoundToInt(15 * s),
                fontStyle = FontStyle.Bold,
                normal = { background = _btnPrimaryTex, textColor = Color.white },
                hover = { background = _goldFillTex, textColor = new Color(0.10f, 0.08f, 0.04f) },
                active = { background = _goldFillTex, textColor = Color.black }
            };

            _secondaryBtnStyle = new GUIStyle(GUI.skin.button)
            {
                font = ChessFontProvider.SerifBold,
                fontSize = Mathf.RoundToInt(14 * s),
                fontStyle = FontStyle.Bold,
                normal = { background = _btnSecondaryTex, textColor = Color.white },
                hover = { background = _trackDarkTex, textColor = new Color(1f, 0.85f, 0.30f) }
            };

            _dangerBtnStyle = new GUIStyle(GUI.skin.button)
            {
                font = ChessFontProvider.SerifBold,
                fontSize = Mathf.RoundToInt(14 * s),
                fontStyle = FontStyle.Bold,
                normal = { background = _btnDangerTex, textColor = Color.white }
            };
        }

        private static Texture2D MakeSolidTex(Color col)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new Color[] { col, col, col, col });
            tex.Apply();
            return tex;
        }

        private void DestroyCachedTextures()
        {
            DestroyTex(ref _darkHudBgTex);
            DestroyTex(ref _goldFillTex);
            DestroyTex(ref _crimsonFillTex);
            DestroyTex(ref _emeraldFillTex);
            DestroyTex(ref _cyanFillTex);
            DestroyTex(ref _trackDarkTex);
            DestroyTex(ref _btnPrimaryTex);
            DestroyTex(ref _btnSecondaryTex);
            DestroyTex(ref _btnDangerTex);
            DestroyTex(ref _modalDimTex);
        }

        private static void DestroyTex(ref Texture2D tex)
        {
            if (tex != null)
            {
                Destroy(tex);
                tex = null;
            }
        }

        #endregion
    }
}

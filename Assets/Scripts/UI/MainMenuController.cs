using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChessBalatro
{
    /// <summary>
    /// Controlador central de la interfaz de usuario para el flujo de pantallas y Game Loop.
    /// Gestiona los paneles de Menú Principal, Selector de Dificultad, HUD de juego,
    /// Modal de Pausa y Pantalla de Game Over (Victoria/Derrota).
    /// Soporta tanto componentes uGUI en Canvas como renderizado OnGUI procedural responsive (9:16).
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Paneles de Pantalla (Canvas uGUI)")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject difficultyPanel;
        [SerializeField] private GameObject gameplayHUD;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject gameOverPanel;

        [Header("Botones de Menú Principal")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button difficultyButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Text menuDifficultyBadgeText;

        [Header("Botones de Selección de Dificultad")]
        [SerializeField] private Button easyButton;
        [SerializeField] private Button mediumButton;
        [SerializeField] private Button hardButton;
        [SerializeField] private Button difficultyBackButton;

        [Header("Controles de Pausa")]
        [SerializeField] private Button pauseTriggerButton; // Botón flotante en la esquina superior durante el juego
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseQuitButton;

        [Header("Elementos de Game Over")]
        [SerializeField] private Text gameOverTitleText;
        [SerializeField] private Text gameOverSubtitleText;
        [SerializeField] private Text gameOverTurnsText;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button gameOverMenuButton;

        [Header("Configuración Móvil")]
        [SerializeField] private bool autoConfigureMobileCanvas = true;
        [SerializeField] private bool showOnScreenFallbackUI = true;

        // Estado interno
        private bool _isDifficultySubmenuOpen = false;
        private bool _lastGameOverIsVictory = false;
        private int _lastGameOverTurns = 0;

        // Estilos para OnGUI
        private GUIStyle _cardPanelStyle;
        private GUIStyle _modalOverlayStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _primaryButtonStyle;
        private GUIStyle _secondaryButtonStyle;
        private GUIStyle _dangerButtonStyle;
        private Texture2D _cardBgTex;
        private Texture2D _modalBgTex;
        private Texture2D _primaryBtnTex;
        private Texture2D _secondaryBtnTex;
        private Texture2D _dangerBtnTex;

        private void Awake()
        {
            if (autoConfigureMobileCanvas)
            {
                ConfigureCanvasScalerForMobile();
            }
            EnsureEventSystem();
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                esObj.AddComponent<StandaloneInputModule>();
#endif
            }
        }

        private void Start()
        {
            SetupUguiButtonListeners();
            SubscribeGameManagerEvents();

            // Sincronizar estado inicial
            if (GameManager.Instance != null)
            {
                HandleGameStateChanged(GameManager.Instance.CurrentState);
                HandleDifficultyChanged(GameManager.Instance.SelectedDifficulty);
            }
            else
            {
                ShowPanelOnly(mainMenuPanel);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeGameManagerEvents();
            DestroyCachedTextures();
        }

        /// <summary>
        /// Configura el CanvasScaler en "Scale With Screen Size" (1080x1920, 9:16 Portrait) con Match = 0.5f.
        /// </summary>
        public void ConfigureCanvasScalerForMobile()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();

            if (canvas != null)
            {
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920); // 9:16 Portrait Estándar Móvil
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f; // Equilibrio simétrico entre ancho y alto
            }
        }

        #region Enlace de Eventos y Botones uGUI

        private void SetupUguiButtonListeners()
        {
            // Menú Principal
            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);

            if (difficultyButton != null)
                difficultyButton.onClick.AddListener(OnOpenDifficultyClicked);

            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitClicked);

            // Dificultad
            if (easyButton != null)
                easyButton.onClick.AddListener(() => OnSelectDifficulty(AIDifficulty.Easy));

            if (mediumButton != null)
                mediumButton.onClick.AddListener(() => OnSelectDifficulty(AIDifficulty.Medium));

            if (hardButton != null)
                hardButton.onClick.AddListener(() => OnSelectDifficulty(AIDifficulty.Hard));

            if (difficultyBackButton != null)
                difficultyBackButton.onClick.AddListener(OnCloseDifficultyClicked);

            // Pausa
            if (pauseTriggerButton != null)
                pauseTriggerButton.onClick.AddListener(OnPauseClicked);

            if (resumeButton != null)
                resumeButton.onClick.AddListener(OnResumeClicked);

            if (pauseQuitButton != null)
                pauseQuitButton.onClick.AddListener(OnReturnToMenuClicked);

            // Game Over
            if (playAgainButton != null)
                playAgainButton.onClick.AddListener(OnPlayAgainClicked);

            if (gameOverMenuButton != null)
                gameOverMenuButton.onClick.AddListener(OnReturnToMenuClicked);
        }

        private void SubscribeGameManagerEvents()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;

                GameManager.Instance.OnDifficultyChanged -= HandleDifficultyChanged;
                GameManager.Instance.OnDifficultyChanged += HandleDifficultyChanged;

                GameManager.Instance.OnGameOver -= HandleGameOver;
                GameManager.Instance.OnGameOver += HandleGameOver;
            }
        }

        private void UnsubscribeGameManagerEvents()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
                GameManager.Instance.OnDifficultyChanged -= HandleDifficultyChanged;
                GameManager.Instance.OnGameOver -= HandleGameOver;
            }
        }

        #endregion

        #region Respuestas a Eventos de GameManager

        private void HandleGameStateChanged(GameState state)
        {
            _isDifficultySubmenuOpen = false;

            switch (state)
            {
                case GameState.MainMenu:
                    ShowPanelOnly(mainMenuPanel);
                    SetPanelActive(gameplayHUD, false);
                    SetPanelActive(pausePanel, false);
                    SetPanelActive(gameOverPanel, false);
                    break;

                case GameState.MasterSelecting:
                    // Ocultar todos los paneles propios; MasterSelectionUI toma el control
                    SetPanelActive(mainMenuPanel, false);
                    SetPanelActive(difficultyPanel, false);
                    SetPanelActive(gameplayHUD, false);
                    SetPanelActive(pausePanel, false);
                    SetPanelActive(gameOverPanel, false);
                    break;

                case GameState.Playing:
                    SetPanelActive(mainMenuPanel, false);
                    SetPanelActive(difficultyPanel, false);
                    SetPanelActive(pausePanel, false);
                    SetPanelActive(gameOverPanel, false);
                    SetPanelActive(gameplayHUD, true);
                    break;

                case GameState.Paused:
                    SetPanelActive(pausePanel, true);
                    break;

                case GameState.GameOver:
                    SetPanelActive(pausePanel, false);
                    SetPanelActive(gameOverPanel, true);
                    break;
            }
        }

        private void HandleDifficultyChanged(AIDifficulty difficulty)
        {
            if (menuDifficultyBadgeText != null)
            {
                menuDifficultyBadgeText.text = $"Dificultad IA: {DifficultySelectorUI.GetDifficultyDisplayName(difficulty)}";
                menuDifficultyBadgeText.color = DifficultySelectorUI.GetDifficultyColor(difficulty);
            }
        }

        private void HandleGameOver(bool isVictory, int totalTurns)
        {
            _lastGameOverIsVictory = isVictory;
            _lastGameOverTurns = totalTurns;

            if (gameOverTitleText != null)
            {
                gameOverTitleText.text = isVictory ? "¡VICTORIA!" : "¡DERROTA!";
                gameOverTitleText.color = isVictory ? new Color(0.35f, 0.85f, 0.45f) : new Color(0.95f, 0.25f, 0.25f);
            }

            if (gameOverSubtitleText != null)
            {
                gameOverSubtitleText.text = isVictory
                    ? "Has derrotado al rey rival en Jaque Mate."
                    : "Tu rey blanco ha sido derrotado en Jaque Mate.";
            }

            if (gameOverTurnsText != null)
            {
                gameOverTurnsText.text = $"Turnos jugados: {totalTurns}";
            }

            SetPanelActive(gameOverPanel, true);
        }

        #endregion

        #region Métodos de Interacción UI

        public void OnPlayClicked()
        {
            Debug.Log("<color=cyan><b>[MainMenuController] Botón JUGAR pulsado. Abriendo selección de Gran Maestro...</b></color>");

            // Asegurar que MasterSelectionUI exista en la escena
            if (FindAnyObjectByType<MasterSelectionUI>() == null)
            {
                BoardView bv = FindAnyObjectByType<BoardView>();
                GameObject host = bv != null ? bv.gameObject : gameObject;
                host.AddComponent<MasterSelectionUI>();
            }

            if (GameManager.Instance != null)
            {
                // El flujo: MainMenu → MasterSelecting → Playing
                GameManager.Instance.BeginMasterSelection();
            }
            else
            {
                GameManager gm = FindAnyObjectByType<GameManager>();
                if (gm == null)
                {
                    BoardView board = FindAnyObjectByType<BoardView>();
                    if (board != null) gm = board.gameObject.AddComponent<GameManager>();
                }

                if (gm != null)
                {
                    gm.BeginMasterSelection();
                }
                else
                {
                    Debug.LogError("[MainMenuController] No se encontró GameManager ni BoardView en la escena.");
                }
            }
        }

        public void OnOpenDifficultyClicked()
        {
            _isDifficultySubmenuOpen = true;
            if (difficultyPanel != null)
            {
                SetPanelActive(mainMenuPanel, false);
                SetPanelActive(difficultyPanel, true);
            }
        }

        public void OnCloseDifficultyClicked()
        {
            _isDifficultySubmenuOpen = false;
            if (difficultyPanel != null)
            {
                SetPanelActive(difficultyPanel, false);
                SetPanelActive(mainMenuPanel, true);
            }
        }

        public void OnSelectDifficulty(AIDifficulty difficulty)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetDifficulty(difficulty);
            }
            OnCloseDifficultyClicked();
        }

        public void OnPauseClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PauseGame();
            }
        }

        public void OnResumeClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ResumeGame();
            }
        }

        public void OnReturnToMenuClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToMenu();
            }
        }

        public void OnPlayAgainClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartGame();
            }
        }

        public void OnQuitClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.QuitGame();
            }
        }

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null) panel.SetActive(active);
        }

        private void ShowPanelOnly(GameObject targetPanel)
        {
            SetPanelActive(mainMenuPanel, targetPanel == mainMenuPanel);
            SetPanelActive(difficultyPanel, targetPanel == difficultyPanel);
            SetPanelActive(gameplayHUD, targetPanel == gameplayHUD);
            SetPanelActive(pausePanel, targetPanel == pausePanel);
            SetPanelActive(gameOverPanel, targetPanel == gameOverPanel);
        }

        #endregion

        #region Renderizado OnGUI Procedural (Zero-Setup Móvil 9:16)

        private void InitStylesIfNeeded()
        {
            if (_cardPanelStyle != null) return;

            _cardBgTex = MakeColorTex(new Color(0.08f, 0.10f, 0.14f, 0.94f));
            _modalBgTex = MakeColorTex(new Color(0.03f, 0.04f, 0.06f, 0.82f));
            _primaryBtnTex = MakeColorTex(new Color(0.18f, 0.62f, 0.92f, 0.95f));
            _secondaryBtnTex = MakeColorTex(new Color(0.18f, 0.22f, 0.28f, 0.90f));
            _dangerBtnTex = MakeColorTex(new Color(0.85f, 0.24f, 0.24f, 0.90f));

            _cardPanelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _cardBgTex },
                padding = new RectOffset(18, 18, 18, 18),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _modalOverlayStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _modalBgTex }
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.95f, 0.96f, 0.98f) }
            };

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.72f, 0.78f, 0.88f) }
            };

            _primaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = _primaryBtnTex, textColor = Color.white },
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _secondaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = _secondaryBtnTex, textColor = new Color(0.88f, 0.92f, 0.96f) },
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };

            _dangerButtonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = _dangerBtnTex, textColor = Color.white },
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void OnGUI()
        {
            if (!showOnScreenFallbackUI) return;

            InitStylesIfNeeded();

            GameState state = GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.MainMenu;

            // 1. Botón de Pausa flotante en la esquina superior durante la partida
            if (state == GameState.Playing)
            {
                DrawFloatingPauseButton();
                return;
            }

            // 2. Si estamos en MasterSelecting, MasterSelectionUI se encarga de renderizar su propia interfaz
            if (state == GameState.MasterSelecting)
            {
                return;
            }

            // Fondo oscurecido para modales (Overlay)
            Rect fullscreen = new Rect(0, 0, Screen.width, Screen.height);
            GUI.Box(fullscreen, GUIContent.none, _modalOverlayStyle);

            // Renderizar según estado
            switch (state)
            {
                case GameState.MainMenu:
                    if (_isDifficultySubmenuOpen) DrawOnGuiDifficultyMenu();
                    else DrawOnGuiMainMenu();
                    break;

                case GameState.Paused:
                    DrawOnGuiPauseMenu();
                    break;

                case GameState.GameOver:
                    DrawOnGuiGameOverMenu();
                    break;
            }
        }

        private void DrawFloatingPauseButton()
        {
            float btnSize = 44f;
            float margin = 16f;
            Rect btnRect = new Rect(Screen.width - btnSize - margin, margin, btnSize, btnSize);

            if (GUI.Button(btnRect, "❚❚", _secondaryButtonStyle))
            {
                OnPauseClicked();
            }
        }

        private void DrawOnGuiMainMenu()
        {
            float panelWidth = Mathf.Min(340f, Screen.width * 0.88f);
            float panelHeight = 310f;
            float posX = (Screen.width - panelWidth) * 0.5f;
            float posY = (Screen.height - panelHeight) * 0.5f;

            Rect rect = new Rect(posX, posY, panelWidth, panelHeight);
            GUI.Box(rect, GUIContent.none, _cardPanelStyle);

            GUILayout.BeginArea(rect);
            GUILayout.Space(8);

            GUILayout.Label("CHESS BALATRO", _titleStyle);
            GUILayout.Label("Roguelike Chess Battles", _subtitleStyle);

            GUILayout.Space(14);

            // Insignia de dificultad actual
            AIDifficulty currentDiff = GameManager.Instance != null ? GameManager.Instance.SelectedDifficulty : AIDifficulty.Medium;
            GUI.color = DifficultySelectorUI.GetDifficultyColor(currentDiff);
            GUILayout.Label($"● DIFICULTAD: {DifficultySelectorUI.GetDifficultyDisplayName(currentDiff).ToUpperInvariant()}", _subtitleStyle);
            GUI.color = Color.white;

            GUILayout.Space(16);

            // Botón Jugar
            if (GUILayout.Button("▶  JUGAR", _primaryButtonStyle, GUILayout.Height(46)))
            {
                OnPlayClicked();
            }

            GUILayout.Space(8);

            // Botón Dificultad
            if (GUILayout.Button("⚙  DIFICULTAD IA", _secondaryButtonStyle, GUILayout.Height(38)))
            {
                OnOpenDifficultyClicked();
            }

            GUILayout.Space(8);

            // Botón Salir
            if (GUILayout.Button("✕  SALIR", _dangerButtonStyle, GUILayout.Height(36)))
            {
                OnQuitClicked();
            }

            GUILayout.EndArea();
        }

        private void DrawOnGuiDifficultyMenu()
        {
            float panelWidth = Mathf.Min(340f, Screen.width * 0.88f);
            float panelHeight = 310f;
            float posX = (Screen.width - panelWidth) * 0.5f;
            float posY = (Screen.height - panelHeight) * 0.5f;

            Rect rect = new Rect(posX, posY, panelWidth, panelHeight);
            GUI.Box(rect, GUIContent.none, _cardPanelStyle);

            GUILayout.BeginArea(rect);
            GUILayout.Space(8);

            GUILayout.Label("DIFICULTAD IA", _titleStyle);
            GUILayout.Label("Selecciona el nivel del rival", _subtitleStyle);

            GUILayout.Space(16);

            AIDifficulty current = GameManager.Instance != null ? GameManager.Instance.SelectedDifficulty : AIDifficulty.Medium;

            DrawDifficultyOptionButton(AIDifficulty.Easy, "FÁCIL", "Aleatorio + 70% capturas", current == AIDifficulty.Easy);
            GUILayout.Space(6);
            DrawDifficultyOptionButton(AIDifficulty.Medium, "MEDIA", "Minimax prof. 2 (Material)", current == AIDifficulty.Medium);
            GUILayout.Space(6);
            DrawDifficultyOptionButton(AIDifficulty.Hard, "DIFÍCIL", "Alfa-Beta prof. 3 + Control central", current == AIDifficulty.Hard);

            GUILayout.Space(12);

            if (GUILayout.Button("← VOLVER", _secondaryButtonStyle, GUILayout.Height(36)))
            {
                OnCloseDifficultyClicked();
            }

            GUILayout.EndArea();
        }

        private void DrawDifficultyOptionButton(AIDifficulty diff, string label, string desc, bool isSelected)
        {
            Color color = DifficultySelectorUI.GetDifficultyColor(diff);
            Color oldColor = GUI.color;

            if (isSelected) GUI.color = color;
            string prefix = isSelected ? "✓ " : "   ";

            if (GUILayout.Button($"{prefix}{label} - {desc}", isSelected ? _primaryButtonStyle : _secondaryButtonStyle, GUILayout.Height(36)))
            {
                OnSelectDifficulty(diff);
            }

            GUI.color = oldColor;
        }

        private void DrawOnGuiPauseMenu()
        {
            float panelWidth = Mathf.Min(320f, Screen.width * 0.86f);
            float panelHeight = 240f;
            float posX = (Screen.width - panelWidth) * 0.5f;
            float posY = (Screen.height - panelHeight) * 0.5f;

            Rect rect = new Rect(posX, posY, panelWidth, panelHeight);
            GUI.Box(rect, GUIContent.none, _cardPanelStyle);

            GUILayout.BeginArea(rect);
            GUILayout.Space(10);

            GUILayout.Label("PARTIDA PAUSADA", _titleStyle);
            GUILayout.Label("El reloj táctico está detenido", _subtitleStyle);

            GUILayout.Space(20);

            if (GUILayout.Button("▶  REANUDAR", _primaryButtonStyle, GUILayout.Height(44)))
            {
                OnResumeClicked();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("⌂  MENÚ PRINCIPAL", _secondaryButtonStyle, GUILayout.Height(38)))
            {
                OnReturnToMenuClicked();
            }

            GUILayout.EndArea();
        }

        private void DrawOnGuiGameOverMenu()
        {
            float panelWidth = Mathf.Min(340f, Screen.width * 0.88f);
            float panelHeight = 270f;
            float posX = (Screen.width - panelWidth) * 0.5f;
            float posY = (Screen.height - panelHeight) * 0.5f;

            Rect rect = new Rect(posX, posY, panelWidth, panelHeight);
            GUI.Box(rect, GUIContent.none, _cardPanelStyle);

            GUILayout.BeginArea(rect);
            GUILayout.Space(12);

            Color titleColor = _lastGameOverIsVictory ? new Color(0.35f, 0.90f, 0.45f) : new Color(0.95f, 0.28f, 0.28f);
            GUI.color = titleColor;
            GUILayout.Label(_lastGameOverIsVictory ? "¡¡ VICTORIA !!" : "¡¡ DERROTA !!", _titleStyle);
            GUI.color = Color.white;

            string sub = _lastGameOverIsVictory
                ? "Has derrotado al Rey rival en Jaque Mate."
                : "Tu Rey Blanco ha caído en Jaque Mate.";
            GUILayout.Label(sub, _subtitleStyle);
            GUILayout.Label($"Turnos completados: <b>{_lastGameOverTurns}</b>", _subtitleStyle);

            GUILayout.Space(20);

            if (GUILayout.Button("▶  JUGAR DE NUEVO", _primaryButtonStyle, GUILayout.Height(44)))
            {
                OnPlayAgainClicked();
            }

            GUILayout.Space(8);

            if (GUILayout.Button("⌂  MENÚ PRINCIPAL", _secondaryButtonStyle, GUILayout.Height(38)))
            {
                OnReturnToMenuClicked();
            }

            GUILayout.EndArea();
        }

        private static Texture2D MakeColorTex(Color col)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[4];
            for (int i = 0; i < 4; i++) pixels[i] = col;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void DestroyCachedTextures()
        {
            if (_cardBgTex != null) Destroy(_cardBgTex);
            if (_modalBgTex != null) Destroy(_modalBgTex);
            if (_primaryBtnTex != null) Destroy(_primaryBtnTex);
            if (_secondaryBtnTex != null) Destroy(_secondaryBtnTex);
            if (_dangerBtnTex != null) Destroy(_dangerBtnTex);
        }

        #endregion
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChessBalatro
{
    /// <summary>
    /// Controlador principal para la escena del Menú Principal (MainMenuScene).
    /// Administra el flujo de pantallas y navegación entre paneles:
    /// - Panel Principal ("JUGAR", "MODOS", "ASPECTOS", "OPCIONES", "DONACIONES")
    /// - Panel Modos (Clásico/Roguelike, Educativo [destacado], Arcade [Próximamente])
    /// - Panel Aspectos (Navegación y equipamiento de Skins con PieceSkinManager)
    /// - Panel Opciones (Sliders de audio y Toggle háptico con SettingsManager)
    /// - Panel Donaciones (Modal de soporte, enlaces web y créditos)
    /// Gestiona cambios de escena asíncronos con efecto Fade Out / Fade In suave.
    /// Incluye soporte nativo para Canvas uGUI y fallback procedural responsive (9:16 móvil).
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        public static MainMenuManager Instance { get; private set; }

        public enum MenuPanel
        {
            Main,
            QuickMatchSetup,
            Modes,
            Skins,
            Options,
            Donations
        }

        [Header("Configuración de Escenas")]
        [SerializeField] private string gameplaySceneName = "SampleScene";
        [SerializeField] private float fadeDuration = 0.35f;

        [Header("Paneles uGUI (Opcionales)")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject quickMatchSetupPanel;
        [SerializeField] private GameObject modesPanel;
        [SerializeField] private GameObject skinsPanel;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private GameObject donationsModal;
        [SerializeField] private CanvasGroup fadeOverlayCanvasGroup;

        [Header("Botones Panel Principal")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button modesButton;
        [SerializeField] private Button tutorialButton;
        [SerializeField] private Button skinsButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button donationsButton;

        [Header("Botones Panel Modos")]
        [SerializeField] private Button classicModeButton;
        [SerializeField] private Button educationModeButton;
        [SerializeField] private Button modesTutorialButton;
        [SerializeField] private Button arcadeModeButton;
        [SerializeField] private Button modesBackButton;

        [Header("Controles Panel Aspectos")]
        [SerializeField] private Text skinNameText;
        [SerializeField] private Text skinDescriptionText;
        [SerializeField] private Image skinPreviewImage;
        [SerializeField] private Button nextSkinButton;
        [SerializeField] private Button prevSkinButton;
        [SerializeField] private Button equipSkinButton;
        [SerializeField] private Text equipSkinButtonText;
        [SerializeField] private Button skinsBackButton;

        [Header("Controles Panel Opciones")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle vibrationToggle;
        [SerializeField] private Button optionsBackButton;

        [Header("Controles Panel Donaciones")]
        [SerializeField] private Button supportKofiButton;
        [SerializeField] private Button supportPatreonButton;
        [SerializeField] private Button supportWebButton;
        [SerializeField] private Button donationsCloseButton;

        [Header("URLs de Soporte")]
        [SerializeField] private string kofiUrl = "https://ko-fi.com";
        [SerializeField] private string patreonUrl = "https://patreon.com";
        [SerializeField] private string webUrl = "https://itch.io";

        [Header("Estado Interno")]
        [SerializeField] private MenuPanel activePanel = MenuPanel.Main;

        // Estado del menú rápido de partida
        private int _quickCardIndex = 0;
        private PieceColor _quickPlayerColor = PieceColor.White;
        private int _quickTimeIndex = 0; // 0: Sin límite, 1: 3 Min, 2: 5 Min, 3: 10 Min
        private int _quickDiffIndex = 1; // 0: Fácil, 1: Medio, 2: Difícil
        private List<CardData> _startingCards = new List<CardData>();
        private readonly float[] _timeValues = { 0f, 180f, 300f, 600f };
        private readonly string[] _timeLabels = { "∞ Sin Límite", "⚡ 3 Min", "⏱ 5 Min", "⏳ 10 Min" };
        private readonly AIDifficulty[] _diffValues = { AIDifficulty.Easy, AIDifficulty.Medium, AIDifficulty.Hard };
        private readonly string[] _diffLabels = { "FÁCIL", "MEDIO", "DIFÍCIL" };

        // Telemetría de rectángulos en pantalla para el Sistema de Onboarding
        public bool IsRenderingOnGui { get; private set; } = false;
        public Rect LastPlayButtonRect { get; private set; }
        public Rect LastArcadeButtonRect { get; private set; }
        public Rect LastEducationButtonRect { get; private set; }
        public Rect LastSkinsAndOptionsRect { get; private set; }
        public Rect LastCardSelectorRect { get; private set; }
        public Rect LastStartMatchButtonRect { get; private set; }

        public void SelectMasterCardIndex(int index)
        {
            if (_startingCards == null || _startingCards.Count == 0)
            {
                _startingCards = GameManager.GetAvailableStartingCards();
            }
            if (_startingCards != null && _startingCards.Count > 0)
            {
                _quickCardIndex = Mathf.Clamp(index, 0, _startingCards.Count - 1);
            }
        }

        // Control de transición suave
        private bool _isTransitioning = false;
        private float _fadeAlpha = 0f;
        private Texture2D _fadeTexture;

        // Estilos para OnGUI fallback y Arcade 32-bits
        private bool _onGuiInitialized = false;
        private int _lastScreenWidth = 0;
        private int _lastScreenHeight = 0;
        private float _panelEntranceStartTime = 0f;
        private GUIStyle _cardPanelStyle;
        private GUIStyle _modalOverlayStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _titleShadowStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _panelHeaderStyle;
        private GUIStyle _primaryButtonStyle;
        private GUIStyle _secondaryButtonStyle;
        private GUIStyle _featuredButtonStyle;
        private GUIStyle _disabledButtonStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _cardTitleStyle;
        private GUIStyle _miniCostStyle;
        private GUIStyle _miniRarityStyle;
        private Texture2D _panelBgTex;
        private Texture2D _cardPanelBgTex;
        private Texture2D _cardFrameTex;
        private Texture2D _modalBgTex;
        private Texture2D _primaryBtnTex;
        private Texture2D _secondaryBtnTex;
        private Texture2D _featuredBtnTex;
        private Texture2D _disabledBtnTex;

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

            EnsureManagers();
            SetupUguiListeners();
        }

        private void Start()
        {
            BoardView.EnsureCanvasScalersConfigured();
            _startingCards = GameManager.GetAvailableStartingCards();
            SwitchPanel(MenuPanel.Main);
            SyncSkinsUI();
            SyncOptionsUI();

            // Fade in inicial de la escena
            StartCoroutine(FadeInRoutine());
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            DestroyCachedTextures();
        }

        private void EnsureManagers()
        {
            if (SettingsManager.Instance == null)
            {
                GameObject sm = new GameObject("SettingsManager");
                sm.AddComponent<SettingsManager>();
            }

            if (PieceSkinManager.Instance == null)
            {
                GameObject pm = new GameObject("PieceSkinManager");
                pm.AddComponent<PieceSkinManager>();
            }

            if (ArcadeBackgroundScroller.Instance == null && FindAnyObjectByType<ArcadeBackgroundScroller>() == null)
            {
                gameObject.AddComponent<ArcadeBackgroundScroller>();
            }

            if (PlayerProfileBadgeUI.Instance == null && FindAnyObjectByType<PlayerProfileBadgeUI>() == null)
            {
                gameObject.AddComponent<PlayerProfileBadgeUI>();
            }
        }

        #region Configuración de Listeners uGUI

        private void SetupUguiListeners()
        {
            // Panel Principal: "JUGAR" abre el menú rápido de configuración
            if (playButton != null)
                playButton.onClick.AddListener(() => SwitchPanel(MenuPanel.QuickMatchSetup));

            if (modesButton != null)
                modesButton.onClick.AddListener(() => StartGameWithMode(GameMode.Education));

            if (tutorialButton != null)
                tutorialButton.onClick.AddListener(() => OnboardingManager.EnsureInstance().OpenTutorialHub());

            if (educationModeButton != null)
                educationModeButton.onClick.AddListener(() => StartGameWithMode(GameMode.Education));

            if (modesTutorialButton != null)
                modesTutorialButton.onClick.AddListener(() => OnboardingManager.EnsureInstance().OpenTutorialHub());

            if (skinsButton != null)
                skinsButton.onClick.AddListener(() => SwitchPanel(MenuPanel.Skins));

            if (optionsButton != null)
                optionsButton.onClick.AddListener(() => SwitchPanel(MenuPanel.Options));

            if (donationsButton != null)
                donationsButton.onClick.AddListener(() => SwitchPanel(MenuPanel.Donations));

            // Panel Modos
            if (classicModeButton != null)
                classicModeButton.onClick.AddListener(() => StartGameWithMode(GameMode.ClassicRoguelike));

            if (educationModeButton != null)
                educationModeButton.onClick.AddListener(() => StartGameWithMode(GameMode.Education));

            if (arcadeModeButton != null)
            {
                arcadeModeButton.interactable = true;
                arcadeModeButton.onClick.AddListener(() => StartGameWithMode(GameMode.Arcade));
            }

            if (modesBackButton != null)
                modesBackButton.onClick.AddListener(() => SwitchPanel(MenuPanel.Main));

            // Panel Aspectos
            if (nextSkinButton != null)
                nextSkinButton.onClick.AddListener(OnNextSkinClicked);

            if (prevSkinButton != null)
                prevSkinButton.onClick.AddListener(OnPrevSkinClicked);

            if (equipSkinButton != null)
                equipSkinButton.onClick.AddListener(OnEquipSkinClicked);

            if (skinsBackButton != null)
                skinsBackButton.onClick.AddListener(() => SwitchPanel(MenuPanel.Main));

            // Panel Opciones
            if (optionsBackButton != null)
                optionsBackButton.onClick.AddListener(() => SwitchPanel(MenuPanel.Main));

            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.BindControls(musicSlider, sfxSlider, vibrationToggle);
            }

            // Panel Donaciones
            if (supportKofiButton != null)
                supportKofiButton.onClick.AddListener(() => Application.OpenURL(kofiUrl));

            if (supportPatreonButton != null)
                supportPatreonButton.onClick.AddListener(() => Application.OpenURL(patreonUrl));

            if (supportWebButton != null)
                supportWebButton.onClick.AddListener(() => Application.OpenURL(webUrl));

            if (donationsCloseButton != null)
                donationsCloseButton.onClick.AddListener(() => SwitchPanel(MenuPanel.Main));
        }

        #endregion

        #region Navegación entre Paneles

        public void SwitchPanel(MenuPanel newPanel)
        {
            activePanel = newPanel;
            _panelEntranceStartTime = Time.realtimeSinceStartup;

            // Actualizar visibilidad de uGUI
            SetGameObjectActive(mainPanel, newPanel == MenuPanel.Main);
            SetGameObjectActive(quickMatchSetupPanel, newPanel == MenuPanel.QuickMatchSetup);
            SetGameObjectActive(modesPanel, newPanel == MenuPanel.Modes);
            SetGameObjectActive(skinsPanel, newPanel == MenuPanel.Skins);
            SetGameObjectActive(optionsPanel, newPanel == MenuPanel.Options);
            SetGameObjectActive(donationsModal, newPanel == MenuPanel.Donations);

            GameObject activeObj = GetActivePanelObject(newPanel);
            if (activeObj != null)
            {
                ArcadeButtonJuice.AutoApplyToButtons(activeObj, 0.05f);
            }

            if (newPanel == MenuPanel.Skins)
            {
                SyncSkinsUI();
            }
            else if (newPanel == MenuPanel.Options)
            {
                SyncOptionsUI();
            }
        }

        private GameObject GetActivePanelObject(MenuPanel panel)
        {
            switch (panel)
            {
                case MenuPanel.Main: return mainPanel;
                case MenuPanel.QuickMatchSetup: return quickMatchSetupPanel;
                case MenuPanel.Modes: return modesPanel;
                case MenuPanel.Skins: return skinsPanel;
                case MenuPanel.Options: return optionsPanel;
                case MenuPanel.Donations: return donationsModal;
                default: return null;
            }
        }

        private void SetGameObjectActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }

        #endregion

        #region Aspectos (Skins)

        private void OnNextSkinClicked()
        {
            if (PieceSkinManager.Instance != null)
            {
                PieceSkinManager.Instance.NextPreview();
                SyncSkinsUI();
            }
        }

        private void OnPrevSkinClicked()
        {
            if (PieceSkinManager.Instance != null)
            {
                PieceSkinManager.Instance.PreviousPreview();
                SyncSkinsUI();
            }
        }

        private void OnEquipSkinClicked()
        {
            if (PieceSkinManager.Instance != null)
            {
                PieceSkinManager.Instance.EquipCurrentPreview();
                SyncSkinsUI();
            }
        }

        private void SyncSkinsUI()
        {
            if (PieceSkinManager.Instance == null) return;

            PieceSpriteLibrary preview = PieceSkinManager.Instance.CurrentPreviewSkin;
            if (preview == null) return;

            if (skinNameText != null)
                skinNameText.text = preview.SkinName;

            if (skinDescriptionText != null)
                skinDescriptionText.text = preview.Description;

            if (skinPreviewImage != null)
            {
                Sprite icon = preview.GetPreviewSprite();
                skinPreviewImage.sprite = icon;
                skinPreviewImage.enabled = icon != null;
            }

            bool isEquipped = PieceSkinManager.Instance.ActiveSkinIndex == PieceSkinManager.Instance.PreviewSkinIndex;
            if (equipSkinButton != null)
            {
                equipSkinButton.interactable = !isEquipped;
            }
            if (equipSkinButtonText != null)
            {
                equipSkinButtonText.text = isEquipped ? "EQUIPADO" : "EQUIPAR";
            }
        }

        #endregion

        #region Opciones y Audio

        private void SyncOptionsUI()
        {
            if (SettingsManager.Instance == null) return;

            if (musicSlider != null)
                musicSlider.value = SettingsManager.Instance.MusicVolume;

            if (sfxSlider != null)
                sfxSlider.value = SettingsManager.Instance.SFXVolume;

            if (vibrationToggle != null)
                vibrationToggle.isOn = SettingsManager.Instance.IsVibrationEnabled;
        }

        #endregion

        #region Transición de Escenas y Fade Suave

        /// <summary>
        /// Inicia una partida con el modo indicado y carga la escena de juego con transición suave.
        /// </summary>
        public void StartGameWithMode(GameMode mode)
        {
            if (_isTransitioning) return;

            GameManager.SelectedGameMode = mode;
            Debug.Log($"<color=green><b>[MainMenuManager] Lanzando partida en modo: {mode}</b></color>");

            StartCoroutine(LoadSceneWithFadeRoutine(gameplaySceneName));
        }

        private IEnumerator LoadSceneWithFadeRoutine(string targetScene)
        {
            _isTransitioning = true;

            // 1. Fade Out a negro
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _fadeAlpha = Mathf.Clamp01(elapsed / fadeDuration);
                if (fadeOverlayCanvasGroup != null)
                {
                    fadeOverlayCanvasGroup.alpha = _fadeAlpha;
                    fadeOverlayCanvasGroup.blocksRaycasts = true;
                }
                yield return null;
            }
            _fadeAlpha = 1f;
            if (fadeOverlayCanvasGroup != null) fadeOverlayCanvasGroup.alpha = 1f;

            // 2. Cargar escena de forma asíncrona
            AsyncOperation asyncOp = SceneManager.LoadSceneAsync(targetScene);
            if (asyncOp != null)
            {
                while (!asyncOp.isDone)
                {
                    yield return null;
                }
            }

            // 3. Fade In desde negro en la nueva escena (o final de transición)
            yield return StartCoroutine(FadeInRoutine());

            _isTransitioning = false;
        }

        private IEnumerator FadeInRoutine()
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _fadeAlpha = Mathf.Clamp01(1f - (elapsed / fadeDuration));
                if (fadeOverlayCanvasGroup != null)
                {
                    fadeOverlayCanvasGroup.alpha = _fadeAlpha;
                }
                yield return null;
            }
            _fadeAlpha = 0f;
            if (fadeOverlayCanvasGroup != null)
            {
                fadeOverlayCanvasGroup.alpha = 0f;
                fadeOverlayCanvasGroup.blocksRaycasts = false;
            }
        }

        #endregion

        #region Fallback Responsive OnGUI (9:16 Vertical Móvil)

        private void OnGUI()
        {
            float screenW = Screen.width;
            float screenH = Screen.height;

            if (screenW != _lastScreenWidth || screenH != _lastScreenHeight || !_onGuiInitialized)
            {
                _lastScreenWidth = (int)screenW;
                _lastScreenHeight = (int)screenH;
                InitOnGuiStyles();
            }

            float scale = ChessFontProvider.GetResponsiveScale();

            // Si los paneles uGUI están activos y asignados en la escena, solo pintar el fade si aplica
            bool hasUgui = (mainPanel != null && mainPanel.activeInHierarchy) ||
                           (quickMatchSetupPanel != null && quickMatchSetupPanel.activeInHierarchy) ||
                           (modesPanel != null && modesPanel.activeInHierarchy) ||
                           (skinsPanel != null && skinsPanel.activeInHierarchy) ||
                           (optionsPanel != null && optionsPanel.activeInHierarchy) ||
                           (donationsModal != null && donationsModal.activeInHierarchy);

            IsRenderingOnGui = !hasUgui;
            if (!hasUgui)
            {
                RenderOnGuiMenu(screenW, screenH, scale);
            }

            // Pintar Overlay de Fundido (Fade)
            if (_fadeAlpha > 0.001f)
            {
                GUI.depth = -1000;
                Color oldColor = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, _fadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, screenW, screenH), _fadeTexture != null ? _fadeTexture : Texture2D.whiteTexture);
                GUI.color = oldColor;
            }
        }

        private void RenderOnGuiMenu(float screenW, float screenH, float scale)
        {
            float panelW = Mathf.Min(screenW * 0.92f, 580f);
            float minTop = 104f * scale;
            float panelH = activePanel == MenuPanel.QuickMatchSetup
                ? Mathf.Min(screenH - minTop - (14f * scale), 785f * scale)
                : (activePanel == MenuPanel.Main
                    ? Mathf.Min(screenH - minTop - (16f * scale), 640f * scale)
                    : Mathf.Min(screenH * 0.82f, 620f));
            float panelX = (screenW - panelW) * 0.5f;
            float panelY = Mathf.Max(minTop, (screenH - panelH) * 0.5f + (24f * scale));

            // 1. Relieve de 32-bits: Caja exterior con sombra proyectada dura y borde biselado
            Rect panelRect = new Rect(panelX, panelY, panelW, panelH);
            DrawArcadeBox(panelRect, _panelBgTex, new Color(0.24f, 0.32f, 0.44f, 1f), 2f, 4f);

            // 2. Encabezado con Título y Sombra dura pixel art (2px offset)
            float titleY = panelY - (105f * scale);
            if (_titleShadowStyle != null)
            {
                GUI.Label(new Rect(2, titleY + 2, screenW, 58f * scale), "CHESS MATCH", _titleShadowStyle);
            }
            GUI.Label(new Rect(0, titleY, screenW, 58f * scale), "CHESS MATCH", _titleStyle);
            GUI.Label(new Rect(0, titleY + (52f * scale), screenW, 30f * scale), "TACTICAL ROGUELIKE & ACADEMY", _subtitleStyle);

            switch (activePanel)
            {
                case MenuPanel.Main:
                    RenderMainPanel(panelRect, scale);
                    break;
                case MenuPanel.QuickMatchSetup:
                    RenderQuickMatchSetupPanel(panelRect, scale);
                    break;
                case MenuPanel.Modes:
                    RenderModesPanel(panelRect, scale);
                    break;
                case MenuPanel.Skins:
                    RenderSkinsPanel(panelRect, scale);
                    break;
                case MenuPanel.Options:
                    RenderOptionsPanel(panelRect, scale);
                    break;
                case MenuPanel.Donations:
                    RenderDonationsModal(screenW, screenH, scale);
                    break;
            }
        }

        private void RenderMainPanel(Rect rect, float scale)
        {
            GUILayout.BeginArea(rect, _cardPanelStyle);
            {
                GUILayout.Space(12 * scale);
                GUILayout.Label("MENÚ PRINCIPAL", _panelHeaderStyle);
                GUILayout.Space(16 * scale);

                float overhead = 90f * scale;
                float spacing = Mathf.Clamp(8f * scale, 5f, 12f);
                float availH = Mathf.Max(250f, rect.height - overhead);
                float btnH = Mathf.Clamp((availH - (spacing * 6f)) / 7.0f, 32f, 58f * scale);

                // Botón 0: JUGAR (Destacado con pulso rítmico y sombra 32-bits)
                Rect btn0Rect = GUILayoutUtility.GetRect(new GUIContent(""), _featuredButtonStyle, GUILayout.Height(btnH));
                if (Event.current.type == EventType.Repaint && btn0Rect.width > 4f)
                {
                    Vector2 tl = GUIUtility.GUIToScreenPoint(btn0Rect.position);
                    LastPlayButtonRect = new Rect(tl.x, tl.y, btn0Rect.width, btn0Rect.height);
                }
                if (DrawArcadeButton(btn0Rect, "⚔  JUGAR  ⚔", _featuredButtonStyle, isFeatured: true, staggerDelay: 0f, onboardingTag: "menu_play"))
                {
                    SwitchPanel(MenuPanel.QuickMatchSetup);
                    OnboardingManager.Instance?.NotifyMenuPlayClicked();
                }

                GUILayout.Space(spacing);

                // Botón 1: MODO ARCADE (Chess Survivors - Asedio de 15 Minutos)
                Rect btnArcadeRect = GUILayoutUtility.GetRect(new GUIContent(""), _primaryButtonStyle, GUILayout.Height(btnH));
                if (Event.current.type == EventType.Repaint && btnArcadeRect.width > 4f)
                {
                    Vector2 tlA = GUIUtility.GUIToScreenPoint(btnArcadeRect.position);
                    LastArcadeButtonRect = new Rect(tlA.x, tlA.y, btnArcadeRect.width, btnArcadeRect.height);
                }
                if (DrawArcadeButton(btnArcadeRect, "⚡  MODO ARCADE (SURVIVORS)  ⚡", _primaryButtonStyle, isFeatured: false, staggerDelay: 0.03f, onboardingTag: "menu_arcade"))
                {
                    StartGameWithMode(GameMode.Arcade);
                }

                GUILayout.Space(spacing);

                // Botón 2: MODO EDUCATIVO
                Rect btn1Rect = GUILayoutUtility.GetRect(new GUIContent(""), _primaryButtonStyle, GUILayout.Height(btnH));
                if (Event.current.type == EventType.Repaint && btn1Rect.width > 4f)
                {
                    Vector2 tlE = GUIUtility.GUIToScreenPoint(btn1Rect.position);
                    LastEducationButtonRect = new Rect(tlE.x, tlE.y, btn1Rect.width, btn1Rect.height);
                }
                if (DrawArcadeButton(btn1Rect, "★  MODO EDUCATIVO  ★", _primaryButtonStyle, isFeatured: false, staggerDelay: 0.06f, onboardingTag: "menu_other"))
                {
                    StartGameWithMode(GameMode.Education);
                }

                GUILayout.Space(spacing);

                // Botón 3: TUTORIAL DEL JUEGO (Centro de Tutoriales: Todo el Juego)
                Rect btnTutRect = GUILayoutUtility.GetRect(new GUIContent(""), _primaryButtonStyle, GUILayout.Height(btnH));
                if (DrawArcadeButton(btnTutRect, "🎓  TUTORIAL DEL JUEGO", _primaryButtonStyle, isFeatured: false, staggerDelay: 0.09f, onboardingTag: "menu_tutorial"))
                {
                    OnboardingManager.EnsureInstance().OpenTutorialHub();
                }

                GUILayout.Space(spacing);

                // Botón 4: ASPECTOS
                Rect btn2Rect = GUILayoutUtility.GetRect(new GUIContent(""), _primaryButtonStyle, GUILayout.Height(btnH));
                if (DrawArcadeButton(btn2Rect, "ASPECTOS (SKINS)", _primaryButtonStyle, isFeatured: false, staggerDelay: 0.12f, onboardingTag: "menu_other"))
                {
                    SwitchPanel(MenuPanel.Skins);
                }

                GUILayout.Space(spacing);

                // Botón 5: OPCIONES
                Rect btn3Rect = GUILayoutUtility.GetRect(new GUIContent(""), _primaryButtonStyle, GUILayout.Height(btnH));
                if (Event.current.type == EventType.Repaint && btn2Rect.width > 4f && btn3Rect.width > 4f)
                {
                    Vector2 tlS = GUIUtility.GUIToScreenPoint(btn2Rect.position);
                    Vector2 tlO = GUIUtility.GUIToScreenPoint(btn3Rect.position);
                    LastSkinsAndOptionsRect = Rect.MinMaxRect(
                        Mathf.Min(tlS.x, tlO.x),
                        Mathf.Min(tlS.y, tlO.y),
                        Mathf.Max(tlS.x + btn2Rect.width, tlO.x + btn3Rect.width),
                        Mathf.Max(tlS.y + btn2Rect.height, tlO.y + btn3Rect.height)
                    );
                }
                if (DrawArcadeButton(btn3Rect, "OPCIONES", _primaryButtonStyle, isFeatured: false, staggerDelay: 0.16f, onboardingTag: "menu_other"))
                {
                    SwitchPanel(MenuPanel.Options);
                }

                GUILayout.Space(spacing);

                // Botón 6: DONACIONES & CRÉDITOS
                Rect btn4Rect = GUILayoutUtility.GetRect(new GUIContent(""), _secondaryButtonStyle, GUILayout.Height(btnH * 0.92f));
                if (DrawArcadeButton(btn4Rect, "DONACIONES & CRÉDITOS", _secondaryButtonStyle, isFeatured: false, staggerDelay: 0.20f, onboardingTag: "menu_other"))
                {
                    SwitchPanel(MenuPanel.Donations);
                }
            }
            GUILayout.EndArea();
        }

        private void RenderQuickMatchSetupPanel(Rect rect, float scale)
        {
            if (_startingCards == null || _startingCards.Count == 0)
            {
                _startingCards = GameManager.GetAvailableStartingCards();
            }

            GUILayout.BeginArea(rect, _cardPanelStyle);
            {
                GUILayout.Space(10 * scale);

                // Encabezado con título y botón de regreso superior rápido
                GUILayout.BeginHorizontal();
                {
                    Rect backTopRect = GUILayoutUtility.GetRect(new GUIContent("◄ MENÚ"), _secondaryButtonStyle, GUILayout.Width(90 * scale), GUILayout.Height(36 * scale));
                    if (DrawArcadeButton(backTopRect, "◄ MENÚ", _secondaryButtonStyle, isFeatured: false, staggerDelay: 0f, onboardingTag: "setup_other"))
                    {
                        SwitchPanel(MenuPanel.Main);
                    }

                    GUILayout.Label("CONFIGURAR PARTIDA", _panelHeaderStyle);
                    GUILayout.Space(90 * scale); // Para centrar el título
                }
                GUILayout.EndHorizontal();

                GUILayout.Label("Ajusta tu carta, bando, reloj y dificultad de IA", _subtitleStyle);
                GUILayout.Space(8 * scale);

                // 1. Selector de Carta Inicial con VISTA PREVIA ARCADE
                CardData currentCard = (_startingCards != null && _quickCardIndex >= 0 && _quickCardIndex < _startingCards.Count)
                    ? _startingCards[_quickCardIndex]
                    : null;

                RenderCardPreviewSelector(scale, currentCard);

                GUILayout.Space(10 * scale);

                // 2. Selector de Bando y Color de Cartas
                GUILayout.Label("Bando y Color de Cartas:", _subtitleStyle);
                GUILayout.BeginHorizontal();
                {
                    bool isWhite = _quickPlayerColor == PieceColor.White;
                    GUIStyle whiteStyle = isWhite ? _featuredButtonStyle : _secondaryButtonStyle;
                    string whiteLabel = isWhite ? "⚪ BLANCAS (Mueves 1°) ✓" : "⚪ BLANCAS (Mueves 1°)";
                    Rect wRect = GUILayoutUtility.GetRect(new GUIContent(""), whiteStyle, GUILayout.Height(44 * scale));
                    if (DrawArcadeButton(wRect, whiteLabel, whiteStyle, isFeatured: isWhite, staggerDelay: 0.05f, onboardingTag: "setup_other"))
                    {
                        _quickPlayerColor = PieceColor.White;
                    }

                    bool isBlack = _quickPlayerColor == PieceColor.Black;
                    GUIStyle blackStyle = isBlack ? _featuredButtonStyle : _secondaryButtonStyle;
                    string blackLabel = isBlack ? "⚫ NEGRAS (IA 1°) ✓" : "⚫ NEGRAS (IA 1°)";
                    Rect bRect = GUILayoutUtility.GetRect(new GUIContent(""), blackStyle, GUILayout.Height(44 * scale));
                    if (DrawArcadeButton(bRect, blackLabel, blackStyle, isFeatured: isBlack, staggerDelay: 0.08f, onboardingTag: "setup_other"))
                    {
                        _quickPlayerColor = PieceColor.Black;
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(10 * scale);

                // 3. Selector de Tiempo de Reloj
                GUILayout.Label("Duración / Reloj de Partida:", _subtitleStyle);
                GUILayout.BeginHorizontal();
                {
                    for (int i = 0; i < _timeLabels.Length; i++)
                    {
                        bool isSel = _quickTimeIndex == i;
                        GUIStyle tStyle = isSel ? _featuredButtonStyle : _secondaryButtonStyle;
                        string tLabel = isSel ? $"{_timeLabels[i]} ✓" : _timeLabels[i];
                        Rect tRect = GUILayoutUtility.GetRect(new GUIContent(""), tStyle, GUILayout.Height(40 * scale));
                        if (DrawArcadeButton(tRect, tLabel, tStyle, isFeatured: isSel, staggerDelay: 0.10f + (i * 0.02f), onboardingTag: "setup_other"))
                        {
                            _quickTimeIndex = i;
                        }
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(10 * scale);

                // 4. Selector de Dificultad IA
                GUILayout.Label("Dificultad de la IA Rival:", _subtitleStyle);
                GUILayout.BeginHorizontal();
                {
                    for (int i = 0; i < _diffLabels.Length; i++)
                    {
                        bool isSel = _quickDiffIndex == i;
                        GUIStyle dStyle = isSel ? _featuredButtonStyle : _secondaryButtonStyle;
                        string dLabel = isSel ? $"{_diffLabels[i]} ✓" : _diffLabels[i];
                        Rect dRect = GUILayoutUtility.GetRect(new GUIContent(""), dStyle, GUILayout.Height(40 * scale));
                        if (DrawArcadeButton(dRect, dLabel, dStyle, isFeatured: isSel, staggerDelay: 0.14f + (i * 0.02f), onboardingTag: "setup_other"))
                        {
                            _quickDiffIndex = i;
                        }
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(14 * scale);

                // 5. Botones de Acción: ¡COMENZAR PARTIDA! (Pulso dorado continuo) y VOLVER AL MENÚ
                Rect startBtnRect = GUILayoutUtility.GetRect(new GUIContent(""), _featuredButtonStyle, GUILayout.Height(62 * scale));
                if (Event.current.type == EventType.Repaint && startBtnRect.width > 4f)
                {
                    Vector2 tl = GUIUtility.GUIToScreenPoint(startBtnRect.position);
                    LastStartMatchButtonRect = new Rect(tl.x, tl.y, startBtnRect.width, startBtnRect.height);
                }
                if (DrawArcadeButton(startBtnRect, "⚔  ¡COMENZAR PARTIDA!  ⚔", _featuredButtonStyle, isFeatured: true, staggerDelay: 0.18f, onboardingTag: "setup_start_match"))
                {
                    ConfirmQuickMatchSetupAndPlay();
                }

                GUILayout.Space(8 * scale);

                Rect backBtnRect = GUILayoutUtility.GetRect(new GUIContent(""), _secondaryButtonStyle, GUILayout.Height(42 * scale));
                if (DrawArcadeButton(backBtnRect, "← VOLVER AL MENÚ", _secondaryButtonStyle, isFeatured: false, staggerDelay: 0.22f, onboardingTag: "setup_other"))
                {
                    SwitchPanel(MenuPanel.Main);
                }
            }
            GUILayout.EndArea();
        }

        /// <summary>
        /// Recuadro de vista previa de carta arcade con relieve 32-bits, aura personalizada por Gran Maestro y balanceo flotante.
        /// </summary>
        private void RenderCardPreviewSelector(float scale, CardData currentCard)
        {
            float rowH = 186f * scale;
            Rect rowRect = GUILayoutUtility.GetRect(0, rowH, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint && rowRect.width > 4f)
            {
                Vector2 tl = GUIUtility.GUIToScreenPoint(rowRect.position);
                LastCardSelectorRect = new Rect(tl.x, tl.y, rowRect.width, rowRect.height);
            }

            Color auraColor = MasterCardDatabase.GetCardAuraColor(currentCard);
            float auraPulse = 0.82f + 0.18f * Mathf.Sin(Time.time * 3.5f);
            Color pulsingAura = new Color(auraColor.r, auraColor.g, auraColor.b, auraPulse);

            // Caja contenedora de la carta con relieve 32-bits teñida por el aura del Gran Maestro
            DrawArcadeBox(rowRect, _cardPanelBgTex != null ? _cardPanelBgTex : _panelBgTex, Color.Lerp(new Color(0.24f, 0.32f, 0.44f, 1f), pulsingAura, 0.55f), 2f, 3f);

            // 1. Recuadro de Vista Previa de la Carta (Izquierda) con aura brillante y balanceo flotante (bobbing)
            float cardPreviewW = 112f * scale;
            float cardPreviewH = 166f * scale;
            float bobY = Mathf.Sin(Time.time * 2.4f) * (3f * scale);
            Rect cardRect = new Rect(rowRect.x + (10f * scale), rowRect.y + (9f * scale) + bobY, cardPreviewW, cardPreviewH);

            Color oldCol = GUI.color;
            // Halo de aura exterior
            GUI.color = new Color(auraColor.r, auraColor.g, auraColor.b, 0.22f * auraPulse);
            GUI.DrawTexture(new Rect(cardRect.x - 4f, cardRect.y - 4f, cardRect.width + 8f, cardRect.height + 8f), Texture2D.whiteTexture);
            GUI.color = oldCol;

            DrawArcadeBox(cardRect, _cardFrameTex != null ? _cardFrameTex : _panelBgTex, pulsingAura, 2.5f, 3f);

            // Coste e índice de Gran Maestro en la barra superior de la mini-carta
            float headerH = 22f * scale;
            Rect headerRect = new Rect(cardRect.x + 2, cardRect.y + 2, cardRect.width - 4, headerH);
            GUI.color = auraColor * 0.82f;
            GUI.DrawTexture(headerRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            int totalCount = _startingCards != null ? _startingCards.Count : 0;
            int currentIdxDisplay = totalCount > 0 ? (Mathf.Clamp(_quickCardIndex, 0, totalCount - 1) + 1) : 1;
            string costStr = currentCard != null ? $"${currentCard.Cost}" : "$1";
            if (_miniCostStyle != null)
            {
                GUIStyle idxStyle = new GUIStyle(_miniCostStyle) { alignment = TextAnchor.MiddleLeft };
                GUI.Label(new Rect(headerRect.x + 4, headerRect.y, headerRect.width * 0.5f, headerH), $"{currentIdxDisplay}/{Mathf.Max(1, totalCount)}", idxStyle);
                GUI.Label(new Rect(headerRect.x, headerRect.y, headerRect.width - 4, headerH), costStr, _miniCostStyle);
            }

            // Sprite de arte (con relación de aspecto preservada)
            float artW = cardPreviewW - (12f * scale);
            float artH = cardPreviewH - headerH - (32f * scale);
            Rect artRect = new Rect(cardRect.x + (6f * scale), cardRect.y + headerH + (4f * scale), artW, artH);
            GUI.color = new Color(0.04f, 0.06f, 0.09f, 0.96f);
            GUI.DrawTexture(artRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            Sprite cardSprite = GetCardPreviewSprite(currentCard);
            if (cardSprite != null && cardSprite.texture != null)
            {
                DrawSpriteInGui(new Rect(artRect.x + 2, artRect.y + 2, artRect.width - 4, artRect.height - 4), cardSprite);
            }

            // Etiqueta de subtítulo/habilidad en la base de la carta
            string subtitleStr = MasterCardDatabase.GetCardSubtitle(currentCard);
            string bottomTag = !string.IsNullOrEmpty(subtitleStr)
                ? subtitleStr.ToUpperInvariant()
                : (currentCard != null ? currentCard.Rarity.ToString().ToUpperInvariant() : "LEYENDA");
            Rect rarityRect = new Rect(cardRect.x + 2, cardRect.yMax - (22f * scale), cardRect.width - 4, 20f * scale);
            GUI.color = pulsingAura;
            if (_miniRarityStyle != null)
            {
                GUI.Label(rarityRect, bottomTag, _miniRarityStyle);
            }
            GUI.color = oldCol;

            // 2. Columna de Navegación (< >) y Datos del Gran Maestro (Derecha)
            float rightX = cardRect.xMax + (14f * scale);
            float rightW = rowRect.xMax - rightX - (10f * scale);
            float rightY = rowRect.y + (8f * scale);

            float navBtnSize = 38f * scale;
            Rect prevBtnRect = new Rect(rightX, rightY, navBtnSize, navBtnSize);
            if (DrawArcadeButton(prevBtnRect, "◄", _secondaryButtonStyle, isFeatured: false, staggerDelay: 0f, onboardingTag: "setup_other"))
            {
                if (_startingCards != null && _startingCards.Count > 0)
                {
                    _quickCardIndex = (_quickCardIndex - 1 + _startingCards.Count) % _startingCards.Count;
                }
            }

            Rect nextBtnRect = new Rect(rightX + rightW - navBtnSize, rightY, navBtnSize, navBtnSize);
            if (DrawArcadeButton(nextBtnRect, "►", _secondaryButtonStyle, isFeatured: false, staggerDelay: 0f, onboardingTag: "setup_other"))
            {
                if (_startingCards != null && _startingCards.Count > 0)
                {
                    _quickCardIndex = (_quickCardIndex + 1) % _startingCards.Count;
                }
            }

            float nameW = rightW - (navBtnSize * 2f + (8f * scale));
            Rect nameRect = new Rect(prevBtnRect.xMax + (4f * scale), rightY, nameW, navBtnSize);
            string cardName = currentCard != null ? currentCard.CardName : "Carta";
            if (_cardTitleStyle != null)
            {
                GUIStyle dynamicTitleStyle = new GUIStyle(_cardTitleStyle)
                {
                    wordWrap = true,
                    fontSize = Mathf.RoundToInt(15 * scale),
                    normal = { textColor = pulsingAura }
                };
                GUI.Label(nameRect, $"★ {cardName} ★", dynamicTitleStyle);
            }

            float badgeY = rightY + navBtnSize + (4f * scale);
            Rect badgeRect = new Rect(rightX, badgeY, rightW, 20f * scale);
            string abilityLine = string.IsNullOrEmpty(subtitleStr)
                ? $"Gran Maestro {currentIdxDisplay}/{Mathf.Max(1, totalCount)}"
                : $"✦ {subtitleStr.ToUpperInvariant()}  •  GRAN MAESTRO ({currentIdxDisplay}/{Mathf.Max(1, totalCount)})";
            GUI.color = pulsingAura;
            GUIStyle abilityStyle = new GUIStyle(_miniRarityStyle)
            {
                fontSize = Mathf.RoundToInt(11 * scale),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            GUI.Label(badgeRect, abilityLine, abilityStyle);
            GUI.color = oldCol;

            float descY = badgeY + (22f * scale);
            float descH = rowRect.yMax - descY - (8f * scale);
            Rect descRect = new Rect(rightX, descY, rightW, descH);
            string cardDesc = currentCard != null ? currentCard.Description : "";
            GUIStyle descStyle = new GUIStyle(_labelStyle)
            {
                fontSize = Mathf.RoundToInt(14 * scale),
                alignment = TextAnchor.UpperLeft
            };
            GUI.Label(descRect, cardDesc, descStyle);
        }

        #region Helpers de Renderizado Arcade 32-Bits

        /// <summary>
        /// Dibuja una caja contenedora estilo arcade con sombra dura inferior-derecha (pixel art) y bisel de relieve.
        /// </summary>
        private void DrawArcadeBox(Rect rect, Texture2D bgTex, Color borderColor, float borderWidth = 2f, float shadowDist = 3f)
        {
            Color oldCol = GUI.color;

            // 1. Sombra dura 32-bits
            Rect shadowRect = new Rect(rect.x + shadowDist, rect.y + shadowDist, rect.width, rect.height);
            GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.88f);
            GUI.DrawTexture(shadowRect, Texture2D.whiteTexture);

            // 2. Fondo
            GUI.color = Color.white;
            GUI.DrawTexture(rect, bgTex != null ? bgTex : Texture2D.whiteTexture);

            // 3. Bordes de 2px
            GUI.color = borderColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, borderWidth), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - borderWidth, rect.width, borderWidth), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, borderWidth, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - borderWidth, rect.y, borderWidth, rect.height), Texture2D.whiteTexture);

            // 4. Bisel superior/izquierdo para relieve
            Color highlight = borderColor + new Color(0.20f, 0.20f, 0.25f, 0f);
            highlight.a = 0.5f;
            GUI.color = highlight;
            GUI.DrawTexture(new Rect(rect.x + borderWidth, rect.y + borderWidth, rect.width - 2 * borderWidth, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + borderWidth, rect.y + borderWidth, 1, rect.height - 2 * borderWidth), Texture2D.whiteTexture);

            GUI.color = oldCol;
        }

        /// <summary>
        /// Dibuja un botón interactivo con sombra dura, elevación de 2px en hover, pulso en featured y entrada escalonada.
        /// </summary>
        private bool DrawArcadeButton(Rect rect, string text, GUIStyle style, bool isFeatured = false, float staggerDelay = 0f, string onboardingTag = null)
        {
            float scale = ChessFontProvider.GetResponsiveScale();
            float elapsed = Time.realtimeSinceStartup - _panelEntranceStartTime;

            // Entrada escalonada (staggered)
            float t = Mathf.Clamp01((elapsed - staggerDelay) / 0.22f);
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            rect.y += (1f - ease) * (20f * scale);

            // Detección de Hover
            bool isHovered = rect.Contains(Event.current.mousePosition);
            if (isHovered)
            {
                rect.y -= 2f * scale; // Desplazamiento hacia arriba
            }

            // Pulso rítmico continuo en botones principales ("JUGAR", "¡COMENZAR PARTIDA!")
            if (isFeatured)
            {
                float pulse = Mathf.Sin(Time.time * 3.8f) * (1.5f * scale);
                rect.x -= pulse;
                rect.width += pulse * 2f;
                rect.y -= pulse * 0.5f;
                rect.height += pulse;
            }

            // Sombra dura inferior-derecha (estilo pixel art)
            Rect shadowRect = new Rect(rect.x + 3, rect.y + 3, rect.width, rect.height);
            Color oldCol = GUI.color;
            GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.88f * t);
            GUI.DrawTexture(shadowRect, Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, t);
            bool allowed = string.IsNullOrEmpty(onboardingTag) || OnboardingManager.IsImGuiButtonAllowed(onboardingTag);
            bool clicked = GUI.Button(rect, text, style) && allowed;

            // Borde duro iluminado en Hover
            if (isHovered && t >= 0.85f)
            {
                GUI.color = isFeatured ? new Color(1f, 0.95f, 0.55f, 0.9f) : new Color(0.6f, 0.85f, 1f, 0.85f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2, rect.width, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 2, rect.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.xMax - 2, rect.y, 2, rect.height), Texture2D.whiteTexture);
            }

            GUI.color = oldCol;
            return clicked;
        }

        private Sprite GetCardPreviewSprite(CardData card)
        {
            if (card == null) return null;
            Sprite masterSpr = MasterCardDatabase.ResolveSprite(card);
            if (masterSpr != null) return masterSpr;

            // Fallback a sprites de fichas del tema activo
            string name = card.CardName.ToLowerInvariant();
            PieceType type = PieceType.King;
            if (name.Contains("peón") || name.Contains("peon")) type = PieceType.Pawn;
            else if (name.Contains("caball") || name.Contains("salto")) type = PieceType.Knight;
            else if (name.Contains("alfil")) type = PieceType.Bishop;
            else if (name.Contains("torre")) type = PieceType.Rook;
            else if (name.Contains("dama") || name.Contains("reina")) type = PieceType.Queen;

            if (PieceSkinManager.Instance != null && PieceSkinManager.Instance.ActiveTheme != null)
            {
                Sprite s = PieceSkinManager.Instance.ActiveTheme.GetSprite(type, PieceColor.White);
                if (s != null) return s;
            }

            BoardView bv = FindAnyObjectByType<BoardView>();
            if (bv != null && bv.PieceTheme != null)
            {
                Sprite s = bv.PieceTheme.GetSprite(type, PieceColor.White);
                if (s != null) return s;
            }

            string spriteName = type switch
            {
                PieceType.Pawn => "white_pawn",
                PieceType.Knight => "white_knight",
                PieceType.Bishop => "white_bishop",
                PieceType.Rook => "white_rook",
                PieceType.Queen => "white_queen",
                PieceType.King => "white_king",
                _ => null
            };
            if (!string.IsNullOrEmpty(spriteName))
            {
                Sprite s = Resources.Load<Sprite>($"Pieces/{spriteName}");
                if (s != null) return s;
            }

            return null;
        }

        private static void DrawSpriteInGui(Rect screenRect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;

            Rect tr = sprite.rect;
            Rect uv = new Rect(
                tr.x / sprite.texture.width,
                tr.y / sprite.texture.height,
                tr.width / sprite.texture.width,
                tr.height / sprite.texture.height
            );

            float spriteAspect = tr.width / Mathf.Max(1f, tr.height);
            float rectAspect = screenRect.width / Mathf.Max(1f, screenRect.height);
            Rect drawRect = screenRect;
            if (spriteAspect > rectAspect)
            {
                float h = screenRect.width / spriteAspect;
                drawRect = new Rect(screenRect.x, screenRect.y + (screenRect.height - h) * 0.5f, screenRect.width, h);
            }
            else
            {
                float w = screenRect.height * spriteAspect;
                drawRect = new Rect(screenRect.x + (screenRect.width - w) * 0.5f, screenRect.y, w, screenRect.height);
            }

            GUI.DrawTextureWithTexCoords(drawRect, sprite.texture, uv, true);
        }

        private static Color GetRarityAccentColor(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return new Color(0.24f, 0.55f, 0.95f, 1.0f);
                case CardRarity.Uncommon: return new Color(0.12f, 0.75f, 0.45f, 1.0f);
                case CardRarity.Rare: return new Color(0.95f, 0.28f, 0.28f, 1.0f);
                case CardRarity.Legendary: return new Color(0.72f, 0.32f, 0.98f, 1.0f);
                default: return Color.white;
            }
        }

        #endregion

        private void ConfirmQuickMatchSetupAndPlay()
        {
            if (_startingCards == null || _startingCards.Count == 0)
            {
                _startingCards = GameManager.GetAvailableStartingCards();
            }

            if (GameManager.CurrentMatchConfig == null)
            {
                GameManager.CurrentMatchConfig = new MatchConfig();
            }

            GameManager.CurrentMatchConfig.startingCard = (_startingCards != null && _quickCardIndex >= 0 && _quickCardIndex < _startingCards.Count)
                ? _startingCards[_quickCardIndex]
                : null;
            if (GameManager.CurrentMatchConfig.startingCard != null)
            {
                GameManager.CurrentMatchConfig.startingCard.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                GameManager.CurrentMatchConfig.startingMasterName = GameManager.CurrentMatchConfig.startingCard.CardName;
            }
            else
            {
                GameManager.CurrentMatchConfig.startingMasterName = null;
            }

            GameManager.CurrentMatchConfig.playerColor = _quickPlayerColor;
            GameManager.CurrentMatchConfig.timeLimitSeconds = _timeValues[Mathf.Clamp(_quickTimeIndex, 0, _timeValues.Length - 1)];
            GameManager.CurrentMatchConfig.aiDifficulty = _diffValues[Mathf.Clamp(_quickDiffIndex, 0, _diffValues.Length - 1)];

            Debug.Log($"<color=green><b>[MainMenuManager] Configuración de partida confirmada: Carta={GameManager.CurrentMatchConfig.startingCard?.CardName}, Bando={GameManager.CurrentMatchConfig.playerColor}, Tiempo={GameManager.CurrentMatchConfig.timeLimitSeconds}s, IA={GameManager.CurrentMatchConfig.aiDifficulty}</b></color>");

            OnboardingManager.Instance?.NotifyQuickMatchStartClicked();
            StartGameWithMode(GameMode.ClassicRoguelike);
        }

        private void RenderModesPanel(Rect rect, float scale)
        {
            GUILayout.BeginArea(rect, _cardPanelStyle);
            {
                GUILayout.Space(16 * scale);
                GUILayout.Label("SELECCIÓN DE MODO", _panelHeaderStyle);
                GUILayout.Space(16 * scale);

                float gap = Mathf.Clamp(12f * scale, 8f, 18f);
                float availH = Mathf.Max(260f, rect.height - (110f * scale));
                float btnH = Mathf.Clamp((availH - gap * 4f) / 5.1f, 44f, 82f * scale);

                // Modo Clásico / Roguelike
                if (GUILayout.Button("MODO CLÁSICO / ROGUELIKE\n<size=14>Barajas de cartas, reliquias y Grandes Maestros</size>", _primaryButtonStyle, GUILayout.Height(btnH * 1.05f)))
                {
                    StartGameWithMode(GameMode.ClassicRoguelike);
                }

                GUILayout.Space(gap);

                // Modo Educativo (Destacado)
                if (GUILayout.Button("★  MODO EDUCATIVO (APRENDE DESDE CERO)  ★\n<size=14>Lecciones interactivas: Torre, Alfil, Caballo, Peón, Dama y Petrosián</size>", _featuredButtonStyle, GUILayout.Height(btnH * 1.12f)))
                {
                    StartGameWithMode(GameMode.Education);
                }

                GUILayout.Space(gap);

                // Tutorial Completo del Juego (Centro de Tutoriales: Todo el Juego)
                if (GUILayout.Button("🎓  TUTORIAL DEL JUEGO\n<size=14>Guías de TODO: Clásico, Arcade Survivors, Academia, Maestros y Cartas</size>", _primaryButtonStyle, GUILayout.Height(btnH * 1.05f)))
                {
                    OnboardingManager.EnsureInstance().OpenTutorialHub();
                }

                GUILayout.Space(gap);

                // Modo Arcade (Chess Survivors - Asedio de 15 Minutos)
                if (GUILayout.Button("⚡  MODO ARCADE (ASEDIO 15 MIN)  ⚡\n<size=14>Tablero 8×8→8×20 · Captura en 1 movimiento, Aliados, Sinergias y Coloso Real</size>", _featuredButtonStyle, GUILayout.Height(btnH * 1.02f)))
                {
                    StartGameWithMode(GameMode.Arcade);
                }

                GUILayout.Space(gap * 1.2f);

                if (GUILayout.Button("◄ VOLVER", _secondaryButtonStyle, GUILayout.Height(btnH * 0.85f)))
                {
                    SwitchPanel(MenuPanel.Main);
                }
            }
            GUILayout.EndArea();
        }

        private void RenderSkinsPanel(Rect rect, float scale)
        {
            GUILayout.BeginArea(rect, _cardPanelStyle);
            {
                GUILayout.Space(25 * scale);
                GUILayout.Label("ASPECTOS DE PIEZAS", _titleStyle);
                GUILayout.Space(20 * scale);

                if (PieceSkinManager.Instance != null)
                {
                    PieceSpriteLibrary preview = PieceSkinManager.Instance.CurrentPreviewSkin;
                    bool isEquipped = PieceSkinManager.Instance.ActiveSkinIndex == PieceSkinManager.Instance.PreviewSkinIndex;

                    string skinTitle = preview != null ? preview.SkinName : "Set Clásico";
                    string skinDesc = preview != null ? preview.Description : "";

                    GUILayout.Label($"<b>{skinTitle}</b>", _subtitleStyle);
                    GUILayout.Space(10 * scale);
                    GUILayout.Label(skinDesc, _labelStyle);
                    GUILayout.Space(25 * scale);

                    // Controles Anterior / Siguiente
                    GUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button("◄ ANTERIOR", _secondaryButtonStyle, GUILayout.Height(70 * scale)))
                        {
                            PieceSkinManager.Instance.PreviousPreview();
                        }

                        string equipText = isEquipped ? "EQUIPADO ✓" : "EQUIPAR";
                        GUI.enabled = !isEquipped;
                        if (GUILayout.Button(equipText, isEquipped ? _disabledButtonStyle : _featuredButtonStyle, GUILayout.Height(70 * scale)))
                        {
                            PieceSkinManager.Instance.EquipCurrentPreview();
                        }
                        GUI.enabled = true;

                        if (GUILayout.Button("SIGUIENTE ►", _secondaryButtonStyle, GUILayout.Height(70 * scale)))
                        {
                            PieceSkinManager.Instance.NextPreview();
                        }
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(40 * scale);

                if (GUILayout.Button("◄ VOLVER AL MENÚ", _secondaryButtonStyle, GUILayout.Height(75 * scale)))
                {
                    SwitchPanel(MenuPanel.Main);
                }
            }
            GUILayout.EndArea();
        }

        private void RenderOptionsPanel(Rect rect, float scale)
        {
            GUILayout.BeginArea(rect, _cardPanelStyle);
            {
                GUILayout.Space(25 * scale);
                GUILayout.Label("CONFIGURACIÓN Y OPCIONES", _titleStyle);
                GUILayout.Space(30 * scale);

                if (SettingsManager.Instance != null)
                {
                    // Música
                    GUILayout.Label($"Volumen Música: {Mathf.RoundToInt(SettingsManager.Instance.MusicVolume * 100)}%", _subtitleStyle);
                    float newMusic = GUILayout.HorizontalSlider(SettingsManager.Instance.MusicVolume, 0f, 1f, GUILayout.Height(45 * scale));
                    if (Mathf.Abs(newMusic - SettingsManager.Instance.MusicVolume) > 0.01f)
                    {
                        SettingsManager.Instance.SetMusicVolume(newMusic);
                    }

                    GUILayout.Space(25 * scale);

                    // Efectos SFX
                    GUILayout.Label($"Volumen Efectos SFX: {Mathf.RoundToInt(SettingsManager.Instance.SFXVolume * 100)}%", _subtitleStyle);
                    float newSFX = GUILayout.HorizontalSlider(SettingsManager.Instance.SFXVolume, 0f, 1f, GUILayout.Height(45 * scale));
                    if (Mathf.Abs(newSFX - SettingsManager.Instance.SFXVolume) > 0.01f)
                    {
                        SettingsManager.Instance.SetSFXVolume(newSFX);
                    }

                    GUILayout.Space(30 * scale);

                    // Vibración / Hápticos
                    bool vib = SettingsManager.Instance.IsVibrationEnabled;
                    bool newVib = GUILayout.Toggle(vib, "  Vibración / Respuesta Háptica (Móvil)", _subtitleStyle);
                    if (newVib != vib)
                    {
                        SettingsManager.Instance.SetVibration(newVib);
                    }
                }

                GUILayout.Space(45 * scale);

                if (GUILayout.Button("◄ GUARDAR Y VOLVER", _primaryButtonStyle, GUILayout.Height(75 * scale)))
                {
                    SwitchPanel(MenuPanel.Main);
                }
            }
            GUILayout.EndArea();
        }

        private void RenderDonationsModal(float screenW, float screenH, float scale)
        {
            GUI.depth = -100;
            GUI.DrawTexture(new Rect(0, 0, screenW, screenH), _modalBgTex);

            float modalW = screenW * 0.90f;
            float modalH = 1050f * scale;
            float modalX = (screenW - modalW) * 0.5f;
            float modalY = (screenH - modalH) * 0.5f;

            GUILayout.BeginArea(new Rect(modalX, modalY, modalW, modalH), _cardPanelStyle);
            {
                GUILayout.Space(25 * scale);
                GUILayout.Label("¡APOYA EL DESARROLLO!", _titleStyle);
                GUILayout.Space(15 * scale);

                string donationText = "ChessMatch es un proyecto independiente creado con amor al ajedrez y pasión por los roguelikes tácticos.\n\n" +
                                     "Tu donación o soporte nos ayuda a mantener el juego sin anuncios abusivos, expandir el Modo Educativo, " +
                                     "diseñar nuevos sets de cartas y agregar más Grandes Maestros legendarios.\n\n" +
                                     "<b>Créditos del Proyecto:</b>\n" +
                                     "• Lead Game Programmer & Systems Architecture\n" +
                                     "• Game Design & Rogue-Deckbuilder Mechanics\n" +
                                     "• Inspiración: Balatro & Campeones Mundiales de Ajedrez";

                GUILayout.Label(donationText, _labelStyle);
                GUILayout.Space(25 * scale);

                float btnH = 68 * scale;

                if (GUILayout.Button("☕ Donar en Ko-fi", _featuredButtonStyle, GUILayout.Height(btnH)))
                {
                    Application.OpenURL(kofiUrl);
                }

                GUILayout.Space(12 * scale);

                if (GUILayout.Button("♥ Apoyar en Patreon", _primaryButtonStyle, GUILayout.Height(btnH)))
                {
                    Application.OpenURL(patreonUrl);
                }

                GUILayout.Space(12 * scale);

                if (GUILayout.Button("🌐 Visitar en Itch.io / Comunidad", _secondaryButtonStyle, GUILayout.Height(btnH)))
                {
                    Application.OpenURL(webUrl);
                }

                GUILayout.Space(25 * scale);

                if (GUILayout.Button("CERRAR", _secondaryButtonStyle, GUILayout.Height(65 * scale)))
                {
                    SwitchPanel(MenuPanel.Main);
                }
            }
            GUILayout.EndArea();
        }

        private void InitOnGuiStyles()
        {
            float scale = ChessFontProvider.GetResponsiveScale();

            if (_panelBgTex == null)
            {
                _panelBgTex = MakeColorTexture(2, 2, new Color(0.10f, 0.12f, 0.16f, 0.95f));
                _cardPanelBgTex = MakeColorTexture(2, 2, new Color(0.08f, 0.10f, 0.15f, 0.98f));
                _cardFrameTex = MakeColorTexture(2, 2, new Color(0.06f, 0.08f, 0.12f, 1f));
                _modalBgTex = MakeColorTexture(2, 2, new Color(0f, 0f, 0f, 0.85f));
                _primaryBtnTex = MakeColorTexture(2, 2, new Color(0.18f, 0.35f, 0.55f, 1f));
                _secondaryBtnTex = MakeColorTexture(2, 2, new Color(0.24f, 0.28f, 0.36f, 1f));
                _featuredBtnTex = MakeColorTexture(2, 2, new Color(0.85f, 0.65f, 0.15f, 1f));
                _disabledBtnTex = MakeColorTexture(2, 2, new Color(0.20f, 0.20f, 0.22f, 0.6f));
                _fadeTexture = MakeColorTexture(2, 2, Color.black);
            }

            Font boldSerif = ChessFontProvider.SerifBold;
            Font regularSerif = ChessFontProvider.SerifRegular;

            _cardPanelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _panelBgTex },
                padding = new RectOffset((int)(24 * scale), (int)(24 * scale), (int)(20 * scale), (int)(20 * scale))
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = boldSerif,
                fontSize = Mathf.RoundToInt(40 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.96f, 0.86f, 0.38f, 1f) }
            };

            _titleShadowStyle = new GUIStyle(_titleStyle)
            {
                normal = { textColor = new Color(0.04f, 0.05f, 0.08f, 0.90f) }
            };

            _cardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = boldSerif,
                fontSize = Mathf.RoundToInt(17 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.96f, 0.86f, 0.38f, 1f) }
            };

            _miniCostStyle = new GUIStyle(GUI.skin.label)
            {
                font = boldSerif,
                fontSize = Mathf.RoundToInt(13 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.98f, 0.95f, 0.40f, 1f) }
            };

            _miniRarityStyle = new GUIStyle(GUI.skin.label)
            {
                font = boldSerif,
                fontSize = Mathf.RoundToInt(10 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = regularSerif,
                fontSize = Mathf.RoundToInt(18 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.90f, 0.95f, 1f) }
            };

            _panelHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                font = boldSerif,
                fontSize = Mathf.RoundToInt(26 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.96f, 0.86f, 0.38f, 1f) }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = regularSerif,
                fontSize = Mathf.RoundToInt(16 * scale),
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.90f, 0.92f, 0.94f, 1f) }
            };

            _primaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                font = boldSerif,
                fontSize = Mathf.RoundToInt(21 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _primaryBtnTex, textColor = Color.white }
            };

            _secondaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                font = boldSerif,
                fontSize = Mathf.RoundToInt(18 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _secondaryBtnTex, textColor = new Color(0.92f, 0.94f, 0.96f, 1f) }
            };

            _featuredButtonStyle = new GUIStyle(GUI.skin.button)
            {
                font = boldSerif,
                fontSize = Mathf.RoundToInt(23 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _featuredBtnTex, textColor = new Color(0.12f, 0.08f, 0.02f, 1f) }
            };

            _disabledButtonStyle = new GUIStyle(GUI.skin.button)
            {
                font = regularSerif,
                fontSize = Mathf.RoundToInt(18 * scale),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _disabledBtnTex, textColor = new Color(0.6f, 0.6f, 0.6f, 1f) }
            };

            _onGuiInitialized = true;
        }

        private Texture2D MakeColorTexture(int width, int height, Color color)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void DestroyCachedTextures()
        {
            if (_panelBgTex != null) Destroy(_panelBgTex);
            if (_cardPanelBgTex != null) Destroy(_cardPanelBgTex);
            if (_cardFrameTex != null) Destroy(_cardFrameTex);
            if (_modalBgTex != null) Destroy(_modalBgTex);
            if (_primaryBtnTex != null) Destroy(_primaryBtnTex);
            if (_secondaryBtnTex != null) Destroy(_secondaryBtnTex);
            if (_featuredBtnTex != null) Destroy(_featuredBtnTex);
            if (_disabledBtnTex != null) Destroy(_disabledBtnTex);
            if (_fadeTexture != null) Destroy(_fadeTexture);
        }

        #endregion
    }
}


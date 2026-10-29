using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChessBalatro
{
    /// <summary>
    /// Componente de interfaz de usuario para seleccionar el nivel de dificultad de la IA rival (Easy, Medium, Hard).
    /// Soporta tanto componentes uGUI (Dropdown / Botones individuales) como renderizado procedural OnGUI instantáneo.
    /// Comunica los cambios a BoardManager y BoardInputHandler en tiempo de ejecución o antes de iniciar partida.
    /// </summary>
    public class DifficultySelectorUI : MonoBehaviour
    {
        [Header("Referencias de Gestión")]
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private BoardInputHandler inputHandler;

        [Header("Componentes uGUI Opcionales")]
        [SerializeField] private Dropdown difficultyDropdown;
        [SerializeField] private Button easyButton;
        [SerializeField] private Button mediumButton;
        [SerializeField] private Button hardButton;
        [SerializeField] private Text currentDifficultyText;

        [Header("Visualización OnGUI en Pantalla")]
        [SerializeField] private bool showOnScreenUI = true;
        [SerializeField] private Vector2 onScreenPosition = new Vector2(16f, 16f); // Esquina superior derecha (offset desde borde derecho)

        public AIDifficulty CurrentDifficulty => _currentDifficulty;
        public event Action<AIDifficulty> OnDifficultySelected;

        private AIDifficulty _currentDifficulty = AIDifficulty.Medium;

        // Estilos para OnGUI
        private GUIStyle _panelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _buttonActiveStyle;
        private GUIStyle _buttonInactiveStyle;
        private GUIStyle _subtextStyle;
        private Texture2D _panelBgTex;
        private Texture2D _btnActiveBgTex;
        private Texture2D _btnInactiveBgTex;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            ResolveReferences();
            SyncCurrentDifficultyFromManagers();
            SetupUguiListeners();
            UpdateVisuals();
        }

        private void OnDestroy()
        {
            RemoveUguiListeners();
            DestroyCachedTextures();
        }

        private void ResolveReferences()
        {
            if (boardManager == null) boardManager = FindAnyObjectByType<BoardManager>();
            if (inputHandler == null && boardManager != null) inputHandler = boardManager.InputHandler;
            if (inputHandler == null) inputHandler = FindAnyObjectByType<BoardInputHandler>();
        }

        private void SyncCurrentDifficultyFromManagers()
        {
            if (inputHandler != null)
            {
                _currentDifficulty = inputHandler.AIDifficulty;
            }
            else if (boardManager != null)
            {
                _currentDifficulty = boardManager.AIDifficulty;
            }
        }

        private void SetupUguiListeners()
        {
            if (difficultyDropdown != null)
            {
                difficultyDropdown.ClearOptions();
                difficultyDropdown.AddOptions(new System.Collections.Generic.List<string> { "Fácil", "Media", "Difícil" });
                difficultyDropdown.value = (int)_currentDifficulty;
                difficultyDropdown.onValueChanged.AddListener(HandleDropdownChanged);
            }

            if (easyButton != null)
                easyButton.onClick.AddListener(() => SelectDifficulty(AIDifficulty.Easy));

            if (mediumButton != null)
                mediumButton.onClick.AddListener(() => SelectDifficulty(AIDifficulty.Medium));

            if (hardButton != null)
                hardButton.onClick.AddListener(() => SelectDifficulty(AIDifficulty.Hard));

            if (boardManager != null)
            {
                boardManager.OnDifficultyChanged -= HandleExternalDifficultyChange;
                boardManager.OnDifficultyChanged += HandleExternalDifficultyChange;
            }

            if (inputHandler != null)
            {
                inputHandler.OnAIDifficultyChanged -= HandleExternalDifficultyChange;
                inputHandler.OnAIDifficultyChanged += HandleExternalDifficultyChange;
            }
        }

        private void RemoveUguiListeners()
        {
            if (difficultyDropdown != null)
            {
                difficultyDropdown.onValueChanged.RemoveListener(HandleDropdownChanged);
            }

            if (easyButton != null) easyButton.onClick.RemoveAllListeners();
            if (mediumButton != null) mediumButton.onClick.RemoveAllListeners();
            if (hardButton != null) hardButton.onClick.RemoveAllListeners();

            if (boardManager != null)
            {
                boardManager.OnDifficultyChanged -= HandleExternalDifficultyChange;
            }

            if (inputHandler != null)
            {
                inputHandler.OnAIDifficultyChanged -= HandleExternalDifficultyChange;
            }
        }

        private void HandleDropdownChanged(int index)
        {
            SelectDifficulty((AIDifficulty)index);
        }

        private void HandleExternalDifficultyChange(AIDifficulty newDifficulty)
        {
            if (_currentDifficulty != newDifficulty)
            {
                _currentDifficulty = newDifficulty;
                UpdateVisuals();
            }
        }

        /// <summary>
        /// Selecciona y aplica el nuevo nivel de dificultad en la IA del juego.
        /// </summary>
        public void SelectDifficulty(AIDifficulty difficulty)
        {
            _currentDifficulty = difficulty;

            if (inputHandler != null)
            {
                inputHandler.AIDifficulty = difficulty;
            }

            if (boardManager != null)
            {
                boardManager.AIDifficulty = difficulty;
            }

            UpdateVisuals();
            OnDifficultySelected?.Invoke(difficulty);

            Debug.Log($"<color=cyan><b>[DifficultySelectorUI]</b> Dificultad IA establecida a: <b>{difficulty}</b>.</color>");
        }

        /// <summary>
        /// Sobrecarga para enlazar con eventos de índice numérico (por ejemplo Dropdown de uGUI).
        /// </summary>
        public void SelectDifficulty(int index)
        {
            if (index >= 0 && index <= 2)
            {
                SelectDifficulty((AIDifficulty)index);
            }
        }

        private void UpdateVisuals()
        {
            if (difficultyDropdown != null && difficultyDropdown.value != (int)_currentDifficulty)
            {
                difficultyDropdown.SetValueWithoutNotify((int)_currentDifficulty);
            }

            if (currentDifficultyText != null)
            {
                currentDifficultyText.text = GetDifficultyDisplayName(_currentDifficulty);
                currentDifficultyText.color = GetDifficultyColor(_currentDifficulty);
            }
        }

        public static string GetDifficultyDisplayName(AIDifficulty difficulty) => difficulty switch
        {
            AIDifficulty.Easy => "Fácil",
            AIDifficulty.Medium => "Media",
            AIDifficulty.Hard => "Difícil",
            _ => difficulty.ToString()
        };

        public static string GetDifficultyDescription(AIDifficulty difficulty) => difficulty switch
        {
            AIDifficulty.Easy => "Aleatoria con 70% prioridad a capturas",
            AIDifficulty.Medium => "Minimax (prof. 2) evaluando material",
            AIDifficulty.Hard => "Alfa-Beta (prof. 3) + control central",
            _ => string.Empty
        };

        public static Color GetDifficultyColor(AIDifficulty difficulty) => difficulty switch
        {
            AIDifficulty.Easy => new Color(0.30f, 0.78f, 0.45f),     // Verde esmeralda
            AIDifficulty.Medium => new Color(0.96f, 0.72f, 0.18f),   // Ámbar dorado
            AIDifficulty.Hard => new Color(0.95f, 0.28f, 0.28f),     // Carmesí intenso
            _ => Color.white
        };

        #region Renderizado OnGUI Procedural (Zero-Setup)

        private void InitStylesIfNeeded()
        {
            if (_panelStyle != null) return;

            _panelBgTex = MakeColorTex(new Color(0.08f, 0.10f, 0.14f, 0.92f));
            _btnActiveBgTex = MakeColorTex(new Color(0.20f, 0.24f, 0.32f, 0.95f));
            _btnInactiveBgTex = MakeColorTex(new Color(0.12f, 0.14f, 0.18f, 0.85f));

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _panelBgTex },
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.88f, 0.92f, 0.98f) }
            };

            _subtextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.70f, 0.75f, 0.85f) }
            };

            _buttonActiveStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = _btnActiveBgTex, textColor = Color.white },
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _buttonInactiveStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = _btnInactiveBgTex, textColor = new Color(0.65f, 0.70f, 0.80f) },
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void OnGUI()
        {
            if (!showOnScreenUI) return;

            InitStylesIfNeeded();

            float panelWidth = 260f;
            float panelHeight = 84f;
            float posX = Screen.width - panelWidth - onScreenPosition.x;
            float posY = onScreenPosition.y;

            Rect panelRect = new Rect(posX, posY, panelWidth, panelHeight);
            GUI.Box(panelRect, GUIContent.none, _panelStyle);

            GUILayout.BeginArea(panelRect);

            // Encabezado
            GUILayout.BeginHorizontal();
            GUILayout.Label("⚔ DIFICULTAD IA (NEGRAS)", _headerStyle);
            GUILayout.FlexibleSpace();
            Color activeColor = GetDifficultyColor(_currentDifficulty);
            GUI.color = activeColor;
            GUILayout.Label($"[ {GetDifficultyDisplayName(_currentDifficulty)} ]", _headerStyle);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Fila de tres botones interactivos
            GUILayout.BeginHorizontal();

            DrawDifficultyButton(AIDifficulty.Easy, "Fácil");
            GUILayout.Space(4);
            DrawDifficultyButton(AIDifficulty.Medium, "Media");
            GUILayout.Space(4);
            DrawDifficultyButton(AIDifficulty.Hard, "Difícil");

            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label(GetDifficultyDescription(_currentDifficulty), _subtextStyle);

            GUILayout.EndArea();
        }

        private void DrawDifficultyButton(AIDifficulty diff, string label)
        {
            bool isCurrent = _currentDifficulty == diff;
            GUIStyle style = isCurrent ? _buttonActiveStyle : _buttonInactiveStyle;

            Color oldColor = GUI.color;
            if (isCurrent)
            {
                GUI.color = GetDifficultyColor(diff);
            }

            if (GUILayout.Button(label, style, GUILayout.Height(24), GUILayout.ExpandWidth(true)))
            {
                SelectDifficulty(diff);
            }

            GUI.color = oldColor;
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
            if (_panelBgTex != null) Destroy(_panelBgTex);
            if (_btnActiveBgTex != null) Destroy(_btnActiveBgTex);
            if (_btnInactiveBgTex != null) Destroy(_btnInactiveBgTex);
        }

        #endregion
    }
}

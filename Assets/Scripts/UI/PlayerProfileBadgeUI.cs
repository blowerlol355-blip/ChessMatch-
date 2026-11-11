using UnityEngine;
using UnityEngine.UI;

namespace ChessBalatro
{
    /// <summary>
    /// Widget de Perfil de Usuario estilo Arcade de 32-bits.
    /// Muestra en la esquina superior de la pantalla:
    /// - Icono/Avatar pixel art de Corona / Rey.
    /// - Nombre de usuario (persistido en PlayerPrefs).
    /// - ELO y Rango de partida.
    /// - Animación sutil de balanceo flotante (idle bobbing).
    /// </summary>
    public class PlayerProfileBadgeUI : MonoBehaviour
    {
        public static PlayerProfileBadgeUI Instance { get; private set; }

        private const string PrefKeyPlayerName = "ChessMatch_PlayerName";
        private const string PrefKeyPlayerElo = "ChessMatch_PlayerELO";

        [Header("Datos de Jugador")]
        [SerializeField] private string playerName = "TEST_PLAYER_01";
        [SerializeField] private int playerElo = 1200;
        [SerializeField] private string playerRank = "NOVATO";

        [Header("Posición y Animación")]
        [SerializeField] private bool placeOnTopRight = true;
        [SerializeField] private Vector2 screenOffset = new Vector2(24f, 20f);
        [SerializeField] private float bobbingAmplitude = 3.5f;
        [SerializeField] private float bobbingFrequency = 2.2f;

        [Header("Componentes uGUI Opcionales")]
        [SerializeField] private RectTransform badgeContainer;
        [SerializeField] private Text nameText;
        [SerializeField] private Text eloText;
        [SerializeField] private Image avatarImage;

        private Texture2D _crownIconTexture;
        private Texture2D _badgeBgTexture;
        private GUIStyle _badgeBoxStyle;
        private GUIStyle _badgeNameStyle;
        private GUIStyle _badgeEloStyle;
        private bool _stylesInitialized = false;

        public string PlayerName => playerName;
        public int PlayerElo => playerElo;
        public string PlayerRank => playerRank;
        public Rect LastBadgeScreenRect { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;

            LoadProfileData();
            GeneratePixelArtCrown();
        }

        private void Start()
        {
            UpdateUguiComponents();
        }

        private void Update()
        {
            if (badgeContainer != null)
            {
                float bob = Mathf.Sin(Time.time * bobbingFrequency) * bobbingAmplitude;
                badgeContainer.anchoredPosition = new Vector2(badgeContainer.anchoredPosition.x, -screenOffset.y + bob);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_crownIconTexture != null) Destroy(_crownIconTexture);
            if (_badgeBgTexture != null) Destroy(_badgeBgTexture);
        }

        public void SetPlayerProfile(string newName, int newElo)
        {
            playerName = newName;
            playerElo = newElo;
            playerRank = CalculateRank(newElo);
            SaveProfileData();
            UpdateUguiComponents();
        }

        private string CalculateRank(int elo)
        {
            if (elo >= 2200) return "GRAN MAESTRO";
            if (elo >= 1800) return "MAESTRO";
            if (elo >= 1500) return "AVANZADO";
            if (elo >= 1300) return "INTERMEDIO";
            return "NOVATO";
        }

        private void LoadProfileData()
        {
            playerName = PlayerPrefs.GetString(PrefKeyPlayerName, "TEST_PLAYER_01");
            playerElo = PlayerPrefs.GetInt(PrefKeyPlayerElo, 1200);
            playerRank = CalculateRank(playerElo);
        }

        private void SaveProfileData()
        {
            PlayerPrefs.SetString(PrefKeyPlayerName, playerName);
            PlayerPrefs.SetInt(PrefKeyPlayerElo, playerElo);
            PlayerPrefs.Save();
        }

        private void UpdateUguiComponents()
        {
            if (nameText != null) nameText.text = $"USER: {playerName}";
            if (eloText != null) eloText.text = $"ELO: {playerElo} | RANGO: {playerRank}";
            if (avatarImage != null && _crownIconTexture != null)
            {
                avatarImage.sprite = Sprite.Create(_crownIconTexture, new Rect(0, 0, _crownIconTexture.width, _crownIconTexture.height), new Vector2(0.5f, 0.5f));
            }
        }

        #region Generación Pixel Art de Corona (FilterMode.Point)

        private void GeneratePixelArtCrown()
        {
            // Matriz 16x16 de Corona Real de Ajedrez
            int w = 16, h = 16;
            _crownIconTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color transparent = new Color(0, 0, 0, 0);
            Color gold = new Color(1.0f, 0.84f, 0.12f, 1f);     // #FFD700
            Color darkGold = new Color(0.72f, 0.54f, 0.08f, 1f); // Sombra dorada
            Color ruby = new Color(0.92f, 0.20f, 0.25f, 1f);    // Joya rubí
            Color white = new Color(0.98f, 0.98f, 1f, 1f);       // Cruz y brillo
            Color outline = new Color(0.12f, 0.08f, 0.03f, 1f);  // Contorno 32-bits

            // Diseño matricial (y=15 arriba, y=0 abajo)
            string[] pattern = new string[]
            {
                "....WWWW....", // Cruz superior
                ".....WW.....",
                "....WWWW....",
                "..O.O..O.O..", // Puntas de la corona
                ".OGO.GG.OGO.",
                ".OGOGGGOGOG.",
                ".OGGGRGGOGG.", // Rubí central
                ".OGGGGGGGOG.",
                ".OGRRRRGGOG.", // Faja de gemas
                ".OGGGGGGGOG.",
                "..OOOOOOOO..", // Base
                "............"
            };

            Color[] pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = transparent;

            int patH = pattern.Length;
            int patW = pattern[0].Length;
            int startX = (w - patW) / 2;
            int startY = (h - patH) / 2;

            for (int py = 0; py < patH; py++)
            {
                int y = startY + (patH - 1 - py);
                string row = pattern[py];
                for (int px = 0; px < patW; px++)
                {
                    int x = startX + px;
                    char c = row[px];
                    Color col = transparent;
                    if (c == 'W') col = white;
                    else if (c == 'G') col = gold;
                    else if (c == 'O') col = outline;
                    else if (c == 'R') col = ruby;
                    else if (c == 'D') col = darkGold;

                    if (x >= 0 && x < w && y >= 0 && y < h)
                    {
                        pixels[y * w + x] = col;
                    }
                }
            }

            _crownIconTexture.SetPixels(pixels);
            _crownIconTexture.Apply();

            // Textura de fondo del badge
            _badgeBgTexture = new Texture2D(2, 2);
            _badgeBgTexture.SetPixels(new Color[] {
                new Color(0.08f, 0.11f, 0.16f, 0.94f), new Color(0.08f, 0.11f, 0.16f, 0.94f),
                new Color(0.08f, 0.11f, 0.16f, 0.94f), new Color(0.08f, 0.11f, 0.16f, 0.94f)
            });
            _badgeBgTexture.Apply();
        }

        #endregion

        #region Renderizado OnGUI

        private void InitOnGuiStyles()
        {
            if (_stylesInitialized && _badgeBoxStyle != null) return;

            float scale = ChessFontProvider.GetResponsiveScale();

            _badgeBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _badgeBgTexture }
            };

            _badgeNameStyle = new GUIStyle(GUI.skin.label)
            {
                font = ChessFontProvider.SerifBold,
                fontSize = Mathf.RoundToInt(15 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.98f, 0.88f, 0.35f, 1f) }
            };

            _badgeEloStyle = new GUIStyle(GUI.skin.label)
            {
                font = ChessFontProvider.SerifRegular,
                fontSize = Mathf.RoundToInt(12 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.75f, 0.82f, 0.92f, 1f) }
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            // Si ya existe un Canvas uGUI asignado y activo, evitar duplicar el dibujo
            if (badgeContainer != null && badgeContainer.gameObject.activeInHierarchy) return;

            InitOnGuiStyles();

            float scale = ChessFontProvider.GetResponsiveScale();
            float badgeW = 280f * scale;
            float badgeH = 58f * scale;

            float bobY = Mathf.Sin(Time.time * bobbingFrequency) * bobbingAmplitude * scale;
            float onboardingOffset = OnboardingManager.IsOnboardingActive ? (48f * scale) : 0f;
            float posX = placeOnTopRight
                ? Screen.width - badgeW - (screenOffset.x * scale)
                : (screenOffset.x * scale);
            float posY = (screenOffset.y * scale) + onboardingOffset + bobY;

            Rect badgeRect = new Rect(posX, posY, badgeW, badgeH);
            if (Event.current.type == EventType.Repaint)
            {
                LastBadgeScreenRect = badgeRect;
            }

            // Sombra pixel art dura de 32-bits (desfase 3px hacia abajo-derecha)
            Rect shadowRect = new Rect(posX + 3, posY + 3, badgeW, badgeH);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.85f);
            GUI.DrawTexture(shadowRect, Texture2D.whiteTexture);

            // Fondo del marco
            GUI.color = new Color(0.08f, 0.11f, 0.16f, 0.94f);
            GUI.DrawTexture(badgeRect, Texture2D.whiteTexture);

            // Borde dorado de 2px estilo arcade
            GUI.color = new Color(0.85f, 0.68f, 0.20f, 1f); // Oro arcade
            // Bordes superior, inferior, izquierdo, derecho
            GUI.DrawTexture(new Rect(badgeRect.x, badgeRect.y, badgeRect.width, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(badgeRect.x, badgeRect.yMax - 2, badgeRect.width, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(badgeRect.x, badgeRect.y, 2, badgeRect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(badgeRect.xMax - 2, badgeRect.y, 2, badgeRect.height), Texture2D.whiteTexture);

            GUI.color = Color.white;

            // Dibujar icono pixel art de la corona (38x38)
            float iconSize = 40f * scale;
            float iconX = posX + (10f * scale);
            float iconY = posY + (badgeH - iconSize) * 0.5f;
            if (_crownIconTexture != null)
            {
                GUI.DrawTexture(new Rect(iconX, iconY, iconSize, iconSize), _crownIconTexture, ScaleMode.StretchToFill);
            }

            // Textos del perfil
            float textX = iconX + iconSize + (10f * scale);
            float textW = badgeW - (iconSize + (24f * scale));

            GUI.Label(new Rect(textX, posY + (8f * scale), textW, 22f * scale), $"USER: {playerName}", _badgeNameStyle);
            GUI.Label(new Rect(textX, posY + (30f * scale), textW, 20f * scale), $"ELO: {playerElo} | {playerRank}", _badgeEloStyle);

            GUI.color = oldColor;
        }

        #endregion
    }
}


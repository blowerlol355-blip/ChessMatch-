using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Administrador de aspectos visuales (Skins) de las piezas de ajedrez.
    /// Permite almacenar múltiples <see cref="PieceSpriteLibrary"/>, navegar entre ellas,
    /// persistir la elección activa en PlayerPrefs y aplicarla a <see cref="BoardView"/>.
    /// Funciona como Singleton persistente (DontDestroyOnLoad).
    /// </summary>
    public class PieceSkinManager : MonoBehaviour
    {
        public static PieceSkinManager Instance { get; private set; }

        private const string PrefKeyActiveSkin = "ChessMatch_ActiveSkinIndex";

        [Header("Bibliotecas de Aspectos")]
        [SerializeField] private List<PieceSpriteLibrary> availableSkins = new List<PieceSpriteLibrary>();

        [Header("Estado Actual (Inspector Debug)")]
        [SerializeField] private int activeSkinIndex = 0;
        [SerializeField] private int previewSkinIndex = 0;

        public IReadOnlyList<PieceSpriteLibrary> AvailableSkins => availableSkins;
        public int ActiveSkinIndex => activeSkinIndex;
        public int PreviewSkinIndex => previewSkinIndex;

        public PieceSpriteLibrary ActiveSkin => (availableSkins != null && activeSkinIndex >= 0 && activeSkinIndex < availableSkins.Count)
            ? availableSkins[activeSkinIndex]
            : null;

        /// <summary>
        /// Alias de conveniencia para <see cref="ActiveSkin"/>.
        /// </summary>
        public PieceSpriteLibrary ActiveLibrary => ActiveSkin;

        public PieceSpriteLibrary CurrentPreviewSkin => (availableSkins != null && previewSkinIndex >= 0 && previewSkinIndex < availableSkins.Count)
            ? availableSkins[previewSkinIndex]
            : null;

        public PieceTheme ActiveTheme => ActiveSkin?.Theme;

        public event Action<PieceSpriteLibrary> OnSkinEquipped;
        public event Action<int, PieceSpriteLibrary> OnPreviewChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                EnsureDefaultSkins();
                LoadActiveSkinPreference();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Notificar aspecto inicial
            if (ActiveSkin != null)
            {
                OnSkinEquipped?.Invoke(ActiveSkin);
            }
        }

        #region Persistencia en PlayerPrefs

        public void LoadActiveSkinPreference()
        {
            if (PlayerPrefs.HasKey(PrefKeyActiveSkin))
            {
                activeSkinIndex = PlayerPrefs.GetInt(PrefKeyActiveSkin, 0);
            }
            else
            {
                activeSkinIndex = 0;
            }

            if (availableSkins.Count > 0)
            {
                activeSkinIndex = Mathf.Clamp(activeSkinIndex, 0, availableSkins.Count - 1);
            }
            else
            {
                activeSkinIndex = 0;
            }

            previewSkinIndex = activeSkinIndex;
        }

        private void SaveActiveSkinPreference()
        {
            PlayerPrefs.SetInt(PrefKeyActiveSkin, activeSkinIndex);
            PlayerPrefs.Save();
        }

        #endregion

        #region Gestión de Skins y Vista Previa

        public void SelectPreview(int index)
        {
            if (availableSkins == null || availableSkins.Count == 0) return;

            previewSkinIndex = Mathf.Clamp(index, 0, availableSkins.Count - 1);
            OnPreviewChanged?.Invoke(previewSkinIndex, CurrentPreviewSkin);
        }

        public void NextPreview()
        {
            if (availableSkins == null || availableSkins.Count == 0) return;
            int next = (previewSkinIndex + 1) % availableSkins.Count;
            SelectPreview(next);
        }

        public void PreviousPreview()
        {
            if (availableSkins == null || availableSkins.Count == 0) return;
            int prev = (previewSkinIndex - 1 + availableSkins.Count) % availableSkins.Count;
            SelectPreview(prev);
        }

        /// <summary>
        /// Equipa el skin actualmente visualizado en la vista previa y guarda la elección.
        /// </summary>
        public void EquipCurrentPreview()
        {
            EquipSkin(previewSkinIndex);
        }

        /// <summary>
        /// Equipa el skin por índice y lo persiste en PlayerPrefs.
        /// </summary>
        public void EquipSkin(int index)
        {
            if (availableSkins == null || index < 0 || index >= availableSkins.Count) return;

            activeSkinIndex = index;
            previewSkinIndex = index;
            SaveActiveSkinPreference();

            PieceSpriteLibrary equipped = ActiveSkin;
            Debug.Log($"<color=#4CAF50><b>[PieceSkinManager] Aspecto equipado: {equipped?.SkinName}</b></color>");

            OnSkinEquipped?.Invoke(equipped);

            // Refrescar cualquier tablero activo en la escena actual
            BoardView activeBoard = FindAnyObjectByType<BoardView>();
            if (activeBoard != null)
            {
                ApplyActiveSkinToBoard(activeBoard);
            }
        }

        /// <summary>
        /// Aplica la biblioteca activa al tablero especificado.
        /// </summary>
        public void ApplyActiveSkinToBoard(BoardView boardView)
        {
            if (boardView == null) return;

            PieceTheme themeToApply = ActiveTheme;
            if (themeToApply != null)
            {
                boardView.SetPieceTheme(themeToApply);
            }
        }

        #endregion

        #region Inicialización por Defecto (Zero-Setup)

        private void EnsureDefaultSkins()
        {
            // Intentar cargar ScriptableObjects existentes en Resources
            if (availableSkins == null) availableSkins = new List<PieceSpriteLibrary>();

            var loadedFromResources = Resources.LoadAll<PieceSpriteLibrary>("Skins");
            if (loadedFromResources != null && loadedFromResources.Length > 0)
            {
                foreach (var lib in loadedFromResources)
                {
                    if (!availableSkins.Contains(lib))
                    {
                        availableSkins.Add(lib);
                    }
                }
            }

            // Garantizar que todos los temas cargados tengan sus sprites asignados
            foreach (var skin in availableSkins)
            {
                if (skin != null && skin.Theme != null && skin.Theme.whitePawn == null)
                {
                    PopulateSpritesFromResources(skin.Theme);
                }
            }

            // Si aún no hay skins, crear runtime defaults con sprites cargados
            if (availableSkins.Count == 0)
            {
                CreateDefaultRuntimeSkins();
            }
        }

        private void PopulateSpritesFromResources(PieceTheme theme)
        {
            if (theme == null) return;
            theme.whitePawn   = Resources.Load<Sprite>("Pieces/white_pawn");
            theme.whiteKnight = Resources.Load<Sprite>("Pieces/white_knight");
            theme.whiteBishop = Resources.Load<Sprite>("Pieces/white_bishop");
            theme.whiteRook   = Resources.Load<Sprite>("Pieces/white_rook");
            theme.whiteQueen  = Resources.Load<Sprite>("Pieces/white_queen");
            theme.whiteKing   = Resources.Load<Sprite>("Pieces/white_king");

            theme.blackPawn   = Resources.Load<Sprite>("Pieces/black_pawn");
            theme.blackKnight = Resources.Load<Sprite>("Pieces/black_knight");
            theme.blackBishop = Resources.Load<Sprite>("Pieces/black_bishop");
            theme.blackRook   = Resources.Load<Sprite>("Pieces/black_rook");
            theme.blackQueen  = Resources.Load<Sprite>("Pieces/black_queen");
            theme.blackKing   = Resources.Load<Sprite>("Pieces/black_king");
        }

        private void CreateDefaultRuntimeSkins()
        {
            // 1. Set Clásico (Tradicional blanco / negro marfil con sprites pixel art)
            PieceTheme classicTheme = ScriptableObject.CreateInstance<PieceTheme>();
            classicTheme.name = "Runtime_ClassicTheme";
            classicTheme.whitePieceColor = new Color(0.96f, 0.97f, 0.98f, 1f);
            classicTheme.whiteBorderColor = new Color(0.72f, 0.76f, 0.82f, 1f);
            classicTheme.whiteTextColor = new Color(0.12f, 0.12f, 0.15f, 1f);
            classicTheme.blackPieceColor = new Color(0.18f, 0.19f, 0.22f, 1f);
            classicTheme.blackBorderColor = new Color(0.38f, 0.42f, 0.48f, 1f);
            classicTheme.blackTextColor = new Color(0.95f, 0.96f, 0.98f, 1f);
            classicTheme.pieceScale = 0.85f;
            PopulateSpritesFromResources(classicTheme);

            var classicSkin = PieceSpriteLibrary.CreateRuntimeSkin(
                "classic",
                "Set Clásico",
                "Estilo tradicional de torneo con piezas sobrias y contraste óptimo para partidas serias.",
                classicTheme
            );
            availableSkins.Add(classicSkin);

            // 2. Set Retro Arcade (Balatro Neón)
            PieceTheme arcadeTheme = ScriptableObject.CreateInstance<PieceTheme>();
            arcadeTheme.name = "Runtime_RetroArcadeTheme";
            arcadeTheme.whitePieceColor = new Color(1f, 0.93f, 0.80f, 1f);
            arcadeTheme.whiteBorderColor = new Color(0.85f, 0.69f, 0.22f, 1f); // Dorado Neón
            arcadeTheme.whiteTextColor = new Color(0.17f, 0.10f, 0.05f, 1f);
            arcadeTheme.blackPieceColor = new Color(0.66f, 0.13f, 0.21f, 1f); // Carmesí Balatro
            arcadeTheme.blackBorderColor = new Color(0.90f, 0.28f, 0.36f, 1f);
            arcadeTheme.blackTextColor = new Color(1f, 1f, 1f, 1f);
            arcadeTheme.pieceScale = 0.88f;
            arcadeTheme.tintSprites = true;
            PopulateSpritesFromResources(arcadeTheme);

            var arcadeSkin = PieceSpriteLibrary.CreateRuntimeSkin(
                "retro_arcade",
                "Set Retro Arcade",
                "Atmósfera estilo Balatro con tokens carmesí neón, ribetes dorados y estética rogue-deckbuilder.",
                arcadeTheme
            );
            availableSkins.Add(arcadeSkin);

            // 3. Set Cyberpunk Minimalista (Azul Cyan & Morado Eléctrico)
            PieceTheme cyberTheme = ScriptableObject.CreateInstance<PieceTheme>();
            cyberTheme.name = "Runtime_CyberTheme";
            cyberTheme.whitePieceColor = new Color(0.10f, 0.85f, 0.95f, 1f); // Cyan
            cyberTheme.whiteBorderColor = new Color(0.20f, 1.0f, 0.90f, 1f);
            cyberTheme.whiteTextColor = new Color(0.05f, 0.15f, 0.25f, 1f);
            cyberTheme.blackPieceColor = new Color(0.45f, 0.10f, 0.70f, 1f); // Púrpura eléctrico
            cyberTheme.blackBorderColor = new Color(0.75f, 0.25f, 0.95f, 1f);
            cyberTheme.blackTextColor = new Color(0.98f, 0.95f, 1f, 1f);
            cyberTheme.pieceScale = 0.85f;
            cyberTheme.tintSprites = true;
            PopulateSpritesFromResources(cyberTheme);

            var cyberSkin = PieceSpriteLibrary.CreateRuntimeSkin(
                "cyber_minimal",
                "Set Cyber Minimal",
                "Fusión futurista de cian brillante y violeta profundo para una lectura táctica rápida.",
                cyberTheme
            );
            availableSkins.Add(cyberSkin);
        }

        #endregion
    }
}


using System;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// ScriptableObject que empaqueta y define una biblioteca visual de aspectos (Skin) para las piezas.
    /// Contiene metadatos de presentación (nombre, descripción, icono de vista previa) y referencia
    /// al <see cref="PieceTheme"/> que define colores y sprites en el tablero.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPieceSkin", menuName = "ChessBalatro/Piece Skin Library")]
    public class PieceSpriteLibrary : ScriptableObject
    {
        [Header("Información del Aspecto")]
        [SerializeField] private string skinId = "classic";
        [SerializeField] private string skinName = "Set Clásico";
        [SerializeField, TextArea(2, 4)] private string description = "Estilo tradicional de torneo con piezas estilizadas de madera y marfil.";
        [SerializeField] private Sprite previewIcon;

        [Header("Configuración Visual")]
        [SerializeField] private PieceTheme theme;

        public string SkinId => skinId;
        public string SkinName => skinName;
        public string Description => description;
        public PieceTheme Theme => theme;

        /// <summary>
        /// Retorna el sprite de vista previa o una pieza representativa del tema (Rey/Caballo blanco).
        /// </summary>
        public Sprite GetPreviewSprite()
        {
            if (previewIcon != null) return previewIcon;
            if (theme != null)
            {
                if (theme.whiteKing != null) return theme.whiteKing;
                if (theme.whiteKnight != null) return theme.whiteKnight;
                if (theme.whitePawn != null) return theme.whitePawn;
            }
            return null;
        }

        public static PieceSpriteLibrary CreateRuntimeSkin(string id, string name, string desc, PieceTheme theme, Sprite icon = null)
        {
            var skin = ScriptableObject.CreateInstance<PieceSpriteLibrary>();
            skin.skinId = id;
            skin.skinName = name;
            skin.description = desc;
            skin.theme = theme;
            skin.previewIcon = icon;
            return skin;
        }
    }
}


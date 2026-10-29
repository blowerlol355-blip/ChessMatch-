using System;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// ScriptableObject que define el aspecto visual, colores y sprites de las piezas de ajedrez.
    /// Permite crear temas visuales configurables directamente desde la carpeta Assets en Unity.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPieceTheme", menuName = "ChessBalatro/Piece Theme")]
    public class PieceTheme : ScriptableObject
    {
        [Header("Piezas Blancas - Colores")]
        [Tooltip("Color de fondo de la ficha o tinte del sprite blanco")]
        public Color whitePieceColor = new Color(0.96f, 0.97f, 0.98f, 1f);

        [Tooltip("Color del borde del token")]
        public Color whiteBorderColor = new Color(0.72f, 0.76f, 0.82f, 1f);

        [Tooltip("Color del texto o símbolo (inicial)")]
        public Color whiteTextColor = new Color(0.12f, 0.12f, 0.15f, 1f);

        [Header("Piezas Negras - Colores")]
        [Tooltip("Color de fondo de la ficha o tinte del sprite negro")]
        public Color blackPieceColor = new Color(0.18f, 0.19f, 0.22f, 1f);

        [Tooltip("Color del borde del token")]
        public Color blackBorderColor = new Color(0.38f, 0.42f, 0.48f, 1f);

        [Tooltip("Color del texto o símbolo (inicial)")]
        public Color blackTextColor = new Color(0.95f, 0.96f, 0.98f, 1f);

        [Header("Dimensiones y Escala")]
        [Range(0.4f, 1.2f)]
        public float pieceScale = 0.85f;

        [Header("Comportamiento de Sprites")]
        [Tooltip("Si está activo, aplica el color de la pieza como tinte sobre el sprite.")]
        public bool tintSprites = false;

        [Header("Sprites Opcionales - Blancas")]
        public Sprite whitePawn;
        public Sprite whiteKnight;
        public Sprite whiteBishop;
        public Sprite whiteRook;
        public Sprite whiteQueen;
        public Sprite whiteKing;

        [Header("Sprites Opcionales - Negras")]
        public Sprite blackPawn;
        public Sprite blackKnight;
        public Sprite blackBishop;
        public Sprite blackRook;
        public Sprite blackQueen;
        public Sprite blackKing;

        public Color GetPieceColor(PieceColor color) => color == PieceColor.White ? whitePieceColor : blackPieceColor;
        public Color GetBorderColor(PieceColor color) => color == PieceColor.White ? whiteBorderColor : blackBorderColor;
        public Color GetTextColor(PieceColor color) => color == PieceColor.White ? whiteTextColor : blackTextColor;

        public Sprite GetSprite(PieceType type, PieceColor color)
        {
            if (color == PieceColor.White)
            {
                return type switch
                {
                    PieceType.Pawn => whitePawn,
                    PieceType.Knight => whiteKnight,
                    PieceType.Bishop => whiteBishop,
                    PieceType.Rook => whiteRook,
                    PieceType.Queen => whiteQueen,
                    PieceType.King => whiteKing,
                    _ => null
                };
            }
            else
            {
                return type switch
                {
                    PieceType.Pawn => blackPawn,
                    PieceType.Knight => blackKnight,
                    PieceType.Bishop => blackBishop,
                    PieceType.Rook => blackRook,
                    PieceType.Queen => blackQueen,
                    PieceType.King => blackKing,
                    _ => null
                };
            }
        }
    }
}

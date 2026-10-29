using System;

namespace ChessBalatro
{
    public enum PieceType
    {
        None = 0,
        Pawn = 1,
        Knight = 2,
        Bishop = 3,
        Rook = 4,
        Queen = 5,
        King = 6
    }

    public enum PieceColor
    {
        White = 0,
        Black = 1
    }

    /// <summary>
    /// Representa una pieza de ajedrez en C# puro.
    /// Diseñado para admitir modificadores y sinergias (estilo Balatro).
    /// </summary>
    [Serializable]
    public class Piece
    {
        public PieceType Type { get; set; }
        public PieceColor Color { get; set; }
        public bool HasMoved { get; set; }

        // Propiedades preparadas para mecánicas estilo Balatro
        public int BonusChips { get; set; }
        public float MultMultiplier { get; set; } = 1.0f;

        public int BaseValue => GetBaseValue(Type);

        public Piece(PieceType type, PieceColor color, bool hasMoved = false)
        {
            Type = type;
            Color = color;
            HasMoved = hasMoved;
        }

        public Piece Clone()
        {
            return new Piece(Type, Color, HasMoved)
            {
                BonusChips = this.BonusChips,
                MultMultiplier = this.MultMultiplier
            };
        }

        public static int GetBaseValue(PieceType type) => type switch
        {
            PieceType.Pawn => 1,
            PieceType.Knight => 3,
            PieceType.Bishop => 3,
            PieceType.Rook => 5,
            PieceType.Queen => 9,
            PieceType.King => 0,
            _ => 0
        };

        public char GetSymbol()
        {
            char symbol = Type switch
            {
                PieceType.Pawn => 'P',
                PieceType.Knight => 'N',
                PieceType.Bishop => 'B',
                PieceType.Rook => 'R',
                PieceType.Queen => 'Q',
                PieceType.King => 'K',
                _ => '.'
            };
            return Color == PieceColor.White ? symbol : char.ToLowerInvariant(symbol);
        }

        public override string ToString() => $"{Color} {Type}";
    }
}

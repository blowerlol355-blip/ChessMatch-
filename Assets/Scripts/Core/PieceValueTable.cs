namespace ChessBalatro
{
    /// <summary>
    /// Tabla oficial de valores de piezas de ajedrez para la economía de puntos de captura
    /// y la bonificación opcional por entregar Jaque válido.
    /// </summary>
    public static class PieceValueTable
    {
        public const int PawnValue = 1;
        public const int KnightValue = 3;
        public const int BishopValue = 3;
        public const int RookValue = 5;
        public const int QueenValue = 9;
        public const int KingValue = 0; // El Rey capturado/mate representa fin de partida

        public const int CheckBonusPoints = 1;

        /// <summary>
        /// Devuelve el valor oficial de captura en puntos según el tipo de pieza.
        /// </summary>
        public static int GetValue(PieceType pieceType)
        {
            switch (pieceType)
            {
                case PieceType.Pawn:   return PawnValue;
                case PieceType.Knight: return KnightValue;
                case PieceType.Bishop: return BishopValue;
                case PieceType.Rook:   return RookValue;
                case PieceType.Queen:  return QueenValue;
                case PieceType.King:   return KingValue;
                default:               return 0;
            }
        }

        /// <summary>
        /// Devuelve el valor oficial de captura para una instancia de pieza.
        /// </summary>
        public static int GetValue(Piece piece)
        {
            return piece != null ? GetValue(piece.Type) : 0;
        }

        /// <summary>
        /// Calcula los puntos totales otorgados por una jugada (captura + bono opcional de Jaque).
        /// </summary>
        public static int CalculateEarnedPoints(PieceType capturedType, bool deliveredValidCheck, bool enableCheckBonus)
        {
            if (capturedType == PieceType.King)
            {
                return 0;
            }

            int basePoints = GetValue(capturedType);
            int bonus = (enableCheckBonus && deliveredValidCheck) ? CheckBonusPoints : 0;
            return basePoints + bonus;
        }

        /// <summary>
        /// Formatea el texto flotante pixel art sobre la casilla ("+1 PT", "+3 PTS", "+9 PTS!").
        /// </summary>
        public static string FormatPopupText(int points, PieceType capturedType = PieceType.None, bool includeCheckTag = false)
        {
            if (points <= 0) return string.Empty;

            string baseText;
            if (points == 1)
            {
                baseText = "+1 PT";
            }
            else if (capturedType == PieceType.Queen || points >= QueenValue)
            {
                baseText = $"+{points} PTS!";
            }
            else
            {
                baseText = $"+{points} PTS";
            }

            return includeCheckTag ? $"{baseText}\n¡JAQUE!" : baseText;
        }
    }
}


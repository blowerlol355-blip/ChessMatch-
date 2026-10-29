using System;
using System.Collections.Generic;

namespace ChessBalatro
{
    /// <summary>
    /// Motor central de cálculo de puntuación estilo Balatro (Fichas x Multiplicador) en C# puro.
    /// Valora la pieza movida, la distancia, capturas, jugadas especiales (enroque, al paso, coronación),
    /// jaques y sinergias provistas por Jokers o cartas pasivas.
    /// </summary>
    public static class ScoreCalculator
    {
        public const int PawnBaseChips = 10;
        public const int KnightBaseChips = 30;
        public const int BishopBaseChips = 30;
        public const int RookBaseChips = 50;
        public const int QueenBaseChips = 90;
        public const int KingBaseChips = 20;

        /// <summary>
        /// Obtiene el valor base en Fichas (Chips) de un tipo de pieza.
        /// </summary>
        public static int GetPieceBaseChips(PieceType type)
        {
            switch (type)
            {
                case PieceType.Pawn: return PawnBaseChips;
                case PieceType.Knight: return KnightBaseChips;
                case PieceType.Bishop: return BishopBaseChips;
                case PieceType.Rook: return RookBaseChips;
                case PieceType.Queen: return QueenBaseChips;
                case PieceType.King: return KingBaseChips;
                default: return 0;
            }
        }

        /// <summary>
        /// Calcula el desglose completo de puntuación de un movimiento ejecutado sobre el tablero.
        /// </summary>
        public static ScoreBreakdown CalculateScore(Move move, BoardState board, IEnumerable<IScoreModifier> scoreModifiers = null)
        {
            ScoreBreakdown breakdown = new ScoreBreakdown();
            if (move == null || move.PieceMoved == null) return breakdown;

            Piece piece = move.PieceMoved;

            // 1. Fichas base por la pieza movida
            int pieceChips = GetPieceBaseChips(piece.Type);
            breakdown.BaseChips = pieceChips;
            breakdown.BaseMult = 1.0f;
            breakdown.AddLog($"{piece.Type}: +{pieceChips} Fichas");

            // 2. Distancia euclidiana/manhattan recorrida (+2 Fichas por casilla)
            int dist = Math.Max(Math.Abs(move.To.File - move.From.File), Math.Abs(move.To.Rank - move.From.Rank));
            if (dist > 1)
            {
                int distanceBonus = dist * 2;
                breakdown.BonusChips += distanceBonus;
                breakdown.AddLog($"Desplazamiento ({dist} casillas): +{distanceBonus} Fichas");
            }

            // 3. Captura
            if (move.PieceCaptured != null)
            {
                int capturedValue = GetPieceBaseChips(move.PieceCaptured.Type);
                breakdown.BonusChips += capturedValue;
                breakdown.BonusMult += 2.0f;
                breakdown.AddLog($"¡Captura de {move.PieceCaptured.Type}!: +{capturedValue} Fichas, +2.0 Mult");
            }

            // 4. Jugadas Especiales
            if (move.IsCastling)
            {
                breakdown.BonusChips += 40;
                breakdown.BonusMult += 2.0f;
                breakdown.AddLog("¡Enroque Fortificado!: +40 Fichas, +2.0 Mult");
            }

            if (move.IsEnPassant)
            {
                breakdown.BonusChips += 35;
                breakdown.BonusMult += 2.5f;
                breakdown.AddLog("¡Peón al Paso!: +35 Fichas, +2.5 Mult");
            }

            if (move.PromotionType != PieceType.None)
            {
                breakdown.BonusChips += 80;
                breakdown.BonusMult += 3.5f;
                breakdown.AddLog($"¡Coronación a {move.PromotionType}!: +80 Fichas, +3.5 Mult");
            }

            // 5. Estado de Jaque o Jaque Mate sobre el rival
            if (board != null)
            {
                PieceColor enemyColor = piece.Color == PieceColor.White ? PieceColor.Black : PieceColor.White;
                if (board.IsInCheck(enemyColor))
                {
                    bool isMate = MoveValidator.IsCheckmate(board, enemyColor);
                    if (isMate)
                    {
                        breakdown.BonusChips += 200;
                        breakdown.BonusMult += 5.0f;
                        breakdown.AddLog("¡JAQUE MATE FINAL!: +200 Fichas, +5.0 Mult");
                    }
                    else
                    {
                        breakdown.BonusChips += 50;
                        breakdown.BonusMult += 1.5f;
                        breakdown.AddLog("¡JAQUE!: +50 Fichas, +1.5 Mult");
                    }
                }
            }

            // 6. Sinergias de Modificadores Pasivos / Jokers
            if (scoreModifiers != null)
            {
                int currentChips = breakdown.FinalChips;
                float currentMult = breakdown.FinalMult;

                foreach (var mod in scoreModifiers)
                {
                    if (mod != null)
                    {
                        int beforeChips = currentChips;
                        float beforeMult = currentMult;

                        mod.ModifyScore(move, board, ref currentChips, ref currentMult, breakdown.ScoreLogs);

                        int chipsDelta = currentChips - beforeChips;
                        float multDelta = currentMult - beforeMult;

                        if (chipsDelta > 0) breakdown.BonusChips += chipsDelta;
                        if (multDelta > 0f) breakdown.BonusMult += multDelta;
                    }
                }
            }

            return breakdown;
        }
    }
}

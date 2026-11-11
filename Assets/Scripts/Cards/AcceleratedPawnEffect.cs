using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Mejora Menor: "Peón Acelerado+" (AcceleratedPawnEffect).
    /// Permite a todos tus Peones avanzar hasta 2 casillas al frente desde cualquier fila
    /// y desplazarse 1 casilla diagonal hacia adelante incluso sobre casillas libres.
    /// </summary>
    [Serializable]
    public class AcceleratedPawnEffect : ICardEffect, IMoveModifier
    {
        [SerializeField] private bool isActive = false;
        [SerializeField] private PieceColor activeColor = PieceColor.White;

        public bool IsActive => isActive;
        public PieceColor ActiveColor => activeColor;

        public bool CanApply(BoardState board, PieceColor turnColor)
        {
            if (board == null) return false;

            for (int f = 0; f < BoardState.BoardSize; f++)
            {
                for (int r = 0; r < BoardState.BoardSize; r++)
                {
                    Piece p = board[f, r];
                    if (p != null && p.Color == turnColor && p.Type == PieceType.Pawn)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void ApplyEffect(BoardState board, BoardCoord? targetCoord = null)
        {
            if (board == null) return;
            isActive = true;
            activeColor = board.CurrentTurn;
        }

        public void ModifyMoves(Piece piece, BoardCoord coord, BoardState board, List<BoardCoord> legalMoves)
        {
            if (!isActive || board == null || piece == null || legalMoves == null) return;
            if (piece.Type != PieceType.Pawn || piece.Color != activeColor) return;

            int forward = piece.Color == PieceColor.White ? 1 : -1;
            int oneStepRank = coord.Rank + forward;
            int twoStepRank = coord.Rank + (forward * 2);

            // 1. Doble avance frontal desde cualquier casilla despejada
            if (BoardState.IsInBounds(coord.File, oneStepRank) &&
                BoardState.IsInBounds(coord.File, twoStepRank) &&
                board[coord.File, oneStepRank] == null &&
                board[coord.File, twoStepRank] == null)
            {
                BoardCoord doubleCoord = new BoardCoord(coord.File, twoStepRank);
                if (!legalMoves.Contains(doubleCoord))
                {
                    legalMoves.Add(doubleCoord);
                }
            }

            // 2. Despliegue diagonal hacia adelante en casillas libres (cambio rápido de columna)
            for (int df = -1; df <= 1; df += 2)
            {
                int diagFile = coord.File + df;
                if (BoardState.IsInBounds(diagFile, oneStepRank) && board[diagFile, oneStepRank] == null)
                {
                    BoardCoord diagCoord = new BoardCoord(diagFile, oneStepRank);
                    if (!legalMoves.Contains(diagCoord))
                    {
                        legalMoves.Add(diagCoord);
                    }
                }
            }
        }

        public void ModifyPseudoLegalMoves(BoardState board, BoardCoord origin, Piece piece, List<Move> moves)
        {
            if (!isActive || board == null || piece == null || moves == null) return;
            if (piece.Type != PieceType.Pawn || piece.Color != activeColor) return;

            List<BoardCoord> extraCoords = new List<BoardCoord>();
            ModifyMoves(piece, origin, board, extraCoords);

            int promotionRank = piece.Color == PieceColor.White ? 7 : 0;

            for (int i = 0; i < extraCoords.Count; i++)
            {
                BoardCoord dest = extraCoords[i];
                if (!moves.Exists(m => m.To == dest))
                {
                    if (dest.Rank == promotionRank)
                    {
                        moves.Add(new Move(origin, dest, piece) { PromotionType = PieceType.Queen });
                        moves.Add(new Move(origin, dest, piece) { PromotionType = PieceType.Rook });
                        moves.Add(new Move(origin, dest, piece) { PromotionType = PieceType.Bishop });
                        moves.Add(new Move(origin, dest, piece) { PromotionType = PieceType.Knight });
                    }
                    else
                    {
                        moves.Add(new Move(origin, dest, piece));
                    }
                }
            }
        }

        public void ResetEffect()
        {
            isActive = false;
        }
    }
}


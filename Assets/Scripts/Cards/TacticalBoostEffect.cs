using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Mejora Menor: "Impulso Táctico" (TacticalBoostEffect).
    /// Concede a tus Alfiles 1 paso ortogonal (cambio de color de casilla),
    /// a tus Torres 1 paso diagonal y a tus Caballos 1 paso corto diagonal durante el turno.
    /// </summary>
    [Serializable]
    public class TacticalBoostEffect : ICardEffect, IMoveModifier
    {
        [SerializeField] private bool isActive = false;
        [SerializeField] private PieceColor activeColor = PieceColor.White;

        private static readonly int[,] OrthoOffsets = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
        private static readonly int[,] DiagOffsets  = { { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 } };

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
                    if (p != null && p.Color == turnColor &&
                        (p.Type == PieceType.Bishop || p.Type == PieceType.Rook || p.Type == PieceType.Knight))
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
            if (piece.Color != activeColor) return;

            int[,] offsetsToAdd = null;
            if (piece.Type == PieceType.Bishop)
            {
                offsetsToAdd = OrthoOffsets;
            }
            else if (piece.Type == PieceType.Rook || piece.Type == PieceType.Knight)
            {
                offsetsToAdd = DiagOffsets;
            }

            if (offsetsToAdd == null) return;

            for (int i = 0; i < 4; i++)
            {
                int targetF = coord.File + offsetsToAdd[i, 0];
                int targetR = coord.Rank + offsetsToAdd[i, 1];

                if (!BoardState.IsInBounds(targetF, targetR)) continue;

                Piece targetPiece = board[targetF, targetR];
                if (targetPiece == null || targetPiece.Color != piece.Color)
                {
                    BoardCoord dest = new BoardCoord(targetF, targetR);
                    if (!legalMoves.Contains(dest))
                    {
                        legalMoves.Add(dest);
                    }
                }
            }
        }

        public void ModifyPseudoLegalMoves(BoardState board, BoardCoord origin, Piece piece, List<Move> moves)
        {
            if (!isActive || board == null || piece == null || moves == null) return;
            if (piece.Color != activeColor) return;

            List<BoardCoord> extraCoords = new List<BoardCoord>();
            ModifyMoves(piece, origin, board, extraCoords);

            for (int i = 0; i < extraCoords.Count; i++)
            {
                BoardCoord dest = extraCoords[i];
                if (!moves.Exists(m => m.To == dest))
                {
                    Piece captured = board[dest];
                    moves.Add(new Move(origin, dest, piece, captured));
                }
            }
        }

        public void ResetEffect()
        {
            isActive = false;
        }
    }
}


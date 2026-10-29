using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Efecto concreto "Salto de Caballería" (Knight Boost).
    /// Concede a los Caballos la habilidad adicional de desplazarse 1 casilla ortogonalmente
    /// (como una torre corta) además de sus saltos en 'L' habituales, permitiéndoles maniobrar
    /// en espacios cerrados o alcanzar casillas clave.
    /// </summary>
    [Serializable]
    public class KnightBoostEffect : ICardEffect, IMoveModifier
    {
        [SerializeField] private bool isActive = false;
        [SerializeField] private PieceColor activeColor = PieceColor.White;

        private static readonly int[,] CardinalOffsets = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };

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
                    if (p != null && p.Color == turnColor && p.Type == PieceType.Knight)
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
            if (piece.Type != PieceType.Knight || piece.Color != activeColor) return;

            for (int i = 0; i < 4; i++)
            {
                int targetFile = coord.File + CardinalOffsets[i, 0];
                int targetRank = coord.Rank + CardinalOffsets[i, 1];

                if (!BoardState.IsInBounds(targetFile, targetRank)) continue;

                Piece targetPiece = board[targetFile, targetRank];
                // Casilla vacía o con pieza enemiga (captura)
                if (targetPiece == null || targetPiece.Color != piece.Color)
                {
                    BoardCoord targetCoord = new BoardCoord(targetFile, targetRank);
                    if (!legalMoves.Contains(targetCoord))
                    {
                        legalMoves.Add(targetCoord);
                    }
                }
            }
        }

        public void ModifyPseudoLegalMoves(BoardState board, BoardCoord origin, Piece piece, List<Move> moves)
        {
            if (!isActive || board == null || piece == null || moves == null) return;
            if (piece.Type != PieceType.Knight || piece.Color != activeColor) return;

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

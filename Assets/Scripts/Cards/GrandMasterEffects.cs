using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Carta de Gran Maestro: "Mijaíl Tal - El Mago de Riga".
    /// Durante este turno, tus Caballos pueden deslizarse hasta 3 casillas en diagonal (Alfil)
    /// y tus Alfiles pueden saltar en 'L' como un Caballo.
    /// </summary>
    [Serializable]
    public class TalMagicEffect : ICardEffect, IMoveModifier
    {
        [SerializeField] private bool isActive = false;
        [SerializeField] private PieceColor activeColor = PieceColor.White;

        private static readonly int[] KnightFileOffsets = { 1, 2, 2, 1, -1, -2, -2, -1 };
        private static readonly int[] KnightRankOffsets = { 2, 1, -1, -2, -2, -1, 1, 2 };
        private static readonly int[,] DiagDirs = { { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 } };

        public bool IsActive => isActive;

        public bool CanApply(BoardState board, PieceColor turnColor)
        {
            if (board == null) return false;
            for (int f = 0; f < BoardState.BoardSize; f++)
            {
                for (int r = 0; r < BoardState.BoardSize; r++)
                {
                    Piece p = board[f, r];
                    if (p != null && p.Color == turnColor && (p.Type == PieceType.Knight || p.Type == PieceType.Bishop))
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

            if (piece.Type == PieceType.Knight)
            {
                // Deslizamiento diagonal de hasta 3 casillas
                for (int d = 0; d < 4; d++)
                {
                    for (int step = 1; step <= 3; step++)
                    {
                        int nf = coord.File + DiagDirs[d, 0] * step;
                        int nr = coord.Rank + DiagDirs[d, 1] * step;
                        if (!BoardState.IsInBounds(nf, nr)) break;

                        Piece target = board[nf, nr];
                        if (target == null)
                        {
                            BoardCoord dest = new BoardCoord(nf, nr);
                            if (!legalMoves.Contains(dest)) legalMoves.Add(dest);
                        }
                        else
                        {
                            if (target.Color != piece.Color)
                            {
                                BoardCoord dest = new BoardCoord(nf, nr);
                                if (!legalMoves.Contains(dest)) legalMoves.Add(dest);
                            }
                            break;
                        }
                    }
                }
            }
            else if (piece.Type == PieceType.Bishop)
            {
                // Saltos en 'L' de Caballo
                for (int i = 0; i < 8; i++)
                {
                    int nf = coord.File + KnightFileOffsets[i];
                    int nr = coord.Rank + KnightRankOffsets[i];
                    if (!BoardState.IsInBounds(nf, nr)) continue;

                    Piece target = board[nf, nr];
                    if (target == null || target.Color != piece.Color)
                    {
                        BoardCoord dest = new BoardCoord(nf, nr);
                        if (!legalMoves.Contains(dest)) legalMoves.Add(dest);
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
                    moves.Add(new Move(origin, dest, piece, board[dest]));
                }
            }
        }

        public void ResetEffect()
        {
            isActive = false;
        }
    }

    /// <summary>
    /// Carta de Gran Maestro: "José Raúl Capablanca - Amazona Real".
    /// Durante este turno, tu Dama suma los saltos en 'L' del Caballo (Amazona)
    /// y tus Torres pueden dar 1 paso diagonal adicional.
    /// </summary>
    [Serializable]
    public class CapablancaAmazonEffect : ICardEffect, IMoveModifier
    {
        [SerializeField] private bool isActive = false;
        [SerializeField] private PieceColor activeColor = PieceColor.White;

        private static readonly int[] KnightFileOffsets = { 1, 2, 2, 1, -1, -2, -2, -1 };
        private static readonly int[] KnightRankOffsets = { 2, 1, -1, -2, -2, -1, 1, 2 };
        private static readonly int[,] DiagDirs = { { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 } };

        public bool IsActive => isActive;

        public bool CanApply(BoardState board, PieceColor turnColor)
        {
            if (board == null) return false;
            for (int f = 0; f < BoardState.BoardSize; f++)
            {
                for (int r = 0; r < BoardState.BoardSize; r++)
                {
                    Piece p = board[f, r];
                    if (p != null && p.Color == turnColor && (p.Type == PieceType.Queen || p.Type == PieceType.Rook))
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

            if (piece.Type == PieceType.Queen)
            {
                for (int i = 0; i < 8; i++)
                {
                    int nf = coord.File + KnightFileOffsets[i];
                    int nr = coord.Rank + KnightRankOffsets[i];
                    if (!BoardState.IsInBounds(nf, nr)) continue;

                    Piece target = board[nf, nr];
                    if (target == null || target.Color != piece.Color)
                    {
                        BoardCoord dest = new BoardCoord(nf, nr);
                        if (!legalMoves.Contains(dest)) legalMoves.Add(dest);
                    }
                }
            }
            else if (piece.Type == PieceType.Rook)
            {
                for (int d = 0; d < 4; d++)
                {
                    int nf = coord.File + DiagDirs[d, 0];
                    int nr = coord.Rank + DiagDirs[d, 1];
                    if (!BoardState.IsInBounds(nf, nr)) continue;

                    Piece target = board[nf, nr];
                    if (target == null || target.Color != piece.Color)
                    {
                        BoardCoord dest = new BoardCoord(nf, nr);
                        if (!legalMoves.Contains(dest)) legalMoves.Add(dest);
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
                    moves.Add(new Move(origin, dest, piece, board[dest]));
                }
            }
        }

        public void ResetEffect()
        {
            isActive = false;
        }
    }
}


using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Mejora Menor: "Trinchera Real" (RoyalTrenchEffect).
    /// Permite a tu Rey desplazarse hasta 2 casillas en línea ortogonal hacia refugio seguro
    /// y autoriza a tus Peones en tu mitad del tablero a deslizarse 1 casilla lateralmente para cerrar brechas.
    /// </summary>
    [Serializable]
    public class RoyalTrenchEffect : ICardEffect, IMoveModifier
    {
        [SerializeField] private bool isActive = false;
        [SerializeField] private PieceColor activeColor = PieceColor.White;

        private static readonly int[,] OrthoDirs = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };

        public bool IsActive => isActive;
        public PieceColor ActiveColor => activeColor;

        public bool CanApply(BoardState board, PieceColor turnColor)
        {
            if (board == null) return false;
            return board.FindKing(turnColor).HasValue;
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

            if (piece.Type == PieceType.King)
            {
                // El Rey puede realizar una retirada táctica de 2 casillas ortogonales si la ruta intermedia está libre
                for (int i = 0; i < 4; i++)
                {
                    int midF = coord.File + OrthoDirs[i, 0];
                    int midR = coord.Rank + OrthoDirs[i, 1];
                    int dstF = coord.File + OrthoDirs[i, 0] * 2;
                    int dstR = coord.Rank + OrthoDirs[i, 1] * 2;

                    if (!BoardState.IsInBounds(midF, midR) || !BoardState.IsInBounds(dstF, dstR)) continue;
                    if (board[midF, midR] != null) continue;

                    Piece destPiece = board[dstF, dstR];
                    if (destPiece == null || destPiece.Color != piece.Color)
                    {
                        BoardCoord dst = new BoardCoord(dstF, dstR);
                        if (!legalMoves.Contains(dst))
                        {
                            legalMoves.Add(dst);
                        }
                    }
                }
            }
            else if (piece.Type == PieceType.Pawn)
            {
                // Los peones en campo propio (filas 1-3 Blancas o 4-6 Negras) pueden cerrarse lateralmente 1 casilla libre
                bool inDefensiveHalf = piece.Color == PieceColor.White
                    ? (coord.Rank >= 1 && coord.Rank <= 3)
                    : (coord.Rank >= 4 && coord.Rank <= 6);

                if (!inDefensiveHalf) return;

                for (int df = -1; df <= 1; df += 2)
                {
                    int sideF = coord.File + df;
                    if (BoardState.IsInBounds(sideF, coord.Rank) && board[sideF, coord.Rank] == null)
                    {
                        BoardCoord sideCoord = new BoardCoord(sideF, coord.Rank);
                        if (!legalMoves.Contains(sideCoord))
                        {
                            legalMoves.Add(sideCoord);
                        }
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


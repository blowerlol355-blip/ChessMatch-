using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Carta de Gran Maestro: "Garry Kaspárov - El Ogro de Bakú" ("Presión Absoluta").
    /// Durante el turno actual, permite a cualquier Caballo o Alfil aliado desplazarse 1 casilla ortogonal adicional,
    /// y aplica el estado "Intimidación" sobre el siguiente turno de la IA: ChessAI tiene prohibido mover
    /// su pieza de mayor valor (Dama o Torre) en su próximo turno.
    /// </summary>
    [Serializable]
    public class GarryKasparovEffect : ICardEffect, IMoveModifier
    {
        public static event Action<PieceColor> OnIntimidationApplied;
        public static event Action OnIntimidationCancelled;

        [SerializeField] private bool isActive = false;
        [SerializeField] private PieceColor activeColor = PieceColor.White;
        [SerializeField] private PieceColor intimidatedColor = PieceColor.Black;
        [SerializeField] private bool appliedIntimidationToBoard = false;

        private static readonly int[,] OrthoDirs =
        {
            { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 }
        };

        public bool IsActive => isActive;
        public PieceColor ActiveColor => activeColor;
        public PieceColor IntimidatedColor => intimidatedColor;

        public bool CanApply(BoardState board, PieceColor turnColor)
        {
            if (board == null) return false;

            for (int f = 0; f < BoardState.BoardSize; f++)
            {
                for (int r = 0; r < BoardState.BoardSize; r++)
                {
                    Piece p = board[f, r];
                    if (p != null && p.Color == turnColor)
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
            intimidatedColor = activeColor == PieceColor.White ? PieceColor.Black : PieceColor.White;

            board.ApplyIntimidation(intimidatedColor, 1);
            appliedIntimidationToBoard = true;

            OnIntimidationApplied?.Invoke(intimidatedColor);
            SafeLog($"<color=#F24444><b>[GarryKasparovEffect]</b> Presión Absoluta: Caballos/Alfiles ganan +1 paso ortogonal e IA ({intimidatedColor}) queda bajo Intimidación su próximo turno.</color>");
        }

        /// <summary>
        /// Revierte la Intimidación en el tablero si el jugador cancela la carta antes de realizar su movimiento.
        /// </summary>
        public void CancelPendingIntimidation(BoardState board)
        {
            if (appliedIntimidationToBoard && board != null)
            {
                board.ClearIntimidation();
                appliedIntimidationToBoard = false;
                OnIntimidationCancelled?.Invoke();
            }
        }

        public void ModifyMoves(Piece piece, BoardCoord origin, BoardState board, List<BoardCoord> legalMoves)
        {
            if (!isActive || board == null || piece == null || legalMoves == null) return;
            if (piece.Color != activeColor) return;
            if (piece.Type != PieceType.Knight && piece.Type != PieceType.Bishop) return;

            for (int d = 0; d < 4; d++)
            {
                int nf = origin.File + OrthoDirs[d, 0];
                int nr = origin.Rank + OrthoDirs[d, 1];
                if (!BoardState.IsInBounds(nf, nr)) continue;

                Piece target = board[nf, nr];
                if (target == null || (target.Color != piece.Color && target.Type != PieceType.King))
                {
                    BoardCoord dest = new BoardCoord(nf, nr);
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
            if (piece.Type != PieceType.Knight && piece.Type != PieceType.Bishop) return;

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
            // Solo termina la mejora de movimiento de este turno; la Intimidación sobre la IA permanece
            // activa hasta el final del siguiente movimiento de la IA (gestionado por BoardState.MakeMove).
            isActive = false;
            appliedIntimidationToBoard = false;
        }

        private static void SafeLog(string message)
        {
            try
            {
                Debug.Log(message);
            }
            catch (Exception)
            {
                // Ignorar en entornos de test .NET sin runtime nativo de Unity
            }
        }
    }
}

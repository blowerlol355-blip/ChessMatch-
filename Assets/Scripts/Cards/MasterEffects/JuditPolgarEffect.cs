using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Carta de Gran Maestro: "Judit Polgár - Reina de la Tormenta" ("Ataque Implacable").
    /// Modifica temporalmente MoveValidator durante el turno actual para que los Alfiles (Bishop)
    /// y la Dama (Queen) del jugador puedan saltar por encima de exactamente 1 pieza bloqueadora
    /// en su línea/diagonal para capturar una pieza enemiga situada detrás.
    /// Si el movimiento resultante da Jaque, suma +3 puntos al CaptureRewardManager.
    /// </summary>
    [Serializable]
    public class JuditPolgarEffect : ICardEffect, IMoveModifier
    {
        public const int CheckBonusPoints = 3;

        public static event Action<PieceColor> OnRelentlessAttackActivated;

        [SerializeField] private bool isActive = false;
        [SerializeField] private PieceColor activeColor = PieceColor.White;

        private static readonly int[,] BishopDirs =
        {
            { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 }
        };

        private static readonly int[,] QueenDirs =
        {
            { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 },
            { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 }
        };

        public bool IsActive => isActive;
        public PieceColor ActiveColor => activeColor;

        public static bool HasActiveEffect(BoardState board, PieceColor playerColor)
        {
            if (board == null || board.ActiveCardEffects == null) return false;

            for (int i = 0; i < board.ActiveCardEffects.Count; i++)
            {
                if (board.ActiveCardEffects[i] is JuditPolgarEffect polgar &&
                    polgar.IsActive &&
                    polgar.ActiveColor == playerColor)
                {
                    return true;
                }
            }
            return false;
        }

        public bool CanApply(BoardState board, PieceColor turnColor)
        {
            if (board == null) return false;

            for (int f = 0; f < BoardState.BoardSize; f++)
            {
                for (int r = 0; r < BoardState.BoardSize; r++)
                {
                    Piece p = board[f, r];
                    if (p != null && p.Color == turnColor && (p.Type == PieceType.Bishop || p.Type == PieceType.Queen))
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
            OnRelentlessAttackActivated?.Invoke(activeColor);
            SafeLog("<color=#FF7733><b>[JuditPolgarEffect]</b> Ataque Implacable activo: Alfiles y Dama pueden saltar 1 bloqueador para capturar detrás (+3 pts al dar Jaque).</color>");
        }

        public void ModifyMoves(Piece piece, BoardCoord origin, BoardState board, List<BoardCoord> legalMoves)
        {
            if (!isActive || board == null || piece == null || legalMoves == null) return;
            if (piece.Color != activeColor) return;

            int[,] dirs;
            if (piece.Type == PieceType.Bishop)
            {
                dirs = BishopDirs;
            }
            else if (piece.Type == PieceType.Queen)
            {
                dirs = QueenDirs;
            }
            else
            {
                return;
            }

            int dirCount = dirs.GetLength(0);
            for (int d = 0; d < dirCount; d++)
            {
                int df = dirs[d, 0];
                int dr = dirs[d, 1];
                int f = origin.File + df;
                int r = origin.Rank + dr;
                int blockersSeen = 0;

                while (BoardState.IsInBounds(f, r))
                {
                    Piece target = board[f, r];
                    if (target != null)
                    {
                        if (blockersSeen == 0)
                        {
                            // Exactamente 1 pieza bloqueadora encontrada: la atravesamos para buscar objetivo detrás.
                            blockersSeen = 1;
                        }
                        else
                        {
                            // Primera pieza encontrada detrás del bloqueador: solo captura si es rival (y no el Rey).
                            if (target.Color != piece.Color && target.Type != PieceType.King)
                            {
                                BoardCoord dest = new BoardCoord(f, r);
                                if (!legalMoves.Contains(dest))
                                {
                                    legalMoves.Add(dest);
                                }
                            }
                            break;
                        }
                    }

                    f += df;
                    r += dr;
                }
            }
        }

        public void ModifyPseudoLegalMoves(BoardState board, BoardCoord origin, Piece piece, List<Move> moves)
        {
            if (!isActive || board == null || piece == null || moves == null) return;
            if (piece.Color != activeColor) return;
            if (piece.Type != PieceType.Bishop && piece.Type != PieceType.Queen) return;

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

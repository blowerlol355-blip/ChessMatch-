using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Carta de Gran Maestro: "Mijaíl Tal - El Mago de Riga" ("Bosque Oscuro").
    /// Permite al jugador seleccionar una pieza aliada (Caballo, Alfil, Torre o Dama) para sacrificarla.
    /// Al sacrificarla, otorga 2 turnos consecutivos al jugador en esa misma ronda (BoardManager.GrantExtraTurn(1))
    /// e inmoviliza por 1 turno a todas las piezas enemigas situadas en un radio de 1 casilla alrededor del sacrificio.
    /// </summary>
    [Serializable]
    public class MikhailTalEffect : ICardEffect
    {
        public static event Action<PieceColor> OnSacrificeTargetingStarted;
        public static event Action OnSacrificeTargetingCancelled;
        public static event Action<BoardCoord, List<BoardCoord>> OnSacrificeExecuted;

        [SerializeField] private bool isActive = false;
        [SerializeField] private bool isAwaitingSacrificeTarget = false;
        [SerializeField] private bool hasSacrificed = false;
        [SerializeField] private PieceColor activeColor = PieceColor.White;

        public bool IsActive => isActive;
        public bool IsAwaitingSacrificeTarget => isAwaitingSacrificeTarget;
        public bool HasSacrificed => hasSacrificed;
        public PieceColor ActiveColor => activeColor;

        public static bool IsValidSacrificePiece(Piece piece, PieceColor ownerColor)
        {
            if (piece == null || piece.Color != ownerColor) return false;

            return piece.Type == PieceType.Knight ||
                   piece.Type == PieceType.Bishop ||
                   piece.Type == PieceType.Rook ||
                   piece.Type == PieceType.Queen;
        }

        public static List<BoardCoord> GetValidSacrificeCoords(BoardState board, PieceColor ownerColor)
        {
            var coords = new List<BoardCoord>();
            if (board == null) return coords;

            for (int f = 0; f < BoardState.BoardSize; f++)
            {
                for (int r = 0; r < BoardState.BoardSize; r++)
                {
                    Piece p = board[f, r];
                    if (IsValidSacrificePiece(p, ownerColor))
                    {
                        coords.Add(new BoardCoord(f, r));
                    }
                }
            }
            return coords;
        }

        public bool CanApply(BoardState board, PieceColor turnColor)
        {
            if (board == null) return false;
            return GetValidSacrificeCoords(board, turnColor).Count > 0;
        }

        public void ApplyEffect(BoardState board, BoardCoord? targetCoord = null)
        {
            if (board == null) return;

            isActive = true;
            hasSacrificed = false;
            activeColor = board.CurrentTurn;

            if (targetCoord.HasValue && targetCoord.Value.IsValid)
            {
                isAwaitingSacrificeTarget = false;
                ExecuteSacrifice(board, targetCoord.Value, null);
                return;
            }

            isAwaitingSacrificeTarget = true;
            OnSacrificeTargetingStarted?.Invoke(activeColor);
            SafeLog("<color=#C066FF><b>[MikhailTalEffect - Bosque Oscuro]</b> Selecciona un Caballo, Alfil, Torre o Dama aliado para sacrificar.</color>");
        }

        /// <summary>
        /// Ejecuta el sacrificio sobre la casilla elegida:
        /// 1. Elimina la pieza aliada del tablero.
        /// 2. Inmoviliza por 1 turno a las piezas enemigas en radio de 1 casilla (Chebyshev <= 1).
        /// 3. Concede 2 turnos consecutivos en esta ronda mediante BoardManager.GrantExtraTurn(1).
        /// </summary>
        public bool ExecuteSacrifice(BoardState board, BoardCoord sacrificeCoord, BoardManager boardManager = null)
        {
            if (board == null || !BoardState.IsInBounds(sacrificeCoord)) return false;

            Piece target = board[sacrificeCoord];
            PieceColor ownerColor = isActive ? activeColor : board.CurrentTurn;
            if (!IsValidSacrificePiece(target, ownerColor)) return false;

            // 1. Eliminar la pieza sacrificada del tablero
            board[sacrificeCoord] = null;

            // 2. Inmovilizar por 1 turno a las piezas enemigas en un radio de 1 casilla alrededor del sacrificio
            var immobilizedCoords = new List<BoardCoord>();
            for (int df = -1; df <= 1; df++)
            {
                for (int dr = -1; dr <= 1; dr++)
                {
                    if (df == 0 && dr == 0) continue;

                    int nf = sacrificeCoord.File + df;
                    int nr = sacrificeCoord.Rank + dr;
                    if (!BoardState.IsInBounds(nf, nr)) continue;

                    Piece nearby = board[nf, nr];
                    if (nearby != null && nearby.Color != ownerColor)
                    {
                        board.ImmobilizePiece(nearby, 1);
                        immobilizedCoords.Add(new BoardCoord(nf, nr));
                    }
                }
            }

            // 3. Otorgar 2 turnos consecutivos al jugador en la ronda actual (+1 turno extra tras la próxima jugada)
            if (boardManager != null)
            {
                boardManager.GrantExtraTurn(1);
            }
            else
            {
                board.GrantExtraTurn(ownerColor, 1);
            }

            hasSacrificed = true;
            isAwaitingSacrificeTarget = false;
            isActive = false;

            OnSacrificeExecuted?.Invoke(sacrificeCoord, immobilizedCoords);
            SafeLog($"<color=#C066FF><b>[MikhailTalEffect]</b> ¡Bosque Oscuro activado! {target.Type} sacrificado en {sacrificeCoord.ToAlgebraic()}. {immobilizedCoords.Count} piezas rivales inmovilizadas 1 turno y +1 turno consecutivo otorgado.</color>");
            return true;
        }

        public void ModifyMoves(Piece piece, BoardCoord coord, BoardState board, List<BoardCoord> legalMoves)
        {
            // Bosque Oscuro modifica la estructura del turno y movilidad rival, no el rango de piezas individuales.
        }

        public void ResetEffect()
        {
            bool wasWaiting = isAwaitingSacrificeTarget;
            isAwaitingSacrificeTarget = false;
            isActive = false;
            hasSacrificed = false;

            if (wasWaiting)
            {
                OnSacrificeTargetingCancelled?.Invoke();
            }
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

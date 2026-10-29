using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Efecto concreto "Peón Acelerado" (Turbo Pawn).
    /// Permite a cualquier peón del bando activo avanzar 2 casillas hacia adelante
    /// en cualquier momento del turno (no solo desde su fila inicial), siempre que
    /// la casilla intermedia y de destino estén despejadas y dentro del tablero.
    /// Implementa ICardEffect e IMoveModifier para garantizar máxima compatibilidad
    /// con el motor de validación MoveValidator y la simulación de prevención de jaque.
    /// </summary>
    [Serializable]
    public class TurboPawnEffect : ICardEffect, IMoveModifier
    {
        [SerializeField] private bool isActive = false;
        [SerializeField] private PieceColor activeColor = PieceColor.White;
        private BoardCoord? restrictedCoord = null;

        public bool IsActive => isActive;
        public PieceColor ActiveColor => activeColor;

        /// <summary>
        /// Comprueba si el efecto se puede aplicar en el estado actual del tablero.
        /// </summary>
        public bool CanApply(BoardState board, PieceColor turnColor)
        {
            if (board == null) return false;

            // Comprueba que al menos exista un peón del color activo en el tablero
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

        /// <summary>
        /// Activa el efecto para el bando en turno sobre el tablero.
        /// </summary>
        public void ApplyEffect(BoardState board, BoardCoord? targetCoord = null)
        {
            if (board == null) return;

            isActive = true;
            activeColor = board.CurrentTurn;
            restrictedCoord = targetCoord;
        }

        /// <summary>
        /// Inyecta casillas de avance de 2 pasos a los peones en la lista de destinos legales.
        /// </summary>
        public void ModifyMoves(Piece piece, BoardCoord coord, BoardState board, List<BoardCoord> legalMoves)
        {
            if (!isActive || board == null || piece == null || legalMoves == null) return;
            if (piece.Type != PieceType.Pawn || piece.Color != activeColor) return;

            // Si se restringió a un peón específico y este no coincide, ignorar
            if (restrictedCoord.HasValue && !restrictedCoord.Value.Equals(coord)) return;

            int forward = piece.Color == PieceColor.White ? 1 : -1;
            int intermediateRank = coord.Rank + forward;
            int targetRank = coord.Rank + (forward * 2);

            // Validar límites del tablero
            if (!BoardState.IsInBounds(coord.File, targetRank) || !BoardState.IsInBounds(coord.File, intermediateRank))
                return;

            // Tanto la casilla intermedia como la casilla objetivo de 2 pasos deben estar vacías
            if (board[coord.File, intermediateRank] == null && board[coord.File, targetRank] == null)
            {
                BoardCoord doubleStepCoord = new BoardCoord(coord.File, targetRank);
                if (!legalMoves.Contains(doubleStepCoord))
                {
                    legalMoves.Add(doubleStepCoord);
                }
            }
        }

        /// <summary>
        /// Adaptador IMoveModifier para que MoveValidator lo consuma de forma nativa en GetPseudoLegalMoves.
        /// </summary>
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
                        // Si el doble salto alcanza la última fila, agregar movimientos con coronación
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

        /// <summary>
        /// Restablece y desactiva el efecto.
        /// </summary>
        public void ResetEffect()
        {
            isActive = false;
            restrictedCoord = null;
        }
    }
}

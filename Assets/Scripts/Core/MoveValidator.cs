using System;
using System.Collections.Generic;

namespace ChessBalatro
{
    /// <summary>
    /// Validador central y generador de movimientos de ajedrez en C# puro.
    /// Separa movimientos geométricos (pseudo-legales) de los movimientos legales (libres de auto-jaque).
    /// Extensible mediante modificadores para soportar mecánicas de cartas estilo roguelike.
    /// </summary>
    public static class MoveValidator
    {
        private static readonly int[] KnightFileOffsets = { 1, 2, 2, 1, -1, -2, -2, -1 };
        private static readonly int[] KnightRankOffsets = { 2, 1, -1, -2, -2, -1, 1, 2 };

        private static readonly int[,] StraightDirections = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
        private static readonly int[,] DiagonalDirections = { { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 } };

        /// <summary>
        /// Hook global opcional para alterar o inyectar reglas de movimiento adicionales.
        /// </summary>
        public static Action<BoardState, BoardCoord, Piece, List<Move>> CustomMoveRule;

        /// <summary>
        /// Obtiene todos los movimientos legales de una pieza, filtrando rigurosamente
        /// cualquier jugada que deje o mantenga al propio Rey en Jaque (evitando auto-jaque).
        /// </summary>
        public static List<Move> GetLegalMoves(BoardState board, BoardCoord from, bool checkTurn = true)
        {
            List<Move> legalMoves = new List<Move>();
            if (board == null || !BoardState.IsInBounds(from)) return legalMoves;

            Piece piece = board[from];
            if (piece == null) return legalMoves;

            // Validación de turno
            if (checkTurn && piece.Color != board.CurrentTurn)
                return legalMoves;

            // 1. Obtener movimientos geométricos (pseudo-legales)
            List<Move> pseudoMoves = GetPseudoLegalMoves(board, from);

            // 2. Simular cada movimiento y filtrar aquellos que dejen al rey en jaque
            for (int i = 0; i < pseudoMoves.Count; i++)
            {
                Move move = pseudoMoves[i];

                // Simulación eficiente sin instanciar un nuevo tablero completo
                BoardMoveUndo undo = board.SimulateMove(move);
                bool leavesKingInCheck = board.IsInCheck(piece.Color);
                board.UndoMove(undo);

                if (!leavesKingInCheck)
                {
                    legalMoves.Add(move);
                }
            }

            return legalMoves;
        }

        /// <summary>
        /// Obtiene los movimientos pseudo-legales (geométricamente válidos según el tipo de pieza y modificadores,
        /// sin verificar aún si exponen al Rey propio a jaque).
        /// </summary>
        public static List<Move> GetPseudoLegalMoves(BoardState board, BoardCoord from)
        {
            List<Move> moves = new List<Move>();
            if (board == null || !BoardState.IsInBounds(from)) return moves;

            Piece piece = board[from];
            if (piece == null) return moves;

            // Movimientos estándar según la geometría de la pieza
            switch (piece.Type)
            {
                case PieceType.Pawn:
                    GetPawnMoves(board, from, piece, moves);
                    break;
                case PieceType.Knight:
                    GetKnightMoves(board, from, piece, moves);
                    break;
                case PieceType.Bishop:
                    GetBishopMoves(board, from, piece, moves);
                    break;
                case PieceType.Rook:
                    GetRookMoves(board, from, piece, moves);
                    break;
                case PieceType.Queen:
                    GetQueenMoves(board, from, piece, moves);
                    break;
                case PieceType.King:
                    GetKingMoves(board, from, piece, moves);
                    break;
            }

            // Aplicación de modificadores de cartas roguelike activos en el tablero
            if (board.Modifiers != null && board.Modifiers.Count > 0)
            {
                for (int m = 0; m < board.Modifiers.Count; m++)
                {
                    board.Modifiers[m]?.ModifyPseudoLegalMoves(board, from, piece, moves);
                }
            }

            // Aplicación de efectos de cartas ICardEffect activos en el tablero
            if (board.ActiveCardEffects != null && board.ActiveCardEffects.Count > 0)
            {
                List<BoardCoord> extraCoords = new List<BoardCoord>();
                for (int c = 0; c < board.ActiveCardEffects.Count; c++)
                {
                    var effect = board.ActiveCardEffects[c];
                    // Evitar duplicar si el efecto ya implementa IMoveModifier y fue ejecutado arriba
                    if (effect != null && !(effect is IMoveModifier mod && board.Modifiers != null && board.Modifiers.Contains(mod)))
                    {
                        extraCoords.Clear();
                        effect.ModifyMoves(piece, from, board, extraCoords);
                        for (int k = 0; k < extraCoords.Count; k++)
                        {
                            BoardCoord dest = extraCoords[k];
                            if (BoardState.IsInBounds(dest) && !moves.Exists(m => m.To == dest))
                            {
                                moves.Add(new Move(from, dest, piece, board[dest]));
                            }
                        }
                    }
                }
            }

            // Hook delegado global
            CustomMoveRule?.Invoke(board, from, piece, moves);

            return moves;
        }

        /// <summary>
        /// Comprueba de forma ultra-rápida con salida temprana si un bando tiene al menos un movimiento legal disponible.
        /// </summary>
        public static bool HasLegalMoves(BoardState board, PieceColor color)
        {
            if (board == null) return false;

            for (int f = 0; f < BoardState.BoardSize; f++)
            {
                for (int r = 0; r < BoardState.BoardSize; r++)
                {
                    Piece p = board[f, r];
                    if (p != null && p.Color == color)
                    {
                        List<Move> moves = GetLegalMoves(board, new BoardCoord(f, r), checkTurn: false);
                        if (moves.Count > 0)
                        {
                            return true; // Salida temprana óptima
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Obtiene la colección completa de todos los movimientos legales de un bando.
        /// </summary>
        public static List<Move> GetAllLegalMoves(BoardState board, PieceColor color)
        {
            List<Move> result = new List<Move>();
            if (board == null) return result;

            for (int f = 0; f < BoardState.BoardSize; f++)
            {
                for (int r = 0; r < BoardState.BoardSize; r++)
                {
                    Piece p = board[f, r];
                    if (p != null && p.Color == color)
                    {
                        result.AddRange(GetLegalMoves(board, new BoardCoord(f, r), checkTurn: false));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Comprueba si el bando especificado se encuentra en Jaque Mate.
        /// (El rey está en jaque y no existe ningún movimiento legal que lo salve).
        /// </summary>
        public static bool IsCheckmate(BoardState board, PieceColor color)
        {
            if (board == null) return false;
            return board.IsInCheck(color) && !HasLegalMoves(board, color);
        }

        /// <summary>
        /// Comprueba si se ha producido Rey Ahogado (Stalemate / Tablas).
        /// (El rey no está en jaque, pero el jugador no tiene ningún movimiento legal disponible).
        /// </summary>
        public static bool IsStalemate(BoardState board, PieceColor color)
        {
            if (board == null) return false;
            return !board.IsInCheck(color) && !HasLegalMoves(board, color);
        }

        /// <summary>
        /// Comprueba si una casilla está amenazada/atacada por piezas de un color determinado.
        /// </summary>
        public static bool IsSquareAttacked(BoardState board, BoardCoord square, PieceColor attackerColor)
        {
            if (board == null) return false;
            return board.IsSquareAttacked(square, attackerColor);
        }

        /// <summary>
        /// Comprueba si el rey de un color está en jaque.
        /// </summary>
        public static bool IsInCheck(BoardState board, PieceColor color)
        {
            if (board == null) return false;
            return board.IsInCheck(color);
        }

        // =========================================================================
        // Generadores Geométricos de Movimientos Pseudo-Legales
        // =========================================================================

        public static void GetPawnMoves(BoardState board, BoardCoord from, Piece pawn, List<Move> moves)
        {
            int forward = pawn.Color == PieceColor.White ? 1 : -1;
            int startRank = pawn.Color == PieceColor.White ? 1 : 6;
            int promotionRank = pawn.Color == PieceColor.White ? 7 : 0;

            // 1 paso al frente
            int nextRank = from.Rank + forward;
            if (BoardState.IsInBounds(from.File, nextRank) && board[from.File, nextRank] == null)
            {
                BoardCoord to = new BoardCoord(from.File, nextRank);
                if (nextRank == promotionRank)
                {
                    AddPromotionMoves(from, to, pawn, null, moves);
                }
                else
                {
                    moves.Add(new Move(from, to, pawn));
                }

                // 2 pasos desde casilla inicial
                int doubleRank = from.Rank + (forward * 2);
                if (from.Rank == startRank && BoardState.IsInBounds(from.File, doubleRank) && board[from.File, doubleRank] == null)
                {
                    moves.Add(new Move(from, new BoardCoord(from.File, doubleRank), pawn));
                }
            }

            // Capturas diagonales
            int[] fileOffsets = { -1, 1 };
            for (int i = 0; i < fileOffsets.Length; i++)
            {
                int targetFile = from.File + fileOffsets[i];
                if (!BoardState.IsInBounds(targetFile, nextRank)) continue;

                BoardCoord to = new BoardCoord(targetFile, nextRank);
                Piece targetPiece = board[to];

                if (targetPiece != null && targetPiece.Color != pawn.Color)
                {
                    if (nextRank == promotionRank)
                    {
                        AddPromotionMoves(from, to, pawn, targetPiece, moves);
                    }
                    else
                    {
                        moves.Add(new Move(from, to, pawn, targetPiece));
                    }
                }
                else if (board.EnPassantTarget.HasValue && board.EnPassantTarget.Value == to)
                {
                    // Captura al paso (En passant)
                    Piece capturedPawn = board[targetFile, from.Rank];
                    moves.Add(new Move(from, to, pawn, capturedPawn) { IsEnPassant = true });
                }
            }
        }

        private static void AddPromotionMoves(BoardCoord from, BoardCoord to, Piece piece, Piece captured, List<Move> moves)
        {
            moves.Add(new Move(from, to, piece, captured) { PromotionType = PieceType.Queen });
            moves.Add(new Move(from, to, piece, captured) { PromotionType = PieceType.Rook });
            moves.Add(new Move(from, to, piece, captured) { PromotionType = PieceType.Bishop });
            moves.Add(new Move(from, to, piece, captured) { PromotionType = PieceType.Knight });
        }

        public static void GetKnightMoves(BoardState board, BoardCoord from, Piece knight, List<Move> moves)
        {
            for (int i = 0; i < 8; i++)
            {
                int f = from.File + KnightFileOffsets[i];
                int r = from.Rank + KnightRankOffsets[i];
                if (!BoardState.IsInBounds(f, r)) continue;

                Piece target = board[f, r];
                if (target == null || target.Color != knight.Color)
                {
                    moves.Add(new Move(from, new BoardCoord(f, r), knight, target));
                }
            }
        }

        public static void GetRookMoves(BoardState board, BoardCoord from, Piece rook, List<Move> moves)
        {
            GenerateSlidingMoves(board, from, rook, moves, StraightDirections);
        }

        public static void GetBishopMoves(BoardState board, BoardCoord from, Piece bishop, List<Move> moves)
        {
            GenerateSlidingMoves(board, from, bishop, moves, DiagonalDirections);
        }

        public static void GetQueenMoves(BoardState board, BoardCoord from, Piece queen, List<Move> moves)
        {
            GetRookMoves(board, from, queen, moves);
            GetBishopMoves(board, from, queen, moves);
        }

        public static void GetKingMoves(BoardState board, BoardCoord from, Piece king, List<Move> moves)
        {
            for (int df = -1; df <= 1; df++)
            {
                for (int dr = -1; dr <= 1; dr++)
                {
                    if (df == 0 && dr == 0) continue;
                    int f = from.File + df;
                    int r = from.Rank + dr;
                    if (!BoardState.IsInBounds(f, r)) continue;

                    Piece target = board[f, r];
                    if (target == null || target.Color != king.Color)
                    {
                        moves.Add(new Move(from, new BoardCoord(f, r), king, target));
                    }
                }
            }

            // Enroques: El rey no puede estar en jaque ni cruzar casillas atacadas
            if (!king.HasMoved && !board.IsInCheck(king.Color))
            {
                PieceColor enemy = king.Color == PieceColor.White ? PieceColor.Black : PieceColor.White;
                int rank = king.Color == PieceColor.White ? 0 : 7;

                // Enroque corto (Kingside: columnas 4 -> 6, torre en 7)
                bool canCastleKingside = king.Color == PieceColor.White ? board.WhiteCanCastleKingside : board.BlackCanCastleKingside;
                if (canCastleKingside && from.Rank == rank && from.File == 4)
                {
                    if (board[5, rank] == null && board[6, rank] == null)
                    {
                        Piece rook = board[7, rank];
                        if (rook != null && rook.Type == PieceType.Rook && !rook.HasMoved)
                        {
                            if (!board.IsSquareAttacked(new BoardCoord(5, rank), enemy) &&
                                !board.IsSquareAttacked(new BoardCoord(6, rank), enemy))
                            {
                                moves.Add(new Move(from, new BoardCoord(6, rank), king) { IsCastling = true });
                            }
                        }
                    }
                }

                // Enroque largo (Queenside: columnas 4 -> 2, torre en 0)
                bool canCastleQueenside = king.Color == PieceColor.White ? board.WhiteCanCastleQueenside : board.BlackCanCastleQueenside;
                if (canCastleQueenside && from.Rank == rank && from.File == 4)
                {
                    if (board[1, rank] == null && board[2, rank] == null && board[3, rank] == null)
                    {
                        Piece rook = board[0, rank];
                        if (rook != null && rook.Type == PieceType.Rook && !rook.HasMoved)
                        {
                            if (!board.IsSquareAttacked(new BoardCoord(3, rank), enemy) &&
                                !board.IsSquareAttacked(new BoardCoord(2, rank), enemy))
                            {
                                moves.Add(new Move(from, new BoardCoord(2, rank), king) { IsCastling = true });
                            }
                        }
                    }
                }
            }
        }

        private static void GenerateSlidingMoves(BoardState board, BoardCoord from, Piece piece, List<Move> moves, int[,] directions)
        {
            int count = directions.GetLength(0);
            for (int i = 0; i < count; i++)
            {
                int df = directions[i, 0];
                int dr = directions[i, 1];
                int f = from.File + df;
                int r = from.Rank + dr;

                while (BoardState.IsInBounds(f, r))
                {
                    Piece target = board[f, r];
                    if (target == null)
                    {
                        moves.Add(new Move(from, new BoardCoord(f, r), piece));
                    }
                    else
                    {
                        if (target.Color != piece.Color)
                        {
                            moves.Add(new Move(from, new BoardCoord(f, r), piece, target));
                        }
                        break;
                    }
                    f += df;
                    r += dr;
                }
            }
        }
    }
}

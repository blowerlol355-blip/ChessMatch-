using System;
using System.Collections.Generic;

namespace ChessBalatro
{
    /// <summary>
    /// Niveles de dificultad para la Inteligencia Artificial del rival.
    /// </summary>
    public enum AIDifficulty
    {
        Easy,   // Aleatorio con 70% de prioridad a capturas
        Medium, // Minimax a profundidad 2 evaluando material básico
        Hard    // Minimax con poda Alfa-Beta (Alpha-Beta Pruning) a profundidad 3 y bonificación posicional central
    }

    /// <summary>
    /// Motor de Inteligencia Artificial para el rival de ajedrez en C# puro (sin dependencias de UnityEngine).
    /// Evalúa rigurosamente movimientos legales generados por MoveValidator respetando cualquier regla modificada.
    /// </summary>
    public static class ChessAI
    {
        // Valores estándar de piezas según especificación:
        // Pawn 10, Knight 30, Bishop 30, Rook 50, Queen 90, King 1000
        public const int PawnValue = 10;
        public const int KnightValue = 30;
        public const int BishopValue = 30;
        public const int RookValue = 50;
        public const int QueenValue = 90;
        public const int KingValue = 1000;

        private const int CheckmateScore = 100000;
        private static readonly Random Rng = new Random();

        /// <summary>
        /// Selecciona la mejor jugada para el color de la IA según el nivel de dificultad seleccionado.
        /// Retorna las coordenadas de origen y destino, o null si no existen movimientos legales (mate/ahogado).
        /// </summary>
        public static (BoardCoord from, BoardCoord to)? GetBestMove(BoardState board, PieceColor aiColor, AIDifficulty difficulty)
        {
            Move bestMove = GetBestMoveObject(board, aiColor, difficulty);
            if (bestMove == null) return null;
            return (bestMove.From, bestMove.To);
        }

        /// <summary>
        /// Obtiene el objeto de jugada completo (Move) con metadatos de coronación, captura y enroque.
        /// </summary>
        public static Move GetBestMoveObject(BoardState board, PieceColor aiColor, AIDifficulty difficulty)
        {
            if (board == null) return null;

            List<Move> legalMoves = MoveValidator.GetAllLegalMoves(board, aiColor);
            if (legalMoves == null || legalMoves.Count == 0) return null;

            return difficulty switch
            {
                AIDifficulty.Easy => SelectEasyMove(legalMoves),
                AIDifficulty.Medium => SelectMediumMove(board, legalMoves, aiColor),
                AIDifficulty.Hard => SelectHardMove(board, legalMoves, aiColor),
                _ => SelectMediumMove(board, legalMoves, aiColor)
            };
        }

        #region Dificultad Fácil (Easy)

        /// <summary>
        /// Dificultad Easy: Selecciona entre movimientos legales aleatorios,
        /// priorizando capturas si existen con un 70% de probabilidad (30% totalmente aleatorio).
        /// </summary>
        private static Move SelectEasyMove(List<Move> legalMoves)
        {
            List<Move> captures = legalMoves.FindAll(m => m.PieceCaptured != null || m.IsEnPassant);

            // 70% de probabilidad de elegir una captura si está disponible
            if (captures.Count > 0 && Rng.NextDouble() < 0.70)
            {
                return captures[Rng.Next(captures.Count)];
            }

            // 30% restante o si no hay capturas disponibles: selección aleatoria
            return legalMoves[Rng.Next(legalMoves.Count)];
        }

        #endregion

        #region Dificultad Media (Medium)

        /// <summary>
        /// Dificultad Medium: Minimax a profundidad 2 evaluando balance de material de piezas.
        /// </summary>
        private static Move SelectMediumMove(BoardState board, List<Move> rootMoves, PieceColor aiColor)
        {
            PieceColor opponentColor = aiColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
            int bestScore = int.MinValue;
            List<Move> bestCandidates = new List<Move>();

            for (int i = 0; i < rootMoves.Count; i++)
            {
                Move move = rootMoves[i];
                BoardMoveUndo undo = board.SimulateMove(move);
                board.CurrentTurn = opponentColor;

                // Nivel 1 (turno del oponente buscando minimizar el puntaje de la IA)
                int score = EvaluateOpponentMin(board, opponentColor, aiColor);

                board.UndoMove(undo);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidates.Clear();
                    bestCandidates.Add(move);
                }
                else if (score == bestScore)
                {
                    bestCandidates.Add(move);
                }
            }

            // Desempate aleatorio entre jugadas de igual puntuación para evitar predictibilidad mecánica
            return bestCandidates.Count > 0 ? bestCandidates[Rng.Next(bestCandidates.Count)] : rootMoves[0];
        }

        private static int EvaluateOpponentMin(BoardState board, PieceColor opponentColor, PieceColor aiColor)
        {
            List<Move> opponentMoves = MoveValidator.GetAllLegalMoves(board, opponentColor);

            if (opponentMoves.Count == 0)
            {
                if (board.IsInCheck(opponentColor))
                {
                    // El oponente está en jaque mate: resultado óptimo para la IA
                    return CheckmateScore;
                }
                // Rey ahogado (tablas)
                return 0;
            }

            int minScore = int.MaxValue;

            for (int i = 0; i < opponentMoves.Count; i++)
            {
                Move oppMove = opponentMoves[i];
                BoardMoveUndo undo = board.SimulateMove(oppMove);
                board.CurrentTurn = aiColor;

                int eval = EvaluateBoardMaterial(board, aiColor);

                board.UndoMove(undo);

                if (eval < minScore)
                {
                    minScore = eval;
                }
            }

            return minScore;
        }

        #endregion

        #region Dificultad Difícil (Hard)

        /// <summary>
        /// Dificultad Hard: Minimax con poda Alfa-Beta (Alpha-Beta Pruning) a profundidad 3,
        /// incorporando ordenamiento de jugadas y bonificación posicional por control de casillas centrales (d4, d5, e4, e5).
        /// </summary>
        private static Move SelectHardMove(BoardState board, List<Move> rootMoves, PieceColor aiColor)
        {
            PieceColor opponentColor = aiColor == PieceColor.White ? PieceColor.Black : PieceColor.White;

            // Ordenamiento previo de jugadas para maximizar las podas Alfa-Beta
            OrderMoves(rootMoves);

            int alpha = -CheckmateScore * 2;
            int beta = CheckmateScore * 2;
            int bestScore = int.MinValue;
            List<Move> bestCandidates = new List<Move>();

            const int searchDepth = 3;

            for (int i = 0; i < rootMoves.Count; i++)
            {
                Move move = rootMoves[i];
                BoardMoveUndo undo = board.SimulateMove(move);
                board.CurrentTurn = opponentColor;

                // Llamada recursiva negamax con poda alfa-beta
                int score = -AlphaBeta(board, searchDepth - 1, -beta, -alpha, opponentColor, aiColor);

                board.UndoMove(undo);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidates.Clear();
                    bestCandidates.Add(move);
                }
                else if (score == bestScore)
                {
                    bestCandidates.Add(move);
                }

                if (score > alpha)
                {
                    alpha = score;
                }
            }

            return bestCandidates.Count > 0 ? bestCandidates[Rng.Next(bestCandidates.Count)] : rootMoves[0];
        }

        private static int AlphaBeta(BoardState board, int depth, int alpha, int beta, PieceColor currentColor, PieceColor aiColor)
        {
            PieceColor nextColor = currentColor == PieceColor.White ? PieceColor.Black : PieceColor.White;

            // Condición de parada por profundidad
            if (depth <= 0)
            {
                return EvaluateBoardHard(board, currentColor);
            }

            List<Move> legalMoves = MoveValidator.GetAllLegalMoves(board, currentColor);

            // Condición de fin de juego (Jaque Mate o Tablas)
            if (legalMoves.Count == 0)
            {
                if (board.IsInCheck(currentColor))
                {
                    // El jugador activo en este nodo recibió jaque mate
                    return -(CheckmateScore + depth * 10);
                }
                // Rey ahogado
                return 0;
            }

            OrderMoves(legalMoves);

            for (int i = 0; i < legalMoves.Count; i++)
            {
                Move move = legalMoves[i];
                BoardMoveUndo undo = board.SimulateMove(move);
                board.CurrentTurn = nextColor;

                int score = -AlphaBeta(board, depth - 1, -beta, -alpha, nextColor, aiColor);

                board.UndoMove(undo);

                if (score >= beta)
                {
                    return beta; // Poda Beta (corte de rama)
                }

                if (score > alpha)
                {
                    alpha = score;
                }
            }

            return alpha;
        }

        #endregion

        #region Heurísticas de Evaluación y Bonificación Central

        /// <summary>
        /// Evaluación estática material pura (para Medium).
        /// </summary>
        public static int EvaluateBoardMaterial(BoardState board, PieceColor perspective)
        {
            int score = 0;

            for (int f = 0; f < BoardState.BoardSize; f++)
            {
                for (int r = 0; r < BoardState.BoardSize; r++)
                {
                    Piece piece = board[f, r];
                    if (piece == null) continue;

                    int value = GetPieceValue(piece.Type);
                    if (piece.Color == perspective)
                    {
                        score += value;
                    }
                    else
                    {
                        score -= value;
                    }
                }
            }

            return score;
        }

        /// <summary>
        /// Evaluación estática avanzada (para Hard) con bonificación posicional por control de casillas centrales (d4, d5, e4, e5).
        /// </summary>
        public static int EvaluateBoardHard(BoardState board, PieceColor perspective)
        {
            int score = 0;

            for (int f = 0; f < BoardState.BoardSize; f++)
            {
                for (int r = 0; r < BoardState.BoardSize; r++)
                {
                    Piece piece = board[f, r];
                    if (piece == null) continue;

                    int val = GetPieceValue(piece.Type);
                    int positionalBonus = 0;

                    // Bonificaciones centrales específicas requeridas: (d4, d5, e4, e5)
                    // d4 = (3,3), d5 = (3,4), e4 = (4,3), e5 = (4,4)
                    bool isCoreCenter = (f == 3 || f == 4) && (r == 3 || r == 4);
                    if (isCoreCenter)
                    {
                        // Mayor relevancia al control central para peones, caballos y alfiles
                        positionalBonus = piece.Type switch
                        {
                            PieceType.Pawn => 4,
                            PieceType.Knight => 5,
                            PieceType.Bishop => 4,
                            PieceType.Queen => 2,
                            _ => 1
                        };
                    }
                    else if (f >= 2 && f <= 5 && r >= 2 && r <= 5)
                    {
                        // Anillo central ampliado (c3 - f6)
                        positionalBonus = 1;
                    }

                    int totalPieceScore = val + positionalBonus;

                    if (piece.Color == perspective)
                    {
                        score += totalPieceScore;
                    }
                    else
                    {
                        score -= totalPieceScore;
                    }
                }
            }

            return score;
        }

        /// <summary>
        /// Valor numérico de cada tipo de pieza según especificación técnica.
        /// </summary>
        public static int GetPieceValue(PieceType type) => type switch
        {
            PieceType.Pawn => PawnValue,
            PieceType.Knight => KnightValue,
            PieceType.Bishop => BishopValue,
            PieceType.Rook => RookValue,
            PieceType.Queen => QueenValue,
            PieceType.King => KingValue,
            _ => 0
        };

        /// <summary>
        /// Ordena movimientos priorizando capturas de mayor valor y avance hacia casillas centrales (MVV-LVA).
        /// </summary>
        private static void OrderMoves(List<Move> moves)
        {
            moves.Sort((a, b) =>
            {
                int scoreA = GetMoveHeuristicScore(a);
                int scoreB = GetMoveHeuristicScore(b);
                return scoreB.CompareTo(scoreA); // Mayor a menor
            });
        }

        private static int GetMoveHeuristicScore(Move move)
        {
            int score = 0;

            // Priorizar capturas (MVV-LVA: Most Valuable Victim - Least Valuable Attacker)
            if (move.PieceCaptured != null)
            {
                score += 100 + GetPieceValue(move.PieceCaptured.Type) * 10 - GetPieceValue(move.PieceMoved?.Type ?? PieceType.Pawn);
            }
            else if (move.IsEnPassant)
            {
                score += 100 + PawnValue * 10;
            }

            // Bonificación por coronación
            if (move.PromotionType != PieceType.None)
            {
                score += GetPieceValue(move.PromotionType) * 5;
            }

            // Bonificación si el destino es una casilla central (d4, d5, e4, e5)
            if ((move.To.File == 3 || move.To.File == 4) && (move.To.Rank == 3 || move.To.Rank == 4))
            {
                score += 8;
            }

            return score;
        }

        #endregion
    }
}

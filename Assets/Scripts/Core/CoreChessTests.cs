using System;
using System.Collections.Generic;

namespace ChessBalatro
{
    /// <summary>
    /// Suite de pruebas unitarias en C# puro para validar la Fase 1 del motor de ajedrez.
    /// Ejecutable tanto en entornos de pruebas como desde un script de validación en Unity.
    /// </summary>
    public static class CoreChessTests
    {
        public struct TestResult
        {
            public string Name;
            public bool Passed;
            public string ErrorMessage;
        }

        public static List<TestResult> RunAllTests()
        {
            List<TestResult> results = new List<TestResult>();

            RunTest(results, "1. Ubicación de Reyes (FindKing)", TestFindKing);
            RunTest(results, "2. Detección de Casillas Atacadas (IsSquareAttacked)", TestSquareAttacked);
            RunTest(results, "3. Movimientos Iniciales y Pseudo-Legales", TestInitialMoves);
            RunTest(results, "4. Prevención de Auto-Jaque (Pieza Clavada)", TestPinnedPiecePreventsSelfCheck);
            RunTest(results, "5. Detección de Jaque (IsInCheck)", TestCheckDetection);
            RunTest(results, "6. Detección de Jaque Mate (Scholar's Mate)", TestScholarsMate);
            RunTest(results, "7. Detección de Rey Ahogado (Stalemate)", TestStalemate);
            RunTest(results, "8. Notación Algebraica Estándar (SAN)", TestAlgebraicNotation);
            RunTest(results, "9. Simulación y Reversión No Destructiva (SimulateMove / UndoMove)", TestSimulationAndUndo);
            RunTest(results, "10. Modificadores de Cartas Roguelike (IMoveModifier)", TestMoveModifier);
            RunTest(results, "11. Despacho de Eventos de Estado (OnCheck, OnCheckmate, OnMoveExecuted)", TestStateEvents);
            RunTest(results, "12. Flujo de Notación y Registro para GameHUDView", TestHUDMoveHistorySequence);
            RunTest(results, "13. Efecto de Carta 'Peón Acelerado' (TurboPawnEffect)", TestTurboPawnEffect);
            RunTest(results, "14. Efecto de Carta 'Salto de Caballería' (KnightBoostEffect)", TestKnightBoostEffect);
            RunTest(results, "15. Ciclo de Vida y Limpieza de Cartas en BoardState", TestCardLifecycleAndTurnFlow);
            RunTest(results, "16. IA Nivel Fácil (Selección Legal y Capturas)", TestChessAIEasy);
            RunTest(results, "17. IA Nivel Medio (Minimax Profundidad 2 y Material)", TestChessAIMedium);
            RunTest(results, "18. IA Nivel Difícil (Alfa-Beta, Mate y Bonificación Central)", TestChessAIHard);

            return results;
        }

        public static void RunTestsFromCommandLine()
        {
            var results = RunAllTests();
            int passed = 0;
            foreach (var r in results)
            {
                if (r.Passed)
                {
                    passed++;
                    UnityEngine.Debug.Log($"[PASS] {r.Name}");
                }
                else
                {
                    UnityEngine.Debug.LogError($"[FAIL] {r.Name} -> {r.ErrorMessage}");
                }
            }
            UnityEngine.Debug.Log($"[SUMMARY] Tests Passed: {passed}/{results.Count}");
        }

        private static void RunTest(List<TestResult> results, string testName, Action testAction)
        {
            try
            {
                testAction();
                results.Add(new TestResult { Name = testName, Passed = true, ErrorMessage = null });
            }
            catch (Exception ex)
            {
                results.Add(new TestResult { Name = testName, Passed = false, ErrorMessage = ex.Message });
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        public static void TestFindKing()
        {
            BoardState board = new BoardState();
            board.InitializeStandardBoard();

            BoardCoord? whiteKing = board.FindKing(PieceColor.White);
            BoardCoord? blackKing = board.FindKing(PieceColor.Black);

            Assert(whiteKing.HasValue && whiteKing.Value == BoardCoord.FromAlgebraic("e1"), "El rey blanco debe estar en e1.");
            Assert(blackKing.HasValue && blackKing.Value == BoardCoord.FromAlgebraic("e8"), "El rey negro debe estar en e8.");
        }

        public static void TestSquareAttacked()
        {
            BoardState board = new BoardState();
            board.Clear();

            // Peón blanco en e4 ataca d5 y f5
            board[BoardCoord.FromAlgebraic("e4")] = new Piece(PieceType.Pawn, PieceColor.White);
            Assert(board.IsSquareAttacked(BoardCoord.FromAlgebraic("d5"), PieceColor.White), "e4 debe atacar d5.");
            Assert(board.IsSquareAttacked(BoardCoord.FromAlgebraic("f5"), PieceColor.White), "e4 debe atacar f5.");
            Assert(!board.IsSquareAttacked(BoardCoord.FromAlgebraic("e5"), PieceColor.White), "Un peón en e4 no ataca e5 (solo avanza).");

            // Caballo blanco en c3 ataca e4, b5, d5, etc.
            board[BoardCoord.FromAlgebraic("c3")] = new Piece(PieceType.Knight, PieceColor.White);
            Assert(board.IsSquareAttacked(BoardCoord.FromAlgebraic("e4"), PieceColor.White), "Caballo en c3 debe atacar e4.");
            Assert(board.IsSquareAttacked(BoardCoord.FromAlgebraic("d5"), PieceColor.White), "Caballo en c3 debe atacar d5.");

            // Torre negra en a8 ataca a1 en línea recta si no hay obstáculos
            board[BoardCoord.FromAlgebraic("a8")] = new Piece(PieceType.Rook, PieceColor.Black);
            Assert(board.IsSquareAttacked(BoardCoord.FromAlgebraic("a1"), PieceColor.Black), "Torre en a8 debe atacar a1 en fila despejada.");

            // Obstáculo en a5 bloquea el ataque de a8 a a1
            board[BoardCoord.FromAlgebraic("a5")] = new Piece(PieceType.Pawn, PieceColor.White);
            Assert(!board.IsSquareAttacked(BoardCoord.FromAlgebraic("a1"), PieceColor.Black), "La torre en a8 debe ser bloqueada por la pieza en a5.");
        }

        public static void TestInitialMoves()
        {
            BoardState board = new BoardState();
            board.InitializeStandardBoard();

            // En la posición inicial el bando blanco debe tener exactamente 20 movimientos legales:
            // 8 peones x 2 movimientos = 16 movimientos de peón
            // 2 caballos x 2 movimientos = 4 movimientos de caballo
            List<Move> allMoves = MoveValidator.GetAllLegalMoves(board, PieceColor.White);
            Assert(allMoves.Count == 20, $"La posición inicial debe tener 20 movimientos legales para las blancas, pero tuvo {allMoves.Count}.");
            Assert(MoveValidator.HasLegalMoves(board, PieceColor.White), "El bando blanco debe tener jugadas legales.");
        }

        public static void TestPinnedPiecePreventsSelfCheck()
        {
            BoardState board = new BoardState();
            board.Clear();

            // Rey blanco en e1
            board[BoardCoord.FromAlgebraic("e1")] = new Piece(PieceType.King, PieceColor.White);
            // Alfil blanco en e2 (clavado por la torre en la columna e)
            board[BoardCoord.FromAlgebraic("e2")] = new Piece(PieceType.Bishop, PieceColor.White);
            // Torre negra en e8
            board[BoardCoord.FromAlgebraic("e8")] = new Piece(PieceType.Rook, PieceColor.Black);
            // Rey negro en h8
            board[BoardCoord.FromAlgebraic("h8")] = new Piece(PieceType.King, PieceColor.Black);

            board.CurrentTurn = PieceColor.White;

            // Movimientos pseudo-legales del alfil: puede moverse en diagonales
            List<Move> pseudoMoves = MoveValidator.GetPseudoLegalMoves(board, BoardCoord.FromAlgebraic("e2"));
            Assert(pseudoMoves.Count > 0, "El alfil tiene movimientos pseudo-legales geométricos.");

            // Movimientos legales: deben ser 0 porque cualquier movimiento del alfil expone al rey en e1 al jaque de la torre en e8
            List<Move> legalMoves = MoveValidator.GetLegalMoves(board, BoardCoord.FromAlgebraic("e2"));
            Assert(legalMoves.Count == 0, $"El alfil clavado no puede moverse porque causaría auto-jaque. Movimientos permitidos: {legalMoves.Count}.");
        }

        public static void TestCheckDetection()
        {
            BoardState board = new BoardState();
            board.Clear();

            board[BoardCoord.FromAlgebraic("e1")] = new Piece(PieceType.King, PieceColor.White);
            board[BoardCoord.FromAlgebraic("e8")] = new Piece(PieceType.King, PieceColor.Black);
            board[BoardCoord.FromAlgebraic("e5")] = new Piece(PieceType.Rook, PieceColor.Black);

            Assert(board.IsInCheck(PieceColor.White), "El rey blanco en e1 debe estar en jaque por la torre en e5.");
            Assert(!board.IsInCheck(PieceColor.Black), "El rey negro no debe estar en jaque.");
        }

        public static void TestScholarsMate()
        {
            BoardState board = new BoardState();
            board.InitializeStandardBoard();

            // Mate del Pastor:
            // 1. e4 e5
            Assert(board.MakeMove(new Move(BoardCoord.FromAlgebraic("e2"), BoardCoord.FromAlgebraic("e4"), board[BoardCoord.FromAlgebraic("e2")])), "1. e4");
            Assert(board.MakeMove(new Move(BoardCoord.FromAlgebraic("e7"), BoardCoord.FromAlgebraic("e5"), board[BoardCoord.FromAlgebraic("e7")])), "1... e5");

            // 2. Bc4 Nc6
            Assert(board.MakeMove(new Move(BoardCoord.FromAlgebraic("f1"), BoardCoord.FromAlgebraic("c4"), board[BoardCoord.FromAlgebraic("f1")])), "2. Bc4");
            Assert(board.MakeMove(new Move(BoardCoord.FromAlgebraic("b8"), BoardCoord.FromAlgebraic("c6"), board[BoardCoord.FromAlgebraic("b8")])), "2... Nc6");

            // 3. Qh5 Nf6
            Assert(board.MakeMove(new Move(BoardCoord.FromAlgebraic("d1"), BoardCoord.FromAlgebraic("h5"), board[BoardCoord.FromAlgebraic("d1")])), "3. Qh5");
            Assert(board.MakeMove(new Move(BoardCoord.FromAlgebraic("g8"), BoardCoord.FromAlgebraic("f6"), board[BoardCoord.FromAlgebraic("g8")])), "3... Nf6");

            // 4. Qxf7#
            Piece whiteQueen = board[BoardCoord.FromAlgebraic("h5")];
            Assert(board.MakeMove(new Move(BoardCoord.FromAlgebraic("h5"), BoardCoord.FromAlgebraic("f7"), whiteQueen)), "4. Qxf7#");

            Assert(board.IsInCheck(PieceColor.Black), "El rey negro debe estar en jaque tras Qxf7.");
            Assert(!board.HasLegalMoves(PieceColor.Black), "El rey negro no debe tener movimientos legales.");
            Assert(board.IsCheckmate(PieceColor.Black), "El bando negro debe estar en Jaque Mate.");

            // Verificar la notación algebraica registrada
            MoveRecord lastRecord = board.History.LastMove.Value;
            Assert(lastRecord.isCheckmate, "El registro de la jugada debe indicar Jaque Mate.");
            Assert(lastRecord.algebraicNotation == "Qxf7#", $"La notación debe ser 'Qxf7#' pero fue '{lastRecord.algebraicNotation}'.");
        }

        public static void TestStalemate()
        {
            BoardState board = new BoardState();
            board.Clear();

            // Posición de Rey Ahogado:
            // Rey negro en a8
            board[BoardCoord.FromAlgebraic("a8")] = new Piece(PieceType.King, PieceColor.Black);
            // Rey blanco en a6
            board[BoardCoord.FromAlgebraic("a6")] = new Piece(PieceType.King, PieceColor.White);
            // Dama blanca en c7
            board[BoardCoord.FromAlgebraic("c7")] = new Piece(PieceType.Queen, PieceColor.White);

            board.CurrentTurn = PieceColor.Black;

            Assert(!board.IsInCheck(PieceColor.Black), "El rey negro en a8 NO está en jaque.");
            Assert(!board.HasLegalMoves(PieceColor.Black), "El rey negro no tiene ningún movimiento legal.");
            Assert(board.IsStalemate(PieceColor.Black), "Debe detectarse Rey Ahogado (Stalemate).");
            Assert(!board.IsCheckmate(PieceColor.Black), "No debe considerarse Jaque Mate.");
        }

        public static void TestAlgebraicNotation()
        {
            Piece whitePawn = new Piece(PieceType.Pawn, PieceColor.White);
            Piece whiteKnight = new Piece(PieceType.Knight, PieceColor.White);
            Piece whiteQueen = new Piece(PieceType.Queen, PieceColor.White);
            Piece whiteBishop = new Piece(PieceType.Bishop, PieceColor.White);
            Piece blackPawn = new Piece(PieceType.Pawn, PieceColor.Black);

            // "e4"
            MoveRecord m1 = new MoveRecord(whitePawn, BoardCoord.FromAlgebraic("e2"), BoardCoord.FromAlgebraic("e4"));
            Assert(m1.AlgebraicNotation == "e4", $"Esperado 'e4', obtenido '{m1.AlgebraicNotation}'");

            // "Nf3"
            MoveRecord m2 = new MoveRecord(whiteKnight, BoardCoord.FromAlgebraic("g1"), BoardCoord.FromAlgebraic("f3"));
            Assert(m2.AlgebraicNotation == "Nf3", $"Esperado 'Nf3', obtenido '{m2.AlgebraicNotation}'");

            // "Qxd5+"
            MoveRecord m3 = new MoveRecord(whiteQueen, BoardCoord.FromAlgebraic("d1"), BoardCoord.FromAlgebraic("d5"),
                pieceCaptured: blackPawn, isCheck: true, isCheckmate: false);
            Assert(m3.AlgebraicNotation == "Qxd5+", $"Esperado 'Qxd5+', obtenido '{m3.AlgebraicNotation}'");

            // "Bxf7#"
            MoveRecord m4 = new MoveRecord(whiteBishop, BoardCoord.FromAlgebraic("c4"), BoardCoord.FromAlgebraic("f7"),
                pieceCaptured: blackPawn, isCheck: true, isCheckmate: true);
            Assert(m4.AlgebraicNotation == "Bxf7#", $"Esperado 'Bxf7#', obtenido '{m4.AlgebraicNotation}'");

            // Enroque corto "O-O"
            Piece whiteKing = new Piece(PieceType.King, PieceColor.White);
            MoveRecord m5 = new MoveRecord(whiteKing, BoardCoord.FromAlgebraic("e1"), BoardCoord.FromAlgebraic("g1"),
                isCastling: true);
            Assert(m5.AlgebraicNotation == "O-O", $"Esperado 'O-O', obtenido '{m5.AlgebraicNotation}'");

            // Captura de peón "exd5"
            MoveRecord m6 = new MoveRecord(whitePawn, BoardCoord.FromAlgebraic("e4"), BoardCoord.FromAlgebraic("d5"),
                pieceCaptured: blackPawn);
            Assert(m6.AlgebraicNotation == "exd5", $"Esperado 'exd5', obtenido '{m6.AlgebraicNotation}'");
        }

        public static void TestSimulationAndUndo()
        {
            BoardState board = new BoardState();
            board.InitializeStandardBoard();

            // Estado previo a la simulación
            Piece initialE2 = board[BoardCoord.FromAlgebraic("e2")];
            Piece initialE4 = board[BoardCoord.FromAlgebraic("e4")];
            PieceColor prevTurn = board.CurrentTurn;

            Move move = new Move(BoardCoord.FromAlgebraic("e2"), BoardCoord.FromAlgebraic("e4"), initialE2);

            // Simular
            BoardMoveUndo undo = board.SimulateMove(move);

            Assert(board[BoardCoord.FromAlgebraic("e2")] == null, "e2 debe estar vacío tras simular e4.");
            Assert(board[BoardCoord.FromAlgebraic("e4")] == initialE2, "e4 debe tener el peón tras simular.");

            // Deshacer simulación
            board.UndoMove(undo);

            Assert(board[BoardCoord.FromAlgebraic("e2")] == initialE2, "e2 debe recuperar el peón original.");
            Assert(board[BoardCoord.FromAlgebraic("e4")] == initialE4, "e4 debe volver a ser null.");
            Assert(board.CurrentTurn == prevTurn, "El turno debe ser restaurado.");
        }

        private class JumpKnightCardModifier : IMoveModifier
        {
            public void ModifyPseudoLegalMoves(BoardState board, BoardCoord origin, Piece piece, List<Move> moves)
            {
                // Si la pieza es un Peón, le añade el poder de saltar 2 casillas laterales
                if (piece.Type == PieceType.Pawn)
                {
                    BoardCoord sideCoord = new BoardCoord(origin.File + 2, origin.Rank);
                    if (BoardState.IsInBounds(sideCoord))
                    {
                        moves.Add(new Move(origin, sideCoord, piece));
                    }
                }
            }
        }

        public static void TestMoveModifier()
        {
            BoardState board = new BoardState();
            board.InitializeStandardBoard();

            // Peón en e2 normalmente solo tiene e3 y e4
            BoardCoord e2 = BoardCoord.FromAlgebraic("e2");
            int normalMovesCount = MoveValidator.GetPseudoLegalMoves(board, e2).Count;
            Assert(normalMovesCount == 2, "El peón en e2 tiene 2 movimientos normales.");

            // Agregar modificador de carta
            board.Modifiers.Add(new JumpKnightCardModifier());

            int modifiedMovesCount = MoveValidator.GetPseudoLegalMoves(board, e2).Count;
            Assert(modifiedMovesCount == 3, "El modificador de carta debió añadir 1 movimiento adicional.");
        }

        public static void TestStateEvents()
        {
            BoardState board = new BoardState();
            board.InitializeStandardBoard();

            bool checkTriggered = false;
            bool checkmateTriggered = false;
            bool moveExecutedTriggered = false;
            PieceColor? checkedSide = null;

            board.OnCheck += (color) =>
            {
                checkTriggered = true;
                checkedSide = color;
            };

            board.OnCheckmate += (color) =>
            {
                checkmateTriggered = true;
            };

            board.OnMoveExecuted += (record) =>
            {
                moveExecutedTriggered = true;
            };

            // Jugada 1: 1. e4
            board.MakeMove(new Move(BoardCoord.FromAlgebraic("e2"), BoardCoord.FromAlgebraic("e4"), board[BoardCoord.FromAlgebraic("e2")]));
            Assert(moveExecutedTriggered, "OnMoveExecuted debió haberse disparado tras 1. e4.");
            Assert(!checkTriggered, "1. e4 no es jaque.");

            // Recrear scholar's mate para verificar eventos de jaque mate
            BoardState mateBoard = new BoardState();
            mateBoard.InitializeStandardBoard();
            mateBoard.OnCheckmate += (color) =>
            {
                checkmateTriggered = true;
            };

            mateBoard.MakeMove(new Move(BoardCoord.FromAlgebraic("e2"), BoardCoord.FromAlgebraic("e4"), mateBoard[BoardCoord.FromAlgebraic("e2")]));
            mateBoard.MakeMove(new Move(BoardCoord.FromAlgebraic("e7"), BoardCoord.FromAlgebraic("e5"), mateBoard[BoardCoord.FromAlgebraic("e7")]));
            mateBoard.MakeMove(new Move(BoardCoord.FromAlgebraic("f1"), BoardCoord.FromAlgebraic("c4"), mateBoard[BoardCoord.FromAlgebraic("f1")]));
            mateBoard.MakeMove(new Move(BoardCoord.FromAlgebraic("b8"), BoardCoord.FromAlgebraic("c6"), mateBoard[BoardCoord.FromAlgebraic("b8")]));
            mateBoard.MakeMove(new Move(BoardCoord.FromAlgebraic("d1"), BoardCoord.FromAlgebraic("h5"), mateBoard[BoardCoord.FromAlgebraic("d1")]));
            mateBoard.MakeMove(new Move(BoardCoord.FromAlgebraic("g8"), BoardCoord.FromAlgebraic("f6"), mateBoard[BoardCoord.FromAlgebraic("g8")]));
            mateBoard.MakeMove(new Move(BoardCoord.FromAlgebraic("h5"), BoardCoord.FromAlgebraic("f7"), mateBoard[BoardCoord.FromAlgebraic("h5")]));

            Assert(checkmateTriggered, "OnCheckmate debió dispararse tras el mate del pastor.");
        }

        public static void TestHUDMoveHistorySequence()
        {
            BoardState board = new BoardState();
            board.InitializeStandardBoard();

            List<string> recordedMoves = new List<string>();
            board.OnMoveExecuted += (record) =>
            {
                recordedMoves.Add(record.AlgebraicNotation);
            };

            // Jugada 1: 1. e4 e5
            board.MakeMove(new Move(BoardCoord.FromAlgebraic("e2"), BoardCoord.FromAlgebraic("e4"), board[BoardCoord.FromAlgebraic("e2")]));
            board.MakeMove(new Move(BoardCoord.FromAlgebraic("e7"), BoardCoord.FromAlgebraic("e5"), board[BoardCoord.FromAlgebraic("e7")]));

            // Jugada 2: 2. Nf3 Nc6
            board.MakeMove(new Move(BoardCoord.FromAlgebraic("g1"), BoardCoord.FromAlgebraic("f3"), board[BoardCoord.FromAlgebraic("g1")]));
            board.MakeMove(new Move(BoardCoord.FromAlgebraic("b8"), BoardCoord.FromAlgebraic("c6"), board[BoardCoord.FromAlgebraic("b8")]));

            Assert(recordedMoves.Count == 4, $"Debieron registrarse 4 jugadas, obtenidas: {recordedMoves.Count}");
            Assert(recordedMoves[0] == "e4", $"Jugada 1 debió ser e4, obtenida: {recordedMoves[0]}");
            Assert(recordedMoves[1] == "e5", $"Jugada 2 debió ser e5, obtenida: {recordedMoves[1]}");
            Assert(recordedMoves[2] == "Nf3", $"Jugada 3 debió ser Nf3, obtenida: {recordedMoves[2]}");
            Assert(recordedMoves[3] == "Nc6", $"Jugada 4 debió ser Nc6, obtenida: {recordedMoves[3]}");
        }

        public static void TestTurboPawnEffect()
        {
            BoardState board = new BoardState();
            board.InitializeStandardBoard();

            // Mover peón de e2 a e3 (fila no inicial)
            board.MakeMove(new Move(BoardCoord.FromAlgebraic("e2"), BoardCoord.FromAlgebraic("e3"), board[BoardCoord.FromAlgebraic("e2")]));
            board.MakeMove(new Move(BoardCoord.FromAlgebraic("a7"), BoardCoord.FromAlgebraic("a6"), board[BoardCoord.FromAlgebraic("a7")])); // Turno de negras pasa

            // Ahora es turno de Blancas. El peón está en e3.
            BoardCoord e3 = BoardCoord.FromAlgebraic("e3");
            List<Move> normalMoves = board.GetLegalMoves(e3);
            Assert(normalMoves.Count == 1, $"Un peón en e3 normalmente solo puede avanzar a e4. Obtenidos: {normalMoves.Count}");
            Assert(normalMoves[0].To == BoardCoord.FromAlgebraic("e4"), "El único movimiento legal normal es e4.");

            // Crear y activar TurboPawnEffect
            TurboPawnEffect turboPawn = new TurboPawnEffect();
            Assert(turboPawn.CanApply(board, PieceColor.White), "Debe poder aplicarse ya que hay peones blancos.");

            turboPawn.ApplyEffect(board);
            board.ActiveCardEffects.Add(turboPawn);

            // Ahora el peón en e3 debe poder avanzar a e4 y a e5 (doble salto)
            List<Move> modifiedMoves = board.GetLegalMoves(e3);
            Assert(modifiedMoves.Count == 2, $"Con Peón Acelerado debe poder avanzar a e4 y a e5. Obtenidos: {modifiedMoves.Count}");
            Assert(modifiedMoves.Exists(m => m.To == BoardCoord.FromAlgebraic("e5")), "Debe existir la opción de avanzar a e5.");

            // Ejecutar el movimiento turbo a e5
            Move turboMove = modifiedMoves.Find(m => m.To == BoardCoord.FromAlgebraic("e5"));
            bool moveSuccess = board.MakeMove(turboMove);
            Assert(moveSuccess, "La jugada turbo a e5 debe ser validada y ejecutada exitosamente.");
            Assert(board[BoardCoord.FromAlgebraic("e5")] != null, "La pieza debe estar ahora en e5.");
            Assert(board[BoardCoord.FromAlgebraic("e3")] == null, "La casilla e3 debe quedar vacía.");

            // Limpieza de efecto
            turboPawn.ResetEffect();
            board.ActiveCardEffects.Remove(turboPawn);
        }

        public static void TestKnightBoostEffect()
        {
            BoardState board = new BoardState();
            board.InitializeStandardBoard();

            // Caballo blanco en b1 normalmente solo puede ir a a3 y c3 (los demás bloqueados por peones)
            BoardCoord b1 = BoardCoord.FromAlgebraic("b1");
            int normalMoves = board.GetLegalMoves(b1).Count;
            Assert(normalMoves == 2, $"Caballo en b1 debe tener 2 jugadas en L disponibles (a3, c3). Obtenidas: {normalMoves}");

            KnightBoostEffect knightBoost = new KnightBoostEffect();
            Assert(knightBoost.CanApply(board, PieceColor.White), "Debe poder aplicarse a las blancas.");

            knightBoost.ApplyEffect(board);
            board.ActiveCardEffects.Add(knightBoost);

            // Para probar el boost ortogonal sin interferencia de piezas iniciales, colocamos un caballo en d4
            board[BoardCoord.FromAlgebraic("b1")] = null;
            Piece knight = new Piece(PieceType.Knight, PieceColor.White);
            BoardCoord d4 = BoardCoord.FromAlgebraic("d4");
            board[d4] = knight;

            // En d4 despejado, el caballo tiene 8 saltos en L + 4 movimientos ortogonales (d5, d3, c4, e4) = 12 jugadas
            List<Move> boostedMoves = board.GetLegalMoves(d4);
            Assert(boostedMoves.Exists(m => m.To == BoardCoord.FromAlgebraic("d5")), "KnightBoost debe permitir d5 ortogonal.");
            Assert(boostedMoves.Exists(m => m.To == BoardCoord.FromAlgebraic("e4")), "KnightBoost debe permitir e4 ortogonal.");
            Assert(boostedMoves.Count == 12, $"En d4 despejado debe tener 8 saltos en L + 4 ortogonales = 12 jugadas. Obtenidas: {boostedMoves.Count}");

            knightBoost.ResetEffect();
            board.ActiveCardEffects.Remove(knightBoost);

            List<Move> normalCenterMoves = board.GetLegalMoves(d4);
            Assert(normalCenterMoves.Count == 8, $"Al resetear, el caballo vuelve a sus 8 saltos estándar. Obtenidas: {normalCenterMoves.Count}");
        }

        public static void TestCardLifecycleAndTurnFlow()
        {
            BoardState board = new BoardState();
            board.InitializeStandardBoard();

            TurboPawnCardData cardData = TurboPawnCardData.CreateInstance();
            Assert(cardData.CardName == "Peón Acelerado", "El nombre de la carta debe ser Peón Acelerado.");
            Assert(cardData.Cost == 3, "El coste de la carta debe ser 3.");
            Assert(cardData.Rarity == CardRarity.Common, "La rareza debe ser Común.");

            ICardEffect effect = cardData.GetOrCreateEffect();
            Assert(effect != null, "El efecto no debe ser nulo.");

            // Aplicar efecto
            effect.ApplyEffect(board);
            board.ActiveCardEffects.Add(effect);
            Assert(board.ActiveCardEffects.Count == 1, "Debe haber 1 efecto activo.");

            // Resetear efecto
            effect.ResetEffect();
            board.ActiveCardEffects.Clear();
            Assert(board.ActiveCardEffects.Count == 0, "Debe quedar 0 efectos tras limpiar.");
        }

        public static void TestChessAIEasy()
        {
            BoardState board = new BoardState();
            board.InitializeStandardBoard();
            board.CurrentTurn = PieceColor.Black;

            var move = ChessAI.GetBestMove(board, PieceColor.Black, AIDifficulty.Easy);
            Assert(move.HasValue, "La IA en Easy debe retornar un movimiento legal inicial.");

            List<Move> legal = MoveValidator.GetLegalMoves(board, move.Value.from);
            Assert(legal.Exists(m => m.To == move.Value.to), "La jugada elegida por Easy debe ser estrictamente legal.");
        }

        public static void TestChessAIMedium()
        {
            // Tablero con una Dama blanca desprotegida que puede ser capturada por un peón negro
            BoardState board = new BoardState();
            board.CurrentTurn = PieceColor.Black;

            board[BoardCoord.FromAlgebraic("e8")] = new Piece(PieceType.King, PieceColor.Black);
            board[BoardCoord.FromAlgebraic("e1")] = new Piece(PieceType.King, PieceColor.White);

            board[BoardCoord.FromAlgebraic("d5")] = new Piece(PieceType.Pawn, PieceColor.Black);
            board[BoardCoord.FromAlgebraic("e4")] = new Piece(PieceType.Queen, PieceColor.White);

            var best = ChessAI.GetBestMove(board, PieceColor.Black, AIDifficulty.Medium);
            Assert(best.HasValue, "Medium debe encontrar jugada.");
            Assert(best.Value.from == BoardCoord.FromAlgebraic("d5") && best.Value.to == BoardCoord.FromAlgebraic("e4"),
                "La IA Medium debe capturar la Dama blanca desprotegida en e4.");
        }

        public static void TestChessAIHard()
        {
            // Mate en 1 para las Negras:
            // Rey blanco en h1.
            // Dama negra en f3 apoyada por alfil en e3 para dar jaque mate en g2.
            BoardState board = new BoardState();
            board.CurrentTurn = PieceColor.Black;

            board[BoardCoord.FromAlgebraic("h1")] = new Piece(PieceType.King, PieceColor.White);
            board[BoardCoord.FromAlgebraic("g8")] = new Piece(PieceType.King, PieceColor.Black);
            board[BoardCoord.FromAlgebraic("f3")] = new Piece(PieceType.Queen, PieceColor.Black);
            board[BoardCoord.FromAlgebraic("e3")] = new Piece(PieceType.Bishop, PieceColor.Black);

            var best = ChessAI.GetBestMove(board, PieceColor.Black, AIDifficulty.Hard);
            Assert(best.HasValue, "Hard debe encontrar jugada.");
            Assert(best.Value.from == BoardCoord.FromAlgebraic("f3") && best.Value.to == BoardCoord.FromAlgebraic("g2"),
                "La IA Hard con Alfa-Beta debe encontrar el Jaque Mate inmediato en g2.");
        }
    }
}

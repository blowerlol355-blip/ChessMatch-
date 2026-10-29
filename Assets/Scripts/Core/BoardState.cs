using System;
using System.Collections.Generic;

namespace ChessBalatro
{
    [Serializable]
    public readonly struct BoardCoord : IEquatable<BoardCoord>
    {
        public readonly int File; // 0..7 => 'a'..'h'
        public readonly int Rank; // 0..7 => '1'..'8'

        public BoardCoord(int file, int rank)
        {
            File = file;
            Rank = rank;
        }

        public bool IsValid => File >= 0 && File < 8 && Rank >= 0 && Rank < 8;

        public static BoardCoord FromAlgebraic(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 2)
                return new BoardCoord(-1, -1);

            int file = char.ToLowerInvariant(s[0]) - 'a';
            int rank = s[1] - '1';
            return new BoardCoord(file, rank);
        }

        public string ToAlgebraic()
        {
            if (!IsValid) return "??";
            return $"{(char)('a' + File)}{(char)('1' + Rank)}";
        }

        public override string ToString() => ToAlgebraic();

        public bool Equals(BoardCoord other) => File == other.File && Rank == other.Rank;

        public override bool Equals(object obj) => obj is BoardCoord other && Equals(other);

        public override int GetHashCode() => (File * 397) ^ Rank;

        public static bool operator ==(BoardCoord left, BoardCoord right) => left.Equals(right);

        public static bool operator !=(BoardCoord left, BoardCoord right) => !left.Equals(right);
    }

    [Serializable]
    public class Move
    {
        public BoardCoord From { get; set; }
        public BoardCoord To { get; set; }
        public Piece PieceMoved { get; set; }
        public Piece PieceCaptured { get; set; }
        public PieceType PromotionType { get; set; } = PieceType.None;
        public bool IsCastling { get; set; }
        public bool IsEnPassant { get; set; }

        public Move(BoardCoord from, BoardCoord to, Piece pieceMoved, Piece pieceCaptured = null)
        {
            From = from;
            To = to;
            PieceMoved = pieceMoved;
            PieceCaptured = pieceCaptured;
        }

        public override string ToString()
        {
            string promo = PromotionType != PieceType.None ? $"={PromotionType}" : "";
            return $"{From}->{To}{promo}";
        }
    }

    /// <summary>
    /// Estructura ligera para revertir estados de simulación sin asignación dinámica de memoria (Zero GC).
    /// </summary>
    public readonly struct BoardMoveUndo
    {
        public readonly Move Move;
        public readonly Piece MovedPiece;
        public readonly bool MovedPieceHadMoved;
        public readonly PieceType OriginalType;
        public readonly Piece CapturedPiece;
        public readonly BoardCoord CapturedPieceCoord;
        public readonly BoardCoord? PrevEnPassantTarget;
        public readonly bool PrevWhiteCanCastleKingside;
        public readonly bool PrevWhiteCanCastleQueenside;
        public readonly bool PrevBlackCanCastleKingside;
        public readonly bool PrevBlackCanCastleQueenside;
        public readonly PieceColor PrevCurrentTurn;
        public readonly int PrevHalfmoveClock;
        public readonly int PrevFullmoveNumber;

        public BoardMoveUndo(
            Move move,
            Piece movedPiece,
            bool movedPieceHadMoved,
            PieceType originalType,
            Piece capturedPiece,
            BoardCoord capturedPieceCoord,
            BoardCoord? prevEnPassantTarget,
            bool prevWhiteCanCastleKingside,
            bool prevWhiteCanCastleQueenside,
            bool prevBlackCanCastleKingside,
            bool prevBlackCanCastleQueenside,
            PieceColor prevCurrentTurn,
            int prevHalfmoveClock,
            int prevFullmoveNumber)
        {
            Move = move;
            MovedPiece = movedPiece;
            MovedPieceHadMoved = movedPieceHadMoved;
            OriginalType = originalType;
            CapturedPiece = capturedPiece;
            CapturedPieceCoord = capturedPieceCoord;
            PrevEnPassantTarget = prevEnPassantTarget;
            PrevWhiteCanCastleKingside = prevWhiteCanCastleKingside;
            PrevWhiteCanCastleQueenside = prevWhiteCanCastleQueenside;
            PrevBlackCanCastleKingside = prevBlackCanCastleKingside;
            PrevBlackCanCastleQueenside = prevBlackCanCastleQueenside;
            PrevCurrentTurn = prevCurrentTurn;
            PrevHalfmoveClock = prevHalfmoveClock;
            PrevFullmoveNumber = prevFullmoveNumber;
        }
    }

    /// <summary>
    /// Representa el estado del tablero, reglas de ajedrez e historial de jugadas en C# puro.
    /// Totalmente testeable y sin dependencias de Unity Engine en la lógica matemática.
    /// </summary>
    public class BoardState
    {
        public const int BoardSize = 8;
        private readonly Piece[,] _grid = new Piece[BoardSize, BoardSize];

        public PieceColor CurrentTurn { get; set; } = PieceColor.White;
        public BoardCoord? EnPassantTarget { get; set; } = null;

        public bool WhiteCanCastleKingside { get; set; } = true;
        public bool WhiteCanCastleQueenside { get; set; } = true;
        public bool BlackCanCastleKingside { get; set; } = true;
        public bool BlackCanCastleQueenside { get; set; } = true;

        public int HalfmoveClock { get; set; } = 0;
        public int FullmoveNumber { get; set; } = 1;

        /// <summary>
        /// Modificadores de cartas activas en este tablero.
        /// </summary>
        public List<IMoveModifier> Modifiers { get; } = new List<IMoveModifier>();

        /// <summary>
        /// Efectos de cartas estilo Balatro (ICardEffect) activos en este tablero.
        /// </summary>
        public List<ICardEffect> ActiveCardEffects { get; } = new List<ICardEffect>();

        /// <summary>
        /// Historial de jugadas y despachador de eventos de estado.
        /// </summary>
        public MoveHistory History { get; } = new MoveHistory();

        // Eventos C# expuestos directamente para suscripción de capas de UI/Vista
        public event Action<PieceColor> OnCheck
        {
            add => History.OnCheck += value;
            remove => History.OnCheck -= value;
        }

        public event Action<PieceColor> OnCheckmate
        {
            add => History.OnCheckmate += value;
            remove => History.OnCheckmate -= value;
        }

        public event Action<PieceColor> OnStalemate
        {
            add => History.OnStalemate += value;
            remove => History.OnStalemate -= value;
        }

        public event Action<MoveRecord> OnMoveExecuted
        {
            add => History.OnMoveExecuted += value;
            remove => History.OnMoveExecuted -= value;
        }

        public Piece this[int file, int rank]
        {
            get => IsInBounds(file, rank) ? _grid[file, rank] : null;
            set
            {
                if (IsInBounds(file, rank))
                    _grid[file, rank] = value;
            }
        }

        public Piece this[BoardCoord coord]
        {
            get => this[coord.File, coord.Rank];
            set => this[coord.File, coord.Rank] = value;
        }

        public BoardState()
        {
            Clear();
        }

        public void Clear()
        {
            for (int f = 0; f < BoardSize; f++)
            {
                for (int r = 0; r < BoardSize; r++)
                {
                    _grid[f, r] = null;
                }
            }
            EnPassantTarget = null;
            WhiteCanCastleKingside = true;
            WhiteCanCastleQueenside = true;
            BlackCanCastleKingside = true;
            BlackCanCastleQueenside = true;
            HalfmoveClock = 0;
            FullmoveNumber = 1;
            CurrentTurn = PieceColor.White;
            Modifiers.Clear();
            History.Clear();
        }

        public void InitializeStandardBoard()
        {
            Clear();

            // Peones
            for (int f = 0; f < BoardSize; f++)
            {
                _grid[f, 1] = new Piece(PieceType.Pawn, PieceColor.White);
                _grid[f, 6] = new Piece(PieceType.Pawn, PieceColor.Black);
            }

            // Piezas mayores Blancas
            _grid[0, 0] = new Piece(PieceType.Rook, PieceColor.White);
            _grid[1, 0] = new Piece(PieceType.Knight, PieceColor.White);
            _grid[2, 0] = new Piece(PieceType.Bishop, PieceColor.White);
            _grid[3, 0] = new Piece(PieceType.Queen, PieceColor.White);
            _grid[4, 0] = new Piece(PieceType.King, PieceColor.White);
            _grid[5, 0] = new Piece(PieceType.Bishop, PieceColor.White);
            _grid[6, 0] = new Piece(PieceType.Knight, PieceColor.White);
            _grid[7, 0] = new Piece(PieceType.Rook, PieceColor.White);

            // Piezas mayores Negras
            _grid[0, 7] = new Piece(PieceType.Rook, PieceColor.Black);
            _grid[1, 7] = new Piece(PieceType.Knight, PieceColor.Black);
            _grid[2, 7] = new Piece(PieceType.Bishop, PieceColor.Black);
            _grid[3, 7] = new Piece(PieceType.Queen, PieceColor.Black);
            _grid[4, 7] = new Piece(PieceType.King, PieceColor.Black);
            _grid[5, 7] = new Piece(PieceType.Bishop, PieceColor.Black);
            _grid[6, 7] = new Piece(PieceType.Knight, PieceColor.Black);
            _grid[7, 7] = new Piece(PieceType.Rook, PieceColor.Black);

            CurrentTurn = PieceColor.White;
        }

        public static bool IsInBounds(int file, int rank)
        {
            return file >= 0 && file < BoardSize && rank >= 0 && rank < BoardSize;
        }

        public static bool IsInBounds(BoardCoord coord) => IsInBounds(coord.File, coord.Rank);

        /// <summary>
        /// Localiza rápidamente la casilla que ocupa el Rey del bando indicado.
        /// </summary>
        public BoardCoord? FindKing(PieceColor color)
        {
            for (int f = 0; f < BoardSize; f++)
            {
                for (int r = 0; r < BoardSize; r++)
                {
                    Piece p = _grid[f, r];
                    if (p != null && p.Type == PieceType.King && p.Color == color)
                        return new BoardCoord(f, r);
                }
            }
            return null;
        }

        /// <summary>
        /// Evalúa si alguna pieza enemiga tiene línea de visión o ataque directo a la casilla objetivo.
        /// </summary>
        public bool IsSquareAttacked(BoardCoord target, PieceColor attackerColor)
        {
            if (!IsInBounds(target)) return false;

            // 1. Ataques de peones enemigos
            int pawnDirection = attackerColor == PieceColor.White ? 1 : -1;
            int enemyPawnRank = target.Rank - pawnDirection;
            if (enemyPawnRank >= 0 && enemyPawnRank < BoardSize)
            {
                if (target.File - 1 >= 0)
                {
                    Piece p = _grid[target.File - 1, enemyPawnRank];
                    if (p != null && p.Color == attackerColor && p.Type == PieceType.Pawn) return true;
                }
                if (target.File + 1 < BoardSize)
                {
                    Piece p = _grid[target.File + 1, enemyPawnRank];
                    if (p != null && p.Color == attackerColor && p.Type == PieceType.Pawn) return true;
                }
            }

            // 2. Ataques de caballos enemigos
            int[] kf = { 1, 2, 2, 1, -1, -2, -2, -1 };
            int[] kr = { 2, 1, -1, -2, -2, -1, 1, 2 };
            for (int i = 0; i < 8; i++)
            {
                int nf = target.File + kf[i];
                int nr = target.Rank + kr[i];
                if (IsInBounds(nf, nr))
                {
                    Piece p = _grid[nf, nr];
                    if (p != null && p.Color == attackerColor && p.Type == PieceType.Knight)
                        return true;
                }
            }

            // 3. Ataques de rey enemigo (1 casilla en cualquier dirección)
            for (int df = -1; df <= 1; df++)
            {
                for (int dr = -1; dr <= 1; dr++)
                {
                    if (df == 0 && dr == 0) continue;
                    int kX = target.File + df;
                    int kY = target.Rank + dr;
                    if (IsInBounds(kX, kY))
                    {
                        Piece p = _grid[kX, kY];
                        if (p != null && p.Color == attackerColor && p.Type == PieceType.King)
                            return true;
                    }
                }
            }

            // 4. Ataques rectos (Torres y Reinas)
            int[,] straightDirs = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
            for (int i = 0; i < 4; i++)
            {
                int df = straightDirs[i, 0];
                int dr = straightDirs[i, 1];
                int f = target.File + df;
                int r = target.Rank + dr;
                while (IsInBounds(f, r))
                {
                    Piece p = _grid[f, r];
                    if (p != null)
                    {
                        if (p.Color == attackerColor && (p.Type == PieceType.Rook || p.Type == PieceType.Queen))
                            return true;
                        break;
                    }
                    f += df;
                    r += dr;
                }
            }

            // 5. Ataques diagonales (Alfiles y Reinas)
            int[,] diagDirs = { { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 } };
            for (int i = 0; i < 4; i++)
            {
                int df = diagDirs[i, 0];
                int dr = diagDirs[i, 1];
                int f = target.File + df;
                int r = target.Rank + dr;
                while (IsInBounds(f, r))
                {
                    Piece p = _grid[f, r];
                    if (p != null)
                    {
                        if (p.Color == attackerColor && (p.Type == PieceType.Bishop || p.Type == PieceType.Queen))
                            return true;
                        break;
                    }
                    f += df;
                    r += dr;
                }
            }

            return false;
        }

        /// <summary>
        /// Comprueba si el rey del color especificado se encuentra actualmente en Jaque.
        /// </summary>
        public bool IsInCheck(PieceColor kingColor)
        {
            BoardCoord? kingCoord = FindKing(kingColor);
            if (!kingCoord.HasValue) return false;

            PieceColor enemyColor = kingColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
            return IsSquareAttacked(kingCoord.Value, enemyColor);
        }

        /// <summary>
        /// Simula temporalmente un movimiento sobre este tablero de forma no destructiva.
        /// Permite chequear jaques sin tener que instanciar copias completas en el heap.
        /// Debe emparejarse siempre con <see cref="UndoMove"/>.
        /// </summary>
        public BoardMoveUndo SimulateMove(Move move)
        {
            Piece piece = _grid[move.From.File, move.From.Rank];
            Piece captured = null;
            BoardCoord capturedCoord = move.To;

            if (move.IsEnPassant)
            {
                capturedCoord = new BoardCoord(move.To.File, move.From.Rank);
                captured = _grid[capturedCoord.File, capturedCoord.Rank];
            }
            else
            {
                captured = _grid[move.To.File, move.To.Rank];
            }

            BoardMoveUndo undo = new BoardMoveUndo(
                move,
                piece,
                piece != null && piece.HasMoved,
                piece != null ? piece.Type : PieceType.None,
                captured,
                capturedCoord,
                EnPassantTarget,
                WhiteCanCastleKingside,
                WhiteCanCastleQueenside,
                BlackCanCastleKingside,
                BlackCanCastleQueenside,
                CurrentTurn,
                HalfmoveClock,
                FullmoveNumber
            );

            ApplyMoveInternal(move);
            return undo;
        }

        /// <summary>
        /// Revierte un movimiento simulado previamente mediante <see cref="SimulateMove"/>.
        /// </summary>
        public void UndoMove(BoardMoveUndo undo)
        {
            Move move = undo.Move;
            Piece piece = undo.MovedPiece;

            if (piece == null) return;

            // Restaurar pieza movida a su posición original
            piece.HasMoved = undo.MovedPieceHadMoved;
            piece.Type = undo.OriginalType;
            _grid[move.From.File, move.From.Rank] = piece;

            // Limpiar casilla de destino
            _grid[move.To.File, move.To.Rank] = null;

            // Restaurar pieza capturada
            if (undo.CapturedPiece != null)
            {
                _grid[undo.CapturedPieceCoord.File, undo.CapturedPieceCoord.Rank] = undo.CapturedPiece;
            }

            // Restaurar torre en caso de enroque
            if (move.IsCastling)
            {
                int rank = move.From.Rank;
                if (move.To.File == 6) // Corto
                {
                    Piece rook = _grid[5, rank];
                    _grid[5, rank] = null;
                    if (rook != null)
                    {
                        rook.HasMoved = false;
                        _grid[7, rank] = rook;
                    }
                }
                else if (move.To.File == 2) // Largo
                {
                    Piece rook = _grid[3, rank];
                    _grid[3, rank] = null;
                    if (rook != null)
                    {
                        rook.HasMoved = false;
                        _grid[0, rank] = rook;
                    }
                }
            }

            // Restaurar banderas de enroque, en-passant y contadores
            EnPassantTarget = undo.PrevEnPassantTarget;
            WhiteCanCastleKingside = undo.PrevWhiteCanCastleKingside;
            WhiteCanCastleQueenside = undo.PrevWhiteCanCastleQueenside;
            BlackCanCastleKingside = undo.PrevBlackCanCastleKingside;
            BlackCanCastleQueenside = undo.PrevBlackCanCastleQueenside;
            CurrentTurn = undo.PrevCurrentTurn;
            HalfmoveClock = undo.PrevHalfmoveClock;
            FullmoveNumber = undo.PrevFullmoveNumber;
        }

        /// <summary>
        /// Crea una copia profunda independiente del tablero y sus piezas.
        /// </summary>
        public BoardState Clone()
        {
            BoardState copy = new BoardState
            {
                CurrentTurn = this.CurrentTurn,
                EnPassantTarget = this.EnPassantTarget,
                WhiteCanCastleKingside = this.WhiteCanCastleKingside,
                WhiteCanCastleQueenside = this.WhiteCanCastleQueenside,
                BlackCanCastleKingside = this.BlackCanCastleKingside,
                BlackCanCastleQueenside = this.BlackCanCastleQueenside,
                HalfmoveClock = this.HalfmoveClock,
                FullmoveNumber = this.FullmoveNumber
            };

            for (int f = 0; f < BoardSize; f++)
            {
                for (int r = 0; r < BoardSize; r++)
                {
                    if (_grid[f, r] != null)
                    {
                        copy._grid[f, r] = _grid[f, r].Clone();
                    }
                }
            }

            if (Modifiers.Count > 0)
            {
                copy.Modifiers.AddRange(this.Modifiers);
            }

            if (ActiveCardEffects.Count > 0)
            {
                copy.ActiveCardEffects.AddRange(this.ActiveCardEffects);
            }

            return copy;
        }

        // =========================================================================
        // Métodos de Conveniencia y Compatibilidad con MoveValidator
        // =========================================================================

        public List<Move> GetLegalMoves(BoardCoord from) => MoveValidator.GetLegalMoves(this, from);

        public List<Move> GetPseudoLegalMoves(BoardCoord from) => MoveValidator.GetPseudoLegalMoves(this, from);

        public List<Move> GetAllLegalMoves(PieceColor color) => MoveValidator.GetAllLegalMoves(this, color);

        public bool HasLegalMoves(PieceColor color) => MoveValidator.HasLegalMoves(this, color);

        public bool IsCheckmate(PieceColor color) => MoveValidator.IsCheckmate(this, color);

        public bool IsStalemate(PieceColor color) => MoveValidator.IsStalemate(this, color);

        /// <summary>
        /// Valida y ejecuta un movimiento de juego, actualizando el turno, evaluando jaque/mate,
        /// registrándolo en el historial de notación algebraica y disparando eventos a la UI.
        /// </summary>
        public bool MakeMove(Move move)
        {
            List<Move> legalMoves = GetLegalMoves(move.From);
            Move validMove = legalMoves.Find(m =>
                m.To == move.To &&
                (move.PromotionType == PieceType.None || m.PromotionType == move.PromotionType));

            if (validMove == null) return false;

            Piece movingPiece = this[validMove.From];
            Piece capturedPiece = validMove.IsEnPassant
                ? this[validMove.To.File, validMove.From.Rank]
                : this[validMove.To];

            // Aplicar el movimiento en la cuadrícula
            ApplyMoveInternal(validMove);

            // Alternar turno
            CurrentTurn = CurrentTurn == PieceColor.White ? PieceColor.Black : PieceColor.White;
            if (CurrentTurn == PieceColor.White)
            {
                FullmoveNumber++;
            }

            // Evaluar estado para el bando que acaba de recibir el turno
            bool isCheck = IsInCheck(CurrentTurn);
            bool hasMoves = MoveValidator.HasLegalMoves(this, CurrentTurn);
            bool isCheckmate = isCheck && !hasMoves;
            bool isStalemate = !isCheck && !hasMoves;

            // Crear y persistir el registro en notación algebraica con eventos
            MoveRecord record = new MoveRecord(
                movingPiece,
                validMove.From,
                validMove.To,
                capturedPiece,
                isCheck,
                isCheckmate,
                validMove.PromotionType,
                validMove.IsCastling,
                validMove.IsEnPassant
            );

            History.AddRecord(record);

            if (isStalemate)
            {
                History.NotifyStalemate(CurrentTurn);
            }

            return true;
        }

        /// <summary>
        /// Aplica las mutaciones físicas del tablero para un movimiento sin chequeos de legalidad.
        /// </summary>
        public void ApplyMoveInternal(Move move)
        {
            Piece piece = _grid[move.From.File, move.From.Rank];
            if (piece == null) return;

            // Resetear en-passant anterior por defecto
            EnPassantTarget = null;

            // En passant captura
            if (move.IsEnPassant)
            {
                _grid[move.To.File, move.From.Rank] = null;
            }

            // Enroque: mover también la torre correspondiente
            if (move.IsCastling)
            {
                int rank = move.From.Rank;
                if (move.To.File == 6) // Corto
                {
                    Piece rook = _grid[7, rank];
                    _grid[7, rank] = null;
                    if (rook != null)
                    {
                        rook.HasMoved = true;
                        _grid[5, rank] = rook;
                    }
                }
                else if (move.To.File == 2) // Largo
                {
                    Piece rook = _grid[0, rank];
                    _grid[0, rank] = null;
                    if (rook != null)
                    {
                        rook.HasMoved = true;
                        _grid[3, rank] = rook;
                    }
                }
            }

            // Doble paso de peón activa casilla en passant
            if (piece.Type == PieceType.Pawn && Math.Abs(move.To.Rank - move.From.Rank) == 2)
            {
                int epRank = (move.From.Rank + move.To.Rank) / 2;
                EnPassantTarget = new BoardCoord(move.From.File, epRank);
            }

            // Actualizar derechos de enroque al mover rey o torres
            if (piece.Type == PieceType.King)
            {
                if (piece.Color == PieceColor.White)
                {
                    WhiteCanCastleKingside = false;
                    WhiteCanCastleQueenside = false;
                }
                else
                {
                    BlackCanCastleKingside = false;
                    BlackCanCastleQueenside = false;
                }
            }
            else if (piece.Type == PieceType.Rook)
            {
                if (move.From.File == 0 && move.From.Rank == 0) WhiteCanCastleQueenside = false;
                if (move.From.File == 7 && move.From.Rank == 0) WhiteCanCastleKingside = false;
                if (move.From.File == 0 && move.From.Rank == 7) BlackCanCastleQueenside = false;
                if (move.From.File == 7 && move.From.Rank == 7) BlackCanCastleKingside = false;
            }

            // Si una torre en su casilla de origen es capturada, anular el enroque correspondiente
            if (move.To.Rank == 0 && move.To.File == 0) WhiteCanCastleQueenside = false;
            else if (move.To.Rank == 0 && move.To.File == 7) WhiteCanCastleKingside = false;
            else if (move.To.Rank == 7 && move.To.File == 0) BlackCanCastleQueenside = false;
            else if (move.To.Rank == 7 && move.To.File == 7) BlackCanCastleKingside = false;

            // Mover la pieza a la casilla destino
            _grid[move.From.File, move.From.Rank] = null;
            piece.HasMoved = true;

            // Coronación / Promoción
            if (move.PromotionType != PieceType.None)
            {
                piece = new Piece(move.PromotionType, piece.Color, true)
                {
                    BonusChips = piece.BonusChips,
                    MultMultiplier = piece.MultMultiplier
                };
            }

            _grid[move.To.File, move.To.Rank] = piece;
        }
    }
}

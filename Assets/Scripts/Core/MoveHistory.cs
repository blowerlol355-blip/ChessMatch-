using System;
using System.Collections.Generic;
using System.Text;

namespace ChessBalatro
{
    /// <summary>
    /// Registro inmutable de una jugada ejecutada en el tablero.
    /// Contiene metadatos para notación algebraica, capturas y estado de jaque/mate.
    /// </summary>
    [Serializable]
    public struct MoveRecord
    {
        // Campos requeridos por especificación
        public Piece pieceMoved;
        public BoardCoord from;
        public BoardCoord to;
        public Piece pieceCaptured;
        public bool isCheck;
        public bool isCheckmate;

        // Metadatos adicionales para enriquecer la notación y el registro
        public PieceType promotionType;
        public bool isCastling;
        public bool isEnPassant;
        public string algebraicNotation;

        // Propiedades PascalCase para compatibilidad con estándares de C#
        public Piece PieceMoved => pieceMoved;
        public BoardCoord From => from;
        public BoardCoord To => to;
        public Piece PieceCaptured => pieceCaptured;
        public bool IsCheck => isCheck;
        public bool IsCheckmate => isCheckmate;
        public PieceType PromotionType => promotionType;
        public bool IsCastling => isCastling;
        public bool IsEnPassant => isEnPassant;
        public string AlgebraicNotation => algebraicNotation;

        public MoveRecord(
            Piece pieceMoved,
            BoardCoord from,
            BoardCoord to,
            Piece pieceCaptured = null,
            bool isCheck = false,
            bool isCheckmate = false,
            PieceType promotionType = PieceType.None,
            bool isCastling = false,
            bool isEnPassant = false)
        {
            this.pieceMoved = pieceMoved;
            this.from = from;
            this.to = to;
            this.pieceCaptured = pieceCaptured;
            this.isCheck = isCheck;
            this.isCheckmate = isCheckmate;
            this.promotionType = promotionType;
            this.isCastling = isCastling;
            this.isEnPassant = isEnPassant;
            this.algebraicNotation = string.Empty;

            // Calcula automáticamente la notación algebraica
            this.algebraicNotation = MoveHistory.ToAlgebraicNotation(this);
        }

        public override string ToString() => !string.IsNullOrEmpty(algebraicNotation)
            ? algebraicNotation
            : MoveHistory.ToAlgebraicNotation(this);
    }

    /// <summary>
    /// Gestiona el historial cronológico de jugadas en notación algebraica
    /// y notifica a las capas superiores (View/UI) mediante eventos desacoplados.
    /// </summary>
    public class MoveHistory
    {
        private readonly List<MoveRecord> _records = new List<MoveRecord>();

        public IReadOnlyList<MoveRecord> Records => _records.AsReadOnly();
        public int Count => _records.Count;
        public MoveRecord? LastMove => _records.Count > 0 ? _records[_records.Count - 1] : null;

        // Eventos C# para la capa View/UI (desacoplados de Unity MonoBehaviour)
        public event Action<PieceColor> OnCheck;
        public event Action<PieceColor> OnCheckmate;
        public event Action<PieceColor> OnStalemate;
        public event Action<MoveRecord> OnMoveExecuted;

        /// <summary>
        /// Agrega una jugada al historial y dispara los eventos pertinentes.
        /// </summary>
        public MoveRecord RecordMove(
            Piece pieceMoved,
            BoardCoord from,
            BoardCoord to,
            Piece pieceCaptured = null,
            bool isCheck = false,
            bool isCheckmate = false,
            PieceType promotionType = PieceType.None,
            bool isCastling = false,
            bool isEnPassant = false)
        {
            MoveRecord record = new MoveRecord(
                pieceMoved,
                from,
                to,
                pieceCaptured,
                isCheck,
                isCheckmate,
                promotionType,
                isCastling,
                isEnPassant
            );

            return AddRecord(record);
        }

        /// <summary>
        /// Inserta un MoveRecord y notifica a los suscriptores.
        /// </summary>
        public MoveRecord AddRecord(MoveRecord record)
        {
            _records.Add(record);

            OnMoveExecuted?.Invoke(record);

            if (record.isCheckmate)
            {
                PieceColor defendingColor = record.pieceMoved != null && record.pieceMoved.Color == PieceColor.White
                    ? PieceColor.Black
                    : PieceColor.White;

                OnCheckmate?.Invoke(defendingColor);
            }
            else if (record.isCheck)
            {
                PieceColor defendingColor = record.pieceMoved != null && record.pieceMoved.Color == PieceColor.White
                    ? PieceColor.Black
                    : PieceColor.White;

                OnCheck?.Invoke(defendingColor);
            }

            return record;
        }

        /// <summary>
        /// Notifica la ocurrencia de Rey Ahogado (tablas por falta de movimientos legales).
        /// </summary>
        public void NotifyStalemate(PieceColor stalematedColor)
        {
            OnStalemate?.Invoke(stalematedColor);
        }

        /// <summary>
        /// Limpia el historial de jugadas.
        /// </summary>
        public void Clear()
        {
            _records.Clear();
        }

        /// <summary>
        /// Convierte un registro de movimiento en notación algebraica estándar (SAN).
        /// Ejemplos: "e4", "Nf3", "Qxd5+", "Bxf7#", "O-O", "exd8=Q#".
        /// </summary>
        public static string ToAlgebraicNotation(MoveRecord move)
        {
            if (move.pieceMoved == null)
            {
                return $"{move.from.ToAlgebraic()}-{move.to.ToAlgebraic()}";
            }

            // Enroques
            if (move.isCastling)
            {
                string castlingStr = move.to.File == 6 ? "O-O" : "O-O-O";
                if (move.isCheckmate) return castlingStr + "#";
                if (move.isCheck) return castlingStr + "+";
                return castlingStr;
            }

            StringBuilder sb = new StringBuilder();
            PieceType type = move.pieceMoved.Type;

            if (type == PieceType.Pawn)
            {
                // Peón: captura indica columna de origen
                if (move.pieceCaptured != null || move.isEnPassant)
                {
                    sb.Append((char)('a' + move.from.File));
                    sb.Append('x');
                }

                sb.Append(move.to.ToAlgebraic());

                // Coronación / Promoción
                if (move.promotionType != PieceType.None)
                {
                    sb.Append('=');
                    sb.Append(GetPieceNotationSymbol(move.promotionType));
                }
            }
            else
            {
                // Pieza mayor o menor
                sb.Append(GetPieceNotationSymbol(type));

                if (move.pieceCaptured != null)
                {
                    sb.Append('x');
                }

                sb.Append(move.to.ToAlgebraic());
            }

            // Sufijo de Jaque Mate (#) o Jaque (+)
            if (move.isCheckmate)
            {
                sb.Append('#');
            }
            else if (move.isCheck)
            {
                sb.Append('+');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Obtiene la letra estándar internacional para la pieza en notación algebraica.
        /// </summary>
        public static char GetPieceNotationSymbol(PieceType type) => type switch
        {
            PieceType.King => 'K',
            PieceType.Queen => 'Q',
            PieceType.Rook => 'R',
            PieceType.Bishop => 'B',
            PieceType.Knight => 'N',
            _ => '?'
        };
    }
}

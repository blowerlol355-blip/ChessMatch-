using System.Collections.Generic;

namespace ChessBalatro
{
    /// <summary>
    /// Interfaz que define el contrato para cualquier efecto de carta (estilo Balatro).
    /// Permite alterar dinámicamente las reglas del tablero, inyectar casillas adicionales
    /// a los movimientos de las piezas y limpiar su impacto al terminar el turno.
    /// </summary>
    public interface ICardEffect
    {
        /// <summary>
        /// Determina si la carta puede activarse dado el estado del tablero y el turno actual.
        /// </summary>
        bool CanApply(BoardState board, PieceColor turnColor);

        /// <summary>
        /// Aplica el efecto de la carta sobre el tablero (o una casilla objetivo opcional).
        /// </summary>
        void ApplyEffect(BoardState board, BoardCoord? targetCoord = null);

        /// <summary>
        /// Permite modificar, agregar o filtrar casillas de destino legales para una pieza concreta.
        /// </summary>
        void ModifyMoves(Piece piece, BoardCoord coord, BoardState board, List<BoardCoord> legalMoves);

        /// <summary>
        /// Limpia y restablece el estado del efecto tras finalizar el turno o consumirse la carta.
        /// </summary>
        void ResetEffect();
    }
}

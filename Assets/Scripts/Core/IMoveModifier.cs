using System.Collections.Generic;

namespace ChessBalatro
{
    /// <summary>
    /// Interfaz para permitir que cartas modificadoras o reliquias (estilo Balatro)
    /// alteren las reglas de movimiento sin romper la validación ni el motor de ajedrez.
    /// </summary>
    public interface IMoveModifier
    {
        /// <summary>
        /// Permite agregar, remover o transformar movimientos pseudo-legales para una pieza determinada.
        /// </summary>
        void ModifyPseudoLegalMoves(BoardState board, BoardCoord origin, Piece piece, List<Move> moves);
    }
}

using System.Collections.Generic;

namespace ChessBalatro
{
    /// <summary>
    /// Interfaz para cualquier elemento pasivo (Joker, Reliquia o Carta Activa)
    /// que altere la puntuación (Fichas x Multiplicador) de una jugada de ajedrez.
    /// </summary>
    public interface IScoreModifier
    {
        /// <summary>
        /// Aplica bonificaciones de Fichas (Chips) o Multiplicador (Mult) a la jugada realizada.
        /// </summary>
        void ModifyScore(Move move, BoardState board, ref int chips, ref float mult, List<string> triggerLogs);
    }
}

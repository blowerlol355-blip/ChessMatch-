using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Efecto de la carta legendaria "Tigrán Petrosián - El Tigre de Hierro".
    /// Anula el jaque al Rey blanco durante el turno actual desplegando el Escudo de Petrosián.
    /// Mientras el escudo está activo:
    ///   - El Rey blanco no puede estar en jaque (se ignoran capturas ilegales sobre él).
    ///   - Se dispara el evento estático <see cref="OnShieldActivated"/> para sincronizar efectos visuales.
    ///   - Al terminar el turno o resolverse, se dispara <see cref="OnShieldDeactivated"/>.
    /// </summary>
    [System.Serializable]
    public class PetrosianDefenseEffect : ICardEffect
    {
        // ── Eventos estáticos para desacoplar la lógica del efecto de los MonoBehaviours visuales ──
        public static event System.Action<BoardCoord> OnShieldActivated;
        public static event System.Action             OnShieldDeactivated;

        /// <summary>
        /// Indica si el Escudo de Petrosián está actualmente activo en el turno.
        /// Utilizado por MoveValidator y BoardState para neutralizar la condición de jaque.
        /// </summary>
        public static bool IsShieldActive { get; private set; } = false;
        public static PieceColor ProtectedColor { get; private set; } = PieceColor.White;

        public static void ForceReset()
        {
            IsShieldActive = false;
            ProtectedColor = PieceColor.White;
        }

        private bool _isActive = false;

        // ── ICardEffect ────────────────────────────────────────────────────────────────────────────

        public bool CanApply(BoardState board, PieceColor turnColor)
        {
            if (board == null) return false;
            // El jugador puede activar el Escudo de Hierro en su turno para anular jaque o como blindaje preventivo
            return board.FindKing(turnColor).HasValue;
        }

        public void ApplyEffect(BoardState board, BoardCoord? targetCoord = null)
        {
            if (board == null) return;

            _isActive = true;
            ProtectedColor = board.CurrentTurn;
            IsShieldActive = true;

            // Localizar la posición del Rey activo para anclar el escudo visual
            BoardCoord kingCoord = FindKingCoord(board, ProtectedColor);

            SafeLog("<color=#FFD700><b>[PetrosianDefenseEffect] ¡Escudo de Petrosián activado! " +
                    $"Rey en {kingCoord}</b></color>");

            // Notificar a los sistemas visuales y al HUD
            OnShieldActivated?.Invoke(kingCoord);
        }

        public void ModifyMoves(Piece piece, BoardCoord coord, BoardState board, List<BoardCoord> legalMoves)
        {
            // El escudo no amplía movimientos; simplemente protege contra el jaque.
            // La lógica de anulación del jaque se maneja en CanApply / ApplyEffect y en el
            // ciclo de turno del BoardInputHandler / GameManager mediante _isActive.
        }

        public void ResetEffect()
        {
            if (_isActive)
            {
                _isActive = false;
                IsShieldActive = false;
                ProtectedColor = PieceColor.White;
                SafeLog("<color=#FFD700>[PetrosianDefenseEffect] Escudo disuelto al finalizar el turno.</color>");
                OnShieldDeactivated?.Invoke();
            }
        }

        private static void SafeLog(string message)
        {
            try
            {
                Debug.Log(message);
            }
            catch (System.Exception)
            {
                // Ignorar en entornos de test .NET sin runtime nativo de Unity
            }
        }

        // ── Accessors ─────────────────────────────────────────────────────────────────────────────

        /// <summary>True mientras el escudo está vigente en el turno actual.</summary>
        public bool IsActive => _isActive;

        // ── Helpers ───────────────────────────────────────────────────────────────────────────────

        private static BoardCoord FindKingCoord(BoardState board, PieceColor color)
        {
            // BoardState.FindKing returns nullable; fall back to invalid coord if not found
            BoardCoord? found = board.FindKing(color);
            return found.HasValue ? found.Value : new BoardCoord(-1, -1);
        }

        // ── Factory ───────────────────────────────────────────────────────────────────────────────

        public static PetrosianDefenseEffect CreateInstance() => new PetrosianDefenseEffect();
    }
}

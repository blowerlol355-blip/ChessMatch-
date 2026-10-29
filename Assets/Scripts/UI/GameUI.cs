using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Controlador base de la interfaz de usuario para ChessBalatro (Puntuación, Fichas, Multiplicador y Turno).
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("Referencias de UI")]
        [SerializeField] private GameObject scorePanel;
        [SerializeField] private GameObject cardsHandPanel;

        public void UpdateScoreDisplay(int chips, float mult, int totalScore)
        {
            // Conexión futura con TextMeshPro o componentes UI
        }

        public void UpdateTurnDisplay(PieceColor currentTurn)
        {
            // Indicador de turno
        }
    }
}

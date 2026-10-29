using System;
using System.Collections.Generic;

namespace ChessBalatro
{
    /// <summary>
    /// Desglose detallado de la puntuación estilo Balatro generada por una jugada (Fichas x Multiplicador).
    /// </summary>
    [Serializable]
    public class ScoreBreakdown
    {
        public int BaseChips { get; set; }
        public int BonusChips { get; set; }
        public float BaseMult { get; set; } = 1.0f;
        public float BonusMult { get; set; } = 0.0f;

        public int FinalChips => Math.Max(0, BaseChips + BonusChips);
        public float FinalMult => Math.Max(1.0f, BaseMult + BonusMult);
        public int TotalScore => (int)Math.Round(FinalChips * FinalMult);

        /// <summary>
        /// Registro de eventos o sinergias que aportaron puntuación (para feedback visual en el HUD).
        /// </summary>
        public List<string> ScoreLogs { get; } = new List<string>();

        public void AddLog(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                ScoreLogs.Add(message);
            }
        }

        public override string ToString()
        {
            return $"{FinalChips} Fichas x {FinalMult:F1} Mult = {TotalScore} pts";
        }
    }
}

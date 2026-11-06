using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Niveles de dificultad progresiva del Modo Educativo.
    /// </summary>
    public enum LessonDifficulty
    {
        Basico,     // Lecciones 1, 2 y 3 (Verde)
        Intermedio, // Lecciones 4 y 5 (Azul/Dorado)
        Dificil     // Lección 6 (Rojo/Violeta)
    }

    /// <summary>
    /// Tipos de condiciones de victoria para superar una lección educativa.
    /// </summary>
    public enum WinConditionType
    {
        CaptureAllTargets, // Capturar todas las piezas objetivo del oponente
        DeliverCheck,      // Dar Jaque legal al Rey contrario o anular jaque propio
        DeliverCheckmate,  // Ejecutar Jaque Mate decisivo
        UseCardAndWin      // Activar una carta especial requerida y cumplir el objetivo
    }

    /// <summary>
    /// Representa la posición inicial de una pieza en el tablero para una lección educativa.
    /// </summary>
    [Serializable]
    public struct InitialPiecePlacement
    {
        public PieceType type;
        public PieceColor color;
        public BoardCoord coord;

        public InitialPiecePlacement(PieceType type, PieceColor color, BoardCoord coord)
        {
            this.type = type;
            this.color = color;
            this.coord = coord;
        }

        public InitialPiecePlacement(PieceType type, PieceColor color, int file, int rank)
        {
            this.type = type;
            this.color = color;
            this.coord = new BoardCoord(file, rank);
        }
    }

    /// <summary>
    /// Definición de datos de una lección del Modo Educativo.
    /// </summary>
    [Serializable]
    public class TutorialLessonData
    {
        [Header("Identificación y Clasificación")]
        public int lessonId;
        public string title;
        public string subtitle;
        public LessonDifficulty difficulty;

        [Header("Instrucciones y Consejos")]
        [TextArea(3, 6)] public string instructionText;
        [TextArea(2, 4)] public string tacticalTip;

        [Header("Configuración Inicial del Tablero")]
        public List<InitialPiecePlacement> initialPieces = new List<InitialPiecePlacement>();

        [Header("Condición de Victoria")]
        public WinConditionType winCondition;
        public int maxMoves = 1;

        [Header("Integración de Cartas")]
        public bool requiresCard;
        public string requiredCardName;
        [NonSerialized] public Func<CardData> cardFactory;

        [Header("Guía Visual (Indicadores)")]
        [NonSerialized] public BoardCoord? suggestedPieceCoord;
        [NonSerialized] public BoardCoord? targetCoord;

        [Header("Filtros y Validadores Personalizados")]
        [NonSerialized] public Func<Move, bool> moveRuleFilter;
        [NonSerialized] public Func<BoardState, bool> customWinValidator;

        /// <summary>
        /// Aplica la configuración de piezas iniciales en el BoardState proporcionado.
        /// </summary>
        public void ApplyToBoard(BoardState board)
        {
            if (board == null) return;
            board.Clear();

            foreach (var placement in initialPieces)
            {
                board[placement.coord.File, placement.coord.Rank] = new Piece(placement.type, placement.color);
            }

            board.CurrentTurn = PieceColor.White;
        }
    }
}

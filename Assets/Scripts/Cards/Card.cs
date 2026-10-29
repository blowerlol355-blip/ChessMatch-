using System;
using UnityEngine;

namespace ChessBalatro
{
    public enum CardRarity
    {
        Common,
        Uncommon,
        Rare,
        Legendary
    }

    /// <summary>
    /// Clasifica la jerarquía de mejora de una carta.
    /// <c>Major_Master</c> = selección de Gran Maestro al comienzo de partida.
    /// <c>Minor_Perk</c>   = mejora menor disponible entre rondas.
    /// </summary>
    public enum UpgradeType
    {
        Minor_Perk   = 0,
        Major_Master = 1
    }

    public enum CardType
    {
        PassiveModifier, // Modificador pasivo permanente (tipo Joker)
        Consumable,      // Consumible de un solo uso (tipo Tarot/Planeta)
        PieceEnhancement // Aplica efectos directos a una pieza seleccionada
    }

    /// <summary>
    /// Modelo base para las cartas y modificadores del juego (estilo Balatro).
    /// </summary>
    [Serializable]
    public abstract class Card
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int Cost { get; set; } = 4;
        public CardRarity Rarity { get; set; } = CardRarity.Common;
        public CardType Type { get; set; } = CardType.PassiveModifier;

        protected Card(string id, string title, string description, int cost, CardRarity rarity, CardType type)
        {
            Id = id;
            Title = title;
            Description = description;
            Cost = cost;
            Rarity = rarity;
            Type = type;
        }

        /// <summary>
        /// Se dispara al evaluar puntuación o jugadas en el tablero.
        /// </summary>
        public virtual void OnMoveScored(Move move, ref int chips, ref float mult)
        {
        }

        /// <summary>
        /// Se dispara al ejecutar un movimiento en el tablero.
        /// </summary>
        public virtual void OnMoveExecuted(BoardState board, Move move)
        {
        }
    }
}

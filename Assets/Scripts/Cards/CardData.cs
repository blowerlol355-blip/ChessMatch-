using System;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Definición de datos para una carta estilo Balatro como ScriptableObject.
    /// Define nombre, descripción, coste, ilustración, rareza y la referencia polimórfica a su efecto.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCardData", menuName = "ChessBalatro/Card Data", order = 10)]
    public class CardData : ScriptableObject
    {
        [Header("Identificación y Apariencia")]
        [SerializeField] private string cardName = "Nueva Carta";
        [SerializeField] private string subtitle = string.Empty;
        [SerializeField, TextArea(2, 4)] private string description = "Efecto de la carta.";
        [SerializeField] private int cost = 3;
        [SerializeField] private Sprite cardSprite;
        [SerializeField] private CardRarity rarity = CardRarity.Common;
        [SerializeField] private Color auraColor = new Color(0.831f, 0.686f, 0.216f, 1f);
        [SerializeField] private bool hasCustomAuraColor = false;

        [Header("Clasificación de Mejora")]
        [Tooltip("Major_Master = selección de Gran Maestro al inicio de partida. Minor_Perk = mejora menor entre rondas.")]
        [SerializeField] private UpgradeType upgradeType = UpgradeType.Minor_Perk;

        [Header("Efecto Polimórfico")]
        [SerializeReference] private ICardEffect effect;

        public string CardName
        {
            get => cardName;
            set => cardName = value;
        }

        public string Subtitle
        {
            get => subtitle;
            set => subtitle = value;
        }

        public string Description
        {
            get => description;
            set => description = value;
        }

        public int Cost
        {
            get => cost;
            set => cost = value;
        }

        public Sprite CardSprite
        {
            get => cardSprite;
            set => cardSprite = value;
        }

        public CardRarity Rarity
        {
            get => rarity;
            set => rarity = value;
        }

        public Color AuraColor
        {
            get => auraColor;
            set
            {
                auraColor = value;
                hasCustomAuraColor = true;
            }
        }

        public bool HasCustomAuraColor => hasCustomAuraColor;

        public UpgradeType UpgradeType
        {
            get => upgradeType;
            set => upgradeType = value;
        }

        public void SetMasterVisuals(string abilitySubtitle, Color customAura)
        {
            subtitle = abilitySubtitle ?? string.Empty;
            auraColor = customAura;
            hasCustomAuraColor = true;
        }

        /// <summary>
        /// Efecto polimórfico asociado a la carta.
        /// </summary>
        public virtual ICardEffect Effect
        {
            get => effect;
            set => effect = value;
        }

        /// <summary>
        /// Obtiene o inicializa la instancia del efecto asignado.
        /// </summary>
        public virtual ICardEffect GetOrCreateEffect()
        {
            return effect;
        }

        /// <summary>
        /// Permite inicializar o configurar los datos de la carta por código (para tests o factorías procedurales).
        /// </summary>
        public void Initialize(string name, string desc, int cardCost, CardRarity cardRarity, ICardEffect cardEffect = null, Sprite sprite = null, UpgradeType upgrade = UpgradeType.Minor_Perk)
        {
            cardName    = name;
            description = desc;
            cost        = cardCost;
            rarity      = cardRarity;
            effect      = cardEffect;
            cardSprite  = sprite;
            upgradeType = upgrade;
        }
    }
}

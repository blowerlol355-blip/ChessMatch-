using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// ScriptableObject especializado para la carta "Peón Acelerado".
    /// </summary>
    [CreateAssetMenu(fileName = "Card_TurboPawn", menuName = "ChessBalatro/Cards/Turbo Pawn", order = 20)]
    public class TurboPawnCardData : CardData
    {
        private void Reset()
        {
            CardName = "Peón Acelerado";
            Description = "Permite a cualquier peón avanzar 2 casillas hacia adelante en cualquier momento (no solo en su fila inicial).";
            Cost = 3;
            Rarity = CardRarity.Common;
            Effect = new TurboPawnEffect();
        }

        public override ICardEffect GetOrCreateEffect()
        {
            if (Effect == null)
            {
                Effect = new TurboPawnEffect();
            }
            return Effect;
        }

        public static TurboPawnCardData CreateInstance()
        {
            var data = CreateInstance<TurboPawnCardData>();
            data.CardName = "Peón Acelerado";
            data.Description = "Permite a cualquier peón avanzar 2 casillas hacia adelante en cualquier momento durante este turno.";
            data.Cost = 3;
            data.Rarity = CardRarity.Common;
            data.Effect = new TurboPawnEffect();
            return data;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChessBalatro
{
    /// <summary>
    /// Gestiona el contenedor visual de la mano de cartas en el Canvas uGUI.
    /// Organiza las cartas horizontalmente centradas en la parte inferior de la pantalla
    /// con separación equilibrada y ligera elevación interactiva.
    /// </summary>
    public class CardHandView : MonoBehaviour
    {
        [Header("Configuración de Layout")]
        [SerializeField] private float cardSpacing = 160f;
        [SerializeField] private float bottomOffset = 20f;
        [SerializeField] private GameObject cardPrefab;

        private readonly List<CardView> _cardsInHand = new List<CardView>();
        public IReadOnlyList<CardView> CardsInHand => _cardsInHand;

        private RectTransform _rectTransform;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null) _rectTransform = gameObject.AddComponent<RectTransform>();

            ConfigureHandContainerRect();
        }

        /// <summary>
        /// Configura el contenedor para anclarse en la parte inferior central de la pantalla.
        /// </summary>
        public void ConfigureHandContainerRect()
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();

            _rectTransform.anchorMin = new Vector2(0.5f, 0f);
            _rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _rectTransform.pivot = new Vector2(0.5f, 0f);
            _rectTransform.anchoredPosition = new Vector2(0f, bottomOffset);
        }

        /// <summary>
        /// Crea e incorpora una nueva carta física a la mano visual.
        /// </summary>
        public CardView AddCardToHand(CardData data, CardManager manager)
        {
            if (data == null) return null;

            GameObject cardObj;
            if (cardPrefab != null)
            {
                cardObj = Instantiate(cardPrefab, transform);
            }
            else
            {
                cardObj = new GameObject($"Card_{data.CardName}");
                cardObj.transform.SetParent(transform, false);
            }

            CardView cardView = cardObj.GetComponent<CardView>();
            if (cardView == null) cardView = cardObj.AddComponent<CardView>();

            cardView.Initialize(data, manager);
            _cardsInHand.Add(cardView);

            ReorganizeHand();
            cardView.PlayDrawAnimation();
            return cardView;
        }

        /// <summary>
        /// Limpia todas las cartas presentes en la mano visual.
        /// </summary>
        public void ClearHand()
        {
            for (int i = _cardsInHand.Count - 1; i >= 0; i--)
            {
                if (_cardsInHand[i] != null)
                {
                    Destroy(_cardsInHand[i].gameObject);
                }
            }
            _cardsInHand.Clear();
        }

        /// <summary>
        /// Elimina una carta de la mano física.
        /// </summary>
        public void RemoveCardFromHand(CardView cardView)
        {
            if (_cardsInHand.Remove(cardView))
            {
                ReorganizeHand();
            }
        }

        /// <summary>
        /// Reposiciona todas las cartas en abanico/fila centrada horizontalmente.
        /// Se eleva a Y=115f para que la carta de 200px de altura quede perfectamente visible sobre el borde inferior.
        /// </summary>
        public void ReorganizeHand()
        {
            int count = _cardsInHand.Count;
            if (count == 0) return;

            float totalWidth = (count - 1) * cardSpacing;
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                CardView card = _cardsInHand[i];
                if (card == null) continue;

                Vector3 targetPos = new Vector3(startX + (i * cardSpacing), 115f, 0f);
                card.SetBasePosition(targetPos);
            }
        }
    }
}

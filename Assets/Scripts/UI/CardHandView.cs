using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChessBalatro
{
    /// <summary>
    /// Modo de alineación espacial de la mano de cartas en el Canvas uGUI.
    /// </summary>
    public enum HandAlignment
    {
        /// <summary>
        /// Anclado en la parte inferior derecha de la pantalla (Recomendado).
        /// Despeja las casillas centrales del tablero (Rey, Dama y piezas de rango 1).
        /// </summary>
        BottomRight,

        /// <summary>
        /// Columna vertical o abanico ubicado en el lateral derecho de la pantalla.
        /// </summary>
        RightSide,

        /// <summary>
        /// Centrado clásico en la parte inferior de la pantalla.
        /// </summary>
        BottomCenter
    }

    /// <summary>
    /// Gestiona el contenedor visual de la mano de cartas en el Canvas uGUI.
    /// Organiza las cartas a gran escala en el lateral derecho de la pantalla
    /// con separación equilibrada, abanico cinético y elevación interactiva estilo Balatro.
    /// </summary>
    public class CardHandView : MonoBehaviour
    {
        [Header("Alineación y Posicionamiento")]
        [Tooltip("Ubicación de anclaje de la mano de cartas en el Canvas.")]
        [SerializeField] private HandAlignment alignment = HandAlignment.RightSide;

        [Tooltip("Margen respecto al borde derecho de la pantalla (en píxeles de Canvas).")]
        [SerializeField] private float rightOffset = 30f;

        [Tooltip("Margen respecto al borde inferior de la pantalla (en píxeles de Canvas).")]
        [SerializeField] private float bottomOffset = 25f;

        [Header("Escala y Dimensiones de Cartas")]
        [Tooltip("Dimensiones base de las cartas en mano (Ancho x Alto).")]
        [SerializeField] private Vector2 cardDimensions = new Vector2(240f, 340f);

        [Tooltip("Separación horizontal estándar entre los centros de las cartas.")]
        [SerializeField] private float cardSpacing = 200f;

        [Tooltip("Ancho máximo permitido para la mano antes de comprimir dinámicamente las cartas.")]
        [SerializeField] private float maxHandWidth = 720f;

        [Header("Estética Balatro - Abanico y Elevación")]
        [Tooltip("Activa la curvatura parabólica y rotación en abanico de la mano.")]
        [SerializeField] private bool enableFanArch = true;

        [Tooltip("Ángulo máximo de rotación en abanico (en grados).")]
        [SerializeField] private float maxFanAngle = 4.5f;

        [Tooltip("Altura de elevación de la carta central en el abanico.")]
        [SerializeField] private float fanArchHeight = 14f;

        [Header("Prefab Opcional")]
        [SerializeField] private GameObject cardPrefab;

        private readonly List<CardView> _cardsInHand = new List<CardView>();
        public IReadOnlyList<CardView> CardsInHand => _cardsInHand;

        public HandAlignment Alignment
        {
            get => alignment;
            set
            {
                alignment = value;
                ConfigureHandContainerRect();
                ReorganizeHand();
            }
        }

        public Vector2 CardDimensions
        {
            get => cardDimensions;
            set
            {
                cardDimensions = value;
                ReorganizeHand();
            }
        }

        private RectTransform _rectTransform;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null) _rectTransform = gameObject.AddComponent<RectTransform>();

            ConfigureHandContainerRect();
        }

        private void OnEnable()
        {
            ConfigureHandContainerRect();
            ReorganizeHand();
        }

        private void Start()
        {
            ConfigureHandContainerRect();
            ReorganizeHand();
        }

        private void OnRectTransformDimensionsChange()
        {
            ConfigureHandContainerRect();
            ReorganizeHand();
        }

        /// <summary>
        /// Configura el contenedor según la alineación elegida (por defecto inferior derecha).
        /// </summary>
        public void ConfigureHandContainerRect()
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();

            switch (alignment)
            {
                case HandAlignment.BottomRight:
                    _rectTransform.anchorMin = new Vector2(1f, 0f);
                    _rectTransform.anchorMax = new Vector2(1f, 0f);
                    _rectTransform.pivot = new Vector2(1f, 0f);
                    _rectTransform.anchoredPosition = new Vector2(-rightOffset, bottomOffset);
                    break;

                case HandAlignment.RightSide:
                    _rectTransform.anchorMin = new Vector2(1f, 0.5f);
                    _rectTransform.anchorMax = new Vector2(1f, 0.5f);
                    _rectTransform.pivot = new Vector2(1f, 0.5f);
                    _rectTransform.anchoredPosition = new Vector2(-rightOffset, 0f);
                    break;

                case HandAlignment.BottomCenter:
                    _rectTransform.anchorMin = new Vector2(0.5f, 0f);
                    _rectTransform.anchorMax = new Vector2(0.5f, 0f);
                    _rectTransform.pivot = new Vector2(0.5f, 0f);
                    _rectTransform.anchoredPosition = new Vector2(0f, bottomOffset);
                    break;
            }
        }

        /// <summary>
        /// Crea e incorpora una nueva carta física a la mano visual con las dimensiones ampliadas.
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

            // Aplicar dimensiones ampliadas
            cardView.ApplyCardSize(cardDimensions);
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
        /// <summary>
        /// Reposiciona todas las cartas en la mano según el modo de alineación configurado.
        /// Excluye automáticamente las cartas que han desaparecido al ser seleccionadas para re-centrar el mazo.
        /// </summary>
        public void ReorganizeHand()
        {
            var visibleCards = new List<CardView>();
            for (int i = 0; i < _cardsInHand.Count; i++)
            {
                if (_cardsInHand[i] != null && !_cardsInHand[i].IsDisappeared)
                {
                    visibleCards.Add(_cardsInHand[i]);
                }
            }

            int count = visibleCards.Count;
            if (count == 0) return;

            switch (alignment)
            {
                case HandAlignment.BottomRight:
                    ReorganizeBottomRight(visibleCards);
                    break;

                case HandAlignment.RightSide:
                    ReorganizeRightSide(visibleCards);
                    break;

                case HandAlignment.BottomCenter:
                    ReorganizeBottomCenter(visibleCards);
                    break;
            }
        }

        private void ReorganizeBottomRight(List<CardView> cards)
        {
            int count = cards.Count;
            float spacing = cardSpacing;
            if (count > 1 && (count - 1) * spacing > maxHandWidth)
            {
                spacing = maxHandWidth / (count - 1);
            }

            for (int i = 0; i < count; i++)
            {
                CardView card = cards[i];
                if (card == null) continue;

                card.ApplyCardSize(cardDimensions);
                card.SetVerticalLayout(false);

                // Card 0 (izquierda) a Card N-1 (derecha, más pegada al margen derecho)
                float posX = -(cardDimensions.x * 0.5f) - (count - 1 - i) * spacing;
                float posY = cardDimensions.y * 0.5f;
                float rotZ = 0f;

                if (enableFanArch && count > 1)
                {
                    float t = ((float)i / (count - 1)) - 0.5f; // -0.5f a 0.5f
                    rotZ = -t * maxFanAngle * 2f;               // Abanico
                    posY += (1f - 4f * t * t) * fanArchHeight;  // Arco parabólico
                }

                card.SetBasePosition(new Vector3(posX, posY, 0f));
                card.SetBaseRotationAngle(rotZ);
            }
        }

        private void ReorganizeRightSide(List<CardView> cards)
        {
            int count = cards.Count;
            float verticalSpacing = Mathf.Min(cardSpacing, 280f);
            float maxAllowedHeight = 1100f;

            if (count > 1 && (count - 1) * verticalSpacing > maxAllowedHeight)
            {
                verticalSpacing = maxAllowedHeight / (count - 1);
            }

            float totalHeight = (count - 1) * verticalSpacing;
            float startY = totalHeight * 0.5f;

            for (int i = 0; i < count; i++)
            {
                CardView card = cards[i];
                if (card == null) continue;

                card.ApplyCardSize(cardDimensions);
                card.SetVerticalLayout(true);

                float posX = -(cardDimensions.x * 0.5f);
                float posY = startY - (i * verticalSpacing);

                card.SetBasePosition(new Vector3(posX, posY, 0f));
                card.SetBaseRotationAngle(0f);
            }
        }

        private void ReorganizeBottomCenter(List<CardView> cards)
        {
            int count = cards.Count;
            float spacing = cardSpacing;
            if (count > 1 && (count - 1) * spacing > maxHandWidth)
            {
                spacing = maxHandWidth / (count - 1);
            }

            float totalWidth = (count - 1) * spacing;
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                CardView card = cards[i];
                if (card == null) continue;

                card.ApplyCardSize(cardDimensions);
                card.SetVerticalLayout(false);

                float posX = startX + (i * spacing);
                float posY = cardDimensions.y * 0.5f;
                float rotZ = 0f;

                if (enableFanArch && count > 1)
                {
                    float t = ((float)i / (count - 1)) - 0.5f;
                    rotZ = -t * maxFanAngle * 2f;
                    posY += (1f - 4f * t * t) * fanArchHeight;
                }

                card.SetBasePosition(new Vector3(posX, posY, 0f));
                card.SetBaseRotationAngle(rotZ);
            }
        }
    }
}

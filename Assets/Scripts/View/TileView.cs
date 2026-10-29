using System;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Representa la vista individual de una casilla del tablero (SpriteRenderer, interacción y resaltado).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public class TileView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private BoxCollider2D boxCollider;

        public BoardCoord Coord { get; private set; }
        public Color BaseColor { get; private set; }
        public bool IsLightSquare => (Coord.File + Coord.Rank) % 2 != 0;

        public event Action<TileView> OnTileClicked;
        public event Action<TileView> OnTileHoverEnter;
        public event Action<TileView> OnTileHoverExit;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();
        }

        public void Initialize(BoardCoord coord, Color baseColor, Sprite sprite, float size)
        {
            Coord = coord;
            BaseColor = baseColor;

            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();

            spriteRenderer.sprite = sprite;
            spriteRenderer.color = baseColor;
            spriteRenderer.drawMode = SpriteDrawMode.Simple;
            spriteRenderer.sortingOrder = 0;

            transform.localScale = Vector3.one * size;
            boxCollider.size = Vector2.one;
            boxCollider.isTrigger = true;
        }

        public void SetHighlight(Color highlightColor)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = highlightColor;
            }
        }

        public void ResetColor()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = BaseColor;
            }
        }

        private void OnMouseDown()
        {
            OnTileClicked?.Invoke(this);
        }

        private void OnMouseEnter()
        {
            OnTileHoverEnter?.Invoke(this);
        }

        private void OnMouseExit()
        {
            OnTileHoverExit?.Invoke(this);
        }
    }
}

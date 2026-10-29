using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Maneja la representación visual de una pieza de ajedrez en escena.
    /// Soporta temas visuales (PieceTheme), colores configurables, sprites y símbolos.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PieceView : MonoBehaviour
    {
        [Header("Componentes Visuales")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TextMesh symbolTextMesh;

        public BoardCoord Coord { get; private set; }
        public Piece PieceData { get; private set; }
        public PieceTheme CurrentTheme { get; private set; }

        public event Action<PieceView> OnPieceClicked;

        private static readonly Dictionary<string, Sprite> DiscSpriteCache = new Dictionary<string, Sprite>();

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Initialize(Piece piece, BoardCoord coord, Vector3 worldPosition, PieceTheme theme = null)
        {
            PieceData = piece;
            Coord = coord;
            CurrentTheme = theme;
            transform.position = worldPosition;

            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

            bool isWhite = piece.Color == PieceColor.White;
            Sprite customSprite = theme != null ? theme.GetSprite(piece.Type, piece.Color) : null;

            float scale = theme != null ? theme.pieceScale : 0.85f;
            transform.localScale = Vector3.one * scale;

            if (customSprite != null)
            {
                spriteRenderer.sprite = customSprite;
                bool shouldTint = theme != null && theme.tintSprites;
                spriteRenderer.color = shouldTint ? theme.GetPieceColor(piece.Color) : Color.white;
                spriteRenderer.sortingOrder = 1;

                if (symbolTextMesh != null)
                {
                    symbolTextMesh.gameObject.SetActive(false);
                }
            }
            else
            {
                Color fillColor = theme != null ? theme.GetPieceColor(piece.Color) : (isWhite ? new Color(0.96f, 0.97f, 0.98f, 1f) : new Color(0.18f, 0.19f, 0.22f, 1f));
                Color borderColor = theme != null ? theme.GetBorderColor(piece.Color) : (isWhite ? new Color(0.72f, 0.76f, 0.82f, 1f) : new Color(0.38f, 0.42f, 0.48f, 1f));
                Color textColor = theme != null ? theme.GetTextColor(piece.Color) : (isWhite ? new Color(0.12f, 0.12f, 0.15f, 1f) : new Color(0.95f, 0.96f, 0.98f, 1f));

                Sprite discSprite = GetOrCreateDiscSprite(fillColor, borderColor);
                spriteRenderer.sprite = discSprite;
                spriteRenderer.color = Color.white;
                spriteRenderer.sortingOrder = 1;

                CreateOrUpdateTextMesh(piece, textColor);
            }

            // Collider para clics en la pieza
            CircleCollider2D col = GetComponent<CircleCollider2D>();
            if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.45f;
            col.isTrigger = true;
        }

        private void CreateOrUpdateTextMesh(Piece piece, Color textColor)
        {
            if (symbolTextMesh == null)
            {
                Transform existing = transform.Find("SymbolText");
                if (existing != null)
                {
                    symbolTextMesh = existing.GetComponent<TextMesh>();
                }
                else
                {
                    GameObject textObj = new GameObject("SymbolText");
                    textObj.transform.SetParent(transform, false);
                    textObj.transform.localPosition = new Vector3(0, 0, -0.05f);

                    symbolTextMesh = textObj.AddComponent<TextMesh>();
                    symbolTextMesh.alignment = TextAlignment.Center;
                    symbolTextMesh.anchor = TextAnchor.MiddleCenter;
                    symbolTextMesh.fontSize = 54;
                    symbolTextMesh.characterSize = 0.08f;
                    symbolTextMesh.fontStyle = FontStyle.Bold;

                    MeshRenderer mr = textObj.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.sortingOrder = 2; // Por encima de la ficha
                    }
                }
            }

            char symbol = piece.GetSymbol();
            symbolTextMesh.text = symbol.ToString().ToUpperInvariant();
            symbolTextMesh.color = textColor;
            symbolTextMesh.gameObject.SetActive(true);
        }

        private Coroutine _activeMoveCoroutine;
        private Coroutine _activeCaptureCoroutine;
        private MeshRenderer _symbolMeshRenderer;
        private int _originalSpriteOrder = 1;
        private int _originalTextOrder = 2;
        private bool _isMoving = false;

        public bool IsAnimating => _isMoving || _activeMoveCoroutine != null || _activeCaptureCoroutine != null;

        private void OnDisable()
        {
            // Restaurar sorting order en caso de desactivación inesperada durante una animación
            if (_isMoving)
            {
                if (spriteRenderer != null) spriteRenderer.sortingOrder = _originalSpriteOrder;
                if (_symbolMeshRenderer != null) _symbolMeshRenderer.sortingOrder = _originalTextOrder;
                _isMoving = false;
            }
        }

        public void UpdateCoord(BoardCoord newCoord)
        {
            Coord = newCoord;
        }

        public void SetPosition(BoardCoord newCoord, Vector3 newWorldPosition)
        {
            Coord = newCoord;
            transform.position = newWorldPosition;
        }

        /// <summary>
        /// Desplaza suavemente la pieza hacia la posición de destino en el mundo usando interpolación cúbica (ease-out).
        /// Eleva temporalmente el sortingOrder durante el desplazamiento para que pase por encima de las demás piezas.
        /// </summary>
        public IEnumerator AnimateMoveTo(Vector3 targetWorldPos, float duration = 0.2f)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (_symbolMeshRenderer == null && symbolTextMesh != null)
            {
                _symbolMeshRenderer = symbolTextMesh.GetComponent<MeshRenderer>();
            }

            _originalSpriteOrder = spriteRenderer != null ? spriteRenderer.sortingOrder : 1;
            _originalTextOrder = _symbolMeshRenderer != null ? _symbolMeshRenderer.sortingOrder : 2;

            // Elevar temporalmente el sortingOrder durante el desplazamiento
            if (spriteRenderer != null) spriteRenderer.sortingOrder = _originalSpriteOrder + 20;
            if (_symbolMeshRenderer != null) _symbolMeshRenderer.sortingOrder = _originalTextOrder + 20;

            _isMoving = true;
            Vector3 startPos = transform.position;
            float elapsed = 0f;
            duration = Mathf.Max(duration, 0.01f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Interpolación cúbica ease-out: f(t) = 1 - (1 - t)^3 para una sensación táctil limpia y ágil
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);

                transform.position = Vector3.Lerp(startPos, targetWorldPos, easeOut);
                yield return null;
            }

            transform.position = targetWorldPos;

            // Restaurar sortingOrder original
            if (spriteRenderer != null) spriteRenderer.sortingOrder = _originalSpriteOrder;
            if (_symbolMeshRenderer != null) _symbolMeshRenderer.sortingOrder = _originalTextOrder;

            _isMoving = false;
            _activeMoveCoroutine = null;
        }

        /// <summary>
        /// Inicia la corutina de movimiento gestionando la referencia activa.
        /// </summary>
        public Coroutine StartMoveAnimation(Vector3 targetWorldPos, float duration = 0.2f)
        {
            if (_activeMoveCoroutine != null)
            {
                StopCoroutine(_activeMoveCoroutine);
            }
            _activeMoveCoroutine = StartCoroutine(AnimateMoveTo(targetWorldPos, duration));
            return _activeMoveCoroutine;
        }

        /// <summary>
        /// Aplica una animación rápida de desvanecimiento de opacidad (alpha) y escala reducida hacia cero
        /// antes de destruir o desactivar el GameObject de la pieza capturada.
        /// </summary>
        public IEnumerator AnimateCaptured(float duration = 0.18f, bool deactivateInsteadOfDestroy = false)
        {
            // Desactivar collider de inmediato para evitar toques durante la transición
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

            Vector3 startScale = transform.localScale;
            Color startSpriteColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
            Color startTextColor = symbolTextMesh != null ? symbolTextMesh.color : Color.white;

            duration = Mathf.Max(duration, 0.01f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease-out para reducción de escala
                float easeOut = 1f - Mathf.Pow(1f - t, 2f);

                // Reducir escala hacia cero
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, easeOut);

                // Desvanecer alpha del sprite
                if (spriteRenderer != null)
                {
                    Color sc = startSpriteColor;
                    sc.a = Mathf.Lerp(startSpriteColor.a, 0f, easeOut);
                    spriteRenderer.color = sc;
                }

                // Desvanecer alpha del texto de símbolo si existe
                if (symbolTextMesh != null)
                {
                    Color tc = startTextColor;
                    tc.a = Mathf.Lerp(startTextColor.a, 0f, easeOut);
                    symbolTextMesh.color = tc;
                }

                yield return null;
            }

            _activeCaptureCoroutine = null;

            if (deactivateInsteadOfDestroy)
            {
                gameObject.SetActive(false);
            }
            else
            {
                DestroySelf();
            }
        }

        /// <summary>
        /// Inicia la animación de captura y posterior destrucción de forma segura.
        /// </summary>
        public void CaptureAndDestroy(float duration = 0.18f)
        {
            if (gameObject.activeInHierarchy)
            {
                if (_activeCaptureCoroutine != null) StopCoroutine(_activeCaptureCoroutine);
                _activeCaptureCoroutine = StartCoroutine(AnimateCaptured(duration, deactivateInsteadOfDestroy: false));
            }
            else
            {
                DestroySelf();
            }
        }

        /// <summary>
        /// Inicia la animación de captura y posterior desactivación (pooling-friendly).
        /// </summary>
        public void CaptureAndDeactivate(float duration = 0.18f)
        {
            if (gameObject.activeInHierarchy)
            {
                if (_activeCaptureCoroutine != null) StopCoroutine(_activeCaptureCoroutine);
                _activeCaptureCoroutine = StartCoroutine(AnimateCaptured(duration, deactivateInsteadOfDestroy: true));
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void OnMouseDown()
        {
            OnPieceClicked?.Invoke(this);
        }

        public void DestroySelf()
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        /// <summary>
        /// Genera y cachea una textura circular suave con borde basada en los colores configurados.
        /// </summary>
        private static Sprite GetOrCreateDiscSprite(Color fillColor, Color borderColor)
        {
            string cacheKey = $"{ColorUtility.ToHtmlStringRGBA(fillColor)}_{ColorUtility.ToHtmlStringRGBA(borderColor)}";
            if (DiscSpriteCache.TryGetValue(cacheKey, out Sprite cached) && cached != null)
            {
                return cached;
            }

            int size = 96;
            float radius = (size - 4) * 0.5f;
            float center = size * 0.5f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                    if (dist > radius + 1f)
                    {
                        texture.SetPixel(x, y, Color.clear);
                    }
                    else if (dist > radius - 3.5f)
                    {
                        float alpha = Mathf.Clamp01(radius + 1f - dist);
                        texture.SetPixel(x, y, new Color(borderColor.r, borderColor.g, borderColor.b, borderColor.a * alpha));
                    }
                    else
                    {
                        texture.SetPixel(x, y, fillColor);
                    }
                }
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size
            );

            DiscSpriteCache[cacheKey] = sprite;
            return sprite;
        }
    }
}

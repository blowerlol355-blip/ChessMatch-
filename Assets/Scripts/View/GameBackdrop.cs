using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Gestiona el fondo visual in-game (SampleScene) detrás del tablero de ajedrez.
    /// Muestra la ilustración artística oficial de Grandes Maestros (GameBackground)
    /// escalada dinámicamente para cubrir el frustum de la cámara ortográfica en cualquier
    /// relación de aspecto (9:16 móvil, 16:9 PC, etc.) con una capa atmosférica de alto contraste.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class GameBackdrop : MonoBehaviour
    {
        public static GameBackdrop Instance { get; private set; }

        [Header("Configuración del Fondo")]
        [Tooltip("Sprite con la ilustración de Grandes Maestros y Cartas.")]
        [SerializeField] private Sprite backdropSprite;

        [Tooltip("Color de tinte atmosférico. Oscurece sutilmente el fondo para que el tablero y las piezas mantengan máximo contraste.")]
        [SerializeField] private Color backdropColor = new Color(0.42f, 0.44f, 0.52f, 1f);

        [Tooltip("Capa de ordenamiento del SpriteRenderer (muy por detrás del tablero a -100).")]
        [SerializeField] private int sortingOrder = -100;

        [Header("Efectos Cinematográficos")]
        [Tooltip("Activa una sutil oscilación o respiración de escala estilo arcade.")]
        [SerializeField] private bool enableSubtlePulse = true;
        [SerializeField] private float pulseSpeed = 0.4f;
        [SerializeField] private float pulseIntensity = 0.015f;

        private SpriteRenderer _spriteRenderer;
        private Camera _cachedCamera;

        public static GameBackdrop EnsureBackdrop()
        {
            if (Instance != null) return Instance;

            GameBackdrop existing = FindAnyObjectByType<GameBackdrop>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject bgObj = new GameObject("GameBackdrop");
            GameBackdrop backdrop = bgObj.AddComponent<GameBackdrop>();
            return backdrop;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            _spriteRenderer.sortingOrder = sortingOrder;

            LoadSpriteIfNeeded();
            ApplyColor();
            FitToCamera();
        }

        private void Start()
        {
            LoadSpriteIfNeeded();
            FitToCamera();
        }

        private void LateUpdate()
        {
            FitToCamera();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LoadSpriteIfNeeded()
        {
            if (backdropSprite == null)
            {
                backdropSprite = Resources.Load<Sprite>("GameBackground");
            }

            // Fallback: Si no se importó aún como Sprite, crear Sprite dinámicamente desde Texture2D
            if (backdropSprite == null)
            {
                Texture2D tex = Resources.Load<Texture2D>("GameBackground");
                if (tex != null)
                {
                    backdropSprite = Sprite.Create(
                        tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f),
                        100f
                    );
                }
            }

            if (_spriteRenderer != null && backdropSprite != null)
            {
                _spriteRenderer.sprite = backdropSprite;
            }
        }

        private void ApplyColor()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = backdropColor;
            }
        }

        public void SetBackdropColor(Color color)
        {
            backdropColor = color;
            ApplyColor();
        }

        public void FitToCamera()
        {
            if (_cachedCamera == null)
            {
                _cachedCamera = Camera.main;
            }
            if (_cachedCamera == null || _spriteRenderer == null || _spriteRenderer.sprite == null) return;

            float camHeight = _cachedCamera.orthographic ? _cachedCamera.orthographicSize * 2f : 10f;
            float camWidth = camHeight * _cachedCamera.aspect;

            Vector2 sprSize = _spriteRenderer.sprite.bounds.size;
            if (sprSize.x <= 0.001f || sprSize.y <= 0.001f) return;

            // Aspect Fill: escala suficiente para cubrir ancho y alto de pantalla
            float scaleX = camWidth / sprSize.x;
            float scaleY = camHeight / sprSize.y;
            float baseScale = Mathf.Max(scaleX, scaleY) * 1.05f;

            float pulse = 1f;
            if (enableSubtlePulse)
            {
                pulse += Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) * pulseIntensity;
            }

            float finalScale = baseScale * pulse;
            transform.localScale = new Vector3(finalScale, finalScale, 1f);

            // Centrar detrás de la cámara con offset Z
            Vector3 camPos = _cachedCamera.transform.position;
            transform.position = new Vector3(camPos.x, camPos.y, 5f);
        }
    }
}


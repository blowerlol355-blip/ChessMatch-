using UnityEngine;
using UnityEngine.UI;

namespace ChessBalatro
{
    /// <summary>
    /// Fondo animado estilo 32-bits Arcade / Pixel Art.
    /// Soporta la ilustración artística oficial de Grandes Maestros (GameBackground)
    /// con encuadre automático responsive (ScaleAndCrop), viñeta cinematográfica y pulso ambiental.
    /// También incluye fallback automático al patrón procedural pixel-art de ajedrez en bucle.
    /// Funciona de forma automática tanto con uGUI (RawImage) como con renderizado OnGUI.
    /// </summary>
    public class ArcadeBackgroundScroller : MonoBehaviour
    {
        public static ArcadeBackgroundScroller Instance { get; private set; }

        [Header("Ilustración Artística de Fondo")]
        [Tooltip("Si está activo, utiliza la ilustración oficial del juego como fondo de pantalla.")]
        [SerializeField] private bool useArtworkBackground = true;

        [Tooltip("Textura de fondo con la ilustración de Grandes Maestros y Cartas.")]
        [SerializeField] private Texture2D backgroundArtworkTexture;

        [Tooltip("Brillo o exposición del fondo artístico para balancear la legibilidad de la interfaz.")]
        [Range(0.2f, 1.2f)]
        [SerializeField] private float artworkBrightness = 0.85f;

        [Tooltip("Tinte de color ambiental aplicado a la ilustración.")]
        [SerializeField] private Color artworkTint = new Color(0.92f, 0.94f, 0.98f, 1f);

        [Tooltip("Activa una sutil animación de respiración / flotación en el menú.")]
        [SerializeField] private bool enableArtworkBreathing = true;
        [SerializeField] private float breathingScale = 0.02f;
        [SerializeField] private float breathingSpeed = 0.6f;

        [Header("Configuración Procedural Fallback")]
        [Tooltip("Velocidad de scrolling diagonal continuo en coordenadas UV.")]
        [SerializeField] private Vector2 scrollSpeed = new Vector2(0.035f, 0.025f);

        [Tooltip("Densidad o repetición de casillas del patrón en pantalla.")]
        [SerializeField] private float tileDensity = 14f;

        [Header("Colores Retro 32-Bits")]
        [SerializeField] private Color colorA = new Color(0.070f, 0.086f, 0.122f, 1f); // #12161F
        [SerializeField] private Color colorB = new Color(0.094f, 0.118f, 0.161f, 1f); // #181E29
        [SerializeField] private Color gridLineColor = new Color(0.125f, 0.155f, 0.205f, 1f); // #202734

        [Header("Efectos Ambientales")]
        [SerializeField] private bool enableVignette = true;
        [SerializeField] private float vignetteStrength = 0.85f;
        [SerializeField] private bool enableAmbientPulse = true;
        [SerializeField] private float pulseFrequency = 0.8f;
        [SerializeField] private float pulseIntensity = 0.03f;

        [Header("Componentes uGUI Opcionales")]
        [SerializeField] private RawImage targetRawImage;

        private Texture2D _checkerTexture;
        private Texture2D _vignetteTexture;
        private Vector2 _currentUVOffset = Vector2.zero;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            EnsureArtworkLoaded();
            GenerateTextures();
            SetupRawImageIfPresent();
        }

        private void Start()
        {
            if (targetRawImage == null)
            {
                targetRawImage = GetComponentInChildren<RawImage>();
            }
            EnsureArtworkLoaded();
            SetupRawImageIfPresent();
        }

        private void EnsureArtworkLoaded()
        {
            if (backgroundArtworkTexture == null)
            {
                backgroundArtworkTexture = Resources.Load<Texture2D>("GameBackground");
            }
        }

        private void Update()
        {
            // Desplazamiento UV continuo independiente del framerate (para el patrón checker)
            _currentUVOffset += scrollSpeed * Time.unscaledDeltaTime;
            _currentUVOffset.x %= 1f;
            _currentUVOffset.y %= 1f;

            if (targetRawImage != null)
            {
                if (useArtworkBackground && backgroundArtworkTexture != null)
                {
                    float pulse = enableAmbientPulse
                        ? 1f + Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * pulseIntensity
                        : 1f;
                    targetRawImage.color = artworkTint * (artworkBrightness * pulse);
                }
                else
                {
                    float aspect = (float)Screen.width / Mathf.Max(Screen.height, 1);
                    targetRawImage.uvRect = new Rect(_currentUVOffset.x, _currentUVOffset.y, tileDensity * aspect, tileDensity);

                    if (enableAmbientPulse)
                    {
                        float pulse = 1f + Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * pulseIntensity;
                        targetRawImage.color = new Color(pulse, pulse, pulse, 1f);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_checkerTexture != null) Destroy(_checkerTexture);
            if (_vignetteTexture != null) Destroy(_vignetteTexture);
        }

        private void SetupRawImageIfPresent()
        {
            if (targetRawImage != null)
            {
                if (useArtworkBackground && backgroundArtworkTexture != null)
                {
                    targetRawImage.texture = backgroundArtworkTexture;
                    targetRawImage.uvRect = new Rect(0, 0, 1, 1);
                    targetRawImage.color = artworkTint * artworkBrightness;
                }
                else if (_checkerTexture != null)
                {
                    targetRawImage.texture = _checkerTexture;
                }
                targetRawImage.raycastTarget = false;
            }
        }

        #region Generación Procedural de Texturas Pixel Art (Point Filter)

        private void GenerateTextures()
        {
            // 1. Textura de Ajedrez Pixel Art (32x32 píxeles con FilterMode.Point)
            int size = 32;
            _checkerTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            int half = size / 2;
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isSquareA = ((x < half && y < half) || (x >= half && y >= half));
                    Color baseCol = isSquareA ? colorA : colorB;

                    // Línea de ribete de 1px en los bordes de cada casilla para relieve arcade
                    bool isBorder = (x == 0 || x == half || y == 0 || y == half);
                    if (isBorder)
                    {
                        baseCol = Color.Lerp(baseCol, gridLineColor, 0.65f);
                    }

                    pixels[y * size + x] = baseCol;
                }
            }

            _checkerTexture.SetPixels(pixels);
            _checkerTexture.Apply();

            // 2. Textura de Viñeta Radial (128x128 con gradiente suave hacia bordes)
            int vSize = 128;
            _vignetteTexture = new Texture2D(vSize, vSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] vPixels = new Color[vSize * vSize];
            Vector2 center = new Vector2(vSize * 0.5f, vSize * 0.5f);
            float maxDist = center.magnitude;

            for (int y = 0; y < vSize; y++)
            {
                for (int x = 0; x < vSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    // Curva cuadrática suave de viñeta
                    float alpha = Mathf.Clamp01(Mathf.Pow(dist, 1.8f) * vignetteStrength);
                    vPixels[y * vSize + x] = new Color(0.02f, 0.03f, 0.05f, alpha);
                }
            }

            _vignetteTexture.SetPixels(vPixels);
            _vignetteTexture.Apply();
        }

        #endregion

        #region Renderizado OnGUI Fallback (Instantáneo en MainMenuScene)

        private void OnGUI()
        {
            // Solo dibujamos en OnGUI si no hay un RawImage activo cubriendo la pantalla
            if (targetRawImage != null && targetRawImage.gameObject.activeInHierarchy) return;

            float screenW = Screen.width;
            float screenH = Screen.height;
            Rect screenRect = new Rect(0, 0, screenW, screenH);

            GUI.depth = 200; // Capa más profunda de la pantalla

            Color oldColor = GUI.color;

            // Prioridad 1: Ilustración Artística Oficial
            if (useArtworkBackground && (backgroundArtworkTexture != null || (backgroundArtworkTexture = Resources.Load<Texture2D>("GameBackground")) != null))
            {
                float pulse = enableAmbientPulse
                    ? 1f + Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * pulseIntensity
                    : 1f;

                Color drawCol = artworkTint * (artworkBrightness * pulse);
                drawCol.a = 1f;
                GUI.color = drawCol;

                Rect drawRect = screenRect;
                if (enableArtworkBreathing)
                {
                    float breathe = 1f + Mathf.Sin(Time.time * breathingSpeed) * breathingScale;
                    float extraW = screenW * (breathe - 1f);
                    float extraH = screenH * (breathe - 1f);
                    drawRect = new Rect(-extraW * 0.5f, -extraH * 0.5f, screenW + extraW, screenH + extraH);
                }

                GUI.DrawTexture(drawRect, backgroundArtworkTexture, ScaleMode.ScaleAndCrop);

                // Viñeta perimetral sobre la ilustración para resaltar la interfaz central
                if (enableVignette && _vignetteTexture != null)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.88f);
                    GUI.DrawTexture(screenRect, _vignetteTexture, ScaleMode.StretchToFill);
                }
            }
            else
            {
                // Prioridad 2: Tablero de ajedrez pixel art procedural
                if (_checkerTexture == null) GenerateTextures();

                float aspect = screenW / Mathf.Max(screenH, 1f);
                float pulse = enableAmbientPulse
                    ? 1f + Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * pulseIntensity
                    : 1f;

                GUI.color = new Color(pulse, pulse, pulse, 1f);

                Rect uvRect = new Rect(_currentUVOffset.x, _currentUVOffset.y, tileDensity * aspect, tileDensity);
                GUI.DrawTextureWithTexCoords(screenRect, _checkerTexture, uvRect, false);

                if (enableVignette && _vignetteTexture != null)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(screenRect, _vignetteTexture, ScaleMode.StretchToFill);
                }
            }

            GUI.color = oldColor;
        }

        #endregion
    }
}

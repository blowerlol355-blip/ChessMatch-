using System.Collections;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Efecto visual del Escudo de Petrosián.
    /// Se instancia sobre la casilla del Rey blanco cuando <see cref="PetrosianDefenseEffect"/>
    /// dispara su evento estático <see cref="PetrosianDefenseEffect.OnShieldActivated"/>.
    ///
    /// Ciclo visual:
    ///   1. Fade-in dorado/neón en 0.2 s.
    ///   2. Pulso brillante persistente mientras el escudo esté activo.
    ///   3. Fade-out suave en 0.3 s al recibir <see cref="PetrosianDefenseEffect.OnShieldDeactivated"/>.
    ///
    /// Requiere que exista un <see cref="BoardView"/> en la escena para convertir
    /// coordenadas de tablero en posiciones de mundo.
    /// </summary>
    public class PetrosianShieldVisual : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────────────────────────

        [Header("Referencias")]
        [Tooltip("Referencia al BoardView para calcular posiciones de casilla.")]
        [SerializeField] private BoardView boardView;

        [Header("Prefab / Sprite del Escudo")]
        [Tooltip("SpriteRenderer usado para dibujar el escudo dorado sobre el Rey. " +
                 "Si es null, se crea uno proceduralmente.")]
        [SerializeField] private SpriteRenderer shieldRenderer;

        [Header("Estética")]
        [SerializeField] private Color shieldBaseColor  = new Color(1f, 0.85f, 0.2f,  0.0f);
        [SerializeField] private Color shieldPeakColor  = new Color(1f, 0.92f, 0.35f, 0.80f);
        [SerializeField] private float pulseFrequency   = 2.8f;
        [SerializeField] private float pulseAmplitude   = 0.15f;
        [SerializeField] private float fadeInDuration   = 0.20f;
        [SerializeField] private float fadeOutDuration  = 0.30f;
        [SerializeField] private float shieldScale      = 1.12f;   // ligeramente mayor que una casilla

        // ── Estado interno ─────────────────────────────────────────────────────────────────────────

        private bool        _isActive       = false;
        private Coroutine   _pulseCoroutine;

        // ── Ciclo de vida ─────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (boardView == null)
                boardView = FindAnyObjectByType<BoardView>();

            EnsureShieldRenderer();

            // Comenzar oculto
            SetAlpha(0f);
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            PetrosianDefenseEffect.OnShieldActivated   += HandleShieldActivated;
            PetrosianDefenseEffect.OnShieldDeactivated += HandleShieldDeactivated;
        }

        private void OnDisable()
        {
            PetrosianDefenseEffect.OnShieldActivated   -= HandleShieldActivated;
            PetrosianDefenseEffect.OnShieldDeactivated -= HandleShieldDeactivated;
        }

        // ── Handlers de eventos ───────────────────────────────────────────────────────────────────

        private void HandleShieldActivated(BoardCoord kingCoord)
        {
            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }

            // Reposicionar sobre el Rey
            PositionOverCoord(kingCoord);
            gameObject.SetActive(true);
            _isActive = true;

            _pulseCoroutine = StartCoroutine(FadeInAndPulse());
        }

        private void HandleShieldDeactivated()
        {
            if (!_isActive) return;

            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }

            _isActive = false;
            _pulseCoroutine = StartCoroutine(FadeOut());
        }

        // ── Corutinas de animación ─────────────────────────────────────────────────────────────────

        private IEnumerator FadeInAndPulse()
        {
            // 1. Fade-in rápido
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeInDuration);
                SetAlpha(Mathf.Lerp(0f, shieldPeakColor.a, t));
                yield return null;
            }
            SetAlpha(shieldPeakColor.a);

            // 2. Pulso brillante mientras el escudo esté activo
            while (_isActive)
            {
                float pulse = Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * pulseAmplitude;
                SetAlpha(Mathf.Clamp01(shieldPeakColor.a + pulse));
                yield return null;
            }
        }

        private IEnumerator FadeOut()
        {
            float startAlpha = shieldRenderer != null ? shieldRenderer.color.a : 0f;
            float elapsed    = 0f;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutDuration);
                SetAlpha(Mathf.Lerp(startAlpha, 0f, t));
                yield return null;
            }

            SetAlpha(0f);
            gameObject.SetActive(false);
            _pulseCoroutine = null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────────────────────

        private void SetAlpha(float alpha)
        {
            if (shieldRenderer == null) return;
            Color c = shieldPeakColor;
            c.a = alpha;
            shieldRenderer.color = c;
        }

        /// <summary>
        /// Convierte una coordenada del tablero a posición de mundo y reposiciona el escudo.
        /// Fallback: posición (0,0,0) si no existe BoardView.
        /// </summary>
        private void PositionOverCoord(BoardCoord coord)
        {
            if (boardView != null)
            {
                // BoardView.CoordToWorldPosition convierte (file, rank) a Vector3 de mundo
                Vector3 worldPos = boardView.CoordToWorldPosition(coord);
                worldPos.z = -0.5f; // asegurar que aparece sobre las piezas
                transform.position = worldPos;
            }

            transform.localScale = Vector3.one * shieldScale;
        }

        /// <summary>
        /// Construye un SpriteRenderer procedural con un quad semitransparente dorado
        /// si no se asignó ninguno en el Inspector.
        /// </summary>
        private void EnsureShieldRenderer()
        {
            if (shieldRenderer != null) return;

            shieldRenderer = gameObject.GetComponent<SpriteRenderer>();
            if (shieldRenderer == null)
                shieldRenderer = gameObject.AddComponent<SpriteRenderer>();

            // Sprite de 1×1 píxel blanco; el color lo controlamos en código
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            shieldRenderer.sprite = Sprite.Create(
                tex,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1f);

            shieldRenderer.sortingLayerName = "Default";
            shieldRenderer.sortingOrder     = 50;   // por encima de casillas y piezas
        }
    }
}

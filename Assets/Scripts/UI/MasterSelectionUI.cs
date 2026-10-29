using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChessBalatro
{
    /// <summary>
    /// Panel modal de Draft Inicial de Gran Maestro.
    ///
    /// Flujo:
    ///   1. <see cref="GameManager"/> cambia a <see cref="GameState.MasterSelecting"/>.
    ///   2. Este componente se hace visible y muestra las cartas de Gran Maestro disponibles.
    ///   3. El jugador selecciona una carta (resaltado con borde brillante + escala 1.08×).
    ///   4. Al confirmar, llama a <see cref="GameManager.ConfirmMasterSelection"/> y ejecuta
    ///      un fade-out antes de que el tablero aparezca.
    ///
    /// Zero-setup: Si no hay Canvas ni componentes uGUI asignados, construye toda la interfaz
    /// de forma procedural usando OnGUI como fallback de inmediata.
    /// </summary>
    public class MasterSelectionUI : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────────────────────────

        [Header("Control de Visibilidad")]
        [Tooltip("Cuando es true este panel se gestiona solo según el GameState. " +
                 "Ponlo en false para mostrarlo manualmente desde código.")]
        [SerializeField] private bool autoManageVisibility = true;

        [Header("Sprites de Ilustración (asignar desde Inspector o código)")]
        [Tooltip("Lista de sprites de ilustración para los Gran Maestros ofertados. " +
                 "Orden: mismo orden que masterPool.")]
        [SerializeField] private List<Sprite> masterIllustrations = new List<Sprite>();

        [Header("Canvas uGUI (opcional — se construye por código si es null)")]
        [SerializeField] private CanvasGroup panelCanvasGroup;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Text confirmButtonText;
        [SerializeField] private Transform cardContainer;

        [Header("Animación")]
        [SerializeField] private float fadeInDuration  = 0.30f;
        [SerializeField] private float fadeOutDuration = 0.35f;

        // ── Estado interno ─────────────────────────────────────────────────────────────────────────

        private bool         _isVisible         = false;
        private CardData     _selectedMaster    = null;
        private readonly List<CardView>  _masterCardViews = new List<CardView>();
        private readonly List<CardData>  _masterPool      = new List<CardData>();
        private Coroutine    _fadeCoroutine;

        // OnGUI fallback state
        private bool    _onGuiReady         = false;
        private GUIStyle _overlayStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _descStyle;
        private GUIStyle _typeStyle;
        private GUIStyle _confirmBtnStyle;
        private GUIStyle _cardFrameStyle;
        private GUIStyle _cardFrameSelectedStyle;
        private Texture2D _overlayTex;
        private Texture2D _panelTex;
        private Texture2D _cardFrameTex;
        private Texture2D _cardFrameSelTex;
        private Texture2D _confirmBtnTex;
        private Texture2D _confirmBtnDisabledTex;

        // ── Ciclo de vida ─────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            BuildMasterPool();
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            }
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        private void Start()
        {
            // Suscribirse si el GameManager ya está listo
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;

                // Sincronizar estado inicial
                HandleGameStateChanged(GameManager.Instance.CurrentState);
            }
            else
            {
                // Sin GameManager: permanecer oculto hasta que alguien llame Show()
                _isVisible = false;
            }
        }

        private void OnDestroy()
        {
            DestroyGUITextures();
        }

        // ── Handlers de GameState ─────────────────────────────────────────────────────────────────

        private void HandleGameStateChanged(GameState newState)
        {
            if (!autoManageVisibility) return;

            if (newState == GameState.MasterSelecting)
            {
                Show();
            }
            else if (_isVisible)
            {
                // Otro estado: ocultar sin animación (ya la habrá hecho ConfirmSelection)
                _isVisible = false;
                if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
            }
        }

        // ── API pública ───────────────────────────────────────────────────────────────────────────

        /// <summary>Muestra el panel con animación de fade-in.</summary>
        public void Show()
        {
            if (_masterPool.Count == 0) BuildMasterPool();

            _isVisible      = true;
            // Pre-seleccionar la primera carta para agilidad de juego
            if (_selectedMaster == null && _masterPool.Count > 0)
            {
                _selectedMaster = _masterPool[0];
            }

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeIn());

            // Si hay Canvas uGUI disponible, construir las tarjetas ahí
            if (cardContainer != null)
            {
                BuildUguiCards();
                UpdateConfirmButton(_selectedMaster != null);
            }
        }

        /// <summary>
        /// Oculta el panel con fade-out y confirma la selección en <see cref="GameManager"/>.
        /// </summary>
        public void ConfirmSelection()
        {
            if (_selectedMaster == null && _masterPool.Count > 0)
            {
                _selectedMaster = _masterPool[0];
            }
            if (_selectedMaster == null) return;

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOutAndConfirm());
        }

        // ── Construcción de la Pool de Gran Maestros ──────────────────────────────────────────────

        private void BuildMasterPool()
        {
            _masterPool.Clear();

            // ── Gran Maestro: Tigrán Petrosián ──────────────────────────────────────────────────
            PetrosianCardData petrosian = PetrosianCardData.CreateRuntimeInstance();
            // Asignar ilustración si está disponible en la lista del Inspector
            if (masterIllustrations != null && masterIllustrations.Count > 0 && masterIllustrations[0] != null)
            {
                petrosian.CardSprite = masterIllustrations[0];
            }
            _masterPool.Add(petrosian);

            // ── Espacio reservado para futuros Gran Maestros ─────────────────────────────────────
            // CardData secondMaster = ...; _masterPool.Add(secondMaster);
            // CardData thirdMaster  = ...; _masterPool.Add(thirdMaster);
        }

        // ── Construcción de Cards uGUI ─────────────────────────────────────────────────────────────

        private void BuildUguiCards()
        {
            // Destruir cards anteriores
            foreach (CardView cv in _masterCardViews)
            {
                if (cv != null) Destroy(cv.gameObject);
            }
            _masterCardViews.Clear();

            CardManager cm = FindAnyObjectByType<CardManager>();

            for (int i = 0; i < _masterPool.Count; i++)
            {
                CardData data = _masterPool[i];
                int      idx  = i;    // capturar para la lambda

                GameObject cardObj = new GameObject($"MasterCard_{data.CardName}");
                cardObj.transform.SetParent(cardContainer, false);

                RectTransform rt = cardObj.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(180f, 260f);
                float spacing = 200f;
                rt.anchoredPosition = new Vector2((_masterPool.Count == 1 ? 0f : (idx - (_masterPool.Count - 1) * 0.5f) * spacing), 0f);

                CardView cv = cardObj.AddComponent<CardView>();
                cv.Initialize(data, cm);

                // Asignar ilustración completa si el sprite está disponible
                if (masterIllustrations != null && idx < masterIllustrations.Count && masterIllustrations[idx] != null)
                {
                    cv.SetIllustrationSprite(masterIllustrations[idx]);
                }

                // Animación de entrada
                cv.PlayDrawAnimation();

                // Evento de selección: al pulsar, marca esta carta como seleccionada
                cv.OnCardClicked += (_) => SelectMaster(data, idx);

                _masterCardViews.Add(cv);
            }
        }

        private void SelectMaster(CardData data, int cardIndex)
        {
            _selectedMaster = data;

            // Actualizar estados visuales de todas las cartas
            for (int i = 0; i < _masterCardViews.Count; i++)
            {
                _masterCardViews[i].SetActivated(i == cardIndex);
            }

            UpdateConfirmButton(true);
            Debug.Log($"<color=#FFD700>[MasterSelectionUI] Gran Maestro seleccionado: <b>{data.CardName}</b></color>");
        }

        private void UpdateConfirmButton(bool interactable)
        {
            if (confirmButton != null)
            {
                confirmButton.interactable = interactable;
            }

            if (confirmButtonText != null)
            {
                confirmButtonText.text = interactable
                    ? "✔  CONFIRMAR Y COMENZAR RONDA"
                    : "Selecciona un Gran Maestro...";
                confirmButtonText.color = interactable
                    ? new Color(1f, 0.92f, 0.25f)
                    : new Color(0.55f, 0.60f, 0.65f);
            }
        }

        // ── Corutinas de Fade ─────────────────────────────────────────────────────────────────────

        private IEnumerator FadeIn()
        {
            if (panelCanvasGroup == null) yield break;

            panelCanvasGroup.alpha          = 0f;
            panelCanvasGroup.interactable   = false;
            panelCanvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                panelCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, elapsed / fadeInDuration);
                yield return null;
            }
            panelCanvasGroup.alpha        = 1f;
            panelCanvasGroup.interactable = true;
            _fadeCoroutine = null;
        }

        private IEnumerator FadeOutAndConfirm()
        {
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.interactable   = false;
                panelCanvasGroup.blocksRaycasts = false;

                float startAlpha = panelCanvasGroup.alpha;
                float elapsed    = 0f;

                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
                    yield return null;
                }
                panelCanvasGroup.alpha = 0f;
            }

            _isVisible     = false;
            _fadeCoroutine = null;

            // Notificar al GameManager — él arrancará la partida
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ConfirmMasterSelection(_selectedMaster);
            }
        }

        // ── Renderizado OnGUI Procedural (fallback zero-setup) ────────────────────────────────────
        // Se activa cuando no hay Canvas uGUI asignado en el Inspector.

        private void OnGUI()
        {
            if (!_isVisible || cardContainer != null) return;   // delegamos a Canvas uGUI si existe

            InitOnGuiStylesIfNeeded();

            // Overlay oscuro a pantalla completa
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _overlayTex);

            // Panel centrado
            float pw = Mathf.Min(800f, Screen.width  * 0.92f);
            float ph = Mathf.Min(640f, Screen.height * 0.88f);
            float px = (Screen.width  - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;
            Rect  panelRect = new Rect(px, py, pw, ph);

            GUI.Box(panelRect, GUIContent.none, _panelStyle);

            GUILayout.BeginArea(panelRect);

            // ── Encabezado ────────────────────────────────────────────────────────────
            GUILayout.Space(22f);
            GUILayout.Label("SELECCIÓN DE GRAN MAESTRO", _titleStyle);
            GUILayout.Label("Elige tu mejora mayor inicial — tu estilo de juego para esta partida.", _subtitleStyle);
            GUILayout.Space(18f);

            // ── Tarjeta(s) de Gran Maestro ─────────────────────────────────────────────
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int i = 0; i < _masterPool.Count; i++)
            {
                CardData data       = _masterPool[i];
                bool     isSelected = (_selectedMaster == data);

                DrawOnGuiMasterCard(data, isSelected, i);

                if (i < _masterPool.Count - 1) GUILayout.Space(24f);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // ── Botón Confirmar ────────────────────────────────────────────────────────
            GUILayout.Space(28f);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            bool canConfirm = (_selectedMaster != null);
            GUI.enabled = canConfirm;

            string btnLabel = canConfirm
                ? "✔  CONFIRMAR Y COMENZAR RONDA"
                : "Selecciona un Gran Maestro para continuar...";

            if (GUILayout.Button(btnLabel, canConfirm ? _confirmBtnStyle : _subtitleStyle, GUILayout.Height(48f), GUILayout.Width(380f)))
            {
                ConfirmSelection();
            }

            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(16f);
            GUILayout.EndArea();
        }

        private void DrawOnGuiMasterCard(CardData data, bool isSelected, int index)
        {
            float cw = 220f;
            float ch = 360f;

            GUIStyle frameStyle = isSelected ? _cardFrameSelectedStyle : _cardFrameStyle;

            GUILayout.BeginVertical(frameStyle, GUILayout.Width(cw), GUILayout.Height(ch));

            // Sprite de arte (Inspector o directamente del CardData)
            Sprite spr = (masterIllustrations != null && index < masterIllustrations.Count && masterIllustrations[index] != null)
                         ? masterIllustrations[index]
                         : data.CardSprite;

            if (spr != null)
            {
                Texture2D tex = spr.texture;
                Rect artRect = GUILayoutUtility.GetRect(cw - 20f, 160f);
                GUI.DrawTexture(artRect, tex, ScaleMode.ScaleToFit);
            }
            else
            {
                // Placeholder de color dorado procedural
                Color savedColor = GUI.color;
                GUI.color = new Color(1f, 0.85f, 0.25f, 0.55f);
                GUILayout.Box("🎭", _titleStyle, GUILayout.Width(cw - 20f), GUILayout.Height(160f));
                GUI.color = savedColor;
            }

            GUILayout.Space(8f);

            // Tipo de carta
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.2f);
            GUILayout.Label("GRAN MAESTRO / MEJORA MAYOR", _typeStyle);
            GUI.color = oldColor;

            // Título
            GUILayout.Label(data.CardName, _titleStyle);

            GUILayout.Space(4f);

            // Descripción
            GUILayout.Label(data.Description, _descStyle);

            GUILayout.FlexibleSpace();

            // Indicador de selección
            if (isSelected)
            {
                oldColor = GUI.color;
                GUI.color = new Color(1f, 0.92f, 0.25f);
                GUILayout.Label("✔  SELECCIONADO", _typeStyle);
                GUI.color = oldColor;
            }
            else
            {
                if (GUILayout.Button("Seleccionar", GUILayout.Height(32f)))
                {
                    _selectedMaster = data;
                    Debug.Log($"<color=#FFD700>[MasterSelectionUI] Gran Maestro seleccionado (OnGUI): <b>{data.CardName}</b></color>");
                }
            }

            GUILayout.Space(8f);
            GUILayout.EndVertical();
        }

        // ── Inicialización de estilos OnGUI ───────────────────────────────────────────────────────

        private void InitOnGuiStylesIfNeeded()
        {
            if (_onGuiReady) return;
            _onGuiReady = true;

            // Texturas procedurales
            _overlayTex          = MakeColorTex(new Color(0.04f, 0.05f, 0.08f, 0.88f));
            _panelTex            = MakeColorTex(new Color(0.09f, 0.11f, 0.16f, 0.97f));
            _cardFrameTex        = MakeColorTex(new Color(0.14f, 0.17f, 0.24f, 0.95f));
            _cardFrameSelTex     = MakeColorTex(new Color(0.20f, 0.18f, 0.06f, 0.98f)); // Dorado oscuro
            _confirmBtnTex       = MakeColorTex(new Color(0.80f, 0.65f, 0.08f, 0.92f));
            _confirmBtnDisabledTex = MakeColorTex(new Color(0.25f, 0.28f, 0.32f, 0.75f));

            _overlayStyle = new GUIStyle(GUI.skin.box)
            {
                normal  = { background = _overlayTex },
                border  = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0)
            };

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal  = { background = _panelTex },
                padding = new RectOffset(20, 20, 16, 16)
            };

            _cardFrameStyle = new GUIStyle(GUI.skin.box)
            {
                normal  = { background = _cardFrameTex },
                padding = new RectOffset(10, 10, 10, 10),
                margin  = new RectOffset(0, 0, 0, 0)
            };

            _cardFrameSelectedStyle = new GUIStyle(_cardFrameStyle)
            {
                normal = { background = _cardFrameSelTex }
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize   = 18,
                fontStyle  = FontStyle.Bold,
                alignment  = TextAnchor.MiddleCenter,
                wordWrap   = true,
                normal     = { textColor = new Color(0.95f, 0.96f, 0.98f) }
            };

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize   = 12,
                alignment  = TextAnchor.MiddleCenter,
                wordWrap   = true,
                normal     = { textColor = new Color(0.72f, 0.78f, 0.88f) }
            };

            _typeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize   = 9,
                fontStyle  = FontStyle.Bold,
                alignment  = TextAnchor.MiddleCenter,
                normal     = { textColor = new Color(1f, 0.85f, 0.25f) }
            };

            _descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize   = 11,
                wordWrap   = true,
                alignment  = TextAnchor.UpperCenter,
                normal     = { textColor = new Color(0.85f, 0.88f, 0.94f) }
            };

            _confirmBtnStyle = new GUIStyle(GUI.skin.button)
            {
                normal    = { background = _confirmBtnTex, textColor = Color.white },
                hover     = { background = _confirmBtnTex, textColor = new Color(1f, 0.95f, 0.6f) },
                fontSize  = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private static Texture2D MakeColorTex(Color col)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[4];
            for (int i = 0; i < 4; i++) pixels[i] = col;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void DestroyGUITextures()
        {
            void Safe(Texture2D t) { if (t != null) Destroy(t); }
            Safe(_overlayTex);
            Safe(_panelTex);
            Safe(_cardFrameTex);
            Safe(_cardFrameSelTex);
            Safe(_confirmBtnTex);
            Safe(_confirmBtnDisabledTex);
        }
    }
}

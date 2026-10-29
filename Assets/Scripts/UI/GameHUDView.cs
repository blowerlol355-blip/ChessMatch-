using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Vista del HUD del juego. Se suscribe a los eventos desacoplados de MoveHistory para:
    /// 1. Mostrar un banner animado con alertas de ¡JAQUE!, ¡JAQUE MATE! o REY AHOGADO.
    /// 2. Mostrar un panel/lista en notación algebraica con las jugadas organizadas por turnos ("1. e4 e5", "2. Nf3 Nc6").
    /// Funciona de forma inmediata tanto con OnGUI moderno procedural como con componentes de Canvas uGUI opcionales.
    /// </summary>
    public class GameHUDView : MonoBehaviour
    {
        [Header("Referencias de Tablero")]
        [SerializeField] private BoardView boardView;

        [Header("Configuración de HUD en Pantalla (OnGUI)")]
        [SerializeField] private bool showOnScreenHUD = true;
        [SerializeField] private bool showAnimatedBanner = true;

        [Header("Opcional: Enlaces a Canvas / uGUI")]
        [SerializeField] private CanvasGroup alertCanvasGroup;
        [SerializeField] private UnityEngine.UI.Text alertTitleText;
        [SerializeField] private UnityEngine.UI.Text alertSubtitleText;
        [SerializeField] private UnityEngine.UI.Text moveHistoryText;

        // Estructura interna para almacenar jugadas por turno
        private struct TurnEntry
        {
            public int TurnNumber;
            public string WhiteMove;
            public string BlackMove;
        }

        private readonly List<TurnEntry> _turnEntries = new List<TurnEntry>();
        private MoveHistory _subscribedHistory;

        // Estado del Banner Animado
        private Coroutine _bannerCoroutine;
        private bool _isBannerActive = false;
        private string _bannerTitle = string.Empty;
        private string _bannerSubtitle = string.Empty;
        private Color _bannerColor = Color.white;
        private float _bannerAlpha = 0f;
        private float _bannerScale = 1f;
        private bool _bannerIsPersistent = false;

        // Estado del Escudo de Petrosían
        private bool _petrosianShieldActive = false;

        // Estado de scroll para el historial
        private Vector2 _historyScrollPos;
        private GUIStyle _cardBoxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _moveListStyle;
        private GUIStyle _bannerTitleStyle;
        private GUIStyle _bannerSubtitleStyle;
        private GUIStyle _badgeStyle;
        private Texture2D _cardBgTex;
        private Texture2D _bannerBgTex;
        private Texture2D _badgeBgTex;

        private void Awake()
        {
            if (boardView == null) boardView = GetComponent<BoardView>();
            if (boardView == null) boardView = FindAnyObjectByType<BoardView>();
        }

        private void OnEnable()
        {
            TrySubscribe();
            PetrosianDefenseEffect.OnShieldActivated   += HandlePetrosianShieldActivated;
            PetrosianDefenseEffect.OnShieldDeactivated += HandlePetrosianShieldDeactivated;
        }

        private void Start()
        {
            if (boardView == null) boardView = FindAnyObjectByType<BoardView>();
            TrySubscribe();
        }

        private void Update()
        {
            // Recuperación diferida en caso de que BoardView inicialice su BoardState después de Start()
            if (_subscribedHistory == null)
            {
                if (boardView == null) boardView = FindAnyObjectByType<BoardView>();
                if (boardView != null) TrySubscribe();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            PetrosianDefenseEffect.OnShieldActivated   -= HandlePetrosianShieldActivated;
            PetrosianDefenseEffect.OnShieldDeactivated -= HandlePetrosianShieldDeactivated;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            DestroyCachedTextures();
        }

        /// <summary>
        /// Suscribe el HUD a los eventos de MoveHistory y del tablero.
        /// </summary>
        private void TrySubscribe()
        {
            if (boardView != null)
            {
                boardView.OnGameInitialized -= HandleGameInitialized;
                boardView.OnGameInitialized += HandleGameInitialized;

                if (boardView.BoardState?.History != null)
                {
                    SubscribeToHistory(boardView.BoardState.History);
                }
            }
        }

        private void SubscribeToHistory(MoveHistory history)
        {
            if (_subscribedHistory == history) return;

            Unsubscribe();

            _subscribedHistory = history;
            if (_subscribedHistory != null)
            {
                _subscribedHistory.OnCheck += HandleCheck;
                _subscribedHistory.OnCheckmate += HandleCheckmate;
                _subscribedHistory.OnStalemate += HandleStalemate;
                _subscribedHistory.OnMoveExecuted += HandleMoveExecuted;

                // Reconstruir historial existente si ya contiene jugadas
                RebuildHistoryFromRecords(_subscribedHistory.Records);
            }
        }

        private void Unsubscribe()
        {
            if (boardView != null)
            {
                boardView.OnGameInitialized -= HandleGameInitialized;
            }

            if (_subscribedHistory != null)
            {
                _subscribedHistory.OnCheck -= HandleCheck;
                _subscribedHistory.OnCheckmate -= HandleCheckmate;
                _subscribedHistory.OnStalemate -= HandleStalemate;
                _subscribedHistory.OnMoveExecuted -= HandleMoveExecuted;
                _subscribedHistory = null;
            }
        }

        private void HandleGameInitialized(BoardState state)
        {
            _turnEntries.Clear();
            _isBannerActive = false;
            _bannerIsPersistent = false;
            if (_bannerCoroutine != null) StopCoroutine(_bannerCoroutine);

            if (state?.History != null)
            {
                SubscribeToHistory(state.History);
            }

            UpdateCanvasElements();
        }

        #region Manejo de Eventos de MoveHistory

        private void HandleMoveExecuted(MoveRecord record)
        {
            // Registrar jugada organizada en formato algebraic "1. e4 e5"
            bool isWhite = record.PieceMoved != null && record.PieceMoved.Color == PieceColor.White;

            if (isWhite)
            {
                TurnEntry newTurn = new TurnEntry
                {
                    TurnNumber = _turnEntries.Count + 1,
                    WhiteMove = record.AlgebraicNotation,
                    BlackMove = string.Empty
                };
                _turnEntries.Add(newTurn);
            }
            else
            {
                if (_turnEntries.Count > 0)
                {
                    TurnEntry last = _turnEntries[_turnEntries.Count - 1];
                    last.BlackMove = record.AlgebraicNotation;
                    _turnEntries[_turnEntries.Count - 1] = last;
                }
                else
                {
                    TurnEntry newTurn = new TurnEntry
                    {
                        TurnNumber = 1,
                        WhiteMove = "...",
                        BlackMove = record.AlgebraicNotation
                    };
                    _turnEntries.Add(newTurn);
                }
            }

            // Auto-scroll al final del historial
            _historyScrollPos.y = float.MaxValue;

            // Si la jugada no provocó jaque ni jaque mate, y el banner no era persistente, desvanecerlo
            if (!_bannerIsPersistent && !record.IsCheck && !record.IsCheckmate)
            {
                if (_isBannerActive && _bannerCoroutine != null)
                {
                    StopCoroutine(_bannerCoroutine);
                    StartCoroutine(FadeOutBanner(0.3f));
                }
            }

            UpdateCanvasElements();
        }

        private void HandleCheck(PieceColor checkedColor)
        {
            // Si el escudo de Petrosían está activo, suprimir la alerta de jaque normal
            if (_petrosianShieldActive && checkedColor == PieceColor.White)
            {
                Debug.Log("<color=#FFD700>[GameHUDView] Jaque al Rey Blanco ANULADO por el Escudo de Petrosían.</color>");
                return;
            }

            string colorName = checkedColor == PieceColor.White ? "REY BLANCO" : "REY NEGRO";
            Color alertCol = new Color(1.0f, 0.40f, 0.20f); // Ámbar/Naranja brillante

            TriggerBanner("¡JAQUE!", $"El {colorName} se encuentra bajo ataque directo", alertCol, isPersistent: false, displayTime: 2.2f);
        }

        private void HandleCheckmate(PieceColor matedColor)
        {
            string winner = matedColor == PieceColor.White ? "NEGRAS" : "BLANCAS";
            Color mateCol = new Color(0.95f, 0.15f, 0.20f); // Rojo carmesí

            TriggerBanner("¡JAQUE MATE!", $"Victoria definitiva de las {winner}", mateCol, isPersistent: true);
        }

        private void HandleStalemate(PieceColor stalematedColor)
        {
            string side = stalematedColor == PieceColor.White ? "Blancas" : "Negras";
            Color staleCol = new Color(0.30f, 0.85f, 0.95f); // Celeste

            TriggerBanner("¡REY AHOGADO!", $"Empate por tablas: las {side} no tienen movimientos legales", staleCol, isPersistent: true);
        }

        private void RebuildHistoryFromRecords(IReadOnlyList<MoveRecord> records)
        {
            _turnEntries.Clear();
            if (records == null) return;

            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                bool isWhite = rec.PieceMoved != null && rec.PieceMoved.Color == PieceColor.White;

                if (isWhite)
                {
                    _turnEntries.Add(new TurnEntry
                    {
                        TurnNumber = _turnEntries.Count + 1,
                        WhiteMove = rec.AlgebraicNotation,
                        BlackMove = string.Empty
                    });
                }
                else
                {
                    if (_turnEntries.Count > 0)
                    {
                        TurnEntry last = _turnEntries[_turnEntries.Count - 1];
                        last.BlackMove = rec.AlgebraicNotation;
                        _turnEntries[_turnEntries.Count - 1] = last;
                    }
                    else
                    {
                        _turnEntries.Add(new TurnEntry
                        {
                            TurnNumber = 1,
                            WhiteMove = "...",
                            BlackMove = rec.AlgebraicNotation
                        });
                    }
                }
            }
        }

        // ── Escudo de Petrosían ────────────────────────────────────────────────────────────────────────

        private void HandlePetrosianShieldActivated(BoardCoord kingCoord)
        {
            _petrosianShieldActive = true;

            // Banner arcade dorado: "¡JAQUE ANULADO! - ESCUDO DE PETROSÍAN"
            Color shieldGold = new Color(1f, 0.85f, 0.2f);   // dorado/neón Petrosían
            TriggerBanner(
                title:       "¡JAQUE ANULADO!",
                subtitle:    "Escudo de Petrosían activo — El Rey Blanco está protegido este turno",
                color:       shieldGold,
                isPersistent: false,
                displayTime:  3.5f
            );

            Debug.Log("<color=#FFD700><b>[GameHUDView] Banner del Escudo de Petrosían activado.</b></color>");
        }

        private void HandlePetrosianShieldDeactivated()
        {
            _petrosianShieldActive = false;
            Debug.Log("<color=#FFD700>[GameHUDView] Escudo de Petrosían desactivado.</color>");
        }

        #endregion

        #region Animación del Banner ("¡JAQUE!" / "¡JAQUE MATE!")

        /// <summary>
        /// Activa la animación del banner de alerta con interpolación suave de escala y desvanecimiento de alfa.
        /// </summary>
        public void TriggerBanner(string title, string subtitle, Color color, bool isPersistent = false, float displayTime = 2.2f)
        {
            if (!showAnimatedBanner) return;

            if (_bannerCoroutine != null)
            {
                StopCoroutine(_bannerCoroutine);
            }

            _bannerTitle = title;
            _bannerSubtitle = subtitle;
            _bannerColor = color;
            _bannerIsPersistent = isPersistent;

            _bannerCoroutine = StartCoroutine(AnimateBannerSequence(isPersistent, displayTime));
        }

        private IEnumerator AnimateBannerSequence(bool isPersistent, float displayTime)
        {
            _isBannerActive = true;
            _bannerAlpha = 0f;
            _bannerScale = 0.6f;

            // 1. Fase de Entrada (Scale Punch + Fade In)
            float inDuration = 0.22f;
            float elapsed = 0f;
            while (elapsed < inDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / inDuration);
                // Cubic ease-out con ligero rebote elástico
                float smooth = Mathf.SmoothStep(0f, 1f, t);
                _bannerAlpha = smooth;
                _bannerScale = Mathf.Lerp(0.6f, 1.05f, smooth);
                UpdateCanvasBanner(_bannerAlpha);
                yield return null;
            }

            // Settle a 1.0f
            float settleDuration = 0.08f;
            elapsed = 0f;
            while (elapsed < settleDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / settleDuration);
                _bannerScale = Mathf.Lerp(1.05f, 1.0f, t);
                yield return null;
            }
            _bannerScale = 1.0f;
            _bannerAlpha = 1.0f;
            UpdateCanvasBanner(1f);

            // 2. Fase de Mantenimiento
            if (!isPersistent)
            {
                yield return new WaitForSeconds(displayTime);

                // 3. Fase de Salida (Fade Out suave)
                yield return StartCoroutine(FadeOutBanner(0.35f));
            }
        }

        private IEnumerator FadeOutBanner(float duration)
        {
            float startAlpha = _bannerAlpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = Mathf.SmoothStep(0f, 1f, t);
                _bannerAlpha = Mathf.Lerp(startAlpha, 0f, smooth);
                _bannerScale = Mathf.Lerp(1.0f, 0.9f, smooth);
                UpdateCanvasBanner(_bannerAlpha);
                yield return null;
            }

            _bannerAlpha = 0f;
            _isBannerActive = false;
            UpdateCanvasBanner(0f);
            _bannerCoroutine = null;
        }

        private void UpdateCanvasBanner(float alpha)
        {
            if (alertCanvasGroup != null)
            {
                alertCanvasGroup.alpha = alpha;
                alertCanvasGroup.gameObject.SetActive(alpha > 0.001f);
            }
            if (alertTitleText != null) alertTitleText.text = _bannerTitle;
            if (alertSubtitleText != null) alertSubtitleText.text = _bannerSubtitle;
        }

        private void UpdateCanvasElements()
        {
            if (moveHistoryText != null)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < _turnEntries.Count; i++)
                {
                    var entry = _turnEntries[i];
                    sb.AppendLine($"{entry.TurnNumber}. {entry.WhiteMove,-8} {entry.BlackMove}");
                }
                moveHistoryText.text = sb.ToString();
            }
        }

        #endregion

        #region Renderizado GUI Visual y Estética Moderna

        private void InitStylesIfNeeded()
        {
            if (_cardBoxStyle != null) return;

            // Texturas base procedurales para glassmorphism
            _cardBgTex = MakeColorTex(new Color(0.08f, 0.10f, 0.14f, 0.90f));
            _bannerBgTex = MakeColorTex(new Color(0.06f, 0.07f, 0.10f, 0.94f));
            _badgeBgTex = MakeColorTex(new Color(0.15f, 0.18f, 0.24f, 0.85f));

            _cardBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _cardBgTex },
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.92f, 0.94f, 0.98f) }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(0.80f, 0.84f, 0.90f) }
            };

            _badgeStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _badgeBgTex, textColor = Color.white },
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 3, 3)
            };

            _moveListStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                richText = true,
                normal = { textColor = new Color(0.90f, 0.92f, 0.95f) }
            };

            _bannerTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };

            _bannerSubtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.88f, 0.94f) }
            };
        }

        private void OnGUI()
        {
            if (!showOnScreenHUD) return;

            InitStylesIfNeeded();

            // 1. Dibujar Panel de Historial de Jugadas en Notación Algebraica (Lateral Izquierdo)
            DrawMoveHistoryPanel();

            // 2. Dibujar Banner Animado ("¡JAQUE!" o "¡JAQUE MATE!") Flotante en Pantalla
            if (_isBannerActive && _bannerAlpha > 0.01f)
            {
                DrawAnimatedAlertBanner();
            }
        }

        private void DrawMoveHistoryPanel()
        {
            float panelWidth = 260f;
            float panelHeight = 310f;
            float posX = 16f;
            float posY = 16f;

            Rect panelRect = new Rect(posX, posY, panelWidth, panelHeight);
            GUI.Box(panelRect, GUIContent.none, _cardBoxStyle);

            GUILayout.BeginArea(panelRect);
            GUILayout.Space(8);

            // Encabezado
            GUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUILayout.Label("PARTIDA DE AJEDREZ", _headerStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Banderín de Turno
            string turnName = (boardView != null && boardView.BoardState?.CurrentTurn == PieceColor.White) ? "Blancas" : "Negras";
            Color turnColor = (boardView != null && boardView.BoardState?.CurrentTurn == PieceColor.White) ? Color.white : new Color(0.55f, 0.60f, 0.70f);

            GUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUI.color = turnColor;
            GUILayout.Label($"● Turno actual: <b>{turnName}</b>", _labelStyle);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Título de la tabla de notación
            GUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUILayout.Label("<b>#     BLANCAS       NEGRAS</b>", _labelStyle);
            GUILayout.EndHorizontal();

            // Contenedor scrollable de jugadas en notación algebraica
            Rect scrollArea = new Rect(8, 76, panelWidth - 16, panelHeight - 90);
            float contentHeight = Mathf.Max(_turnEntries.Count * 22f + 10f, scrollArea.height);
            Rect contentRect = new Rect(0, 0, panelWidth - 36, contentHeight);

            _historyScrollPos = GUI.BeginScrollView(scrollArea, _historyScrollPos, contentRect, false, true);

            if (_turnEntries.Count == 0)
            {
                GUI.color = new Color(0.6f, 0.65f, 0.75f);
                GUI.Label(new Rect(10, 10, panelWidth - 50, 24), "(Sin jugadas aún)", _moveListStyle);
                GUI.color = Color.white;
            }
            else
            {
                for (int i = 0; i < _turnEntries.Count; i++)
                {
                    TurnEntry entry = _turnEntries[i];
                    bool isLatestTurn = (i == _turnEntries.Count - 1);
                    float rowY = i * 22f + 4f;

                    // Si es el último movimiento, destacar sutilmente
                    string turnPrefix = $"<color=#88AACC>{entry.TurnNumber}.</color>";
                    string whiteStr = string.IsNullOrEmpty(entry.WhiteMove) ? "—" : entry.WhiteMove;
                    string blackStr = string.IsNullOrEmpty(entry.BlackMove) ? "—" : entry.BlackMove;

                    if (isLatestTurn)
                    {
                        if (string.IsNullOrEmpty(entry.BlackMove))
                            whiteStr = $"<b><color=#FFE066>{whiteStr}</color></b>";
                        else
                            blackStr = $"<b><color=#FFE066>{blackStr}</color></b>";
                    }

                    string rowText = $"{turnPrefix,-14}  {whiteStr,-14}  {blackStr}";
                    GUI.Label(new Rect(8, rowY, panelWidth - 40, 22), rowText, _moveListStyle);
                }
            }

            GUI.EndScrollView();

            GUILayout.EndArea();
        }

        private void DrawAnimatedAlertBanner()
        {
            float baseWidth = 440f;
            float baseHeight = 90f;

            float bannerWidth = baseWidth * _bannerScale;
            float bannerHeight = baseHeight * _bannerScale;

            float centerX = Screen.width * 0.5f;
            float topY = 32f;

            Rect bannerRect = new Rect(centerX - bannerWidth * 0.5f, topY, bannerWidth, bannerHeight);

            // Aplicar alpha acumulado
            Color oldGuiColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _bannerAlpha);

            // 1. Sombra suave de fondo
            Rect shadowRect = new Rect(bannerRect.x + 3, bannerRect.y + 4, bannerRect.width, bannerRect.height);
            GUI.color = new Color(0f, 0f, 0f, _bannerAlpha * 0.45f);
            GUI.Box(shadowRect, GUIContent.none, _cardBoxStyle);

            // 2. Fondo del banner
            GUI.color = new Color(0.07f, 0.08f, 0.12f, _bannerAlpha * 0.95f);
            GUI.Box(bannerRect, GUIContent.none, _cardBoxStyle);

            // 3. Barra de acento con color temático
            Rect accentStripe = new Rect(bannerRect.x, bannerRect.y, bannerRect.width, 4f);
            GUI.color = new Color(_bannerColor.r, _bannerColor.g, _bannerColor.b, _bannerAlpha);
            GUI.Box(accentStripe, GUIContent.none, _cardBoxStyle);

            // 4. Contenido de Texto
            _bannerTitleStyle.normal.textColor = _bannerColor;
            GUI.color = new Color(1f, 1f, 1f, _bannerAlpha);

            float titleHeight = 36f * _bannerScale;
            Rect titleRect = new Rect(bannerRect.x, bannerRect.y + 12f * _bannerScale, bannerRect.width, titleHeight);
            _bannerTitleStyle.fontSize = Mathf.RoundToInt(24 * _bannerScale);
            GUI.Label(titleRect, _bannerTitle, _bannerTitleStyle);

            Rect subRect = new Rect(bannerRect.x, bannerRect.y + 48f * _bannerScale, bannerRect.width, 24f * _bannerScale);
            _bannerSubtitleStyle.fontSize = Mathf.RoundToInt(12 * _bannerScale);
            GUI.Label(subRect, _bannerSubtitle, _bannerSubtitleStyle);

            GUI.color = oldGuiColor;
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

        private void DestroyCachedTextures()
        {
            if (_cardBgTex != null) Destroy(_cardBgTex);
            if (_bannerBgTex != null) Destroy(_bannerBgTex);
            if (_badgeBgTex != null) Destroy(_badgeBgTex);
        }

        #endregion
    }
}

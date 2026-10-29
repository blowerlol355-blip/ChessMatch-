using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChessBalatro
{
    /// <summary>
    /// Gestiona la detección táctil/ratón, la selección de piezas, el despliegue de indicadores
    /// de casillas legales y la orquestación de animaciones de movimiento y captura.
    /// Garantiza el bloqueo de interacción (IsInteractionLocked) durante las transiciones.
    /// </summary>
    [RequireComponent(typeof(BoardView))]
    public class BoardInputHandler : MonoBehaviour
    {
        [Header("Referencias Principales")]
        [SerializeField] private BoardView boardView;
        [SerializeField] private Camera inputCamera;

        [Header("Tiempos de Animación")]
        [SerializeField, Range(0.05f, 0.5f)] private float moveDuration = 0.22f;
        [SerializeField, Range(0.05f, 0.5f)] private float captureDuration = 0.18f;

        [Header("Estilo de Indicadores de Movimiento")]
        [SerializeField] private Color normalMoveIndicatorColor = new Color(0.22f, 0.78f, 0.45f, 0.50f);
        [SerializeField] private Color captureMoveIndicatorColor = new Color(0.92f, 0.28f, 0.28f, 0.65f);
        [SerializeField] private Color selectedOriginColor = new Color(0.96f, 0.90f, 0.38f, 0.85f);
        [SerializeField] private float dotIndicatorScale = 0.32f;
        [SerializeField] private float ringIndicatorScale = 0.75f;

        [Header("Inteligencia Artificial (IA Rival)")]
        [SerializeField] private bool enableAI = true;
        [SerializeField] private PieceColor aiColor = PieceColor.Black;
        [SerializeField] private AIDifficulty aiDifficulty = AIDifficulty.Medium;
        [SerializeField, Range(0.1f, 2.0f)] private float aiThinkingDelay = 0.5f;

        public bool EnableAI
        {
            get => enableAI;
            set => enableAI = value;
        }

        public PieceColor AIColor
        {
            get => aiColor;
            set => aiColor = value;
        }

        public AIDifficulty AIDifficulty
        {
            get => aiDifficulty;
            set
            {
                aiDifficulty = value;
                OnAIDifficultyChanged?.Invoke(value);
            }
        }

        public float AIThinkingDelay
        {
            get => aiThinkingDelay;
            set => aiThinkingDelay = Mathf.Max(0f, value);
        }

        public event Action<AIDifficulty> OnAIDifficultyChanged;
        public event Action OnAITurnStarted;
        public event Action<Move> OnAITurnCompleted;

        // Estado de selección y control
        public bool IsInteractionLocked { get; private set; } = false;
        public BoardCoord? SelectedCoord => _selectedCoord;

        private BoardCoord? _selectedCoord = null;
        private readonly List<Move> _legalMoves = new List<Move>();

        // Contenedor y pool de indicadores visuales (Puntos / Marcos)
        private Transform _markersContainer;
        private readonly List<GameObject> _indicatorPool = new List<GameObject>();

        private static Sprite _cachedDotSprite;
        private static Sprite _cachedRingSprite;

        public event Action<BoardCoord> OnPieceSelected;
        public event Action OnSelectionCleared;
        public event Action<Move> OnMoveStarted;
        public event Action<Move> OnMoveCompleted;

        private int _lastProcessedFrame = -1;
        private BoardCoord? _lastProcessedCoord = null;

        private void Awake()
        {
            if (boardView == null) boardView = GetComponent<BoardView>();
            if (inputCamera == null) inputCamera = Camera.main;

            EnsureMarkersContainer();
        }

        private void Start()
        {
            if (inputCamera == null) inputCamera = Camera.main;
        }

        private void OnDisable()
        {
            IsInteractionLocked = false;
            ClearSelectionVisuals();
            _selectedCoord = null;
            _legalMoves.Clear();
        }

        private void Update()
        {
            // Bloquear interacción si el juego no está en estado activo de juego (Playing)
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing) return;

            // Requisito 2c: Bloquear cualquier selección o interacción si una pieza está animando
            if (IsInteractionLocked) return;
            if (boardView == null || boardView.BoardState == null) return;

            DetectPointerInput();
        }

        /// <summary>
        /// Detecta toques en pantallas móviles o clics de ratón mediante Raycast 2D y conversión de coordenadas.
        /// Compatible tanto con el New Input System (Unity 6 / InputSystem package) como con el Legacy Input Manager.
        /// </summary>
        private void DetectPointerInput()
        {
            bool hasPointerDown = false;
            Vector3 screenPos = Vector3.zero;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                Vector2 touchPos = UnityEngine.InputSystem.Touchscreen.current.primaryTouch.position.ReadValue();
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }
                hasPointerDown = true;
                screenPos = touchPos;
            }
            else if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }
                hasPointerDown = true;
                screenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            }
#endif

            // Fallback a Legacy Input Manager si no se detectó por el nuevo sistema
            if (!hasPointerDown)
            {
                try
                {
                    if (Input.touchCount > 0)
                    {
                        Touch touch = Input.GetTouch(0);
                        if (touch.phase == TouchPhase.Began)
                        {
                            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                            {
                                return;
                            }
                            hasPointerDown = true;
                            screenPos = touch.position;
                        }
                    }
                    else if (Input.GetMouseButtonDown(0))
                    {
                        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                        {
                            return;
                        }
                        hasPointerDown = true;
                        screenPos = Input.mousePosition;
                    }
                }
                catch (System.InvalidOperationException)
                {
                    // Protegido si el proyecto tiene configurado exclusivamente el New Input System
                }
            }

            if (!hasPointerDown) return;

            Camera cam = inputCamera != null ? inputCamera : Camera.main;
            if (cam == null) return;

            float distanceToPlane = Mathf.Abs(cam.transform.position.z - transform.position.z);
            Vector3 screenWithDistance = new Vector3(screenPos.x, screenPos.y, distanceToPlane);
            Vector3 worldPos = cam.ScreenToWorldPoint(screenWithDistance);
            worldPos.z = 0f;

            // 1. Detección por Raycast 2D (Colliders en PieceView o TileView)
            Collider2D hit = Physics2D.OverlapPoint(worldPos);
            if (hit != null)
            {
                PieceView pv = hit.GetComponent<PieceView>();
                if (pv != null)
                {
                    ProcessSquareSelection(pv.Coord);
                    return;
                }

                TileView tv = hit.GetComponent<TileView>();
                if (tv != null)
                {
                    ProcessSquareSelection(tv.Coord);
                    return;
                }
            }

            // 2. Detección por cálculo geométrico de casilla en el tablero
            if (boardView.WorldPositionToCoord(worldPos, out BoardCoord gridCoord))
            {
                ProcessSquareSelection(gridCoord);
            }
        }

        /// <summary>
        /// Flujo de interacción unificado para selección de piezas y ejecución de jugadas legales.
        /// </summary>
        public void ProcessSquareSelection(BoardCoord clickedCoord)
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing) return;
            if (IsInteractionLocked) return;
            if (boardView == null || boardView.BoardState == null) return;

            // Evitar doble despacho en el mismo fotograma entre Update() y eventos OnMouseDown()
            if (Time.frameCount == _lastProcessedFrame && _lastProcessedCoord.HasValue && _lastProcessedCoord.Value.Equals(clickedCoord))
            {
                return;
            }
            _lastProcessedFrame = Time.frameCount;
            _lastProcessedCoord = clickedCoord;

            BoardState state = boardView.BoardState;

            // Flujo b: Si ya había una pieza seleccionada y se toca una casilla legal marcada, ejecutar jugada
            if (_selectedCoord.HasValue)
            {
                Move targetMove = _legalMoves.Find(m => m.To == clickedCoord);
                if (targetMove != null)
                {
                    StartCoroutine(ExecuteMoveRoutine(targetMove));
                    return;
                }
            }

            // Flujo a: Al tocar una pieza del bando en turno, seleccionarla y mostrar casillas legales
            Piece piece = state[clickedCoord];
            if (piece != null && piece.Color == state.CurrentTurn)
            {
                SelectPiece(clickedCoord);
            }
            else
            {
                // Toque en casilla vacía o enemiga no válida -> Deseleccionar
                Deselect();
            }
        }

        /// <summary>
        /// Recalcula y refresca visualmente los movimientos legales de la pieza actualmente seleccionada.
        /// Fundamental al activar o desactivar cartas de efecto que alteren las reglas en mitad del turno.
        /// </summary>
        public void RefreshLegalMoves()
        {
            if (_selectedCoord.HasValue && boardView != null && boardView.BoardState != null)
            {
                SelectPiece(_selectedCoord.Value);
            }
        }

        /// <summary>
        /// Selecciona una pieza, ilumina su casilla de origen e instancia/marca los indicadores de movimiento legal.
        /// </summary>
        public void SelectPiece(BoardCoord coord)
        {
            ClearSelectionVisuals();

            _selectedCoord = coord;
            boardView.HighlightSquare(coord, selectedOriginColor);

            // Obtener todos los movimientos legales según reglas de ajedrez y jaque
            _legalMoves.Clear();
            _legalMoves.AddRange(MoveValidator.GetLegalMoves(boardView.BoardState, coord));

            // Instanciar / desplegar indicadores semitransparentes en cada casilla destino
            SpawnMoveIndicators(_legalMoves);

            OnPieceSelected?.Invoke(coord);
        }

        /// <summary>
        /// Limpia la selección activa y oculta los indicadores.
        /// </summary>
        public void Deselect()
        {
            ClearSelectionVisuals();
            _selectedCoord = null;
            _legalMoves.Clear();

            boardView.ClearHighlights();
            OnSelectionCleared?.Invoke();
        }

        /// <summary>
        /// Corutina que orquesta el deslizamiento suave, animaciones de captura, actualización de BoardState
        /// y registro en MoveHistory bloqueando la interacción durante el proceso.
        /// </summary>
        private IEnumerator ExecuteMoveRoutine(Move move)
        {
            // Requisito 2c: Bloquear toda interacción
            IsInteractionLocked = true;
            OnMoveStarted?.Invoke(move);

            ClearSelectionVisuals();
            boardView.ClearHighlights();

            BoardCoord from = move.From;
            BoardCoord to = move.To;

            PieceView movingPieceView = boardView.GetPieceView(from);
            Vector3 targetWorldPos = boardView.CoordToWorldPosition(to);

            // Detección de pieza capturada (captura ordinaria o peón al paso)
            BoardCoord capturedCoord = move.IsEnPassant
                ? new BoardCoord(to.File, from.Rank)
                : to;

            PieceView capturedPieceView = boardView.GetPieceView(capturedCoord);

            // 1. Iniciar animación de captura si corresponde (desvanecimiento y escala reducida)
            if (capturedPieceView != null)
            {
                capturedPieceView.CaptureAndDestroy(captureDuration);
                boardView.ClearPieceViewAt(capturedCoord);
            }

            // 2. Desplazamiento animado de la pieza (y torre simultáneamente si es enroque)
            Coroutine rookCoroutine = null;
            PieceView rookView = null;
            BoardCoord rookFrom = default;
            BoardCoord rookTo = default;

            if (move.IsCastling)
            {
                int rank = from.Rank;
                if (to.File == 6) // Enroque corto (torre de h a f)
                {
                    rookFrom = new BoardCoord(7, rank);
                    rookTo = new BoardCoord(5, rank);
                }
                else // Enroque largo (torre de a a d)
                {
                    rookFrom = new BoardCoord(0, rank);
                    rookTo = new BoardCoord(3, rank);
                }

                rookView = boardView.GetPieceView(rookFrom);
                if (rookView != null)
                {
                    boardView.ClearPieceViewAt(rookFrom);
                    Vector3 rookTargetPos = boardView.CoordToWorldPosition(rookTo);
                    rookCoroutine = StartCoroutine(rookView.AnimateMoveTo(rookTargetPos, moveDuration * 0.95f));
                }
            }

            if (movingPieceView != null)
            {
                yield return StartCoroutine(movingPieceView.AnimateMoveTo(targetWorldPos, moveDuration));
                movingPieceView.UpdateCoord(to);
            }

            if (rookCoroutine != null)
            {
                yield return rookCoroutine;
                if (rookView != null)
                {
                    rookView.UpdateCoord(rookTo);
                    boardView.SetPieceView(rookTo, rookView);
                }
            }

            // 3. Actualizar posiciones internas de piezas en la vista
            if (movingPieceView != null)
            {
                boardView.ClearPieceViewAt(from);
                boardView.SetPieceView(to, movingPieceView);
            }

            // 4. Aplicar jugada a nivel lógico matemático (BoardState actualiza turno y MoveHistory registra)
            bool moveSuccess = boardView.BoardState.MakeMove(move);

            // 5. Si hubo coronación de peón, actualizar el aspecto visual de la pieza coronada
            if (moveSuccess && move.PromotionType != PieceType.None && movingPieceView != null)
            {
                Piece promotedPiece = boardView.BoardState[to];
                if (promotedPiece != null)
                {
                    movingPieceView.Initialize(promotedPiece, to, targetWorldPos, boardView.PieceTheme);
                }
            }

            // Refrescar resaltados de jaque / mate en los reyes
            boardView.UpdateCheckHighlight();

            _selectedCoord = null;
            _legalMoves.Clear();

            // Pequeña pausa de seguridad antes de liberar interacción
            yield return new WaitForSeconds(0.04f);

            boardView?.NotifyMoveExecuted(move);
            OnMoveCompleted?.Invoke(move);

            // Requisito 2: Al finalizar el movimiento, si el turno pasa a las Negras (IA), ejecutar su turno automáticamente
            if (enableAI && boardView != null && boardView.BoardState != null &&
                boardView.BoardState.CurrentTurn == aiColor &&
                !boardView.BoardState.IsCheckmate(aiColor) &&
                !boardView.BoardState.IsStalemate(aiColor))
            {
                yield return StartCoroutine(ExecuteAITurn());
            }
            else
            {
                IsInteractionLocked = false;
            }
        }

        /// <summary>
        /// Corutina que gestiona el turno de la Inteligencia Artificial rival:
        /// 1. Bloquea temporalmente el input del jugador humano.
        /// 2. Aplica una pausa intencional de 0.5 segundos para simular "pensamiento".
        /// 3. Calcula la mejor jugada con ChessAI.GetBestMove en segundo plano (Task.Run) para no congelar Unity.
        /// 4. Ejecuta la animación suave de deslizamiento en la PieceView seleccionada.
        /// 5. Actualiza BoardState, registra en MoveHistory y devuelve el control al jugador blanco.
        /// </summary>
        public IEnumerator ExecuteAITurn()
        {
            IsInteractionLocked = true;
            OnAITurnStarted?.Invoke();

            // Pausa intencional de 0.5s para simular "pensamiento"
            if (aiThinkingDelay > 0f)
            {
                yield return new WaitForSeconds(aiThinkingDelay);
            }

            if (boardView == null || boardView.BoardState == null)
            {
                IsInteractionLocked = false;
                yield break;
            }

            BoardState state = boardView.BoardState;
            if (state.CurrentTurn != aiColor)
            {
                IsInteractionLocked = false;
                yield break;
            }

            // Clonar estado para evaluación en hilo secundario evitando congelar el hilo principal de Unity
            BoardState stateClone = state.Clone();
            PieceColor currentAIColor = aiColor;
            AIDifficulty currentDiff = aiDifficulty;
            (BoardCoord from, BoardCoord to)? bestCoords = null;

            System.Threading.Tasks.Task computeTask = System.Threading.Tasks.Task.Run(() =>
            {
                bestCoords = ChessAI.GetBestMove(stateClone, currentAIColor, currentDiff);
            });

            while (!computeTask.IsCompleted)
            {
                yield return null;
            }

            if (!bestCoords.HasValue)
            {
                // Sin movimientos legales (Jaque Mate o Rey Ahogado)
                IsInteractionLocked = false;
                yield break;
            }

            // Obtener el objeto Move legal correspondiente a las coordenadas calculadas
            List<Move> legalMoves = MoveValidator.GetLegalMoves(state, bestCoords.Value.from);
            Move aiMove = legalMoves.Find(m => m.To == bestCoords.Value.to);

            if (aiMove == null)
            {
                List<Move> allMoves = MoveValidator.GetAllLegalMoves(state, currentAIColor);
                aiMove = allMoves.Find(m => m.From == bestCoords.Value.from && m.To == bestCoords.Value.to);
            }

            if (aiMove != null)
            {
                // Ejecuta la animación suave de deslizamiento y actualización completa
                yield return StartCoroutine(ExecuteMoveRoutine(aiMove));
                OnAITurnCompleted?.Invoke(aiMove);
            }
            else
            {
                IsInteractionLocked = false;
            }
        }

        /// <summary>
        /// Permite forzar o desencadenar manualmente el turno de la IA si es su turno.
        /// </summary>
        public void TriggerAITurn()
        {
            if (!IsInteractionLocked && boardView != null && boardView.BoardState != null &&
                boardView.BoardState.CurrentTurn == aiColor &&
                !boardView.BoardState.IsCheckmate(aiColor) &&
                !boardView.BoardState.IsStalemate(aiColor))
            {
                StartCoroutine(ExecuteAITurn());
            }
        }

        #region Indicadores Visuales Procedurales (Puntos y Marcos)

        private void EnsureMarkersContainer()
        {
            if (_markersContainer == null)
            {
                Transform existing = transform.Find("MoveIndicatorsContainer");
                if (existing != null)
                {
                    _markersContainer = existing;
                }
                else
                {
                    GameObject containerObj = new GameObject("MoveIndicatorsContainer");
                    containerObj.transform.SetParent(transform);
                    containerObj.transform.localPosition = Vector3.zero;
                    _markersContainer = containerObj.transform;
                }
            }
        }

        private void SpawnMoveIndicators(IEnumerable<Move> moves)
        {
            EnsureMarkersContainer();
            int index = 0;
            HashSet<BoardCoord> markedCoords = new HashSet<BoardCoord>();

            foreach (Move move in moves)
            {
                // Evitar duplicar indicadores si hay múltiples promociones para la misma casilla
                if (!markedCoords.Add(move.To)) continue;

                GameObject indicator = GetOrCreateIndicator(index);
                indicator.transform.position = boardView.CoordToWorldPosition(move.To);

                SpriteRenderer sr = indicator.GetComponent<SpriteRenderer>();

                bool isCapture = move.PieceCaptured != null || move.IsEnPassant || boardView.BoardState[move.To] != null;

                if (isCapture)
                {
                    // Marco o anillo semitransparente para capturas
                    sr.sprite = GetOrCreateRingSprite();
                    sr.color = captureMoveIndicatorColor;
                    indicator.transform.localScale = Vector3.one * ringIndicatorScale;
                }
                else
                {
                    // Punto / disco suave semitransparente para casillas libres
                    sr.sprite = GetOrCreateDotSprite();
                    sr.color = normalMoveIndicatorColor;
                    indicator.transform.localScale = Vector3.one * dotIndicatorScale;
                }

                indicator.SetActive(true);
                index++;
            }

            // Ocultar indicadores sobrantes del pool
            for (int i = index; i < _indicatorPool.Count; i++)
            {
                _indicatorPool[i].SetActive(false);
            }
        }

        private void ClearSelectionVisuals()
        {
            for (int i = 0; i < _indicatorPool.Count; i++)
            {
                if (_indicatorPool[i] != null)
                {
                    _indicatorPool[i].SetActive(false);
                }
            }
        }

        private GameObject GetOrCreateIndicator(int index)
        {
            while (index >= _indicatorPool.Count)
            {
                GameObject indicatorObj = new GameObject($"Indicator_{_indicatorPool.Count}");
                indicatorObj.transform.SetParent(_markersContainer);

                SpriteRenderer sr = indicatorObj.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 4; // Por encima de las casillas (0) y por debajo de las piezas en movimiento (20)

                indicatorObj.SetActive(false);
                _indicatorPool.Add(indicatorObj);
            }

            return _indicatorPool[index];
        }

        /// <summary>
        /// Genera y cachea un sprite de disco suave (punto) para casillas de movimiento ordinario.
        /// </summary>
        private static Sprite GetOrCreateDotSprite()
        {
            if (_cachedDotSprite != null) return _cachedDotSprite;

            int size = 64;
            float radius = (size - 2) * 0.5f;
            float center = size * 0.5f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                    if (dist > radius + 1f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (dist > radius - 1.5f)
                    {
                        float alpha = Mathf.Clamp01(radius + 1f - dist);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.white);
                    }
                }
            }

            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();

            _cachedDotSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cachedDotSprite;
        }

        /// <summary>
        /// Genera y cachea un sprite de anillo circular para casillas de captura.
        /// </summary>
        private static Sprite GetOrCreateRingSprite()
        {
            if (_cachedRingSprite != null) return _cachedRingSprite;

            int size = 96;
            float outerRadius = (size - 4) * 0.5f;
            float innerRadius = outerRadius * 0.70f;
            float center = size * 0.5f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                    if (dist > outerRadius + 1f || dist < innerRadius - 1f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else
                    {
                        float alpha = 1f;
                        if (dist > outerRadius - 1.5f)
                        {
                            alpha = Mathf.Clamp01(outerRadius + 1f - dist);
                        }
                        else if (dist < innerRadius + 1.5f)
                        {
                            alpha = Mathf.Clamp01(dist - (innerRadius - 1f));
                        }
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }
            }

            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();

            _cachedRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cachedRingSprite;
        }

        #endregion
    }
}

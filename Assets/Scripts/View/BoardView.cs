using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Genera y administra la vista procedural 8x8 del tablero de ajedrez y sus piezas en Unity.
    /// Soporta interacción táctil/ratón, selección y visualización de movimientos legales.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [Header("Configuración Visual")]
        [SerializeField] private float tileSize = 1.0f;
        [SerializeField] private Sprite squareSprite;
        [SerializeField] private Color lightSquareColor = new Color(0.93f, 0.93f, 0.82f, 1.0f); // #EEEED2
        [SerializeField] private Color darkSquareColor = new Color(0.46f, 0.58f, 0.33f, 1.0f);  // #769656

        [Header("Colores de Resaltado")]
        [SerializeField] private Color selectedColor = new Color(0.96f, 0.90f, 0.38f, 1.0f);    // Amarillo
        [SerializeField] private Color validMoveColor = new Color(0.50f, 0.78f, 0.90f, 1.0f);   // Celeste
        [SerializeField] private Color attackMoveColor = new Color(0.90f, 0.38f, 0.38f, 1.0f);  // Rojo
        [SerializeField] private Color checkColor = new Color(1.0f, 0.25f, 0.25f, 1.0f);        // Rojo brillante Jaque
        [SerializeField] private Color checkmateColor = new Color(0.75f, 0.05f, 0.05f, 1.0f);   // Rojo carmesí Mate

        [Header("HUD Visual en Pantalla")]
        [SerializeField] private bool showOnScreenHUD = true;

        [Header("Estado de Partida (Inspector Live Debug)")]
        [SerializeField] private string currentTurnDisplay = "Blancas";
        [SerializeField] private bool isWhiteInCheck = false;
        [SerializeField] private bool isBlackInCheck = false;
        [SerializeField] private bool isCheckmate = false;
        [SerializeField] private bool isStalemate = false;
        [SerializeField] private string lastMoveNotation = "Ninguna";
        [SerializeField, TextArea(3, 8)] private string pgnHistory = "";

        [Header("Generación")]
        [SerializeField] private bool autoGenerateOnStart = true;
        [SerializeField] private PieceTheme pieceTheme;
        [SerializeField] private Transform tilesContainer;
        [SerializeField] private Transform piecesContainer;

        [Header("Control de Entrada")]
        [SerializeField] private BoardInputHandler inputHandler;

        private readonly TileView[,] _tileViews = new TileView[BoardState.BoardSize, BoardState.BoardSize];
        private readonly PieceView[,] _pieceViews = new PieceView[BoardState.BoardSize, BoardState.BoardSize];
        private static Sprite _defaultWhiteSprite;

        public BoardState BoardState { get; private set; }
        public PieceTheme PieceTheme => pieceTheme;
        public float TileSize => tileSize;
        public BoardInputHandler InputHandler => inputHandler;

        private BoardCoord? _selectedCoord = null;
        private List<Move> _currentLegalMoves = new List<Move>();

        public event Action<BoardCoord> OnSquareClicked;
        public event Action<Move> OnMoveExecuted;
        public event Action<BoardState> OnGameInitialized;

        private void Awake()
        {
            if (inputHandler == null) inputHandler = GetComponent<BoardInputHandler>();
            if (inputHandler == null) inputHandler = gameObject.AddComponent<BoardInputHandler>();

            if (GetComponent<GameHUDView>() == null && FindAnyObjectByType<GameHUDView>() == null)
            {
                gameObject.AddComponent<GameHUDView>();
            }

            if (GetComponent<CardManager>() == null && FindAnyObjectByType<CardManager>() == null)
            {
                gameObject.AddComponent<CardManager>();
            }

            if (GetComponent<DifficultySelectorUI>() == null && FindAnyObjectByType<DifficultySelectorUI>() == null)
            {
                gameObject.AddComponent<DifficultySelectorUI>();
            }

            if (GetComponent<GameManager>() == null && FindAnyObjectByType<GameManager>() == null)
            {
                gameObject.AddComponent<GameManager>();
            }

            if (GetComponent<MainMenuController>() == null && FindAnyObjectByType<MainMenuController>() == null)
            {
                gameObject.AddComponent<MainMenuController>();
            }

            if (GetComponent<MasterSelectionUI>() == null && FindAnyObjectByType<MasterSelectionUI>() == null)
            {
                gameObject.AddComponent<MasterSelectionUI>();
            }
        }

        private void Start()
        {
            if (autoGenerateOnStart)
            {
                InitializeGame();
            }
        }

        [ContextMenu("Run Core Logic Tests")]
        public void RunCoreLogicTests()
        {
            var results = CoreChessTests.RunAllTests();
            int passed = 0;
            foreach (var r in results)
            {
                if (r.Passed)
                {
                    passed++;
                    Debug.Log($"<color=green>[PASS]</color> {r.Name}");
                }
                else
                {
                    Debug.LogError($"<color=red>[FAIL]</color> {r.Name} -> {r.ErrorMessage}");
                }
            }
            Debug.Log($"<color={(passed == results.Count ? "lime" : "yellow")}><b>Resumen Pruebas Core: {passed}/{results.Count} superadas exitosamente.</b></color>");
        }

        [ContextMenu("Initialize Game")]
        public void InitializeGame()
        {
            GenerateBoard();

            if (BoardState == null)
            {
                BoardState = new BoardState();
            }

            // Desuscribir anteriores para evitar duplicados
            BoardState.OnCheck -= HandleOnCheck;
            BoardState.OnCheckmate -= HandleOnCheckmate;
            BoardState.OnStalemate -= HandleOnStalemate;
            BoardState.OnMoveExecuted -= HandleOnCoreMoveExecuted;

            BoardState.OnCheck += HandleOnCheck;
            BoardState.OnCheckmate += HandleOnCheckmate;
            BoardState.OnStalemate += HandleOnStalemate;
            BoardState.OnMoveExecuted += HandleOnCoreMoveExecuted;

            BoardState.InitializeStandardBoard();

            isWhiteInCheck = false;
            isBlackInCheck = false;
            isCheckmate = false;
            isStalemate = false;
            lastMoveNotation = "Ninguna";
            pgnHistory = "";
            currentTurnDisplay = "Blancas";

            SpawnAllPieces();
            UpdateCheckHighlight();
            OnGameInitialized?.Invoke(BoardState);
        }

        private void HandleOnCheck(PieceColor checkedColor)
        {
            if (checkedColor == PieceColor.White) isWhiteInCheck = true;
            else isBlackInCheck = true;
            UpdateCheckHighlight();
            Debug.Log($"<color=yellow><b>¡JAQUE!</b> El rey {checkedColor} está bajo ataque.</color>");
        }

        private void HandleOnCheckmate(PieceColor matedColor)
        {
            isCheckmate = true;
            UpdateCheckHighlight();
            Debug.Log($"<color=red><b>¡JAQUE MATE!</b> El bando {matedColor} ha sido derrotado.</color>");
        }

        private void HandleOnStalemate(PieceColor stalematedColor)
        {
            isStalemate = true;
            Debug.Log($"<color=cyan><b>¡REY AHOGADO (Tablas)!</b> El bando {stalematedColor} no tiene movimientos legales.</color>");
        }

        private void HandleOnCoreMoveExecuted(MoveRecord record)
        {
            lastMoveNotation = record.AlgebraicNotation;
            currentTurnDisplay = BoardState.CurrentTurn == PieceColor.White ? "Blancas" : "Negras";
            isWhiteInCheck = BoardState.IsInCheck(PieceColor.White);
            isBlackInCheck = BoardState.IsInCheck(PieceColor.Black);
            isCheckmate = record.isCheckmate;
            RebuildPgnDisplay();
            UpdateCheckHighlight();
            Debug.Log($"<color=white>[Turno {BoardState.FullmoveNumber}] <b>{record.AlgebraicNotation}</b> ({record.pieceMoved?.Color} {record.pieceMoved?.Type})</color>");
        }

        private void RebuildPgnDisplay()
        {
            if (BoardState?.History == null) return;
            var records = BoardState.History.Records;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < records.Count; i++)
            {
                if (i % 2 == 0)
                {
                    sb.Append($"{(i / 2) + 1}. ");
                }
                sb.Append(records[i].AlgebraicNotation);
                sb.Append("  ");
            }
            pgnHistory = sb.ToString().TrimEnd();
        }

        public void UpdateCheckHighlight()
        {
            if (BoardState == null) return;

            if (isCheckmate)
            {
                PieceColor matedColor = BoardState.CurrentTurn;
                BoardCoord? k = BoardState.FindKing(matedColor);
                if (k.HasValue) HighlightSquare(k.Value, checkmateColor);
                return;
            }

            if (BoardState.IsInCheck(PieceColor.White))
            {
                BoardCoord? wk = BoardState.FindKing(PieceColor.White);
                if (wk.HasValue) HighlightSquare(wk.Value, checkColor);
            }

            if (BoardState.IsInCheck(PieceColor.Black))
            {
                BoardCoord? bk = BoardState.FindKing(PieceColor.Black);
                if (bk.HasValue) HighlightSquare(bk.Value, checkColor);
            }
        }

        [ContextMenu("Generate Board")]
        public void GenerateBoard()
        {
            ClearBoard();

            if (tilesContainer == null)
            {
                GameObject containerObj = new GameObject("TilesContainer");
                containerObj.transform.SetParent(transform);
                containerObj.transform.localPosition = Vector3.zero;
                tilesContainer = containerObj.transform;
            }

            Sprite tileSpriteToUse = squareSprite != null ? squareSprite : GetOrCreateProceduralSprite();

            for (int file = 0; file < BoardState.BoardSize; file++)
            {
                for (int rank = 0; rank < BoardState.BoardSize; rank++)
                {
                    BoardCoord coord = new BoardCoord(file, rank);
                    bool isLight = (file + rank) % 2 != 0;
                    Color baseColor = isLight ? lightSquareColor : darkSquareColor;

                    Vector3 worldPos = CoordToWorldPosition(coord);

                    GameObject tileObj = new GameObject($"Tile_{coord.ToAlgebraic()} ({file},{rank})");
                    tileObj.transform.SetParent(tilesContainer);
                    tileObj.transform.position = worldPos;

                    TileView tileView = tileObj.AddComponent<TileView>();
                    tileView.Initialize(coord, baseColor, tileSpriteToUse, tileSize);

                    tileView.OnTileClicked += HandleTileOrPieceClicked;
                    _tileViews[file, rank] = tileView;
                }
            }
        }

        public void SpawnAllPieces()
        {
            ClearPieces();

            if (piecesContainer == null)
            {
                GameObject pContainer = new GameObject("PiecesContainer");
                pContainer.transform.SetParent(transform);
                pContainer.transform.localPosition = Vector3.zero;
                piecesContainer = pContainer.transform;
            }

            if (BoardState == null) return;

            for (int f = 0; f < BoardState.BoardSize; f++)
            {
                for (int r = 0; r < BoardState.BoardSize; r++)
                {
                    Piece piece = BoardState[f, r];
                    if (piece != null)
                    {
                        BoardCoord coord = new BoardCoord(f, r);
                        SpawnPieceView(piece, coord);
                    }
                }
            }
        }

        private PieceView SpawnPieceView(Piece piece, BoardCoord coord)
        {
            Vector3 worldPos = CoordToWorldPosition(coord);
            GameObject pieceObj = new GameObject($"Piece_{piece.Color}_{piece.Type}_{coord.ToAlgebraic()}");
            pieceObj.transform.SetParent(piecesContainer);

            PieceView pieceView = pieceObj.AddComponent<PieceView>();
            pieceView.Initialize(piece, coord, worldPos, pieceTheme);
            pieceView.OnPieceClicked += HandlePieceClicked;

            _pieceViews[coord.File, coord.Rank] = pieceView;
            return pieceView;
        }

        private void HandlePieceClicked(PieceView pieceView)
        {
            HandleTileOrPieceClicked(pieceView.Coord);
        }

        private void HandleTileOrPieceClicked(TileView tileView)
        {
            HandleTileOrPieceClicked(tileView.Coord);
        }

        public void HandleTileOrPieceClicked(BoardCoord clickedCoord)
        {
            OnSquareClicked?.Invoke(clickedCoord);

            // Si el controlador táctil y de animaciones está activo, delegar la interacción
            if (inputHandler != null)
            {
                inputHandler.ProcessSquareSelection(clickedCoord);
                return;
            }

            // Si hay una casilla seleccionada previamente y se hace clic en un movimiento legal
            if (_selectedCoord.HasValue)
            {
                Move matchedMove = _currentLegalMoves.Find(m => m.To == clickedCoord);
                if (matchedMove != null)
                {
                    ExecuteMove(matchedMove);
                    return;
                }
            }

            // Si se hace clic en una pieza del turno actual, se selecciona
            Piece clickedPiece = BoardState?[clickedCoord];
            if (clickedPiece != null && clickedPiece.Color == BoardState.CurrentTurn)
            {
                SelectPiece(clickedCoord);
            }
            else
            {
                DeselectCurrent();
            }
        }

        private void SelectPiece(BoardCoord coord)
        {
            ClearHighlights();
            _selectedCoord = coord;

            SelectSquare(coord);

            _currentLegalMoves = MoveValidator.GetLegalMoves(BoardState, coord);
            HighlightLegalMoves(_currentLegalMoves);
        }

        private void DeselectCurrent()
        {
            ClearHighlights();
            _selectedCoord = null;
            _currentLegalMoves.Clear();
        }

        private void ExecuteMove(Move move)
        {
            BoardCoord from = move.From;
            BoardCoord to = move.To;

            // Mover en la lógica
            bool success = BoardState.MakeMove(move);
            if (!success) return;

            // Actualizar vista de piezas
            PieceView movingPieceView = _pieceViews[from.File, from.Rank];

            // Si es captura en-passant, destruir el peón capturado en la casilla adyacente
            if (move.IsEnPassant)
            {
                int capturedRank = from.Rank;
                PieceView epCaptured = _pieceViews[to.File, capturedRank];
                if (epCaptured != null)
                {
                    epCaptured.DestroySelf();
                    _pieceViews[to.File, capturedRank] = null;
                }
            }
            else
            {
                // Captura ordinaria
                PieceView capturedView = _pieceViews[to.File, to.Rank];
                if (capturedView != null)
                {
                    capturedView.DestroySelf();
                    _pieceViews[to.File, to.Rank] = null;
                }
            }

            // Si es enroque, mover también la torre correspondiente
            if (move.IsCastling)
            {
                int rank = from.Rank;
                if (to.File == 6) // Corto
                {
                    PieceView rookView = _pieceViews[7, rank];
                    if (rookView != null)
                    {
                        rookView.SetPosition(new BoardCoord(5, rank), CoordToWorldPosition(new BoardCoord(5, rank)));
                        _pieceViews[5, rank] = rookView;
                        _pieceViews[7, rank] = null;
                    }
                }
                else if (to.File == 2) // Largo
                {
                    PieceView rookView = _pieceViews[0, rank];
                    if (rookView != null)
                    {
                        rookView.SetPosition(new BoardCoord(3, rank), CoordToWorldPosition(new BoardCoord(3, rank)));
                        _pieceViews[3, rank] = rookView;
                        _pieceViews[0, rank] = null;
                    }
                }
            }

            // Desplazar visualmente la pieza movida
            if (movingPieceView != null)
            {
                movingPieceView.SetPosition(to, CoordToWorldPosition(to));
                _pieceViews[to.File, to.Rank] = movingPieceView;
                _pieceViews[from.File, from.Rank] = null;

                // Si fue coronación, refrescar inicial
                if (move.PromotionType != PieceType.None)
                {
                    movingPieceView.Initialize(BoardState[to], to, CoordToWorldPosition(to), pieceTheme);
                }
            }

            DeselectCurrent();
            OnMoveExecuted?.Invoke(move);
        }

        /// <summary>
        /// Permite a controladores externos (como BoardInputHandler) disparar el evento OnMoveExecuted
        /// tras completar el flujo de animación de una jugada.
        /// </summary>
        public void NotifyMoveExecuted(Move move)
        {
            OnMoveExecuted?.Invoke(move);
        }

        [ContextMenu("Clear Board")]
        public void ClearBoard()
        {
            Transform container = tilesContainer != null ? tilesContainer : transform.Find("TilesContainer");
            if (container != null)
            {
                DestroyChildren(container);
            }

            for (int f = 0; f < BoardState.BoardSize; f++)
            {
                for (int r = 0; r < BoardState.BoardSize; r++)
                {
                    _tileViews[f, r] = null;
                }
            }
        }

        [ContextMenu("Clear Pieces")]
        public void ClearPieces()
        {
            Transform container = piecesContainer != null ? piecesContainer : transform.Find("PiecesContainer");
            if (container != null)
            {
                DestroyChildren(container);
            }

            for (int f = 0; f < BoardState.BoardSize; f++)
            {
                for (int r = 0; r < BoardState.BoardSize; r++)
                {
                    _pieceViews[f, r] = null;
                }
            }
        }

        [ContextMenu("Refresh Piece Themes")]
        public void RefreshPieceThemes()
        {
            for (int f = 0; f < BoardState.BoardSize; f++)
            {
                for (int r = 0; r < BoardState.BoardSize; r++)
                {
                    PieceView pv = _pieceViews[f, r];
                    if (pv != null && pv.PieceData != null)
                    {
                        pv.Initialize(pv.PieceData, pv.Coord, CoordToWorldPosition(pv.Coord), pieceTheme);
                    }
                }
            }
        }

        [ContextMenu("Clear All")]
        public void ClearAll()
        {
            ClearPieces();
            ClearBoard();
            DeselectCurrent();
        }

        private void DestroyChildren(Transform parent)
        {
            List<GameObject> children = new List<GameObject>();
            for (int i = 0; i < parent.childCount; i++)
            {
                children.Add(parent.GetChild(i).gameObject);
            }

            foreach (GameObject child in children)
            {
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        public Vector3 CoordToWorldPosition(BoardCoord coord)
        {
            float offsetX = (coord.File - (BoardState.BoardSize - 1) * 0.5f) * tileSize;
            float offsetY = (coord.Rank - (BoardState.BoardSize - 1) * 0.5f) * tileSize;
            return transform.position + new Vector3(offsetX, offsetY, 0f);
        }

        public TileView GetTileView(BoardCoord coord)
        {
            if (!BoardState.IsInBounds(coord)) return null;
            return _tileViews[coord.File, coord.Rank];
        }

        public PieceView GetPieceView(BoardCoord coord)
        {
            if (!BoardState.IsInBounds(coord)) return null;
            return _pieceViews[coord.File, coord.Rank];
        }

        public void SetPieceView(BoardCoord coord, PieceView view)
        {
            if (BoardState.IsInBounds(coord))
            {
                _pieceViews[coord.File, coord.Rank] = view;
            }
        }

        public void ClearPieceViewAt(BoardCoord coord)
        {
            if (BoardState.IsInBounds(coord))
            {
                _pieceViews[coord.File, coord.Rank] = null;
            }
        }

        public bool WorldPositionToCoord(Vector3 worldPos, out BoardCoord coord)
        {
            Vector3 local = worldPos - transform.position;
            float fileF = (local.x / tileSize) + (BoardState.BoardSize - 1) * 0.5f;
            float rankF = (local.y / tileSize) + (BoardState.BoardSize - 1) * 0.5f;
            int file = Mathf.RoundToInt(fileF);
            int rank = Mathf.RoundToInt(rankF);
            coord = new BoardCoord(file, rank);
            return BoardState.IsInBounds(coord);
        }

        public void HighlightSquare(BoardCoord coord, Color color)
        {
            TileView tile = GetTileView(coord);
            if (tile != null)
            {
                tile.SetHighlight(color);
            }
        }

        public void SelectSquare(BoardCoord coord)
        {
            HighlightSquare(coord, selectedColor);
        }

        public void HighlightLegalMoves(IEnumerable<Move> moves)
        {
            if (moves == null) return;

            foreach (Move move in moves)
            {
                Color color = move.PieceCaptured != null ? attackMoveColor : validMoveColor;
                HighlightSquare(move.To, color);
            }
        }

        public void ClearHighlights()
        {
            for (int f = 0; f < BoardState.BoardSize; f++)
            {
                for (int r = 0; r < BoardState.BoardSize; r++)
                {
                    TileView tile = _tileViews[f, r];
                    if (tile != null)
                    {
                        tile.ResetColor();
                    }
                }
            }

            UpdateCheckHighlight();
        }

        private void OnGUI()
        {
            if (!showOnScreenHUD || FindAnyObjectByType<GameHUDView>() != null) return;

            int panelWidth = 270;
            int panelHeight = 240;
            Rect panelRect = new Rect(14, 14, panelWidth, panelHeight);

            // Fondo translúcido elegante
            GUI.Box(panelRect, "");

            GUILayout.BeginArea(panelRect);
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUILayout.Label("<b>CHESS ROGUELIKE (Fase 1)</b>");
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            string turnName = (BoardState != null && BoardState.CurrentTurn == PieceColor.White) ? "Blancas" : "Negras";
            GUILayout.Label($"  Turno: <b>{turnName}</b> (Mov #{BoardState?.FullmoveNumber ?? 1})");
            GUILayout.Label($"  Última jugada: <b>{(string.IsNullOrEmpty(lastMoveNotation) ? "Ninguna" : lastMoveNotation)}</b>");

            // Estado de Jaque / Mate
            if (isCheckmate)
            {
                GUI.color = Color.red;
                GUILayout.Label("  <b>¡¡ JAQUE MATE !!</b>");
                GUI.color = Color.white;
            }
            else if (isWhiteInCheck)
            {
                GUI.color = new Color(1f, 0.35f, 0.35f);
                GUILayout.Label("  <b>¡¡ JAQUE AL REY BLANCO !!</b>");
                GUI.color = Color.white;
            }
            else if (isBlackInCheck)
            {
                GUI.color = new Color(1f, 0.35f, 0.35f);
                GUILayout.Label("  <b>¡¡ JAQUE AL REY NEGRO !!</b>");
                GUI.color = Color.white;
            }
            else if (isStalemate)
            {
                GUI.color = Color.cyan;
                GUILayout.Label("  <b>¡ TABLAS POR REY AHOGADO !</b>");
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(0.4f, 0.9f, 0.4f);
                GUILayout.Label("  Estado: Posición normal");
                GUI.color = Color.white;
            }

            GUILayout.Space(4);
            GUILayout.Label("  Historial:");
            GUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUILayout.Label(string.IsNullOrEmpty(pgnHistory) ? "(sin movimientos)" : pgnHistory, GUILayout.Height(40), GUILayout.Width(250));
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Space(8);
            if (GUILayout.Button("▶ Ejecutar Tests Core", GUILayout.Width(250), GUILayout.Height(26)))
            {
                RunCoreLogicTests();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private Sprite GetOrCreateProceduralSprite()
        {
            if (_defaultWhiteSprite != null) return _defaultWhiteSprite;

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.filterMode = FilterMode.Point;
            texture.Apply();

            _defaultWhiteSprite = Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1.0f
            );

            return _defaultWhiteSprite;
        }
    }
}

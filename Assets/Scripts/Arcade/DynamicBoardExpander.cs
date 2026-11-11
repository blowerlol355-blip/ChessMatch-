using System;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Controlador de Expansión Dinámica del Tablero para el Modo Arcade:
    /// - Arranca en 8x8.
    /// - Cada 210s exactos (03:30, 07:00, 10:30, 14:00) agrega +3 filas en la parte superior con animación de caída.
    /// - Ajusta suavemente la cámara (orthographicSize y posición) y el marco dorado 32-bits para encajar en 9:16.
    /// </summary>
    public class DynamicBoardExpander : MonoBehaviour
    {
        private ArcadeSurvivorController _controller;

        private Transform _arenaRoot;
        private Transform _tilesParent;
        private Transform _shadowFrame;
        private Transform _goldOuterFrame;
        private Transform _darkMidFrame;
        private Transform _innerGoldFrame;

        private readonly SpriteRenderer[,] _tileRenderers = new SpriteRenderer[ArcadeLogic.BoardWidth, ArcadeLogic.MaxBoardHeight];
        private readonly SpriteRenderer[,] _telegraphRenderers = new SpriteRenderer[ArcadeLogic.BoardWidth, ArcadeLogic.MaxBoardHeight];
        private readonly float[,] _telegraphTimers = new float[ArcadeLogic.BoardWidth, ArcadeLogic.MaxBoardHeight];
        private readonly float[,] _rowDropProgress = new float[ArcadeLogic.BoardWidth, ArcadeLogic.MaxBoardHeight];

        private int _currentRows = ArcadeLogic.InitialBoardHeight;
        private int _expansionStage = 0;

        private float _targetOrthoSize = 7.5f;
        private Vector3 _targetCamPos = new Vector3(0f, 0f, -10f);
        private bool _hasCameraTargets = false;

        private readonly Color _lightSquareColor = new Color(0.84f, 0.85f, 0.74f, 1.0f);
        private readonly Color _darkSquareColor = new Color(0.36f, 0.47f, 0.28f, 1.0f);

        public int CurrentWidth => ArcadeLogic.BoardWidth;
        public int CurrentRows => _currentRows;
        public int ExpansionStage => _expansionStage;

        public event Action<int, int> OnBoardExpanded; // (oldRows, newRows)

        public void InitializeExpander(ArcadeSurvivorController controller)
        {
            _controller = controller;
        }

        public void ResetAndBuildInitialBoard()
        {
            ClearArenaVisuals();

            _currentRows = ArcadeLogic.InitialBoardHeight;
            _expansionStage = 0;
            _hasCameraTargets = false;

            for (int x = 0; x < ArcadeLogic.BoardWidth; x++)
            {
                for (int y = 0; y < ArcadeLogic.MaxBoardHeight; y++)
                {
                    _telegraphTimers[x, y] = 0f;
                    _rowDropProgress[x, y] = 1f;
                }
            }

            BuildArenaContainerAndInitialRows();
            RecalculateCameraTargets(snapImmediately: true);
        }

        public void ClearArenaVisuals()
        {
            if (_arenaRoot != null)
            {
                Destroy(_arenaRoot.gameObject);
                _arenaRoot = null;
            }
            _tilesParent = null;
            for (int x = 0; x < ArcadeLogic.BoardWidth; x++)
            {
                for (int y = 0; y < ArcadeLogic.MaxBoardHeight; y++)
                {
                    _tileRenderers[x, y] = null;
                    _telegraphRenderers[x, y] = null;
                }
            }
        }

        public Vector3 CoordToWorld(int x, int y)
        {
            float tile = _controller != null ? _controller.TileSize : 1f;
            float wx = (x - (ArcadeLogic.BoardWidth - 1) * 0.5f) * tile;
            float wy = (y - (ArcadeLogic.InitialBoardHeight - 1) * 0.5f) * tile;
            return new Vector3(wx, wy, 0f);
        }

        public bool WorldToCoord(Vector3 worldPos, out ArcadeCoord coord)
        {
            float tile = _controller != null ? _controller.TileSize : 1f;
            int x = Mathf.RoundToInt(worldPos.x / tile + (ArcadeLogic.BoardWidth - 1) * 0.5f);
            int y = Mathf.RoundToInt(worldPos.y / tile + (ArcadeLogic.InitialBoardHeight - 1) * 0.5f);
            coord = new ArcadeCoord(x, y);
            return coord.IsValidIn(_currentRows);
        }

        private void BuildArenaContainerAndInitialRows()
        {
            GameObject rootObj = new GameObject("ArcadeArena_DynamicBoard");
            rootObj.transform.SetParent(transform, false);
            _arenaRoot = rootObj.transform;

            _shadowFrame = CreateFrameQuad("ArenaShadow", new Color(0.01f, 0.02f, 0.04f, 0.86f), -12);
            _goldOuterFrame = CreateFrameQuad("ArenaGoldBorder", new Color(0.831f, 0.686f, 0.216f, 1f), -10);
            _darkMidFrame = CreateFrameQuad("ArenaDarkFrame", new Color(0.095f, 0.12f, 0.165f, 1f), -8);
            _innerGoldFrame = CreateFrameQuad("ArenaInnerGold", new Color(0.831f, 0.686f, 0.216f, 0.65f), -6);

            _tilesParent = new GameObject("Tiles").transform;
            _tilesParent.SetParent(_arenaRoot, false);

            SpawnRowsRange(0, _currentRows, animateDrop: false);
            UpdateFrameDimensions();
        }

        private Transform CreateFrameQuad(string objName, Color color, int sortingOrder)
        {
            GameObject go = new GameObject(objName);
            go.transform.SetParent(_arenaRoot, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _controller.GetProceduralSquareSprite();
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go.transform;
        }

        private void SpawnRowsRange(int fromRowInclusive, int toRowExclusive, bool animateDrop)
        {
            if (_tilesParent == null || _controller == null) return;

            float tile = _controller.TileSize;
            Sprite sq = _controller.GetProceduralSquareSprite();

            for (int y = fromRowInclusive; y < toRowExclusive; y++)
            {
                for (int x = 0; x < ArcadeLogic.BoardWidth; x++)
                {
                    if (_tileRenderers[x, y] != null) continue;

                    Vector3 finalPos = CoordToWorld(x, y);
                    bool isLight = (x + y) % 2 != 0;

                    GameObject tileObj = new GameObject($"Tile_{x}_{y}");
                    tileObj.transform.SetParent(_tilesParent, false);
                    tileObj.transform.position = animateDrop
                        ? finalPos + new Vector3(0f, (3.8f + (y - fromRowInclusive) * 0.65f) * tile, 0f)
                        : finalPos;
                    tileObj.transform.localScale = new Vector3(tile, tile, 1f);

                    SpriteRenderer sr = tileObj.AddComponent<SpriteRenderer>();
                    sr.sprite = sq;
                    sr.color = isLight ? _lightSquareColor : _darkSquareColor;
                    sr.sortingOrder = -2;
                    _tileRenderers[x, y] = sr;
                    _rowDropProgress[x, y] = animateDrop ? 0f : 1f;

                    GameObject teleObj = new GameObject("Telegraph");
                    teleObj.transform.SetParent(tileObj.transform, false);
                    teleObj.transform.localPosition = new Vector3(0f, 0f, -0.02f);
                    teleObj.transform.localScale = new Vector3(0.94f, 0.94f, 1f);

                    SpriteRenderer teleSR = teleObj.AddComponent<SpriteRenderer>();
                    teleSR.sprite = sq;
                    teleSR.color = new Color(1f, 0.2f, 0.2f, 0f);
                    teleSR.sortingOrder = 0;
                    _telegraphRenderers[x, y] = teleSR;
                }
            }
        }

        private void UpdateFrameDimensions()
        {
            if (_controller == null) return;
            float tile = _controller.TileSize;
            float boardW = ArcadeLogic.BoardWidth * tile;
            float boardH = _currentRows * tile;

            Vector3 botWorld = CoordToWorld(0, 0);
            Vector3 topWorld = CoordToWorld(ArcadeLogic.BoardWidth - 1, _currentRows - 1);
            float centerY = (botWorld.y + topWorld.y) * 0.5f;

            SetFrame(_shadowFrame, new Vector3(0.08f, centerY - 0.10f, 0.25f), new Vector3(boardW + 0.90f, boardH + 0.90f, 1f));
            SetFrame(_goldOuterFrame, new Vector3(0f, centerY, 0.20f), new Vector3(boardW + 0.42f, boardH + 0.42f, 1f));
            SetFrame(_darkMidFrame, new Vector3(0f, centerY, 0.16f), new Vector3(boardW + 0.30f, boardH + 0.30f, 1f));
            SetFrame(_innerGoldFrame, new Vector3(0f, centerY, 0.12f), new Vector3(boardW + 0.06f, boardH + 0.06f, 1f));
        }

        private static void SetFrame(Transform t, Vector3 pos, Vector3 scale)
        {
            if (t == null) return;
            t.localPosition = pos;
            t.localScale = scale;
        }

        public void TickExpansion(float elapsedSeconds, float dt)
        {
            int expectedRows = ArcadeLogic.GetBoardHeightForElapsedTime(elapsedSeconds);
            if (expectedRows > _currentRows)
            {
                PerformExpansionTo(expectedRows);
            }

            UpdateRowDropAnimations(dt);
            UpdateTileTelegraphs(dt);
            SmoothUpdateCamera(dt);
        }

        [ContextMenu("Trigger Next Board Expansion (+3 Rows)")]
        public void ExpandBoardByThreeRowsNow()
        {
            int nextRows = Mathf.Min(ArcadeLogic.MaxBoardHeight, _currentRows + ArcadeLogic.ExpansionRowDelta);
            if (nextRows > _currentRows)
            {
                PerformExpansionTo(nextRows);
            }
        }

        private void PerformExpansionTo(int targetRows)
        {
            int oldRows = _currentRows;
            targetRows = Mathf.Clamp(targetRows, ArcadeLogic.InitialBoardHeight, ArcadeLogic.MaxBoardHeight);
            if (targetRows <= oldRows) return;

            _currentRows = targetRows;
            _expansionStage = (_currentRows - ArcadeLogic.InitialBoardHeight) / ArcadeLogic.ExpansionRowDelta;

            SpawnRowsRange(oldRows, _currentRows, animateDrop: true);
            UpdateFrameDimensions();
            RecalculateCameraTargets(snapImmediately: false);

            // Flash dorado en las filas nuevas al aterrizar
            for (int x = 0; x < ArcadeLogic.BoardWidth; x++)
            {
                for (int y = oldRows; y < _currentRows; y++)
                {
                    SetTileTelegraph(new ArcadeCoord(x, y), new Color(1f, 0.86f, 0.28f, 0.55f), 0.70f);
                }
            }

            if (_controller != null)
            {
                _controller.RunStats.CurrentBoardRows = _currentRows;
                _controller.ShowBanner(
                    "¡EXPANSIÓN DEL TABLERO! (+3 FILAS)",
                    $"Arena ampliada a {ArcadeLogic.BoardWidth}x{_currentRows} · Horda enemiga incrementada",
                    new Color(1.0f, 0.84f, 0.24f),
                    3.6f);
            }

            RetroAudioManager.Instance?.PlayShieldActivate();
            OnBoardExpanded?.Invoke(oldRows, _currentRows);
        }

        private void UpdateRowDropAnimations(float dt)
        {
            float tile = _controller != null ? _controller.TileSize : 1f;
            for (int y = 0; y < _currentRows; y++)
            {
                for (int x = 0; x < ArcadeLogic.BoardWidth; x++)
                {
                    if (_rowDropProgress[x, y] >= 1f) continue;

                    SpriteRenderer sr = _tileRenderers[x, y];
                    if (sr == null) continue;

                    _rowDropProgress[x, y] = Mathf.Clamp01(_rowDropProgress[x, y] + dt * 1.65f);
                    float t = _rowDropProgress[x, y];
                    // Ease out con ligero rebote mecánico arcade
                    float inv = 1f - t;
                    float ease = 1f - inv * inv * inv;
                    Vector3 finalPos = CoordToWorld(x, y);
                    Vector3 dropOffset = new Vector3(0f, (1f - ease) * 4.2f * tile, 0f);
                    sr.transform.position = finalPos + dropOffset;
                }
            }
        }

        public void SetTileTelegraph(ArcadeCoord coord, Color color, float duration)
        {
            if (!coord.IsValidIn(_currentRows)) return;
            SpriteRenderer sr = _telegraphRenderers[coord.X, coord.Y];
            if (sr == null) return;

            sr.color = color;
            _telegraphTimers[coord.X, coord.Y] = Mathf.Max(_telegraphTimers[coord.X, coord.Y], duration);
        }

        private void UpdateTileTelegraphs(float dt)
        {
            for (int x = 0; x < ArcadeLogic.BoardWidth; x++)
            {
                for (int y = 0; y < _currentRows; y++)
                {
                    if (_telegraphTimers[x, y] > 0f)
                    {
                        _telegraphTimers[x, y] -= dt;
                        SpriteRenderer sr = _telegraphRenderers[x, y];
                        if (sr == null) continue;

                        if (_telegraphTimers[x, y] <= 0f)
                        {
                            _telegraphTimers[x, y] = 0f;
                            Color c = sr.color;
                            c.a = 0f;
                            sr.color = c;
                        }
                        else
                        {
                            Color c = sr.color;
                            if (c.r > 0.75f && c.g < 0.35f)
                            {
                                float pulse = 0.52f + 0.34f * Mathf.Abs(Mathf.Sin(Time.time * 14f));
                                c.a = Mathf.Clamp01(pulse * 0.80f);
                                sr.color = c;
                            }
                        }
                    }
                }
            }
        }

        public void RecalculateCameraTargets(bool snapImmediately)
        {
            Camera cam = Camera.main;
            if (cam == null || _controller == null) return;

            float tile = _controller.TileSize;
            float aspect = Mathf.Max(0.25f, cam.aspect);
            float scale = ChessFontProvider.GetResponsiveScale();

            float screenH = Mathf.Max(320f, Screen.height);
            float safeTop = Mathf.Max(0f, screenH - Screen.safeArea.yMax);
            float topHudPx = safeTop + 104f * scale + ((_controller.BossController != null && _controller.BossController.IsBossActive) ? 46f * scale : 0f);
            float botStripPx = 78f * scale;

            float topFrac = Mathf.Clamp(topHudPx / screenH, 0.08f, 0.34f);
            float botFrac = Mathf.Clamp(botStripPx / screenH, 0.05f, 0.22f);
            float usableFrac = Mathf.Max(0.45f, 1f - topFrac - botFrac);

            float boardWorldW = ArcadeLogic.BoardWidth * tile + 1.05f;
            float boardWorldH = _currentRows * tile + 0.90f;

            float fullViewHNeeded = boardWorldH / usableFrac;
            float sizeByHeight = fullViewHNeeded * 0.5f;
            float sizeByWidth = (boardWorldW * 0.5f) / aspect;

            _targetOrthoSize = Mathf.Max(sizeByHeight, sizeByWidth);

            Vector3 botWorld = CoordToWorld(0, 0);
            Vector3 topWorld = CoordToWorld(ArcadeLogic.BoardWidth - 1, _currentRows - 1);
            float boardCenterY = (botWorld.y + topWorld.y) * 0.5f;
            float camY = boardCenterY + (topFrac - botFrac) * _targetOrthoSize;

            _targetCamPos = new Vector3(0f, camY, -10f);
            _hasCameraTargets = true;

            if (snapImmediately)
            {
                cam.orthographicSize = _targetOrthoSize;
                cam.transform.position = _targetCamPos;
            }
        }

        private void SmoothUpdateCamera(float dt)
        {
            if (!_hasCameraTargets || Camera.main == null) return;
            Camera cam = Camera.main;
            float lerpT = 1f - Mathf.Exp(-dt * 4.5f);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, _targetOrthoSize, lerpT);
            cam.transform.position = Vector3.Lerp(cam.transform.position, _targetCamPos, lerpT);
        }
    }
}


using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Gestor de Oleadas Enemigas y Gemas de EXP bajo reglas de Captura Instantánea (1 movimiento):
    /// - Sin barras de vida (HP) ni daño numérico: cualquier pieza que cae en la casilla rival lo captura de un golpe.
    /// - Muestra indicador visual rojo 0.5s antes de atacar al Rey Blanco o capturar una pieza aliada.
    /// - Adapta los bordes de spawn y el límite máximo de enemigos conforme el tablero crece de 8x8 a 8x20.
    /// </summary>
    public class EnemyWaveSpawner : MonoBehaviour
    {
        private class ActiveEnemy
        {
            public int Id;
            public PieceType PieceType;
            public bool IsBossGuard;
            public ArcadeCoord Coord;
            public ArcadeCoord VisualFrom;
            public float SlideProgress;

            public GameObject Obj;
            public SpriteRenderer Renderer;
            public SpriteRenderer GuardHaloRenderer;

            public float MoveTimer;
            public float BaseMoveInterval;

            public bool IsTelegraphing;
            public float TelegraphTimer;
            public ArcadeCoord TelegraphTarget;
        }

        private class ActiveGem
        {
            public GameObject Obj;
            public SpriteRenderer Renderer;
            public int ExpAmount;
            public bool IsMagnetized;
            public float BobPhase;
        }

        private ArcadeSurvivorController _controller;
        private Transform _enemiesRoot;
        private Transform _gemsRoot;

        private readonly List<ActiveEnemy> _enemies = new List<ActiveEnemy>();
        private readonly List<ActiveGem> _gems = new List<ActiveGem>();
        private List<ArcadeCoord> _edgeCandidates;
        private int _cachedCandidatesHeight = -1;

        private int _nextEnemyId = 1;
        private float _spawnTimer = 0.5f;
        private bool _spawningPaused = false;

        private Sprite _diamondGemSprite;

        public int ActiveEnemyCount => _enemies.Count;

        public void InitializeSpawner(ArcadeSurvivorController controller)
        {
            _controller = controller;
            EnsureContainers();
            RefreshEdgeCandidates();
        }

        private void EnsureContainers()
        {
            if (_enemiesRoot == null)
            {
                GameObject eo = new GameObject("ArcadeEnemies");
                eo.transform.SetParent(transform, false);
                _enemiesRoot = eo.transform;
            }
            if (_gemsRoot == null)
            {
                GameObject go = new GameObject("ArcadeExpGems");
                go.transform.SetParent(transform, false);
                _gemsRoot = go.transform;
            }
        }

        public void RefreshEdgeCandidates()
        {
            int boardH = (_controller != null && _controller.BoardExpander != null)
                ? _controller.BoardExpander.CurrentRows
                : ArcadeLogic.InitialBoardHeight;

            if (_cachedCandidatesHeight != boardH || _edgeCandidates == null)
            {
                _cachedCandidatesHeight = boardH;
                _edgeCandidates = ArcadeLogic.GetEdgeSpawnCandidates(boardH);
            }
        }

        public void ResetSpawner()
        {
            ClearAllEnemies();
            ClearAllGems();

            _nextEnemyId = 1;
            _spawnTimer = 0.45f;
            _spawningPaused = false;
            _cachedCandidatesHeight = -1;
            RefreshEdgeCandidates();
        }

        public void SetSpawningPaused(bool paused)
        {
            _spawningPaused = paused;
        }

        public void ClearAllEnemies()
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i].Obj != null) Destroy(_enemies[i].Obj);
            }
            _enemies.Clear();
        }

        public void ClearAllMinorEnemiesKeepGuards()
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                if (!_enemies[i].IsBossGuard)
                {
                    if (_enemies[i].Obj != null) Destroy(_enemies[i].Obj);
                    _enemies.RemoveAt(i);
                }
            }
        }

        private void ClearAllGems()
        {
            for (int i = 0; i < _gems.Count; i++)
            {
                if (_gems[i].Obj != null) Destroy(_gems[i].Obj);
            }
            _gems.Clear();
        }

        public bool IsOccupiedByEnemy(ArcadeCoord coord)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i].Coord == coord) return true;
            }
            return false;
        }

        public int GetEnemyValueAt(ArcadeCoord coord)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i].Coord == coord)
                {
                    return ArcadeLogic.GetExpValueForPiece(_enemies[i].PieceType);
                }
            }
            return 0;
        }

        public bool IsBossGuardAt(ArcadeCoord coord)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i].Coord == coord && _enemies[i].IsBossGuard)
                {
                    return true;
                }
            }
            return false;
        }

        public int CountActiveBossGuards()
        {
            int count = 0;
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i].IsBossGuard) count++;
            }
            return count;
        }

        private void Update()
        {
            if (_controller == null || !_controller.IsSessionActive || _controller.IsPaused || _controller.IsGameOver)
            {
                return;
            }

            RefreshEdgeCandidates();
            float dt = Time.deltaTime;
            float elapsed = _controller.ElapsedSeconds;

            UpdateWaveSpawning(dt, elapsed);
            UpdateEnemies(dt);
            UpdateGems(dt);
        }

        private void UpdateWaveSpawning(float dt, float elapsed)
        {
            if (_spawningPaused || _controller.BossTriggered) return;

            int expStage = _controller.BoardExpander != null ? _controller.BoardExpander.ExpansionStage : 0;
            int maxEnemies = ArcadeLogic.GetMaxConcurrentEnemies(elapsed, expStage);
            if (_enemies.Count >= maxEnemies) return;

            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = ArcadeLogic.GetEnemySpawnInterval(elapsed);

                int burst = expStage >= 2 ? 2 : 1;
                List<PieceType> pool = ArcadeLogic.GetAllowedEnemyTypes(elapsed);

                for (int b = 0; b < burst && _enemies.Count < maxEnemies; b++)
                {
                    PieceType chosen = pool[Random.Range(0, pool.Count)];
                    SpawnRegularEnemy(chosen, elapsed);
                }
            }
        }

        public void SpawnBossMinions(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_enemies.Count >= 24) break;
                SpawnRegularEnemy(PieceType.Pawn, ArcadeLogic.SurvivalDurationSeconds);
            }
        }

        public void SpawnBossGuardPiece(PieceType pieceType, ArcadeCoord preferredCoord)
        {
            int boardH = _controller.BoardExpander != null ? _controller.BoardExpander.CurrentRows : ArcadeLogic.MaxBoardHeight;
            ArcadeCoord coord = preferredCoord;

            if (!coord.IsValidIn(boardH) || IsOccupiedByEnemy(coord) || coord == _controller.KingCoord ||
                (_controller.AllyManager != null && _controller.AllyManager.IsOccupiedByActiveAlly(coord)))
            {
                if (!TryFindNearbyFreeSpawn(preferredCoord, out coord))
                {
                    TryPickEdgeSpawnCoord(out coord);
                }
            }

            float interval = pieceType == PieceType.Knight ? 1.45f : (pieceType == PieceType.Bishop ? 1.55f : 1.65f);
            SpawnEnemyEntity(pieceType, coord, interval, isBossGuard: true);
        }

        private bool TryFindNearbyFreeSpawn(ArcadeCoord origin, out ArcadeCoord found)
        {
            int boardH = _controller.BoardExpander != null ? _controller.BoardExpander.CurrentRows : ArcadeLogic.MaxBoardHeight;
            for (int r = 1; r <= 4; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        ArcadeCoord c = new ArcadeCoord(origin.X + dx, origin.Y + dy);
                        if (!c.IsValidIn(boardH)) continue;
                        if (c == _controller.KingCoord) continue;
                        if (IsOccupiedByEnemy(c)) continue;
                        if (_controller.AllyManager != null && _controller.AllyManager.IsOccupiedByActiveAlly(c)) continue;
                        if (_controller.BossController != null && _controller.BossController.IsBossActive && _controller.BossController.IsTileOccupiedByBoss(c.X, c.Y)) continue;

                        found = c;
                        return true;
                    }
                }
            }
            found = origin;
            return false;
        }

        private void SpawnRegularEnemy(PieceType type, float elapsed)
        {
            if (!TryPickEdgeSpawnCoord(out ArcadeCoord coord)) return;

            float interval;
            switch (type)
            {
                case PieceType.Pawn:   interval = 1.65f; break;
                case PieceType.Knight: interval = 1.48f; break;
                case PieceType.Bishop: interval = 1.58f; break;
                case PieceType.Rook:   interval = 1.72f; break;
                default:               interval = 1.45f; break;
            }

            // Ligera aceleración progresiva en minutos altos
            float speedFactor = 1f - Mathf.Clamp01(elapsed / ArcadeLogic.SurvivalDurationSeconds) * 0.22f;
            SpawnEnemyEntity(type, coord, Mathf.Max(0.95f, interval * speedFactor), isBossGuard: false);
        }

        private bool TryPickEdgeSpawnCoord(out ArcadeCoord result)
        {
            RefreshEdgeCandidates();
            int boardH = _controller.BoardExpander != null ? _controller.BoardExpander.CurrentRows : ArcadeLogic.InitialBoardHeight;
            ArcadeCoord king = _controller.KingCoord;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                ArcadeCoord cand = _edgeCandidates[Random.Range(0, _edgeCandidates.Count)];
                if (!cand.IsValidIn(boardH)) continue;
                if (cand.ChebyshevDistanceTo(king) < 3) continue;
                if (IsOccupiedByEnemy(cand)) continue;
                if (_controller.AllyManager != null && _controller.AllyManager.IsOccupiedByActiveAlly(cand)) continue;
                if (_controller.BossController != null && _controller.BossController.IsBossActive && _controller.BossController.IsTileOccupiedByBoss(cand.X, cand.Y)) continue;

                result = cand;
                return true;
            }

            result = default;
            return false;
        }

        private void SpawnEnemyEntity(PieceType type, ArcadeCoord coord, float moveInterval, bool isBossGuard)
        {
            EnsureContainers();

            Vector3 worldPos = _controller.CoordToWorld(coord);
            GameObject go = new GameObject($"Enemy_{type}_{(isBossGuard ? "Guard_" : "")}{_nextEnemyId}");
            go.transform.SetParent(_enemiesRoot, false);
            go.transform.position = worldPos;

            float span = (isBossGuard ? 0.92f : 0.78f) * _controller.TileSize;
            Sprite pieceSprite = _controller.GetPieceSprite(type, PieceColor.Black);

            GameObject spriteObj = new GameObject("Visual");
            spriteObj.transform.SetParent(go.transform, false);
            SpriteRenderer sr = spriteObj.AddComponent<SpriteRenderer>();
            sr.sprite = pieceSprite != null ? pieceSprite : _controller.GetProceduralDiscSprite();
            sr.color = pieceSprite != null ? Color.white : new Color(0.20f, 0.22f, 0.26f, 1f);
            sr.sortingOrder = isBossGuard ? 11 : 9;
            float uScale = ArcadeSurvivorController.ComputeUniformSpriteScale(sr.sprite, span);
            spriteObj.transform.localScale = new Vector3(uScale, uScale, 1f);

            SpriteRenderer haloSR = null;
            if (isBossGuard)
            {
                GameObject haloObj = new GameObject("GuardRoyalHalo");
                haloObj.transform.SetParent(go.transform, false);
                haloObj.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                haloObj.transform.localScale = new Vector3(0.98f * _controller.TileSize, 0.98f * _controller.TileSize, 1f);
                haloSR = haloObj.AddComponent<SpriteRenderer>();
                haloSR.sprite = _controller.GetProceduralDiscSprite();
                haloSR.color = new Color(0.96f, 0.20f, 0.28f, 0.56f);
                haloSR.sortingOrder = 8;
            }

            _enemies.Add(new ActiveEnemy
            {
                Id = _nextEnemyId++,
                PieceType = type,
                IsBossGuard = isBossGuard,
                Coord = coord,
                VisualFrom = coord,
                SlideProgress = 1f,
                Obj = go,
                Renderer = sr,
                GuardHaloRenderer = haloSR,
                MoveTimer = Random.Range(0.45f, moveInterval),
                BaseMoveInterval = moveInterval
            });
        }

        private void UpdateEnemies(float dt)
        {
            int boardH = _controller.BoardExpander != null ? _controller.BoardExpander.CurrentRows : ArcadeLogic.InitialBoardHeight;
            ArcadeCoord kingCoord = _controller.KingCoord;
            SummonedAllyPieceManager allies = _controller.AllyManager;

            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                ActiveEnemy e = _enemies[i];
                if (e.Obj == null)
                {
                    _enemies.RemoveAt(i);
                    continue;
                }

                if (e.Renderer != null)
                {
                    e.Renderer.color = e.IsTelegraphing ? new Color(1f, 0.55f, 0.25f, 1f) : Color.white;
                }

                if (e.GuardHaloRenderer != null)
                {
                    float pulse = 0.42f + 0.25f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
                    e.GuardHaloRenderer.color = new Color(1.0f, 0.22f, 0.26f, pulse);
                }

                if (e.SlideProgress < 1f)
                {
                    e.SlideProgress = Mathf.Clamp01(e.SlideProgress + dt * 7.5f);
                    Vector3 fromPos = _controller.CoordToWorld(e.VisualFrom);
                    Vector3 toPos = _controller.CoordToWorld(e.Coord);
                    Vector3 pos = Vector3.Lerp(fromPos, toPos, e.SlideProgress);
                    if (e.PieceType == PieceType.Knight)
                    {
                        pos.y += Mathf.Sin(e.SlideProgress * Mathf.PI) * 0.45f * _controller.TileSize;
                    }
                    e.Obj.transform.position = pos;
                }

                // Telegrafía roja 0.5s previa al ataque sobre el Rey o sobre una pieza aliada
                if (e.IsTelegraphing)
                {
                    e.TelegraphTimer -= dt;
                    if (e.TelegraphTimer <= 0f)
                    {
                        e.IsTelegraphing = false;
                        ExecuteEnemyMoveOrStrike(i, e, e.TelegraphTarget, boardH);
                    }
                    continue;
                }

                e.MoveTimer -= dt;
                if (e.MoveTimer <= 0f)
                {
                    ArcadeCoord nextStep = ArcadeLogic.ChooseEnemyNextStep(
                        e.PieceType,
                        e.Coord,
                        boardH,
                        kingCoord,
                        c => IsOccupiedByEnemy(c) || (_controller.BossController != null && _controller.BossController.IsBossActive && _controller.BossController.IsTileOccupiedByBoss(c.X, c.Y)),
                        c => allies != null && allies.IsOccupiedByActiveAlly(c));

                    bool strikesKing = nextStep == kingCoord;
                    bool strikesAlly = allies != null && allies.IsOccupiedByActiveAlly(nextStep);
                    bool landsAdjacentKing = nextStep.ChebyshevDistanceTo(kingCoord) <= 1;

                    if (strikesKing || strikesAlly || landsAdjacentKing)
                    {
                        e.IsTelegraphing = true;
                        e.TelegraphTimer = ArcadeLogic.EnemyTelegraphSeconds; // 0.50s aviso rojo
                        e.TelegraphTarget = nextStep;
                        _controller.SetTileTelegraph(nextStep, new Color(1f, 0.14f, 0.16f, 0.80f), ArcadeLogic.EnemyTelegraphSeconds);
                    }
                    else
                    {
                        ExecuteEnemyMoveOrStrike(i, e, nextStep, boardH);
                    }
                }
            }
        }

        private void ExecuteEnemyMoveOrStrike(int listIndex, ActiveEnemy e, ArcadeCoord targetCoord, int boardH)
        {
            if (listIndex < 0 || listIndex >= _enemies.Count || _enemies[listIndex] != e) return;

            e.MoveTimer = e.BaseMoveInterval;
            if (!targetCoord.IsValidIn(boardH)) return;

            // 1. Ataque directo contra la casilla actual del Rey Blanco
            if (targetCoord == _controller.KingCoord)
            {
                _controller.HandleEnemyAttackOnKing(e.PieceType, e.IsBossGuard, out bool destroyAttacker);
                if (destroyAttacker)
                {
                    RemoveCapturedEnemy(listIndex, spawnExpGem: !e.IsBossGuard);
                }
                return;
            }

            // 2. Ataque contra una pieza aliada física en el tablero
            if (_controller.AllyManager != null && _controller.AllyManager.IsOccupiedByActiveAlly(targetCoord))
            {
                _controller.AllyManager.HandleEnemyCaptureAttemptOnAlly(targetCoord, e.PieceType, e.Obj.transform.position, out bool attackerDestroyed);
                if (attackerDestroyed)
                {
                    // Escudo Dorado de Falange (Petrosián) destruyó al atacante en su lugar
                    RemoveCapturedEnemy(listIndex, spawnExpGem: true);
                    return;
                }

                // Aliado entró en Reconstrucción; la pieza negra ocupa la casilla
                e.VisualFrom = e.Coord;
                e.Coord = targetCoord;
                e.SlideProgress = 0f;
                RetroAudioManager.Instance?.PlayPieceCapture();
                return;
            }

            // 3. Desplazamiento estándar si la casilla sigue libre
            if (!IsOccupiedByEnemy(targetCoord) &&
                !(_controller.BossController != null && _controller.BossController.IsBossActive && _controller.BossController.IsTileOccupiedByBoss(targetCoord.X, targetCoord.Y)))
            {
                e.VisualFrom = e.Coord;
                e.Coord = targetCoord;
                e.SlideProgress = 0f;
            }
        }

        public bool CaptureEnemyAt(ArcadeCoord coord, bool capturedByAlly)
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                if (_enemies[i].Coord == coord)
                {
                    RemoveCapturedEnemy(i, spawnExpGem: true);
                    return true;
                }
            }
            return false;
        }

        private void RemoveCapturedEnemy(int index, bool spawnExpGem)
        {
            if (index < 0 || index >= _enemies.Count) return;

            ActiveEnemy e = _enemies[index];
            Vector3 deathPos = e.Obj != null ? e.Obj.transform.position : _controller.CoordToWorld(e.Coord);

            _controller.RecordEnemyKill(e.PieceType, e.IsBossGuard);
            RetroAudioManager.Instance?.PlayPieceCapture();

            if (spawnExpGem)
            {
                int expVal = ArcadeLogic.GetExpValueForPiece(e.PieceType);
                SpawnExpGemAt(deathPos, expVal);
            }

            bool wasGuard = e.IsBossGuard;
            if (e.Obj != null) Destroy(e.Obj);
            _enemies.RemoveAt(index);

            if (wasGuard && _controller.BossController != null && _controller.BossController.IsBossActive)
            {
                _controller.BossController.NotifyGuardCaptured(deathPos);
            }
        }

        #region Gemas de EXP y Recogida Magnética

        public void SpawnExpGemAt(Vector3 worldPos, int expValue)
        {
            EnsureContainers();

            GameObject go = new GameObject($"ExpGem_{expValue}");
            go.transform.SetParent(_gemsRoot, false);
            go.transform.position = new Vector3(worldPos.x, worldPos.y, -0.05f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetDiamondGemSprite();

            if (expValue >= 9)
                sr.color = new Color(0.95f, 0.30f, 0.92f, 1f); // Morado Dama (9)
            else if (expValue >= 5)
                sr.color = new Color(1.0f, 0.82f, 0.22f, 1f);  // Dorado Torre (5)
            else if (expValue >= 3)
                sr.color = new Color(0.24f, 0.95f, 0.56f, 1f); // Esmeralda Caballo / Alfil (3)
            else
                sr.color = new Color(0.26f, 0.82f, 1.0f, 1f);  // Cian Peón (1)

            sr.sortingOrder = 6;
            float span = (expValue >= 5 ? 0.42f : 0.34f) * _controller.TileSize;
            float scale = ArcadeSurvivorController.ComputeUniformSpriteScale(sr.sprite, span);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            _gems.Add(new ActiveGem
            {
                Obj = go,
                Renderer = sr,
                ExpAmount = expValue,
                IsMagnetized = false,
                BobPhase = Random.Range(0f, Mathf.PI * 2f)
            });
        }

        private void UpdateGems(float dt)
        {
            Vector3 kingPos = _controller.KingWorldPosition;
            float magnetRange = _controller.MagnetPickupRange * _controller.TileSize;
            float collectDist = 0.48f * _controller.TileSize;

            for (int i = _gems.Count - 1; i >= 0; i--)
            {
                ActiveGem g = _gems[i];
                if (g.Obj == null)
                {
                    _gems.RemoveAt(i);
                    continue;
                }

                Vector3 gp = g.Obj.transform.position;
                float dist = Vector2.Distance(new Vector2(gp.x, gp.y), new Vector2(kingPos.x, kingPos.y));

                if (!g.IsMagnetized && dist <= magnetRange)
                {
                    g.IsMagnetized = true;
                }

                if (g.IsMagnetized)
                {
                    Vector3 target = new Vector3(kingPos.x, kingPos.y, gp.z);
                    g.Obj.transform.position = Vector3.MoveTowards(gp, target, 11.5f * _controller.TileSize * dt);
                    dist = Vector2.Distance(new Vector2(g.Obj.transform.position.x, g.Obj.transform.position.y), new Vector2(kingPos.x, kingPos.y));
                }
                else
                {
                    float bob = Mathf.Sin(Time.time * 4.5f + g.BobPhase) * 0.04f;
                    g.Obj.transform.position = new Vector3(gp.x, gp.y + bob * dt, gp.z);
                }

                if (dist <= collectDist)
                {
                    _controller.AddExp(g.ExpAmount);
                    RetroAudioManager.Instance?.PlayTapIfNoPieceSoundThisFrame();

                    Destroy(g.Obj);
                    _gems.RemoveAt(i);
                }
            }
        }

        private Sprite GetDiamondGemSprite()
        {
            if (_diamondGemSprite != null) return _diamondGemSprite;
            const int sz = 20;
            Texture2D tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color[] cols = new Color[sz * sz];
            float half = (sz - 1) * 0.5f;
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float man = (Mathf.Abs(x - half) + Mathf.Abs(y - half)) / half;
                    if (man > 0.95f) cols[y * sz + x] = Color.clear;
                    else if (man > 0.72f) cols[y * sz + x] = new Color(0.95f, 0.98f, 1f, 1f);
                    else cols[y * sz + x] = Color.white;
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            _diamondGemSprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz);
            return _diamondGemSprite;
        }

        #endregion
    }
}

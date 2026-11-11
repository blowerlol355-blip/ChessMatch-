using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Gestor de Piezas Aliadas Autónomas invocadas físicamente en el tablero:
    /// - Ocupan casillas reales y ejecutan capturas puras de 1 movimiento cada 1.2s (ajustable).
    /// - IA aliada escoge capturas por mayor valor (PieceValueTable) o cercanía al Rey Blanco.
    /// - Soporta Sinergias Polgár (barrido perforante), Tal (doble salto en L), Petrosián (Escudo Dorado) y Kaspárov (x2 velocidad).
    /// - Si es capturada, entra en Reconstrucción (4s) y reaparece junto al Rey Blanco.
    /// </summary>
    public class SummonedAllyPieceManager : MonoBehaviour
    {
        public class ActiveAllyPiece
        {
            public int Id;
            public PieceType PieceType;
            public ArcadeCoord Coord;
            public ArcadeCoord VisualFrom;
            public ArcadeCoord ChainMidWaypoint;
            public bool HasChainMidWaypoint;
            public float SlideProgress;

            public GameObject Obj;
            public SpriteRenderer PieceRenderer;
            public SpriteRenderer BaseRingRenderer;
            public SpriteRenderer ShieldBadgeRenderer;
            public GameObject ReconstructGhostObj;
            public TextMesh ReconstructCountdownMesh;

            public float TurnTimer;
            public float BaseTurnInterval;
            public bool HasGoldenShield;

            public bool IsReconstructing;
            public float ReconstructionTimer;
        }

        private ArcadeSurvivorController _controller;
        private Transform _alliesRoot;
        private readonly List<ActiveAllyPiece> _allies = new List<ActiveAllyPiece>();
        private int _nextAllyId = 1;

        public int ActiveAllyCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < _allies.Count; i++)
                {
                    if (!_allies[i].IsReconstructing) c++;
                }
                return c;
            }
        }

        public int TotalAllyCount => _allies.Count;

        public void InitializeManager(ArcadeSurvivorController controller)
        {
            _controller = controller;
            EnsureRoot();
        }

        private void EnsureRoot()
        {
            if (_alliesRoot == null)
            {
                GameObject go = new GameObject("ArcadeSummonedAllies");
                go.transform.SetParent(transform, false);
                _alliesRoot = go.transform;
            }
        }

        public void ResetAllies()
        {
            for (int i = 0; i < _allies.Count; i++)
            {
                if (_allies[i].Obj != null) Destroy(_allies[i].Obj);
                if (_allies[i].ReconstructGhostObj != null) Destroy(_allies[i].ReconstructGhostObj);
            }
            _allies.Clear();
            _nextAllyId = 1;
        }

        public void CollectAllOwnedAllyTypes(List<PieceType> results)
        {
            if (results == null) return;
            for (int i = 0; i < _allies.Count; i++)
            {
                results.Add(_allies[i].PieceType);
            }
        }

        public int CountAlliesOfType(PieceType type)
        {
            int count = 0;
            for (int i = 0; i < _allies.Count; i++)
            {
                if (_allies[i].PieceType == type) count++;
            }
            return count;
        }

        public bool IsOccupiedByActiveAlly(ArcadeCoord coord)
        {
            for (int i = 0; i < _allies.Count; i++)
            {
                if (!_allies[i].IsReconstructing && _allies[i].Coord == coord)
                {
                    return true;
                }
            }
            return false;
        }

        public bool SwapAllyFromSquare(ArcadeCoord targetSquare, ArcadeCoord moveToSquare)
        {
            for (int i = 0; i < _allies.Count; i++)
            {
                ActiveAllyPiece a = _allies[i];
                if (a.IsReconstructing || a.Coord != targetSquare) continue;

                a.HasChainMidWaypoint = false;
                a.VisualFrom = a.Coord;
                a.Coord = moveToSquare;
                a.SlideProgress = 0f;
                return true;
            }
            return false;
        }

        public void SummonAllyPiece(PieceType pieceType, bool silent = false)
        {
            if (_controller == null) return;
            EnsureRoot();

            if (!TryFindFreeAdjacentSquare(_controller.KingCoord, out ArcadeCoord spawnCoord))
            {
                spawnCoord = new ArcadeCoord(
                    Mathf.Clamp(_controller.KingCoord.X + 1, 0, ArcadeLogic.BoardWidth - 1),
                    _controller.KingCoord.Y);
            }

            Vector3 worldPos = _controller.CoordToWorld(spawnCoord);
            GameObject go = new GameObject($"Ally_{pieceType}_{_nextAllyId}");
            go.transform.SetParent(_alliesRoot, false);
            go.transform.position = worldPos;

            float tile = _controller.TileSize;
            Sprite pieceSpr = _controller.GetPieceSprite(pieceType, PieceColor.White);

            // Anillo identificador esmeralda-cian bajo la pieza aliada
            GameObject ringObj = new GameObject("AllyRing");
            ringObj.transform.SetParent(go.transform, false);
            ringObj.transform.localPosition = new Vector3(0f, -0.02f, 0.04f);
            ringObj.transform.localScale = new Vector3(0.84f * tile, 0.84f * tile, 1f);
            SpriteRenderer ringSR = ringObj.AddComponent<SpriteRenderer>();
            ringSR.sprite = _controller.GetProceduralDiscSprite();
            ringSR.color = new Color(0.18f, 0.88f, 0.68f, 0.42f);
            ringSR.sortingOrder = 7;

            GameObject visObj = new GameObject("Visual");
            visObj.transform.SetParent(go.transform, false);
            SpriteRenderer sr = visObj.AddComponent<SpriteRenderer>();
            sr.sprite = pieceSpr != null ? pieceSpr : _controller.GetProceduralDiscSprite();
            sr.color = pieceSpr != null ? Color.white : new Color(0.92f, 0.96f, 1f, 1f);
            sr.sortingOrder = 11;
            float uScale = ArcadeSurvivorController.ComputeUniformSpriteScale(sr.sprite, 0.78f * tile);
            visObj.transform.localScale = new Vector3(uScale, uScale, 1f);

            // Insignia de Escudo Dorado (Sinergia Petrosián Falange de Hierro)
            GameObject shieldObj = new GameObject("IronPhalanxBadge");
            shieldObj.transform.SetParent(go.transform, false);
            shieldObj.transform.localPosition = new Vector3(0.28f * tile, -0.26f * tile, -0.03f);
            Sprite shieldSpr = _controller.GetShieldEmblemSprite();
            SpriteRenderer shieldSR = shieldObj.AddComponent<SpriteRenderer>();
            shieldSR.sprite = shieldSpr;
            shieldSR.sortingOrder = 13;
            float shScale = ArcadeSurvivorController.ComputeUniformSpriteScale(shieldSpr, 0.34f * tile);
            shieldObj.transform.localScale = new Vector3(shScale, shScale, 1f);
            shieldObj.SetActive(false);

            var ally = new ActiveAllyPiece
            {
                Id = _nextAllyId++,
                PieceType = pieceType,
                Coord = spawnCoord,
                VisualFrom = spawnCoord,
                SlideProgress = 1f,
                Obj = go,
                PieceRenderer = sr,
                BaseRingRenderer = ringSR,
                ShieldBadgeRenderer = shieldSR,
                TurnTimer = Random.Range(0.30f, 0.75f),
                BaseTurnInterval = ArcadeLogic.BaseAllyTurnInterval, // 1.2s base
                HasGoldenShield = false,
                IsReconstructing = false,
                ReconstructionTimer = 0f
            };

            _allies.Add(ally);
            _controller.SynergySystem?.EvaluateAndSyncSynergies(showNotifications: !silent);
            RefreshEligibleGoldenShields();

            _controller.FlashTileColor(spawnCoord, new Color(0.22f, 0.92f, 0.68f, 0.52f), 0.30f);

            if (!silent)
            {
                RetroAudioManager.Instance?.PlayPieceSelect();
                _controller.SpawnFloatingText(worldPos + Vector3.up * 0.4f, $"+{ArcadeLogic.GetPieceDisplayNameEs(pieceType).ToUpperInvariant()}", new Color(0.32f, 0.96f, 0.76f), true);
            }
        }

        public void RefreshEligibleGoldenShields()
        {
            bool ironPhalanxActive = _controller != null && _controller.SynergySystem != null && _controller.SynergySystem.IsIronPhalanxActive;
            for (int i = 0; i < _allies.Count; i++)
            {
                ActiveAllyPiece a = _allies[i];
                if (ironPhalanxActive && (a.PieceType == PieceType.Pawn || a.PieceType == PieceType.Rook))
                {
                    a.HasGoldenShield = true;
                }
                UpdateAllyShieldVisual(a);
            }
        }

        private static void UpdateAllyShieldVisual(ActiveAllyPiece a)
        {
            if (a.ShieldBadgeRenderer != null)
            {
                a.ShieldBadgeRenderer.gameObject.SetActive(!a.IsReconstructing && a.HasGoldenShield);
            }
        }

        /// <summary>
        /// Resuelve el intento de captura enemigo sobre una pieza aliada:
        /// - Si tiene Escudo Dorado (IronPhalanxSynergy), sobrevive y destruye al atacante en su lugar.
        /// - Si no tiene escudo, entra en estado de Reconstrucción durante 4s para reaparecer junto al Rey Blanco.
        /// </summary>
        public bool HandleEnemyCaptureAttemptOnAlly(ArcadeCoord coord, PieceType attackerType, Vector3 attackerWorld, out bool attackerDestroyed)
        {
            attackerDestroyed = false;

            for (int i = 0; i < _allies.Count; i++)
            {
                ActiveAllyPiece a = _allies[i];
                if (a.IsReconstructing || a.Coord != coord) continue;

                if (a.HasGoldenShield)
                {
                    a.HasGoldenShield = false;
                    UpdateAllyShieldVisual(a);
                    attackerDestroyed = true;

                    _controller.RunStats.BlockedAttacksByShield++;
                    RetroAudioManager.Instance?.PlayShieldActivate();
                    _controller.FlashTileColor(coord, new Color(1f, 0.85f, 0.22f, 0.75f), 0.35f);
                    _controller.SpawnFloatingText(_controller.CoordToWorld(coord) + Vector3.up * 0.45f, "¡FALANGE DORADA!", new Color(1f, 0.86f, 0.24f), true);
                    return true;
                }

                BeginAllyReconstruction(a);
                return true;
            }

            return false;
        }

        private void BeginAllyReconstruction(ActiveAllyPiece ally)
        {
            if (ally.IsReconstructing) return;

            ally.IsReconstructing = true;
            ally.ReconstructionTimer = _controller != null && _controller.SynergySystem != null
                ? _controller.SynergySystem.GetReconstructionDuration()
                : ArcadeLogic.AllyReconstructionSeconds;

            if (_controller != null)
            {
                _controller.RunStats.AllyReconstructions++;
                _controller.FlashTileColor(ally.Coord, new Color(0.95f, 0.38f, 0.22f, 0.65f), 0.35f);
                _controller.SpawnFloatingText(_controller.CoordToWorld(ally.Coord) + Vector3.up * 0.38f, "¡RECONSTRUCCIÓN!", new Color(1f, 0.56f, 0.22f), false);
            }

            if (ally.Obj != null) ally.Obj.SetActive(false);
            CreateReconstructionGhostVisual(ally);
            UpdateAllyShieldVisual(ally);
        }

        private void CreateReconstructionGhostVisual(ActiveAllyPiece ally)
        {
            if (ally.ReconstructGhostObj != null) Destroy(ally.ReconstructGhostObj);

            EnsureRoot();
            GameObject ghost = new GameObject($"Reconstructing_{ally.PieceType}_{ally.Id}");
            ghost.transform.SetParent(_alliesRoot, false);
            ghost.transform.position = _controller.CoordToWorld(ally.Coord);

            Sprite spr = _controller.GetPieceSprite(ally.PieceType, PieceColor.White);
            GameObject sprObj = new GameObject("GhostSprite");
            sprObj.transform.SetParent(ghost.transform, false);
            SpriteRenderer sr = sprObj.AddComponent<SpriteRenderer>();
            sr.sprite = spr != null ? spr : _controller.GetProceduralDiscSprite();
            sr.color = new Color(0.45f, 0.85f, 1f, 0.42f);
            sr.sortingOrder = 6;
            float sc = ArcadeSurvivorController.ComputeUniformSpriteScale(sr.sprite, 0.65f * _controller.TileSize);
            sprObj.transform.localScale = new Vector3(sc, sc, 1f);

            GameObject txtObj = new GameObject("TimerText");
            txtObj.transform.SetParent(ghost.transform, false);
            txtObj.transform.localPosition = new Vector3(0f, 0.46f * _controller.TileSize, -0.1f);
            TextMesh tm = txtObj.AddComponent<TextMesh>();
            tm.text = $"{ally.ReconstructionTimer:F1}s";
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontSize = 42;
            tm.fontStyle = FontStyle.Bold;
            tm.characterSize = 0.048f * _controller.TileSize;
            tm.color = new Color(0.55f, 0.92f, 1f, 1f);

            Font serif = ChessFontProvider.SerifBold;
            MeshRenderer mr = txtObj.GetComponent<MeshRenderer>();
            if (serif != null)
            {
                tm.font = serif;
                if (serif.material != null && mr != null) mr.sharedMaterial = serif.material;
            }
            if (mr != null) mr.sortingOrder = 40;

            ally.ReconstructGhostObj = ghost;
            ally.ReconstructCountdownMesh = tm;
        }

        private void CompleteAllyReconstruction(ActiveAllyPiece ally)
        {
            ally.IsReconstructing = false;
            ally.ReconstructionTimer = 0f;

            if (ally.ReconstructGhostObj != null)
            {
                Destroy(ally.ReconstructGhostObj);
                ally.ReconstructGhostObj = null;
                ally.ReconstructCountdownMesh = null;
            }

            if (!TryFindFreeAdjacentSquare(_controller.KingCoord, out ArcadeCoord respawnCoord))
            {
                respawnCoord = _controller.KingCoord;
            }

            // Si había un enemigo justo encima al reaparecer cerca, destruirlo o buscar libre; TryFindFreeAdjacentSquare ya evita ocupadas
            ally.Coord = respawnCoord;
            ally.VisualFrom = respawnCoord;
            ally.HasChainMidWaypoint = false;
            ally.SlideProgress = 1f;
            ally.TurnTimer = 0.35f;

            bool ironPhalanxActive = _controller.SynergySystem != null && _controller.SynergySystem.IsIronPhalanxActive;
            if (ironPhalanxActive && (ally.PieceType == PieceType.Pawn || ally.PieceType == PieceType.Rook))
            {
                ally.HasGoldenShield = true;
            }

            if (ally.Obj != null)
            {
                ally.Obj.transform.position = _controller.CoordToWorld(respawnCoord);
                ally.Obj.SetActive(true);
            }
            UpdateAllyShieldVisual(ally);

            _controller.FlashTileColor(respawnCoord, new Color(0.32f, 0.94f, 1f, 0.65f), 0.35f);
            _controller.SpawnFloatingText(_controller.CoordToWorld(respawnCoord) + Vector3.up * 0.4f, "¡REFORJADO!", new Color(0.45f, 0.96f, 1f), false);
            RetroAudioManager.Instance?.PlayPieceSelect();
        }

        public bool TryFindFreeAdjacentSquare(ArcadeCoord origin, out ArcadeCoord found)
        {
            int boardH = _controller.BoardExpander != null ? _controller.BoardExpander.CurrentRows : ArcadeLogic.InitialBoardHeight;

            for (int radius = 1; radius <= 4; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue;
                        ArcadeCoord c = new ArcadeCoord(origin.X + dx, origin.Y + dy);
                        if (!c.IsValidIn(boardH)) continue;
                        if (c == _controller.KingCoord) continue;
                        if (IsOccupiedByActiveAlly(c)) continue;
                        if (_controller.EnemySpawner != null && _controller.EnemySpawner.IsOccupiedByEnemy(c)) continue;
                        if (_controller.BossController != null && _controller.BossController.IsBossActive && _controller.BossController.IsTileOccupiedByBoss(c.X, c.Y)) continue;

                        found = c;
                        return true;
                    }
                }
            }

            // Fallback aceptando casillas sin aliado/rey aunque tengan borde
            for (int radius = 1; radius <= 3; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        ArcadeCoord c = new ArcadeCoord(origin.X + dx, origin.Y + dy);
                        if (!c.IsValidIn(boardH)) continue;
                        if (c == _controller.KingCoord || IsOccupiedByActiveAlly(c)) continue;
                        found = c;
                        return true;
                    }
                }
            }

            found = origin;
            return false;
        }

        private void Update()
        {
            if (_controller == null || !_controller.IsSessionActive || _controller.IsPaused || _controller.IsGameOver)
            {
                return;
            }

            float dt = Time.deltaTime;
            bool blitz = _controller.SynergySystem != null && _controller.SynergySystem.IsBlitzOverdriveActive;

            for (int i = 0; i < _allies.Count; i++)
            {
                ActiveAllyPiece a = _allies[i];

                if (a.IsReconstructing)
                {
                    a.ReconstructionTimer -= dt;
                    if (a.ReconstructCountdownMesh != null)
                    {
                        a.ReconstructCountdownMesh.text = $"{Mathf.Max(0f, a.ReconstructionTimer):F1}s";
                    }
                    if (a.ReconstructionTimer <= 0f)
                    {
                        CompleteAllyReconstruction(a);
                    }
                    continue;
                }

                // Brillo y animaciones del sprite
                if (a.BaseRingRenderer != null)
                {
                    a.BaseRingRenderer.color = blitz
                        ? new Color(1f, 0.42f, 0.16f, 0.56f + 0.16f * Mathf.Sin(Time.time * 10f))
                        : new Color(0.18f, 0.88f, 0.68f, 0.42f);
                }
                UpdateAllyShieldVisual(a);

                if (a.SlideProgress < 1f && a.Obj != null)
                {
                    float speed = blitz ? 11.5f : 8.5f;
                    a.SlideProgress = Mathf.Clamp01(a.SlideProgress + dt * speed);
                    float ease = 1f - Mathf.Pow(1f - a.SlideProgress, 3f);

                    Vector3 worldPos;
                    if (a.HasChainMidWaypoint)
                    {
                        Vector3 p0 = _controller.CoordToWorld(a.VisualFrom);
                        Vector3 p1 = _controller.CoordToWorld(a.ChainMidWaypoint);
                        Vector3 p2 = _controller.CoordToWorld(a.Coord);

                        if (ease < 0.5f)
                        {
                            float sub = ease * 2f;
                            worldPos = Vector3.Lerp(p0, p1, sub);
                            worldPos.y += Mathf.Sin(sub * Mathf.PI) * 0.48f * _controller.TileSize;
                        }
                        else
                        {
                            float sub = (ease - 0.5f) * 2f;
                            worldPos = Vector3.Lerp(p1, p2, sub);
                            worldPos.y += Mathf.Sin(sub * Mathf.PI) * 0.55f * _controller.TileSize;
                        }
                    }
                    else
                    {
                        Vector3 fromW = _controller.CoordToWorld(a.VisualFrom);
                        Vector3 toW = _controller.CoordToWorld(a.Coord);
                        worldPos = Vector3.Lerp(fromW, toW, ease);
                        if (a.PieceType == PieceType.Knight)
                        {
                            worldPos.y += Mathf.Sin(ease * Mathf.PI) * 0.48f * _controller.TileSize;
                        }
                    }

                    a.Obj.transform.position = worldPos;
                    if (a.SlideProgress >= 1f) a.HasChainMidWaypoint = false;
                }

                // Turno autónomo cada 1.2s (escalado por Sinergia Kaspárov / Modificadores)
                float speedMult = _controller.SynergySystem != null ? _controller.SynergySystem.GetAllyTurnSpeedMultiplier(a.PieceType) : 1f;
                a.TurnTimer -= dt * speedMult;
                if (a.TurnTimer <= 0f)
                {
                    a.TurnTimer = a.BaseTurnInterval;
                    ExecuteAllyTurn(a);
                }
            }
        }

        private void ExecuteAllyTurn(ActiveAllyPiece ally)
        {
            if (_controller == null || _controller.IsGameOver) return;

            int boardH = _controller.BoardExpander != null ? _controller.BoardExpander.CurrentRows : ArcadeLogic.InitialBoardHeight;
            CardSynergySystem syn = _controller.SynergySystem;
            EnemyWaveSpawner spawner = _controller.EnemySpawner;
            MegaBossPieceController boss = _controller.BossController;

            bool hasPiercing = syn != null && syn.IsPiercingCaptureActive;
            bool hasChain = syn != null && syn.IsChainLeapActive;
            bool pawnBoost = syn != null && syn.HasPawnAdvanceBoost;
            bool knightBoost = syn != null && syn.HasKnightAssaultBoost;

            bool bossActive = boss != null && boss.IsBossActive;
            int bossX = bossActive ? boss.AnchorCoord.X : -99;
            int bossY = bossActive ? boss.AnchorCoord.Y : -99;
            bool bossVulnerable = bossActive && boss.IsVulnerableToDirectCapture;

            ArcadeMoveOption bestMove = ArcadeLogic.EvaluateBestAllyMove(
                ally.PieceType,
                ally.Coord,
                boardH,
                _controller.KingCoord,
                c => c == _controller.KingCoord || (c != ally.Coord && IsOccupiedByActiveAlly(c)),
                c => spawner != null ? spawner.GetEnemyValueAt(c) : 0,
                c => spawner != null && spawner.IsBossGuardAt(c),
                bossActive,
                bossX,
                bossY,
                bossVulnerable,
                hasPiercing,
                hasChain,
                pawnBoost,
                knightBoost);

            if (bestMove == null) return;

            // Ejecutar desplazamiento principal + capturas en trayectoria (PiercingCaptureSynergy si aplica)
            ArcadeCoord origCoord = ally.Coord;
            ally.VisualFrom = origCoord;
            ally.Coord = bestMove.To;
            ally.HasChainMidWaypoint = false;
            ally.SlideProgress = 0f;

            bool capturedAnything = false;

            if (bestMove.CapturedCoords.Count > 0)
            {
                // Resaltar estela de barrido si es PiercingCaptureSynergy (Polgár)
                Color flashCol = bestMove.CapturedCoords.Count > 1
                    ? MasterCardDatabase.PolgarAuraColor
                    : new Color(0.28f, 0.94f, 0.72f, 0.65f);

                for (int i = 0; i < bestMove.CapturedCoords.Count; i++)
                {
                    ArcadeCoord targetCap = bestMove.CapturedCoords[i];
                    _controller.FlashTileColor(targetCap, flashCol, 0.28f);

                    if (spawner != null && spawner.CaptureEnemyAt(targetCap, capturedByAlly: true))
                    {
                        capturedAnything = true;
                    }
                }
            }

            if (bestMove.CapturesBoss && bossActive && bossVulnerable)
            {
                boss.CaptureBossDefinitiveCheckmate(ally.PieceType.ToString());
                return;
            }

            // Sinergia Tal + Caballo (ChainLeapSynergy): 2° salto en el mismo turno si capturó en el primero
            if (ally.PieceType == PieceType.Knight && hasChain && capturedAnything)
            {
                ArcadeCoord? chainTarget = ArcadeLogic.GetKnightChainCaptureTarget(
                    ally.Coord,
                    boardH,
                    _controller.KingCoord,
                    c => c == _controller.KingCoord || (c != ally.Coord && IsOccupiedByActiveAlly(c)),
                    c => spawner != null ? spawner.GetEnemyValueAt(c) : 0,
                    c => spawner != null && spawner.IsBossGuardAt(c),
                    bossActive,
                    bossX,
                    bossY,
                    bossVulnerable);

                if (chainTarget.HasValue)
                {
                    ArcadeCoord midSquare = ally.Coord;
                    ArcadeCoord secondTarget = chainTarget.Value;

                    ally.VisualFrom = origCoord;
                    ally.ChainMidWaypoint = midSquare;
                    ally.HasChainMidWaypoint = true;
                    ally.Coord = secondTarget;
                    ally.SlideProgress = 0f;

                    _controller.FlashTileColor(midSquare, MasterCardDatabase.TalAuraColor, 0.35f);
                    _controller.FlashTileColor(secondTarget, MasterCardDatabase.TalAuraColor, 0.35f);
                    _controller.SpawnFloatingText(_controller.CoordToWorld(midSquare) + Vector3.up * 0.42f, "¡DOBLE SALTO!", MasterCardDatabase.TalAuraColor, true);

                    if (spawner != null)
                    {
                        spawner.CaptureEnemyAt(secondTarget, capturedByAlly: true);
                    }

                    if (bossActive && bossVulnerable && boss.IsTileOccupiedByBoss(secondTarget.X, secondTarget.Y))
                    {
                        boss.CaptureBossDefinitiveCheckmate("Caballo (Doble Salto)");
                        return;
                    }
                }
            }

            // Coronación automática si un Peón Aliado alcanza la fila superior actual
            if (ally.PieceType == PieceType.Pawn && ally.Coord.Y >= boardH - 1)
            {
                PromotePawnToQueen(ally);
            }
            else if (!capturedAnything)
            {
                _controller.FlashTileColor(ally.Coord, new Color(0.28f, 0.88f, 0.72f, 0.28f), 0.16f);
            }
        }

        private void PromotePawnToQueen(ActiveAllyPiece pawn)
        {
            pawn.PieceType = PieceType.Queen;
            pawn.HasGoldenShield = false;
            UpdateAllyShieldVisual(pawn);

            if (pawn.PieceRenderer != null)
            {
                Sprite queenSpr = _controller.GetPieceSprite(PieceType.Queen, PieceColor.White);
                if (queenSpr != null)
                {
                    pawn.PieceRenderer.sprite = queenSpr;
                    float uScale = ArcadeSurvivorController.ComputeUniformSpriteScale(queenSpr, 0.82f * _controller.TileSize);
                    pawn.PieceRenderer.transform.localScale = new Vector3(uScale, uScale, 1f);
                }
            }

            _controller.SynergySystem?.EvaluateAndSyncSynergies(showNotifications: true);
            _controller.SpawnFloatingText(_controller.CoordToWorld(pawn.Coord) + Vector3.up * 0.55f, "¡CORONACIÓN -> DAMA!", new Color(1f, 0.85f, 0.24f), true);
            RetroAudioManager.Instance?.PlayRewardSound();
        }
    }
}

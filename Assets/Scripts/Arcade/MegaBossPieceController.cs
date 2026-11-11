using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Controlador del Jefe Final al Minuto 15:00 bajo Captura por Fases (sin barra de HP):
    /// - "El Coloso Real (Dama Acorazada 2x2)" posee 3 "Sellos de Jaque" protegidos por una Guardia Real de 3 piezas élite.
    /// - Cada pieza de Guardia Real capturada rompe 1 Sello de Jaque.
    /// - Al romperse los 3 Sellos, el Jefe queda vulnerable y puede ser capturado de 1 golpe directo para lograr el Jaque Mate definitivo.
    /// </summary>
    public class MegaBossPieceController : MonoBehaviour
    {
        private enum BossState
        {
            Inactive = 0,
            Intro = 1,
            Recovery = 2,
            Telegraphing = 3,
            Executing = 4,
            Defeated = 5
        }

        private ArcadeSurvivorController _controller;
        private BossState _state = BossState.Inactive;
        private ArcadeBossAttackType _currentAttack = ArcadeBossAttackType.RookCharge;
        private int _attackCycleIndex = 0;

        private ArcadeCoord _anchorCoord = new ArcadeCoord(3, 16);
        private int _remainingCheckSeals = ArcadeLogic.BossTotalCheckSeals;

        private float _stateTimer = 0f;
        private readonly List<ArcadeCoord> _telegraphedTiles = new List<ArcadeCoord>();
        private Vector3 _moveFromWorld;
        private Vector3 _moveToWorld;
        private int _destAnchorX;
        private int _destAnchorY;
        private float _executeDuration = 0.25f;
        private float _executeElapsed = 0f;
        private bool _isLeapArc = false;

        private GameObject _bossObj;
        private SpriteRenderer _bossRenderer;
        private SpriteRenderer _armorHaloRenderer;
        private readonly SpriteRenderer[] _sealOrbs = new SpriteRenderer[ArcadeLogic.BossTotalCheckSeals];
        private readonly List<GameObject> _barrageFX = new List<GameObject>();
        private float _sealOrbitAngle = 0f;

        public bool IsBossActive => _state != BossState.Inactive && _state != BossState.Defeated;
        public bool IsBossDefeated => _state == BossState.Defeated;
        public int RemainingCheckSeals => _remainingCheckSeals;
        public int TotalCheckSeals => ArcadeLogic.BossTotalCheckSeals;
        public bool IsVulnerableToDirectCapture => IsBossActive && _remainingCheckSeals <= 0;
        public ArcadeCoord AnchorCoord => _anchorCoord;

        public void InitializeBossController(ArcadeSurvivorController controller)
        {
            _controller = controller;
        }

        public void ResetBoss()
        {
            _state = BossState.Inactive;
            _remainingCheckSeals = ArcadeLogic.BossTotalCheckSeals;
            _attackCycleIndex = 0;
            _telegraphedTiles.Clear();

            if (_bossObj != null)
            {
                Destroy(_bossObj);
                _bossObj = null;
            }
            ClearBarrageFx();
        }

        public void StartFinalBossBattle()
        {
            if (_controller == null) return;
            ResetBoss();

            int boardH = _controller.BoardExpander != null ? _controller.BoardExpander.CurrentRows : ArcadeLogic.MaxBoardHeight;
            _remainingCheckSeals = ArcadeLogic.BossTotalCheckSeals;

            int preferredY = Mathf.Clamp(boardH - 4, 2, Mathf.Max(2, boardH - 2));
            _anchorCoord = new ArcadeCoord(3, preferredY);
            if (IsTileOccupiedByBoss(_controller.KingCoord.X, _controller.KingCoord.Y))
            {
                _anchorCoord = new ArcadeCoord(3, _controller.KingCoord.Y > boardH / 2 ? 1 : preferredY);
            }

            SpawnBossVisuals();
            SpawnRoyalGuardSquad(boardH);

            _state = BossState.Intro;
            _stateTimer = 1.65f;
        }

        [ContextMenu("Force Trigger Final Boss")]
        public void ForceTriggerBossNow()
        {
            if (_controller == null) return;
            _controller.ForceJumpToBossPhase();
        }

        private void SpawnRoyalGuardSquad(int boardH)
        {
            EnemyWaveSpawner spawner = _controller.EnemySpawner;
            if (spawner == null) return;

            spawner.ClearAllMinorEnemiesKeepGuards();

            // Guardia Real de Élite rodeando al Coloso: Torre, Caballo y Alfil (1 por cada Sello de Jaque)
            ArcadeCoord[] offsets = new ArcadeCoord[]
            {
                new ArcadeCoord(Mathf.Max(0, _anchorCoord.X - 1), Mathf.Clamp(_anchorCoord.Y, 0, boardH - 1)),
                new ArcadeCoord(Mathf.Min(ArcadeLogic.BoardWidth - 1, _anchorCoord.X + 2), Mathf.Clamp(_anchorCoord.Y, 0, boardH - 1)),
                new ArcadeCoord(_anchorCoord.X, Mathf.Max(0, _anchorCoord.Y - 1))
            };

            PieceType[] guardTypes = new PieceType[]
            {
                PieceType.Rook,
                PieceType.Knight,
                PieceType.Bishop
            };

            for (int i = 0; i < 3; i++)
            {
                spawner.SpawnBossGuardPiece(guardTypes[i], offsets[i]);
            }
        }

        public bool IsTileOccupiedByBoss(int tileX, int tileY)
        {
            if (!IsBossActive) return false;
            return ArcadeLogic.IsBossOccupyingTile(_anchorCoord.X, _anchorCoord.Y, tileX, tileY);
        }

        private Vector3 AnchorToCenterWorld(int anchorX, int anchorY)
        {
            Vector3 bl = _controller.CoordToWorld(anchorX, anchorY);
            Vector3 tr = _controller.CoordToWorld(anchorX + 1, anchorY + 1);
            return (bl + tr) * 0.5f;
        }

        private void SpawnBossVisuals()
        {
            if (_bossObj != null) Destroy(_bossObj);

            float tile = _controller.TileSize;
            Vector3 centerWorld = AnchorToCenterWorld(_anchorCoord.X, _anchorCoord.Y);
            _bossObj = new GameObject("MegaBoss_ColosoReal_2x2");
            _bossObj.transform.position = centerWorld + new Vector3(0f, 0f, -0.05f);

            Sprite queenSprite = _controller.GetPieceSprite(PieceType.Queen, PieceColor.Black);
            GameObject visualChild = new GameObject("BossQueenSprite");
            visualChild.transform.SetParent(_bossObj.transform, false);
            _bossRenderer = visualChild.AddComponent<SpriteRenderer>();
            _bossRenderer.sprite = queenSprite != null ? queenSprite : _controller.GetProceduralDiscSprite();
            _bossRenderer.color = queenSprite != null ? new Color(1f, 0.92f, 0.92f, 1f) : new Color(0.18f, 0.05f, 0.08f, 1f);
            _bossRenderer.sortingOrder = 16;

            float targetSpan = 1.92f * tile;
            float uScale = ArcadeSurvivorController.ComputeUniformSpriteScale(_bossRenderer.sprite, targetSpan);
            visualChild.transform.localScale = new Vector3(uScale, uScale, 1f);

            GameObject haloObj = new GameObject("ArmoredSealHalo");
            haloObj.transform.SetParent(_bossObj.transform, false);
            haloObj.transform.localPosition = new Vector3(0f, 0f, 0.06f);
            haloObj.transform.localScale = new Vector3(2.15f * tile, 2.15f * tile, 1f);
            _armorHaloRenderer = haloObj.AddComponent<SpriteRenderer>();
            _armorHaloRenderer.sprite = _controller.GetProceduralDiscSprite();
            _armorHaloRenderer.color = new Color(0.88f, 0.16f, 0.22f, 0.52f);
            _armorHaloRenderer.sortingOrder = 14;

            Sprite shieldSpr = _controller.GetShieldEmblemSprite();
            for (int i = 0; i < ArcadeLogic.BossTotalCheckSeals; i++)
            {
                GameObject sealObj = new GameObject($"CheckSealOrb_{i}");
                sealObj.transform.SetParent(_bossObj.transform, false);
                SpriteRenderer sr = sealObj.AddComponent<SpriteRenderer>();
                sr.sprite = shieldSpr;
                sr.color = new Color(1.0f, 0.30f, 0.34f, 1f);
                sr.sortingOrder = 18;
                float orbScale = ArcadeSurvivorController.ComputeUniformSpriteScale(shieldSpr, 0.44f * tile);
                sealObj.transform.localScale = new Vector3(orbScale, orbScale, 1f);
                _sealOrbs[i] = sr;
            }
        }

        private void Update()
        {
            if (_controller == null || !IsBossActive || _controller.IsPaused || _controller.IsGameOver)
            {
                return;
            }

            float dt = Time.deltaTime;
            int boardH = _controller.BoardExpander != null ? _controller.BoardExpander.CurrentRows : ArcadeLogic.MaxBoardHeight;

            // Garantizar que si toda la Guardia Real ya cayó, los sellos queden en 0
            if (_remainingCheckSeals > 0 && _controller.EnemySpawner != null && _controller.EnemySpawner.CountActiveBossGuards() == 0)
            {
                _remainingCheckSeals = 0;
                OnAllSealsBroken();
            }

            UpdateSealOrbitVisuals(dt);

            if (_armorHaloRenderer != null)
            {
                float pulse = 0.44f + 0.28f * Mathf.Abs(Mathf.Sin(Time.time * 5.2f));
                if (IsVulnerableToDirectCapture)
                {
                    _armorHaloRenderer.color = new Color(1.0f, 0.85f, 0.18f, pulse + 0.15f);
                }
                else
                {
                    _armorHaloRenderer.color = _state == BossState.Telegraphing
                        ? new Color(1f, 0.28f, 0.14f, 0.72f)
                        : new Color(0.86f, 0.14f, 0.24f, pulse);
                }
            }

            // Si el Rey toca directamente el cuerpo 2x2 cuando aún hay Sellos -> recibe impacto
            if (IsTileOccupiedByBoss(_controller.KingCoord.X, _controller.KingCoord.Y))
            {
                if (IsVulnerableToDirectCapture)
                {
                    CaptureBossDefinitiveCheckmate("Rey Blanco");
                    return;
                }
                else
                {
                    _controller.HandleEnemyAttackOnKing(PieceType.Queen, true, out _);
                }
            }

            switch (_state)
            {
                case BossState.Intro:
                case BossState.Recovery:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f)
                    {
                        BeginNextTelegraphedAttack(boardH);
                    }
                    break;

                case BossState.Telegraphing:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f)
                    {
                        ExecuteCurrentAttack();
                    }
                    break;

                case BossState.Executing:
                    UpdateExecutingMotion(dt);
                    break;
            }
        }

        private void UpdateSealOrbitVisuals(float dt)
        {
            _sealOrbitAngle += dt * 2.4f;
            float radius = 1.18f * _controller.TileSize;

            for (int i = 0; i < _sealOrbs.Length; i++)
            {
                SpriteRenderer sr = _sealOrbs[i];
                if (sr == null) continue;

                bool intact = i < _remainingCheckSeals;
                sr.gameObject.SetActive(intact);
                if (!intact) continue;

                float ang = _sealOrbitAngle + (Mathf.PI * 2f * i / Mathf.Max(1, _remainingCheckSeals));
                sr.transform.localPosition = new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, -0.04f);
                float glow = 0.85f + 0.15f * Mathf.Sin(Time.time * 7f + i);
                sr.color = new Color(1.0f * glow, 0.24f * glow, 0.28f * glow, 1f);
            }
        }

        private void BeginNextTelegraphedAttack(int boardH)
        {
            _telegraphedTiles.Clear();

            ArcadeBossAttackType nextType = (ArcadeBossAttackType)(_attackCycleIndex % 3);
            _attackCycleIndex++;
            _currentAttack = nextType;

            float telegraphDur = 0.95f;

            switch (nextType)
            {
                case ArcadeBossAttackType.RookCharge:
                    _telegraphedTiles.AddRange(ArcadeLogic.GetBossRookChargePath(
                        _anchorCoord.X, _anchorCoord.Y, boardH, _controller.KingCoord,
                        out _destAnchorX, out _destAnchorY, out _));
                    telegraphDur = 0.95f;
                    _controller.ShowBanner("⚠ CARGA LINEAL DE TORRE", "Sal del corredor rojo antes de la embestida", new Color(1f, 0.35f, 0.25f), 1.4f);
                    break;

                case ArcadeBossAttackType.KnightLeap:
                    _telegraphedTiles.AddRange(ArcadeLogic.GetBossKnightLeapTiles(
                        _anchorCoord.X, _anchorCoord.Y, boardH, _controller.KingCoord,
                        out _destAnchorX, out _destAnchorY));
                    telegraphDur = 1.05f;
                    _controller.ShowBanner("⚠ SALTO EXPLOSIVO EN L", "Aléjate de la zona de aterrizaje roja", new Color(1f, 0.52f, 0.20f), 1.4f);
                    break;

                case ArcadeBossAttackType.QueenBarrageSummon:
                    _destAnchorX = _anchorCoord.X;
                    _destAnchorY = _anchorCoord.Y;
                    _telegraphedTiles.AddRange(ArcadeLogic.GetBossQueenBarrageTiles(_anchorCoord.X, _anchorCoord.Y, boardH));
                    telegraphDur = 1.15f;
                    _controller.ShowBanner("⚠ RÁFAGA EN 8 DIRECCIONES", "¡Despeja las líneas de Dama!", new Color(0.95f, 0.22f, 0.65f), 1.5f);
                    break;
            }

            Color redTele = new Color(1f, 0.12f, 0.14f, 0.84f);
            for (int i = 0; i < _telegraphedTiles.Count; i++)
            {
                _controller.SetTileTelegraph(_telegraphedTiles[i], redTele, telegraphDur + 0.05f);
            }

            RetroAudioManager.Instance?.PlayCheckAlert();
            _state = BossState.Telegraphing;
            _stateTimer = telegraphDur;
        }

        private void ExecuteCurrentAttack()
        {
            _state = BossState.Executing;
            _executeElapsed = 0f;
            ClearBarrageFx();

            switch (_currentAttack)
            {
                case ArcadeBossAttackType.RookCharge:
                    _moveFromWorld = AnchorToCenterWorld(_anchorCoord.X, _anchorCoord.Y);
                    _moveToWorld = AnchorToCenterWorld(_destAnchorX, _destAnchorY);
                    _anchorCoord = new ArcadeCoord(_destAnchorX, _destAnchorY);
                    _executeDuration = 0.24f;
                    _isLeapArc = false;
                    RetroAudioManager.Instance?.PlayPieceCapture();
                    break;

                case ArcadeBossAttackType.KnightLeap:
                    _moveFromWorld = AnchorToCenterWorld(_anchorCoord.X, _anchorCoord.Y);
                    _moveToWorld = AnchorToCenterWorld(_destAnchorX, _destAnchorY);
                    _anchorCoord = new ArcadeCoord(_destAnchorX, _destAnchorY);
                    _executeDuration = 0.34f;
                    _isLeapArc = true;
                    RetroAudioManager.Instance?.PlayPieceSelect();
                    break;

                case ArcadeBossAttackType.QueenBarrageSummon:
                    _moveFromWorld = AnchorToCenterWorld(_anchorCoord.X, _anchorCoord.Y);
                    _moveToWorld = _moveFromWorld;
                    _executeDuration = 0.35f;
                    _isLeapArc = false;
                    SpawnQueenBarrageFlashFX();
                    ResolveTelegraphedImpactOnKing();
                    _controller.EnemySpawner?.SpawnBossMinions(2);
                    RetroAudioManager.Instance?.PlayPieceCapture();
                    break;
            }
        }

        private void UpdateExecutingMotion(float dt)
        {
            _executeElapsed += dt;
            float t = Mathf.Clamp01(_executeElapsed / Mathf.Max(0.05f, _executeDuration));

            if (_bossObj != null)
            {
                Vector3 pos = Vector3.Lerp(_moveFromWorld, _moveToWorld, t);
                if (_isLeapArc)
                {
                    pos.y += Mathf.Sin(t * Mathf.PI) * 1.45f * _controller.TileSize;
                }
                _bossObj.transform.position = pos + new Vector3(0f, 0f, -0.05f);
            }

            if (_currentAttack == ArcadeBossAttackType.RookCharge && t >= 0.45f && _telegraphedTiles.Count > 0)
            {
                ResolveTelegraphedImpactOnKing();
                _telegraphedTiles.Clear();
            }

            if (t >= 1f)
            {
                if (_currentAttack == ArcadeBossAttackType.KnightLeap && _telegraphedTiles.Count > 0)
                {
                    ResolveTelegraphedImpactOnKing();
                    SpawnKnightImpactRingFX(_moveToWorld);
                    _telegraphedTiles.Clear();
                    RetroAudioManager.Instance?.PlayPieceCapture();
                }

                ClearBarrageFx();
                _state = BossState.Recovery;
                _stateTimer = 1.25f;
            }
        }

        private void ResolveTelegraphedImpactOnKing()
        {
            ArcadeCoord king = _controller.KingCoord;
            for (int i = 0; i < _telegraphedTiles.Count; i++)
            {
                if (_telegraphedTiles[i] == king)
                {
                    _controller.HandleEnemyAttackOnKing(PieceType.Queen, true, out _);
                    break;
                }
            }
        }

        private void SpawnQueenBarrageFlashFX()
        {
            Sprite sq = _controller.GetProceduralSquareSprite();
            Color col = new Color(1f, 0.22f, 0.55f, 0.72f);

            for (int i = 0; i < _telegraphedTiles.Count; i++)
            {
                GameObject fx = new GameObject("BarrageTileFX");
                fx.transform.position = _controller.CoordToWorld(_telegraphedTiles[i]) + new Vector3(0f, 0f, -0.08f);
                fx.transform.localScale = new Vector3(0.85f * _controller.TileSize, 0.85f * _controller.TileSize, 1f);
                SpriteRenderer sr = fx.AddComponent<SpriteRenderer>();
                sr.sprite = sq;
                sr.color = col;
                sr.sortingOrder = 15;
                _barrageFX.Add(fx);
            }
        }

        private void SpawnKnightImpactRingFX(Vector3 centerWorld)
        {
            GameObject ring = new GameObject("KnightBossRingFX");
            ring.transform.position = centerWorld + new Vector3(0f, 0f, -0.08f);
            ring.transform.localScale = new Vector3(3.4f * _controller.TileSize, 3.4f * _controller.TileSize, 1f);
            SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
            sr.sprite = _controller.GetProceduralDiscSprite();
            sr.color = new Color(1f, 0.45f, 0.16f, 0.65f);
            sr.sortingOrder = 15;
            _barrageFX.Add(ring);
        }

        private void ClearBarrageFx()
        {
            for (int i = 0; i < _barrageFX.Count; i++)
            {
                if (_barrageFX[i] != null) Destroy(_barrageFX[i]);
            }
            _barrageFX.Clear();
        }

        public void NotifyGuardCaptured(Vector3 guardPos)
        {
            if (!IsBossActive || _remainingCheckSeals <= 0) return;

            _remainingCheckSeals = Mathf.Max(0, _remainingCheckSeals - 1);
            RetroAudioManager.Instance?.PlayCheckAlert();

            if (_remainingCheckSeals > 0)
            {
                _controller.SpawnFloatingText(guardPos + Vector3.up * 0.5f, "¡SELLO ROTO!", new Color(1f, 0.78f, 0.22f), true);
                _controller.ShowBanner(
                    $"¡SELLO DE JAQUE ROTO! ({_remainingCheckSeals}/{ArcadeLogic.BossTotalCheckSeals} RESTANTES)",
                    "Captura al resto de la Guardia Real alrededor del Coloso",
                    new Color(1.0f, 0.76f, 0.22f),
                    2.6f);
            }
            else
            {
                OnAllSealsBroken();
            }
        }

        private void OnAllSealsBroken()
        {
            RetroAudioManager.Instance?.PlayRewardSound();
            Vector3 bossWorld = _bossObj != null ? _bossObj.transform.position : AnchorToCenterWorld(_anchorCoord.X, _anchorCoord.Y);
            _controller.SpawnFloatingText(bossWorld + Vector3.up * 0.7f, "¡VULNERABLE!", new Color(1f, 0.88f, 0.22f), true);
            _controller.ShowBanner(
                "★ ¡SELLOS ROTOS! ¡CAPTURA AL COLOSO DE UN GOLPE! ★",
                "Tus piezas aliadas o el Rey Blanco pueden dar el Jaque Mate definitivo",
                new Color(1.0f, 0.86f, 0.24f),
                3.6f);
        }

        public void CaptureBossDefinitiveCheckmate(string capturerLabel)
        {
            if (!IsBossActive || !IsVulnerableToDirectCapture) return;

            _state = BossState.Defeated;
            ClearBarrageFx();
            _controller.EnemySpawner?.ClearAllEnemies();
            _controller.RecordEnemyKill(PieceType.Queen, isGuard: true);

            if (_bossObj != null)
            {
                _controller.SpawnFloatingText(_bossObj.transform.position, "¡JAQUE MATE DEFINITIVO!", new Color(1f, 0.86f, 0.24f), true);
                Destroy(_bossObj);
                _bossObj = null;
            }

            RetroAudioManager.Instance?.PlayPieceCapture();
            _controller.HandleBossDefeated();
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    [Serializable]
    public struct ArcadeCoord : IEquatable<ArcadeCoord>
    {
        public int X;
        public int Y;

        public ArcadeCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool IsValid => ArcadeLogic.IsInBoard(X, Y, ArcadeLogic.MaxBoardHeight);

        public bool IsValidIn(int boardHeight)
        {
            return ArcadeLogic.IsInBoard(X, Y, boardHeight);
        }

        public int ManhattanDistanceTo(ArcadeCoord other)
        {
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        }

        public int ChebyshevDistanceTo(ArcadeCoord other)
        {
            return Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));
        }

        public float SqrDistanceTo(ArcadeCoord other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            return dx * dx + dy * dy;
        }

        public bool Equals(ArcadeCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is ArcadeCoord other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public static bool operator ==(ArcadeCoord a, ArcadeCoord b) => a.Equals(b);
        public static bool operator !=(ArcadeCoord a, ArcadeCoord b) => !a.Equals(b);
        public override string ToString() => $"({X},{Y})";
    }

    public enum ArcadeMasterId
    {
        Petrosian = 0,
        Tal = 1,
        Polgar = 2,
        Kasparov = 3
    }

    public enum ArcadeModifierId
    {
        None = 0,
        PawnAdvanceBoost = 1,       // Acelera Peones + pasos tácticos
        KnightAssaultBoost = 2,     // Caballeros más rápidos + paso ortogonal
        TacticalTempoBoost = 3,     // Reduce tiempo de turno global de piezas aliadas
        FastReconstruction = 4,     // Reduce Reconstrucción de 4.0s a 2.2s y recarga Escudo Dorado
        MagnetPickupBoost = 5,      // Aumenta alcance de recogida de gemas EXP
        RoyalShieldRepair = 6       // Restaura +1 Escudo Real del Rey Blanco (máx. 3)
    }

    public enum ArcadeUpgradeKind
    {
        SummonAllyPiece = 0,
        AllyModifierCard = 1,
        MasterCard = 2
    }

    public enum ArcadeSynergyId
    {
        PiercingCaptureSynergy = 0, // Judit Polgár + Torre / Alfil / Dama
        ChainLeapSynergy = 1,       // Mijaíl Tal + Caballo
        IronPhalanxSynergy = 2,     // Tigrán Petrosián + Peones / Torres
        BlitzOverdriveSynergy = 3   // Garry Kaspárov + Aliados en tablero
    }

    public enum ArcadeBossAttackType
    {
        RookCharge = 0,
        KnightLeap = 1,
        QueenBarrageSummon = 2
    }

    [Serializable]
    public struct CardSynergyState
    {
        public bool PiercingCaptureSynergy;
        public bool ChainLeapSynergy;
        public bool IronPhalanxSynergy;
        public bool BlitzOverdriveSynergy;

        public int ActiveCount
        {
            get
            {
                int c = 0;
                if (PiercingCaptureSynergy) c++;
                if (ChainLeapSynergy) c++;
                if (IronPhalanxSynergy) c++;
                if (BlitzOverdriveSynergy) c++;
                return c;
            }
        }
    }

    public class ArcadeMoveOption
    {
        public ArcadeCoord From;
        public ArcadeCoord To;
        public readonly List<ArcadeCoord> CapturedCoords = new List<ArcadeCoord>();
        public bool CapturesBoss;
        public bool CapturesGuard;
        public int TotalCaptureValue;
        public float EvaluationScore;
    }

    [Serializable]
    public class ArcadeUpgradeChoice
    {
        public ArcadeUpgradeKind Kind;
        public PieceType AllyPieceType;
        public ArcadeModifierId ModifierId;
        public ArcadeMasterId MasterId;
        public string Title;
        public string Subtitle;
        public string Description;
        public string BadgeText;
        public bool HighlightsSynergy;
        public Color AccentColor;
    }

    [Serializable]
    public class ArcadeRunStats
    {
        public float ElapsedSeconds;
        public float BossFightSeconds;
        public int FinalLevel = 1;
        public int CurrentBoardRows = ArcadeLogic.InitialBoardHeight;
        public int DestroyedPieces;
        public int PawnsKilled;
        public int KnightsKilled;
        public int BishopsKilled;
        public int RooksKilled;
        public int QueensKilled;
        public int BossGuardsKilled;
        public int TotalExpCollected;
        public int BlockedAttacksByShield;
        public int AllyReconstructions;
        public bool BossDefeated;
        public readonly List<string> UnlockedSynergyNames = new List<string>();
        public readonly List<ArcadeMasterId> OwnedMasters = new List<ArcadeMasterId>();

        public void RecordKill(PieceType pieceType, bool isGuard)
        {
            DestroyedPieces++;
            if (isGuard) BossGuardsKilled++;

            switch (pieceType)
            {
                case PieceType.Pawn:   PawnsKilled++; break;
                case PieceType.Knight: KnightsKilled++; break;
                case PieceType.Bishop: BishopsKilled++; break;
                case PieceType.Rook:   RooksKilled++; break;
                case PieceType.Queen:  QueensKilled++; break;
            }
        }
    }

    /// <summary>
    /// Reglas y cálculos deterministas en C# puro para el Modo Arcade Puro de Ajedrez:
    /// - Tablero inicial 8x8 con expansión +3 filas cada 210s (3:30) hasta 8x20.
    /// - Captura instantánea de 1 solo movimiento (sin barras de vida).
    /// - IA aliada con trayectorias reales y sinergias de Gran Maestro.
    /// - Jefe Final con 3 Sellos de Jaque y Guardia Real de élite.
    /// </summary>
    public static class ArcadeLogic
    {
        public const int BoardWidth = 8;
        public const int InitialBoardHeight = 8;
        public const int ExpansionRowDelta = 3;
        public const int MaxExpansions = 4;
        public const int MaxBoardHeight = InitialBoardHeight + ExpansionRowDelta * MaxExpansions; // 20 filas (8x20)

        public const float ExpansionIntervalSeconds = 210f; // 3 minutos 30 segundos (03:30, 07:00, 10:30, 14:00)
        public const float SurvivalDurationSeconds = 900f;  // 15:00
        public const float EnemyTelegraphSeconds = 0.50f;
        public const float MoveCooldownSeconds = 0.35f;

        public const int BaseRoyalShields = 3;
        public const float BaseAllyTurnInterval = 1.2f;
        public const float AllyReconstructionSeconds = 4.0f;
        public const int BossTotalCheckSeals = 3;

        public static readonly ArcadeCoord[] KnightOffsets = new ArcadeCoord[]
        {
            new ArcadeCoord(1, 2), new ArcadeCoord(2, 1),
            new ArcadeCoord(2, -1), new ArcadeCoord(1, -2),
            new ArcadeCoord(-1, -2), new ArcadeCoord(-2, -1),
            new ArcadeCoord(-2, 1), new ArcadeCoord(-1, 2)
        };

        public static readonly ArcadeCoord[] DiagonalDirs = new ArcadeCoord[]
        {
            new ArcadeCoord(1, 1),
            new ArcadeCoord(-1, 1),
            new ArcadeCoord(1, -1),
            new ArcadeCoord(-1, -1)
        };

        public static readonly ArcadeCoord[] CardinalDirs = new ArcadeCoord[]
        {
            new ArcadeCoord(0, 1),
            new ArcadeCoord(0, -1),
            new ArcadeCoord(1, 0),
            new ArcadeCoord(-1, 0)
        };

        public static bool IsInBoard(int x, int y, int boardHeight)
        {
            int h = Math.Max(InitialBoardHeight, Math.Min(MaxBoardHeight, boardHeight));
            return x >= 0 && x < BoardWidth && y >= 0 && y < h;
        }

        public static int GetExpansionCountForElapsedTime(float elapsedSeconds)
        {
            if (elapsedSeconds < 0f) return 0;
            int count = (int)Math.Floor((elapsedSeconds + 0.0001f) / ExpansionIntervalSeconds);
            return Math.Max(0, Math.Min(MaxExpansions, count));
        }

        public static int GetBoardHeightForElapsedTime(float elapsedSeconds)
        {
            return InitialBoardHeight + GetExpansionCountForElapsedTime(elapsedSeconds) * ExpansionRowDelta;
        }

        public static string FormatClockMMSS(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = (int)Math.Floor(seconds + 0.0001f);
            int mins = total / 60;
            int secs = total % 60;
            return $"{mins:00}:{secs:00}";
        }

        public static int GetExpRequiredForNextLevel(int currentLevel)
        {
            if (currentLevel < 1) currentLevel = 1;
            int tierBonus = ((currentLevel - 1) / 3) * 3;
            return 4 + (currentLevel - 1) * 3 + tierBonus;
        }

        public static int GetExpValueForPiece(PieceType type)
        {
            int val = PieceValueTable.GetValue(type);
            return val > 0 ? val : 1;
        }

        public static ArcadeCoord ComputeKingStepDirection(ArcadeCoord from, ArcadeCoord target)
        {
            return new ArcadeCoord(Math.Sign(target.X - from.X), Math.Sign(target.Y - from.Y));
        }

        public static ArcadeCoord ComputeSwipeStepDirection(float deltaX, float deltaY, float threshold)
        {
            float distSq = deltaX * deltaX + deltaY * deltaY;
            if (distSq < threshold * threshold)
            {
                return new ArcadeCoord(0, 0);
            }

            double angle = Math.Atan2(deltaY, deltaX) * (180.0 / Math.PI);
            int sector = (int)Math.Round(angle / 45.0);
            double rad = sector * 45.0 * (Math.PI / 180.0);
            return new ArcadeCoord((int)Math.Round(Math.Cos(rad)), (int)Math.Round(Math.Sin(rad)));
        }

        public static CardSynergyState EvaluateSynergies(
            bool hasPetrosian,
            bool hasTal,
            bool hasPolgar,
            bool hasKasparov,
            IReadOnlyList<PieceType> activeAllyTypes)
        {
            bool hasPawnOrRook = false;
            bool hasKnight = false;
            bool hasLineSlider = false;
            bool hasAnyAlly = activeAllyTypes != null && activeAllyTypes.Count > 0;

            if (activeAllyTypes != null)
            {
                for (int i = 0; i < activeAllyTypes.Count; i++)
                {
                    PieceType pt = activeAllyTypes[i];
                    if (pt == PieceType.Pawn || pt == PieceType.Rook) hasPawnOrRook = true;
                    if (pt == PieceType.Knight) hasKnight = true;
                    if (pt == PieceType.Rook || pt == PieceType.Bishop || pt == PieceType.Queen) hasLineSlider = true;
                }
            }

            return new CardSynergyState
            {
                PiercingCaptureSynergy = hasPolgar && hasLineSlider,
                ChainLeapSynergy = hasTal && hasKnight,
                IronPhalanxSynergy = hasPetrosian && hasPawnOrRook,
                BlitzOverdriveSynergy = hasKasparov && hasAnyAlly
            };
        }

        public static string GetSynergyDisplayName(ArcadeSynergyId id)
        {
            switch (id)
            {
                case ArcadeSynergyId.PiercingCaptureSynergy: return "Barrido Implacable (Polgár)";
                case ArcadeSynergyId.ChainLeapSynergy:       return "Doble Salto Encadenado (Tal)";
                case ArcadeSynergyId.IronPhalanxSynergy:     return "Falange de Hierro (Petrosián)";
                case ArcadeSynergyId.BlitzOverdriveSynergy:  return "Sobrecarga Blitz x2 (Kaspárov)";
                default:                                     return id.ToString();
            }
        }

        public static string GetMasterDisplayName(ArcadeMasterId master)
        {
            switch (master)
            {
                case ArcadeMasterId.Petrosian: return "Tigrán Petrosián";
                case ArcadeMasterId.Tal:       return "Mijaíl Tal";
                case ArcadeMasterId.Polgar:    return "Judit Polgár";
                case ArcadeMasterId.Kasparov:  return "Garry Kaspárov";
                default:                       return master.ToString();
            }
        }

        public static string GetPieceDisplayNameEs(PieceType type)
        {
            switch (type)
            {
                case PieceType.Pawn:   return "Peón Aliado";
                case PieceType.Knight: return "Caballo Aliado";
                case PieceType.Bishop: return "Alfil Aliado";
                case PieceType.Rook:   return "Torre Aliada";
                case PieceType.Queen:  return "Dama Aliada";
                case PieceType.King:   return "Rey Blanco";
                default:               return type.ToString();
            }
        }

        public static string GetWavePhaseTitle(float elapsedSeconds, bool bossActive)
        {
            if (bossActive || elapsedSeconds >= SurvivalDurationSeconds)
                return "JEFE FINAL · 3 SELLOS DE JAQUE";
            if (elapsedSeconds >= 630f) // 10:30
                return "OLEADA IV · CORONACIÓN OSCURA";
            if (elapsedSeconds >= 420f) // 07:00
                return "OLEADA III · ALFILES & TORRES";
            if (elapsedSeconds >= 210f) // 03:30
                return "OLEADA II · PEONES & CABALLOS";
            return "OLEADA I · ASEDIO INICIAL";
        }

        public static List<PieceType> GetAllowedEnemyTypes(float elapsedSeconds)
        {
            var list = new List<PieceType>();
            if (elapsedSeconds < 210f)
            {
                list.Add(PieceType.Pawn);
            }
            else if (elapsedSeconds < 420f)
            {
                list.Add(PieceType.Pawn);
                list.Add(PieceType.Pawn);
                list.Add(PieceType.Knight);
            }
            else if (elapsedSeconds < 630f)
            {
                list.Add(PieceType.Pawn);
                list.Add(PieceType.Knight);
                list.Add(PieceType.Bishop);
                list.Add(PieceType.Rook);
            }
            else
            {
                list.Add(PieceType.Knight);
                list.Add(PieceType.Bishop);
                list.Add(PieceType.Bishop);
                list.Add(PieceType.Rook);
                list.Add(PieceType.Rook);
                if (elapsedSeconds >= 720f)
                {
                    list.Add(PieceType.Queen);
                }
            }
            return list;
        }

        public static float GetEnemySpawnInterval(float elapsedSeconds)
        {
            float progress = Math.Max(0f, Math.Min(1f, elapsedSeconds / SurvivalDurationSeconds));
            return 1.75f - (1.15f * progress);
        }

        public static int GetMaxConcurrentEnemies(float elapsedSeconds, int boardExpansionStage)
        {
            int expansionBonus = Math.Max(0, boardExpansionStage) * 6;
            int timeBase;
            if (elapsedSeconds < 210f)
                timeBase = 7 + (int)(elapsedSeconds / 40f);
            else if (elapsedSeconds < 420f)
                timeBase = 12 + (int)((elapsedSeconds - 210f) / 35f);
            else if (elapsedSeconds < 630f)
                timeBase = 17 + (int)((elapsedSeconds - 420f) / 35f);
            else
                timeBase = 21 + (int)((elapsedSeconds - 630f) / 40f);

            return timeBase + expansionBonus;
        }

        public static List<ArcadeCoord> GetEdgeSpawnCandidates(int boardHeight)
        {
            int h = Math.Max(InitialBoardHeight, Math.Min(MaxBoardHeight, boardHeight));
            var edges = new List<ArcadeCoord>();
            for (int x = 0; x < BoardWidth; x++)
            {
                edges.Add(new ArcadeCoord(x, h - 1));
                edges.Add(new ArcadeCoord(x, 0));
            }
            for (int y = 1; y < h - 1; y++)
            {
                edges.Add(new ArcadeCoord(0, y));
                edges.Add(new ArcadeCoord(BoardWidth - 1, y));
            }
            return edges;
        }

        public static bool IsValidBossAnchor(int anchorX, int anchorY, int boardHeight)
        {
            int h = Math.Max(InitialBoardHeight, Math.Min(MaxBoardHeight, boardHeight));
            return anchorX >= 0 && anchorX <= BoardWidth - 2 &&
                   anchorY >= 0 && anchorY <= h - 2;
        }

        public static bool IsBossOccupyingTile(int bossAnchorX, int bossAnchorY, int tileX, int tileY)
        {
            return tileX >= bossAnchorX && tileX <= bossAnchorX + 1 &&
                   tileY >= bossAnchorY && tileY <= bossAnchorY + 1;
        }

        /// <summary>
        /// Evalúa todos los movimientos legales de una pieza aliada y escoge la jugada óptima:
        /// 1. Capturar Jefe vulnerable / Guardia Real / Enemigo de mayor valor o más cercano al Rey Blanco.
        /// 2. Aprovechar PiercingCaptureSynergy (Polgár) barriendo múltiples enemigos en línea/diagonal.
        /// 3. Si no hay captura inmediata, acercarse tácticamente hacia las amenazas más cercanas al Rey.
        /// </summary>
        public static ArcadeMoveOption EvaluateBestAllyMove(
            PieceType pieceType,
            ArcadeCoord from,
            int boardHeight,
            ArcadeCoord kingCoord,
            Func<ArcadeCoord, bool> isFriendlyAt,
            Func<ArcadeCoord, int> getEnemyValueAt,
            Func<ArcadeCoord, bool> isGuardAt,
            bool bossActive,
            int bossAnchorX,
            int bossAnchorY,
            bool bossVulnerable,
            bool hasPiercingSynergy,
            bool hasChainLeapSynergy,
            bool pawnAdvanceBoost,
            bool knightAssaultBoost)
        {
            var options = GenerateAllyLegalMoves(
                pieceType, from, boardHeight, kingCoord,
                isFriendlyAt, getEnemyValueAt, isGuardAt,
                bossActive, bossAnchorX, bossAnchorY, bossVulnerable,
                hasPiercingSynergy, pawnAdvanceBoost, knightAssaultBoost);

            if (options.Count == 0) return null;

            ArcadeMoveOption best = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < options.Count; i++)
            {
                ArcadeMoveOption opt = options[i];
                float score = 0f;

                if (opt.TotalCaptureValue > 0 || opt.CapturesBoss)
                {
                    score += 1000f + opt.TotalCaptureValue * 120f;
                    if (opt.CapturesBoss) score += 100000f;
                    if (opt.CapturesGuard) score += 600f;

                    // Priorizar al enemigo capturado más cercano al Rey Blanco como desempate táctico
                    float minDistToKing = float.MaxValue;
                    for (int c = 0; c < opt.CapturedCoords.Count; c++)
                    {
                        float d = opt.CapturedCoords[c].SqrDistanceTo(kingCoord);
                        if (d < minDistToKing) minDistToKing = d;
                    }
                    if (minDistToKing < float.MaxValue)
                    {
                        score += Math.Max(0f, 60f - minDistToKing * 2.5f);
                    }

                    // Bonus para Caballo con ChainLeapSynergy si tras aterrizar tiene otro enemigo en L
                    if (pieceType == PieceType.Knight && hasChainLeapSynergy)
                    {
                        if (HasFollowUpKnightCapture(opt.To, opt.From, opt.CapturedCoords, boardHeight, isFriendlyAt, getEnemyValueAt, bossActive, bossAnchorX, bossAnchorY, bossVulnerable))
                        {
                            score += 90f;
                        }
                    }
                }
                else
                {
                    // Movimiento posicional: acercar la pieza al enemigo/guardia/jefe o proteger al Rey
                    ArcadeCoord focalTarget = FindBestApproachTarget(boardHeight, kingCoord, from, getEnemyValueAt, isGuardAt, bossActive, bossAnchorX, bossAnchorY);
                    float distAfter = opt.To.SqrDistanceTo(focalTarget);
                    float distBefore = from.SqrDistanceTo(focalTarget);
                    score += (distBefore - distAfter) * 14f;

                    // Peón gana incentivo al avanzar hacia coronación en el tope
                    if (pieceType == PieceType.Pawn)
                    {
                        score += (opt.To.Y - from.Y) * 8f;
                    }

                    // Mantener proximidad saludable alrededor del Rey si no hay amenazas claras
                    score -= opt.To.ManhattanDistanceTo(kingCoord) * 0.4f;
                }

                opt.EvaluationScore = score;
                if (best == null || score > bestScore)
                {
                    best = opt;
                    bestScore = score;
                }
            }

            return best;
        }

        private static ArcadeCoord FindBestApproachTarget(
            int boardHeight,
            ArcadeCoord kingCoord,
            ArcadeCoord allyFrom,
            Func<ArcadeCoord, int> getEnemyValueAt,
            Func<ArcadeCoord, bool> isGuardAt,
            bool bossActive,
            int bossAnchorX,
            int bossAnchorY)
        {
            if (bossActive)
            {
                // Si el jefe está activo, buscar guardias reales o su centro
                ArcadeCoord bestGuard = default;
                float guardScore = float.NegativeInfinity;
                bool foundGuard = false;
                for (int x = 0; x < BoardWidth; x++)
                {
                    for (int y = 0; y < boardHeight; y++)
                    {
                        ArcadeCoord c = new ArcadeCoord(x, y);
                        if (isGuardAt != null && isGuardAt(c))
                        {
                            float s = -c.SqrDistanceTo(allyFrom);
                            if (!foundGuard || s > guardScore)
                            {
                                foundGuard = true;
                                guardScore = s;
                                bestGuard = c;
                            }
                        }
                    }
                }
                if (foundGuard) return bestGuard;
                return new ArcadeCoord(bossAnchorX, bossAnchorY);
            }

            ArcadeCoord bestEnemy = kingCoord;
            float bestPriority = float.NegativeInfinity;

            for (int x = 0; x < BoardWidth; x++)
            {
                for (int y = 0; y < boardHeight; y++)
                {
                    ArcadeCoord c = new ArcadeCoord(x, y);
                    int val = getEnemyValueAt != null ? getEnemyValueAt(c) : 0;
                    if (val <= 0) continue;

                    float priority = val * 12f - c.ManhattanDistanceTo(kingCoord) * 2.5f - c.ManhattanDistanceTo(allyFrom) * 0.6f;
                    if (priority > bestPriority)
                    {
                        bestPriority = priority;
                        bestEnemy = c;
                    }
                }
            }

            return bestEnemy;
        }

        private static bool HasFollowUpKnightCapture(
            ArcadeCoord pivot,
            ArcadeCoord ignoreOrig,
            List<ArcadeCoord> alreadyCaptured,
            int boardHeight,
            Func<ArcadeCoord, bool> isFriendlyAt,
            Func<ArcadeCoord, int> getEnemyValueAt,
            bool bossActive,
            int bossAnchorX,
            int bossAnchorY,
            bool bossVulnerable)
        {
            for (int i = 0; i < KnightOffsets.Length; i++)
            {
                ArcadeCoord c = new ArcadeCoord(pivot.X + KnightOffsets[i].X, pivot.Y + KnightOffsets[i].Y);
                if (!c.IsValidIn(boardHeight)) continue;
                if (c != ignoreOrig && isFriendlyAt != null && isFriendlyAt(c)) continue;
                if (alreadyCaptured != null && alreadyCaptured.Contains(c)) continue;

                if (bossActive && bossVulnerable && IsBossOccupyingTile(bossAnchorX, bossAnchorY, c.X, c.Y))
                    return true;
                if (getEnemyValueAt != null && getEnemyValueAt(c) > 0)
                    return true;
            }
            return false;
        }

        public static ArcadeCoord? GetKnightChainCaptureTarget(
            ArcadeCoord from,
            int boardHeight,
            ArcadeCoord kingCoord,
            Func<ArcadeCoord, bool> isFriendlyAt,
            Func<ArcadeCoord, int> getEnemyValueAt,
            Func<ArcadeCoord, bool> isGuardAt,
            bool bossActive,
            int bossAnchorX,
            int bossAnchorY,
            bool bossVulnerable)
        {
            ArcadeCoord? best = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < KnightOffsets.Length; i++)
            {
                ArcadeCoord c = new ArcadeCoord(from.X + KnightOffsets[i].X, from.Y + KnightOffsets[i].Y);
                if (!c.IsValidIn(boardHeight)) continue;
                if (isFriendlyAt != null && isFriendlyAt(c)) continue;

                bool hitBoss = bossActive && IsBossOccupyingTile(bossAnchorX, bossAnchorY, c.X, c.Y);
                if (hitBoss && !bossVulnerable) continue;

                int val = getEnemyValueAt != null ? getEnemyValueAt(c) : 0;
                if (!hitBoss && val <= 0) continue;

                bool hitGuard = isGuardAt != null && isGuardAt(c);
                float score = (hitBoss ? 100000f : val * 120f) + (hitGuard ? 600f : 0f) - c.SqrDistanceTo(kingCoord);
                if (!best.HasValue || score > bestScore)
                {
                    best = c;
                    bestScore = score;
                }
            }

            return best;
        }

        public static List<ArcadeMoveOption> GenerateAllyLegalMoves(
            PieceType pieceType,
            ArcadeCoord from,
            int boardHeight,
            ArcadeCoord kingCoord,
            Func<ArcadeCoord, bool> isFriendlyAt,
            Func<ArcadeCoord, int> getEnemyValueAt,
            Func<ArcadeCoord, bool> isGuardAt,
            bool bossActive,
            int bossAnchorX,
            int bossAnchorY,
            bool bossVulnerable,
            bool hasPiercingSynergy,
            bool pawnAdvanceBoost,
            bool knightAssaultBoost)
        {
            var list = new List<ArcadeMoveOption>();

            switch (pieceType)
            {
                case PieceType.Pawn:
                {
                    // Capturas diagonales frontales (+1 Y)
                    ArcadeCoord[] fwdCaps = new ArcadeCoord[]
                    {
                        new ArcadeCoord(from.X - 1, from.Y + 1),
                        new ArcadeCoord(from.X + 1, from.Y + 1)
                    };
                    TryAddPawnCaptureOptions(fwdCaps, from, boardHeight, isFriendlyAt, getEnemyValueAt, isGuardAt, bossActive, bossAnchorX, bossAnchorY, bossVulnerable, list);

                    if (pawnAdvanceBoost)
                    {
                        ArcadeCoord[] backCaps = new ArcadeCoord[]
                        {
                            new ArcadeCoord(from.X - 1, from.Y - 1),
                            new ArcadeCoord(from.X + 1, from.Y - 1)
                        };
                        TryAddPawnCaptureOptions(backCaps, from, boardHeight, isFriendlyAt, getEnemyValueAt, isGuardAt, bossActive, bossAnchorX, bossAnchorY, bossVulnerable, list);
                    }

                    // Avance ortogonal de Peón (+1 Y, o +2 si Boost) si casilla libre
                    ArcadeCoord step1 = new ArcadeCoord(from.X, from.Y + 1);
                    if (step1.IsValidIn(boardHeight) && IsEmptySquare(step1, isFriendlyAt, getEnemyValueAt, bossActive, bossAnchorX, bossAnchorY))
                    {
                        list.Add(new ArcadeMoveOption { From = from, To = step1 });
                        if (pawnAdvanceBoost)
                        {
                            ArcadeCoord step2 = new ArcadeCoord(from.X, from.Y + 2);
                            if (step2.IsValidIn(boardHeight) && IsEmptySquare(step2, isFriendlyAt, getEnemyValueAt, bossActive, bossAnchorX, bossAnchorY))
                            {
                                list.Add(new ArcadeMoveOption { From = from, To = step2 });
                            }
                        }
                    }

                    if (pawnAdvanceBoost)
                    {
                        ArcadeCoord left = new ArcadeCoord(from.X - 1, from.Y);
                        ArcadeCoord right = new ArcadeCoord(from.X + 1, from.Y);
                        if (left.IsValidIn(boardHeight) && IsEmptySquare(left, isFriendlyAt, getEnemyValueAt, bossActive, bossAnchorX, bossAnchorY))
                            list.Add(new ArcadeMoveOption { From = from, To = left });
                        if (right.IsValidIn(boardHeight) && IsEmptySquare(right, isFriendlyAt, getEnemyValueAt, bossActive, bossAnchorX, bossAnchorY))
                            list.Add(new ArcadeMoveOption { From = from, To = right });
                    }
                    break;
                }

                case PieceType.Knight:
                {
                    for (int i = 0; i < KnightOffsets.Length; i++)
                    {
                        ArcadeCoord to = new ArcadeCoord(from.X + KnightOffsets[i].X, from.Y + KnightOffsets[i].Y);
                        if (!to.IsValidIn(boardHeight)) continue;
                        if (isFriendlyAt != null && isFriendlyAt(to)) continue;

                        bool hitBoss = bossActive && IsBossOccupyingTile(bossAnchorX, bossAnchorY, to.X, to.Y);
                        if (hitBoss && !bossVulnerable) continue;

                        var opt = new ArcadeMoveOption { From = from, To = to };
                        if (hitBoss)
                        {
                            opt.CapturesBoss = true;
                            opt.TotalCaptureValue = 1000;
                            opt.CapturedCoords.Add(to);
                        }
                        else
                        {
                            int val = getEnemyValueAt != null ? getEnemyValueAt(to) : 0;
                            if (val > 0)
                            {
                                opt.CapturedCoords.Add(to);
                                opt.TotalCaptureValue = val;
                                opt.CapturesGuard = isGuardAt != null && isGuardAt(to);
                            }
                        }
                        list.Add(opt);
                    }

                    if (knightAssaultBoost)
                    {
                        for (int i = 0; i < CardinalDirs.Length; i++)
                        {
                            ArcadeCoord step = new ArcadeCoord(from.X + CardinalDirs[i].X, from.Y + CardinalDirs[i].Y);
                            if (step.IsValidIn(boardHeight) && IsEmptySquare(step, isFriendlyAt, getEnemyValueAt, bossActive, bossAnchorX, bossAnchorY))
                            {
                                list.Add(new ArcadeMoveOption { From = from, To = step });
                            }
                        }
                    }
                    break;
                }

                case PieceType.Bishop:
                    GenerateSlidingRayOptions(from, boardHeight, DiagonalDirs, isFriendlyAt, getEnemyValueAt, isGuardAt, bossActive, bossAnchorX, bossAnchorY, bossVulnerable, hasPiercingSynergy, list);
                    break;

                case PieceType.Rook:
                    GenerateSlidingRayOptions(from, boardHeight, CardinalDirs, isFriendlyAt, getEnemyValueAt, isGuardAt, bossActive, bossAnchorX, bossAnchorY, bossVulnerable, hasPiercingSynergy, list);
                    break;

                case PieceType.Queen:
                    GenerateSlidingRayOptions(from, boardHeight, CardinalDirs, isFriendlyAt, getEnemyValueAt, isGuardAt, bossActive, bossAnchorX, bossAnchorY, bossVulnerable, hasPiercingSynergy, list);
                    GenerateSlidingRayOptions(from, boardHeight, DiagonalDirs, isFriendlyAt, getEnemyValueAt, isGuardAt, bossActive, bossAnchorX, bossAnchorY, bossVulnerable, hasPiercingSynergy, list);
                    break;
            }

            return list;
        }

        private static bool IsEmptySquare(
            ArcadeCoord c,
            Func<ArcadeCoord, bool> isFriendlyAt,
            Func<ArcadeCoord, int> getEnemyValueAt,
            bool bossActive,
            int bossAnchorX,
            int bossAnchorY)
        {
            if (isFriendlyAt != null && isFriendlyAt(c)) return false;
            if (getEnemyValueAt != null && getEnemyValueAt(c) > 0) return false;
            if (bossActive && IsBossOccupyingTile(bossAnchorX, bossAnchorY, c.X, c.Y)) return false;
            return true;
        }

        private static void TryAddPawnCaptureOptions(
            ArcadeCoord[] coords,
            ArcadeCoord from,
            int boardHeight,
            Func<ArcadeCoord, bool> isFriendlyAt,
            Func<ArcadeCoord, int> getEnemyValueAt,
            Func<ArcadeCoord, bool> isGuardAt,
            bool bossActive,
            int bossAnchorX,
            int bossAnchorY,
            bool bossVulnerable,
            List<ArcadeMoveOption> list)
        {
            for (int i = 0; i < coords.Length; i++)
            {
                ArcadeCoord to = coords[i];
                if (!to.IsValidIn(boardHeight)) continue;
                if (isFriendlyAt != null && isFriendlyAt(to)) continue;

                bool hitBoss = bossActive && IsBossOccupyingTile(bossAnchorX, bossAnchorY, to.X, to.Y);
                if (hitBoss && bossVulnerable)
                {
                    var opt = new ArcadeMoveOption { From = from, To = to, CapturesBoss = true, TotalCaptureValue = 1000 };
                    opt.CapturedCoords.Add(to);
                    list.Add(opt);
                    continue;
                }

                int val = getEnemyValueAt != null ? getEnemyValueAt(to) : 0;
                if (val > 0)
                {
                    var opt = new ArcadeMoveOption
                    {
                        From = from,
                        To = to,
                        TotalCaptureValue = val,
                        CapturesGuard = isGuardAt != null && isGuardAt(to)
                    };
                    opt.CapturedCoords.Add(to);
                    list.Add(opt);
                }
            }
        }

        private static void GenerateSlidingRayOptions(
            ArcadeCoord from,
            int boardHeight,
            ArcadeCoord[] dirs,
            Func<ArcadeCoord, bool> isFriendlyAt,
            Func<ArcadeCoord, int> getEnemyValueAt,
            Func<ArcadeCoord, bool> isGuardAt,
            bool bossActive,
            int bossAnchorX,
            int bossAnchorY,
            bool bossVulnerable,
            bool piercing,
            List<ArcadeMoveOption> results)
        {
            for (int d = 0; d < dirs.Length; d++)
            {
                int dx = dirs[d].X;
                int dy = dirs[d].Y;
                var sweptCoords = new List<ArcadeCoord>();
                int cumulativeVal = 0;
                bool cumulativeGuard = false;

                int step = 1;
                while (true)
                {
                    ArcadeCoord c = new ArcadeCoord(from.X + dx * step, from.Y + dy * step);
                    if (!c.IsValidIn(boardHeight)) break;
                    if (isFriendlyAt != null && isFriendlyAt(c)) break;

                    bool hitBoss = bossActive && IsBossOccupyingTile(bossAnchorX, bossAnchorY, c.X, c.Y);
                    if (hitBoss)
                    {
                        if (bossVulnerable)
                        {
                            var opt = new ArcadeMoveOption
                            {
                                From = from,
                                To = c,
                                CapturesBoss = true,
                                CapturesGuard = cumulativeGuard,
                                TotalCaptureValue = cumulativeVal + 1000
                            };
                            opt.CapturedCoords.AddRange(sweptCoords);
                            opt.CapturedCoords.Add(c);
                            results.Add(opt);
                        }
                        break;
                    }

                    int enemyVal = getEnemyValueAt != null ? getEnemyValueAt(c) : 0;
                    if (enemyVal > 0)
                    {
                        sweptCoords.Add(c);
                        cumulativeVal += enemyVal;
                        if (isGuardAt != null && isGuardAt(c)) cumulativeGuard = true;

                        var capOpt = new ArcadeMoveOption
                        {
                            From = from,
                            To = c,
                            CapturesGuard = cumulativeGuard,
                            TotalCaptureValue = cumulativeVal
                        };
                        capOpt.CapturedCoords.AddRange(sweptCoords);
                        results.Add(capOpt);

                        if (!piercing)
                        {
                            // Sin Sinergia de Barrido (Polgár), se detiene en el primer enemigo capturado
                            break;
                        }
                    }
                    else if (sweptCoords.Count == 0)
                    {
                        results.Add(new ArcadeMoveOption { From = from, To = c });
                    }

                    step++;
                }
            }
        }

        /// <summary>
        /// Movimiento táctico de enemigos negros hacia el Rey Blanco o piezas aliadas en su camino.
        /// </summary>
        public static ArcadeCoord ChooseEnemyNextStep(
            PieceType type,
            ArcadeCoord from,
            int boardHeight,
            ArcadeCoord targetKing,
            Func<ArcadeCoord, bool> isEnemyOccupied,
            Func<ArcadeCoord, bool> isAllyAt)
        {
            if (from == targetKing) return from;

            var candidates = new List<ArcadeCoord>();

            switch (type)
            {
                case PieceType.Pawn:
                {
                    // Captura diagonal de Peón hacia cualquier blanco (Rey o Aliado) adyacente en diagonal
                    for (int i = 0; i < DiagonalDirs.Length; i++)
                    {
                        ArcadeCoord diag = new ArcadeCoord(from.X + DiagonalDirs[i].X, from.Y + DiagonalDirs[i].Y);
                        if (!diag.IsValidIn(boardHeight)) continue;
                        if (diag == targetKing || (isAllyAt != null && isAllyAt(diag)))
                        {
                            candidates.Add(diag);
                        }
                    }

                    for (int i = 0; i < CardinalDirs.Length; i++)
                    {
                        ArcadeCoord step = new ArcadeCoord(from.X + CardinalDirs[i].X, from.Y + CardinalDirs[i].Y);
                        if (!step.IsValidIn(boardHeight)) continue;
                        if (isEnemyOccupied != null && isEnemyOccupied(step)) continue;
                        if (step == targetKing || (isAllyAt != null && isAllyAt(step))) continue; // Peón avanza o ataca diagonalmente
                        candidates.Add(step);
                    }
                    break;
                }

                case PieceType.Knight:
                {
                    for (int i = 0; i < KnightOffsets.Length; i++)
                    {
                        ArcadeCoord c = new ArcadeCoord(from.X + KnightOffsets[i].X, from.Y + KnightOffsets[i].Y);
                        if (!c.IsValidIn(boardHeight)) continue;
                        if (isEnemyOccupied != null && isEnemyOccupied(c)) continue;
                        candidates.Add(c);
                    }
                    break;
                }

                case PieceType.Bishop:
                    AddEnemySlidingCandidates(from, boardHeight, targetKing, DiagonalDirs, 4, isEnemyOccupied, isAllyAt, candidates);
                    break;

                case PieceType.Rook:
                    AddEnemySlidingCandidates(from, boardHeight, targetKing, CardinalDirs, 4, isEnemyOccupied, isAllyAt, candidates);
                    break;

                case PieceType.Queen:
                    AddEnemySlidingCandidates(from, boardHeight, targetKing, CardinalDirs, 4, isEnemyOccupied, isAllyAt, candidates);
                    AddEnemySlidingCandidates(from, boardHeight, targetKing, DiagonalDirs, 4, isEnemyOccupied, isAllyAt, candidates);
                    break;
            }

            if (candidates.Count == 0) return from;

            ArcadeCoord best = candidates[0];
            float bestScore = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                ArcadeCoord c = candidates[i];
                if (c == targetKing) return c; // Captura al Rey tiene prioridad máxima

                float score = c.SqrDistanceTo(targetKing) + c.ManhattanDistanceTo(targetKing) * 0.05f;
                if (isAllyAt != null && isAllyAt(c))
                {
                    // Si está en línea directa de capturar una pieza aliada valiosa que bloquea el paso
                    score -= 18f;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }

            return best;
        }

        private static void AddEnemySlidingCandidates(
            ArcadeCoord from,
            int boardHeight,
            ArcadeCoord targetKing,
            ArcadeCoord[] dirs,
            int maxRange,
            Func<ArcadeCoord, bool> isEnemyOccupied,
            Func<ArcadeCoord, bool> isAllyAt,
            List<ArcadeCoord> results)
        {
            for (int d = 0; d < dirs.Length; d++)
            {
                int dx = dirs[d].X;
                int dy = dirs[d].Y;
                for (int step = 1; step <= maxRange; step++)
                {
                    ArcadeCoord c = new ArcadeCoord(from.X + dx * step, from.Y + dy * step);
                    if (!c.IsValidIn(boardHeight)) break;
                    if (isEnemyOccupied != null && isEnemyOccupied(c)) break;

                    results.Add(c);
                    if (c == targetKing || (isAllyAt != null && isAllyAt(c))) break;
                }
            }
        }

        public static List<ArcadeCoord> GetBossRookChargePath(
            int anchorX,
            int anchorY,
            int boardHeight,
            ArcadeCoord king,
            out int destAnchorX,
            out int destAnchorY,
            out bool horizontal)
        {
            var corridor = new List<ArcadeCoord>();
            int centerDistX = Math.Abs(king.X - anchorX);
            int centerDistY = Math.Abs(king.Y - anchorY);

            horizontal = centerDistX >= centerDistY;

            if (horizontal)
            {
                int dirX = king.X >= anchorX ? 1 : -1;
                destAnchorY = anchorY;
                destAnchorX = dirX > 0 ? BoardWidth - 2 : 0;
                if (destAnchorX == anchorX) destAnchorX = anchorX == 0 ? BoardWidth - 2 : 0;

                int minX = Math.Min(anchorX, destAnchorX);
                int maxX = Math.Max(anchorX, destAnchorX) + 1;
                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = anchorY; y <= anchorY + 1; y++)
                    {
                        ArcadeCoord c = new ArcadeCoord(x, y);
                        if (c.IsValidIn(boardHeight)) corridor.Add(c);
                    }
                }
            }
            else
            {
                int dirY = king.Y >= anchorY ? 1 : -1;
                destAnchorX = anchorX;
                destAnchorY = dirY > 0 ? Math.Max(0, boardHeight - 2) : 0;
                if (destAnchorY == anchorY) destAnchorY = anchorY == 0 ? Math.Max(0, boardHeight - 2) : 0;

                int minY = Math.Min(anchorY, destAnchorY);
                int maxY = Math.Max(anchorY, destAnchorY) + 1;
                for (int x = anchorX; x <= anchorX + 1; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        ArcadeCoord c = new ArcadeCoord(x, y);
                        if (c.IsValidIn(boardHeight)) corridor.Add(c);
                    }
                }
            }

            return corridor;
        }

        public static List<ArcadeCoord> GetBossKnightLeapTiles(
            int anchorX,
            int anchorY,
            int boardHeight,
            ArcadeCoord king,
            out int destAnchorX,
            out int destAnchorY)
        {
            ArcadeCoord[] bossJumps = new ArcadeCoord[]
            {
                new ArcadeCoord(2, 3), new ArcadeCoord(3, 2),
                new ArcadeCoord(3, -2), new ArcadeCoord(2, -3),
                new ArcadeCoord(-2, -3), new ArcadeCoord(-3, -2),
                new ArcadeCoord(-3, 2), new ArcadeCoord(-2, 3),
                new ArcadeCoord(1, 2), new ArcadeCoord(2, 1),
                new ArcadeCoord(2, -1), new ArcadeCoord(1, -2),
                new ArcadeCoord(-1, -2), new ArcadeCoord(-2, -1),
                new ArcadeCoord(-2, 1), new ArcadeCoord(-1, 2)
            };

            destAnchorX = anchorX;
            destAnchorY = anchorY;
            float bestDist = float.MaxValue;

            for (int i = 0; i < bossJumps.Length; i++)
            {
                int candX = anchorX + bossJumps[i].X;
                int candY = anchorY + bossJumps[i].Y;
                if (!IsValidBossAnchor(candX, candY, boardHeight)) continue;

                float d = ((candX + 0.5f) - king.X) * ((candX + 0.5f) - king.X) +
                          ((candY + 0.5f) - king.Y) * ((candY + 0.5f) - king.Y);
                if (d < bestDist)
                {
                    bestDist = d;
                    destAnchorX = candX;
                    destAnchorY = candY;
                }
            }

            var danger = new List<ArcadeCoord>();
            for (int x = destAnchorX - 1; x <= destAnchorX + 2; x++)
            {
                for (int y = destAnchorY - 1; y <= destAnchorY + 2; y++)
                {
                    ArcadeCoord c = new ArcadeCoord(x, y);
                    if (c.IsValidIn(boardHeight)) danger.Add(c);
                }
            }
            return danger;
        }

        public static List<ArcadeCoord> GetBossQueenBarrageTiles(int anchorX, int anchorY, int boardHeight)
        {
            var danger = new List<ArcadeCoord>();
            var rayOrigins = new ArcadeCoord[]
            {
                new ArcadeCoord(anchorX, anchorY),
                new ArcadeCoord(anchorX + 1, anchorY),
                new ArcadeCoord(anchorX, anchorY + 1),
                new ArcadeCoord(anchorX + 1, anchorY + 1)
            };

            int[,] rayDirs = new int[,]
            {
                { -1, -1 }, { 0, -1 }, { -1, 0 },
                { 1, -1 },  { 0, -1 }, { 1, 0 },
                { -1, 1 },  { 0, 1 },  { -1, 0 },
                { 1, 1 },   { 0, 1 },  { 1, 0 }
            };

            for (int c = 0; c < 4; c++)
            {
                ArcadeCoord origin = rayOrigins[c];
                for (int r = 0; r < 3; r++)
                {
                    int idx = c * 3 + r;
                    int dx = rayDirs[idx, 0];
                    int dy = rayDirs[idx, 1];
                    int step = 1;
                    while (true)
                    {
                        ArcadeCoord tile = new ArcadeCoord(origin.X + dx * step, origin.Y + dy * step);
                        if (!tile.IsValidIn(boardHeight)) break;
                        if (!danger.Contains(tile)) danger.Add(tile);
                        step++;
                    }
                }
            }

            return danger;
        }
    }
}

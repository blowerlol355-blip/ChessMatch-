using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Sistema de Sinergias y Cartas Modificadoras para el Modo Arcade Puro de Ajedrez:
    /// - PiercingCaptureSynergy (Judit Polgár + Torre / Alfil / Dama): barrido de captura en toda la línea o diagonal.
    /// - ChainLeapSynergy (Mijaíl Tal + Caballo): doble salto consecutivo en L al capturar.
    /// - IronPhalanxSynergy (Tigrán Petrosián + Peones / Torres): Escudo Dorado en Peones y Torres aliadas que destruye al atacante.
    /// - BlitzOverdriveSynergy (Garry Kaspárov): duplica x2 la velocidad de turno de todas las piezas aliadas.
    /// </summary>
    public class CardSynergySystem : MonoBehaviour
    {
        private ArcadeSurvivorController _controller;

        private readonly Dictionary<ArcadeModifierId, int> _modifierStacks = new Dictionary<ArcadeModifierId, int>();
        private readonly List<PieceType> _scratchAllyTypes = new List<PieceType>();

        private CardSynergyState _currentState;

        public CardSynergyState CurrentState => _currentState;
        public bool IsPiercingCaptureActive => _currentState.PiercingCaptureSynergy;
        public bool IsChainLeapActive => _currentState.ChainLeapSynergy;
        public bool IsIronPhalanxActive => _currentState.IronPhalanxSynergy;
        public bool IsBlitzOverdriveActive => _currentState.BlitzOverdriveSynergy;

        public void InitializeSystem(ArcadeSurvivorController controller)
        {
            _controller = controller;
        }

        public void ResetSynergies()
        {
            _modifierStacks.Clear();
            _currentState = default;
        }

        public int GetModifierStacks(ArcadeModifierId mod)
        {
            return _modifierStacks.TryGetValue(mod, out int count) ? count : 0;
        }

        public bool HasPawnAdvanceBoost => GetModifierStacks(ArcadeModifierId.PawnAdvanceBoost) > 0;
        public bool HasKnightAssaultBoost => GetModifierStacks(ArcadeModifierId.KnightAssaultBoost) > 0;
        public bool HasFastReconstruction => GetModifierStacks(ArcadeModifierId.FastReconstruction) > 0;

        public void GrantModifierCard(ArcadeModifierId mod)
        {
            if (mod == ArcadeModifierId.None) return;
            _modifierStacks[mod] = GetModifierStacks(mod) + 1;

            if (_controller == null) return;

            switch (mod)
            {
                case ArcadeModifierId.PawnAdvanceBoost:
                    _controller.ShowBanner("¡PEÓN ACELERADO!", "Tus Peones aliados ganan zancada doble, pasos laterales y turnos más rápidos", new Color(0.95f, 0.85f, 0.30f), 2.5f);
                    break;
                case ArcadeModifierId.KnightAssaultBoost:
                    _controller.ShowBanner("¡CABALLERÍA DE ASALTO!", "Tus Caballos aliados ganan agilidad y reposicionamiento táctico", new Color(0.38f, 0.94f, 0.56f), 2.5f);
                    break;
                case ArcadeModifierId.TacticalTempoBoost:
                    _controller.ShowBanner("¡RITMO TÁCTICO!", "+18% velocidad de turno para todas tus piezas aliadas en tablero", new Color(0.28f, 0.86f, 1.0f), 2.5f);
                    break;
                case ArcadeModifierId.FastReconstruction:
                    _controller.AllyManager?.RefreshEligibleGoldenShields();
                    _controller.ShowBanner("¡FORJA RÁPIDA!", "Reconstrucción reducida a 2.2s y escudos dorados recargados", new Color(1.0f, 0.65f, 0.24f), 2.5f);
                    break;
                case ArcadeModifierId.MagnetPickupBoost:
                    _controller.UpgradeMagnetRange(1.25f);
                    break;
                case ArcadeModifierId.RoyalShieldRepair:
                    _controller.RestoreRoyalShield(1);
                    break;
            }
        }

        public float GetAllyTurnSpeedMultiplier(PieceType pieceType)
        {
            float mult = 1.0f;

            // BlitzOverdriveSynergy (Kaspárov): duplica x2 velocidad de turno de TODAS las piezas aliadas
            if (IsBlitzOverdriveActive)
            {
                mult *= 2.0f;
            }

            // Carta Modificadora: Ritmo Táctico
            int tempoStacks = GetModifierStacks(ArcadeModifierId.TacticalTempoBoost);
            if (tempoStacks > 0)
            {
                mult *= (1f + tempoStacks * 0.18f);
            }

            // Modificadores específicos por tipo de pieza
            if (pieceType == PieceType.Pawn)
            {
                int pawnStacks = GetModifierStacks(ArcadeModifierId.PawnAdvanceBoost);
                if (pawnStacks > 0) mult *= (1f + pawnStacks * 0.22f);
            }
            else if (pieceType == PieceType.Knight)
            {
                int knightStacks = GetModifierStacks(ArcadeModifierId.KnightAssaultBoost);
                if (knightStacks > 0) mult *= (1f + knightStacks * 0.20f);
            }

            return Mathf.Clamp(mult, 1f, 5f);
        }

        public float GetReconstructionDuration()
        {
            int stacks = GetModifierStacks(ArcadeModifierId.FastReconstruction);
            if (stacks <= 0) return ArcadeLogic.AllyReconstructionSeconds; // 4.0s base
            return Mathf.Max(1.4f, ArcadeLogic.AllyReconstructionSeconds - stacks * 1.4f);
        }

        public void EvaluateAndSyncSynergies(bool showNotifications = true)
        {
            if (_controller == null) return;

            bool hasPetrosian = _controller.HasMaster(ArcadeMasterId.Petrosian);
            bool hasTal = _controller.HasMaster(ArcadeMasterId.Tal);
            bool hasPolgar = _controller.HasMaster(ArcadeMasterId.Polgar);
            bool hasKasparov = _controller.HasMaster(ArcadeMasterId.Kasparov);

            _scratchAllyTypes.Clear();
            if (_controller.AllyManager != null)
            {
                _controller.AllyManager.CollectAllOwnedAllyTypes(_scratchAllyTypes);
            }

            CardSynergyState prev = _currentState;
            _currentState = ArcadeLogic.EvaluateSynergies(hasPetrosian, hasTal, hasPolgar, hasKasparov, _scratchAllyTypes);

            if (_currentState.IronPhalanxSynergy && !prev.IronPhalanxSynergy)
            {
                _controller.AllyManager?.RefreshEligibleGoldenShields();
            }

            if (!showNotifications) return;

            CheckAnnounceSynergy(!prev.PiercingCaptureSynergy && _currentState.PiercingCaptureSynergy, ArcadeSynergyId.PiercingCaptureSynergy, MasterCardDatabase.PolgarAuraColor);
            CheckAnnounceSynergy(!prev.ChainLeapSynergy && _currentState.ChainLeapSynergy, ArcadeSynergyId.ChainLeapSynergy, MasterCardDatabase.TalAuraColor);
            CheckAnnounceSynergy(!prev.IronPhalanxSynergy && _currentState.IronPhalanxSynergy, ArcadeSynergyId.IronPhalanxSynergy, MasterCardDatabase.PetrosianAuraColor);
            CheckAnnounceSynergy(!prev.BlitzOverdriveSynergy && _currentState.BlitzOverdriveSynergy, ArcadeSynergyId.BlitzOverdriveSynergy, MasterCardDatabase.KasparovAuraColor);
        }

        private void CheckAnnounceSynergy(bool newlyActivated, ArcadeSynergyId id, Color color)
        {
            if (!newlyActivated || _controller == null) return;

            string name = ArcadeLogic.GetSynergyDisplayName(id);
            if (!_controller.RunStats.UnlockedSynergyNames.Contains(name))
            {
                _controller.RunStats.UnlockedSynergyNames.Add(name);
            }

            RetroAudioManager.Instance?.PlayRewardSound();
            _controller.SpawnFloatingText(_controller.KingWorldPosition + Vector3.up * 0.72f, "¡SINERGIA!", color, true);
            _controller.ShowBanner($"★ SINERGIA: {name.ToUpperInvariant()} ★", GetSynergySubtitle(id), color, 3.2f);
        }

        public static string GetSynergySubtitle(ArcadeSynergyId id)
        {
            switch (id)
            {
                case ArcadeSynergyId.PiercingCaptureSynergy:
                    return "Torres, Alfiles y Damas barren a todos los enemigos en su línea o diagonal";
                case ArcadeSynergyId.ChainLeapSynergy:
                    return "Cada Caballo aliado encadena un 2° salto en L al comer un enemigo";
                case ArcadeSynergyId.IronPhalanxSynergy:
                    return "Peones y Torres aliadas portan Escudo Dorado y destruyen a su atacante";
                case ArcadeSynergyId.BlitzOverdriveSynergy:
                    return "Todas las piezas aliadas actúan al doble de velocidad (x2)";
                default:
                    return string.Empty;
            }
        }
    }
}


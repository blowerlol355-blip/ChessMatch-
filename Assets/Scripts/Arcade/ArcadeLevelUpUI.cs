using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Modal de subida de nivel para el Modo Arcade Puro de Ajedrez:
    /// Pausa Time.timeScale = 0f y ofrece elegir entre:
    /// a) Invocar una nueva pieza aliada física al tablero (Peón, Caballo, Alfil, Torre o Dama).
    /// b) Mejorar piezas presentes en el tablero con Cartas Modificadoras y Cartas de Gran Maestro (Sinergias).
    /// </summary>
    public class ArcadeLevelUpUI : MonoBehaviour
    {
        private ArcadeSurvivorController _controller;
        private bool _isOpen = false;
        private int _draftLevel = 1;
        private float _popElapsed = 0f;
        private readonly List<ArcadeUpgradeChoice> _choices = new List<ArcadeUpgradeChoice>();

        private int _lastScreenW = 0;
        private int _lastScreenH = 0;

        private Texture2D _dimTex;
        private Texture2D _panelBgTex;
        private Texture2D _cardBgTex;
        private Texture2D _cardHoverTex;
        private Texture2D _btnTex;
        private Texture2D _evoBtnTex;
        private Texture2D _whiteTex;

        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _cardTitleStyle;
        private GUIStyle _cardSubStyle;
        private GUIStyle _cardDescStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _pickBtnStyle;
        private GUIStyle _evoBtnStyle;

        public bool IsModalOpen => _isOpen;

        public void InitializeUI(ArcadeSurvivorController controller)
        {
            _controller = controller;
        }

        private void OnDestroy()
        {
            DestroyTex(ref _dimTex);
            DestroyTex(ref _panelBgTex);
            DestroyTex(ref _cardBgTex);
            DestroyTex(ref _cardHoverTex);
            DestroyTex(ref _btnTex);
            DestroyTex(ref _evoBtnTex);
            DestroyTex(ref _whiteTex);
        }

        public void OpenLevelUpDraft(int reachedLevel)
        {
            _draftLevel = reachedLevel;
            _isOpen = true;
            _popElapsed = 0f;
            Time.timeScale = 0f;

            BuildThreeUpgradeChoices();
            RetroAudioManager.Instance?.PlayRewardSound();
        }

        public void CloseModal()
        {
            _isOpen = false;
            _choices.Clear();
            if (_controller != null && _controller.IsSessionActive && !_controller.IsPaused && !_controller.IsGameOver)
            {
                Time.timeScale = 1f;
            }
        }

        private void Update()
        {
            if (_isOpen)
            {
                _popElapsed += Time.unscaledDeltaTime;
            }
        }

        private void BuildThreeUpgradeChoices()
        {
            _choices.Clear();
            var summonPool = BuildSummonChoices();
            var upgradePool = BuildModifierAndMasterChoices();

            ShuffleList(summonPool);
            ShuffleList(upgradePool);

            // Priorizar opciones que despiertan Sinergias activas
            summonPool.Sort((a, b) => b.HighlightsSynergy.CompareTo(a.HighlightsSynergy));
            upgradePool.Sort((a, b) => b.HighlightsSynergy.CompareTo(a.HighlightsSynergy));

            // Asegurar mezcla equilibrada: al menos 1 pieza física invocable + cartas de mejora/Gran Maestro
            var picked = new List<ArcadeUpgradeChoice>();
            if (summonPool.Count > 0)
            {
                picked.Add(summonPool[0]);
            }
            if (upgradePool.Count > 0)
            {
                picked.Add(upgradePool[0]);
            }

            var remaining = new List<ArcadeUpgradeChoice>();
            for (int i = 1; i < summonPool.Count; i++) remaining.Add(summonPool[i]);
            for (int i = 1; i < upgradePool.Count; i++) remaining.Add(upgradePool[i]);
            ShuffleList(remaining);

            for (int i = 0; i < remaining.Count && picked.Count < 3; i++)
            {
                if (!ContainsSameOption(picked, remaining[i]))
                {
                    picked.Add(remaining[i]);
                }
            }

            while (picked.Count < 3)
            {
                picked.Add(CreateTempoModifierChoice());
            }

            _choices.AddRange(picked);
        }

        private static bool ContainsSameOption(List<ArcadeUpgradeChoice> list, ArcadeUpgradeChoice item)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Kind == item.Kind &&
                    list[i].AllyPieceType == item.AllyPieceType &&
                    list[i].ModifierId == item.ModifierId &&
                    list[i].MasterId == item.MasterId)
                {
                    return true;
                }
            }
            return false;
        }

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private List<ArcadeUpgradeChoice> BuildSummonChoices()
        {
            var list = new List<ArcadeUpgradeChoice>();
            SummonedAllyPieceManager allies = _controller != null ? _controller.AllyManager : null;

            bool hasPetrosian = _controller != null && _controller.HasMaster(ArcadeMasterId.Petrosian);
            bool hasTal = _controller != null && _controller.HasMaster(ArcadeMasterId.Tal);
            bool hasPolgar = _controller != null && _controller.HasMaster(ArcadeMasterId.Polgar);

            // Peón Aliado
            bool pawnSyn = hasPetrosian;
            list.Add(new ArcadeUpgradeChoice
            {
                Kind = ArcadeUpgradeKind.SummonAllyPiece,
                AllyPieceType = PieceType.Pawn,
                Title = "Invocar Peón Aliado",
                Subtitle = $"En tablero: {allies?.CountAlliesOfType(PieceType.Pawn) ?? 0} · Valor 1 EXP",
                Description = pawnSyn
                    ? "Despliega un Peón autónomo con Escudo Dorado de Petrosián y avance hacia Coronación."
                    : "Despliega un Peón aliado que avanza y captura en diagonal cada 1.2s hasta coronar.",
                BadgeText = pawnSyn ? "★ INVOCAR + SINERGIA ★" : "PIEZA ALIADA",
                HighlightsSynergy = pawnSyn,
                AccentColor = pawnSyn ? MasterCardDatabase.PetrosianAuraColor : new Color(0.95f, 0.85f, 0.30f)
            });

            // Caballo Aliado
            bool knightSyn = hasTal;
            list.Add(new ArcadeUpgradeChoice
            {
                Kind = ArcadeUpgradeKind.SummonAllyPiece,
                AllyPieceType = PieceType.Knight,
                Title = "Invocar Caballo Aliado",
                Subtitle = $"En tablero: {allies?.CountAlliesOfType(PieceType.Knight) ?? 0} · Salto en L",
                Description = knightSyn
                    ? "Despliega un Caballo con Sinergia Tal: realiza DOBLE salto en L consecutivo al capturar."
                    : "Salta sobre cualquier bloqueo para cazar enemigos de alto valor cerca del Rey.",
                BadgeText = knightSyn ? "★ INVOCAR + SINERGIA TAL ★" : "PIEZA ALIADA",
                HighlightsSynergy = knightSyn,
                AccentColor = knightSyn ? MasterCardDatabase.TalAuraColor : new Color(0.36f, 0.94f, 0.56f)
            });

            // Alfil Aliado
            bool bishopSyn = hasPolgar;
            list.Add(new ArcadeUpgradeChoice
            {
                Kind = ArcadeUpgradeKind.SummonAllyPiece,
                AllyPieceType = PieceType.Bishop,
                Title = "Invocar Alfil Aliado",
                Subtitle = $"En tablero: {allies?.CountAlliesOfType(PieceType.Bishop) ?? 0} · Diagonales",
                Description = bishopSyn
                    ? "Despliega un Alfil con Sinergia Polgár: barre y come TODA la diagonal sin detenerse."
                    : "Domina largas diagonales cazando amenazas a distancia en 1 movimiento.",
                BadgeText = bishopSyn ? "★ INVOCAR + SINERGIA POLGÁR ★" : "PIEZA ALIADA",
                HighlightsSynergy = bishopSyn,
                AccentColor = bishopSyn ? MasterCardDatabase.PolgarAuraColor : new Color(1.0f, 0.58f, 0.24f)
            });

            // Torre Aliada
            bool rookSyn = hasPolgar || hasPetrosian;
            list.Add(new ArcadeUpgradeChoice
            {
                Kind = ArcadeUpgradeKind.SummonAllyPiece,
                AllyPieceType = PieceType.Rook,
                Title = "Invocar Torre Aliada",
                Subtitle = $"En tablero: {allies?.CountAlliesOfType(PieceType.Rook) ?? 0} · Líneas Rectas",
                Description = rookSyn
                    ? "Despliega una Torre con sinergia activa (Barrido Polgár y/o Escudo Dorado Petrosián)."
                    : "Barre filas y columnas limpiando oleadas enteras en 1 golpe.",
                BadgeText = rookSyn ? "★ INVOCAR + SINERGIA ★" : "PIEZA ÉLITE",
                HighlightsSynergy = rookSyn,
                AccentColor = rookSyn ? MasterCardDatabase.KasparovAuraColor : new Color(0.28f, 0.85f, 1.0f)
            });

            // Dama Aliada (disponible desde Nivel 3 o si ya tienes 2+ aliados)
            if (_draftLevel >= 3 || (allies != null && allies.TotalAllyCount >= 2))
            {
                list.Add(new ArcadeUpgradeChoice
                {
                    Kind = ArcadeUpgradeKind.SummonAllyPiece,
                    AllyPieceType = PieceType.Queen,
                    Title = "Invocar Dama Aliada",
                    Subtitle = $"En tablero: {allies?.CountAlliesOfType(PieceType.Queen) ?? 0} · Pieza Suprema",
                    Description = hasPolgar
                        ? "Dama suprema con Sinergia Polgár: aniquila filas y diagonales completas en un movimiento."
                        : "Pieza de poder total que captura en 8 direcciones priorizando al rival de mayor valor.",
                    BadgeText = hasPolgar ? "★ DAMA + BARRIDO POLGÁR ★" : "★ INVOCACIÓN SUPREMA ★",
                    HighlightsSynergy = true,
                    AccentColor = new Color(1.0f, 0.84f, 0.22f)
                });
            }

            return list;
        }

        private List<ArcadeUpgradeChoice> BuildModifierAndMasterChoices()
        {
            var list = new List<ArcadeUpgradeChoice>();
            SummonedAllyPieceManager allies = _controller != null ? _controller.AllyManager : null;

            bool hasPawnOrRook = allies != null && (allies.CountAlliesOfType(PieceType.Pawn) > 0 || allies.CountAlliesOfType(PieceType.Rook) > 0);
            bool hasKnight = allies != null && allies.CountAlliesOfType(PieceType.Knight) > 0;
            bool hasSlider = allies != null && (allies.CountAlliesOfType(PieceType.Rook) > 0 || allies.CountAlliesOfType(PieceType.Bishop) > 0 || allies.CountAlliesOfType(PieceType.Queen) > 0);

            // Cartas de Gran Maestro (Sinergias)
            ArcadeMasterId[] masters = new ArcadeMasterId[]
            {
                ArcadeMasterId.Petrosian,
                ArcadeMasterId.Tal,
                ArcadeMasterId.Polgar,
                ArcadeMasterId.Kasparov
            };

            for (int i = 0; i < masters.Length; i++)
            {
                ArcadeMasterId m = masters[i];
                if (_controller != null && _controller.HasMaster(m)) continue;

                bool comboNow = m switch
                {
                    ArcadeMasterId.Petrosian => hasPawnOrRook,
                    ArcadeMasterId.Tal       => hasKnight,
                    ArcadeMasterId.Polgar    => hasSlider,
                    ArcadeMasterId.Kasparov  => allies != null && allies.TotalAllyCount > 0,
                    _                        => false
                };

                list.Add(new ArcadeUpgradeChoice
                {
                    Kind = ArcadeUpgradeKind.MasterCard,
                    MasterId = m,
                    Title = ArcadeLogic.GetMasterDisplayName(m),
                    Subtitle = GetMasterSubtitle(m),
                    Description = GetMasterDescription(m, comboNow),
                    BadgeText = comboNow ? "★ DESPIERTA SINERGIA ★" : "GRAN MAESTRO",
                    HighlightsSynergy = comboNow,
                    AccentColor = GetMasterColor(m)
                });
            }

            // Cartas Modificadoras de tablero
            list.Add(CreateTempoModifierChoice());

            if (allies != null && allies.CountAlliesOfType(PieceType.Pawn) > 0)
            {
                list.Add(new ArcadeUpgradeChoice
                {
                    Kind = ArcadeUpgradeKind.AllyModifierCard,
                    ModifierId = ArcadeModifierId.PawnAdvanceBoost,
                    AllyPieceType = PieceType.Pawn,
                    Title = "Peón Acelerado",
                    Subtitle = "Modificador de Peones",
                    Description = "Tus Peones aliados actúan un 22% más rápido, avanzan hasta 2 casillas y atacan flancos.",
                    BadgeText = "MODIFICADOR · PEÓN",
                    AccentColor = new Color(0.96f, 0.84f, 0.28f)
                });
            }

            if (allies != null && allies.CountAlliesOfType(PieceType.Knight) > 0)
            {
                list.Add(new ArcadeUpgradeChoice
                {
                    Kind = ArcadeUpgradeKind.AllyModifierCard,
                    ModifierId = ArcadeModifierId.KnightAssaultBoost,
                    AllyPieceType = PieceType.Knight,
                    Title = "Caballería de Asalto",
                    Subtitle = "Modificador de Caballos",
                    Description = "Tus Caballos actúan un 20% más rápido y ganan paso ortogonal de reposicionamiento.",
                    BadgeText = "MODIFICADOR · CABALLO",
                    AccentColor = new Color(0.36f, 0.94f, 0.56f)
                });
            }

            list.Add(new ArcadeUpgradeChoice
            {
                Kind = ArcadeUpgradeKind.AllyModifierCard,
                ModifierId = ArcadeModifierId.FastReconstruction,
                Title = "Forja de Reconstrucción",
                Subtitle = "Resiliencia del Escuadrón",
                Description = "Reduce la espera tras ser capturado (4.0s -> 2.6s) y recarga Escudos Dorados activos.",
                BadgeText = "MODIFICADOR · FORJA",
                AccentColor = new Color(1.0f, 0.60f, 0.24f)
            });

            list.Add(new ArcadeUpgradeChoice
            {
                Kind = ArcadeUpgradeKind.AllyModifierCard,
                ModifierId = ArcadeModifierId.MagnetPickupBoost,
                Title = "Imán Táctico",
                Subtitle = "Atracción de Gemas EXP",
                Description = "Amplía +1.25 casillas el alcance magnético para absorber gemas de valor de piezas.",
                BadgeText = "MODIFICADOR · IMÁN",
                AccentColor = new Color(0.28f, 0.86f, 1.0f)
            });

            if (_controller != null && _controller.RoyalShields < ArcadeLogic.BaseRoyalShields)
            {
                list.Add(new ArcadeUpgradeChoice
                {
                    Kind = ArcadeUpgradeKind.AllyModifierCard,
                    ModifierId = ArcadeModifierId.RoyalShieldRepair,
                    Title = "Escudo Real Restaurado",
                    Subtitle = "Protección del Rey Blanco",
                    Description = $"Restaura +1 Escudo Real ({_controller.RoyalShields}/{ArcadeLogic.BaseRoyalShields}) al Rey Blanco.",
                    BadgeText = "★ DEFENSA REAL ★",
                    HighlightsSynergy = true,
                    AccentColor = new Color(0.30f, 0.95f, 0.48f)
                });
            }

            return list;
        }

        private ArcadeUpgradeChoice CreateTempoModifierChoice()
        {
            return new ArcadeUpgradeChoice
            {
                Kind = ArcadeUpgradeKind.AllyModifierCard,
                ModifierId = ArcadeModifierId.TacticalTempoBoost,
                Title = "Ritmo Táctico",
                Subtitle = "Velocidad de Turno Aliada",
                Description = "Acelera un 18% el ritmo de jugada de TODAS las piezas aliadas presentes en el tablero.",
                BadgeText = "MODIFICADOR · RITMO",
                AccentColor = new Color(0.28f, 0.85f, 1.0f)
            };
        }

        private void ApplyChoice(ArcadeUpgradeChoice choice)
        {
            if (_controller == null || choice == null) return;

            RetroAudioManager.Instance?.PlayCardActivate();

            switch (choice.Kind)
            {
                case ArcadeUpgradeKind.SummonAllyPiece:
                    _controller.AllyManager?.SummonAllyPiece(choice.AllyPieceType);
                    break;
                case ArcadeUpgradeKind.AllyModifierCard:
                    _controller.SynergySystem?.GrantModifierCard(choice.ModifierId);
                    break;
                case ArcadeUpgradeKind.MasterCard:
                    _controller.GrantMaster(choice.MasterId);
                    break;
            }

            CloseModal();
        }

        private static string GetMasterSubtitle(ArcadeMasterId m)
        {
            switch (m)
            {
                case ArcadeMasterId.Petrosian: return "Escudo de Hierro + Falange";
                case ArcadeMasterId.Tal:       return "Bosque Oscuro · Doble Salto";
                case ArcadeMasterId.Polgar:    return "Ataque Implacable · Barrido";
                case ArcadeMasterId.Kasparov:  return "Presión Absoluta · Blitz x2";
                default:                       return string.Empty;
            }
        }

        private static string GetMasterDescription(ArcadeMasterId m, bool synergyNow)
        {
            string tag = synergyNow ? " ¡SINERGIA ACTIVA AL INSTANTE!" : "";
            switch (m)
            {
                case ArcadeMasterId.Petrosian:
                    return "Otorga Escudo de Hierro al Rey y Escudo Dorado a Peones/Torres (destruyen al atacante)." + tag;
                case ArcadeMasterId.Tal:
                    return "Sinergia Caballo: al comer una pieza, salta inmediatamente otra vez en L en el mismo turno." + tag;
                case ArcadeMasterId.Polgar:
                    return "Sinergia Torre/Alfil/Dama: barre y come a TODAS las piezas enemigas de su línea o diagonal." + tag;
                case ArcadeMasterId.Kasparov:
                    return "Sobrecarga Blitz: duplica (x2) la velocidad de turno de todas tus piezas aliadas en el tablero." + tag;
                default:
                    return tag;
            }
        }

        private static Color GetMasterColor(ArcadeMasterId m)
        {
            switch (m)
            {
                case ArcadeMasterId.Petrosian: return MasterCardDatabase.PetrosianAuraColor;
                case ArcadeMasterId.Tal:       return MasterCardDatabase.TalAuraColor;
                case ArcadeMasterId.Polgar:    return MasterCardDatabase.PolgarAuraColor;
                case ArcadeMasterId.Kasparov:  return MasterCardDatabase.KasparovAuraColor;
                default:                       return Color.white;
            }
        }

        private Sprite GetChoiceSprite(ArcadeUpgradeChoice choice)
        {
            if (choice.Kind == ArcadeUpgradeKind.MasterCard)
            {
                return MasterCardDatabase.ResolveMasterSpriteByName(choice.Title);
            }
            if (choice.Kind == ArcadeUpgradeKind.SummonAllyPiece && _controller != null)
            {
                return _controller.GetPieceSprite(choice.AllyPieceType, PieceColor.White);
            }
            if (choice.AllyPieceType != PieceType.None && _controller != null)
            {
                return _controller.GetPieceSprite(choice.AllyPieceType, PieceColor.White);
            }
            return _controller != null ? _controller.GetShieldEmblemSprite() : null;
        }

        #region Renderizado OnGUI

        private void OnGUI()
        {
            if (!_isOpen || _choices.Count == 0) return;

            InitStylesIfNeeded();

            float w = Screen.width;
            float h = Screen.height;
            float s = ChessFontProvider.GetResponsiveScale();

            GUI.depth = -250;
            GUI.DrawTexture(new Rect(0, 0, w, h), _dimTex);

            float popT = Mathf.Clamp01(_popElapsed / 0.22f);
            float popScale = Mathf.Lerp(0.88f, 1.0f, 1f - Mathf.Pow(1f - popT, 3f));

            bool portrait = h > w * 1.05f;
            float panelW = Mathf.Min(w * 0.94f, (portrait ? 540f : 860f) * s) * popScale;
            float panelH = Mathf.Min(h * 0.88f, (portrait ? 670f : 470f) * s) * popScale;
            Rect panelRect = new Rect((w - panelW) * 0.5f, (h - panelH) * 0.5f, panelW, panelH);

            GUI.DrawTexture(panelRect, _panelBgTex);
            DrawBorder(panelRect, new Color(0.831f, 0.686f, 0.216f, 0.95f), 2f);

            float pad = 16f * s;
            GUI.Label(new Rect(panelRect.x + pad, panelRect.y + 12f * s, panelRect.width - pad * 2f, 34f * s),
                $"★ ¡SUBISTE AL NIVEL {_draftLevel}! ★", _headerStyle);
            GUI.Label(new Rect(panelRect.x + pad, panelRect.y + 44f * s, panelRect.width - pad * 2f, 22f * s),
                "Invoca una pieza al tablero o elige una Carta de Sinergia / Modificador:", _subHeaderStyle);

            float contentTop = panelRect.y + 74f * s;
            float contentH = panelRect.yMax - contentTop - 16f * s;
            float contentW = panelRect.width - pad * 2f;

            if (portrait)
            {
                float gap = 10f * s;
                float cardH = (contentH - gap * 2f) / 3f;
                for (int i = 0; i < _choices.Count; i++)
                {
                    Rect cardRect = new Rect(panelRect.x + pad, contentTop + i * (cardH + gap), contentW, cardH);
                    DrawChoiceCard(cardRect, _choices[i], isHorizontalCard: true, s);
                }
            }
            else
            {
                float gap = 12f * s;
                float cardW = (contentW - gap * 2f) / 3f;
                for (int i = 0; i < _choices.Count; i++)
                {
                    Rect cardRect = new Rect(panelRect.x + pad + i * (cardW + gap), contentTop, cardW, contentH);
                    DrawChoiceCard(cardRect, _choices[i], isHorizontalCard: false, s);
                }
            }
        }

        private void DrawChoiceCard(Rect rect, ArcadeUpgradeChoice choice, bool isHorizontalCard, float s)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            GUI.DrawTexture(rect, hover ? _cardHoverTex : _cardBgTex);
            DrawBorder(rect, choice.AccentColor, choice.HighlightsSynergy ? 2.5f : 1.5f);

            Sprite icon = GetChoiceSprite(choice);
            float pad = 10f * s;

            if (isHorizontalCard)
            {
                float iconSize = Mathf.Min(rect.height - pad * 2f, 82f * s);
                Rect iconRect = new Rect(rect.x + pad, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize);
                if (icon != null)
                {
                    DrawSpriteAspectFit(iconRect, icon);
                }

                float textX = iconRect.xMax + 10f * s;
                float btnW = Mathf.Min(108f * s, rect.width * 0.25f);
                float textW = rect.xMax - textX - btnW - 14f * s;

                GUI.color = choice.AccentColor;
                GUI.Label(new Rect(textX, rect.y + 8f * s, textW, 18f * s), choice.BadgeText, _badgeStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(textX, rect.y + 26f * s, textW, 24f * s), choice.Title, _cardTitleStyle);
                GUI.Label(new Rect(textX, rect.y + 48f * s, textW, 18f * s), choice.Subtitle, _cardSubStyle);
                GUI.Label(new Rect(textX, rect.y + 66f * s, textW, rect.height - 72f * s), choice.Description, _cardDescStyle);

                Rect btnRect = new Rect(rect.xMax - btnW - pad, rect.y + (rect.height - 42f * s) * 0.5f, btnW, 42f * s);
                if (GUI.Button(btnRect, "ELEGIR", choice.HighlightsSynergy ? _evoBtnStyle : _pickBtnStyle))
                {
                    ApplyChoice(choice);
                }
            }
            else
            {
                float curY = rect.y + 8f * s;
                GUI.color = choice.AccentColor;
                GUI.Label(new Rect(rect.x + pad, curY, rect.width - pad * 2f, 20f * s), choice.BadgeText,
                    new GUIStyle(_badgeStyle) { alignment = TextAnchor.MiddleCenter });
                GUI.color = Color.white;
                curY += 22f * s;

                float iconH = Mathf.Min(98f * s, rect.height * 0.30f);
                if (icon != null)
                {
                    DrawSpriteAspectFit(new Rect(rect.x + pad, curY, rect.width - pad * 2f, iconH), icon);
                }
                curY += iconH + 6f * s;

                GUI.Label(new Rect(rect.x + pad, curY, rect.width - pad * 2f, 26f * s), choice.Title,
                    new GUIStyle(_cardTitleStyle) { alignment = TextAnchor.MiddleCenter });
                curY += 24f * s;

                GUI.Label(new Rect(rect.x + pad, curY, rect.width - pad * 2f, 18f * s), choice.Subtitle,
                    new GUIStyle(_cardSubStyle) { alignment = TextAnchor.MiddleCenter });
                curY += 22f * s;

                float btnH = 38f * s;
                float descH = Mathf.Max(40f * s, rect.yMax - btnH - 12f * s - curY);
                GUI.Label(new Rect(rect.x + pad, curY, rect.width - pad * 2f, descH), choice.Description,
                    new GUIStyle(_cardDescStyle) { alignment = TextAnchor.UpperCenter });

                Rect btnRect = new Rect(rect.x + pad, rect.yMax - btnH - 8f * s, rect.width - pad * 2f, btnH);
                if (GUI.Button(btnRect, "ELEGIR", choice.HighlightsSynergy ? _evoBtnStyle : _pickBtnStyle))
                {
                    ApplyChoice(choice);
                }
            }
        }

        private static void DrawSpriteAspectFit(Rect container, Sprite spr)
        {
            if (spr == null || spr.texture == null) return;

            Rect uv = new Rect(
                spr.textureRect.x / spr.texture.width,
                spr.textureRect.y / spr.texture.height,
                spr.textureRect.width / spr.texture.width,
                spr.textureRect.height / spr.texture.height
            );

            float sprAspect = spr.textureRect.width / Mathf.Max(1f, spr.textureRect.height);
            float boxAspect = container.width / Mathf.Max(1f, container.height);

            Rect drawRect = container;
            if (sprAspect > boxAspect)
            {
                float targetH = container.width / sprAspect;
                drawRect = new Rect(container.x, container.y + (container.height - targetH) * 0.5f, container.width, targetH);
            }
            else
            {
                float targetW = container.height * sprAspect;
                drawRect = new Rect(container.x + (container.width - targetW) * 0.5f, container.y, targetW, container.height);
            }

            GUI.DrawTextureWithTexCoords(drawRect, spr.texture, uv);
        }

        private void DrawBorder(Rect rect, Color col, float thickness)
        {
            if (_whiteTex == null) _whiteTex = MakeTex(Color.white);
            Color prev = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _whiteTex);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), _whiteTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _whiteTex);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), _whiteTex);
            GUI.color = prev;
        }

        private void InitStylesIfNeeded()
        {
            if (_headerStyle != null && Screen.width == _lastScreenW && Screen.height == _lastScreenH) return;
            _lastScreenW = Screen.width;
            _lastScreenH = Screen.height;

            float s = ChessFontProvider.GetResponsiveScale();

            if (_dimTex == null) _dimTex = MakeTex(new Color(0.02f, 0.03f, 0.06f, 0.80f));
            if (_panelBgTex == null) _panelBgTex = MakeTex(new Color(0.06f, 0.08f, 0.12f, 0.97f));
            if (_cardBgTex == null) _cardBgTex = MakeTex(new Color(0.10f, 0.13f, 0.19f, 0.98f));
            if (_cardHoverTex == null) _cardHoverTex = MakeTex(new Color(0.14f, 0.18f, 0.26f, 0.99f));
            if (_btnTex == null) _btnTex = MakeTex(new Color(0.18f, 0.52f, 0.34f, 1f));
            if (_evoBtnTex == null) _evoBtnTex = MakeTex(new Color(0.85f, 0.62f, 0.14f, 1f));
            if (_whiteTex == null) _whiteTex = MakeTex(Color.white);

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                font = ChessFontProvider.SerifBold,
                fontSize = Mathf.RoundToInt(22 * s),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.86f, 0.26f) }
            };

            _subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                font = ChessFontProvider.SerifRegular,
                fontSize = Mathf.RoundToInt(13 * s),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.84f, 0.88f, 0.96f) }
            };

            _badgeStyle = new GUIStyle(GUI.skin.label)
            {
                font = ChessFontProvider.SerifBold,
                fontSize = Mathf.RoundToInt(11 * s),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _cardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = ChessFontProvider.SerifBold,
                fontSize = Mathf.RoundToInt(16 * s),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _cardSubStyle = new GUIStyle(GUI.skin.label)
            {
                font = ChessFontProvider.SerifRegular,
                fontSize = Mathf.RoundToInt(12 * s),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.76f, 0.82f, 0.92f) }
            };

            _cardDescStyle = new GUIStyle(GUI.skin.label)
            {
                font = ChessFontProvider.SerifRegular,
                fontSize = Mathf.RoundToInt(12 * s),
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.90f, 0.92f, 0.97f) }
            };

            _pickBtnStyle = new GUIStyle(GUI.skin.button)
            {
                font = ChessFontProvider.SerifBold,
                fontSize = Mathf.RoundToInt(13 * s),
                fontStyle = FontStyle.Bold,
                normal = { background = _btnTex, textColor = Color.white }
            };

            _evoBtnStyle = new GUIStyle(GUI.skin.button)
            {
                font = ChessFontProvider.SerifBold,
                fontSize = Mathf.RoundToInt(13 * s),
                fontStyle = FontStyle.Bold,
                normal = { background = _evoBtnTex, textColor = new Color(0.08f, 0.06f, 0.02f) }
            };
        }

        private static Texture2D MakeTex(Color c)
        {
            Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.SetPixels(new Color[] { c, c, c, c });
            t.Apply();
            return t;
        }

        private static void DestroyTex(ref Texture2D tex)
        {
            if (tex != null)
            {
                Destroy(tex);
                tex = null;
            }
        }

        #endregion
    }
}

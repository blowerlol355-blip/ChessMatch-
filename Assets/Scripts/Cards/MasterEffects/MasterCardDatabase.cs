using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Base de datos por defecto y factoría centralizada de los 4 Grandes Maestros (Major_Master):
    /// 1. Tigrán Petrosián - El Tigre de Hierro ("Jaque Anulado")
    /// 2. Mijaíl Tal - El Mago de Riga ("Bosque Oscuro")
    /// 3. Judit Polgár - Reina de la Tormenta ("Ataque Implacable")
    /// 4. Garry Kaspárov - El Ogro de Bakú ("Presión Absoluta")
    /// </summary>
    public static class MasterCardDatabase
    {
        public static readonly Color PetrosianAuraColor = new Color(0.831f, 0.686f, 0.216f, 1f); // Dorado Hierro #D4AF37
        public static readonly Color TalAuraColor       = new Color(0.76f,  0.30f,  1.00f,  1f); // Violeta Bosque Oscuro
        public static readonly Color PolgarAuraColor    = new Color(1.00f,  0.45f,  0.16f,  1f); // Fuego Fénix
        public static readonly Color KasparovAuraColor  = new Color(0.92f,  0.24f,  0.28f,  1f); // Carmesí Presión Absoluta

        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        public static List<CardData> CreateDefaultMasterCards()
        {
            return new List<CardData>
            {
                CreatePetrosianCard(),
                CreateTalCard(),
                CreatePolgarCard(),
                CreateKasparovCard()
            };
        }

        public static CardData CreateMasterByName(string masterName)
        {
            if (string.IsNullOrEmpty(masterName)) return CreatePetrosianCard();

            string lower = masterName.ToLowerInvariant();
            if (lower.Contains("tal") || lower.Contains("mago") || lower.Contains("bosque"))
                return CreateTalCard();
            if (lower.Contains("polg") || lower.Contains("tormenta") || lower.Contains("implacable"))
                return CreatePolgarCard();
            if (lower.Contains("kasp") || lower.Contains("bak") || lower.Contains("ogro") || lower.Contains("presi"))
                return CreateKasparovCard();

            return CreatePetrosianCard();
        }

        public static CardData CloneCardWithFreshEffect(CardData template, string fallbackMasterName = null)
        {
            if (template == null)
            {
                return CreateMasterByName(fallbackMasterName);
            }

            if (template is PetrosianCardData)
            {
                return CreatePetrosianCard();
            }

            CardData clone = ScriptableObject.CreateInstance<CardData>();
            ICardEffect clonedEffect = null;
            ICardEffect sourceEffect = template.GetOrCreateEffect();
            if (sourceEffect != null)
            {
                try
                {
                    clonedEffect = Activator.CreateInstance(sourceEffect.GetType()) as ICardEffect;
                }
                catch
                {
                    clonedEffect = sourceEffect;
                }
            }

            clone.Initialize(
                template.CardName,
                template.Description,
                template.Cost,
                template.Rarity,
                clonedEffect,
                ResolveSprite(template),
                template.UpgradeType
            );
            string subtitle = GetCardSubtitle(template);
            if (template.HasCustomAuraColor || !string.IsNullOrEmpty(subtitle))
            {
                clone.SetMasterVisuals(subtitle, GetCardAuraColor(template));
            }
            return clone;
        }

        public static PetrosianCardData CreatePetrosianCard()
        {
            PetrosianCardData card = PetrosianCardData.CreateRuntimeInstance();
            card.SetMasterVisuals("Jaque Anulado", PetrosianAuraColor);
            if (card.CardSprite == null)
            {
                card.CardSprite = LoadMasterSprite("PetrosianIllustration", "PetrosianIllustration.jpg");
            }
            return card;
        }

        public static CardData CreateTalCard()
        {
            CardData card = ScriptableObject.CreateInstance<CardData>();
            card.Initialize(
                "Mijaíl Tal - El Mago de Riga",
                "Sacrifica una pieza aliada (Caballo, Alfil, Torre o Dama) para obtener 2 turnos consecutivos en esta ronda e inmovilizar 1 turno a las piezas enemigas a 1 casilla de distancia.",
                6,
                CardRarity.Legendary,
                new MikhailTalEffect(),
                LoadMasterSprite("TalIllustration", "TalIllustration.jpg"),
                UpgradeType.Major_Master
            );
            card.SetMasterVisuals("Bosque Oscuro", TalAuraColor);
            return card;
        }

        public static CardData CreatePolgarCard()
        {
            CardData card = ScriptableObject.CreateInstance<CardData>();
            card.Initialize(
                "Judit Polgár - Reina de la Tormenta",
                "Tus Alfiles y Dama pueden saltar exactamente sobre 1 pieza bloqueadora en su línea o diagonal para capturar detrás. Si la jugada da Jaque, suma +3 puntos de captura.",
                5,
                CardRarity.Legendary,
                new JuditPolgarEffect(),
                LoadMasterSprite("PolgarIllustration", "PolgarIllustration.jpg"),
                UpgradeType.Major_Master
            );
            card.SetMasterVisuals("Ataque Implacable", PolgarAuraColor);
            return card;
        }

        public static CardData CreateKasparovCard()
        {
            CardData card = ScriptableObject.CreateInstance<CardData>();
            card.Initialize(
                "Garry Kaspárov - El Ogro de Bakú",
                "Tus Caballos y Alfiles ganan 1 casilla ortogonal adicional este turno y aplican Intimidación: la IA tendrá prohibido mover su Dama o Torre en su próximo turno.",
                5,
                CardRarity.Legendary,
                new GarryKasparovEffect(),
                LoadMasterSprite("KasparovIllustration", "KasparovIllustration.jpg"),
                UpgradeType.Major_Master
            );
            card.SetMasterVisuals("Presión Absoluta", KasparovAuraColor);
            return card;
        }

        public static Color GetCardAuraColor(CardData card)
        {
            if (card == null) return new Color(0.55f, 0.65f, 0.80f, 1f);
            if (card.HasCustomAuraColor) return card.AuraColor;

            string lower = string.IsNullOrEmpty(card.CardName) ? string.Empty : card.CardName.ToLowerInvariant();
            if (lower.Contains("petrosi")) return PetrosianAuraColor;
            if (lower.Contains("tal") || lower.Contains("mago")) return TalAuraColor;
            if (lower.Contains("polg") || lower.Contains("tormenta")) return PolgarAuraColor;
            if (lower.Contains("kasp") || lower.Contains("bak") || lower.Contains("ogro")) return KasparovAuraColor;

            return card.Rarity switch
            {
                CardRarity.Legendary => PetrosianAuraColor,
                CardRarity.Rare      => new Color(0.95f, 0.28f, 0.28f, 1f),
                CardRarity.Uncommon  => new Color(0.12f, 0.75f, 0.45f, 1f),
                _                    => new Color(0.24f, 0.55f, 0.95f, 1f)
            };
        }

        public static string GetCardSubtitle(CardData card)
        {
            if (card == null) return string.Empty;
            if (!string.IsNullOrEmpty(card.Subtitle)) return card.Subtitle;

            string lower = string.IsNullOrEmpty(card.CardName) ? string.Empty : card.CardName.ToLowerInvariant();
            if (lower.Contains("petrosi")) return "Jaque Anulado";
            if (lower.Contains("tal") || lower.Contains("mago")) return "Bosque Oscuro";
            if (lower.Contains("polg") || lower.Contains("tormenta")) return "Ataque Implacable";
            if (lower.Contains("kasp") || lower.Contains("bak") || lower.Contains("ogro")) return "Presión Absoluta";
            return string.Empty;
        }

        public static Sprite ResolveSprite(CardData card)
        {
            if (card == null) return null;
            if (card.CardSprite != null) return card.CardSprite;
            return ResolveMasterSpriteByName(card.CardName);
        }

        public static Sprite ResolveMasterSpriteByName(string cardName)
        {
            if (string.IsNullOrEmpty(cardName)) return null;

            string lower = cardName.ToLowerInvariant();
            if (lower.Contains("petrosi") || lower.Contains("tigrán") || lower.Contains("tigran"))
            {
                return LoadMasterSprite("PetrosianIllustration", "PetrosianIllustration.jpg");
            }
            if (lower.Contains("tal") || lower.Contains("mago") || lower.Contains("bosque"))
            {
                return LoadMasterSprite("TalIllustration", "TalIllustration.jpg");
            }
            if (lower.Contains("polg") || lower.Contains("tormenta") || lower.Contains("implacable"))
            {
                return LoadMasterSprite("PolgarIllustration", "PolgarIllustration.jpg");
            }
            if (lower.Contains("kasp") || lower.Contains("ogro") || lower.Contains("bak") || lower.Contains("presi"))
            {
                return LoadMasterSprite("KasparovIllustration", "KasparovIllustration.jpg");
            }
            return null;
        }

        public static Sprite LoadMasterSprite(string resourceName, string fallbackFileName)
        {
            if (string.IsNullOrEmpty(resourceName)) return null;

            if (SpriteCache.TryGetValue(resourceName, out Sprite cached) && cached != null && cached.texture != null)
            {
                return cached;
            }

            Sprite spr = Resources.Load<Sprite>(resourceName);
            if (spr != null)
            {
                SpriteCache[resourceName] = spr;
                return spr;
            }

            if (!string.IsNullOrEmpty(fallbackFileName))
            {
                try
                {
                    string path = Path.Combine(Application.dataPath, "Resources", fallbackFileName);
                    if (File.Exists(path))
                    {
                        byte[] bytes = File.ReadAllBytes(path);
                        Texture2D tex = new Texture2D(2, 2);
                        if (tex.LoadImage(bytes))
                        {
                            spr = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                            SpriteCache[resourceName] = spr;
                            return spr;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MasterCardDatabase] No se pudo cargar imagen '{fallbackFileName}': {ex.Message}");
                }
            }

            return null;
        }
    }
}

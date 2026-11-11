using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// ScriptableObject especializado para la carta legendaria "Tigrán Petrosián - El Tigre de Hierro".
    /// Contiene los metadatos predefinidos (nombre, descripción, coste, rareza) y enlaza
    /// automáticamente con <see cref="PetrosianDefenseEffect"/>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Card_PetrosianDefense",
        menuName  = "ChessBalatro/Cards/Petrosian Defense",
        order     = 30)]
    public class PetrosianCardData : CardData
    {
        public static readonly Color DefaultAuraColor = new Color(0.831f, 0.686f, 0.216f, 1f);
        private static Sprite _cachedSprite;

        private void Reset()
        {
            CardName    = "Tigrán Petrosián - El Tigre de Hierro";
            Subtitle    = "Jaque Anulado";
            AuraColor   = DefaultAuraColor;
            Description = "Anula el jaque actual sobre el Rey blanco. Despliega el Escudo de Petrosián durante este turno: ninguna pieza enemiga puede capturar al Rey. Al finalizar el turno el escudo se disuelve.";
            Cost        = 6;
            Rarity      = CardRarity.Legendary;
            UpgradeType = UpgradeType.Major_Master;
            Effect      = new PetrosianDefenseEffect();
            CardSprite  = LoadPetrosianSprite();
        }

        public override ICardEffect GetOrCreateEffect()
        {
            if (Effect == null)
                Effect = new PetrosianDefenseEffect();
            if (CardSprite == null)
                CardSprite = LoadPetrosianSprite();
            return Effect;
        }

        // ── Factory (código) ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Carga el sprite de Tigrán Petrosián desde Resources o desde archivo físico directamente.
        /// </summary>
        public static Sprite LoadPetrosianSprite()
        {
            if (_cachedSprite != null && _cachedSprite.texture != null)
            {
                return _cachedSprite;
            }

            Sprite spr = Resources.Load<Sprite>("PetrosianIllustration");
            if (spr != null)
            {
                _cachedSprite = spr;
                return spr;
            }

            string path = System.IO.Path.Combine(Application.dataPath, "Resources", "PetrosianIllustration.jpg");
            if (System.IO.File.Exists(path))
            {
                try
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(path);
                    Texture2D tex = new Texture2D(2, 2);
                    if (tex.LoadImage(bytes))
                    {
                        _cachedSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                        return _cachedSprite;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[PetrosianCardData] No se pudo cargar imagen directa: {ex.Message}");
                }
            }
            return null;
        }

        /// <summary>
        /// Crea una instancia de runtime de la carta de Petrosián sin necesidad de un asset en disco.
        /// Útil para pruebas unitarias y generación procedural de manos de cartas.
        /// </summary>
        public static PetrosianCardData CreateRuntimeInstance()
        {
            var data = CreateInstance<PetrosianCardData>();
            data.CardName    = "Tigrán Petrosián - El Tigre de Hierro";
            data.Subtitle    = "Jaque Anulado";
            data.AuraColor   = DefaultAuraColor;
            data.Description = "Anula el jaque actual sobre el Rey blanco. Despliega el Escudo de Petrosián durante este turno: ninguna pieza enemiga puede capturar al Rey. Al finalizar el turno el escudo se disuelve.";
            data.Cost        = 6;
            data.Rarity      = CardRarity.Legendary;
            data.UpgradeType = UpgradeType.Major_Master;
            data.Effect      = new PetrosianDefenseEffect();
            data.CardSprite  = LoadPetrosianSprite();
            return data;
        }
    }
}

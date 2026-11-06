using UnityEngine;

namespace ChessBalatro
{
    /// <summary>
    /// Proveedor centralizado de tipografías y escalas responsivas para el juego.
    /// Utiliza una tipografía Serif clásica (Georgia / ChessSerif) inspirada en torneos
    /// oficiales de ajedrez y piezas de campeonato, asegurando máxima elegancia y legibilidad.
    /// </summary>
    public static class ChessFontProvider
    {
        private static Font _serifBold;
        private static Font _serifRegular;

        /// <summary>
        /// Fuente Serif en negrita para títulos, botones principales y números destacados.
        /// </summary>
        public static Font SerifBold
        {
            get
            {
                if (_serifBold == null) LoadFonts();
                return _serifBold ?? SerifRegular ?? GUI.skin.font;
            }
        }

        /// <summary>
        /// Fuente Serif regular para subtítulos, descripciones y notación de partidas.
        /// </summary>
        public static Font SerifRegular
        {
            get
            {
                if (_serifRegular == null) LoadFonts();
                return _serifRegular ?? _serifBold ?? GUI.skin.font;
            }
        }

        /// <summary>
        /// Retorna la fuente según si se requiere variante en negrita o regular.
        /// </summary>
        public static Font GetFont(bool bold = true)
        {
            return bold ? SerifBold : SerifRegular;
        }

        private static void LoadFonts()
        {
            try
            {
                _serifBold = Resources.Load<Font>("Fonts/ChessSerif-Bold");
                _serifRegular = Resources.Load<Font>("Fonts/ChessSerif");
            }
            catch { }

            if (_serifBold == null)
            {
                try
                {
                    _serifBold = Font.CreateDynamicFontFromOSFont(new string[] { "Georgia", "Palatino", "Garamond", "Times New Roman" }, 24);
                }
                catch { }
            }

            if (_serifRegular == null)
            {
                try
                {
                    _serifRegular = Font.CreateDynamicFontFromOSFont(new string[] { "Georgia", "Palatino", "Garamond", "Times New Roman" }, 20);
                }
                catch { }
            }

            if (_serifBold == null && _serifRegular != null) _serifBold = _serifRegular;
            if (_serifRegular == null && _serifBold != null) _serifRegular = _serifBold;
        }

        /// <summary>
        /// Calcula un factor de escala UI responsivo y equilibrado.
        /// Previene que en resoluciones apaisadas (16:9) o pantallas pequeñas los textos
        /// se reduzcan a tamaños microscópicos.
        /// </summary>
        public static float GetResponsiveScale()
        {
            float screenW = Screen.width;
            float screenH = Screen.height;

            // Escala simétrica basada en dimensiones estándar
            float referenceSize = Mathf.Min(screenW / 760f, screenH / 960f);
            return Mathf.Clamp(referenceSize, 0.90f, 1.45f);
        }
    }
}


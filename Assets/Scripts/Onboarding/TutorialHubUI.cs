using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessBalatro
{
    /// <summary>
    /// Categorías principales del Centro de Tutoriales ("Todos los Tutoriales del Juego").
    /// </summary>
    public enum TutorialHubCategory
    {
        Overview = 0,       // Mapa completo del juego y Menú Principal
        Classic = 1,        // Modo Clásico / Roguelike y tablero táctico
        Arcade = 2,         // Modo Arcade (Chess Survivors 15 min)
        Education = 3,      // Modo Educativo (Academia de 6 lecciones)
        CardsAndExtras = 4  // 4 Grandes Maestros, Cartas Menores, Skins, Audio y ELO
    }

    /// <summary>
    /// Acciones directas disponibles desde las tarjetas del Centro de Tutoriales.
    /// </summary>
    public enum TutorialQuickAction
    {
        None = 0,
        StartInteractiveTour = 1,
        LaunchClassicQuickSetup = 2,
        LaunchArcade = 3,
        OpenEducationAcademy = 4,
        StartEducationLesson1 = 5,
        OpenSkinsMenu = 6,
        OpenOptionsMenu = 7
    }

    /// <summary>
    /// Descriptor puro C# de cada bloque/tarjeta informativa dentro de una categoría del Centro de Tutoriales.
    /// </summary>
    public struct TutorialTopicEntry
    {
        public string Tag;
        public string Title;
        public string Summary;
        public TutorialQuickAction Action;
        public string ActionButtonLabel;
    }

    /// <summary>
    /// Catálogo puro C# (testeable fuera de Unity) con todos los tutoriales completos de Chess Match:
    /// Menú Principal, Modo Clásico Roguelike, Modo Arcade (Survivors), Academia Educativa y Cartas/Maestros/Extras.
    /// </summary>
    public static class TutorialCatalogLogic
    {
        public static readonly TutorialHubCategory[] AllCategories =
        {
            TutorialHubCategory.Overview,
            TutorialHubCategory.Classic,
            TutorialHubCategory.Arcade,
            TutorialHubCategory.Education,
            TutorialHubCategory.CardsAndExtras
        };

        public static string GetCategoryTabLabel(TutorialHubCategory category)
        {
            return category switch
            {
                TutorialHubCategory.Overview       => "1. MENÚ Y GUÍA",
                TutorialHubCategory.Classic        => "2. CLÁSICO",
                TutorialHubCategory.Arcade         => "3. ARCADE",
                TutorialHubCategory.Education      => "4. ACADEMIA",
                TutorialHubCategory.CardsAndExtras => "5. CARTAS Y MÁS",
                _ => "TUTORIALES"
            };
        }

        public static void GetCategoryHeader(TutorialHubCategory category, out string title, out string subtitle, out Color accentColor)
        {
            switch (category)
            {
                case TutorialHubCategory.Overview:
                    title = "★ GUÍA COMPLETA DE CHESS MATCH ★";
                    subtitle = "Explora cada modo, botón y sistema del juego o inicia el tour interactivo con flecha guiada.";
                    accentColor = new Color(0.98f, 0.82f, 0.24f, 1f);
                    break;

                case TutorialHubCategory.Classic:
                    title = "⚔ MODO CLÁSICO / ROGUELIKE";
                    subtitle = "Ajedrez táctico frente a la IA con Economía de Capturas, Drafts y Cartas de Gran Maestro.";
                    accentColor = new Color(0.32f, 0.72f, 1.00f, 1f);
                    break;

                case TutorialHubCategory.Arcade:
                    title = "⚡ MODO ARCADE (CHESS SURVIVORS · 15 MIN)";
                    subtitle = "Captura pura en 1 movimiento, 3 Escudos Reales, Aliados autónomos, Sinergias, 8×8→8×20 y Jefe Final.";
                    accentColor = new Color(0.95f, 0.42f, 0.98f, 1f);
                    break;

                case TutorialHubCategory.Education:
                    title = "★ MODO EDUCATIVO: ACADEMIA (6 LECCIONES)";
                    subtitle = "Domina cada pieza, el Jaque, la Defensa Real y los poderes de cartas progresivamente.";
                    accentColor = new Color(0.24f, 0.86f, 0.48f, 1f);
                    break;

                case TutorialHubCategory.CardsAndExtras:
                    title = "✦ GRANDES MAESTROS, CARTAS Y AJUSTES";
                    subtitle = "Poderes de las 4 Leyendas, tabla de puntos oficial, Aspectos de piezas, Audio SFX y ELO.";
                    accentColor = new Color(1.00f, 0.54f, 0.22f, 1f);
                    break;

                default:
                    title = "TUTORIALES DE CHESS MATCH";
                    subtitle = string.Empty;
                    accentColor = new Color(0.98f, 0.82f, 0.24f, 1f);
                    break;
            }
        }

        public static List<TutorialTopicEntry> GetTopics(TutorialHubCategory category)
        {
            var list = new List<TutorialTopicEntry>();

            switch (category)
            {
                case TutorialHubCategory.Overview:
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "TOUR EN VIVO",
                        Title = "Recorrido Interactivo Paso a Paso (Menú + Tablero)",
                        Summary = "El Maestro Guía ilumina con flecha dorada cada sección del Menú Principal (Perfil, Arcade, Academia, Aspectos y JUGAR), te ayuda a elegir Gran Maestro y te enseña en vivo a mover, capturar, hacer un Draft y activar una carta.",
                        Action = TutorialQuickAction.StartInteractiveTour,
                        ActionButtonLabel = "▶ INICIAR TOUR EN VIVO"
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "MODO CLÁSICO",
                        Title = "⚔ JUGAR · Batalla Clásica Roguelike",
                        Summary = "Configura tu partida rápida (Blancas o Negras, reloj de 3/5/10 min o sin límite, IA Fácil/Medio/Difícil y 1 de los 4 Grandes Maestros) para derrotar a la IA ganando cartas por cada captura.",
                        Action = TutorialQuickAction.LaunchClassicQuickSetup,
                        ActionButtonLabel = "⚔ CONFIGURAR PARTIDA"
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "MODO ARCADE",
                        Title = "⚡ MODO ARCADE · Chess Survivors (15:00)",
                        Summary = "Supervivencia sobre tablero que crece cada 3:30 de 8×8 hasta 8×20. Sin barras de HP: captura en 1 movimiento, protege tus 3 Escudos Reales, invoca aliados autónomos, activa sinergias y derrota al Coloso Real al minuto 15:00.",
                        Action = TutorialQuickAction.LaunchArcade,
                        ActionButtonLabel = "⚡ PROBAR MODO ARCADE"
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "ACADEMIA",
                        Title = "★ MODO EDUCATIVO · 6 Lecciones Interactivas",
                        Summary = "Entrenamiento guiado desde cero dividido en Básico (Torre, Alfil, Caballo, Peón, Dama y Jaque), Intermedio (Escapar del Jaque y Cartas Menores) y Difícil (Escudo de Tigrán Petrosián).",
                        Action = TutorialQuickAction.OpenEducationAcademy,
                        ActionButtonLabel = "★ ABRIR ACADEMIA"
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "SISTEMA",
                        Title = "⚙ Perfil ELO, Aspectos (Skins), Opciones y Donaciones",
                        Summary = "• Arriba a la derecha ves tu Perfil, Rango y ELO acumulados.\n• ASPECTOS (SKINS) cambia la estética de las fichas del tablero.\n• OPCIONES ajusta Música, SFX Arcade retro y vibración háptica móvil.",
                        Action = TutorialQuickAction.OpenSkinsMenu,
                        ActionButtonLabel = "✦ IR A ASPECTOS"
                    });
                    break;

                case TutorialHubCategory.Classic:
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "PASO 1 · SETUP",
                        Title = "Configurar Partida y Grandes Maestros",
                        Summary = "Al pulsar 'JUGAR' escoges:\n• Carta de Gran Maestro inicial (Petrosián, Tal, Polgár o Kaspárov).\n• Bando: Blancas (mueves 1°) o Negras (mueve la IA primero).\n• Reloj: ∞ Sin límite, ⚡ 3 min, ⏱ 5 min o ⏳ 10 min.\n• Dificultad IA: Fácil, Medio o Difícil.",
                        Action = TutorialQuickAction.LaunchClassicQuickSetup,
                        ActionButtonLabel = "⚔ CONFIGURAR PARTIDA"
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "PASO 2 · TABLERO",
                        Title = "Movimiento Legal y Puntos de Captura",
                        Summary = "Toca una pieza para iluminar en verde sus movimientos válidos y en rojo sus capturas. Al comer piezas sumas Puntos de Captura según su valor oficial:\nPeón = 1  |  Caballo = 3  |  Alfil = 3  |  Torre = 5  |  Dama = 9\n¡Dar Jaque válido otorga +1 punto extra!",
                        Action = TutorialQuickAction.StartInteractiveTour,
                        ActionButtonLabel = "▶ PRACTICAR EN VIVO"
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "PASO 3 · DRAFTS",
                        Title = "Umbrales de Recompensa (4 pts y 10 pts)",
                        Summary = "• Cada 4 puntos de captura se abre un Draft de MEJORA MENOR (elige 1 de 3 cartas para potenciar tus piezas).\n• Al llegar a 10 puntos (o al capturar directamente a la Dama enemiga) obtienes un Draft de GRAN MAESTRO.",
                        Action = TutorialQuickAction.None,
                        ActionButtonLabel = null
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "PASO 4 · MANO",
                        Title = "Activar Cartas de la Mano Inferior",
                        Summary = "Las cartas elegidas se añaden a tu mano en la parte inferior de la pantalla. Pulsa cualquier carta disponible antes de mover para consumir su energía y aplicar su efecto táctico sobre el tablero.",
                        Action = TutorialQuickAction.None,
                        ActionButtonLabel = null
                    });
                    break;

                case TutorialHubCategory.Arcade:
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "REGLA PURA",
                        Title = "1. Captura de 1 Solo Movimiento y Control del Rey",
                        Summary = "En Modo Arcade NO existen puntos de vida (HP) ni daño numérico: toda captura elimina al rival en 1 solo movimiento y suelta Gemas de EXP (Peón=1, Caballo/Alfil=3, Torre=5, Dama=9).\nMueve al Rey Blanco 1 casilla en las 8 direcciones tocando el destino, deslizando el dedo o usando WASD/Flechas (cooldown de 0.35s).",
                        Action = TutorialQuickAction.LaunchArcade,
                        ActionButtonLabel = "⚡ JUGAR MODO ARCADE"
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "DEFENSA REAL",
                        Title = "2. Los 3 Escudos Reales y el Escudo de Hierro",
                        Summary = "• Tu Rey comienza con 3 Escudos Reales (los pips dorados sobre tu ficha). Si un enemigo te captura, pierdes 1 Escudo y ganas breve invulnerabilidad.\n• Con Tigrán Petrosián activas además un Escudo de Hierro orbital que bloquea 1 golpe gratis (18s de recarga).",
                        Action = TutorialQuickAction.None,
                        ActionButtonLabel = null
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "ALIADOS 1.2s",
                        Title = "3. Piezas Aliadas Físicas, Coronación y Reconstrucción",
                        Summary = "Al subir de nivel puedes invocar Peones, Caballos, Alfiles, Torres y Damas aliadas que capturan automáticamente cada 1.2s, o elegir Mejoras (Reconstrucción Rápida, Imán EXP, Reparar Escudo Real).\n• Los Peones coronan en Dama al llegar al borde y si una aliada cae vuelve junto a tu Rey tras 4s de Reconstrucción.",
                        Action = TutorialQuickAction.None,
                        ActionButtonLabel = null
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "SINERGIAS",
                        Title = "4. Las 4 Sinergias de Gran Maestro",
                        Summary = "Combina cartas de Gran Maestro con piezas aliadas al subir de nivel:\n• Judit Polgár + Dama/Alfil/Torre → Barrido Implacable (atraviesa varios enemigos seguidos).\n• Mijaíl Tal + Caballo → Doble Salto Táctico en L.\n• Tigrán Petrosián + Peón/Torre → Falange de Hierro y Escudo orbital.\n• Garry Kaspárov + Aliados → Blitz x2 (mueven cada 0.6s).",
                        Action = TutorialQuickAction.None,
                        ActionButtonLabel = null
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "EXPANSIÓN 3:30",
                        Title = "5. Expansión Dinámica del Tablero (8×8 → 8×20)",
                        Summary = "La arena comienza en 8×8 y se expande automáticamente cada 3 minutos y 30 segundos:\n0:00 (8×8) → 3:30 (8×11) → 7:00 (8×14) → 10:30 (8×17) → 14:00 (8×20).\n¡El tablero más largo permite mayores hordas, más espacio táctico y coronaciones más épicas!",
                        Action = TutorialQuickAction.None,
                        ActionButtonLabel = null
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "JEFE 15:00",
                        Title = "6. Jefe Final: El Coloso Real y 3 Sellos de Jaque",
                        Summary = "Al alcanzar 15:00 aparece el Coloso Real protegido por 3 Sellos de Jaque e invulnerable hasta que rompas su defensa.\nElimina a los 3 guardianes de la Guardia Real que lo escoltan para destruir los 3 Sellos y luego captúralo en 1 golpe (Jaque Mate) para conquistar la victoria.",
                        Action = TutorialQuickAction.LaunchArcade,
                        ActionButtonLabel = "⚡ IR A SOBREVIVIR"
                    });
                    break;

                case TutorialHubCategory.Education:
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "BÁSICO · 1/6",
                        Title = "Lección 1 · Torre y Alfil: Líneas y Diagonales",
                        Summary = "Aprende cómo la Torre domina filas y columnas completas y el Alfil barre diagonales de su color. Objetivo: capturar 2 peones objetivo en 2 jugadas.",
                        Action = TutorialQuickAction.StartEducationLesson1,
                        ActionButtonLabel = "▶ INICIAR LECCIÓN 1"
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "BÁSICO · 2-3/6",
                        Title = "Lecciones 2 y 3 · Caballo, Peón, Dama y Primer Jaque",
                        Summary = "• Lección 2: El Caballo es la única pieza que salta en 'L' sobre barreras y el Peón avanza recto pero captura en diagonal.\n• Lección 3: Combina la fuerza de la Dama para poner al Rey rival en Jaque directo.",
                        Action = TutorialQuickAction.OpenEducationAcademy,
                        ActionButtonLabel = "★ VER 6 LECCIONES"
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "INTERMEDIO · 4-5/6",
                        Title = "Lecciones 4 y 5 · Defensa Real y Cartas Menores",
                        Summary = "• Lección 4: Resuelve un Jaque en contra interponiendo tu Torre como escudo o apartando al Rey.\n• Lección 5: Activa la carta menor 'Salto de Caballería' en tu mano para permitir al Caballo avanzar 1 casilla ortogonal y capturar.",
                        Action = TutorialQuickAction.OpenEducationAcademy,
                        ActionButtonLabel = "★ ABRIR ACADEMIA"
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "DIFÍCIL · 6/6",
                        Title = "Lección 6 · Gran Maestro: El Escudo de Petrosián",
                        Summary = "Usa la carta legendaria 'Tigrán Petrosián - Jaque Anulado' para neutralizar el Jaque rival al instante y liberar a tu Dama para dar el Jaque Mate decisivo.",
                        Action = TutorialQuickAction.OpenEducationAcademy,
                        ActionButtonLabel = "★ IR AL SELECTOR"
                    });
                    break;

                case TutorialHubCategory.CardsAndExtras:
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "LEYENDAS 1-2",
                        Title = "Tigrán Petrosián y Mijaíl Tal (Major_Master)",
                        Summary = "• TIGRÁN PETROSIÁN ('Jaque Anulado'): Anula un Jaque en tu contra gracias a su Escudo de Hierro defensivo.\n• MIJAÍL TAL ('Bosque Oscuro'): Sacrifica una pieza aliada (Caballo, Alfil, Torre o Dama) para ganar 2 turnos seguidos e inmovilizar 1 turno a las piezas enemigas adyacentes.",
                        Action = TutorialQuickAction.None,
                        ActionButtonLabel = null
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "LEYENDAS 3-4",
                        Title = "Judit Polgár y Garry Kaspárov (Major_Master)",
                        Summary = "• JUDIT POLGÁR ('Ataque Implacable'): Tus Alfiles y tu Dama saltan exactamente sobre 1 pieza bloqueadora para capturar detrás (y suman +3 pts si dan Jaque).\n• GARRY KASPÁROV ('Presión Absoluta'): Tus Caballos y Alfiles ganan +1 paso ortogonal y aplican Intimidación (prohibido que la IA mueva su Dama o Torre).",
                        Action = TutorialQuickAction.None,
                        ActionButtonLabel = null
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "CARTAS MENORES",
                        Title = "Mejoras Menores (Minor_Perk) y Tabla de Puntos",
                        Summary = "• Peón Acelerado+: Permite avance doble de Peones en cualquier casilla.\n• Salto de Caballería / Impulso Táctico / Trinchera Real: Mejoran movilidad y defensa.\n• Economía Oficial: Peón=1 | Caballo/Alfil=3 | Torre=5 | Dama=9 | Jaque=+1.",
                        Action = TutorialQuickAction.None,
                        ActionButtonLabel = null
                    });
                    list.Add(new TutorialTopicEntry
                    {
                        Tag = "PERSONALIZACIÓN",
                        Title = "Aspectos (Skins), Audio Retro, Hápticos y ELO",
                        Summary = "• ASPECTOS (SKINS): Alterna y equipa conjuntos visuales de piezas con vista previa inmediata.\n• OPCIONES: Controla volumen de Música, SFX sintetizados arcade y vibración táctil.\n• PERFIL ELO: Asciende de NOVATO a INTERMEDIO, AVANZADO, MAESTRO y GRAN MAESTRO.",
                        Action = TutorialQuickAction.OpenOptionsMenu,
                        ActionButtonLabel = "⚙ ABRIR OPCIONES"
                    });
                    break;
            }

            return list;
        }
    }

    /// <summary>
    /// Pantalla modal/panel interactivo del Centro de Tutoriales ("Todos los Tutoriales de Chess Match").
    /// Se abre al pulsar "🎓 TUTORIAL DEL JUEGO" desde cualquier menú para explicar todos los modos,
    /// mecánicas, cartas, Grandes Maestros, Aspectos y Opciones, y permitir lanzar cualquier sección.
    /// </summary>
    public class TutorialHubUI : MonoBehaviour
    {
        public static TutorialHubUI Instance { get; private set; }
        public static bool IsHubOpen => Instance != null && Instance._isOpen;

        private bool _isOpen = false;
        private TutorialHubCategory _activeCategory = TutorialHubCategory.Overview;
        private Vector2 _scrollPos = Vector2.zero;

        // Estilos y texturas OnGUI cacheadas
        private bool _stylesInitialized = false;
        private int _lastWidth = 0;
        private int _lastHeight = 0;

        private Texture2D _backdropTex;
        private Texture2D _panelBgTex;
        private Texture2D _cardBgTex;
        private Texture2D _goldFillTex;
        private Texture2D _blueFillTex;
        private Texture2D _darkTabTex;

        private GUIStyle _headerTitleStyle;
        private GUIStyle _headerSubStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _sectionSubStyle;
        private GUIStyle _tabBtnStyle;
        private GUIStyle _activeTabBtnStyle;
        private GUIStyle _cardTagStyle;
        private GUIStyle _cardTitleStyle;
        private GUIStyle _cardBodyStyle;
        private GUIStyle _primaryBtnStyle;
        private GUIStyle _secondaryBtnStyle;
        private GUIStyle _closeBtnStyle;

        public bool IsOpen => _isOpen;
        public TutorialHubCategory ActiveCategory => _activeCategory;

        public static TutorialHubUI EnsureInstance()
        {
            if (Instance != null) return Instance;

            TutorialHubUI existing = FindAnyObjectByType<TutorialHubUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            OnboardingManager om = OnboardingManager.EnsureInstance();
            GameObject host = om != null ? om.gameObject : new GameObject("TutorialHubUI");
            Instance = host.AddComponent<TutorialHubUI>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DestroyCachedTextures();
        }

        public void Open(TutorialHubCategory initialCategory = TutorialHubCategory.Overview)
        {
            _activeCategory = initialCategory;
            _scrollPos = Vector2.zero;
            _isOpen = true;
            RetroAudioManager.Instance?.PlayTap();
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            RetroAudioManager.Instance?.PlayTap();
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            GUI.depth = -1950;
            InitStylesIfNeeded();

            float sw = Screen.width;
            float sh = Screen.height;
            bool isLandscape = sw > sh * 1.15f;
            float scale = !isLandscape
                ? Mathf.Clamp(Mathf.Min(sw / 1080f, sh / 1920f), 0.56f, 1.35f)
                : Mathf.Clamp(sh / 1080f, 0.62f, 1.25f);

            // Fondo oscurecido que bloquea clics hacia atrás
            GUI.DrawTexture(new Rect(0f, 0f, sw, sh), _backdropTex);

            float safeTop = Mathf.Max(0f, Screen.height - Screen.safeArea.yMax);
            float safeBottom = Mathf.Max(0f, Screen.safeArea.yMin);
            float safeLeft = Mathf.Max(0f, Screen.safeArea.xMin);
            float safeRight = Mathf.Max(0f, Screen.width - Screen.safeArea.xMax);
            float horizSafe = Mathf.Max(safeLeft, safeRight);

            float marginH = horizSafe + (16f * scale);
            float marginTop = safeTop + (14f * scale);
            float marginBottom = safeBottom + (14f * scale);

            float panelW = Mathf.Min(sw - marginH * 2f, isLandscape ? 1180f * scale : 1020f * scale);
            float panelH = Mathf.Max(260f, sh - marginTop - marginBottom);
            float panelX = (sw - panelW) * 0.5f;
            float panelY = marginTop;

            Rect panelRect = new Rect(panelX, panelY, panelW, panelH);
            DrawBox(panelRect, _panelBgTex, new Color(0.95f, 0.78f, 0.22f, 1f), 2.5f);

            float pad = 18f * scale;
            float innerX = panelX + pad;
            float innerW = Mathf.Max(140f, panelW - pad * 2f);
            float curY = panelY + pad;

            // =========================================================================
            // 1. ENCABEZADO SUPERIOR + BOTÓN VOLVER
            // =========================================================================
            float closeW = Mathf.Clamp(155f * scale, 110f, 210f);
            float headerBtnH = Mathf.Clamp(46f * scale, 36f, 56f);
            Rect closeRect = new Rect(panelX + panelW - pad - closeW, curY, closeW, headerBtnH);

            float titleAreaW = Mathf.Max(100f, innerW - closeW - 10f * scale);
            GUI.Label(new Rect(innerX, curY, titleAreaW, 30f * scale), "★ CENTRO DE TUTORIALES · TODO EL JUEGO", _headerTitleStyle);
            GUI.Label(new Rect(innerX, curY + 30f * scale, titleAreaW, 24f * scale), "Guía completa de modos, piezas, cartas y ajustes", _headerSubStyle);

            if (DrawStyledButton(closeRect, "✕ CERRAR", _closeBtnStyle, _darkTabTex, new Color(0.85f, 0.42f, 0.36f, 1f)))
            {
                Close();
                ConsumeAllClicksInRect(new Rect(0, 0, sw, sh));
                return;
            }

            curY += Mathf.Max(headerBtnH, 56f * scale) + 10f * scale;

            // =========================================================================
            // 2. PESTAÑAS DE LAS 5 CATEGORÍAS (ADAPTATIVAS EN 1 O 2 FILAS)
            // =========================================================================
            TutorialHubCategory[] cats = TutorialCatalogLogic.AllCategories;
            bool twoRows = !isLandscape && innerW < 720f;
            float tabGap = 6f * scale;
            float tabH = Mathf.Clamp(44f * scale, 34f, 52f);

            if (!twoRows)
            {
                float tabW = (innerW - tabGap * (cats.Length - 1)) / cats.Length;
                for (int i = 0; i < cats.Length; i++)
                {
                    Rect tRect = new Rect(innerX + i * (tabW + tabGap), curY, tabW, tabH);
                    DrawCategoryTab(tRect, cats[i]);
                }
                curY += tabH + 10f * scale;
            }
            else
            {
                // Fila 1: 3 pestañas, Fila 2: 2 pestañas
                float row1W = (innerW - tabGap * 2f) / 3f;
                for (int i = 0; i < 3; i++)
                {
                    Rect tRect = new Rect(innerX + i * (row1W + tabGap), curY, row1W, tabH);
                    DrawCategoryTab(tRect, cats[i]);
                }
                curY += tabH + tabGap;

                float row2W = (innerW - tabGap) / 2f;
                for (int i = 3; i < cats.Length; i++)
                {
                    Rect tRect = new Rect(innerX + (i - 3) * (row2W + tabGap), curY, row2W, tabH);
                    DrawCategoryTab(tRect, cats[i]);
                }
                curY += tabH + 10f * scale;
            }

            // =========================================================================
            // 3. SUB-ENCABEZADO DE LA CATEGORÍA ACTIVA
            // =========================================================================
            TutorialCatalogLogic.GetCategoryHeader(_activeCategory, out string catTitle, out string catSub, out Color catAccent);
            float catTitleH = Mathf.Max(26f * scale, _sectionTitleStyle.CalcHeight(new GUIContent(catTitle), innerW - 16f * scale) + 2f);
            float catSubH = Mathf.Max(22f * scale, _sectionSubStyle.CalcHeight(new GUIContent(catSub), innerW - 16f * scale) + 4f);
            float catBoxH = catTitleH + catSubH + 16f * scale;

            DrawBox(new Rect(innerX, curY, innerW, catBoxH), _cardBgTex, catAccent, 2f);
            Color oldCol = GUI.color;
            GUI.color = catAccent;
            GUI.Label(new Rect(innerX + 10f * scale, curY + 6f * scale, innerW - 20f * scale, catTitleH), catTitle, _sectionTitleStyle);
            GUI.color = oldCol;
            GUI.Label(new Rect(innerX + 10f * scale, curY + 8f * scale + catTitleH, innerW - 20f * scale, catSubH), catSub, _sectionSubStyle);

            curY += catBoxH + 10f * scale;

            // =========================================================================
            // 4. BARRA INFERIOR DE NAVEGACIÓN ENTRE TUTORIALES + TOUR EN VIVO
            // =========================================================================
            float footerH = Mathf.Clamp(52f * scale, 42f, 62f);
            float footerY = panelY + panelH - pad - footerH;

            int activeIdx = Array.IndexOf(cats, _activeCategory);
            if (activeIdx < 0) activeIdx = 0;

            float fGap = 8f * scale;
            float prevNextW = Mathf.Clamp(innerW * 0.24f, 92f, 180f);
            float tourBtnW = Mathf.Max(120f, innerW - prevNextW * 2f - fGap * 2f);

            Rect prevRect = new Rect(innerX, footerY, prevNextW, footerH);
            Rect tourRect = new Rect(prevRect.xMax + fGap, footerY, tourBtnW, footerH);
            Rect nextRect = new Rect(tourRect.xMax + fGap, footerY, prevNextW, footerH);

            if (DrawStyledButton(prevRect, "◄ Anterior", _secondaryBtnStyle, _darkTabTex, new Color(0.38f, 0.48f, 0.64f, 1f)))
            {
                int prevIdx = (activeIdx - 1 + cats.Length) % cats.Length;
                _activeCategory = cats[prevIdx];
                _scrollPos = Vector2.zero;
                RetroAudioManager.Instance?.PlayTap();
            }

            if (DrawStyledButton(tourRect, "▶ TOUR GUIADO EN VIVO (FLECHA)", _primaryBtnStyle, _goldFillTex, new Color(1f, 0.94f, 0.54f, 1f)))
            {
                ExecuteQuickAction(TutorialQuickAction.StartInteractiveTour);
                ConsumeAllClicksInRect(new Rect(0, 0, sw, sh));
                return;
            }

            if (DrawStyledButton(nextRect, "Siguiente ►", _secondaryBtnStyle, _blueFillTex, new Color(0.42f, 0.72f, 1f, 1f)))
            {
                int nextIdx = (activeIdx + 1) % cats.Length;
                _activeCategory = cats[nextIdx];
                _scrollPos = Vector2.zero;
                RetroAudioManager.Instance?.PlayTap();
            }

            // =========================================================================
            // 5. ZONA SCROLLEABLE DE TARJETAS DE TUTORIAL DE LA CATEGORÍA
            // =========================================================================
            float scrollY = curY;
            float scrollH = Mathf.Max(80f, footerY - scrollY - 10f * scale);
            List<TutorialTopicEntry> topics = TutorialCatalogLogic.GetTopics(_activeCategory);

            float scrollInnerW = innerW - (18f * scale);
            float totalContentH = MeasureTopicsHeight(topics, scrollInnerW, scale);

            Rect viewRect = new Rect(innerX, scrollY, innerW, scrollH);
            Rect contentRect = new Rect(0f, 0f, scrollInnerW, Mathf.Max(totalContentH, scrollH));
            _scrollPos = GUI.BeginScrollView(viewRect, _scrollPos, contentRect, false, totalContentH > scrollH);

            float cardY = 0f;
            float cardPad = 14f * scale;
            float cardGap = 10f * scale;

            for (int i = 0; i < topics.Count; i++)
            {
                TutorialTopicEntry entry = topics[i];
                bool hasAction = entry.Action != TutorialQuickAction.None && !string.IsNullOrEmpty(entry.ActionButtonLabel);
                float cardH = MeasureSingleCardHeight(entry, scrollInnerW, cardPad, scale, hasAction);

                Rect cardRect = new Rect(0f, cardY, scrollInnerW, cardH);
                DrawBox(cardRect, _cardBgTex, Color.Lerp(catAccent, new Color(0.28f, 0.36f, 0.48f, 1f), 0.45f), 2f);

                float textW = scrollInnerW - cardPad * 2f;
                float cy = cardY + cardPad * 0.8f;

                if (!string.IsNullOrEmpty(entry.Tag))
                {
                    GUI.color = catAccent;
                    GUI.Label(new Rect(cardPad, cy, textW, 20f * scale), $"■ {entry.Tag}", _cardTagStyle);
                    GUI.color = oldCol;
                    cy += 22f * scale;
                }

                float titleH = Mathf.Max(24f * scale, _cardTitleStyle.CalcHeight(new GUIContent(entry.Title), textW) + 2f);
                GUI.Label(new Rect(cardPad, cy, textW, titleH), entry.Title, _cardTitleStyle);
                cy += titleH + 6f * scale;

                float bodyH = Mathf.Max(26f * scale, _cardBodyStyle.CalcHeight(new GUIContent(entry.Summary), textW) + 4f);
                GUI.Label(new Rect(cardPad, cy, textW, bodyH), entry.Summary, _cardBodyStyle);
                cy += bodyH;

                if (hasAction)
                {
                    cy += 10f * scale;
                    float actionBtnW = Mathf.Min(textW, 330f * scale);
                    float actionBtnH = Mathf.Clamp(42f * scale, 34f, 48f);
                    Rect actionRect = new Rect(cardPad, cy, actionBtnW, actionBtnH);
                    if (DrawStyledButton(actionRect, entry.ActionButtonLabel, _primaryBtnStyle, _goldFillTex, new Color(1f, 0.94f, 0.56f, 1f)))
                    {
                        ExecuteQuickAction(entry.Action);
                        GUI.EndScrollView();
                        ConsumeAllClicksInRect(new Rect(0, 0, sw, sh));
                        return;
                    }
                }

                cardY += cardH + cardGap;
            }

            GUI.EndScrollView();

            // Bloquear clics restantes para que nada detrás reciba pulsaciones
            ConsumeAllClicksInRect(new Rect(0f, 0f, sw, sh));
        }

        private void DrawCategoryTab(Rect rect, TutorialHubCategory cat)
        {
            bool isActive = _activeCategory == cat;
            string label = TutorialCatalogLogic.GetCategoryTabLabel(cat);
            Texture2D fill = isActive ? _goldFillTex : _darkTabTex;
            Color border = isActive ? new Color(1f, 0.92f, 0.45f, 1f) : new Color(0.32f, 0.42f, 0.56f, 1f);
            GUIStyle style = isActive ? _activeTabBtnStyle : _tabBtnStyle;

            if (DrawStyledButton(rect, label, style, fill, border))
            {
                _activeCategory = cat;
                _scrollPos = Vector2.zero;
                RetroAudioManager.Instance?.PlayTap();
            }
        }

        private float MeasureTopicsHeight(List<TutorialTopicEntry> topics, float innerW, float scale)
        {
            float cardPad = 14f * scale;
            float cardGap = 10f * scale;
            float total = 0f;
            for (int i = 0; i < topics.Count; i++)
            {
                bool hasAction = topics[i].Action != TutorialQuickAction.None && !string.IsNullOrEmpty(topics[i].ActionButtonLabel);
                total += MeasureSingleCardHeight(topics[i], innerW, cardPad, scale, hasAction) + cardGap;
            }
            return total;
        }

        private float MeasureSingleCardHeight(TutorialTopicEntry entry, float width, float pad, float scale, bool hasAction)
        {
            float textW = Mathf.Max(80f, width - pad * 2f);
            float h = pad * 0.8f;

            if (!string.IsNullOrEmpty(entry.Tag))
            {
                h += 22f * scale;
            }

            float titleH = Mathf.Max(24f * scale, _cardTitleStyle.CalcHeight(new GUIContent(entry.Title), textW) + 2f);
            float bodyH = Mathf.Max(26f * scale, _cardBodyStyle.CalcHeight(new GUIContent(entry.Summary), textW) + 4f);
            h += titleH + 6f * scale + bodyH + pad * 0.8f;

            if (hasAction)
            {
                h += 10f * scale + Mathf.Clamp(42f * scale, 34f, 48f);
            }

            return h;
        }

        public void ExecuteQuickAction(TutorialQuickAction action)
        {
            RetroAudioManager.Instance?.PlayTap();
            Close();

            switch (action)
            {
                case TutorialQuickAction.StartInteractiveTour:
                    OnboardingManager.EnsureInstance().StartOnboarding();
                    break;

                case TutorialQuickAction.LaunchClassicQuickSetup:
                    OnboardingManager.Instance?.CancelActiveOnboardingWithoutStartingMatch();
                    GameManager.SelectedGameMode = GameMode.ClassicRoguelike;
                    if (MainMenuManager.Instance != null)
                    {
                        MainMenuManager.Instance.SwitchPanel(MainMenuManager.MenuPanel.QuickMatchSetup);
                    }
                    else if (GameManager.Instance != null)
                    {
                        GameManager.Instance.StartNewGame();
                    }
                    else if (Application.CanStreamedLevelBeLoaded("MainMenuScene"))
                    {
                        SceneManager.LoadSceneAsync("MainMenuScene");
                    }
                    break;

                case TutorialQuickAction.LaunchArcade:
                    OnboardingManager.Instance?.CancelActiveOnboardingWithoutStartingMatch();
                    GameManager.SelectedGameMode = GameMode.Arcade;
                    if (MainMenuManager.Instance != null)
                    {
                        MainMenuManager.Instance.StartGameWithMode(GameMode.Arcade);
                    }
                    else if (GameManager.Instance != null)
                    {
                        GameManager.Instance.StartNewGame();
                    }
                    break;

                case TutorialQuickAction.OpenEducationAcademy:
                    OnboardingManager.Instance?.CancelActiveOnboardingWithoutStartingMatch();
                    GameManager.SelectedGameMode = GameMode.Education;
                    GameManager.PendingOpenEducationSelector = true;
                    GameManager.PendingEducationLessonIndex = 0;
                    if (MainMenuManager.Instance != null)
                    {
                        MainMenuManager.Instance.StartGameWithMode(GameMode.Education);
                    }
                    else if (GameManager.Instance != null)
                    {
                        GameManager.Instance.StartNewGame();
                    }
                    break;

                case TutorialQuickAction.StartEducationLesson1:
                    OnboardingManager.Instance?.CancelActiveOnboardingWithoutStartingMatch();
                    GameManager.SelectedGameMode = GameMode.Education;
                    GameManager.PendingOpenEducationSelector = false;
                    GameManager.PendingEducationLessonIndex = 0;
                    if (MainMenuManager.Instance != null)
                    {
                        MainMenuManager.Instance.StartGameWithMode(GameMode.Education);
                    }
                    else if (GameManager.Instance != null)
                    {
                        GameManager.Instance.StartNewGame();
                    }
                    break;

                case TutorialQuickAction.OpenSkinsMenu:
                    OnboardingManager.Instance?.CancelActiveOnboardingWithoutStartingMatch();
                    if (MainMenuManager.Instance != null)
                    {
                        MainMenuManager.Instance.SwitchPanel(MainMenuManager.MenuPanel.Skins);
                    }
                    else if (GameManager.Instance != null)
                    {
                        GameManager.Instance.ReturnToMenu();
                    }
                    break;

                case TutorialQuickAction.OpenOptionsMenu:
                    OnboardingManager.Instance?.CancelActiveOnboardingWithoutStartingMatch();
                    if (MainMenuManager.Instance != null)
                    {
                        MainMenuManager.Instance.SwitchPanel(MainMenuManager.MenuPanel.Options);
                    }
                    else if (GameManager.Instance != null)
                    {
                        GameManager.Instance.ReturnToMenu();
                    }
                    break;
            }
        }

        private bool DrawStyledButton(Rect rect, string label, GUIStyle textStyle, Texture2D bgTex, Color borderCol)
        {
            Color oldCol = GUI.color;
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.85f);
            GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.DrawTexture(rect, bgTex);

            GUI.color = borderCol;
            DrawBorder(rect, 2f);
            GUI.color = oldCol;

            return GUI.Button(rect, label, textStyle);
        }

        private static void DrawBox(Rect rect, Texture2D bgTex, Color borderColor, float borderWidth)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.85f);
            GUI.DrawTexture(new Rect(rect.x + 3, rect.y + 3, rect.width, rect.height), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.DrawTexture(rect, bgTex);

            GUI.color = borderColor;
            DrawBorder(rect, borderWidth);
            GUI.color = old;
        }

        private static void DrawBorder(Rect rect, float t)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - t, rect.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, t, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - t, rect.y, t, rect.height), Texture2D.whiteTexture);
        }

        private static void ConsumeAllClicksInRect(Rect rect)
        {
            Event ev = Event.current;
            if (ev != null && (ev.type == EventType.MouseDown || ev.type == EventType.MouseUp) && rect.Contains(ev.mousePosition))
            {
                ev.Use();
            }
        }

        private void InitStylesIfNeeded()
        {
            if (_stylesInitialized && _lastWidth == Screen.width && _lastHeight == Screen.height) return;

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;

            float sw = Screen.width;
            float sh = Screen.height;
            bool isLandscape = sw > sh * 1.15f;
            float scale = !isLandscape
                ? Mathf.Clamp(Mathf.Min(sw / 1080f, sh / 1920f), 0.56f, 1.35f)
                : Mathf.Clamp(sh / 1080f, 0.62f, 1.25f);

            if (_backdropTex == null) _backdropTex = MakeTex(new Color(0.01f, 0.02f, 0.05f, 0.86f));
            if (_panelBgTex == null) _panelBgTex = MakeTex(new Color(0.07f, 0.09f, 0.14f, 0.98f));
            if (_cardBgTex == null) _cardBgTex = MakeTex(new Color(0.11f, 0.14f, 0.21f, 0.97f));
            if (_goldFillTex == null) _goldFillTex = MakeTex(new Color(0.88f, 0.68f, 0.16f, 1f));
            if (_blueFillTex == null) _blueFillTex = MakeTex(new Color(0.16f, 0.34f, 0.58f, 1f));
            if (_darkTabTex == null) _darkTabTex = MakeTex(new Color(0.15f, 0.19f, 0.28f, 1f));

            Font bold = ChessFontProvider.SerifBold;
            Font regular = ChessFontProvider.SerifRegular;

            _headerTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.Max(16, Mathf.RoundToInt(24 * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.88f, 0.32f, 1f) }
            };

            _headerSubStyle = new GUIStyle(GUI.skin.label)
            {
                font = regular,
                fontSize = Mathf.Max(12, Mathf.RoundToInt(16 * scale)),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.82f, 0.88f, 0.96f, 1f) }
            };

            _sectionTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.Max(15, Mathf.RoundToInt(21 * scale)),
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _sectionSubStyle = new GUIStyle(GUI.skin.label)
            {
                font = regular,
                fontSize = Mathf.Max(12, Mathf.RoundToInt(17 * scale)),
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.86f, 0.90f, 0.96f, 1f) }
            };

            _tabBtnStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.Max(11, Mathf.RoundToInt(16 * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.90f, 0.94f, 0.99f, 1f) }
            };

            _activeTabBtnStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.Max(11, Mathf.RoundToInt(16 * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.08f, 0.06f, 0.02f, 1f) }
            };

            _cardTagStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.Max(11, Mathf.RoundToInt(14 * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _cardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.Max(15, Mathf.RoundToInt(20 * scale)),
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.92f, 0.48f, 1f) }
            };

            _cardBodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = regular,
                fontSize = Mathf.Max(13, Mathf.RoundToInt(18 * scale)),
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.92f, 0.95f, 0.99f, 1f) }
            };

            _primaryBtnStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.Max(12, Mathf.RoundToInt(18 * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.08f, 0.06f, 0.02f, 1f) }
            };

            _secondaryBtnStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.Max(12, Mathf.RoundToInt(17 * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.94f, 0.96f, 1f, 1f) }
            };

            _closeBtnStyle = new GUIStyle(GUI.skin.label)
            {
                font = bold,
                fontSize = Mathf.Max(12, Mathf.RoundToInt(17 * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.80f, 1f) }
            };

            _stylesInitialized = true;
        }

        private static Texture2D MakeTex(Color col)
        {
            Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.SetPixels(new Color[] { col, col, col, col });
            t.Apply();
            return t;
        }

        private void DestroyCachedTextures()
        {
            if (_backdropTex != null) Destroy(_backdropTex);
            if (_panelBgTex != null) Destroy(_panelBgTex);
            if (_cardBgTex != null) Destroy(_cardBgTex);
            if (_goldFillTex != null) Destroy(_goldFillTex);
            if (_blueFillTex != null) Destroy(_blueFillTex);
            if (_darkTabTex != null) Destroy(_darkTabTex);
        }
    }
}

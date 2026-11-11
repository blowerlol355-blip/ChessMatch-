using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessBalatro
{
    /// <summary>
    /// Singleton persistente encargado de reproducir efectos de sonido (SFX) retro/arcade.
    /// Usa los AudioClip asignados en el Inspector si están disponibles y genera formas de onda
    /// procedurales (AudioClip.Create) en memoria como fallback automático si algún clip es null.
    /// </summary>
    public class RetroAudioManager : MonoBehaviour
    {
        private const string PrefKeySFXVolume = "ChessMatch_SFXVolume";
        private const int SampleRate = 44100;
        private const int AudioSourcePoolSize = 6;
        private const float MinPitch = 0.95f;
        private const float MaxPitch = 1.05f;

        private static RetroAudioManager _instance;
        private static bool _isQuitting = false;

        public static RetroAudioManager Instance
        {
            get
            {
                if (_isQuitting) return null;
                if (_instance == null)
                {
                    EnsureInstance();
                }
                return _instance;
            }
        }

        [Header("Clips Opcionales (Fallback Procedural Automático si son null)")]
        [SerializeField] private AudioClip screenTapClip;
        [SerializeField] private AudioClip pieceSelectClip;
        [SerializeField] private AudioClip pieceMoveClip;
        [SerializeField] private AudioClip pieceCaptureClip;
        [SerializeField] private AudioClip checkAlertClip;
        [SerializeField] private AudioClip cardActivateClip;
        [SerializeField] private AudioClip rewardSoundClip;

        private readonly List<AudioSource> _sourcePool = new List<AudioSource>(AudioSourcePoolSize);
        private readonly List<AudioClip> _generatedClips = new List<AudioClip>();
        private int _poolCursor = 0;
        private bool _isInitialized = false;

        // Clips procedurales cacheados en memoria
        private AudioClip _procTapClip;
        private AudioClip _procPieceSelectClip;
        private AudioClip _procPieceMoveClip;
        private AudioClip _procPieceCaptureClip;
        private AudioClip _procCheckAlertClip;
        private AudioClip _procShieldActivateClip;
        private AudioClip _procRewardClip;

        // Deduplicación por fotograma para evitar solapamientos indeseados
        private int _lastPieceSoundFrame = -1;
        private int _lastTapSoundFrame = -1;
        private int _lastCheckSoundFrame = -1;
        private int _lastCardSoundFrame = -1;

        public int LastPieceSoundFrame => _lastPieceSoundFrame;
        public int LastTapSoundFrame => _lastTapSoundFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeOnLaunch()
        {
            if (!_isQuitting)
            {
                EnsureInstance();
            }
        }

        /// <summary>
        /// Garantiza que exista una única instancia persistente en ejecución.
        /// </summary>
        public static RetroAudioManager EnsureInstance()
        {
            if (_isQuitting || !Application.isPlaying) return _instance;

            if (_instance == null)
            {
                RetroAudioManager existing = FindAnyObjectByType<RetroAudioManager>(FindObjectsInactive.Include);
                if (existing != null)
                {
                    _instance = existing;
                    if (!existing.gameObject.activeSelf)
                    {
                        existing.gameObject.SetActive(true);
                    }
                    existing.InitializeIfNeeded();
                    existing.MakePersistent();
                }
                else
                {
                    GameObject go = new GameObject("RetroAudioManager");
                    _instance = go.AddComponent<RetroAudioManager>();
                    _instance.InitializeIfNeeded();
                    _instance.MakePersistent();
                }
            }

            _instance.EnsureGlobalTouchDetector();
            return _instance;
        }

        private void MakePersistent()
        {
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            MakePersistent();
            InitializeIfNeeded();
            EnsureGlobalTouchDetector();
        }

        private void OnEnable()
        {
            PetrosianDefenseEffect.OnShieldActivated -= HandlePetrosianShieldActivated;
            PetrosianDefenseEffect.OnShieldActivated += HandlePetrosianShieldActivated;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            PetrosianDefenseEffect.OnShieldActivated -= HandlePetrosianShieldActivated;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            for (int i = 0; i < _generatedClips.Count; i++)
            {
                if (_generatedClips[i] != null)
                {
                    Destroy(_generatedClips[i]);
                }
            }
            _generatedClips.Clear();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureGlobalTouchDetector();
        }

        private void HandlePetrosianShieldActivated(BoardCoord kingCoord)
        {
            PlayShieldActivate();
        }

        private void InitializeIfNeeded()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            EnsureAudioSourcePool();
            EnsureProceduralClips();
        }

        private void EnsureAudioSourcePool()
        {
            if (_sourcePool.Count > 0) return;

            AudioSource[] existingSources = GetComponents<AudioSource>();
            for (int i = 0; i < existingSources.Length; i++)
            {
                ConfigureAudioSource(existingSources[i]);
                _sourcePool.Add(existingSources[i]);
            }

            while (_sourcePool.Count < AudioSourcePoolSize)
            {
                AudioSource src = gameObject.AddComponent<AudioSource>();
                ConfigureAudioSource(src);
                _sourcePool.Add(src);
            }
        }

        private static void ConfigureAudioSource(AudioSource src)
        {
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.priority = 64;
        }

        private void EnsureGlobalTouchDetector()
        {
            if (!Application.isPlaying || _isQuitting) return;

            GlobalTouchSound existingDetector = FindAnyObjectByType<GlobalTouchSound>(FindObjectsInactive.Include);
            if (existingDetector == null)
            {
                if (GetComponent<GlobalTouchSound>() == null)
                {
                    gameObject.AddComponent<GlobalTouchSound>();
                }
            }
            else if (!existingDetector.enabled)
            {
                existingDetector.enabled = true;
            }
        }

        private void EnsureProceduralClips()
        {
            if (_procTapClip == null)           _procTapClip = BuildTapClip();
            if (_procPieceSelectClip == null)   _procPieceSelectClip = BuildPieceSelectClip();
            if (_procPieceMoveClip == null)     _procPieceMoveClip = BuildPieceMoveClip();
            if (_procPieceCaptureClip == null)  _procPieceCaptureClip = BuildPieceCaptureClip();
            if (_procCheckAlertClip == null)    _procCheckAlertClip = BuildCheckAlertClip();
            if (_procShieldActivateClip == null)_procShieldActivateClip = BuildShieldActivateClip();
            if (_procRewardClip == null)        _procRewardClip = BuildRewardClip();
        }

        #region API Pública de Reproducción SFX

        /// <summary>
        /// Blip corto y suave (800Hz -> 1200Hz, 0.04s) al tocar la pantalla o botones.
        /// </summary>
        public void PlayTap()
        {
            InitializeIfNeeded();
            if (_lastTapSoundFrame == Time.frameCount) return;

            _lastTapSoundFrame = Time.frameCount;
            AudioClip clip = screenTapClip != null ? screenTapClip : _procTapClip;
            PlayClipWithJitter(clip);
        }

        /// <summary>
        /// Reproduce un toque de pantalla únicamente si no se reprodujo un sonido de pieza o botón en este mismo frame.
        /// </summary>
        public void PlayTapIfNoPieceSoundThisFrame()
        {
            int frame = Time.frameCount;
            if (_lastPieceSoundFrame == frame || _lastTapSoundFrame == frame || _lastCardSoundFrame == frame)
            {
                return;
            }
            PlayTap();
        }

        /// <summary>
        /// Clic táctil (600Hz, 0.05s) al levantar o seleccionar una pieza propia válida.
        /// </summary>
        public void PlayPieceSelect()
        {
            InitializeIfNeeded();
            _lastPieceSoundFrame = Time.frameCount;
            AudioClip clip = pieceSelectClip != null ? pieceSelectClip : _procPieceSelectClip;
            PlayClipWithJitter(clip);
        }

        /// <summary>
        /// Golpe grave de madera/ficha (180Hz -> 90Hz con ligero ruido blanco, 0.08s) al posar la pieza.
        /// </summary>
        public void PlayPieceMove()
        {
            InitializeIfNeeded();
            _lastPieceSoundFrame = Time.frameCount;
            AudioClip clip = pieceMoveClip != null ? pieceMoveClip : _procPieceMoveClip;
            PlayClipWithJitter(clip);
        }

        /// <summary>
        /// Impacto contundente (300Hz -> 80Hz, 0.15s) al comer una pieza enemiga.
        /// </summary>
        public void PlayPieceCapture()
        {
            InitializeIfNeeded();
            _lastPieceSoundFrame = Time.frameCount;
            AudioClip clip = pieceCaptureClip != null ? pieceCaptureClip : _procPieceCaptureClip;
            PlayClipWithJitter(clip);
        }

        /// <summary>
        /// Doble tono de alerta arcade (440Hz -> 880Hz, 0.25s) cuando un movimiento provoca Jaque.
        /// </summary>
        public void PlayCheckAlert()
        {
            InitializeIfNeeded();
            if (_lastCheckSoundFrame == Time.frameCount) return;

            _lastCheckSoundFrame = Time.frameCount;
            _lastPieceSoundFrame = Time.frameCount;
            AudioClip clip = checkAlertClip != null ? checkAlertClip : _procCheckAlertClip;
            PlayClipWithJitter(clip);
        }

        /// <summary>
        /// Arpegio ascendente brillante para el Escudo de Tigrán Petrosián.
        /// </summary>
        public void PlayShieldActivate()
        {
            InitializeIfNeeded();
            if (_lastCardSoundFrame == Time.frameCount) return;

            _lastCardSoundFrame = Time.frameCount;
            _lastPieceSoundFrame = Time.frameCount;
            AudioClip clip = cardActivateClip != null ? cardActivateClip : _procShieldActivateClip;
            PlayClipWithJitter(clip);
        }

        /// <summary>
        /// Sonido arcade al activar una carta de poder (usa cardActivateClip o arpegio fallback si no sonó ya el escudo en este frame).
        /// </summary>
        public void PlayCardActivate()
        {
            InitializeIfNeeded();
            if (_lastCardSoundFrame == Time.frameCount) return;

            _lastCardSoundFrame = Time.frameCount;
            _lastPieceSoundFrame = Time.frameCount;
            AudioClip clip = cardActivateClip != null ? cardActivateClip : _procShieldActivateClip;
            PlayClipWithJitter(clip);
        }

        /// <summary>
        /// Fanfarria retro de recompensa al reclamar una carta en el Draft de capturas.
        /// </summary>
        public void PlayRewardSound()
        {
            InitializeIfNeeded();
            _lastCardSoundFrame = Time.frameCount;
            _lastPieceSoundFrame = Time.frameCount;
            AudioClip clip = rewardSoundClip != null ? rewardSoundClip : _procRewardClip;
            PlayClipWithJitter(clip);
        }

        #endregion

        #region Reproducción y Volumen

        private float GetEffectiveSFXVolume()
        {
            if (SettingsManager.Instance != null)
            {
                return Mathf.Clamp01(SettingsManager.Instance.SFXVolume);
            }
            return Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeySFXVolume, 1.0f));
        }

        private void PlayClipWithJitter(AudioClip clip)
        {
            if (clip == null) return;

            float volume = GetEffectiveSFXVolume();
            if (volume <= 0.001f) return;

            AudioSource src = AcquireAudioSource();
            if (src == null) return;

            src.pitch = UnityEngine.Random.Range(MinPitch, MaxPitch);
            src.PlayOneShot(clip, volume);
        }

        private AudioSource AcquireAudioSource()
        {
            EnsureAudioSourcePool();
            for (int i = 0; i < _sourcePool.Count; i++)
            {
                if (_sourcePool[i] != null && !_sourcePool[i].isPlaying)
                {
                    return _sourcePool[i];
                }
            }

            if (_sourcePool.Count == 0) return null;
            AudioSource fallback = _sourcePool[_poolCursor % _sourcePool.Count];
            _poolCursor = (_poolCursor + 1) % _sourcePool.Count;
            return fallback;
        }

        #endregion

        #region Generación Procedural de Ondas Retro (AudioClip.Create)

        private AudioClip CreateClipFromSamples(string clipName, float[] samples)
        {
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            _generatedClips.Add(clip);
            return clip;
        }

        private static float SampleTriangle(double phaseRadians)
        {
            double norm = (phaseRadians / (Math.PI * 2.0)) % 1.0;
            if (norm < 0.0) norm += 1.0;
            return (float)(1.0 - 4.0 * Math.Abs(norm - 0.5));
        }

        private static float SampleSquare(double phaseRadians)
        {
            double norm = (phaseRadians / (Math.PI * 2.0)) % 1.0;
            if (norm < 0.0) norm += 1.0;
            return norm < 0.5 ? 1f : -1f;
        }

        private AudioClip BuildTapClip()
        {
            const float duration = 0.04f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[count];
            double phase = 0.0;
            const double twoPi = Math.PI * 2.0;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                float sec = (float)i / SampleRate;

                float freq = Mathf.Lerp(800f, 1200f, t);
                phase += twoPi * freq / SampleRate;

                // Onda triangular suave con un toque cuadrado discreto y decaimiento rápido
                float wave = SampleTriangle(phase) * 0.78f + SampleSquare(phase) * 0.22f;
                float attack = Mathf.Clamp01(sec / 0.003f);
                float decay = Mathf.Exp(-6.5f * t) * (1f - t * t);

                data[i] = Mathf.Clamp(wave * attack * decay * 0.22f, -1f, 1f);
            }

            return CreateClipFromSamples("SFX_Procedural_Tap", data);
        }

        private AudioClip BuildPieceSelectClip()
        {
            const float duration = 0.05f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[count];
            double phase = 0.0;
            const double twoPi = Math.PI * 2.0;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                float sec = (float)i / SampleRate;

                // Clic táctil centrado en 600Hz
                const float freq = 600f;
                phase += twoPi * freq / SampleRate;

                float wave = SampleSquare(phase) * 0.55f + SampleTriangle(phase) * 0.45f;
                float attack = Mathf.Clamp01(sec / 0.002f);
                float decay = Mathf.Exp(-8.5f * t) * (1f - Mathf.Pow(t, 4f));

                data[i] = Mathf.Clamp(wave * attack * decay * 0.26f, -1f, 1f);
            }

            return CreateClipFromSamples("SFX_Procedural_PieceSelect", data);
        }

        private AudioClip BuildPieceMoveClip()
        {
            const float duration = 0.08f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[count];
            double phase = 0.0;
            const double twoPi = Math.PI * 2.0;
            System.Random rng = new System.Random(1337);

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                float sec = (float)i / SampleRate;

                float freq = Mathf.Lerp(180f, 90f, Mathf.Pow(t, 0.65f));
                phase += twoPi * freq / SampleRate;

                // Cuerpo grave de madera/ficha + ligero ruido blanco en el contacto inicial
                float bodyWave = SampleTriangle(phase) * 0.72f + SampleSquare(phase) * 0.28f;
                float bodyEnv = Mathf.Clamp01(sec / 0.002f) * Mathf.Exp(-7.5f * t) * (1f - t * t);

                float whiteNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float noiseEnv = Mathf.Exp(-24f * t) * 0.24f;

                float mixed = (bodyWave * bodyEnv) + (whiteNoise * noiseEnv);
                data[i] = Mathf.Clamp(mixed * 0.44f, -1f, 1f);
            }

            return CreateClipFromSamples("SFX_Procedural_PieceMove", data);
        }

        private AudioClip BuildPieceCaptureClip()
        {
            const float duration = 0.15f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[count];
            double phase = 0.0;
            double subPhase = 0.0;
            const double twoPi = Math.PI * 2.0;
            System.Random rng = new System.Random(7331);

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                float sec = (float)i / SampleRate;

                float freq = Mathf.Lerp(300f, 80f, Mathf.Pow(t, 0.55f));
                phase += twoPi * freq / SampleRate;
                subPhase += twoPi * (freq * 0.5f) / SampleRate;

                float impactWave = SampleSquare(phase) * 0.62f + SampleTriangle(subPhase) * 0.38f;
                float env = Mathf.Clamp01(sec / 0.002f) * Mathf.Exp(-5.5f * t) * (1f - Mathf.Pow(t, 3f));

                float whiteNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float noiseEnv = Mathf.Exp(-14f * t) * 0.32f;

                float mixed = (impactWave * env) + (whiteNoise * noiseEnv);
                data[i] = Mathf.Clamp(mixed * 0.48f, -1f, 1f);
            }

            return CreateClipFromSamples("SFX_Procedural_PieceCapture", data);
        }

        private AudioClip BuildCheckAlertClip()
        {
            const float duration = 0.25f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[count];
            double phase = 0.0;
            const double twoPi = Math.PI * 2.0;

            const float pulse1End = 0.105f;
            const float pulse2Start = 0.120f;

            for (int i = 0; i < count; i++)
            {
                float sec = (float)i / SampleRate;
                float sample = 0f;

                if (sec < pulse1End)
                {
                    // Primer tono: 440Hz
                    phase += twoPi * 440.0 / SampleRate;
                    float wave = SampleSquare(phase) * 0.72f + SampleTriangle(phase) * 0.28f;
                    float attack = Mathf.Clamp01(sec / 0.004f);
                    float tail = Mathf.Clamp01((pulse1End - sec) / 0.012f);
                    sample = wave * attack * tail;
                }
                else if (sec >= pulse2Start)
                {
                    // Segundo tono: 880Hz
                    phase += twoPi * 880.0 / SampleRate;
                    float localSec = sec - pulse2Start;
                    float localDur = duration - pulse2Start;
                    float localT = Mathf.Clamp01(localSec / localDur);

                    float wave = SampleSquare(phase) * 0.72f + SampleTriangle(phase) * 0.28f;
                    float attack = Mathf.Clamp01(localSec / 0.004f);
                    float decay = Mathf.Exp(-4.5f * localT) * (1f - Mathf.Pow(localT, 4f));
                    sample = wave * attack * decay;
                }

                data[i] = Mathf.Clamp(sample * 0.34f, -1f, 1f);
            }

            return CreateClipFromSamples("SFX_Procedural_CheckAlert", data);
        }

        private AudioClip BuildShieldActivateClip()
        {
            // Arpegio ascendente brillante (C5 -> E5 -> G5 -> C6) para el Escudo de Tigrán Petrosián
            const float duration = 0.32f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[count];
            double phase = 0.0;
            double shimmerPhase = 0.0;
            const double twoPi = Math.PI * 2.0;

            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f };
            float[] starts = { 0.000f, 0.068f, 0.136f, 0.204f };
            float[] ends = { 0.068f, 0.136f, 0.204f, 0.320f };

            for (int i = 0; i < count; i++)
            {
                float sec = (float)i / SampleRate;
                int step = 3;
                if (sec < ends[0]) step = 0;
                else if (sec < ends[1]) step = 1;
                else if (sec < ends[2]) step = 2;

                float noteFreq = notes[step];
                float noteStart = starts[step];
                float noteDur = ends[step] - noteStart;
                float localSec = sec - noteStart;
                float localT = Mathf.Clamp01(localSec / noteDur);

                phase += twoPi * noteFreq / SampleRate;
                shimmerPhase += twoPi * (noteFreq * 2.0) / SampleRate;

                float wave = SampleTriangle(phase) * 0.56f
                           + SampleSquare(phase) * 0.32f
                           + SampleTriangle(shimmerPhase) * 0.12f;

                float attack = Mathf.Clamp01(localSec / 0.004f);
                float env = (step == 3)
                    ? Mathf.Exp(-5.0f * localT) * (1f - Mathf.Pow(localT, 3f))
                    : Mathf.Exp(-3.0f * localT) * Mathf.Clamp01((ends[step] - sec) / 0.008f);

                data[i] = Mathf.Clamp(wave * attack * env * 0.34f, -1f, 1f);
            }

            return CreateClipFromSamples("SFX_Procedural_ShieldActivate", data);
        }

        private AudioClip BuildRewardClip()
        {
            // Fanfarria triunfal arcade (D5 -> F#5 -> A5 -> D6) para recompensa de Draft
            const float duration = 0.36f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[count];
            double phase = 0.0;
            double harmonyPhase = 0.0;
            const double twoPi = Math.PI * 2.0;

            float[] notes = { 587.33f, 739.99f, 880.00f, 1174.66f };
            float[] starts = { 0.000f, 0.072f, 0.144f, 0.216f };
            float[] ends = { 0.072f, 0.144f, 0.216f, 0.360f };

            for (int i = 0; i < count; i++)
            {
                float sec = (float)i / SampleRate;
                int step = 3;
                if (sec < ends[0]) step = 0;
                else if (sec < ends[1]) step = 1;
                else if (sec < ends[2]) step = 2;

                float noteFreq = notes[step];
                float noteStart = starts[step];
                float noteDur = ends[step] - noteStart;
                float localSec = sec - noteStart;
                float localT = Mathf.Clamp01(localSec / noteDur);

                phase += twoPi * noteFreq / SampleRate;
                harmonyPhase += twoPi * (noteFreq * 1.5) / SampleRate;

                float wave = SampleSquare(phase) * 0.45f
                           + SampleTriangle(phase) * 0.40f
                           + SampleTriangle(harmonyPhase) * 0.15f;

                float attack = Mathf.Clamp01(localSec / 0.004f);
                float env = (step == 3)
                    ? Mathf.Exp(-4.2f * localT) * (1f - Mathf.Pow(localT, 3f))
                    : Mathf.Exp(-2.5f * localT) * Mathf.Clamp01((ends[step] - sec) / 0.008f);

                data[i] = Mathf.Clamp(wave * attack * env * 0.36f, -1f, 1f);
            }

            return CreateClipFromSamples("SFX_Procedural_Reward", data);
        }

        #endregion
    }
}


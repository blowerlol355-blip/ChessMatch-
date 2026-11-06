using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChessBalatro
{
    /// <summary>
    /// Administrador central de configuraciones y audio del juego.
    /// Gestiona volumen de música, efectos SFX, vibración/hápticos y su persistencia en PlayerPrefs.
    /// Funciona como Singleton persistente (DontDestroyOnLoad) entre escenas.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        private const string PrefKeyMusicVolume = "ChessMatch_MusicVolume";
        private const string PrefKeySFXVolume = "ChessMatch_SFXVolume";
        private const string PrefKeyVibration = "ChessMatch_VibrationEnabled";

        [Header("Valores por Defecto")]
        [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float defaultSFXVolume = 1.0f;
        [SerializeField] private bool defaultVibrationEnabled = true;

        [Header("Fuentes de Audio (Opcionales)")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Estado Actual (Inspector Debug)")]
        [SerializeField] private float musicVolume = 0.8f;
        [SerializeField] private float sfxVolume = 1.0f;
        [SerializeField] private bool isVibrationEnabled = true;

        public float MusicVolume => musicVolume;
        public float SFXVolume => sfxVolume;
        public bool IsVibrationEnabled => isVibrationEnabled;

        public event Action<float> OnMusicVolumeChanged;
        public event Action<float> OnSFXVolumeChanged;
        public event Action<bool> OnVibrationChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                LoadSettings();
                EnsureAudioSources();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void EnsureAudioSources()
        {
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.playOnAwake = false;
            }
            musicSource.volume = musicVolume;

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
            }
            sfxSource.volume = sfxVolume;
        }

        #region Persistencia y Carga

        public void LoadSettings()
        {
            musicVolume = PlayerPrefs.GetFloat(PrefKeyMusicVolume, defaultMusicVolume);
            sfxVolume = PlayerPrefs.GetFloat(PrefKeySFXVolume, defaultSFXVolume);
            isVibrationEnabled = PlayerPrefs.GetInt(PrefKeyVibration, defaultVibrationEnabled ? 1 : 0) == 1;

            if (musicSource != null) musicSource.volume = musicVolume;
            if (sfxSource != null) sfxSource.volume = sfxVolume;
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat(PrefKeyMusicVolume, musicVolume);
            PlayerPrefs.SetFloat(PrefKeySFXVolume, sfxVolume);
            PlayerPrefs.SetInt(PrefKeyVibration, isVibrationEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        #endregion

        #region Modificación de Valores

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            if (musicSource != null)
            {
                musicSource.volume = musicVolume;
            }
            PlayerPrefs.SetFloat(PrefKeyMusicVolume, musicVolume);
            PlayerPrefs.Save();

            OnMusicVolumeChanged?.Invoke(musicVolume);
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
            }
            PlayerPrefs.SetFloat(PrefKeySFXVolume, sfxVolume);
            PlayerPrefs.Save();

            OnSFXVolumeChanged?.Invoke(sfxVolume);
        }

        public void SetVibration(bool enabled)
        {
            isVibrationEnabled = enabled;
            PlayerPrefs.SetInt(PrefKeyVibration, isVibrationEnabled ? 1 : 0);
            PlayerPrefs.Save();

            OnVibrationChanged?.Invoke(isVibrationEnabled);

            if (isVibrationEnabled)
            {
                TriggerHapticFeedback();
            }
        }

        public void ToggleVibration()
        {
            SetVibration(!isVibrationEnabled);
        }

        #endregion

        #region Hápticos y Reproducción SFX

        /// <summary>
        /// Dispara una vibración táctil en dispositivos móviles soportados si la opción está habilitada.
        /// </summary>
        public void TriggerHapticFeedback()
        {
            if (!isVibrationEnabled) return;

#if UNITY_ANDROID || UNITY_IOS
            try
            {
                Handheld.Vibrate();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SettingsManager] No se pudo ejecutar vibración en dispositivo: {ex.Message}");
            }
#else
            Debug.Log("<color=magenta>[SettingsManager] Háptico simulado (Desktop/Editor)</color>");
#endif
        }

        /// <summary>
        /// Reproduce un efecto sonoro aplicando el volumen de SFX configurado.
        /// </summary>
        public void PlaySFX(AudioClip clip, float pitch = 1f)
        {
            if (clip == null || sfxSource == null || sfxVolume <= 0.001f) return;
            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(clip, sfxVolume);
        }

        /// <summary>
        /// Inicia o cambia la pista musical de fondo con el volumen actual.
        /// </summary>
        public void PlayMusic(AudioClip musicClip)
        {
            if (musicSource == null) return;
            if (musicClip == null)
            {
                musicSource.Stop();
                return;
            }

            if (musicSource.clip == musicClip && musicSource.isPlaying) return;

            musicSource.clip = musicClip;
            musicSource.volume = musicVolume;
            musicSource.Play();
        }

        #endregion

        #region Enlace Automático con UI (uGUI)

        /// <summary>
        /// Vincula de forma bidireccional los componentes uGUI de opciones con los valores del gestor.
        /// </summary>
        public void BindControls(Slider musicSlider, Slider sfxSlider, Toggle vibrationToggle)
        {
            if (musicSlider != null)
            {
                musicSlider.value = musicVolume;
                musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
                musicSlider.onValueChanged.AddListener(SetMusicVolume);
            }

            if (sfxSlider != null)
            {
                sfxSlider.value = sfxVolume;
                sfxSlider.onValueChanged.RemoveListener(SetSFXVolume);
                sfxSlider.onValueChanged.AddListener(SetSFXVolume);
            }

            if (vibrationToggle != null)
            {
                vibrationToggle.isOn = isVibrationEnabled;
                vibrationToggle.onValueChanged.RemoveListener(SetVibration);
                vibrationToggle.onValueChanged.AddListener(SetVibration);
            }
        }

        #endregion
    }
}


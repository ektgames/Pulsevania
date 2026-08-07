using System.Collections;
using UnityEngine;

namespace Pulsevania.Core
{
    public enum SoundEffect
    {
        SwordSwing,
        DamageTaken,
        CoinPickup
    }

    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private AudioSource sfxSource;
        private AudioSource musicSource;

        private AudioClip swingClip;
        private AudioClip damageClip;
        private AudioClip coinClip;

        private float musicVolume = 0.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            sfxSource = gameObject.AddComponent<AudioSource>();
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = false;

            GenerateProceduralSFX();
        }

        private void Start()
        {
            musicVolume = PlayerPrefs.GetFloat("Pulsevania_MusicVolume", 0.5f);
            AudioListener.volume = PlayerPrefs.GetFloat("Pulsevania_MasterVolume", 1f);
            StartCoroutine(PlayChiptuneMusic());
        }

        private void GenerateProceduralSFX()
        {
            swingClip = CreateSweepTone(800f, 200f, 0.1f, "SwordSwing");
            damageClip = CreateSweepTone(150f, 40f, 0.15f, "DamageTaken");
            coinClip = CreateDoubleTone(523.25f, 659.25f, 0.05f, 0.1f, "CoinPickup");
        }

        public void PlaySFX(SoundEffect sfx)
        {
            AudioClip clip = null;
            switch (sfx)
            {
                case SoundEffect.SwordSwing:
                    clip = swingClip;
                    break;
                case SoundEffect.DamageTaken:
                    clip = damageClip;
                    break;
                case SoundEffect.CoinPickup:
                    clip = coinClip;
                    break;
            }

            if (clip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip, 1f);
            }
        }

        private static AudioClip CreateSweepTone(float startFreq, float endFreq, float duration, string name)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, progress);
                
                float phase = 2f * Mathf.PI * currentFreq * t;
                samples[i] = Mathf.Sin(phase) * (1f - progress);
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateDoubleTone(float freq1, float freq2, float dur1, float dur2, string name)
        {
            int sampleRate = 44100;
            int sampleCount1 = (int)(sampleRate * dur1);
            int sampleCount2 = (int)(sampleRate * dur2);
            int totalSamples = sampleCount1 + sampleCount2;
            float[] samples = new float[totalSamples];

            for (int i = 0; i < sampleCount1; i++)
            {
                float t = (float)i / sampleRate;
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq1 * t) * 0.5f;
            }

            for (int i = 0; i < sampleCount2; i++)
            {
                float t = (float)i / sampleRate;
                samples[sampleCount1 + i] = Mathf.Sin(2f * Mathf.PI * freq2 * t) * (1f - (float)i / sampleCount2) * 0.5f;
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateSynthTone(float frequency, float duration, string name)
        {
            int sampleRate = 22050;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * (1f - t / duration) * 0.3f;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private IEnumerator PlayChiptuneMusic()
        {
            float[] notes = { 261.63f, 293.66f, 329.63f, 392.00f, 440.00f, 392.00f, 329.63f, 293.66f };
            int noteIndex = 0;

            while (true)
            {
                if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Gameplay)
                {
                    float freq = notes[noteIndex];
                    AudioClip noteClip = CreateSynthTone(freq, 0.15f, "Note_" + noteIndex);
                    if (musicSource != null)
                    {
                        musicSource.PlayOneShot(noteClip, 0.15f * musicVolume);
                    }
                    noteIndex = (noteIndex + 1) % notes.Length;
                }
                yield return new WaitForSeconds(0.25f);
            }
        }

        public void SetMasterVolume(float vol)
        {
            AudioListener.volume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat("Pulsevania_MasterVolume", vol);
            PlayerPrefs.Save();
        }

        public void SetMusicVolume(float vol)
        {
            musicVolume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat("Pulsevania_MusicVolume", vol);
            PlayerPrefs.Save();
        }

        public float GetMasterVolume()
        {
            return PlayerPrefs.GetFloat("Pulsevania_MasterVolume", 1f);
        }

        public float GetMusicVolume()
        {
            return PlayerPrefs.GetFloat("Pulsevania_MusicVolume", 0.5f);
        }
    }
}

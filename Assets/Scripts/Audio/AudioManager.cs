using System.Collections;
using SlotGame.Core;
using SlotGame.Game;
using SlotGame.UI;
using UnityEngine;

namespace SlotGame.Audio
{
    /// <summary>
    /// Plays all game audio by listening to SlotMachine and UI events.
    /// Nothing calls into audio for gameplay, so it can be swapped or removed without side effects.
    /// Also owns the background music, which ducks under big-win fanfares.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private SlotMachine machine;

        [Header("Sources")]
        [SerializeField] private AudioSource effectsSource;
        [Tooltip("Looping source for the reel whirr while spinning.")]
        [SerializeField] private AudioSource loopSource;
        [SerializeField] private AudioSource musicSource;

        [Header("Clips")]
        [SerializeField] private AudioClip buttonClick;
        [SerializeField] private AudioClip leverPull;
        [SerializeField] private AudioClip reelSpinLoop;
        [SerializeField] private AudioClip reelStop;
        [SerializeField] private AudioClip smallWin;
        [SerializeField] private AudioClip bigWin;
        [SerializeField] private AudioClip backgroundMusic;

        [Header("Music")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.35f;
        [Tooltip("Music volume multiplier while a big-win fanfare plays.")]
        [SerializeField, Range(0f, 1f)] private float duckedVolume = 0.25f;
        [SerializeField] private float duckDuration = 2.5f;

        private Coroutine duckRoutine;

        public bool MusicEnabled { get; private set; } = true;

        private void OnEnable()
        {
            machine.SpinStarted += HandleSpinStarted;
            machine.ReelStopped += HandleReelStopped;
            machine.SpinCompleted += HandleSpinCompleted;
            UIButtonSound.AnyButtonClicked += HandleButtonClicked;
        }

        private void OnDisable()
        {
            machine.SpinStarted -= HandleSpinStarted;
            machine.ReelStopped -= HandleReelStopped;
            machine.SpinCompleted -= HandleSpinCompleted;
            UIButtonSound.AnyButtonClicked -= HandleButtonClicked;
        }

        private void Start()
        {
            // Browsers block audio until the first click. Unity WebGL resumes it automatically.
            if (backgroundMusic != null)
            {
                musicSource.clip = backgroundMusic;
                musicSource.loop = true;
                musicSource.volume = musicVolume;
                musicSource.Play();
            }
        }

        public void SetMusicEnabled(bool isEnabled)
        {
            MusicEnabled = isEnabled;
            musicSource.mute = !isEnabled;
        }

        private void HandleButtonClicked()
        {
            Play(buttonClick, 0.6f);
        }

        private void HandleSpinStarted()
        {
            Play(leverPull);

            if (reelSpinLoop != null)
            {
                loopSource.clip = reelSpinLoop;
                loopSource.loop = true;
                loopSource.Play();
            }
        }

        private void HandleReelStopped(int reelIndex)
        {
            // Each reel lands a little louder, adding tension as the result is revealed.
            Play(reelStop, 0.7f + reelIndex * 0.15f);
        }

        private void HandleSpinCompleted(SpinReport report)
        {
            loopSource.Stop();

            if (!report.IsWin)
            {
                return;
            }

            bool isBig = report.IsJackpot || report.TotalWin >= report.Bet * machine.Config.BigWinMultiplier;
            Play(isBig ? bigWin : smallWin);

            if (isBig)
            {
                if (duckRoutine != null)
                {
                    StopCoroutine(duckRoutine);
                }

                duckRoutine = StartCoroutine(DuckMusic());
            }
        }

        /// <summary>Drops the music under the fanfare, then fades it back in.</summary>
        private IEnumerator DuckMusic()
        {
            musicSource.volume = musicVolume * duckedVolume;
            yield return new WaitForSeconds(duckDuration);

            const float fadeTime = 1f;
            for (float t = 0f; t < fadeTime; t += Time.deltaTime)
            {
                musicSource.volume = Mathf.Lerp(musicVolume * duckedVolume, musicVolume, t / fadeTime);
                yield return null;
            }

            musicSource.volume = musicVolume;
            duckRoutine = null;
        }

        private void Play(AudioClip clip, float volume = 1f)
        {
            if (clip != null)
            {
                effectsSource.PlayOneShot(clip, volume);
            }
        }
    }
}

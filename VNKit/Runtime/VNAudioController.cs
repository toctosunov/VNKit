using DG.Tweening;
using UnityEngine;

namespace VNKit
{
    /// <summary>Owns music crossfades (driven by the story's Music clips) and one-shot sfx.</summary>
    public class VNAudioController
    {
        readonly AudioSource music;
        readonly AudioSource sfx;
        Tween musicTween;

        public VNClip ActiveMusic { get; private set; }

        public VNAudioController(AudioSource musicSource, AudioSource sfxSource)
        {
            music = musicSource;
            sfx = sfxSource;
        }

        public void ApplyMusic(VNClip clip)
        {
            if (clip == ActiveMusic) return;
            var old = ActiveMusic;
            ActiveMusic = clip;
            if (music == null) return;

            if (musicTween != null && musicTween.IsActive()) musicTween.Kill();
            var seq = DOTween.Sequence();
            if (music.isPlaying)
                seq.Append(music.DOFade(0f, old != null ? old.fadeOut : 0.5f));

            seq.AppendCallback(() =>
            {
                if (clip == null || clip.audio == null)
                {
                    music.Stop();
                    return;
                }
                music.clip = clip.audio;
                music.loop = clip.loop;
                music.volume = 0f;
                music.Play();
            });

            if (clip != null && clip.audio != null)
                seq.Append(music.DOFade(clip.volume, clip.fadeIn));

            musicTween = seq;
        }

        public void PlaySfx(AudioClip clip, float volume)
        {
            if (clip != null && sfx != null) sfx.PlayOneShot(clip, volume);
        }

        /// <summary>Stops music immediately with no crossfade. Used when jumping to a new story/state (Play, ApplyState).</summary>
        public void Reset()
        {
            ActiveMusic = null;
            if (musicTween != null && musicTween.IsActive()) musicTween.Kill();
            if (music != null) music.Stop();
        }
    }
}

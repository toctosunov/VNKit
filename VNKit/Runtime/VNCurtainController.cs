using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>
    /// A full-screen overlay used for the Start frame's entrance and the End frame's exit. Optional:
    /// every method no-ops (but still calls onDone) if no Image was wired up, so a player without one
    /// still advances past Start/End frames, just without the fade.
    /// </summary>
    public class VNCurtainController
    {
        readonly Image curtain;

        public VNCurtainController(Image curtain)
        {
            this.curtain = curtain;
            if (curtain == null) return;
            curtain.raycastTarget = false;
            SetAlpha(0f);
            curtain.enabled = false;
        }

        /// <summary>Starts fully opaque and fades to transparent - how a Start frame reveals the story.</summary>
        public void FadeIn(VNFade fade, Action onDone)
        {
            if (curtain == null) { onDone?.Invoke(); return; }
            curtain.DOKill();
            curtain.enabled = true;
            SetAlpha(1f);
            curtain.DOFade(0f, Mathf.Max(0.01f, fade.duration))
                .SetDelay(fade.delay)
                .SetEase(fade.ease)
                .OnComplete(() =>
                {
                    curtain.enabled = false;
                    onDone?.Invoke();
                });
        }

        /// <summary>Starts transparent and fades to opaque - how an End frame hides the story before ending it.</summary>
        public void FadeOut(VNFade fade, Action onDone)
        {
            if (curtain == null) { onDone?.Invoke(); return; }
            curtain.DOKill();
            curtain.enabled = true;
            SetAlpha(0f);
            curtain.DOFade(1f, Mathf.Max(0.01f, fade.duration))
                .SetDelay(fade.delay)
                .SetEase(fade.ease)
                .OnComplete(() => onDone?.Invoke());
        }

        void SetAlpha(float a)
        {
            var c = curtain.color;
            c.a = a;
            curtain.color = c;
        }
    }
}

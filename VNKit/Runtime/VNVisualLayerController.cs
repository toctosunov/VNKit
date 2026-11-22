using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>
    /// Owns the background, the up-to-three character slots and the single-image layer, and their
    /// enter animations. Character slots are separate, persistently-anchored Images (one per VNSlot)
    /// rather than one Image that gets repositioned, so several characters can be on screen at once.
    /// </summary>
    public class VNVisualLayerController
    {
        class Layer
        {
            public Image image;
            public Sprite shown;
        }

        readonly Layer bg;
        readonly Layer single;
        readonly Dictionary<VNSlot, Layer> characterSlots = new Dictionary<VNSlot, Layer>();
        static readonly HashSet<VNSlot> ScratchUsed = new HashSet<VNSlot>();

        public VNVisualLayerController(Image background, Image singleImage, Image leftSlot, Image centerSlot, Image rightSlot)
        {
            bg = MakeLayer(background);
            single = MakeLayer(singleImage);
            if (leftSlot != null) characterSlots[VNSlot.Left] = MakeLayer(leftSlot);
            if (centerSlot != null) characterSlots[VNSlot.Center] = MakeLayer(centerSlot);
            if (rightSlot != null) characterSlots[VNSlot.Right] = MakeLayer(rightSlot);

            if (singleImage != null) singleImage.preserveAspect = true;
            foreach (var l in characterSlots.Values)
                if (l.image != null) l.image.preserveAspect = true;
        }

        static Layer MakeLayer(Image img)
        {
            var l = new Layer { image = img };
            if (img != null)
            {
                SetAlpha(img, 0f);
                img.enabled = false;
            }
            return l;
        }

        /// <summary>Shows the background and every character cue in its slot; any slot not mentioned hides.</summary>
        public void ApplyScene(Sprite background, VNAnim backgroundAnim, List<VNCharacterCue> cues)
        {
            ShowLayer(bg, background, backgroundAnim);
            HideLayer(single);

            ScratchUsed.Clear();
            if (cues != null)
            {
                for (int i = 0; i < cues.Count; i++)
                {
                    var cue = cues[i];
                    if (cue == null || cue.sprite == null) continue;
                    if (!characterSlots.TryGetValue(cue.slot, out var layer)) continue;
                    ScratchUsed.Add(cue.slot);
                    ShowLayer(layer, cue.sprite, cue.anim);
                }
            }
            foreach (var kv in characterSlots)
                if (!ScratchUsed.Contains(kv.Key)) HideLayer(kv.Value);
        }

        public void ApplySingleImage(Sprite image, VNAnim anim)
        {
            HideLayer(bg);
            foreach (var l in characterSlots.Values) HideLayer(l);
            ShowLayer(single, image, anim);
        }

        void ShowLayer(Layer l, Sprite sprite, VNAnim anim)
        {
            if (l == null || l.image == null) return;
            if (sprite == null) { HideLayer(l); return; }

            bool changed = l.shown != sprite;
            bool emphasis = anim != null && (anim.type == VNAnimType.Punch || anim.type == VNAnimType.Shake);
            if (!changed && !emphasis) return;

            l.shown = sprite;
            l.image.sprite = sprite;
            l.image.enabled = true;
            Animate(l.image, anim ?? new VNAnim());
        }

        void HideLayer(Layer l)
        {
            if (l == null || l.image == null || l.shown == null) return;
            l.shown = null;
            l.image.DOKill();
            l.image.rectTransform.DOKill();
            l.image.DOFade(0f, 0.25f).OnComplete(() =>
            {
                if (l.shown == null) l.image.enabled = false;
            });
        }

        static void SetAlpha(Graphic g, float a)
        {
            var c = g.color;
            c.a = a;
            g.color = c;
        }

        void Animate(Image img, VNAnim a)
        {
            var rt = img.rectTransform;
            img.DOKill();
            rt.DOKill();
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            SetAlpha(img, 1f);

            float d = Mathf.Max(0.01f, a.duration);
            float w = 1920f, h = 1080f;
            if (img.canvas != null)
            {
                var crt = img.canvas.rootCanvas.transform as RectTransform;
                if (crt != null) { w = crt.rect.width; h = crt.rect.height; }
            }

            switch (a.type)
            {
                case VNAnimType.Fade:
                    SetAlpha(img, 0f);
                    img.DOFade(1f, d).SetDelay(a.delay).SetEase(a.ease);
                    break;
                case VNAnimType.SlideFromLeft:
                    Slide(img, rt, new Vector2(-w * 0.35f, 0f), a, d);
                    break;
                case VNAnimType.SlideFromRight:
                    Slide(img, rt, new Vector2(w * 0.35f, 0f), a, d);
                    break;
                case VNAnimType.SlideFromBottom:
                    Slide(img, rt, new Vector2(0f, -h * 0.35f), a, d);
                    break;
                case VNAnimType.SlideFromTop:
                    Slide(img, rt, new Vector2(0f, h * 0.35f), a, d);
                    break;
                case VNAnimType.Scale:
                    rt.localScale = Vector3.one * 0.6f;
                    SetAlpha(img, 0f);
                    rt.DOScale(1f, d).SetDelay(a.delay).SetEase(a.ease);
                    img.DOFade(1f, d).SetDelay(a.delay);
                    break;
                case VNAnimType.Punch:
                    rt.DOPunchScale(Vector3.one * 0.12f, d, 6, 0.6f).SetDelay(a.delay);
                    break;
                case VNAnimType.Shake:
                    rt.DOShakeAnchorPos(d, 30f, 25, 90f).SetDelay(a.delay);
                    break;
            }
        }

        void Slide(Image img, RectTransform rt, Vector2 from, VNAnim a, float d)
        {
            rt.anchoredPosition = from;
            SetAlpha(img, 0f);
            rt.DOAnchorPos(Vector2.zero, d).SetDelay(a.delay).SetEase(a.ease);
            img.DOFade(1f, d).SetDelay(a.delay);
        }
    }
}

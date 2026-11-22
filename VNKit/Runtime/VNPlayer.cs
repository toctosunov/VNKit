using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>Plays a VNStory. Create the UI with Tools > Visual Novel > Create Player In Scene.</summary>
    public class VNPlayer : MonoBehaviour
    {
        [Header("Story")]
        public VNStory story;
        public bool playOnStart = true;

        [Header("Scene layers")]
        public Image backgroundImage;
        public Image characterImage;
        public Image singleImage;

        [Header("Text")]
        public CanvasGroup textPanel;
        public TMP_Text speakerLabel;
        public TMP_Text bodyLabel;

        [Header("Choices")]
        public RectTransform choicesContainer;
        public Button choiceButtonTemplate;

        [Header("Input")]
        public Button advanceButton;

        [Header("Audio")]
        public AudioSource musicSource;
        public AudioSource sfxSource;

        [Tooltip("On: after the question text is read, a click replaces it with the answers. Off: answers replace the text right away.")]
        public bool choicesAfterClick = true;

        public UnityEvent onStoryEnd;

        class Layer
        {
            public Image image;
            public Sprite shown;
            public VNSlot slot;
        }

        Layer bgLayer, charLayer, singleLayer;
        List<VNNodeData> order = new List<VNNodeData>();
        VNNodeData current;
        Tween typeTween;
        Tween musicTween;
        bool typing;
        VNClip activeMusic;
        VNClip activeBg;
        VNTextStyle activeStyle;
        bool waitingForChoices;
        TMP_FontAsset sceneBodyFont, sceneSpeakerFont;

        void Awake()
        {
            if (bodyLabel != null) sceneBodyFont = bodyLabel.font;
            if (speakerLabel != null) sceneSpeakerFont = speakerLabel.font;
            bgLayer = MakeLayer(backgroundImage);
            charLayer = MakeLayer(characterImage);
            singleLayer = MakeLayer(singleImage);
            if (characterImage) characterImage.preserveAspect = true;
            if (singleImage) singleImage.preserveAspect = true;
            if (advanceButton) advanceButton.onClick.AddListener(Advance);
            if (choiceButtonTemplate) choiceButtonTemplate.gameObject.SetActive(false);
            if (textPanel) textPanel.alpha = 0f;

            SetupChoicesContainer();
            EnsureLayerOrder();
            if (characterImage == null)
                Debug.LogWarning("VNPlayer: 'Character Image' is not assigned, so characters will not be shown.", this);
        }

        /// <summary>Answers live inside the text panel, exactly where the body text is.</summary>
        void SetupChoicesContainer()
        {
            if (choicesContainer == null) return;

            if (textPanel != null && !choicesContainer.IsChildOf(textPanel.transform))
            {
                choicesContainer.SetParent(textPanel.transform, false);
                if (bodyLabel != null)
                {
                    var b = bodyLabel.rectTransform;
                    choicesContainer.anchorMin = b.anchorMin;
                    choicesContainer.anchorMax = b.anchorMax;
                    choicesContainer.offsetMin = b.offsetMin;
                    choicesContainer.offsetMax = b.offsetMax;
                }
            }

            var vlg = choicesContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = choicesContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }

        /// <summary>The character must be drawn above the background whatever the hierarchy order is.</summary>
        void EnsureLayerOrder()
        {
            if (backgroundImage == null || characterImage == null) return;
            var bgT = backgroundImage.transform;
            var chT = characterImage.transform;
            if (bgT.parent == chT.parent && chT.GetSiblingIndex() < bgT.GetSiblingIndex())
                bgT.SetSiblingIndex(chT.GetSiblingIndex());
        }

        void Start()
        {
            if (playOnStart && story != null) Play(story);
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

        // ---------------------------------------------------------------- flow

        public void Play(VNStory s)
        {
            story = s;
            order = story.GetOrderedNodes();
            activeMusic = null;
            activeBg = null;
            var start = story.GetNode(story.startNodeId);
            if (start == null && story.nodes.Count > 0) start = story.nodes[0];
            if (start == null) { End(); return; }
            Show(start);
        }

        public void Restart()
        {
            if (story != null) Play(story);
        }

        public void Advance()
        {
            if (current == null) return;
            if (typing)
            {
                if (typeTween != null && typeTween.IsActive()) typeTween.Complete();
                return;
            }
            if (waitingForChoices)
            {
                waitingForChoices = false;
                ShowChoices(current);
                return;
            }
            if (current.choices.Count > 0) return;   // must pick a choice
            GoTo(current.nextId);
        }

        void GoTo(string id)
        {
            var n = story != null ? story.GetNode(id) : null;
            if (n == null) End();
            else Show(n);
        }

        void End()
        {
            current = null;
            ClearChoices();
            onStoryEnd?.Invoke();
        }

        void Show(VNNodeData n)
        {
            current = n;
            ClearChoices();

            int idx = order.IndexOf(n);
            activeBg = FindClip(VNTrack.Background, idx);
            ApplyMusic(FindClip(VNTrack.Music, idx));
            ApplyVisuals(n);

            if (n.sfx != null && sfxSource != null) sfxSource.PlayOneShot(n.sfx, n.sfxVolume);
            StartText(n);
        }

        // ---------------------------------------------------------------- timeline clips

        int IndexOfId(string id)
        {
            for (int i = 0; i < order.Count; i++)
                if (order[i].id == id) return i;
            return -1;
        }

        VNClip FindClip(VNTrack track, int nodeIndex)
        {
            VNClip best = null;
            int bestStart = -1;
            foreach (var c in story.clips)
            {
                if (c.track != track) continue;
                int s = IndexOfId(c.startNodeId);
                int e = IndexOfId(c.endNodeId);
                if (s < 0 || e < 0) continue;
                if (s > e) { int t = s; s = e; e = t; }
                if (nodeIndex >= s && nodeIndex <= e && s >= bestStart)
                {
                    best = c;
                    bestStart = s;
                }
            }
            return best;
        }

        void ApplyMusic(VNClip clip)
        {
            if (clip == activeMusic) return;
            var old = activeMusic;
            activeMusic = clip;
            if (musicSource == null) return;

            if (musicTween != null && musicTween.IsActive()) musicTween.Kill();
            var seq = DOTween.Sequence();
            if (musicSource.isPlaying)
                seq.Append(musicSource.DOFade(0f, old != null ? old.fadeOut : 0.5f));

            seq.AppendCallback(() =>
            {
                if (clip == null || clip.audio == null)
                {
                    musicSource.Stop();
                    return;
                }
                musicSource.clip = clip.audio;
                musicSource.loop = clip.loop;
                musicSource.volume = 0f;
                musicSource.Play();
            });

            if (clip != null && clip.audio != null)
                seq.Append(musicSource.DOFade(clip.volume, clip.fadeIn));

            musicTween = seq;
        }

        // ---------------------------------------------------------------- visuals

        void ApplyVisuals(VNNodeData n)
        {
            if (n.layout == VNLayout.Scene)
            {
                Sprite bg = null;
                VNAnim bgAnim = n.backgroundAnim;
                if (n.background != null) bg = n.background;
                else if (activeBg != null && activeBg.sprite != null)
                {
                    bg = activeBg.sprite;
                    bgAnim = new VNAnim { type = VNAnimType.Fade, duration = activeBg.fadeIn };
                }
                ShowLayer(bgLayer, bg, bgAnim);
                ShowLayer(charLayer, n.character, n.characterAnim, n.slot, true);
                HideLayer(singleLayer);
            }
            else
            {
                HideLayer(bgLayer);
                HideLayer(charLayer);
                ShowLayer(singleLayer, n.image, n.imageAnim);
            }
        }

        void ShowLayer(Layer l, Sprite sprite, VNAnim anim, VNSlot slot = VNSlot.Center, bool isCharacter = false)
        {
            if (l.image == null) return;
            if (sprite == null) { HideLayer(l); return; }

            bool changed = l.shown != sprite || (isCharacter && l.slot != slot);
            bool emphasis = anim.type == VNAnimType.Punch || anim.type == VNAnimType.Shake;
            if (!changed && !emphasis) return;

            l.shown = sprite;
            l.slot = slot;
            l.image.sprite = sprite;
            l.image.enabled = true;
            if (isCharacter) PlaceCharacter(l.image, slot);
            Animate(l.image, anim);
        }

        void HideLayer(Layer l)
        {
            if (l.image == null || l.shown == null) return;
            l.shown = null;
            l.image.DOKill();
            l.image.rectTransform.DOKill();
            l.image.DOFade(0f, 0.25f).OnComplete(() =>
            {
                if (l.shown == null) l.image.enabled = false;
            });
        }

        static void PlaceCharacter(Image img, VNSlot slot)
        {
            float x = slot == VNSlot.Left ? 0.22f : slot == VNSlot.Right ? 0.78f : 0.5f;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(x - 0.25f, 0f);
            rt.anchorMax = new Vector2(x + 0.25f, 0.95f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
                default:
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

        // ---------------------------------------------------------------- text

        void ApplyTextStyle(VNNodeData n)
        {
            var s = n.overrideTextStyle ? n.textStyle : (story != null ? story.defaultTextStyle : null);
            if (s == null) return;
            activeStyle = s;

            if (bodyLabel != null)
            {
                bodyLabel.font = s.font != null ? s.font : sceneBodyFont;
                bodyLabel.fontSize = s.fontSize;
                bodyLabel.fontStyle = s.fontStyle;
                bodyLabel.color = s.textColor;
                bodyLabel.outlineWidth = s.outlineWidth;
                bodyLabel.outlineColor = s.outlineColor;
            }

            if (speakerLabel != null)
            {
                speakerLabel.font = s.font != null ? s.font : sceneSpeakerFont;
                speakerLabel.fontSize = s.speakerSize;
                speakerLabel.color = s.speakerColor;
            }

            if (textPanel != null)
            {
                var img = textPanel.GetComponent<Image>();
                if (img != null)
                {
                    img.color = s.panelColor;
                    img.sprite = s.panelSprite;
                    img.type = s.panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;

                    var border = textPanel.GetComponent<Outline>();
                    if (border == null) border = textPanel.gameObject.AddComponent<Outline>();
                    border.effectColor = s.borderColor;
                    border.effectDistance = new Vector2(s.borderWidth, -s.borderWidth);
                    border.enabled = s.borderWidth > 0f;
                }
            }
        }

        void StartText(VNNodeData n)
        {
            ApplyTextStyle(n);
            waitingForChoices = false;
            if (bodyLabel != null) bodyLabel.gameObject.SetActive(true);
            bool hasText = !string.IsNullOrWhiteSpace(n.text);
            bool hasSpeaker = !string.IsNullOrWhiteSpace(n.speaker);

            if (speakerLabel != null)
            {
                speakerLabel.text = n.speaker;
                speakerLabel.gameObject.SetActive(hasSpeaker);
            }
            if (textPanel != null)
            {
                textPanel.DOKill();
                textPanel.DOFade(hasText || hasSpeaker || n.choices.Count > 0 ? 1f : 0f, 0.2f);
            }

            if (typeTween != null && typeTween.IsActive()) typeTween.Kill();
            typing = false;

            if (bodyLabel == null)
            {
                OnTextDone();
                return;
            }

            bodyLabel.text = n.text ?? "";
            bodyLabel.ForceMeshUpdate();
            int total = bodyLabel.textInfo.characterCount;

            if (n.typeSpeed <= 0f || total == 0)
            {
                bodyLabel.maxVisibleCharacters = 99999;
                OnTextDone();
                return;
            }

            bodyLabel.maxVisibleCharacters = 0;
            typing = true;
            int counter = 0;
            typeTween = DOTween.To(() => counter, v =>
                {
                    counter = v;
                    bodyLabel.maxVisibleCharacters = v;
                }, total, total / n.typeSpeed)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    typing = false;
                    bodyLabel.maxVisibleCharacters = 99999;
                    OnTextDone();
                });
        }

        void OnTextDone()
        {
            if (current == null || current.choices.Count == 0) return;
            if (choicesAfterClick && !string.IsNullOrWhiteSpace(current.text)) waitingForChoices = true;
            else ShowChoices(current);
        }

        // ---------------------------------------------------------------- choices

        void ShowChoices(VNNodeData n)
        {
            if (choicesContainer == null || choiceButtonTemplate == null) return;
            var s = activeStyle;

            // the answers replace the body text inside the same panel
            if (bodyLabel != null) bodyLabel.gameObject.SetActive(false);

            for (int i = 0; i < n.choices.Count; i++)
            {
                var choice = n.choices[i];
                var btn = Instantiate(choiceButtonTemplate, choicesContainer);
                btn.gameObject.SetActive(true);
                StyleChoiceButton(btn, s);

                var label = btn.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = choice.text;
                    label.margin = new Vector4(16f, 2f, 16f, 2f);
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 18f;
                    label.fontSizeMax = Mathf.Max(18f, s != null ? s.fontSize : 34f);
                    if (s != null)
                    {
                        label.font = s.font != null ? s.font : sceneBodyFont;
                        label.fontStyle = s.fontStyle;
                        label.color = s.textColor;
                        label.outlineWidth = s.outlineWidth;
                        label.outlineColor = s.outlineColor;
                    }
                }

                string target = choice.targetId;
                btn.onClick.AddListener(() => Choose(target));

                // own group so the buttons stay clickable although the panel itself blocks nothing
                var cg = btn.GetComponent<CanvasGroup>();
                if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
                cg.ignoreParentGroups = true;
                cg.interactable = true;
                cg.blocksRaycasts = true;
                cg.alpha = 0f;
                cg.DOFade(1f, 0.25f).SetDelay(i * 0.08f);
                btn.transform.localScale = Vector3.one * 0.95f;
                btn.transform.DOScale(1f, 0.25f).SetDelay(i * 0.08f).SetEase(Ease.OutBack);
            }
        }

        void StyleChoiceButton(Button btn, VNTextStyle s)
        {
            var le = btn.GetComponent<LayoutElement>();
            if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 30f;
            le.preferredHeight = 60f;
            le.flexibleHeight = 0f;

            if (s == null) return;

            if (btn.targetGraphic != null) btn.targetGraphic.color = Color.white;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            cb.normalColor = s.choiceColor;
            cb.disabledColor = s.choiceColor;
            cb.highlightedColor = s.choiceHoverColor;
            cb.selectedColor = s.choiceHoverColor;
            cb.pressedColor = Color.Lerp(s.choiceHoverColor, Color.white, 0.25f);
            btn.colors = cb;

            var border = btn.GetComponent<Outline>();
            if (border == null) border = btn.gameObject.AddComponent<Outline>();
            border.effectColor = s.borderColor;
            border.effectDistance = new Vector2(s.borderWidth, -s.borderWidth);
            border.enabled = s.borderWidth > 0f;
        }

        void Choose(string targetId)
        {
            ClearChoices();
            GoTo(targetId);
        }

        void ClearChoices()
        {
            waitingForChoices = false;
            if (bodyLabel != null && !bodyLabel.gameObject.activeSelf) bodyLabel.gameObject.SetActive(true);
            if (choicesContainer == null) return;
            for (int i = choicesContainer.childCount - 1; i >= 0; i--)
            {
                var child = choicesContainer.GetChild(i);
                if (choiceButtonTemplate != null && child.gameObject == choiceButtonTemplate.gameObject) continue;
                Destroy(child.gameObject);
            }
        }
    }
}

using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>Owns the text panel: speaker/body labels, the per-frame style and the typewriter effect.</summary>
    public class VNTextPresenter
    {
        readonly CanvasGroup textPanel;
        readonly TMP_Text speakerLabel;
        readonly TMP_Text bodyLabel;
        readonly TMP_FontAsset sceneBodyFont;
        readonly TMP_FontAsset sceneSpeakerFont;

        Tween typeTween;

        public bool Typing { get; private set; }
        public VNTextStyle ActiveStyle { get; private set; }
        public TMP_FontAsset FallbackBodyFont => sceneBodyFont;

        public VNTextPresenter(CanvasGroup textPanel, TMP_Text speakerLabel, TMP_Text bodyLabel)
        {
            this.textPanel = textPanel;
            this.speakerLabel = speakerLabel;
            this.bodyLabel = bodyLabel;
            if (bodyLabel != null) sceneBodyFont = bodyLabel.font;
            if (speakerLabel != null) sceneSpeakerFont = speakerLabel.font;
            if (textPanel != null) textPanel.alpha = 0f;
        }

        public void ApplyStyle(VNStory story, VNNodeData n)
        {
            var s = n.overrideTextStyle ? n.textStyle : story != null ? story.defaultTextStyle : null;
            if (s == null) return;
            ActiveStyle = s;

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

        /// <summary>Starts a frame's text (and its typewriter, if any). Calls onDone once the full text is visible.</summary>
        public void Show(VNStory story, VNNodeData n, Action onDone)
        {
            ApplyStyle(story, n);
            if (bodyLabel != null) bodyLabel.gameObject.SetActive(true);
            bool hasText = !string.IsNullOrWhiteSpace(n.text);
            bool hasSpeaker = !string.IsNullOrWhiteSpace(n.speaker);
            bool hasChoices = n.choices.Count > 0;

            if (speakerLabel != null)
            {
                speakerLabel.text = n.speaker;
                speakerLabel.gameObject.SetActive(hasSpeaker);
            }
            if (textPanel != null)
            {
                textPanel.DOKill();
                textPanel.DOFade(hasText || hasSpeaker || hasChoices ? 1f : 0f, 0.2f);
            }

            if (typeTween != null && typeTween.IsActive()) typeTween.Kill();
            Typing = false;

            if (bodyLabel == null)
            {
                onDone?.Invoke();
                return;
            }

            bodyLabel.text = n.text ?? "";
            bodyLabel.ForceMeshUpdate();
            int total = bodyLabel.textInfo.characterCount;

            if (n.typeSpeed <= 0f || total == 0)
            {
                bodyLabel.maxVisibleCharacters = 99999;
                onDone?.Invoke();
                return;
            }

            bodyLabel.maxVisibleCharacters = 0;
            Typing = true;
            int counter = 0;
            typeTween = DOTween.To(() => counter, v =>
                {
                    counter = v;
                    bodyLabel.maxVisibleCharacters = v;
                }, total, total / n.typeSpeed)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    Typing = false;
                    bodyLabel.maxVisibleCharacters = 99999;
                    onDone?.Invoke();
                });
        }

        /// <summary>Jumps straight to the fully-typed text, if a typewriter is running.</summary>
        public void SkipTyping()
        {
            if (Typing && typeTween != null && typeTween.IsActive()) typeTween.Complete();
        }

        public void SetBodyVisible(bool visible)
        {
            if (bodyLabel != null) bodyLabel.gameObject.SetActive(visible);
        }

        public RectTransform BodyRect => bodyLabel != null ? bodyLabel.rectTransform : null;
        public Transform PanelTransform => textPanel != null ? textPanel.transform : null;
    }
}

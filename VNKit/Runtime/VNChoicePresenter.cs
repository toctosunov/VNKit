using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>
    /// Owns the choice buttons. Instances are pooled (shown/hidden, not destroyed/instantiated every
    /// branch) to avoid GC churn on stories with frequent choices.
    /// </summary>
    public class VNChoicePresenter
    {
        readonly RectTransform container;
        readonly Button template;
        readonly List<Button> pool = new List<Button>();

        public VNChoicePresenter(RectTransform container, Button template)
        {
            this.container = container;
            this.template = template;
            if (template != null) template.gameObject.SetActive(false);
            SetupContainer();
        }

        void SetupContainer()
        {
            if (container == null) return;
            var vlg = container.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = container.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }

        /// <summary>Answers live inside the text panel, exactly where the body text is.</summary>
        public void AlignToBody(Transform parent, RectTransform body)
        {
            if (container == null || parent == null) return;
            if (container.IsChildOf(parent)) return;
            container.SetParent(parent, false);
            if (body != null)
            {
                container.anchorMin = body.anchorMin;
                container.anchorMax = body.anchorMax;
                container.offsetMin = body.offsetMin;
                container.offsetMax = body.offsetMax;
            }
        }

        public void Show(List<VNChoice> visible, VNTextStyle style, TMP_FontAsset fallbackFont, Action<VNChoice> onChosen)
        {
            Clear();
            if (container == null || template == null || visible == null) return;

            for (int i = 0; i < visible.Count; i++)
            {
                var choice = visible[i];
                var btn = GetFromPool(i);
                btn.gameObject.SetActive(true);
                StyleButton(btn, style);

                var label = btn.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = choice.text;
                    label.margin = new Vector4(16f, 2f, 16f, 2f);
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 18f;
                    label.fontSizeMax = Mathf.Max(18f, style != null ? style.fontSize : 34f);
                    if (style != null)
                    {
                        label.font = style.font != null ? style.font : fallbackFont;
                        label.fontStyle = style.fontStyle;
                        label.color = style.textColor;
                        label.outlineWidth = style.outlineWidth;
                        label.outlineColor = style.outlineColor;
                    }
                }

                var cg = btn.GetComponent<CanvasGroup>();
                if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
                cg.ignoreParentGroups = true;
                cg.interactable = true;
                cg.blocksRaycasts = true;
                cg.alpha = 0f;
                cg.DOFade(1f, 0.25f).SetDelay(i * 0.08f);
                btn.transform.localScale = Vector3.one * 0.95f;
                btn.transform.DOScale(1f, 0.25f).SetDelay(i * 0.08f).SetEase(Ease.OutBack);

                btn.onClick.RemoveAllListeners();
                var captured = choice;
                btn.onClick.AddListener(() => onChosen?.Invoke(captured));
            }
        }

        Button GetFromPool(int index)
        {
            while (pool.Count <= index)
            {
                var btn = UnityEngine.Object.Instantiate(template, container);
                pool.Add(btn);
            }
            return pool[index];
        }

        void StyleButton(Button btn, VNTextStyle s)
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

        /// <summary>Hides every pooled button. Cheap: no Destroy, so it's safe to call on every frame change.</summary>
        public void Clear()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].onClick.RemoveAllListeners();
                pool[i].transform.DOKill();
                var cg = pool[i].GetComponent<CanvasGroup>();
                if (cg != null) cg.DOKill();
                pool[i].gameObject.SetActive(false);
            }
        }
    }
}

using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VNKit;

namespace VNKitEditor
{
    /// <summary>Tools > Visual Novel > Create Player In Scene: builds the whole UI and wires up VNPlayer.</summary>
    public static class VNPlayerSetup
    {
        [MenuItem("Tools/Visual Novel/Create Player In Scene")]
        public static void Create()
        {
            var root = new GameObject("VN Player", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(root, "Create VN Player");

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var player = root.AddComponent<VNPlayer>();

            // layers (back to front)
            var bg = NewImage("Background", root.transform, Color.white, false);
            Stretch(bg.rectTransform);

            // three fixed character slots so several characters can be on screen at once
            var charLeft = NewCharacterSlot("Character Left", root.transform, 0.22f);
            var charCenter = NewCharacterSlot("Character Center", root.transform, 0.5f);
            var charRight = NewCharacterSlot("Character Right", root.transform, 0.78f);

            var single = NewImage("SingleImage", root.transform, Color.white, false);
            single.preserveAspect = true;
            Stretch(single.rectTransform);

            // click catcher
            var advance = NewImage("AdvanceButton", root.transform, new Color(0f, 0f, 0f, 0f), true);
            Stretch(advance.rectTransform);
            var advanceBtn = advance.gameObject.AddComponent<Button>();
            advanceBtn.transition = Selectable.Transition.None;

            // text panel
            var panelImg = NewImage("TextPanel", root.transform, new Color(0f, 0f, 0f, 0.65f), false);
            Anchor(panelImg.rectTransform, 0.05f, 0.03f, 0.95f, 0.28f);
            var panelGroup = panelImg.gameObject.AddComponent<CanvasGroup>();
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable = false;

            var speaker = NewText("Speaker", panelImg.transform, 40f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            speaker.color = new Color(1f, 0.85f, 0.4f);
            Anchor(speaker.rectTransform, 0.03f, 0.72f, 0.97f, 0.97f);

            var body = NewText("Body", panelImg.transform, 36f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Anchor(body.rectTransform, 0.03f, 0.05f, 0.97f, 0.72f);

            // choices: live inside the text panel, in place of the body text
            var choicesRt = NewRect("Choices", panelImg.transform);
            Anchor(choicesRt, 0.03f, 0.05f, 0.97f, 0.72f);
            var layout = choicesRt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var template = NewImage("ChoiceButtonTemplate", choicesRt, new Color(0.15f, 0.2f, 0.35f, 0.95f), true);
            var templateBtn = template.gameObject.AddComponent<Button>();
            var le = template.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 30f;
            le.preferredHeight = 60f;
            var choiceLabel = NewText("Label", template.transform, 30f, FontStyles.Normal, TextAlignmentOptions.Center);
            Stretch(choiceLabel.rectTransform);
            template.gameObject.SetActive(false);

            // audio
            var music = root.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            var sfx = root.AddComponent<AudioSource>();
            sfx.playOnAwake = false;

            // wire
            player.backgroundImage = bg;
            player.leftCharacterImage = charLeft;
            player.centerCharacterImage = charCenter;
            player.rightCharacterImage = charRight;
            player.singleImage = single;
            player.textPanel = panelGroup;
            player.speakerLabel = speaker;
            player.bodyLabel = body;
            player.choicesContainer = choicesRt;
            player.choiceButtonTemplate = templateBtn;
            player.advanceButton = advanceBtn;
            player.musicSource = music;
            player.sfxSource = sfx;

            EnsureEventSystem();

            Selection.activeGameObject = root;
            EditorUtility.SetDirty(player);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>A character slot anchored at a fixed horizontal position (0..1 of the canvas width).</summary>
        static Image NewCharacterSlot(string name, Transform parent, float centerX)
        {
            var img = NewImage(name, parent, Color.white, false);
            img.preserveAspect = true;
            Anchor(img.rectTransform, centerX - 0.25f, 0f, centerX + 0.25f, 0.95f);
            return img;
        }

        static Image NewImage(string name, Transform parent, Color color, bool raycast)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        static TextMeshProUGUI NewText(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions align)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Anchor(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}

using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace VNKit
{
    public enum VNLayout { Scene, SingleImage }
    public enum VNSlot { Left, Center, Right }
    public enum VNAnimType { None, Fade, SlideFromLeft, SlideFromRight, SlideFromBottom, SlideFromTop, Scale, Punch, Shake }
    public enum VNTrack { Background = 0, Music = 1 }

    [Serializable]
    public class VNAnim
    {
        public VNAnimType type = VNAnimType.Fade;
        [Min(0f)] public float duration = 0.5f;
        [Min(0f)] public float delay = 0f;
        public Ease ease = Ease.OutQuad;
    }

    [Serializable]
    public class VNChoice
    {
        public string text = "Choice";
        [HideInInspector] public string targetId;
    }

    /// <summary>Look of the text box: font, colors, outlines, panel. One default per story, optional override per frame.</summary>
    [Serializable]
    public class VNTextStyle
    {
        [Header("Body text")]
        public TMP_FontAsset font;                       // empty = font from the scene
        [Min(1f)] public float fontSize = 36f;
        public FontStyles fontStyle = FontStyles.Normal;
        public Color textColor = Color.white;
        [Range(0f, 1f)] public float outlineWidth = 0f;  // text outline (needs a TMP SDF font)
        public Color outlineColor = Color.black;

        [Header("Speaker name")]
        [Min(1f)] public float speakerSize = 40f;
        public Color speakerColor = new Color(1f, 0.85f, 0.4f);

        [Header("Panel")]
        public Color panelColor = new Color(0f, 0f, 0f, 0.65f);
        public Sprite panelSprite;                       // optional, 9-sliced frame/background
        [Min(0f)] public float borderWidth = 0f;         // panel outline thickness in px
        public Color borderColor = Color.white;

        [Header("Choice buttons (shown inside the panel)")]
        public Color choiceColor = new Color(1f, 1f, 1f, 0.12f);
        public Color choiceHoverColor = new Color(1f, 0.85f, 0.4f, 0.35f);
    }

    /// <summary>One frame (node) of the story.</summary>
    [Serializable]
    public class VNNodeData
    {
        [HideInInspector] public string id;
        [HideInInspector] public Vector2 position;
        [HideInInspector] public string nextId;

        public string title = "Frame";
        public VNLayout layout = VNLayout.Scene;

        // Scene layout
        public Sprite background;            // optional override; otherwise taken from the Background track
        public VNAnim backgroundAnim = new VNAnim();
        public Sprite character;
        public VNSlot slot = VNSlot.Center;
        public VNAnim characterAnim = new VNAnim();

        // SingleImage layout
        public Sprite image;
        public VNAnim imageAnim = new VNAnim();

        // Text
        public string speaker;
        [TextArea(2, 6)] public string text;
        [Min(0f)] public float typeSpeed = 40f;   // chars per second, 0 = instant

        // Text box style: uses the story's default style unless overridden
        public bool overrideTextStyle;
        public VNTextStyle textStyle = new VNTextStyle();

        // Sound
        public AudioClip sfx;
        [Range(0f, 1f)] public float sfxVolume = 1f;

        // Branching: no choices -> goes to nextId, otherwise player must pick
        public List<VNChoice> choices = new List<VNChoice>();
    }

    /// <summary>A clip on the Background or Music track. Spans frames from startNodeId to endNodeId (in story order).</summary>
    [Serializable]
    public class VNClip
    {
        public string id;
        public VNTrack track;
        public string startNodeId;
        public string endNodeId;
        public Sprite sprite;
        public AudioClip audio;
        [Range(0f, 1f)] public float volume = 1f;
        [Min(0f)] public float fadeIn = 0.6f;
        [Min(0f)] public float fadeOut = 0.6f;
        public bool loop = true;
    }

    [CreateAssetMenu(fileName = "NewStory", menuName = "Visual Novel/Story")]
    public class VNStory : ScriptableObject
    {
        public string startNodeId;
        public VNTextStyle defaultTextStyle = new VNTextStyle();
        public List<VNNodeData> nodes = new List<VNNodeData>();
        public List<VNClip> clips = new List<VNClip>();

        public VNNodeData GetNode(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].id == id) return nodes[i];
            return null;
        }

        /// <summary>
        /// Frames in "story order": depth-first from the start frame, first choice / next first.
        /// Unreachable frames are appended at the end. Timeline clips are measured in this order.
        /// </summary>
        public List<VNNodeData> GetOrderedNodes()
        {
            var result = new List<VNNodeData>();
            var visited = new HashSet<string>();
            var stack = new Stack<string>();

            var start = GetNode(startNodeId);
            if (start == null && nodes.Count > 0) start = nodes[0];
            if (start != null) stack.Push(start.id);

            while (stack.Count > 0)
            {
                var id = stack.Pop();
                if (string.IsNullOrEmpty(id) || visited.Contains(id)) continue;
                var n = GetNode(id);
                if (n == null) continue;
                visited.Add(id);
                result.Add(n);

                var targets = new List<string>();
                if (n.choices.Count == 0) targets.Add(n.nextId);
                else foreach (var c in n.choices) targets.Add(c.targetId);
                for (int i = targets.Count - 1; i >= 0; i--) stack.Push(targets[i]);
            }

            foreach (var n in nodes)
                if (!visited.Contains(n.id)) { visited.Add(n.id); result.Add(n); }

            return result;
        }
    }
}

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

    /// <summary>
    /// What a frame is for. Story must be first (0) so frames serialized before this field existed
    /// still deserialize as ordinary story frames. Exactly one Start frame is expected per story
    /// (auto-created with the asset); any number of End frames is fine (a story can have several endings).
    /// </summary>
    public enum VNNodeKind { Story, Start, End }

    /// <summary>Type of a story variable. Unity can't serialize a union, so each def carries one default per type.</summary>
    public enum VNVarType { Bool, Int, Float, String }

    /// <summary>How a condition compares a variable against a literal value.</summary>
    public enum VNCompareOp { Equals, NotEquals, Greater, GreaterOrEqual, Less, LessOrEqual }

    /// <summary>How an effect changes a variable.</summary>
    public enum VNVarOpType { Set, Add, Toggle }

    [Serializable]
    public class VNAnim
    {
        public VNAnimType type = VNAnimType.Fade;
        [Min(0f)] public float duration = 0.5f;
        [Min(0f)] public float delay = 0f;
        public Ease ease = Ease.OutQuad;
    }

    /// <summary>A plain full-screen fade, used by the Start frame's entrance and the End frame's exit curtain.</summary>
    [Serializable]
    public class VNFade
    {
        [Min(0f)] public float duration = 1f;
        [Min(0f)] public float delay = 0f;
        public Ease ease = Ease.InOutQuad;
    }

    /// <summary>Declares one story variable and its default value. Lives on the VNStory asset.</summary>
    [Serializable]
    public class VNVariableDef
    {
        public string name = "flag";
        public VNVarType type = VNVarType.Bool;
        public bool boolDefault;
        public int intDefault;
        public float floatDefault;
        public string stringDefault = "";

        /// <summary>The default value as a string, in the same format VNVariables stores values in.</summary>
        public string DefaultAsString()
        {
            switch (type)
            {
                case VNVarType.Bool: return boolDefault ? "true" : "false";
                case VNVarType.Int: return intDefault.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case VNVarType.Float: return floatDefault.ToString(System.Globalization.CultureInfo.InvariantCulture);
                default: return stringDefault ?? "";
            }
        }
    }

    /// <summary>A single "if variable OP value" test. A list of these is an AND.</summary>
    [Serializable]
    public class VNCondition
    {
        public string variable;
        public VNCompareOp op = VNCompareOp.Equals;
        public string value = "true";

        public bool Evaluate(VNVariables vars)
        {
            if (vars == null || string.IsNullOrEmpty(variable)) return true;
            var type = vars.GetType(variable);
            switch (type)
            {
                case VNVarType.Bool:
                {
                    bool a = vars.GetBool(variable);
                    bool b = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
                    return op == VNCompareOp.NotEquals ? a != b : a == b;
                }
                case VNVarType.Int:
                {
                    int a = vars.GetInt(variable);
                    int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int b);
                    return Compare(a, b);
                }
                case VNVarType.Float:
                {
                    float a = vars.GetFloat(variable);
                    float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float b);
                    return Compare(a, b);
                }
                default:
                {
                    string a = vars.GetString(variable) ?? "";
                    string b = value ?? "";
                    return op == VNCompareOp.NotEquals ? a != b : a == b;
                }
            }
        }

        bool Compare(float a, float b)
        {
            switch (op)
            {
                case VNCompareOp.Equals: return Mathf.Approximately(a, b);
                case VNCompareOp.NotEquals: return !Mathf.Approximately(a, b);
                case VNCompareOp.Greater: return a > b;
                case VNCompareOp.GreaterOrEqual: return a >= b;
                case VNCompareOp.Less: return a < b;
                case VNCompareOp.LessOrEqual: return a <= b;
                default: return true;
            }
        }

        public static bool EvaluateAll(List<VNCondition> conditions, VNVariables vars)
        {
            if (conditions == null) return true;
            for (int i = 0; i < conditions.Count; i++)
                if (!conditions[i].Evaluate(vars)) return false;
            return true;
        }
    }

    /// <summary>A change applied to a variable: on a node when it's shown, or on a choice when it's picked.</summary>
    [Serializable]
    public class VNVariableOp
    {
        public string variable;
        public VNVarOpType op = VNVarOpType.Set;
        public string value = "true";

        public void Apply(VNVariables vars)
        {
            if (vars == null || string.IsNullOrEmpty(variable)) return;
            var type = vars.GetType(variable);
            switch (type)
            {
                case VNVarType.Bool:
                    bool b = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
                    vars.SetBool(variable, op == VNVarOpType.Toggle ? !vars.GetBool(variable) : b);
                    break;
                case VNVarType.Int:
                    int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int iv);
                    vars.SetInt(variable, op == VNVarOpType.Add ? vars.GetInt(variable) + iv : iv);
                    break;
                case VNVarType.Float:
                    float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fv);
                    vars.SetFloat(variable, op == VNVarOpType.Add ? vars.GetFloat(variable) + fv : fv);
                    break;
                default:
                    vars.SetString(variable, op == VNVarOpType.Add ? (vars.GetString(variable) ?? "") + (value ?? "") : (value ?? ""));
                    break;
            }
        }

        public static void ApplyAll(List<VNVariableOp> ops, VNVariables vars)
        {
            if (ops == null) return;
            for (int i = 0; i < ops.Count; i++) ops[i].Apply(vars);
        }
    }

    /// <summary>One answer in a branch. Hidden at runtime unless its conditions pass.</summary>
    [Serializable]
    public class VNChoice
    {
        public string text = "Choice";
        [HideInInspector] public string targetId;
        public List<VNCondition> conditions = new List<VNCondition>();
        public List<VNVariableOp> effects = new List<VNVariableOp>();
    }

    /// <summary>A conditional fork used when a frame has no choices: first matching branch wins, else nextId.</summary>
    [Serializable]
    public class VNConditionalBranch
    {
        public List<VNCondition> conditions = new List<VNCondition>();
        [HideInInspector] public string targetId;
    }

    /// <summary>One character shown in a Scene frame. A frame lists every character visible in it.</summary>
    [Serializable]
    public class VNCharacterCue
    {
        public Sprite sprite;
        public VNSlot slot = VNSlot.Center;
        public VNAnim anim = new VNAnim();
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
        public VNNodeKind kind = VNNodeKind.Story;

        // Start-kind only: no content fields apply, just a curtain fade-in, then automatically nextId.
        public VNFade startFade = new VNFade();

        // End-kind only: no content fields apply (terminal - no outgoing link). A curtain fade-out,
        // then either loads a scene (if endSceneToLoad parses to a build index) or just stays black.
        public VNFade endFade = new VNFade();
        public string endSceneToLoad = "";

        public VNLayout layout = VNLayout.Scene;

        // Scene layout
        public Sprite background;            // optional override; otherwise taken from the Background track
        public VNAnim backgroundAnim = new VNAnim();
        public List<VNCharacterCue> characters = new List<VNCharacterCue>();   // every character visible in this frame

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

        // Variables: applied the moment this frame is shown
        public List<VNVariableOp> onEnterEffects = new List<VNVariableOp>();

        // Branching: choices take priority; with no choices, the first matching conditional branch wins;
        // with neither, the frame goes to nextId.
        public List<VNChoice> choices = new List<VNChoice>();
        public List<VNConditionalBranch> branches = new List<VNConditionalBranch>();
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
        public VNTextStyle defaultTextStyle = new VNTextStyle();
        public List<VNVariableDef> variables = new List<VNVariableDef>();
        public List<VNNodeData> nodes = new List<VNNodeData>();
        public List<VNClip> clips = new List<VNClip>();

        // ---------------------------------------------------------------- cache
        // GetNode/GetOrderedNodes are called very often (every frame shown, every timeline repaint).
        // Rebuilding an id -> node map and the DFS order on every call doesn't scale past a handful of
        // frames, so both are cached here and only rebuilt when something structural changes.
        [NonSerialized] Dictionary<string, VNNodeData> _index;
        [NonSerialized] List<VNNodeData> _orderedCache;
        [NonSerialized] VNNodeData _startCache;
        [NonSerialized] bool _dirty = true;

        /// <summary>Call after any structural edit (add/remove a frame, change a link, change a frame's kind).
        /// The editor calls this for edits made through code; property-field edits invalidate via OnValidate.</summary>
        public void InvalidateCache()
        {
            _dirty = true;
        }

        void OnEnable() => _dirty = true;
        void OnValidate() => _dirty = true;

        void RebuildIfNeeded()
        {
            if (!_dirty && _index != null && _orderedCache != null) return;
            _index = new Dictionary<string, VNNodeData>(nodes.Count);
            _startCache = null;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n == null) continue;
                if (!string.IsNullOrEmpty(n.id)) _index[n.id] = n;
                if (n.kind == VNNodeKind.Start && _startCache == null) _startCache = n;
            }
            _orderedCache = BuildOrderedNodes();
            _dirty = false;
        }

        public VNNodeData GetNode(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            RebuildIfNeeded();
            _index.TryGetValue(id, out var n);
            return n;
        }

        /// <summary>
        /// The story's entry point: the one frame of kind Start. Falls back to the first frame in the
        /// list for a story saved before Start frames existed (or one where it was deleted) - same
        /// behavior the runtime always had.
        /// </summary>
        public VNNodeData GetStartNode()
        {
            RebuildIfNeeded();
            return _startCache ?? (nodes.Count > 0 ? nodes[0] : null);
        }

        /// <summary>
        /// Frames in "story order": depth-first from the start frame, first choice / branch / next first.
        /// Unreachable frames are appended at the end. Timeline clips are measured in this order.
        /// </summary>
        public List<VNNodeData> GetOrderedNodes()
        {
            RebuildIfNeeded();
            return _orderedCache;
        }

        List<VNNodeData> BuildOrderedNodes()
        {
            var result = new List<VNNodeData>();
            var visited = new HashSet<string>();
            var stack = new Stack<string>();

            var start = _startCache ?? (nodes.Count > 0 ? nodes[0] : null);
            if (start != null) stack.Push(start.id);

            while (stack.Count > 0)
            {
                var id = stack.Pop();
                if (string.IsNullOrEmpty(id) || visited.Contains(id)) continue;
                if (!_index.TryGetValue(id, out var n)) continue;
                visited.Add(id);
                result.Add(n);

                var targets = new List<string>();
                if (n.kind == VNNodeKind.End)
                {
                    // terminal: nothing comes after an End frame
                }
                else if (n.choices.Count > 0)
                {
                    foreach (var c in n.choices) targets.Add(c.targetId);
                }
                else if (n.branches.Count > 0)
                {
                    foreach (var b in n.branches) targets.Add(b.targetId);
                    targets.Add(n.nextId);
                }
                else
                {
                    targets.Add(n.nextId);
                }
                for (int i = targets.Count - 1; i >= 0; i--) stack.Push(targets[i]);
            }

            foreach (var n in nodes)
                if (!visited.Contains(n.id)) { visited.Add(n.id); result.Add(n); }

            return result;
        }

        public VNVariableDef GetVariableDef(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < variables.Count; i++)
                if (variables[i].name == name) return variables[i];
            return null;
        }
    }
}

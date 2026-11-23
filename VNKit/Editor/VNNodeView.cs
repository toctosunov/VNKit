using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using VNKit;

namespace VNKitEditor
{
    /// <summary>Visual representation of one frame in the graph.</summary>
    public class VNNodeView : Node
    {
        /// <summary>userData of the single "Next" port (used when the frame has no choices).</summary>
        public const int NextIndex = -1;

        /// <summary>userData of branch port i is BranchIndexBase - i, kept well clear of NextIndex/choice indices.</summary>
        public const int BranchIndexBase = -1000;

        public VNNodeData Data;
        public Port InputPort;
        public readonly List<Port> OutputPorts = new List<Port>();
        public Action<VNNodeView> Selected;

        readonly UnityEngine.UIElements.Image thumb;
        readonly Label textLabel;

        public VNNodeView(VNNodeData data)
        {
            Data = data;
            viewDataKey = data.id;
            SetPosition(new Rect(data.position, Vector2.zero));

            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = "In";
            inputContainer.Add(InputPort);

            thumb = new UnityEngine.UIElements.Image();
            thumb.scaleMode = ScaleMode.ScaleToFit;
            thumb.style.width = 200;
            thumb.style.height = 112;
            thumb.style.marginTop = 4;
            extensionContainer.Add(thumb);

            textLabel = new Label();
            textLabel.style.whiteSpace = WhiteSpace.Normal;
            textLabel.style.maxWidth = 200;
            textLabel.style.marginTop = 4;
            textLabel.style.marginBottom = 4;
            extensionContainer.Add(textLabel);

            RebuildPorts();
            RefreshVisuals();
        }

        public override Port InstantiatePort(Orientation orientation, Direction direction, Port.Capacity capacity, Type type)
        {
            return VNPort.Create(orientation, direction, capacity, type);
        }

        public override void OnSelected()
        {
            base.OnSelected();
            Selected?.Invoke(this);
        }

        /// <summary>
        /// A Start frame has a single "Next" port (one thread out, straight to the first real frame).
        /// An End frame is terminal: no output ports at all.
        /// A Story frame: with choices, one port per choice plus a "+ choice" port to add another.
        /// Without choices but with conditional branches: one "If N" port per branch (drag to link,
        /// same as a choice), a "+ branch" port to add another, and a "Next (else)" fallback port.
        /// With neither: a single "Next" port that accepts several threads (a 2nd thread turns the
        /// frame into a two-choice branch - see VNGraphView.Link).
        /// </summary>
        public void RebuildPorts()
        {
            outputContainer.Clear();
            OutputPorts.Clear();

            if (Data.kind == VNNodeKind.Start)
            {
                var next = MakeOutput("Next", NextIndex, Port.Capacity.Single);
                next.tooltip = "Where the story begins once the entrance fade finishes.";
                OutputPorts.Add(next);
            }
            else if (Data.kind == VNNodeKind.End)
            {
                // terminal: nothing links out of an End frame
            }
            else if (Data.choices.Count > 0)
            {
                for (int i = 0; i < Data.choices.Count; i++)
                    OutputPorts.Add(MakeOutput((i + 1) + ". " + Short(Data.choices[i].text, 18), i, Port.Capacity.Single));

                var add = MakeOutput("+ choice", Data.choices.Count, Port.Capacity.Single);
                add.portColor = new Color(0.55f, 0.85f, 0.45f);
                add.tooltip = "Drag a thread from here to add another choice.";
                OutputPorts.Add(add);
            }
            else
            {
                for (int i = 0; i < Data.branches.Count; i++)
                {
                    var p = MakeOutput("If " + (i + 1), BranchIndexBase - i, Port.Capacity.Single);
                    p.portColor = new Color(0.5f, 0.65f, 0.95f);
                    p.tooltip = DescribeBranch(Data.branches[i]);
                    OutputPorts.Add(p);
                }

                if (Data.branches.Count > 0)
                {
                    var addBranch = MakeOutput("+ branch", BranchIndexBase - Data.branches.Count, Port.Capacity.Single);
                    addBranch.portColor = new Color(0.5f, 0.65f, 0.95f);
                    addBranch.tooltip = "Drag a thread from here to add another conditional branch.";
                    OutputPorts.Add(addBranch);
                }

                var next = MakeOutput(Data.branches.Count > 0 ? "Next (else)" : "Next", NextIndex, Port.Capacity.Multi);
                next.tooltip = Data.branches.Count > 0
                    ? "Where the story goes if none of the branches above match."
                    : "Drag a thread out and release on empty space to create the next frame.\n" +
                      "Drag a second thread from here to split the story into choices.\n" +
                      "Right-click the frame > Add Conditional Branch for a non-player-facing fork instead.";
                OutputPorts.Add(next);
            }

            RefreshExpandedState();
            RefreshPorts();
        }

        static string DescribeBranch(VNConditionalBranch b)
        {
            if (b.conditions == null || b.conditions.Count == 0) return "Always matches (no conditions set)";
            var parts = new List<string>();
            foreach (var c in b.conditions)
            {
                if (string.IsNullOrEmpty(c.variable)) continue;
                parts.Add($"{c.variable} {OpSymbol(c.op)} {c.value}");
            }
            return parts.Count == 0 ? "Always matches (no conditions set)" : string.Join(" AND ", parts);
        }

        static string OpSymbol(VNCompareOp op)
        {
            switch (op)
            {
                case VNCompareOp.Equals: return "==";
                case VNCompareOp.NotEquals: return "!=";
                case VNCompareOp.Greater: return ">";
                case VNCompareOp.GreaterOrEqual: return ">=";
                case VNCompareOp.Less: return "<";
                case VNCompareOp.LessOrEqual: return "<=";
                default: return "?";
            }
        }

        Port MakeOutput(string name, int index, Port.Capacity capacity)
        {
            var p = InstantiatePort(Orientation.Horizontal, Direction.Output, capacity, typeof(bool));
            p.portName = name;
            p.userData = index;
            outputContainer.Add(p);
            return p;
        }

        public void RefreshVisuals()
        {
            string t = string.IsNullOrEmpty(Data.title) ? "Frame" : Data.title;
            switch (Data.kind)
            {
                case VNNodeKind.Start:
                    title = "\u25B6 " + t;
                    titleContainer.style.backgroundColor = new Color(0.75f, 0.42f, 0.1f);
                    break;
                case VNNodeKind.End:
                    title = "\u25A0 " + t;
                    titleContainer.style.backgroundColor = new Color(0.5f, 0.15f, 0.15f);
                    break;
                default:
                    title = t;
                    titleContainer.style.backgroundColor = new StyleColor(StyleKeyword.Null);
                    break;
            }

            Sprite sprite = null;
            if (Data.kind == VNNodeKind.Story)
                sprite = Data.layout == VNLayout.SingleImage ? Data.image : Data.background;
            thumb.sprite = sprite;
            thumb.style.display = sprite != null ? DisplayStyle.Flex : DisplayStyle.None;

            string label;
            switch (Data.kind)
            {
                case VNNodeKind.Start: label = $"Fades in over {Data.startFade.duration:0.##}s, then continues."; break;
                case VNNodeKind.End: label = string.IsNullOrEmpty(Data.endSceneToLoad)
                    ? $"Fades out over {Data.endFade.duration:0.##}s, then ends the story."
                    : $"Fades out over {Data.endFade.duration:0.##}s, then loads scene {Data.endSceneToLoad}."; break;
                default:
                    string speaker = string.IsNullOrEmpty(Data.speaker) ? "" : Data.speaker + ": ";
                    label = Short(speaker + Data.text, 70);
                    break;
            }
            textLabel.text = label;
            textLabel.style.display = string.IsNullOrEmpty(textLabel.text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        static string Short(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ");
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
}

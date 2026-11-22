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
            RefreshVisuals(false);
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
        /// No choices: one "Next" port that accepts several threads (a 2nd thread turns the frame into a branch).
        /// With choices: one port per choice, plus a "+ choice" port to add another one.
        /// </summary>
        public void RebuildPorts()
        {
            outputContainer.Clear();
            OutputPorts.Clear();

            if (Data.choices.Count == 0)
            {
                var next = MakeOutput("Next", NextIndex, Port.Capacity.Multi);
                next.tooltip = "Drag a thread out and release on empty space to create the next frame.\n" +
                               "Drag a second thread from here to split the story into choices.";
                OutputPorts.Add(next);
            }
            else
            {
                for (int i = 0; i < Data.choices.Count; i++)
                    OutputPorts.Add(MakeOutput((i + 1) + ". " + Short(Data.choices[i].text, 18), i, Port.Capacity.Single));

                var add = MakeOutput("+ choice", Data.choices.Count, Port.Capacity.Single);
                add.portColor = new Color(0.55f, 0.85f, 0.45f);
                add.tooltip = "Drag a thread from here to add another choice.";
                OutputPorts.Add(add);
            }

            RefreshExpandedState();
            RefreshPorts();
        }

        Port MakeOutput(string name, int index, Port.Capacity capacity)
        {
            var p = InstantiatePort(Orientation.Horizontal, Direction.Output, capacity, typeof(bool));
            p.portName = name;
            p.userData = index;
            outputContainer.Add(p);
            return p;
        }

        public void RefreshVisuals(bool isStart)
        {
            string t = string.IsNullOrEmpty(Data.title) ? "Frame" : Data.title;
            title = (isStart ? "\u25B6 " : "") + t;
            if (isStart) titleContainer.style.backgroundColor = new Color(0.75f, 0.42f, 0.1f);
            else titleContainer.style.backgroundColor = new StyleColor(StyleKeyword.Null);

            Sprite sprite = Data.layout == VNLayout.Scene ? Data.background : Data.image;
            thumb.sprite = sprite;
            thumb.style.display = sprite != null ? DisplayStyle.Flex : DisplayStyle.None;

            string speaker = string.IsNullOrEmpty(Data.speaker) ? "" : Data.speaker + ": ";
            textLabel.text = Short(speaker + Data.text, 70);
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

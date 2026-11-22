using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using VNKit;

namespace VNKitEditor
{
    /// <summary>The node canvas. Data lives in VNStory; edges are derived from nextId / choice targetId.</summary>
    public class VNGraphView : GraphView
    {
        public VNStory Story { get; private set; }
        public Action<VNNodeView> NodeSelected;
        public Action GraphChanged;

        readonly Dictionary<string, VNNodeView> views = new Dictionary<string, VNNodeView>();
        readonly HashSet<Edge> dataEdges = new HashSet<Edge>();   // edges we built from story data
        bool loading;

        public VNGraphView()
        {
            style.flexGrow = 1f;
            SetupZoom(0.2f, 2f);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            graphViewChanged = OnGraphViewChanged;
        }

        // ------------------------------------------------------------ loading

        public void Load(VNStory story)
        {
            loading = true;
            Story = story;
            DeleteElements(graphElements.ToList());
            views.Clear();
            dataEdges.Clear();

            if (story != null)
            {
                foreach (var d in story.nodes) AddView(d);
                foreach (var v in views.Values.ToList()) ConnectEdges(v);
            }
            loading = false;
        }

        VNNodeView AddView(VNNodeData d)
        {
            var v = new VNNodeView(d);
            v.Selected = n => NodeSelected?.Invoke(n);
            v.RefreshVisuals(d.id == Story.startNodeId);
            AddElement(v);
            views[d.id] = v;
            return v;
        }

        void ConnectEdges(VNNodeView v)
        {
            for (int i = 0; i < v.OutputPorts.Count; i++)
            {
                var port = v.OutputPorts[i];
                int idx = (int)port.userData;
                string targetId = idx < 0 ? v.Data.nextId : (idx < v.Data.choices.Count ? v.Data.choices[idx].targetId : null);
                if (string.IsNullOrEmpty(targetId)) continue;
                if (!views.TryGetValue(targetId, out var target)) continue;
                var edge = port.ConnectTo(target.InputPort);
                AddElement(edge);
                dataEdges.Add(edge);
            }
        }

        public VNNodeView GetView(string id)
        {
            views.TryGetValue(id ?? "", out var v);
            return v;
        }

        public void SelectNode(string id)
        {
            var v = GetView(id);
            if (v == null) return;
            ClearSelection();
            AddToSelection(v);
        }

        /// <summary>Call after the node's data (choices, text, images...) was edited elsewhere.</summary>
        public void RefreshNode(VNNodeView v)
        {
            if (v == null) return;
            loading = true;
            foreach (var p in v.OutputPorts)
            {
                foreach (var e in p.connections.ToList())
                {
                    dataEdges.Remove(e);
                    e.input?.Disconnect(e);
                    e.output?.Disconnect(e);
                    RemoveElement(e);
                }
            }
            v.RebuildPorts();
            ConnectEdges(v);
            v.RefreshVisuals(v.Data.id == Story.startNodeId);
            loading = false;
        }

        void RefreshAllVisuals()
        {
            foreach (var v in views.Values) v.RefreshVisuals(v.Data.id == Story.startNodeId);
        }

        // ------------------------------------------------------------ editing

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var result = new List<Port>();
            ports.ForEach(p =>
            {
                if (p != startPort && p.node != startPort.node && p.direction != startPort.direction)
                    result.Add(p);
            });
            return result;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (Story == null) return;
            var pos = contentViewContainer.WorldToLocal(evt.mousePosition);
            evt.menu.AppendAction("Add Frame", a => CreateNode(pos));
            if (evt.target is VNNodeView nv)
                evt.menu.AppendAction("Set as Start Frame", a => SetStart(nv.Data.id));
            base.BuildContextualMenu(evt);
        }

        /// <summary>Right-click "Add Frame": creates a frame and selects it.</summary>
        public VNNodeView CreateNode(Vector2 position)
        {
            if (Story == null) return null;
            var v = CreateNodeInternal(position);
            ClearSelection();
            AddToSelection(v);
            GraphChanged?.Invoke();
            return v;
        }

        VNNodeView CreateNodeInternal(Vector2 position)
        {
            Undo.RecordObject(Story, "Add Frame");
            var d = new VNNodeData
            {
                id = Guid.NewGuid().ToString("N"),
                position = position,
                title = "Frame " + (Story.nodes.Count + 1)
            };
            Story.nodes.Add(d);
            if (string.IsNullOrEmpty(Story.startNodeId)) Story.startNodeId = d.id;
            Story.InvalidateCache();
            EditorUtility.SetDirty(Story);
            return AddView(d);
        }

        public void SetStart(string id)
        {
            if (Story == null) return;
            Undo.RecordObject(Story, "Set Start Frame");
            Story.startNodeId = id;
            Story.InvalidateCache();
            EditorUtility.SetDirty(Story);
            RefreshAllVisuals();
            GraphChanged?.Invoke();
        }

        // ------------------------------------------------------------ threads (called from VNPort)

        /// <summary>A thread was released on a port.</summary>
        public void ConnectFromDrop(Edge edge)
        {
            var from = edge.output != null ? edge.output.node as VNNodeView : null;
            var to = edge.input != null ? edge.input.node as VNNodeView : null;
            int idx = edge.output != null && edge.output.userData is int i ? i : VNNodeView.NextIndex;
            bool rewire = dataEdges.Contains(edge);   // an existing thread was moved, not a new one pulled out

            DiscardEdge(edge);
            if (Story == null || from == null || to == null) return;

            bool branched = Link(from, idx, to, rewire);
            if (branched) SelectNode(from.Data.id);   // so the choice fields appear in the inspector
            GraphChanged?.Invoke();
        }

        /// <summary>A thread was released on empty space: create a new frame there and connect it.</summary>
        public void CreateNodeFromDrop(Edge edge, Vector2 worldPosition)
        {
            var from = edge.output != null ? edge.output.node as VNNodeView : null;
            int idx = edge.output != null && edge.output.userData is int i ? i : VNNodeView.NextIndex;
            bool wasExisting = dataEdges.Contains(edge) || edge.input != null;

            DiscardEdge(edge);
            if (Story == null || from == null) return;

            if (wasExisting)
            {
                RefreshNode(from);   // an existing thread dropped in the void: leave the story as it was
                return;
            }

            int group = Undo.GetCurrentGroup();
            var to = CreateNodeInternal(contentViewContainer.WorldToLocal(worldPosition));
            bool branched = Link(from, idx, to, false);
            Undo.CollapseUndoOperations(group);

            // branch -> show the parent (choice fields), otherwise jump to the new frame to write its text
            ClearSelection();
            AddToSelection(branched ? from : to);
            GraphChanged?.Invoke();
        }

        /// <summary>
        /// Writes the connection into the story data and rebuilds the node's ports/threads.
        /// Returns true if the frame has choices afterwards because of this link.
        /// </summary>
        bool Link(VNNodeView from, int portIndex, VNNodeView to, bool replace)
        {
            var d = from.Data;
            bool branched = false;
            Undo.RecordObject(Story, "Connect Frames");

            if (portIndex < 0)
            {
                if (replace || string.IsNullOrEmpty(d.nextId) || d.nextId == to.Data.id)
                {
                    d.nextId = to.Data.id;
                }
                else
                {
                    // second thread out of "Next": the frame becomes a branch with two choices
                    d.choices.Add(new VNChoice { text = "Choice 1", targetId = d.nextId });
                    d.choices.Add(new VNChoice { text = "Choice 2", targetId = to.Data.id });
                    d.nextId = null;
                    branched = true;
                }
            }
            else if (portIndex < d.choices.Count)
            {
                d.choices[portIndex].targetId = to.Data.id;
            }
            else
            {
                // the "+ choice" port
                d.choices.Add(new VNChoice { text = "Choice " + (d.choices.Count + 1), targetId = to.Data.id });
                branched = true;
            }

            Story.InvalidateCache();
            EditorUtility.SetDirty(Story);
            RefreshNode(from);
            return branched;
        }

        void DiscardEdge(Edge edge)
        {
            if (edge == null) return;
            dataEdges.Remove(edge);
            edge.input?.Disconnect(edge);
            edge.output?.Disconnect(edge);
            if (edge.parent != null) RemoveElement(edge);
        }

        // ------------------------------------------------------------ graph changes (delete / move)

        GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (loading || Story == null) return change;
            Undo.RecordObject(Story, "Edit Story Graph");

            if (change.elementsToRemove != null)
            {
                foreach (var el in change.elementsToRemove)
                    if (el is Edge edge) ClearLink(edge);

                foreach (var el in change.elementsToRemove)
                    if (el is VNNodeView nv) RemoveNodeData(nv);
            }

            if (change.movedElements != null)
            {
                foreach (var el in change.movedElements)
                    if (el is VNNodeView nv) nv.Data.position = nv.GetPosition().position;
            }

            Story.InvalidateCache();
            EditorUtility.SetDirty(Story);
            GraphChanged?.Invoke();
            return change;
        }

        static void ClearLink(Edge edge)
        {
            var from = edge.output != null ? edge.output.node as VNNodeView : null;
            var to = edge.input != null ? edge.input.node as VNNodeView : null;
            if (from == null || to == null) return;
            int idx = (int)edge.output.userData;
            if (idx < 0)
            {
                if (from.Data.nextId == to.Data.id) from.Data.nextId = null;
            }
            else if (idx < from.Data.choices.Count && from.Data.choices[idx].targetId == to.Data.id)
            {
                from.Data.choices[idx].targetId = null;
            }
        }

        void RemoveNodeData(VNNodeView nv)
        {
            string id = nv.Data.id;
            Story.nodes.RemoveAll(n => n.id == id);
            views.Remove(id);

            foreach (var n in Story.nodes)
            {
                if (n.nextId == id) n.nextId = null;
                foreach (var c in n.choices)
                    if (c.targetId == id) c.targetId = null;
            }

            Story.clips.RemoveAll(c => c.startNodeId == id || c.endNodeId == id);

            if (Story.startNodeId == id)
            {
                Story.startNodeId = Story.nodes.Count > 0 ? Story.nodes[0].id : null;
                RefreshAllVisuals();
            }
        }
    }
}

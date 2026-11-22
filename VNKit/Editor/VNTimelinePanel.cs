using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VNKit;

namespace VNKitEditor
{
    /// <summary>
    /// Video-editor style timeline under the graph. Columns are frames in story order,
    /// two tracks (Background, Music). Drag a clip to move it, drag its edges to change length.
    /// </summary>
    public class VNTimelinePanel : VisualElement
    {
        public Action<string> NodeClicked;
        public string selectedNodeId;

        VNStory story;
        string selectedClipId;
        float cellW = 110f;
        float scroll;
        readonly IMGUIContainer gui;

        enum DragMode { None, Move, ResizeLeft, ResizeRight }
        DragMode drag;
        VNClip dragClip;
        float dragStartX;
        int dragStart, dragEnd;

        const float LabelW = 90f;
        const float HeaderH = 24f;
        const float RowH = 46f;
        const float EdgeGrab = 8f;

        static readonly string[] TrackNames = { "Background", "Music" };
        static readonly Color[] TrackColors =
        {
            new Color(0.25f, 0.45f, 0.75f),
            new Color(0.3f, 0.65f, 0.4f)
        };

        GUIStyle cellStyle, clipStyle;

        public VNTimelinePanel()
        {
            style.flexGrow = 1f;
            gui = new IMGUIContainer(OnGUI);
            gui.style.flexGrow = 1f;
            Add(gui);
        }

        public void SetStory(VNStory s)
        {
            story = s;
            selectedClipId = null;
            scroll = 0f;
            Refresh();
        }

        public void Refresh()
        {
            gui.MarkDirtyRepaint();
        }

        // ------------------------------------------------------------ GUI

        void EnsureStyles()
        {
            if (cellStyle != null) return;
            cellStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };
            cellStyle.normal.textColor = Color.white;
            clipStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(6, 4, 0, 0)
            };
            clipStyle.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            if (story == null)
            {
                GUILayout.Label("Open a Story asset to see the timeline.");
                return;
            }
            EnsureStyles();

            var order = story.GetOrderedNodes();
            var e = Event.current;
            int ctrl = GUIUtility.GetControlID(FocusType.Passive);

            DrawToolbar(order);

            Rect area = GUILayoutUtility.GetRect(10f, HeaderH + RowH * 2f, GUILayout.ExpandWidth(true));
            float viewW = Mathf.Max(10f, area.width - LabelW);
            float totalW = Mathf.Max(order.Count * cellW, viewW);
            scroll = Mathf.Clamp(scroll, 0f, totalW - viewW);

            for (int t = 0; t < 2; t++)
                GUI.Label(new Rect(area.x + 4f, area.y + HeaderH + t * RowH, LabelW - 4f, RowH), TrackNames[t], EditorStyles.boldLabel);

            GUI.BeginClip(new Rect(area.x + LabelW, area.y, viewW, area.height));
            DrawTracks(order, e, ctrl, viewW);
            GUI.EndClip();

            if (e.type == EventType.ScrollWheel && area.Contains(e.mousePosition))
            {
                scroll = Mathf.Clamp(scroll + (e.delta.y + e.delta.x) * 25f, 0f, totalW - viewW);
                e.Use();
            }

            Rect sb = GUILayoutUtility.GetRect(10f, 16f, GUILayout.ExpandWidth(true));
            scroll = GUI.HorizontalScrollbar(new Rect(sb.x + LabelW, sb.y, sb.width - LabelW, sb.height), scroll, viewW, 0f, totalW);

            DrawClipInspector();
        }

        void DrawToolbar(List<VNNodeData> order)
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Timeline (frames in story order)", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Zoom", EditorStyles.miniLabel, GUILayout.Width(36f));
            cellW = GUILayout.HorizontalSlider(cellW, 60f, 240f, GUILayout.Width(110f));
            if (GUILayout.Button("+ Background", EditorStyles.toolbarButton, GUILayout.Width(95f)))
                AddClip(VNTrack.Background, Mathf.Max(0, IndexOf(order, selectedNodeId)));
            if (GUILayout.Button("+ Music", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                AddClip(VNTrack.Music, Mathf.Max(0, IndexOf(order, selectedNodeId)));
            GUILayout.EndHorizontal();
        }

        void DrawTracks(List<VNNodeData> order, Event e, int ctrl, float viewW)
        {
            // track rows
            for (int t = 0; t < 2; t++)
                EditorGUI.DrawRect(new Rect(0f, HeaderH + t * RowH, viewW, RowH - 2f), new Color(0.16f, 0.16f, 0.16f));

            // header cells + grid
            for (int i = 0; i < order.Count; i++)
            {
                float x = i * cellW - scroll;
                if (x + cellW < 0f || x > viewW) continue;

                var hr = new Rect(x, 0f, cellW - 1f, HeaderH - 2f);
                bool sel = order[i].id == selectedNodeId;
                EditorGUI.DrawRect(hr, sel ? new Color(0.85f, 0.5f, 0.15f) : new Color(0.26f, 0.26f, 0.26f));
                string title = string.IsNullOrEmpty(order[i].title) ? "Frame" : order[i].title;
                GUI.Label(hr, (i + 1) + ". " + title, cellStyle);
                EditorGUI.DrawRect(new Rect(x + cellW - 1f, HeaderH, 1f, RowH * 2f), new Color(1f, 1f, 1f, 0.06f));

                if (e.type == EventType.MouseDown && e.button == 0 && hr.Contains(e.mousePosition))
                {
                    NodeClicked?.Invoke(order[i].id);
                    e.Use();
                }
            }

            // clips
            foreach (var c in story.clips)
            {
                int s = IndexOf(order, c.startNodeId);
                int en = IndexOf(order, c.endNodeId);
                if (s < 0 || en < 0) continue;
                if (s > en) { int tmp = s; s = en; en = tmp; }

                var r = new Rect(s * cellW - scroll + 1f, HeaderH + (int)c.track * RowH + 2f, (en - s + 1) * cellW - 3f, RowH - 6f);
                bool sel = c.id == selectedClipId;
                Color col = TrackColors[(int)c.track];
                if (sel) col = Color.Lerp(col, Color.white, 0.25f);
                EditorGUI.DrawRect(r, col);
                if (sel)
                {
                    var o = new Color(1f, 1f, 1f, 0.9f);
                    EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), o);
                    EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), o);
                    EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), o);
                    EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), o);
                }

                string name = c.track == VNTrack.Background
                    ? (c.sprite != null ? c.sprite.name : "(no image)")
                    : (c.audio != null ? c.audio.name : "(no audio)");
                GUI.Label(r, name, clipStyle);

                EditorGUIUtility.AddCursorRect(new Rect(r.x, r.y, EdgeGrab, r.height), MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(new Rect(r.xMax - EdgeGrab, r.y, EdgeGrab, r.height), MouseCursor.ResizeHorizontal);

                if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
                {
                    if (e.button == 1)
                    {
                        ShowClipMenu(c);
                        e.Use();
                    }
                    else if (e.button == 0)
                    {
                        selectedClipId = c.id;
                        Undo.RecordObject(story, "Edit Clip");
                        dragClip = c;
                        dragStart = s;
                        dragEnd = en;
                        dragStartX = e.mousePosition.x;
                        float lx = e.mousePosition.x - r.xMin;
                        if (lx < EdgeGrab) drag = DragMode.ResizeLeft;
                        else if (r.xMax - e.mousePosition.x < EdgeGrab) drag = DragMode.ResizeRight;
                        else drag = DragMode.Move;
                        GUIUtility.hotControl = ctrl;
                        e.Use();
                    }
                }
            }

            // dragging
            if (GUIUtility.hotControl == ctrl && drag != DragMode.None && dragClip != null)
            {
                if (e.type == EventType.MouseDrag)
                {
                    int d = Mathf.RoundToInt((e.mousePosition.x - dragStartX) / cellW);
                    int ns = dragStart, ne = dragEnd;
                    int last = Mathf.Max(0, order.Count - 1);
                    switch (drag)
                    {
                        case DragMode.Move:
                            int len = dragEnd - dragStart;
                            ns = Mathf.Clamp(dragStart + d, 0, Mathf.Max(0, last - len));
                            ne = ns + len;
                            break;
                        case DragMode.ResizeLeft:
                            ns = Mathf.Clamp(dragStart + d, 0, dragEnd);
                            break;
                        case DragMode.ResizeRight:
                            ne = Mathf.Clamp(dragEnd + d, dragStart, last);
                            break;
                    }
                    if (ns < order.Count && ne < order.Count)
                    {
                        dragClip.startNodeId = order[ns].id;
                        dragClip.endNodeId = order[ne].id;
                        EditorUtility.SetDirty(story);
                    }
                    e.Use();
                    gui.MarkDirtyRepaint();
                }
                else if (e.type == EventType.MouseUp)
                {
                    drag = DragMode.None;
                    dragClip = null;
                    GUIUtility.hotControl = 0;
                    e.Use();
                }
            }

            // right click on empty track area -> add clip
            var tracksRect = new Rect(0f, HeaderH, viewW, RowH * 2f);
            if (e.type == EventType.MouseDown && e.button == 1 && tracksRect.Contains(e.mousePosition))
            {
                int track = Mathf.Clamp((int)((e.mousePosition.y - HeaderH) / RowH), 0, 1);
                int cell = Mathf.FloorToInt((e.mousePosition.x + scroll) / cellW);
                if (cell >= 0 && cell < order.Count)
                {
                    var menu = new GenericMenu();
                    var tr = (VNTrack)track;
                    menu.AddItem(new GUIContent("Add " + TrackNames[track] + " clip here"), false, () => AddClip(tr, cell));
                    menu.ShowAsContext();
                }
                e.Use();
            }
        }

        void DrawClipInspector()
        {
            var c = story.clips.Find(x => x.id == selectedClipId);
            if (c == null)
            {
                EditorGUILayout.LabelField("Select a clip. Drag it to move, drag its edges to resize, right-click an empty slot to add.", EditorStyles.miniLabel);
                return;
            }

            float oldLabel = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 62f;
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            Sprite sprite = c.sprite;
            AudioClip audio = c.audio;
            if (c.track == VNTrack.Background)
                sprite = (Sprite)EditorGUILayout.ObjectField("Image", c.sprite, typeof(Sprite), false, GUILayout.MinWidth(170f));
            else
                audio = (AudioClip)EditorGUILayout.ObjectField("Audio", c.audio, typeof(AudioClip), false, GUILayout.MinWidth(170f));

            float volume = c.volume;
            if (c.track == VNTrack.Music)
                volume = EditorGUILayout.Slider("Volume", c.volume, 0f, 1f, GUILayout.MinWidth(150f));
            float fadeIn = Mathf.Max(0f, EditorGUILayout.FloatField("Fade in", c.fadeIn, GUILayout.Width(110f)));
            float fadeOut = Mathf.Max(0f, EditorGUILayout.FloatField("Fade out", c.fadeOut, GUILayout.Width(115f)));
            bool loop = c.loop;
            if (c.track == VNTrack.Music)
                loop = EditorGUILayout.ToggleLeft("Loop", c.loop, GUILayout.Width(55f));

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(story, "Edit Clip");
                c.sprite = sprite;
                c.audio = audio;
                c.volume = volume;
                c.fadeIn = fadeIn;
                c.fadeOut = fadeOut;
                c.loop = loop;
                EditorUtility.SetDirty(story);
            }

            if (GUILayout.Button("Delete", GUILayout.Width(55f))) DeleteClip(c);
            EditorGUILayout.EndHorizontal();
            EditorGUIUtility.labelWidth = oldLabel;
        }

        // ------------------------------------------------------------ helpers

        void ShowClipMenu(VNClip c)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Delete clip"), false, () => DeleteClip(c));
            menu.ShowAsContext();
        }

        void DeleteClip(VNClip c)
        {
            Undo.RecordObject(story, "Delete Clip");
            story.clips.Remove(c);
            if (selectedClipId == c.id) selectedClipId = null;
            EditorUtility.SetDirty(story);
            gui.MarkDirtyRepaint();
        }

        void AddClip(VNTrack track, int startIdx)
        {
            var order = story.GetOrderedNodes();
            if (order.Count == 0) return;
            startIdx = Mathf.Clamp(startIdx, 0, order.Count - 1);
            int endIdx = Mathf.Min(order.Count - 1, startIdx + 2);

            Undo.RecordObject(story, "Add Clip");
            var c = new VNClip
            {
                id = Guid.NewGuid().ToString("N"),
                track = track,
                startNodeId = order[startIdx].id,
                endNodeId = order[endIdx].id
            };
            story.clips.Add(c);
            selectedClipId = c.id;
            EditorUtility.SetDirty(story);
            gui.MarkDirtyRepaint();
        }

        static int IndexOf(List<VNNodeData> order, string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < order.Count; i++)
                if (order[i].id == id) return i;
            return -1;
        }
    }
}

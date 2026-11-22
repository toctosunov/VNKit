using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VNKit;

namespace VNKitEditor
{
    /// <summary>Main window: graph on top, frame inspector on the right, timeline at the bottom.</summary>
    public class VNStoryEditorWindow : EditorWindow
    {
        [SerializeField] VNStory story;

        ObjectField storyField;
        VNGraphView graph;
        VNTimelinePanel timeline;
        IMGUIContainer inspector;
        SerializedObject so;
        string selectedNodeId;
        Vector2 inspectorScroll;

        [MenuItem("Tools/Visual Novel/Story Editor")]
        public static VNStoryEditorWindow Open()
        {
            var w = GetWindow<VNStoryEditorWindow>("Story Editor");
            w.minSize = new Vector2(900f, 560f);
            return w;
        }

        public void SetStory(VNStory s)
        {
            story = s;
            if (graph != null) Bind(s);
        }

        void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            AssetDatabase.SaveAssets();
        }

        void OnLostFocus()
        {
            AssetDatabase.SaveAssets();
        }

        void OnUndoRedo()
        {
            if (graph == null || story == null) return;
            so = new SerializedObject(story);
            graph.Load(story);
            timeline.Refresh();
            inspector.MarkDirtyRepaint();
        }

        void CreateGUI()
        {
            var root = rootVisualElement;

            var toolbar = new Toolbar();
            storyField = new ObjectField("Story") { objectType = typeof(VNStory), allowSceneObjects = false };
            storyField.style.minWidth = 320f;
            storyField.RegisterValueChangedCallback(evt => Bind(evt.newValue as VNStory));
            toolbar.Add(storyField);
            toolbar.Add(new ToolbarButton(NewStory) { text = "New Story" });
            toolbar.Add(new ToolbarButton(() => AssetDatabase.SaveAssets()) { text = "Save" });
            root.Add(toolbar);

            graph = new VNGraphView();
            graph.NodeSelected = v =>
            {
                selectedNodeId = v.Data.id;
                timeline.selectedNodeId = selectedNodeId;
                timeline.Refresh();
                inspector.MarkDirtyRepaint();
            };
            graph.GraphChanged = () =>
            {
                timeline.Refresh();
                inspector.MarkDirtyRepaint();   // choices may have been created by dragging threads
            };

            inspector = new IMGUIContainer(DrawInspector);
            inspector.style.minWidth = 260f;

            timeline = new VNTimelinePanel();
            timeline.NodeClicked = id =>
            {
                selectedNodeId = id;
                timeline.selectedNodeId = id;
                graph.SelectNode(id);
                inspector.MarkDirtyRepaint();
            };

            var horizontal = new TwoPaneSplitView(1, 340f, TwoPaneSplitViewOrientation.Horizontal);
            horizontal.Add(graph);
            horizontal.Add(inspector);

            var vertical = new TwoPaneSplitView(1, 250f, TwoPaneSplitViewOrientation.Vertical);
            vertical.Add(horizontal);
            vertical.Add(timeline);
            vertical.style.flexGrow = 1f;
            root.Add(vertical);

            if (story != null) Bind(story);
        }

        void NewStory()
        {
            string path = EditorUtility.SaveFilePanelInProject("New Story", "NewStory", "asset", "Where to save the story asset?");
            if (string.IsNullOrEmpty(path)) return;
            var s = CreateInstance<VNStory>();
            AssetDatabase.CreateAsset(s, path);
            AssetDatabase.SaveAssets();
            Bind(s);
        }

        void Bind(VNStory s)
        {
            story = s;
            so = s != null ? new SerializedObject(s) : null;
            selectedNodeId = null;
            if (timeline != null) timeline.selectedNodeId = null;
            storyField?.SetValueWithoutNotify(s);
            graph.Load(s);
            timeline.SetStory(s);
            inspector.MarkDirtyRepaint();
        }

        // ------------------------------------------------------------ inspector

        void DrawInspector()
        {
            if (story == null)
            {
                EditorGUILayout.HelpBox("Pick a Story asset above or press New Story.", MessageType.Info);
                return;
            }
            if (so == null || so.targetObject != story) so = new SerializedObject(story);
            so.Update();

            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);

            int idx = story.nodes.FindIndex(n => n.id == selectedNodeId);
            bool generalChanged = false, choicesChanged = false;

            if (idx < 0)
            {
                EditorGUILayout.HelpBox(
                    "Right-click the graph > Add Frame, or drag a thread out of a frame's Next port and release it on empty space. " +
                    "Drag a second thread from the same port to make a choice.", MessageType.Info);
            }
            else
            {
                var np = so.FindProperty("nodes").GetArrayElementAtIndex(idx);
                DrawFrame(np, out generalChanged, out choicesChanged);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.PropertyField(so.FindProperty("defaultTextStyle"),
                new GUIContent("Default Text Style (all frames)"), true);

            EditorGUILayout.EndScrollView();
            so.ApplyModifiedProperties();

            if (idx >= 0)
            {
                var view = graph.GetView(selectedNodeId);
                if (view != null)
                {
                    view.Data = story.nodes[idx];
                    if (choicesChanged)
                    {
                        graph.RefreshNode(view);
                        timeline.Refresh();
                    }
                    else if (generalChanged)
                    {
                        view.RefreshVisuals(story.startNodeId == view.Data.id);
                    }
                }
            }
        }

        void DrawFrame(SerializedProperty np, out bool generalChanged, out bool choicesChanged)
        {
            EditorGUI.BeginChangeCheck();
            Prop(np, "title");
            Prop(np, "layout");
            var layout = (VNLayout)np.FindPropertyRelative("layout").enumValueIndex;

            EditorGUILayout.Space(4f);
            if (layout == VNLayout.Scene)
            {
                Prop(np, "background");
                Prop(np, "backgroundAnim");
                Prop(np, "character");
                Prop(np, "slot");
                Prop(np, "characterAnim");
            }
            else
            {
                Prop(np, "image");
                Prop(np, "imageAnim");
            }

            EditorGUILayout.Space(4f);
            Prop(np, "speaker");
            Prop(np, "text");
            Prop(np, "typeSpeed");
            Prop(np, "sfx");
            Prop(np, "sfxVolume");

            EditorGUILayout.Space(4f);
            Prop(np, "overrideTextStyle");
            if (np.FindPropertyRelative("overrideTextStyle").boolValue)
                Prop(np, "textStyle");
            generalChanged = EditorGUI.EndChangeCheck();

            // ---- choices
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Choices (none = goes to the Next port)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Pull a 2nd thread out of the Next port, or use \"+ choice\" on the frame, to add a choice.",
                EditorStyles.wordWrappedMiniLabel);

            var cp = np.FindPropertyRelative("choices");
            var nextProp = np.FindPropertyRelative("nextId");
            choicesChanged = false;
            int removeAt = -1;

            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < cp.arraySize; i++)
            {
                var el = cp.GetArrayElementAtIndex(i);
                var target = story.GetNode(el.FindPropertyRelative("targetId").stringValue);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField((i + 1) + ".", GUILayout.Width(18f));
                EditorGUILayout.PropertyField(el.FindPropertyRelative("text"), GUIContent.none);
                GUILayout.Label(target != null ? "\u2192 " + target.title : "\u2192 (not linked)",
                    EditorStyles.miniLabel, GUILayout.Width(95f));
                if (GUILayout.Button("\u2715", GUILayout.Width(24f))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (EditorGUI.EndChangeCheck()) choicesChanged = true;

            if (removeAt >= 0)
            {
                string removedTarget = cp.GetArrayElementAtIndex(removeAt).FindPropertyRelative("targetId").stringValue;
                cp.DeleteArrayElementAtIndex(removeAt);
                if (cp.arraySize == 0) nextProp.stringValue = removedTarget;   // back to a plain linear frame
                choicesChanged = true;
            }

            if (GUILayout.Button("+ Add choice"))
            {
                string carry = "";
                if (cp.arraySize == 0)
                {
                    carry = nextProp.stringValue;   // keep the existing link as the first choice's target
                    nextProp.stringValue = "";
                }
                cp.InsertArrayElementAtIndex(cp.arraySize);
                var added = cp.GetArrayElementAtIndex(cp.arraySize - 1);
                added.FindPropertyRelative("text").stringValue = "Choice " + cp.arraySize;
                added.FindPropertyRelative("targetId").stringValue = carry ?? "";
                choicesChanged = true;
            }
        }

        static void Prop(SerializedProperty parent, string relative)
        {
            EditorGUILayout.PropertyField(parent.FindPropertyRelative(relative), true);
        }
    }
}

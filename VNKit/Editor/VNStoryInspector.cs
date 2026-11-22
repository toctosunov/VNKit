using UnityEditor;
using UnityEngine;
using VNKit;

namespace VNKitEditor
{
    [CustomEditor(typeof(VNStory))]
    public class VNStoryInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var s = (VNStory)target;
            EditorGUILayout.LabelField("Frames: " + s.nodes.Count + "    Clips: " + s.clips.Count);
            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Open Story Editor", GUILayout.Height(32f)))
                VNStoryEditorWindow.Open().SetStory(s);
        }
    }
}

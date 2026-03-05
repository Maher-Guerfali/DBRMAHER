using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayAnimationArm))]
public class PlayAnimationArmEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        var script = (PlayAnimationArm)target;
        if (GUILayout.Button("Trigger Once"))
        {
            script.Trigger();
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("You can click Trigger Once in edit mode, but it is mainly useful in Play Mode.", MessageType.Info);
        }
    }
}

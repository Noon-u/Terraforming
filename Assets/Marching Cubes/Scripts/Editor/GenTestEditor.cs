using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GenTest))]
public class GenTestEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		var genTest = (GenTest)target;

		EditorGUILayout.Space();
		using (new EditorGUI.DisabledScope(genTest.generateOnStart))
		{
			if (GUILayout.Button("Generate Now"))
			{
				genTest.GenerateNow();
			}
		}

		using (new EditorGUI.DisabledScope(false))
		{
			if (GUILayout.Button("Clear Generated"))
			{
				genTest.ClearGenerated();
			}
		}

		if (genTest.generateOnStart)
		{
			EditorGUILayout.HelpBox("Disable 'Generate On Start' to use 'Generate Now'.", MessageType.None);
		}
	}
}



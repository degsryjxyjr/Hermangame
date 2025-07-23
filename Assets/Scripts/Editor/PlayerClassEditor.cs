using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerClassDefinition))]
public class PlayerClassEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        // Draw default properties
        DrawPropertiesExcluding(serializedObject, "m_Script");
        
        // Add preview section
        var classDef = (PlayerClassDefinition)target;
        if (classDef.characterPrefab != null)
        {
            GUILayout.Space(10);
            GUILayout.Label("Class Preview", EditorStyles.boldLabel);
            Texture icon = AssetPreview.GetAssetPreview(classDef.characterPrefab);
            GUILayout.Label(icon, GUILayout.Height(100), GUILayout.Width(100));
        }
        
        serializedObject.ApplyModifiedProperties();
    }
}
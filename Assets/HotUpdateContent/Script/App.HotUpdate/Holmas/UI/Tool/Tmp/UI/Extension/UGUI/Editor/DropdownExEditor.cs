using UnityEngine.UI;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

[CustomEditor(typeof(DropdownEx), true)]
[CanEditMultipleObjects]
public class DropdownExEditor : DropdownEditor
{
    SerializedProperty m_IsAlwaysTrigger;
    SerializedProperty m_OnCreateDropdownListItem;

    protected override void OnEnable()
    {
        base.OnEnable();
        m_IsAlwaysTrigger = serializedObject.FindProperty("m_IsAlwaysTrigger");
        m_OnCreateDropdownListItem = serializedObject.FindProperty("m_OnCreateDropdownListItem");

        if (m_OnCreateDropdownListItem == null)
            Debug.Log("null");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        EditorGUILayout.PropertyField(m_IsAlwaysTrigger);
        EditorGUILayout.PropertyField(m_OnCreateDropdownListItem);
        serializedObject.ApplyModifiedProperties();
    }
}

using App.HotUpdate.Holmas.UI.Core;
using UnityEditor;
using UnityEngine;

namespace Holmas.Editor
{
    [CustomEditor(typeof(UiScenePrefabExportMarker))]
    public sealed class UiScenePrefabExportMarkerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("prefabName"), new GUIContent("Prefab Name"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("exportRootPath"), new GUIContent("Export Root Path"));

            serializedObject.ApplyModifiedProperties();

            UiScenePrefabExportMarker marker = (UiScenePrefabExportMarker)target;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Prefab Asset Path", marker.BuildPrefabAssetPath());
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("导出 Prefab"))
            {
                TryExport(marker);
            }
        }

        [MenuItem("GameObject/Holmas/UI/Export Prefab", false, 10)]
        private static void ExportSelectedUiRootPreview(MenuCommand command)
        {
            if (!TryGetMarkerFromCommand(command, out UiScenePrefabExportMarker marker))
            {
                Debug.LogError("当前选中对象缺少 UiScenePrefabExportMarker。");
                return;
            }

            TryExport(marker);
        }

        [MenuItem("GameObject/Holmas/UI/Export Prefab", true)]
        private static bool ValidateExportSelectedUiRootPreview(MenuCommand command)
        {
            return TryGetMarkerFromCommand(command, out _);
        }

        private static void TryExport(UiScenePrefabExportMarker marker)
        {
            try
            {
                UiScenePrefabExporter.Export(marker);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("UIRootPreview prefab export failed: " + ex.Message, marker);
            }
        }

        private static bool TryGetMarkerFromCommand(MenuCommand command, out UiScenePrefabExportMarker marker)
        {
            GameObject target = command != null && command.context is GameObject contextObject
                ? contextObject
                : Selection.activeGameObject;
            return UiScenePrefabExporter.TryGetMarker(target, out marker);
        }
    }
}

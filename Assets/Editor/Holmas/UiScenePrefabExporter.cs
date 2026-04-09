using System.IO;
using App.HotUpdate.Holmas.UI.Core;
using UnityEditor;
using UnityEngine;

namespace Holmas.Editor
{
    public sealed class UiScenePrefabExportRequest
    {
        public GameObject ExportRoot;
        public string PrefabName = string.Empty;
        public string ExportRootPath = UiScenePrefabExportMarker.DefaultManualUiPrefabRoot;
    }

    public static class UiScenePrefabExporter
    {
        private const string DebugLayerName = "DebugLayer";
        private const string DebugRootName = "DebugRoot";

        public static string BuildPrefabAssetPath(UiScenePrefabExportRequest request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            string exportRootPath = NormalizeAssetPath(request.ExportRootPath);
            string prefabName = string.IsNullOrWhiteSpace(request.PrefabName) ? string.Empty : request.PrefabName.Trim();
            if (string.IsNullOrWhiteSpace(exportRootPath) || string.IsNullOrWhiteSpace(prefabName))
            {
                return string.Empty;
            }

            return exportRootPath + "/" + prefabName + ".prefab";
        }

        public static void Export(UiScenePrefabExportMarker marker)
        {
            if (marker == null)
            {
                throw new System.InvalidOperationException("导出根不存在。");
            }

            Export(new UiScenePrefabExportRequest
            {
                ExportRoot = marker.gameObject,
                PrefabName = marker.PrefabName,
                ExportRootPath = marker.ExportRootPath,
            });
        }

        public static void Export(UiScenePrefabExportRequest request)
        {
            ValidateRequest(request);

            string assetPath = BuildPrefabAssetPath(request);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new System.InvalidOperationException("保存 prefab 失败：目标路径无效。");
            }

            EnsureAssetFolders(assetPath);
            GameObject exportCopy = null;
            try
            {
                exportCopy = BuildExportCopy(request.ExportRoot, request.PrefabName.Trim());
                StripEditorOnlyNodes(exportCopy);
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(exportCopy, assetPath);

                if (savedPrefab == null)
                {
                    throw new System.InvalidOperationException("保存 prefab 失败。");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("UIRootPreview prefab exported: " + assetPath, savedPrefab);
                EditorGUIUtility.PingObject(savedPrefab);
            }
            finally
            {
                if (exportCopy != null)
                {
                    Object.DestroyImmediate(exportCopy);
                }
            }
        }

        public static bool CanExportFromSelection()
        {
            return TryGetMarkerFromSelection(out _);
        }

        public static bool TryGetMarkerFromSelection(out UiScenePrefabExportMarker marker)
        {
            return TryGetMarker(Selection.activeGameObject, out marker);
        }

        public static bool TryGetMarker(GameObject candidate, out UiScenePrefabExportMarker marker)
        {
            marker = candidate != null ? candidate.GetComponent<UiScenePrefabExportMarker>() : null;
            return marker != null;
        }

        private static void ValidateRequest(UiScenePrefabExportRequest request)
        {
            if (request == null)
            {
                throw new System.InvalidOperationException("导出根不存在。");
            }

            if (request.ExportRoot == null)
            {
                throw new System.InvalidOperationException("导出根不存在。");
            }

            if (!string.Equals(request.ExportRoot.name, UiScenePrefabExportMarker.ExpectedExportRootName, System.StringComparison.Ordinal))
            {
                throw new System.InvalidOperationException("挂载对象不是 UIRootPreview。");
            }

            if (string.IsNullOrWhiteSpace(request.PrefabName))
            {
                throw new System.InvalidOperationException("prefabName 为空。");
            }

            if (request.ExportRoot.transform.childCount == 0)
            {
                throw new System.InvalidOperationException("导出根不存在有效内容。");
            }

            if (!HasExportableContent(request.ExportRoot.transform))
            {
                throw new System.InvalidOperationException("导出根不存在有效内容。");
            }
        }

        private static void StripEditorOnlyNodes(GameObject clonedRoot)
        {
            RemoveChildIfExists(clonedRoot.transform, DebugLayerName);
            RemoveChildIfExists(clonedRoot.transform, DebugRootName);

            UiScenePrefabExportMarker marker = clonedRoot.GetComponent<UiScenePrefabExportMarker>();
            if (marker != null)
            {
                Object.DestroyImmediate(marker, true);
            }
        }

        private static void RemoveChildIfExists(Transform rootTransform, string childName)
        {
            Transform child = rootTransform != null ? rootTransform.Find(childName) : null;
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static bool HasExportableContent(Transform rootTransform)
        {
            if (rootTransform == null)
            {
                return false;
            }

            for (int i = 0; i < rootTransform.childCount; i++)
            {
                Transform child = rootTransform.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child.name == DebugLayerName || child.name == DebugRootName)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static GameObject BuildExportCopy(GameObject sourceRoot, string exportedName)
        {
            if (sourceRoot == null)
            {
                throw new System.InvalidOperationException("导出根不存在。");
            }

            GameObject copyRoot = CreateMirroredObject(sourceRoot, exportedName);
            CopyHierarchy(sourceRoot.transform, copyRoot.transform);
            return copyRoot;
        }

        private static void CopyHierarchy(Transform sourceParent, Transform destinationParent)
        {
            for (int i = 0; i < sourceParent.childCount; i++)
            {
                Transform sourceChild = sourceParent.GetChild(i);
                if (sourceChild == null)
                {
                    continue;
                }

                GameObject destinationChild = CreateMirroredObject(sourceChild.gameObject, sourceChild.gameObject.name);
                destinationChild.transform.SetParent(destinationParent, false);
                CopyHierarchy(sourceChild, destinationChild.transform);
            }
        }

        private static GameObject CreateMirroredObject(GameObject sourceObject, string objectName)
        {
            bool useRectTransform = sourceObject.transform is RectTransform;
            var mirroredObject = new GameObject(objectName, useRectTransform ? typeof(RectTransform) : typeof(Transform));
            mirroredObject.layer = sourceObject.layer;
            mirroredObject.tag = sourceObject.tag;
            mirroredObject.SetActive(sourceObject.activeSelf);

            CopyTransform(sourceObject.transform, mirroredObject.transform);
            CopyComponents(sourceObject, mirroredObject);
            return mirroredObject;
        }

        private static void CopyTransform(Transform sourceTransform, Transform destinationTransform)
        {
            if (sourceTransform is RectTransform sourceRect && destinationTransform is RectTransform destinationRect)
            {
                EditorUtility.CopySerialized(sourceRect, destinationRect);
                return;
            }

            destinationTransform.localPosition = sourceTransform.localPosition;
            destinationTransform.localRotation = sourceTransform.localRotation;
            destinationTransform.localScale = sourceTransform.localScale;
        }

        private static void CopyComponents(GameObject sourceObject, GameObject destinationObject)
        {
            Component[] components = sourceObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component sourceComponent = components[i];
                if (sourceComponent == null)
                {
                    throw new System.InvalidOperationException("节点存在 Missing Script，无法导出: " + GetHierarchyPath(sourceObject.transform));
                }

                if (sourceComponent is Transform || sourceComponent is UiScenePrefabExportMarker)
                {
                    continue;
                }

                Component destinationComponent = destinationObject.AddComponent(sourceComponent.GetType());
                EditorUtility.CopySerialized(sourceComponent, destinationComponent);
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static void EnsureAssetFolders(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath);
            string normalizedDirectory = NormalizeAssetPath(directory);
            if (string.IsNullOrWhiteSpace(normalizedDirectory))
            {
                throw new System.InvalidOperationException("目录创建失败。");
            }

            string[] parts = normalizedDirectory.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new System.InvalidOperationException("目录创建失败。");
            }

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string createdGuid = AssetDatabase.CreateFolder(current, parts[i]);
                    if (string.IsNullOrWhiteSpace(createdGuid))
                    {
                        throw new System.InvalidOperationException("目录创建失败。");
                    }
                }

                current = next;
            }
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
        }
    }
}

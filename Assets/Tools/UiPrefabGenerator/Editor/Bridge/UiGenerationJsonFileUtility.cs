using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UiPrefabGenerator.Editor.Bridge
{
    public static class UiGenerationJsonFileUtility
    {
        public static bool Exists(string assetPath)
        {
            return File.Exists(UiGenerationDataPaths.ToAbsolutePath(assetPath));
        }

        public static void SaveJson<T>(string assetPath, T value)
        {
            string absolutePath = UiGenerationDataPaths.ToAbsolutePath(assetPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(absolutePath, JsonUtility.ToJson(value, true), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
        }

        public static bool TryLoadJson<T>(string assetPath, out T value)
        {
            value = default(T);
            if (!File.Exists(UiGenerationDataPaths.ToAbsolutePath(assetPath)))
            {
                return false;
            }

            string text = File.ReadAllText(UiGenerationDataPaths.ToAbsolutePath(assetPath));
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            value = JsonUtility.FromJson<T>(text);
            return value != null;
        }

        public static void SaveText(string assetPath, string text)
        {
            string absolutePath = UiGenerationDataPaths.ToAbsolutePath(assetPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(absolutePath, text ?? string.Empty, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
        }

        public static string LoadText(string assetPath)
        {
            string absolutePath = UiGenerationDataPaths.ToAbsolutePath(assetPath);
            return File.Exists(absolutePath) ? File.ReadAllText(absolutePath) : string.Empty;
        }
    }
}

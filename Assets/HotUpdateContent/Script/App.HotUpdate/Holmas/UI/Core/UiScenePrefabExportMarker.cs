using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Core
{
    [DisallowMultipleComponent]
    public sealed class UiScenePrefabExportMarker : MonoBehaviour
    {
        public const string DefaultManualUiPrefabRoot = "Assets/HotUpdateContent/Res/Perfabs/UI";
        public const string ExpectedExportRootName = "UIRootPreview";

        [SerializeField]
        private string prefabName = string.Empty;

        [SerializeField]
        private string exportRootPath = DefaultManualUiPrefabRoot;

        public string PrefabName
        {
            get => prefabName;
            set => prefabName = value ?? string.Empty;
        }

        public string ExportRootPath
        {
            get => string.IsNullOrWhiteSpace(exportRootPath) ? DefaultManualUiPrefabRoot : exportRootPath;
            set => exportRootPath = string.IsNullOrWhiteSpace(value) ? DefaultManualUiPrefabRoot : value;
        }

        public string BuildPrefabAssetPath()
        {
            string safeName = string.IsNullOrWhiteSpace(prefabName) ? string.Empty : prefabName.Trim();
            return string.IsNullOrWhiteSpace(safeName)
                ? ExportRootPath.TrimEnd('/')
                : ExportRootPath.TrimEnd('/') + "/" + safeName + ".prefab";
        }

        private void Reset()
        {
            exportRootPath = DefaultManualUiPrefabRoot;
        }
    }
}

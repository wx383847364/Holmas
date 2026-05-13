using App.HotUpdate.Holmas.UI.Binding;
using App.Shared.Contracts;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Core
{
    public sealed class UiLoadedPrefabHandle
    {
        public UiLoadedPrefabHandle(string assetAddress, IAssetHandle assetHandle, GameObject prefabAsset, GameObject instanceRoot)
        {
            AssetAddress = assetAddress ?? string.Empty;
            AssetHandle = assetHandle;
            PrefabAsset = prefabAsset;
            InstanceRoot = instanceRoot;
        }

        public string AssetAddress { get; }

        public IAssetHandle AssetHandle { get; }

        public GameObject PrefabAsset { get; }

        public GameObject InstanceRoot { get; }

        public bool IsPlaceholder => PrefabAsset == null;

        public UiReferenceCollector GetReferenceCollector()
        {
            if (InstanceRoot == null)
            {
                return null;
            }

            return InstanceRoot.GetComponent<UiReferenceCollector>();
        }

        public void SetParent(Transform parent)
        {
            if (InstanceRoot == null || parent == null)
            {
                return;
            }

            InstanceRoot.transform.SetParent(parent, false);
        }

        public void SetActive(bool isActive)
        {
            if (InstanceRoot != null)
            {
                InstanceRoot.SetActive(isActive);
            }
        }

        public void Release()
        {
            if (InstanceRoot != null)
            {
                Object.Destroy(InstanceRoot);
            }

            AssetHandle?.Release();
        }

        public static UiLoadedPrefabHandle CreatePlaceholder(string assetAddress)
        {
            string objectName = string.IsNullOrWhiteSpace(assetAddress) ? "UiPlaceholder" : assetAddress.Replace('/', '_');
            var instanceRoot = new GameObject(objectName, typeof(RectTransform));
            return new UiLoadedPrefabHandle(assetAddress, null, null, instanceRoot);
        }
    }
}

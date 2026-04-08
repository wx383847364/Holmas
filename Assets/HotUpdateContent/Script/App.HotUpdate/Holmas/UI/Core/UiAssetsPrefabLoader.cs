using System.Threading.Tasks;
using App.Shared.Contracts;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Core
{
    public sealed class UiAssetsPrefabLoader : IUiPrefabLoader
    {
        private readonly IAssetsRuntime _assetsRuntime;

        public UiAssetsPrefabLoader(IAssetsRuntime assetsRuntime)
        {
            _assetsRuntime = assetsRuntime;
        }

        public async Task<UiLoadedPrefabHandle> LoadAsync(string assetAddress)
        {
            if (_assetsRuntime == null || string.IsNullOrWhiteSpace(assetAddress))
            {
                return UiLoadedPrefabHandle.CreatePlaceholder(assetAddress);
            }

            IAssetHandle assetHandle = await _assetsRuntime.LoadAssetAsync(assetAddress);
            if (!(assetHandle?.AssetObject is GameObject prefab))
            {
                assetHandle?.Release();
                return UiLoadedPrefabHandle.CreatePlaceholder(assetAddress);
            }

            GameObject instanceRoot = Object.Instantiate(prefab);
            instanceRoot.name = prefab.name;
            return new UiLoadedPrefabHandle(assetAddress, assetHandle, prefab, instanceRoot);
        }

        public void Release(UiLoadedPrefabHandle handle)
        {
            handle?.Release();
        }
    }
}

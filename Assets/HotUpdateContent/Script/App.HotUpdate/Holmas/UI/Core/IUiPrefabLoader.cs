using System.Threading.Tasks;

namespace App.HotUpdate.Holmas.UI.Core
{
    public interface IUiPrefabLoader
    {
        Task<UiLoadedPrefabHandle> LoadAsync(string assetAddress);

        void Release(UiLoadedPrefabHandle handle);
    }
}

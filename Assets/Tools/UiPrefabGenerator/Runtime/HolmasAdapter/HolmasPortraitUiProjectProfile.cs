using System.Collections.Generic;
using UiPrefabGenerator.Core.Profile;

namespace UiPrefabGenerator.HolmasAdapter
{
    public sealed class HolmasPortraitUiProjectProfile : IProjectUiProfile
    {
        public string ProfileId
        {
            get { return "holmas_ugui_portrait"; }
        }

        public string DraftPrefabRoot
        {
            get { return "Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/Portrait"; }
        }

        public string RuntimeBindingNamespace
        {
            get { return "App.HotUpdate.Holmas.UI.Generated"; }
        }

        public IReadOnlyCollection<string> AllowedComponentTypes
        {
            get { return HolmasUiProfileShared.AllowedComponentTypes; }
        }

        public string BuildDraftPrefabPath(string prefabName)
        {
            return HolmasUiProfileShared.BuildDraftPrefabPath(DraftPrefabRoot, prefabName);
        }

        public bool IsDraftPrefabPathWithinAllowedRoot(string prefabDraftPath)
        {
            return HolmasUiProfileShared.IsDraftPrefabPathWithinAllowedRoot(prefabDraftPath, DraftPrefabRoot);
        }
    }
}

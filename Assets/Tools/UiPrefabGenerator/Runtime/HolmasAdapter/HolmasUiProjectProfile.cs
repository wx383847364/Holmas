using UiPrefabGenerator.Core.Profile;

namespace UiPrefabGenerator.HolmasAdapter
{
    public sealed class HolmasUiProjectProfile : IProjectUiProfile
    {
        public string ProfileId
        {
            get { return "holmas_ugui"; }
        }

        public string DraftPrefabRoot
        {
            get { return "Assets/Res/Perfabs/Generated/Holmas"; }
        }

        public string RuntimeBindingNamespace
        {
            get { return "App.HotUpdate.Holmas.UI.Generated"; }
        }

        public System.Collections.Generic.IReadOnlyCollection<string> AllowedComponentTypes
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

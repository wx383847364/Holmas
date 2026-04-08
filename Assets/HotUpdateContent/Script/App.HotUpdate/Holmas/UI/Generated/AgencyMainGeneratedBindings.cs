using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Screens.AgencyMain;

namespace App.HotUpdate.Holmas.UI.Generated
{
    /// <summary>
    /// AgencyMain 当前轮次的最小 generated 结果消费桥。
    /// 后续如果生成器开始直出业务侧运行时绑定文件，可直接替换这里而不改页面控制器。
    /// </summary>
    public static class AgencyMainGeneratedBindings
    {
        public const string ScreenId = "agency.main";
        public const string PrefabName = "AgencyMainPanel";
        public const string PrefabAssetPath = "Assets/Res/Perfabs/Generated/Holmas/Portrait/AgencyMainPanel.prefab";

        private static readonly UiBindingManifest ManifestInstance = BuildManifest();

        public static UiBindingManifest Manifest => ManifestInstance;

        private static UiBindingManifest BuildManifest()
        {
            var manifest = new UiBindingManifest(ScreenId, PrefabName, PrefabAssetPath);

            manifest.AddEntry(
                AgencyMainBindings.RootPanelKey,
                "RectTransform",
                AgencyMainBindings.RootNodePath,
                notes: "formal_root_panel");
            manifest.AddEntry(
                AgencyMainBindings.TitleTextKey,
                "Text",
                AgencyMainBindings.TitleTextNodePath,
                notes: "formal_title_text");
            manifest.AddEntry(
                AgencyMainBindings.SummaryTextKey,
                "Text",
                AgencyMainBindings.SummaryTextNodePath,
                notes: "formal_summary_text");
            manifest.AddEntry(
                AgencyMainBindings.TaskSummaryTextKey,
                "Text",
                AgencyMainBindings.TaskSummaryTextNodePath,
                notes: "formal_task_summary_text");
            manifest.AddEntry(
                AgencyMainBindings.BoardSummaryTextKey,
                "Text",
                AgencyMainBindings.BoardSummaryTextNodePath,
                notes: "formal_board_summary_text");
            manifest.AddEntry(
                AgencyMainBindings.StatusTextKey,
                "Text",
                AgencyMainBindings.StatusTextNodePath,
                notes: "formal_status_text");
            manifest.AddEntry(
                AgencyMainBindings.PrimaryActionButtonKey,
                "Button",
                AgencyMainBindings.PrimaryActionButtonNodePath,
                AgencyMainBindings.PrimaryActionButtonClickEvent,
                requiresManualWiring: true,
                notes: "controller_wires_on_click");

            return manifest;
        }
    }
}

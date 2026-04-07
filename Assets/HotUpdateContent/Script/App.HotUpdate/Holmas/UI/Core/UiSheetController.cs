namespace App.HotUpdate.Holmas.UI.Core
{
    /// <summary>
    /// Sheet 表达同页组内切换语义。
    /// 同一 SheetGroupId 下只允许一个激活项。
    /// </summary>
    public abstract class UiSheetController : UiScreenController
    {
        protected sealed override UiScreenKind DeclaredKind => UiScreenKind.Sheet;
    }
}

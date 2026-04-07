namespace App.HotUpdate.Holmas.UI.Core
{
    /// <summary>
    /// Page 表达主流程页面语义。
    /// 打开新的 Page 时，旧页面进入暂停态，并参与 Back 历史栈。
    /// </summary>
    public abstract class UiPageController : UiScreenController
    {
        protected sealed override UiScreenKind DeclaredKind => UiScreenKind.Page;
    }
}

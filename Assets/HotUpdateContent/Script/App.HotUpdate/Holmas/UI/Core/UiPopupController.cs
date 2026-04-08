namespace App.HotUpdate.Holmas.UI.Core
{
    /// <summary>
    /// Popup 表达模态弹窗语义。
    /// Popup 进入独立弹窗栈，不参与 Page 历史返回。
    /// </summary>
    public abstract class UiPopupController : UiScreenController
    {
        protected sealed override UiScreenKind DeclaredKind => UiScreenKind.Popup;
    }
}

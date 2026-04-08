namespace App.HotUpdate.Holmas.UI.Core
{
    /// <summary>
    /// Overlay 表达不进入导航栈的全局遮罩语义。
    /// 典型场景包括 loading、toast、引导遮罩等。
    /// </summary>
    public abstract class UiOverlayController : UiScreenController
    {
        protected sealed override UiScreenKind DeclaredKind => UiScreenKind.Overlay;
    }
}

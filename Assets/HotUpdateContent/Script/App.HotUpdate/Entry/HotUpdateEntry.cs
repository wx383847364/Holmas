using App.Shared.Contracts;
using App.HotUpdate.Holmas.Bootstrap;

namespace App.HotUpdate.Entry
{
    /// <summary>
    /// 热更层总入口。
    /// AOT 会通过反射调用这里的 Start 方法，HotUpdate 再把控制权交给 Holmas 的正式业务骨架。
    /// </summary>
    public static class HotUpdateEntry
    {
        /// <summary>
        /// 启动热更层。
        /// 这里保持入口签名稳定，避免影响 AOT 的反射加载逻辑。
        /// </summary>
        public static void Start(IServiceContainer serviceContainer)
        {
            var logger = serviceContainer?.Get<IAppLogger>();
            logger?.LogInfo("HotUpdateEntry: 热更层启动，准备进入 Holmas 业务骨架。");

            // 这轮只先冻结 DTO 与业务骨架入口，不在这里直接实现地图、任务和 UI 细节。
            HolmasGameBootstrap.Start(serviceContainer);

            logger?.LogInfo("HotUpdateEntry: Holmas 业务骨架接线完成。");
        }
    }
}

using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.UI.Core;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI
{
    /// <summary>
    /// 旧 HolmasUiRoot 的过渡兼容壳。
    /// v2 正式入口已经切到 Core/UiRoot；这里仅保留同名组件，避免外部旧引用立即失效。
    /// </summary>
    public sealed class HolmasUiRoot : MonoBehaviour
    {
        private HolmasApplicationContext _context;
        private IHolmasLevelLaunchGateway _levelLaunchGateway;

        public UiRoot RuntimeRoot { get; private set; }

        public HolmasApplicationContext Context => _context;

        public IHolmasLevelLaunchGateway LevelLaunchGateway => _levelLaunchGateway;

        public void InitializeCompatibility(HolmasApplicationContext context, IHolmasLevelLaunchGateway levelLaunchGateway, UiRoot runtimeRoot)
        {
            _context = context;
            _levelLaunchGateway = levelLaunchGateway;
            RuntimeRoot = runtimeRoot;
        }
    }
}

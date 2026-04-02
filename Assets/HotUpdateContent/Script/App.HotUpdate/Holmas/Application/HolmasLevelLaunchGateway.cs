using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Levels;

namespace App.HotUpdate.Holmas.Application
{
    /// <summary>
    /// Holmas 关卡启动门面。
    /// 这一层只负责把外部已经生成好的 LevelGenerationRequest 交给正式组合层，不负责解析配置。
    /// </summary>
    public interface IHolmasLevelLaunchGateway
    {
        Task<BoardRuntime> StartLevelAsync(LevelGenerationRequest request);
        Task<BoardRuntime> StartLevelForPlayerAsync(int playerLevel, int seed, IReadOnlyList<BoardSpawnEntry> catPool = null);
        Task<BoardRuntime> StartLevelForCurrentPlayerAsync(int seed, IReadOnlyList<BoardSpawnEntry> catPool = null);
    }

    /// <summary>
    /// Holmas 关卡启动门面实现。
    /// 保持为一个很薄的组合层入口，方便后续正式地图配置生成结果统一从这里进入。
    /// </summary>
    public sealed class HolmasLevelLaunchGateway : IHolmasLevelLaunchGateway
    {
        private readonly HolmasApplicationContext _context;
        private readonly HolmasLevelRequestGenerator _requestGenerator;

        public HolmasLevelLaunchGateway(HolmasApplicationContext context, HolmasLevelRequestGenerator requestGenerator = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _requestGenerator = requestGenerator;
        }

        public Task<BoardRuntime> StartLevelAsync(LevelGenerationRequest request)
        {
            return _context.StartLevelAsync(request);
        }

        public Task<BoardRuntime> StartLevelForPlayerAsync(int playerLevel, int seed, IReadOnlyList<BoardSpawnEntry> catPool = null)
        {
            if (_requestGenerator == null)
            {
                throw new InvalidOperationException("HolmasLevelLaunchGateway: 当前没有可用的关卡请求生成器。");
            }

            LevelGenerationRequest request = _requestGenerator.GenerateForPlayerLevel(playerLevel, seed, catPool);
            return StartLevelAsync(request);
        }

        public Task<BoardRuntime> StartLevelForCurrentPlayerAsync(int seed, IReadOnlyList<BoardSpawnEntry> catPool = null)
        {
            if (_context == null)
            {
                throw new InvalidOperationException("HolmasLevelLaunchGateway: 当前没有可用的应用上下文。");
            }

            return StartLevelForPlayerAsync(_context.CurrentPlayerLevel, seed, catPool);
        }
    }
}

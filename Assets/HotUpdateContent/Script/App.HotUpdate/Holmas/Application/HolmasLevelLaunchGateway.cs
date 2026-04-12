using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Levels;
using App.HotUpdate.Holmas.Tasks.Config;

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

            IReadOnlyList<BoardSpawnEntry> effectiveCatPool = ResolveCatPoolForLaunch(playerLevel, catPool);
            if (effectiveCatPool == null || effectiveCatPool.Count == 0)
            {
                throw new InvalidOperationException("HolmasLevelLaunchGateway: 当前任务栏没有可用于地图生成的猫种池。");
            }

            LevelGenerationRequest request = _requestGenerator.GenerateForPlayerLevel(playerLevel, seed, effectiveCatPool);
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

        private IReadOnlyList<BoardSpawnEntry> ResolveCatPoolForLaunch(int playerLevel, IReadOnlyList<BoardSpawnEntry> explicitCatPool)
        {
            IReadOnlyList<BoardSpawnEntry> normalizedExplicit = NormalizeCatPool(explicitCatPool);
            if (normalizedExplicit.Count > 0)
            {
                return normalizedExplicit;
            }

            _context.RefillAvailableTasks();

            if (_context.GameplayRuntime == null || _context.GameplayRuntime.TaskBarState == null)
            {
                return Array.Empty<BoardSpawnEntry>();
            }

            IReadOnlyCollection<string> activeCatIds = _context.GameplayRuntime.TaskBarState.GetActiveCatIds();
            if (activeCatIds == null || activeCatIds.Count == 0)
            {
                return Array.Empty<BoardSpawnEntry>();
            }

            IHolmasTaskCatalog taskCatalog = _context.ServiceContainer != null
                ? _context.ServiceContainer.Get<IHolmasTaskCatalog>()
                : null;

            return activeCatIds
                .Where(catId => !string.IsNullOrWhiteSpace(catId))
                .Select(catId => new BoardSpawnEntry
                {
                    CatId = catId,
                    Weight = ResolveCatWeight(taskCatalog, catId),
                })
                .ToList();
        }

        private static IReadOnlyList<BoardSpawnEntry> NormalizeCatPool(IReadOnlyList<BoardSpawnEntry> catPool)
        {
            if (catPool == null || catPool.Count == 0)
            {
                return Array.Empty<BoardSpawnEntry>();
            }

            return catPool
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.CatId) && entry.Weight > 0)
                .Select(entry => new BoardSpawnEntry
                {
                    CatId = entry.CatId,
                    Weight = entry.Weight,
                })
                .ToList();
        }

        private static int ResolveCatWeight(IHolmasTaskCatalog taskCatalog, string catId)
        {
            if (taskCatalog != null &&
                taskCatalog.TryGetCat(catId, out HolmasCatDefinition catDefinition) &&
                catDefinition != null &&
                catDefinition.Weight > 0)
            {
                return catDefinition.Weight;
            }

            return 1;
        }
    }
}

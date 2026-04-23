using System;
using System.Collections.Generic;
using System.Linq;
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

            IReadOnlyList<BoardSpawnEntry> effectiveCatPool = ResolveCatPoolForLaunch(catPool);
            if (effectiveCatPool == null || effectiveCatPool.Count == 0)
            {
                throw new InvalidOperationException("HolmasLevelLaunchGateway: 当前没有可用于启动地图的任务猫。");
            }

            LevelGenerationRequest request = _requestGenerator.GenerateForPlayerLevel(playerLevel, seed);
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

        private IReadOnlyList<BoardSpawnEntry> ResolveCatPoolForLaunch(IReadOnlyList<BoardSpawnEntry> explicitCatPool)
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

            var taskBar = _context.GameplayRuntime.TaskBarState;
            if (taskBar.Tasks == null || taskBar.Tasks.Count == 0)
            {
                return Array.Empty<BoardSpawnEntry>();
            }

            var entries = new List<BoardSpawnEntry>();
            var seenCatIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < taskBar.Tasks.Count; i++)
            {
                var runtimeTask = taskBar.Tasks[i];
                if (runtimeTask == null ||
                    runtimeTask.Task == null ||
                    runtimeTask.IsRewardClaimed ||
                    runtimeTask.Task.CurrentCount >= runtimeTask.Task.TargetCount ||
                    string.IsNullOrWhiteSpace(runtimeTask.Task.CatId) ||
                    !seenCatIds.Add(runtimeTask.Task.CatId))
                {
                    continue;
                }

                entries.Add(new BoardSpawnEntry
                {
                    CatId = runtimeTask.Task.CatId,
                    Weight = 1,
                });
            }

            return entries;
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
    }
}

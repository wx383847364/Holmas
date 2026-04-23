using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Config;
using App.Shared.Contracts;

namespace App.HotUpdate.Holmas.Levels
{
    /// <summary>
    /// 单次地图请求生成结果。
    /// </summary>
    [Serializable]
    public sealed class HolmasLevelRequestGenerationResult
    {
        public bool Success;
        public string FailureReason = string.Empty;
        public string SelectedMapId = string.Empty;
        public LevelGenerationRequest Request;
    }

    /// <summary>
    /// 由等级配置和地图配置生成正式 LevelGenerationRequest 的服务。
    /// </summary>
    public sealed class HolmasLevelRequestGenerator
    {
        private readonly IHolmasTaskCatalog _taskCatalog;
        private readonly IHolmasMapCatalog _mapCatalog;
        private readonly IHolmasRandomSource _randomSource;

        public HolmasLevelRequestGenerator(
            IHolmasTaskCatalog taskCatalog,
            IHolmasMapCatalog mapCatalog,
            IHolmasRandomSource randomSource)
        {
            _taskCatalog = taskCatalog ?? throw new ArgumentNullException(nameof(taskCatalog));
            _mapCatalog = mapCatalog ?? throw new ArgumentNullException(nameof(mapCatalog));
            _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        }

        public HolmasLevelRequestGenerationResult TryGenerateForPlayerLevel(
            int playerLevel,
            int seed,
            IReadOnlyList<BoardSpawnEntry> catPool = null)
        {
            var result = new HolmasLevelRequestGenerationResult();
            if (!_taskCatalog.TryGetPlayerLevel(playerLevel, out var levelDefinition) || levelDefinition == null)
            {
                return Fail("找不到玩家等级配置。");
            }

            if (!TryBuildMapCandidates(levelDefinition, out var candidates, out var failureReason))
            {
                return Fail(failureReason);
            }

            if (candidates.Count == 0)
            {
                return Fail("当前等级没有合法地图配置。");
            }

            int pickedIndex = HolmasWeightedPicker.PickIndex(candidates.Select(item => item.Weight).ToArray(), _randomSource);
            if (pickedIndex < 0 || pickedIndex >= candidates.Count)
            {
                return Fail("当前等级没有合法地图配置。");
            }

            MapCandidate selected = candidates[pickedIndex];
            result.Success = true;
            result.SelectedMapId = selected.Definition.MapId;
            result.Request = new LevelGenerationRequest
            {
                MapId = selected.Definition.MapId,
                TerrainPath = App.HotUpdate.Holmas.Terrain.HolmasTerrainAssetPathUtility.NormalizeStoredTerrainPath(selected.Definition.TerrainPath),
                Seed = seed,
                CatCountMin = selected.Definition.CatCountMin,
                CatCountMax = selected.Definition.CatCountMax,
                CatPool = Array.Empty<BoardSpawnEntry>(),
            };
            return result;
        }

        public LevelGenerationRequest GenerateForPlayerLevel(
            int playerLevel,
            int seed,
            IReadOnlyList<BoardSpawnEntry> catPool = null)
        {
            HolmasLevelRequestGenerationResult result = TryGenerateForPlayerLevel(playerLevel, seed, catPool);
            if (!result.Success)
            {
                throw new InvalidOperationException(result.FailureReason);
            }

            return result.Request;
        }

        private bool TryBuildMapCandidates(
            HolmasPlayerLevelDefinition levelDefinition,
            out List<MapCandidate> candidates,
            out string failureReason)
        {
            candidates = new List<MapCandidate>();
            failureReason = string.Empty;

            if (levelDefinition == null)
            {
                failureReason = "玩家等级配置为空。";
                return false;
            }

            string[] mapIds = levelDefinition.MapIds ?? Array.Empty<string>();
            int[] mapWeights = levelDefinition.MapWeights ?? Array.Empty<int>();

            if (mapIds.Length == 0 || mapWeights.Length == 0)
            {
                failureReason = "玩家等级配置缺少可用地图。";
                return false;
            }

            if (mapIds.Length != mapWeights.Length)
            {
                failureReason = "玩家等级配置的地图标识和权重长度不一致。";
                return false;
            }

            for (int i = 0; i < mapIds.Length; i++)
            {
                string mapId = mapIds[i] ?? string.Empty;
                int weight = mapWeights[i];

                if (string.IsNullOrWhiteSpace(mapId))
                {
                    failureReason = "玩家等级配置存在空的 MapId。";
                    return false;
                }

                if (weight < 0)
                {
                    failureReason = $"地图权重不能为负: {mapId}。";
                    return false;
                }

                if (weight == 0)
                {
                    continue;
                }

                if (!_mapCatalog.TryGetMap(mapId, out var mapDefinition) || mapDefinition == null)
                {
                    failureReason = $"找不到地图配置: {mapId}。";
                    return false;
                }

                if (!string.Equals(mapDefinition.MapId, mapId, StringComparison.Ordinal))
                {
                    failureReason = $"地图配置标识不一致: {mapId}。";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(mapDefinition.TerrainPath))
                {
                    failureReason = $"地图配置缺少 TerrainPath: {mapId}。";
                    return false;
                }

                if (mapDefinition.CatCountMin < 0 || mapDefinition.CatCountMax < mapDefinition.CatCountMin)
                {
                    failureReason = $"地图配置的猫数范围非法: {mapId}。";
                    return false;
                }

                candidates.Add(new MapCandidate(mapDefinition, weight));
            }

            if (candidates.Count == 0)
            {
                failureReason = "当前等级没有合法地图配置。";
                return false;
            }

            return true;
        }

        private static HolmasLevelRequestGenerationResult Fail(string reason)
        {
            return new HolmasLevelRequestGenerationResult
            {
                Success = false,
                FailureReason = reason ?? string.Empty,
            };
        }

        private sealed class MapCandidate
        {
            public MapCandidate(HolmasMapDefinition definition, int weight)
            {
                Definition = definition;
                Weight = weight;
            }

            public HolmasMapDefinition Definition { get; }

            public int Weight { get; }
        }
    }
}

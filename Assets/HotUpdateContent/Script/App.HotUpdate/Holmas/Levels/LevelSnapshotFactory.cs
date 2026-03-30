using System;
using System.Collections.Generic;
using App.HotUpdate.Holmas.Terrain;
using App.Shared.Contracts;
using App.Shared.Holmas.RuntimeData;
using UnityEngine;
using TerrainAssetPathUtility = App.HotUpdate.Holmas.Terrain.HolmasTerrainAssetPathUtility;

namespace App.HotUpdate.Holmas.Levels
{
    /// <summary>
    /// 将地形模板和配置输入转成单局运行时快照。
    /// </summary>
    public static class LevelSnapshotFactory
    {
        /// <summary>
        /// 先把地形转换成模板，再生成运行时快照。
        /// </summary>
        public static LevelSnapshot CreateFromTerrain(UnityEngine.Object terrainAsset, LevelGenerationRequest request)
        {
            BoardTemplate template = TerrainBoardTemplateConverter.Convert(terrainAsset);
            return Create(template, request);
        }

        /// <summary>
        /// 先通过资源服务加载地形，再把地形转换成模板并生成运行时快照。
        /// </summary>
        public static async System.Threading.Tasks.Task<LevelSnapshot> CreateFromTerrainAsync(IAssetsRuntime assetsRuntime, LevelGenerationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            BoardTemplate template = await HolmasTerrainAssetLoader.LoadBoardTemplateAsync(assetsRuntime, request.TerrainPath);
            return Create(template, request);
        }

        /// <summary>
        /// 直接基于棋盘模板生成运行时快照。
        /// </summary>
        public static LevelSnapshot Create(BoardTemplate template, LevelGenerationRequest request)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            int cellCount = template.Rows * template.Cols;
            var validCellIndices = CollectValidCellIndices(template);
            var spawnPool = NormalizePool(request.CatPool);
            var rng = new System.Random(request.Seed);

            int requestedMin = Math.Max(0, request.CatCountMin);
            int requestedMax = Math.Max(requestedMin, request.CatCountMax);
            int maxByBoard = validCellIndices.Count;

            if (spawnPool.Count == 0 || maxByBoard == 0)
            {
                requestedMin = 0;
                requestedMax = 0;
            }
            else
            {
                requestedMax = Math.Min(requestedMax, maxByBoard);
                requestedMin = Math.Min(requestedMin, requestedMax);
            }

            int catCount = requestedMax == 0 ? 0 : rng.Next(requestedMin, requestedMax + 1);
            var spawnedCats = new List<SpawnedCatData>(catCount);
            var availableCells = new List<int>(validCellIndices);

            for (int i = 0; i < catCount && availableCells.Count > 0; i++)
            {
                int cellIndex = TakeRandomCell(availableCells, rng);
                string catId = PickWeightedCatId(spawnPool, rng);

                if (string.IsNullOrWhiteSpace(catId))
                {
                    break;
                }

                spawnedCats.Add(new SpawnedCatData
                {
                    CatId = catId,
                    CellIndex = cellIndex,
                });
            }

            spawnedCats.Sort((left, right) => left.CellIndex.CompareTo(right.CellIndex));

            return new LevelSnapshot
            {
                MapId = request.MapId ?? string.Empty,
                TerrainPath = TerrainAssetPathUtility.NormalizeStoredTerrainPath(request.TerrainPath),
                Seed = request.Seed,
                SpawnedCats = spawnedCats,
                RevealedCells = new bool[cellCount],
                Completed = spawnedCats.Count == 0,
            };
        }

        private static List<int> CollectValidCellIndices(BoardTemplate template)
        {
            int cellCount = template.Rows * template.Cols;
            var validCellIndices = new List<int>(cellCount);
            bool[] validMask = template.ValidMask ?? Array.Empty<bool>();

            for (int i = 0; i < cellCount; i++)
            {
                if (i < validMask.Length && validMask[i])
                {
                    validCellIndices.Add(i);
                }
            }

            return validCellIndices;
        }

        private static List<BoardSpawnEntry> NormalizePool(IReadOnlyList<BoardSpawnEntry> pool)
        {
            var normalized = new List<BoardSpawnEntry>();
            if (pool == null)
            {
                return normalized;
            }

            for (int i = 0; i < pool.Count; i++)
            {
                BoardSpawnEntry entry = pool[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.CatId) || entry.Weight <= 0)
                {
                    continue;
                }

                normalized.Add(entry);
            }

            return normalized;
        }

        private static int TakeRandomCell(IList<int> availableCells, System.Random rng)
        {
            int index = rng.Next(availableCells.Count);
            int cellIndex = availableCells[index];
            availableCells[index] = availableCells[availableCells.Count - 1];
            availableCells.RemoveAt(availableCells.Count - 1);
            return cellIndex;
        }

        private static string PickWeightedCatId(IReadOnlyList<BoardSpawnEntry> pool, System.Random rng)
        {
            if (pool == null || pool.Count == 0)
            {
                return string.Empty;
            }

            int totalWeight = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                totalWeight += Math.Max(0, pool[i].Weight);
            }

            if (totalWeight <= 0)
            {
                return pool[0].CatId ?? string.Empty;
            }

            int roll = rng.Next(totalWeight);
            int cumulative = 0;

            for (int i = 0; i < pool.Count; i++)
            {
                cumulative += Math.Max(0, pool[i].Weight);
                if (roll < cumulative)
                {
                    return pool[i].CatId ?? string.Empty;
                }
            }

            return pool[pool.Count - 1].CatId ?? string.Empty;
        }
    }
}

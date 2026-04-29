using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Terrain;
using App.Shared.Contracts;
using App.Shared.Holmas.RuntimeData;

namespace App.HotUpdate.Holmas.Tutorial
{
    public static class CoreFindCatTutorialBoardDefinition
    {
        public const string MapId = "tutorial_core_find_cat_v1";
        public const string TerrainPath = "Assets/HotUpdateContent/Res/Map/11-8-8.asset";
        public const int FirstCatCellIndex = 27;
        public const int SecondCatCellIndex = 44;
        public const int Seed = 0;

        public static readonly int[] FixedCatCellIndices =
        {
            FirstCatCellIndex,
            SecondCatCellIndex,
        };
    }

    public sealed class CoreFindCatTutorialLevelService
    {
        private static readonly string[] DemoCatIds =
        {
            "tutorial-cat-a",
            "tutorial-cat-b",
        };

        public static async Task<CoreFindCatTutorialSession> CreateTutorialSessionAsync(IAssetsRuntime assetsRuntime)
        {
            if (assetsRuntime == null)
            {
                throw new InvalidOperationException("CoreFindCatTutorialLevelService: 资源运行时不可用。");
            }

            BoardTemplate template = await HolmasTerrainAssetLoader.LoadBoardTemplateAsync(
                assetsRuntime,
                CoreFindCatTutorialBoardDefinition.TerrainPath);
            ValidateFixedCells(template);
            LevelSnapshot snapshot = CreateTutorialSnapshot(template, DemoCatIds);
            return new CoreFindCatTutorialSession(template, snapshot);
        }

        public static bool IsTutorialLevel(LevelSnapshot snapshot)
        {
            return snapshot != null &&
                   string.Equals(snapshot.MapId, CoreFindCatTutorialBoardDefinition.MapId, StringComparison.Ordinal);
        }

        public static LevelSnapshot CreateTutorialSnapshot(BoardTemplate template, IReadOnlyList<string> catIds)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            ValidateFixedCells(template);
            string firstCatId = ResolveCatId(catIds, 0);
            string secondCatId = ResolveCatId(catIds, 1);
            int cellCount = Math.Max(0, template.Rows * template.Cols);

            return new LevelSnapshot
            {
                MapId = CoreFindCatTutorialBoardDefinition.MapId,
                TerrainPath = HolmasTerrainAssetPathUtility.NormalizeStoredTerrainPath(CoreFindCatTutorialBoardDefinition.TerrainPath),
                Seed = CoreFindCatTutorialBoardDefinition.Seed,
                RevealedCells = new bool[cellCount],
                Completed = false,
                SpawnedCats = new List<SpawnedCatData>
                {
                    new SpawnedCatData
                    {
                        CatId = firstCatId,
                        CellIndex = CoreFindCatTutorialBoardDefinition.FirstCatCellIndex,
                    },
                    new SpawnedCatData
                    {
                        CatId = secondCatId,
                        CellIndex = CoreFindCatTutorialBoardDefinition.SecondCatCellIndex,
                    },
                },
            };
        }

        private static string ResolveCatId(IReadOnlyList<string> catIds, int index)
        {
            if (catIds == null || catIds.Count == 0)
            {
                throw new InvalidOperationException("CoreFindCatTutorialLevelService: 教程猫池为空。");
            }

            int safeIndex = Math.Min(Math.Max(0, index), catIds.Count - 1);
            string catId = catIds[safeIndex];
            if (string.IsNullOrWhiteSpace(catId))
            {
                catId = catIds.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
            }

            if (string.IsNullOrWhiteSpace(catId))
            {
                throw new InvalidOperationException("CoreFindCatTutorialLevelService: 教程猫池没有有效 catId。");
            }

            return catId;
        }

        private static void ValidateFixedCells(BoardTemplate template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            int cellCount = Math.Max(0, template.Rows * template.Cols);
            bool[] validMask = template.ValidMask ?? Array.Empty<bool>();
            for (int i = 0; i < CoreFindCatTutorialBoardDefinition.FixedCatCellIndices.Length; i++)
            {
                int cellIndex = CoreFindCatTutorialBoardDefinition.FixedCatCellIndices[i];
                if (cellIndex < 0 ||
                    cellIndex >= cellCount ||
                    cellIndex >= validMask.Length ||
                    !validMask[cellIndex])
                {
                    throw new InvalidOperationException(
                        $"CoreFindCatTutorialLevelService: 教程固定格 {cellIndex} 不是有效地形格。");
                }
            }
        }
    }
}

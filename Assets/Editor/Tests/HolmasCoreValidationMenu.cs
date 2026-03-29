using System;
using System.Collections.Generic;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Levels;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Config;
using App.HotUpdate.Holmas.Tasks.Runtime;
using App.HotUpdate.Holmas.Tasks.Services;
using App.Shared.Contracts;
using App.Shared.Holmas.RuntimeData;
using UnityEditor;
using UnityEngine;

public static class HolmasCoreValidationMenu
{
    [MenuItem("Holmas/Validation/Run Core Logic Smoke Test")]
    public static void RunCoreLogicSmokeTest()
    {
        var catalog = CreateCatalog();
        var taskService = new HolmasTaskProgressService(
            catalog,
            new ScriptedRandomSource(0, 0, 1, 0, 1, 1),
            new FixedUtcClock { UtcNowMilliseconds = 1000 });
        var metaService = new HolmasMetaProgressionService(
            CreateMetaCatalog(),
            new HolmasDefaultMetaExperienceSource(),
            new HolmasDefaultMetaExperienceSource());
        var coordinator = new HolmasProgressionCoordinator(taskService, metaService);
        var runtime = new HolmasGameplayRuntime(taskService, metaService, coordinator, new NullLogger());

        runtime.RefillAvailableTasks(1);

        var template = CreateBoardTemplate(1, 1);
        var snapshot = new LevelSnapshot
        {
            MapId = "validation-map",
            TerrainPath = "validation://terrain",
            Seed = 1,
            SpawnedCats = new List<SpawnedCatData>
            {
                new SpawnedCatData
                {
                    CatId = "cat-a",
                    CellIndex = 0,
                }
            },
            RevealedCells = new bool[1],
        };

        runtime.StartLevel(template, snapshot);
        var reveal = runtime.RevealCell(0, out HolmasProgressionAdvanceResult progressionResult);
        var claim = runtime.ClaimTaskReward(0, 1);

        Debug.Log($"Holmas smoke test passed. revealCompleted={reveal.Completed}, taskProgressed={progressionResult?.ProgressedTaskIds.Count ?? 0}, metaExp={runtime.MetaProgressionState.Experience}, taskClaimSuccess={claim.Success}, agencyLevel={runtime.MetaProgressionState.AgencyLevel}");
    }

    private static HolmasTaskCatalog CreateCatalog()
    {
        return new HolmasTaskCatalog(
            new[]
            {
                new HolmasCatDefinition { CatId = "cat-a", Price = 10 },
                new HolmasCatDefinition { CatId = "cat-b", Price = 20 },
            },
            new[]
            {
                new HolmasTaskTemplateDefinition
                {
                    TaskTypeId = "task-normal",
                    CatIdList = new[] { "cat-a", "cat-b" },
                    CountMin = 1,
                    CountMax = 1,
                    RewardArray = Array.Empty<string>(),
                    LevelRewardFactor = 2f,
                }
            },
            new[]
            {
                new HolmasPlayerLevelDefinition
                {
                    PlayerLevel = 1,
                    UpgradeExp = 0,
                    TaskTypeIds = new[] { "task-normal" },
                    TaskTypeWeights = new[] { 1 },
                    MapIds = new[] { "map-1" },
                    MapWeights = new[] { 1 },
                }
            });
    }

    private static HolmasMetaCatalog CreateMetaCatalog()
    {
        return new HolmasMetaCatalog(
            new[]
            {
                new HolmasMetaProgressionDefinition
                {
                    AgencyLevel = 1,
                    MinExperience = 0,
                },
                new HolmasMetaProgressionDefinition
                {
                    AgencyLevel = 2,
                    MinExperience = 5,
                }
            });
    }

    private static BoardTemplate CreateBoardTemplate(int rows, int cols)
    {
        var validMask = new bool[rows * cols];
        var blockColors = new Color32[rows * cols];
        for (int i = 0; i < validMask.Length; i++)
        {
            validMask[i] = true;
            blockColors[i] = new Color32(180, 180, 180, 255);
        }

        return new BoardTemplate
        {
            Rows = rows,
            Cols = cols,
            ValidMask = validMask,
            BlockColors = blockColors,
        };
    }

    private sealed class ScriptedRandomSource : IHolmasRandomSource
    {
        private readonly Queue<int> _scriptedInts;

        public ScriptedRandomSource(params int[] scriptedInts)
        {
            _scriptedInts = new Queue<int>(scriptedInts ?? Array.Empty<int>());
        }

        public int Next(int maxExclusive)
        {
            return Next(0, maxExclusive);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            int range = maxExclusive - minInclusive;
            int value = _scriptedInts.Count > 0 ? _scriptedInts.Dequeue() : 0;
            value = Math.Abs(value);
            return minInclusive + (value % range);
        }

        public double NextDouble()
        {
            return 0d;
        }
    }

    private sealed class FixedUtcClock : IHolmasUtcClock
    {
        public long UtcNowMilliseconds { get; set; }
    }

    private sealed class NullLogger : IAppLogger
    {
        public void Log(LogLevel level, string message, params object[] args)
        {
        }

        public void LogDebug(string message, params object[] args)
        {
        }

        public void LogInfo(string message, params object[] args)
        {
        }

        public void LogWarning(string message, params object[] args)
        {
        }

        public void LogError(string message, params object[] args)
        {
        }
    }
}

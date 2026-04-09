using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using App.HotUpdate.Holmas.Levels;
using App.HotUpdate.Holmas.Meta;
using App.HotUpdate.Holmas.Progression;
using App.HotUpdate.Holmas.Tasks.Config;
using App.Shared.Contracts;
using App.Shared.Holmas.RuntimeData;
using UnityEngine;

namespace Holmas.Tests
{
    internal static class HolmasTestSupport
    {
        public static UnityEngine.Object CreateTerrain(int rows, int cols, Func<int, int, bool> isValid = null, Func<int, int, Color32> colorFactory = null)
        {
            var terrain = CreateTerrainAsset();
            InvokeVoid(terrain, "Resize", rows, cols);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    bool valid = isValid == null ? true : isValid(row, col);
                    Color32 color = colorFactory == null ? DefaultColor(row, col) : colorFactory(row, col);
                    InvokeVoid(terrain, "SetValid", row, col, valid);
                    InvokeVoid(terrain, "SetColor", row, col, color);
                }
            }

            return terrain;
        }

        public static BoardTemplate CreateBoardTemplate(int rows, int cols, Func<int, int, bool> isValid = null, Func<int, int, Color32> colorFactory = null)
        {
            var validMask = new bool[rows * cols];
            var blockColors = new Color32[rows * cols];

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int cellIndex = row * cols + col;
                    validMask[cellIndex] = isValid == null ? true : isValid(row, col);
                    blockColors[cellIndex] = colorFactory == null ? DefaultColor(row, col) : colorFactory(row, col);
                }
            }

            return new BoardTemplate
            {
                Rows = rows,
                Cols = cols,
                ValidMask = validMask,
                BlockColors = blockColors,
            };
        }

        public static LevelGenerationRequest CreateRequest(
            string mapId,
            string terrainPath,
            int seed,
            int catCountMin,
            int catCountMax,
            params BoardSpawnEntry[] catPool)
        {
            return new LevelGenerationRequest
            {
                MapId = mapId,
                TerrainPath = terrainPath,
                Seed = seed,
                CatCountMin = catCountMin,
                CatCountMax = catCountMax,
                CatPool = catPool ?? Array.Empty<BoardSpawnEntry>(),
            };
        }

        public static HolmasTaskCatalog CreateStandardTaskCatalog()
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

        public static HolmasMetaCatalog CreateMetaCatalog()
        {
            return new HolmasMetaCatalog(
                new[]
                {
                new HolmasMetaProgressionDefinition
                {
                    PlayerLevel = 1,
                    MinExperience = 0,
                    OfflineRewardPerHour = 6,
                    AdUnlockHours = 24,
                },
                new HolmasMetaProgressionDefinition
                {
                    PlayerLevel = 2,
                    MinExperience = 100,
                    OfflineRewardPerHour = 8,
                    AdUnlockHours = 24,
                }
            });
        }

        public static T ResolveTerrainType<T>(string simpleName) where T : class
        {
            var type = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(GetTypesSafe)
                .FirstOrDefault(candidate => string.Equals(candidate.Name, simpleName, StringComparison.Ordinal));

            if (type == null)
            {
                throw new InvalidOperationException($"Could not resolve type '{simpleName}' from loaded assemblies.");
            }

            return ScriptableObject.CreateInstance(type) as T;
        }

        public static void InvokeVoid(object target, string methodName, params object[] arguments)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException($"Could not find method '{methodName}' on '{target.GetType().FullName}'.");
            }

            method.Invoke(target, arguments);
        }

        private static ScriptableObject CreateTerrainAsset()
        {
            var terrain = ResolveTerrainType<ScriptableObject>("MinesweeperTerrainData");
            if (terrain == null)
            {
                throw new InvalidOperationException("MinesweeperTerrainData was not found or could not be instantiated.");
            }

            return terrain;
        }

        private static IEnumerable<Type> GetTypesSafe(Assembly assembly)
        {
            if (assembly == null)
            {
                yield break;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            if (types == null)
            {
                yield break;
            }

            foreach (var type in types)
            {
                if (type != null)
                {
                    yield return type;
                }
            }
        }

        private static Color32 DefaultColor(int row, int col)
        {
            byte r = (byte)(160 + (row * 11) % 60);
            byte g = (byte)(160 + (col * 13) % 60);
            byte b = 180;
            return new Color32(r, g, b, 255);
        }
    }

    internal sealed class ScriptedRandomSource : IHolmasRandomSource
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

    internal sealed class FixedUtcClock : IHolmasUtcClock
    {
        public long UtcNowMilliseconds { get; set; }
    }

    internal sealed class NullLogger : IAppLogger
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

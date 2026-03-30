using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Levels;
using App.HotUpdate.Holmas.Terrain;
using App.Shared.Holmas.RuntimeData;
using NUnit.Framework;
using UnityEngine;

namespace Holmas.Tests
{
    public sealed class HolmasLevelAndBoardTests
    {
        [Test]
        public void LevelSnapshotFactory_ClampsCatCountToValidCells()
        {
            var terrain = HolmasTestSupport.CreateTerrain(2, 2, (row, col) => row == 0);
            var request = HolmasTestSupport.CreateRequest(
                "map-1",
                "terrain://map-1",
                7,
                3,
                3,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            var snapshot = LevelSnapshotFactory.CreateFromTerrain(terrain, request);

            Assert.That(snapshot.MapId, Is.EqualTo("map-1"));
            Assert.That(snapshot.TerrainPath, Is.EqualTo("terrain://map-1"));
            Assert.That(snapshot.RevealedCells, Has.Length.EqualTo(4));
            Assert.That(snapshot.SpawnedCats, Has.Count.EqualTo(2));
            Assert.That(snapshot.SpawnedCats.Select(item => item.CellIndex).Distinct().Count(), Is.EqualTo(2));
            Assert.That(snapshot.SpawnedCats.Select(item => item.CellIndex), Is.All.LessThan(2));
            Assert.That(snapshot.Completed, Is.False);
        }

        [Test]
        public void LevelSnapshotFactory_InvalidOrEmptyPool_ReturnsCompletedEmptySnapshot()
        {
            var template = HolmasTestSupport.CreateBoardTemplate(2, 2);
            var request = HolmasTestSupport.CreateRequest(
                "map-invalid-pool",
                "terrain://invalid-pool",
                9,
                1,
                3,
                null,
                new BoardSpawnEntry { CatId = string.Empty, Weight = 1 },
                new BoardSpawnEntry { CatId = "cat-a", Weight = 0 });

            var snapshot = LevelSnapshotFactory.Create(template, request);

            Assert.That(snapshot.MapId, Is.EqualTo("map-invalid-pool"));
            Assert.That(snapshot.SpawnedCats, Is.Empty);
            Assert.That(snapshot.Completed, Is.True);
            Assert.That(snapshot.RevealedCells, Has.Length.EqualTo(4));
        }

        [Test]
        public void LevelSnapshotFactory_NoValidCells_ReturnsCompletedEmptySnapshot()
        {
            var template = HolmasTestSupport.CreateBoardTemplate(2, 2, (_, _) => false);
            var request = HolmasTestSupport.CreateRequest(
                "map-no-valid-cells",
                "terrain://no-valid-cells",
                5,
                1,
                4,
                new BoardSpawnEntry { CatId = "cat-a", Weight = 1 });

            var snapshot = LevelSnapshotFactory.Create(template, request);

            Assert.That(snapshot.SpawnedCats, Is.Empty);
            Assert.That(snapshot.Completed, Is.True);
            Assert.That(snapshot.RevealedCells, Has.Length.EqualTo(4));
        }

        [Test]
        [TestCase("Assets/1.asset", "Assets/HotUpdateContent/Res/1.asset")]
        [TestCase("1", "Assets/HotUpdateContent/Res/1.asset")]
        [TestCase("2.asset", "Assets/HotUpdateContent/Res/2.asset")]
        [TestCase("terrain://map-1", "terrain://map-1")]
        public void HolmasTerrainAssetPathUtility_NormalizesTerrainPath(string input, string expected)
        {
            string actual = HolmasTerrainAssetPathUtility.NormalizeStoredTerrainPath(input);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase("1", "Assets/HotUpdateContent/Res/1.asset")]
        [TestCase("Assets/1.asset", "Assets/HotUpdateContent/Res/1.asset")]
        [TestCase("Assets/HotUpdateContent/Res/3", "Assets/HotUpdateContent/Res/3.asset")]
        [TestCase("terrain://map-1", "")]
        public void HolmasTerrainAssetPathUtility_ResolvesTerrainLoadLocation(string input, string expected)
        {
            string actual = HolmasTerrainAssetPathUtility.ResolveTerrainLoadLocation(input);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void TerrainBoardTemplateConverter_InvalidTerrainTemplate_Throws()
        {
            var invalidTerrain = ScriptableObject.CreateInstance<InvalidTerrainDimensionsAsset>();

            var ex = Assert.Throws<System.InvalidOperationException>(() => TerrainBoardTemplateConverter.Convert(invalidTerrain));

            Assert.That(ex.Message, Does.Contain("invalid dimensions"));
        }

        [Test]
        public void TerrainBoardTemplateConverter_TryConvertInvalidTerrain_ReturnsFalse()
        {
            var invalidTerrain = ScriptableObject.CreateInstance<InvalidTerrainDimensionsAsset>();

            bool success = TerrainBoardTemplateConverter.TryConvert(invalidTerrain, out BoardTemplate template);

            Assert.That(success, Is.False);
            Assert.That(template, Is.Null);
        }

        [Test]
        public void BoardRuntime_RevealAdvancesCompletionAndTracksCats()
        {
            var template = HolmasTestSupport.CreateBoardTemplate(3, 3);
            var snapshot = new LevelSnapshot
            {
                MapId = "map",
                TerrainPath = "terrain",
                Seed = 1,
                SpawnedCats = new List<SpawnedCatData>
                {
                    new SpawnedCatData
                    {
                        CatId = "cat-a",
                        CellIndex = 8,
                    }
                },
                RevealedCells = new bool[9],
            };

            var runtime = new BoardRuntime(template, snapshot);

            var invalid = runtime.Reveal(-1);
            Assert.That(invalid.IsIgnored, Is.True);
            Assert.That(invalid.IsValidAction, Is.False);

            var reveal = runtime.Reveal(0);
            Assert.That(reveal.IsValidAction, Is.True);
            Assert.That(reveal.FoundCat, Is.False);
            Assert.That(reveal.Completed, Is.False);
            Assert.That(runtime.Completed, Is.False);
            Assert.That(runtime.GetCellState(0).IsRevealed, Is.True);
            Assert.That(runtime.GetCellState(8).IsFoundCat, Is.False);

            var found = runtime.Reveal(8);
            Assert.That(found.IsValidAction, Is.True);
            Assert.That(found.FoundCat, Is.True);
            Assert.That(found.Completed, Is.True);
            Assert.That(runtime.Completed, Is.True);
            Assert.That(runtime.GetCellState(8).IsFoundCat, Is.True);
        }

        private sealed class InvalidTerrainDimensionsAsset : ScriptableObject
        {
            public int Rows => 0;

            public int Cols => 2;

            public bool GetValid(int row, int col)
            {
                return true;
            }

            public Color32 GetColor(int row, int col)
            {
                return new Color32(128, 128, 128, 255);
            }
        }
    }
}

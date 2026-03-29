using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.Holmas.Board;
using App.HotUpdate.Holmas.Levels;
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
    }
}

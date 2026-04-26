using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using App.AOT.Infrastructure.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Holmas.Tests
{
    public sealed class HolmasPersistenceProviderTests
    {
        [Test]
        public void FilePersistenceProvider_BuildSafeFileNameRemovesPathSeparators()
        {
            string safeName = FilePersistenceProvider.BuildSafeFileName("holmas/tutorial:core/find_cat?v1");

            Assert.That(safeName, Does.Not.Contain("/"));
            Assert.That(safeName, Does.Not.Contain("\\"));
            Assert.That(safeName, Does.Not.Contain(":"));
            Assert.That(safeName, Does.Not.Contain("?"));
            Assert.That(safeName, Is.Not.Empty);
        }

        [Test]
        public void FilePersistenceProvider_LoadsLegacyUnsanitizedPath()
        {
            string basePath = CreateTempDirectory();
            try
            {
                string legacyDirectory = Path.Combine(basePath, "holmas");
                Directory.CreateDirectory(legacyDirectory);
                string legacyPath = Path.Combine(legacyDirectory, "player_archive.dat");
                byte[] expected = Encoding.UTF8.GetBytes("legacy-data");
                File.WriteAllBytes(legacyPath, expected);

                var provider = new FilePersistenceProvider(basePath);
                byte[] actual = provider.LoadAsync("holmas/player_archive").GetAwaiter().GetResult();

                Assert.That(actual, Is.EqualTo(expected));
            }
            finally
            {
                DeleteTempDirectory(basePath);
            }
        }

        [Test]
        public void FilePersistenceProvider_FallsBackToPlayerPrefsWhenFileSaveFails()
        {
            string basePath = Path.Combine(Path.GetTempPath(), $"holmas-persistence-blocked-{Guid.NewGuid():N}");
            string key = $"holmas/fallback/{Guid.NewGuid():N}";
            byte[] expected = Encoding.UTF8.GetBytes("fallback-data");
            File.WriteAllText(basePath, "not-a-directory");

            try
            {
                var provider = new FilePersistenceProvider(basePath);

                LogAssert.Expect(
                    LogType.Warning,
                    new Regex("FilePersistenceProvider: File save failed.*using PlayerPrefs fallback"));
                bool saved = provider.SaveAsync(key, expected).GetAwaiter().GetResult();
                byte[] actual = provider.LoadAsync(key).GetAwaiter().GetResult();

                Assert.That(saved, Is.True);
                Assert.That(actual, Is.EqualTo(expected));
                Assert.That(provider.Exists(key), Is.True);

                provider.DeleteAsync(key).GetAwaiter().GetResult();
                Assert.That(provider.Exists(key), Is.False);
            }
            finally
            {
                PlayerPrefs.DeleteKey($"Holmas.Persistence.{FilePersistenceProvider.BuildSafeFileName(key)}");
                PlayerPrefs.Save();
                if (File.Exists(basePath))
                {
                    File.Delete(basePath);
                }
            }
        }

        private static string CreateTempDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), $"holmas-persistence-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DeleteTempDirectory(string directory)
        {
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}

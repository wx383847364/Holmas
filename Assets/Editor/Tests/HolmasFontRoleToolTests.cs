using App.HotUpdate.Holmas.UI.Core;
using Holmas.Editor.FontRole;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Text = UnityEngine.UI.Text;

namespace Holmas.Tests
{
    public sealed class HolmasFontRoleToolTests
    {
        private const string TempRoot = "Assets/Temp/HolmasFontRoleToolTests";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempRoot);
            AssetDatabase.Refresh();
        }

        [Test]
        public void ClassifyRole_UsesExpectedAutomaticRules()
        {
            Assert.That(
                HolmasFontRoleToolLogic.ClassifyRole("MainPanel/Header/TitleText", "侦探社", 24f),
                Is.EqualTo(HolmasFontRole.FormalTitle));

            Assert.That(
                HolmasFontRoleToolLogic.ClassifyRole("ActivityBanner/TitleText", "夏日活动", 24f),
                Is.EqualTo(HolmasFontRole.ActivityTitle));

            Assert.That(
                HolmasFontRoleToolLogic.ClassifyRole("TopBar/GoldCount", "12,345", 18f),
                Is.EqualTo(HolmasFontRole.Numeric));

            Assert.That(
                HolmasFontRoleToolLogic.ClassifyRole("TaskItem/Description", "找到目标猫咪", 22f),
                Is.EqualTo(HolmasFontRole.FormalBody));
        }

        [Test]
        public void ResolveRole_ManualOverrideWinsOverAutomaticRules()
        {
            HolmasFontRoleProfile profile = ScriptableObject.CreateInstance<HolmasFontRoleProfile>();
            try
            {
                profile.EnsureDefaultEntries();
                profile.SetManualOverride("Assets/Fake.prefab", "Header/TitleText", nameof(TextMeshProUGUI), HolmasFontRole.ActivityTitle);

                HolmasFontRole role = HolmasFontRoleToolLogic.ResolveRole(
                    profile,
                    "Assets/Fake.prefab",
                    "Header/TitleText",
                    nameof(TextMeshProUGUI),
                    "TitleText",
                    "侦探社",
                    24f,
                    out string source);

                Assert.That(role, Is.EqualTo(HolmasFontRole.ActivityTitle));
                Assert.That(source, Is.EqualTo("Manual"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ApplyToPrefab_ReplacesTmpAndLegacyFonts()
        {
            EnsureFolder("Assets/Temp");
            EnsureFolder(TempRoot);

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Res/Font/04B_19__.TTF") ??
                              Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Assert.That(sourceFont, Is.Not.Null);

            TMP_FontAsset tmpFont = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);
            tmpFont.name = "HolmasFontRoleToolTests_TMP";
            AssetDatabase.CreateAsset(tmpFont, TempRoot + "/HolmasFontRoleToolTests_TMP.asset");

            HolmasFontRoleProfile profile = ScriptableObject.CreateInstance<HolmasFontRoleProfile>();
            GameObject root = new GameObject("FontRoleTestPrefab", typeof(RectTransform));
            try
            {
                profile.EnsureDefaultEntries();
                ConfigureAllRoles(profile, sourceFont, tmpFont);

                GameObject tmpObject = new GameObject("TitleText", typeof(RectTransform));
                tmpObject.transform.SetParent(root.transform, false);
                TextMeshProUGUI tmpText = tmpObject.AddComponent<TextMeshProUGUI>();
                tmpText.text = "侦探社";
                tmpText.fontSize = 34f;

                GameObject legacyObject = new GameObject("BodyText", typeof(RectTransform));
                legacyObject.transform.SetParent(root.transform, false);
                Text legacyText = legacyObject.AddComponent<Text>();
                legacyText.text = "任务说明";
                legacyText.fontSize = 20;

                string prefabPath = TempRoot + "/FontRoleTestPrefab.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

                var result = new HolmasFontRoleApplyResult();
                HolmasFontRoleToolLogic.ApplyToPrefab(profile, prefabPath, result);

                GameObject loaded = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(loaded, Is.Not.Null);

                TextMeshProUGUI loadedTmp = loaded.GetComponentInChildren<TextMeshProUGUI>(true);
                Text loadedLegacy = loaded.GetComponentInChildren<Text>(true);

                Assert.That(loadedTmp.font, Is.SameAs(tmpFont));
                Assert.That(loadedLegacy.font, Is.SameAs(sourceFont));
                Assert.That(result.ChangedCount, Is.EqualTo(2));
                Assert.That(result.SkippedCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ApplyToPrefab_SkipsMissingRoleAssetsWithoutChangingText()
        {
            EnsureFolder("Assets/Temp");
            EnsureFolder(TempRoot);

            HolmasFontRoleProfile profile = ScriptableObject.CreateInstance<HolmasFontRoleProfile>();
            GameObject root = new GameObject("FontRoleSkipPrefab", typeof(RectTransform));
            try
            {
                profile.EnsureDefaultEntries();
                Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Res/Font/04B_19__.TTF") ??
                                  Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                TMP_FontAsset original = TMP_FontAsset.CreateFontAsset(
                    sourceFont,
                    90,
                    9,
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true);
                original.name = "HolmasFontRoleToolTests_OriginalTMP";
                AssetDatabase.CreateAsset(original, TempRoot + "/HolmasFontRoleToolTests_OriginalTMP.asset");

                GameObject tmpObject = new GameObject("TitleText", typeof(RectTransform));
                tmpObject.transform.SetParent(root.transform, false);
                TextMeshProUGUI tmpText = tmpObject.AddComponent<TextMeshProUGUI>();
                tmpText.text = "侦探社";
                tmpText.fontSize = 34f;
                tmpText.font = original;

                string prefabPath = TempRoot + "/FontRoleSkipPrefab.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

                var result = new HolmasFontRoleApplyResult();
                HolmasFontRoleToolLogic.ApplyToPrefab(profile, prefabPath, result);

                GameObject loaded = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                TextMeshProUGUI loadedTmp = loaded.GetComponentInChildren<TextMeshProUGUI>(true);

                Assert.That(loadedTmp.font, Is.SameAs(original));
                Assert.That(result.ChangedCount, Is.EqualTo(0));
                Assert.That(result.SkippedCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(profile);
            }
        }

        private static void ConfigureAllRoles(HolmasFontRoleProfile profile, Font sourceFont, TMP_FontAsset tmpFont)
        {
            foreach (HolmasFontRole role in System.Enum.GetValues(typeof(HolmasFontRole)))
            {
                HolmasFontRoleEntry entry = profile.GetEntry(role);
                entry.Enabled = true;
                entry.SourceFont = sourceFont;
                entry.TmpFontAsset = tmpFont;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using App.HotUpdate.Holmas.UI.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Text = UnityEngine.UI.Text;

namespace Holmas.Editor.FontRole
{
    public sealed class HolmasFontRoleToolWindow : EditorWindow
    {
        private const float MinReportHeight = 220f;
        private const float MaxReportHeight = 520f;

        private HolmasFontRoleProfile _profile;
        private Vector2 _contentScroll;
        private Vector2 _reportScroll;
        private List<HolmasFontRoleScanItem> _scanItems = new List<HolmasFontRoleScanItem>();
        private string _lastSummary = string.Empty;
        private int _expandedActionIndex = -1;

        [MenuItem("Holmas/UI/Font Role Tool")]
        public static void Open()
        {
            var window = GetWindow<HolmasFontRoleToolWindow>("Holmas Font Roles");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            _profile = HolmasFontRoleToolLogic.LoadOrCreateDefaultProfile();
        }

        private void OnGUI()
        {
            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);

            try
            {
                EditorGUILayout.Space(8f);
                DrawProfileHeader();
                EditorGUILayout.Space(8f);

                if (_profile == null)
                {
                    EditorGUILayout.HelpBox("HolmasFontRoleProfile 缺失。", MessageType.Error);
                    return;
                }

                DrawRoleEntries();
                EditorGUILayout.Space(8f);
                DrawActions();
                EditorGUILayout.Space(8f);
                DrawReport();
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawProfileHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Profile", GUILayout.Width(72f));
                _profile = (HolmasFontRoleProfile)EditorGUILayout.ObjectField(_profile, typeof(HolmasFontRoleProfile), false);
                if (GUILayout.Button("Load / Create Default", GUILayout.Width(168f)))
                {
                    _profile = HolmasFontRoleToolLogic.LoadOrCreateDefaultProfile();
                    _scanItems.Clear();
                }
            }

            EditorGUILayout.LabelField("Profile Path", HolmasFontRoleProfile.DefaultAssetPath);
            EditorGUILayout.LabelField("TMP Output", HolmasFontRoleProfile.DefaultTmpAssetDirectory);
        }

        private void DrawRoleEntries()
        {
            _profile.EnsureDefaultEntries();
            SerializedObject serializedProfile = new SerializedObject(_profile);
            SerializedProperty entries = serializedProfile.FindProperty("roleEntries");

            EditorGUILayout.LabelField("Font Roles", EditorStyles.boldLabel);
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                HolmasFontRole role = (HolmasFontRole)entry.FindPropertyRelative("Role").enumValueIndex;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("Enabled"), GUIContent.none, GUILayout.Width(20f));
                        EditorGUILayout.LabelField(role.ToString(), EditorStyles.boldLabel, GUILayout.Width(112f));
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("SourceFont"), GUIContent.none);
                    }

                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("TmpFontAsset"), new GUIContent("TMP Font"));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("Description"));
                }
            }

            if (serializedProfile.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_profile);
            }
        }

        private void DrawActions()
        {
            DrawActionButtonWithHelp(
                0,
                "Scan Fonts Folder",
                "扫描字体目录，给空角色补推荐字体。",
                "用途：从 Assets/Res/Font 重新查找可用的 .ttf / .otf 字体，并给空角色补上推荐字体。\n会改：只更新 HolmasFontRoleProfile.asset 里的角色源字体。\n不会：不生成 TMP Font Asset，也不会修改任何 prefab。\n何时点：新增、删除、替换字体文件后先点它。",
                () =>
                {
                    HolmasFontRoleToolLogic.AssignRecommendedDefaults(_profile, overwriteExisting: false);
                    EditorUtility.SetDirty(_profile);
                    AssetDatabase.SaveAssets();
                    _lastSummary = "Font roles scanned and recommended defaults assigned.";
                });

            DrawActionButtonWithHelp(
                1,
                "Generate / Refresh TMP Assets",
                "生成 TMP 字体资产，解决 TMP Font 为 None。",
                "用途：把已启用角色的 Source Font 生成或复用为 TMP Font Asset。\n会改：创建或刷新 Assets/Res/Font/TMP 下的 TMP 资源，并回填到每个角色的 TMP Font。\n不会：不修改 prefab，只准备 TextMeshPro 可用的字体资产。\n何时点：TMP Font 显示 None，或更换角色字体后点它。",
                () =>
                {
                    HolmasFontRoleToolLogic.GenerateOrRefreshTmpAssets(_profile);
                    EditorUtility.SetDirty(_profile);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    _lastSummary = "TMP font assets generated/refreshed under " + HolmasFontRoleProfile.DefaultTmpAssetDirectory + ".";
                });

            DrawActionButtonWithHelp(
                2,
                "Create / Refresh Runtime Settings",
                "同步运行时新建文本使用的正式字体和兜底字体。",
                "用途：同步运行时新建文本使用的正式字体和中文兜底字体。\n会改：创建或刷新 Assets/Res/Font/HolmasFontRuntimeSettings.asset。\n不会：不修改 prefab，也不会替换已有界面字体。\n何时点：调整 FormalBody 或 Fallback 后点它，避免运行时文本和 prefab 风格不一致。",
                () =>
                {
                    HolmasFontRuntimeSettings settings = HolmasFontRoleToolLogic.CreateOrRefreshRuntimeSettings(_profile);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    _lastSummary = "Runtime settings refreshed: " + AssetDatabase.GetAssetPath(settings);
                });

            DrawActionButtonWithHelp(
                3,
                "Scan Formal Prefabs",
                "预览正式 prefab 的字体角色匹配结果，不写 prefab。",
                "用途：预览正式 prefab 里的每个 TMP / Text 会匹配到哪个字体角色。\n范围：Main / Battle / Loading / Leaderboard / AgencyMain。\n会改：普通扫描不写 prefab；如果在报告里手动改角色，会保存覆盖规则到 Profile。\n何时点：Apply 前先点它，用来检查匹配结果和缺失状态。",
                () =>
                {
                    _scanItems = HolmasFontRoleToolLogic.ScanFormalPrefabs(_profile);
                    _lastSummary = $"Scanned {_scanItems.Count} text components.";
                });

            DrawActionButtonWithHelp(
                4,
                "Apply To Formal Prefabs",
                "把字体角色真正写入正式 prefab。",
                "用途：把字体角色真正写入正式 prefab。\n会改：TextMeshProUGUI 使用角色的 TMP Font，legacy Text 使用角色的 Source Font。\n不会：不改字号、颜色、对齐、文本内容、布局和材质颜色。\n何时点：确认 Scan 结果没问题，并且 TMP Font 都已生成后再点它。",
                () =>
                {
                    HolmasFontRoleApplyResult result = HolmasFontRoleToolLogic.ApplyToFormalPrefabs(_profile);
                    _scanItems = HolmasFontRoleToolLogic.ScanFormalPrefabs(_profile);
                    _lastSummary = $"Changed {result.ChangedCount}, unchanged {result.UnchangedCount}, skipped {result.SkippedCount}.";
                    Debug.Log("[HolmasFontRoleTool] " + _lastSummary + "\n" + result.BuildLog());
                });

            if (!string.IsNullOrWhiteSpace(_lastSummary))
            {
                EditorGUILayout.HelpBox(_lastSummary, MessageType.Info);
            }
        }

        private void DrawActionButtonWithHelp(int index, string label, string summary, string detail, Action onClick)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(label, GUILayout.Width(280f), GUILayout.Height(28f)))
                    {
                        ExecuteAction(label, onClick);
                    }

                    EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(28f));

                    bool expanded = _expandedActionIndex == index;
                    if (GUILayout.Button(expanded ? "Hide" : "Details", GUILayout.Width(72f), GUILayout.Height(28f)))
                    {
                        _expandedActionIndex = expanded ? -1 : index;
                    }
                }

                if (_expandedActionIndex == index)
                {
                    EditorGUILayout.HelpBox(detail, MessageType.None);
                }
            }
        }

        private void ExecuteAction(string label, Action onClick)
        {
            try
            {
                _lastSummary = string.Empty;
                onClick?.Invoke();

                string message = string.IsNullOrWhiteSpace(_lastSummary)
                    ? label + " completed."
                    : _lastSummary;
                EditorUtility.DisplayDialog("执行成功", label + "\n\n" + message, "OK");
            }
            catch (Exception exception)
            {
                _lastSummary = label + " failed: " + exception.Message;
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("执行失败", label + "\n\n" + exception.Message, "OK");
            }
        }

        private void DrawReport()
        {
            if (_scanItems == null || _scanItems.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField($"Scan Report ({_scanItems.Count})", EditorStyles.boldLabel);

            float reportHeight = Mathf.Clamp(position.height * 0.42f, MinReportHeight, MaxReportHeight);
            _reportScroll = EditorGUILayout.BeginScrollView(_reportScroll, GUILayout.Height(reportHeight));
            for (int i = 0; i < _scanItems.Count; i++)
            {
                HolmasFontRoleScanItem item = _scanItems[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(item.PrefabName, GUILayout.Width(138f));
                        EditorGUILayout.LabelField(item.ComponentType, GUILayout.Width(118f));
                        HolmasFontRole nextRole = (HolmasFontRole)EditorGUILayout.EnumPopup(item.Role, GUILayout.Width(130f));
                        if (nextRole != item.Role)
                        {
                            _profile.SetManualOverride(item.PrefabPath, item.TransformPath, item.ComponentType, nextRole);
                            EditorUtility.SetDirty(_profile);
                            AssetDatabase.SaveAssets();
                            item.Role = nextRole;
                            item.Source = "Manual";
                        }

                        if (GUILayout.Button("Select Prefab", GUILayout.Width(102f)))
                        {
                            Object prefab = AssetDatabase.LoadAssetAtPath<Object>(item.PrefabPath);
                            Selection.activeObject = prefab;
                        }

                        if (GUILayout.Button("Ping", GUILayout.Width(54f)))
                        {
                            Object prefab = AssetDatabase.LoadAssetAtPath<Object>(item.PrefabPath);
                            EditorGUIUtility.PingObject(prefab);
                        }
                    }

                    EditorGUILayout.LabelField("Path", string.IsNullOrEmpty(item.TransformPath) ? "(root)" : item.TransformPath);
                    EditorGUILayout.LabelField("Text", item.TextPreview);
                    EditorGUILayout.LabelField("Rule", item.Source + (string.IsNullOrWhiteSpace(item.Warning) ? string.Empty : " / " + item.Warning));
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    public sealed class HolmasFontRoleScanItem
    {
        public string PrefabPath;
        public string PrefabName;
        public string TransformPath;
        public string ComponentType;
        public string TextPreview;
        public float FontSize;
        public HolmasFontRole Role;
        public string Source;
        public string Warning;
    }

    public sealed class HolmasFontRoleApplyResult
    {
        private readonly List<string> _logs = new List<string>();

        public int ChangedCount;
        public int UnchangedCount;
        public int SkippedCount;

        public void AddLog(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _logs.Add(message);
            }
        }

        public string BuildLog()
        {
            return _logs.Count == 0 ? "No details." : string.Join("\n", _logs);
        }
    }

    public static class HolmasFontRoleToolLogic
    {
        public static readonly string[] FormalPrefabPaths =
        {
            "Assets/HotUpdateContent/Res/Perfabs/UI/MainPanel.prefab",
            "Assets/HotUpdateContent/Res/Perfabs/UI/BattlePanel.prefab",
            "Assets/HotUpdateContent/Res/Perfabs/UI/LoadingPanel.prefab",
            "Assets/HotUpdateContent/Res/Perfabs/UI/LeadbroadPanel.prefab",
            "Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/Portrait/AgencyMainPanel.prefab",
        };

        private const string FontRoot = "Assets/Res/Font";
        private const string TmpSample = "Holmas侦探社游园竹舍正式字体活动标题金币体力等级奖励排行榜收藏品找猫0123456789+-/.,:%";

        public static HolmasFontRoleProfile LoadOrCreateDefaultProfile()
        {
            HolmasFontRoleProfile profile = AssetDatabase.LoadAssetAtPath<HolmasFontRoleProfile>(HolmasFontRoleProfile.DefaultAssetPath);
            if (profile == null)
            {
                EnsureFolder("Assets/Res");
                EnsureFolder(FontRoot);
                profile = ScriptableObject.CreateInstance<HolmasFontRoleProfile>();
                profile.EnsureDefaultEntries();
                AssignRecommendedDefaults(profile, overwriteExisting: true);
                AssetDatabase.CreateAsset(profile, HolmasFontRoleProfile.DefaultAssetPath);
                AssetDatabase.SaveAssets();
            }
            else
            {
                profile.EnsureDefaultEntries();
                AssignRecommendedDefaults(profile, overwriteExisting: false);
                EditorUtility.SetDirty(profile);
            }

            return profile;
        }

        public static void AssignRecommendedDefaults(HolmasFontRoleProfile profile, bool overwriteExisting)
        {
            if (profile == null)
            {
                return;
            }

            profile.EnsureDefaultEntries();
            AssignFont(profile, HolmasFontRole.FormalBody, "LXGWMarkerGothic-Regular", overwriteExisting);
            AssignFont(profile, HolmasFontRole.FormalTitle, "LXGWMarkerGothic-Regular", overwriteExisting);
            AssignFont(profile, HolmasFontRole.ActivityTitle, "SmileySans-Oblique", overwriteExisting);
            AssignFont(profile, HolmasFontRole.Numeric, "ResourceHanRoundedCN-Regular", overwriteExisting);
            AssignFont(profile, HolmasFontRole.Fallback, "NotoSansSC", overwriteExisting);
        }

        public static void GenerateOrRefreshTmpAssets(HolmasFontRoleProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            profile.EnsureDefaultEntries();
            EnsureFolder("Assets/Res");
            EnsureFolder(FontRoot);
            EnsureFolder(HolmasFontRoleProfile.DefaultTmpAssetDirectory);

            for (int i = 0; i < profile.RoleEntries.Count; i++)
            {
                HolmasFontRoleEntry entry = profile.RoleEntries[i];
                if (entry == null || !entry.Enabled || entry.SourceFont == null)
                {
                    continue;
                }

                string sourcePath = AssetDatabase.GetAssetPath(entry.SourceFont);
                string fileName = SanitizeFileName("Holmas_" + entry.Role + "_" + Path.GetFileNameWithoutExtension(sourcePath));
                string assetPath = HolmasFontRoleProfile.DefaultTmpAssetDirectory + "/" + fileName + ".asset";
                TMP_FontAsset tmpAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                if (tmpAsset == null)
                {
                    tmpAsset = TMP_FontAsset.CreateFontAsset(
                        entry.SourceFont,
                        90,
                        9,
                        GlyphRenderMode.SDFAA,
                        1024,
                        1024,
                        AtlasPopulationMode.Dynamic,
                        true);

                    tmpAsset.name = fileName;
                    tmpAsset.TryAddCharacters(TmpSample, true);
                    AssetDatabase.CreateAsset(tmpAsset, assetPath);
                }
                else
                {
                    tmpAsset.TryAddCharacters(TmpSample, true);
                    EditorUtility.SetDirty(tmpAsset);
                }

                entry.TmpFontAsset = tmpAsset;
            }
        }

        public static HolmasFontRuntimeSettings CreateOrRefreshRuntimeSettings(HolmasFontRoleProfile profile)
        {
            HolmasFontRuntimeSettings settings = AssetDatabase.LoadAssetAtPath<HolmasFontRuntimeSettings>(HolmasFontRuntimeSettings.DefaultAssetPath);
            if (settings == null)
            {
                EnsureFolder("Assets/Res");
                EnsureFolder(FontRoot);
                settings = ScriptableObject.CreateInstance<HolmasFontRuntimeSettings>();
                AssetDatabase.CreateAsset(settings, HolmasFontRuntimeSettings.DefaultAssetPath);
            }

            Font formalBody = profile?.GetEntry(HolmasFontRole.FormalBody)?.SourceFont;
            Font fallback = profile?.GetEntry(HolmasFontRole.Fallback)?.SourceFont;
            settings.Configure(formalBody, fallback);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        public static List<HolmasFontRoleScanItem> ScanFormalPrefabs(HolmasFontRoleProfile profile)
        {
            var result = new List<HolmasFontRoleScanItem>();
            for (int i = 0; i < FormalPrefabPaths.Length; i++)
            {
                ScanPrefab(profile, FormalPrefabPaths[i], result);
            }

            return result;
        }

        public static HolmasFontRoleApplyResult ApplyToFormalPrefabs(HolmasFontRoleProfile profile)
        {
            var result = new HolmasFontRoleApplyResult();
            if (profile == null)
            {
                result.SkippedCount++;
                result.AddLog("Profile is null.");
                return result;
            }

            for (int i = 0; i < FormalPrefabPaths.Length; i++)
            {
                ApplyToPrefab(profile, FormalPrefabPaths[i], result);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        public static HolmasFontRole ResolveRole(
            HolmasFontRoleProfile profile,
            string prefabPath,
            string transformPath,
            string componentType,
            string objectName,
            string text,
            float fontSize,
            out string source)
        {
            if (profile != null &&
                profile.TryGetManualOverride(prefabPath, transformPath, componentType, out HolmasFontRole manualRole))
            {
                source = "Manual";
                return manualRole;
            }

            source = "Rule";
            return ClassifyRole(transformPath + "/" + objectName, text, fontSize);
        }

        public static HolmasFontRole ClassifyRole(string pathOrName, string text, float fontSize)
        {
            string haystack = (pathOrName ?? string.Empty).ToLowerInvariant();
            if (ContainsAny(haystack, "activity", "event", "festival", "campaign", "banner", "publicity"))
            {
                return HolmasFontRole.ActivityTitle;
            }

            if (ContainsAny(haystack, "title", "pagetitle", "标题") || fontSize >= 32f)
            {
                return HolmasFontRole.FormalTitle;
            }

            if (ContainsAny(haystack, "count", "level", "gold", "money", "energy", "reward") || IsMostlyNumeric(text))
            {
                return HolmasFontRole.Numeric;
            }

            return HolmasFontRole.FormalBody;
        }

        private static void ScanPrefab(HolmasFontRoleProfile profile, string prefabPath, List<HolmasFontRoleScanItem> result)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                TextMeshProUGUI[] tmpTexts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int i = 0; i < tmpTexts.Length; i++)
                {
                    TextMeshProUGUI text = tmpTexts[i];
                    string transformPath = BuildTransformPath(root.transform, text.transform);
                    HolmasFontRole role = ResolveRole(profile, prefabPath, transformPath, nameof(TextMeshProUGUI), text.name, text.text, text.fontSize, out string source);
                    result.Add(CreateScanItem(profile, prefabPath, root.name, transformPath, nameof(TextMeshProUGUI), text.text, text.fontSize, role, source));
                }

                Text[] legacyTexts = root.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < legacyTexts.Length; i++)
                {
                    Text text = legacyTexts[i];
                    string transformPath = BuildTransformPath(root.transform, text.transform);
                    HolmasFontRole role = ResolveRole(profile, prefabPath, transformPath, nameof(Text), text.name, text.text, text.fontSize, out string source);
                    result.Add(CreateScanItem(profile, prefabPath, root.name, transformPath, nameof(Text), text.text, text.fontSize, role, source));
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static HolmasFontRoleScanItem CreateScanItem(
            HolmasFontRoleProfile profile,
            string prefabPath,
            string prefabName,
            string transformPath,
            string componentType,
            string text,
            float fontSize,
            HolmasFontRole role,
            string source)
        {
            HolmasFontRoleEntry entry = profile?.GetEntry(role);
            string warning = string.Empty;
            if (entry == null || !entry.Enabled)
            {
                warning = "role disabled or missing";
            }
            else if (componentType == nameof(TextMeshProUGUI) && entry.TmpFontAsset == null)
            {
                warning = "TMP font missing";
            }
            else if (componentType == nameof(Text) && entry.SourceFont == null)
            {
                warning = "source font missing";
            }

            return new HolmasFontRoleScanItem
            {
                PrefabPath = prefabPath,
                PrefabName = prefabName,
                TransformPath = transformPath,
                ComponentType = componentType,
                TextPreview = PreviewText(text),
                FontSize = fontSize,
                Role = role,
                Source = source,
                Warning = warning,
            };
        }

        public static void ApplyToPrefab(HolmasFontRoleProfile profile, string prefabPath, HolmasFontRoleApplyResult result)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                result.SkippedCount++;
                result.AddLog("Missing prefab: " + prefabPath);
                return;
            }

            bool changed = false;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                TextMeshProUGUI[] tmpTexts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int i = 0; i < tmpTexts.Length; i++)
                {
                    TextMeshProUGUI text = tmpTexts[i];
                    string transformPath = BuildTransformPath(root.transform, text.transform);
                    HolmasFontRole role = ResolveRole(profile, prefabPath, transformPath, nameof(TextMeshProUGUI), text.name, text.text, text.fontSize, out _);
                    HolmasFontRoleEntry entry = profile.GetEntry(role);
                    if (entry == null || !entry.Enabled || entry.TmpFontAsset == null)
                    {
                        result.SkippedCount++;
                        result.AddLog($"Skip TMP {prefabPath}:{transformPath}, role={role}, missing TMP font.");
                        continue;
                    }

                    if (text.font == entry.TmpFontAsset)
                    {
                        result.UnchangedCount++;
                        continue;
                    }

                    Undo.RecordObject(text, "Apply Holmas TMP Font Role");
                    text.font = entry.TmpFontAsset;
                    EditorUtility.SetDirty(text);
                    changed = true;
                    result.ChangedCount++;
                }

                Text[] legacyTexts = root.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < legacyTexts.Length; i++)
                {
                    Text text = legacyTexts[i];
                    string transformPath = BuildTransformPath(root.transform, text.transform);
                    HolmasFontRole role = ResolveRole(profile, prefabPath, transformPath, nameof(Text), text.name, text.text, text.fontSize, out _);
                    HolmasFontRoleEntry entry = profile.GetEntry(role);
                    if (entry == null || !entry.Enabled || entry.SourceFont == null)
                    {
                        result.SkippedCount++;
                        result.AddLog($"Skip Legacy Text {prefabPath}:{transformPath}, role={role}, missing source font.");
                        continue;
                    }

                    if (text.font == entry.SourceFont)
                    {
                        result.UnchangedCount++;
                        continue;
                    }

                    Undo.RecordObject(text, "Apply Holmas Legacy Font Role");
                    text.font = entry.SourceFont;
                    EditorUtility.SetDirty(text);
                    changed = true;
                    result.ChangedCount++;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AssignFont(HolmasFontRoleProfile profile, HolmasFontRole role, string fileNameWithoutExtension, bool overwriteExisting)
        {
            HolmasFontRoleEntry entry = profile.GetEntry(role);
            if (entry == null || (!overwriteExisting && entry.SourceFont != null))
            {
                return;
            }

            Font font = FindFont(fileNameWithoutExtension);
            if (font != null)
            {
                entry.SourceFont = font;
            }
        }

        private static Font FindFont(string fileNameWithoutExtension)
        {
            string[] guids = AssetDatabase.FindAssets("t:Font", new[] { FontRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.Equals(Path.GetFileNameWithoutExtension(path), fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<Font>(path);
                }
            }

            return null;
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (value.Contains(needles[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsMostlyNumeric(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            int meaningful = 0;
            int numeric = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (char.IsWhiteSpace(ch))
                {
                    continue;
                }

                meaningful++;
                if (char.IsDigit(ch) || ch == '+' || ch == '-' || ch == '/' || ch == '.' || ch == ',' || ch == ':' || ch == '%' || ch == 'B' || ch == 'K' || ch == 'M')
                {
                    numeric++;
                }
            }

            return meaningful > 0 && numeric >= Mathf.CeilToInt(meaningful * 0.6f);
        }

        private static string BuildTransformPath(Transform root, Transform target)
        {
            if (root == null || target == null || root == target)
            {
                return string.Empty;
            }

            var stack = new Stack<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                stack.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", stack.ToArray());
        }

        private static string PreviewText(string value)
        {
            string safe = string.IsNullOrEmpty(value) ? "(empty)" : value.Replace("\r", " ").Replace("\n", " ");
            return safe.Length <= 48 ? safe : safe.Substring(0, 48) + "...";
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = value ?? "FontAsset";
            for (int i = 0; i < invalid.Length; i++)
            {
                safe = safe.Replace(invalid[i], '_');
            }

            return safe.Replace(' ', '_');
        }
    }
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Core
{
    public enum HolmasFontRole
    {
        FormalBody = 0,
        FormalTitle = 1,
        ActivityTitle = 2,
        Numeric = 3,
        Fallback = 4,
    }

    [Serializable]
    public sealed class HolmasFontRoleEntry
    {
        public HolmasFontRole Role;
        public bool Enabled = true;
        public Font SourceFont;
        public TMP_FontAsset TmpFontAsset;
        [TextArea(1, 3)]
        public string Description;
    }

    [Serializable]
    public sealed class HolmasFontRoleOverride
    {
        public string PrefabPath;
        public string TransformPath;
        public string ComponentType;
        public HolmasFontRole Role;
    }

    public sealed class HolmasFontRoleProfile : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Res/Font/HolmasFontRoleProfile.asset";
        public const string DefaultTmpAssetDirectory = "Assets/Res/Font/TMP";

        [SerializeField]
        private List<HolmasFontRoleEntry> roleEntries = new List<HolmasFontRoleEntry>();

        [SerializeField]
        private List<HolmasFontRoleOverride> manualOverrides = new List<HolmasFontRoleOverride>();

        public IReadOnlyList<HolmasFontRoleEntry> RoleEntries => roleEntries;

        public IReadOnlyList<HolmasFontRoleOverride> ManualOverrides => manualOverrides;

        public void EnsureDefaultEntries()
        {
            EnsureEntry(HolmasFontRole.FormalBody, "正式 UI 正文、按钮、任务卡常规文本。");
            EnsureEntry(HolmasFontRole.FormalTitle, "正式 UI 页面标题、重要模块标题。");
            EnsureEntry(HolmasFontRole.ActivityTitle, "活动、宣传、节日、Banner 标题。");
            EnsureEntry(HolmasFontRole.Numeric, "金币、体力、等级、计数、奖励等数字文本。");
            EnsureEntry(HolmasFontRole.Fallback, "中文兜底字体，用于长正文与缺字回退。");
        }

        public HolmasFontRoleEntry GetEntry(HolmasFontRole role)
        {
            EnsureDefaultEntries();
            for (int i = 0; i < roleEntries.Count; i++)
            {
                HolmasFontRoleEntry entry = roleEntries[i];
                if (entry != null && entry.Role == role)
                {
                    return entry;
                }
            }

            return null;
        }

        public bool TryGetManualOverride(
            string prefabPath,
            string transformPath,
            string componentType,
            out HolmasFontRole role)
        {
            string safePrefabPath = prefabPath ?? string.Empty;
            string safeTransformPath = transformPath ?? string.Empty;
            string safeComponentType = componentType ?? string.Empty;

            for (int i = 0; i < manualOverrides.Count; i++)
            {
                HolmasFontRoleOverride item = manualOverrides[i];
                if (item == null)
                {
                    continue;
                }

                if (string.Equals(item.PrefabPath, safePrefabPath, StringComparison.Ordinal) &&
                    string.Equals(item.TransformPath, safeTransformPath, StringComparison.Ordinal) &&
                    string.Equals(item.ComponentType, safeComponentType, StringComparison.Ordinal))
                {
                    role = item.Role;
                    return true;
                }
            }

            role = HolmasFontRole.FormalBody;
            return false;
        }

        public void SetManualOverride(
            string prefabPath,
            string transformPath,
            string componentType,
            HolmasFontRole role)
        {
            string safePrefabPath = prefabPath ?? string.Empty;
            string safeTransformPath = transformPath ?? string.Empty;
            string safeComponentType = componentType ?? string.Empty;

            for (int i = 0; i < manualOverrides.Count; i++)
            {
                HolmasFontRoleOverride item = manualOverrides[i];
                if (item == null)
                {
                    continue;
                }

                if (string.Equals(item.PrefabPath, safePrefabPath, StringComparison.Ordinal) &&
                    string.Equals(item.TransformPath, safeTransformPath, StringComparison.Ordinal) &&
                    string.Equals(item.ComponentType, safeComponentType, StringComparison.Ordinal))
                {
                    item.Role = role;
                    return;
                }
            }

            manualOverrides.Add(new HolmasFontRoleOverride
            {
                PrefabPath = safePrefabPath,
                TransformPath = safeTransformPath,
                ComponentType = safeComponentType,
                Role = role,
            });
        }

        private void EnsureEntry(HolmasFontRole role, string description)
        {
            for (int i = 0; i < roleEntries.Count; i++)
            {
                HolmasFontRoleEntry entry = roleEntries[i];
                if (entry != null && entry.Role == role)
                {
                    if (string.IsNullOrWhiteSpace(entry.Description))
                    {
                        entry.Description = description;
                    }

                    return;
                }
            }

            roleEntries.Add(new HolmasFontRoleEntry
            {
                Role = role,
                Enabled = true,
                Description = description,
            });
        }
    }
}

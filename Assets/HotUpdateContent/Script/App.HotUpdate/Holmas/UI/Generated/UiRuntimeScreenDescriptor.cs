using System;
using App.HotUpdate.Holmas.UI.Binding;

namespace App.HotUpdate.Holmas.UI.Generated
{
    /// <summary>
    /// 运行时可消费的页面描述产物。
    /// 业务页面注册只依赖这个描述，不直接依赖生成器内部 schema。
    /// </summary>
    public sealed class UiRuntimeScreenDescriptor
    {
        public UiRuntimeScreenDescriptor(string prefabName, string prefabAssetPath, UiBindingManifest bindingManifest)
        {
            if (string.IsNullOrWhiteSpace(prefabName))
            {
                throw new ArgumentException("UiRuntimeScreenDescriptor: prefabName 不能为空。", nameof(prefabName));
            }

            PrefabName = prefabName;
            PrefabAssetPath = prefabAssetPath ?? string.Empty;
            BindingManifest = bindingManifest ?? throw new ArgumentNullException(nameof(bindingManifest));
        }

        public string PrefabName { get; }

        public string PrefabAssetPath { get; }

        public UiBindingManifest BindingManifest { get; }
    }
}

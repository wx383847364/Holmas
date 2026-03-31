using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Tasks.Config;
using App.Shared.Contracts;
using UnityEngine;

namespace App.HotUpdate.Holmas.Levels
{
    /// <summary>
    /// 配置包加载结果。
    /// </summary>
    [Serializable]
    public sealed class HolmasConfigLoadResult
    {
        public bool Success;
        public string FailureReason = string.Empty;
        public HolmasConfigCatalogBundle Bundle;
        public HolmasConfigReport Report = new HolmasConfigReport();
    }

    /// <summary>
    /// 运行时配置加载门面。
    /// 负责从正式资源入口加载 core / cat meta 两个二进制包，并恢复成 Catalog。
    /// </summary>
    public sealed class HolmasConfigRuntimeLoader
    {
        public const string DefaultCoreConfigLocation = "Assets/HotUpdateContent/Config/holmas_core_config.bytes";
        public const string DefaultCatMetaConfigLocation = "Assets/HotUpdateContent/Config/holmas_cat_meta.bytes";

        private readonly IAssetsRuntime _assetsRuntime;

        public HolmasConfigRuntimeLoader(IAssetsRuntime assetsRuntime)
        {
            _assetsRuntime = assetsRuntime ?? throw new ArgumentNullException(nameof(assetsRuntime));
        }

        public async Task<HolmasConfigLoadResult> LoadAsync(string coreLocation, string catMetaLocation)
        {
            var result = new HolmasConfigLoadResult();

            if (string.IsNullOrWhiteSpace(coreLocation))
            {
                result.FailureReason = "核心配置路径为空。";
                return result;
            }

            if (string.IsNullOrWhiteSpace(catMetaLocation))
            {
                result.FailureReason = "猫元数据路径为空。";
                return result;
            }

            BytesLoadResult coreLoad = await LoadBytesAsync(coreLocation);
            if (coreLoad.Bytes == null)
            {
                result.FailureReason = coreLoad.FailureReason;
                return result;
            }

            BytesLoadResult catLoad = await LoadBytesAsync(catMetaLocation);
            if (catLoad.Bytes == null)
            {
                result.FailureReason = catLoad.FailureReason;
                return result;
            }

            if (!HolmasConfigCatalogFactory.TryCreateFromBinary(coreLoad.Bytes, catLoad.Bytes, out var bundle, out var report))
            {
                result.FailureReason = report != null && report.Errors.Count > 0 ? report.Errors[0] : "配置包恢复失败。";
                result.Report = report;
                return result;
            }

            result.Success = true;
            result.Bundle = bundle;
            result.Report = report;
            return result;
        }

        public Task<HolmasConfigLoadResult> LoadDefaultAsync()
        {
            return LoadAsync(DefaultCoreConfigLocation, DefaultCatMetaConfigLocation);
        }

        private async Task<BytesLoadResult> LoadBytesAsync(string location)
        {
            if (_assetsRuntime == null)
            {
                return BytesLoadResult.Fail("当前没有可用的 IAssetsRuntime。");
            }

            var handle = await _assetsRuntime.LoadAssetAsync(location);
            if (handle == null)
            {
                return BytesLoadResult.Fail($"无法加载资源: {location}");
            }

            try
            {
                if (handle.AssetObject is TextAsset textAsset && textAsset.bytes != null)
                {
                    return BytesLoadResult.Succeed(textAsset.bytes);
                }

                return BytesLoadResult.Fail($"资源不是可解析的 TextAsset: {location}");
            }
            finally
            {
                handle.Release();
            }
        }

        private sealed class BytesLoadResult
        {
            public byte[] Bytes;
            public string FailureReason = string.Empty;

            public static BytesLoadResult Succeed(byte[] bytes)
            {
                return new BytesLoadResult
                {
                    Bytes = bytes ?? Array.Empty<byte>(),
                };
            }

            public static BytesLoadResult Fail(string reason)
            {
                return new BytesLoadResult
                {
                    Bytes = null,
                    FailureReason = reason ?? string.Empty,
                };
            }
        }
    }
}

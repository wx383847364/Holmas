using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Progression;
using App.Shared.Contracts;
using App.Shared.Holmas.PlayerData;
using UnityEngine;

namespace App.HotUpdate.Holmas.PlayerData
{
    public enum HolmasPlayerArchiveLoadStatus
    {
        Success,
        Missing,
        InvalidData,
        SchemaMismatch,
    }

    [Serializable]
    public sealed class HolmasPlayerArchiveLoadResult
    {
        public HolmasPlayerArchiveLoadStatus Status;
        public string FailureReason = string.Empty;
        public HolmasPlayerArchiveRoot Archive;

        public bool Success => Status == HolmasPlayerArchiveLoadStatus.Success && Archive != null;
    }

    [Serializable]
    public sealed class HolmasPlayerArchiveSaveResult
    {
        public bool Success;
        public string FailureReason = string.Empty;
    }

    public interface IHolmasPlayerArchiveGateway
    {
        Task<HolmasPlayerArchiveLoadResult> LoadAsync();
        Task<HolmasPlayerArchiveSaveResult> SaveAsync(HolmasPlayerArchiveRoot archive);
    }

    /// <summary>
    /// 第一阶段的本地假服实现。
    /// 通过 IPersistence 维护一份 authoritative archive，并保留一份 backup 副本。
    /// </summary>
    public sealed class HolmasLocalMockServerGateway : IHolmasPlayerArchiveGateway
    {
        public const string DefaultSchemaVersion = "holmas.v1.local-mock";
        public const string DefaultPlayerId = "local-player";

        private const string PrimaryArchiveKey = "holmas/player_archive";
        private const string BackupArchiveKey = "holmas/player_archive.backup";

        private readonly IPersistence _persistence;
        private readonly IAppLogger _logger;
        private readonly IHolmasUtcClock _clock;
        private readonly string _schemaVersion;

        private sealed class LoadedArchiveRecord
        {
            public string Key = string.Empty;
            public byte[] Bytes;
            public HolmasPlayerArchiveLoadResult Result = new HolmasPlayerArchiveLoadResult();
        }

        public HolmasLocalMockServerGateway(
            IPersistence persistence,
            IAppLogger logger,
            IHolmasUtcClock clock,
            string schemaVersion = DefaultSchemaVersion)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _logger = logger;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _schemaVersion = string.IsNullOrWhiteSpace(schemaVersion) ? DefaultSchemaVersion : schemaVersion;
        }

        public Task<HolmasPlayerArchiveLoadResult> LoadAsync()
        {
            return LoadInternalAsync();
        }

        public async Task<HolmasPlayerArchiveSaveResult> SaveAsync(HolmasPlayerArchiveRoot archive)
        {
            if (archive == null)
            {
                return new HolmasPlayerArchiveSaveResult
                {
                    FailureReason = "玩家档案为空。",
                };
            }

            archive.SchemaVersion = string.IsNullOrWhiteSpace(archive.SchemaVersion) ? _schemaVersion : archive.SchemaVersion;
            archive.PlayerId = string.IsNullOrWhiteSpace(archive.PlayerId) ? DefaultPlayerId : archive.PlayerId;
            archive.SavedAtUtcMilliseconds = _clock.UtcNowMilliseconds;

            string json = JsonUtility.ToJson(archive, true);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);

            bool primarySaved = await _persistence.SaveAsync(PrimaryArchiveKey, bytes).ConfigureAwait(false);
            bool backupSaved = await _persistence.SaveAsync(BackupArchiveKey, bytes).ConfigureAwait(false);
            if (!primarySaved || !backupSaved)
            {
                string failure = $"保存本地模拟服务器档案失败。primary={primarySaved}, backup={backupSaved}";
                _logger?.LogWarning("HolmasLocalMockServerGateway: {0}", failure);
                return new HolmasPlayerArchiveSaveResult
                {
                    FailureReason = failure,
                };
            }

            return new HolmasPlayerArchiveSaveResult
            {
                Success = true,
            };
        }

        private async Task<HolmasPlayerArchiveLoadResult> LoadInternalAsync()
        {
            LoadedArchiveRecord primaryRecord = await TryLoadByKeyAsync(PrimaryArchiveKey).ConfigureAwait(false);
            LoadedArchiveRecord backupRecord = await TryLoadByKeyAsync(BackupArchiveKey).ConfigureAwait(false);

            HolmasPlayerArchiveLoadResult primaryResult = primaryRecord.Result;
            HolmasPlayerArchiveLoadResult backupResult = backupRecord.Result;

            if (primaryResult.Success && backupResult.Success)
            {
                LoadedArchiveRecord authoritativeRecord = SelectAuthoritativeRecord(primaryRecord, backupRecord);
                LoadedArchiveRecord staleRecord = ReferenceEquals(authoritativeRecord, primaryRecord) ? backupRecord : primaryRecord;
                if (IsArchiveNewer(authoritativeRecord.Result.Archive, staleRecord.Result.Archive))
                {
                    await TryHealReplicaAsync(staleRecord.Key, authoritativeRecord.Bytes, authoritativeRecord.Result.Archive).ConfigureAwait(false);
                }

                return authoritativeRecord.Result;
            }

            if (backupResult.Success)
            {
                _logger?.LogWarning(
                    "HolmasLocalMockServerGateway: 主档案不可用，已从 backup 恢复。primary={0}",
                    primaryResult.FailureReason);
                await TryHealReplicaAsync(PrimaryArchiveKey, backupRecord.Bytes, backupResult.Archive).ConfigureAwait(false);
                return backupResult;
            }

            if (primaryResult.Success)
            {
                await TryHealReplicaAsync(BackupArchiveKey, primaryRecord.Bytes, primaryResult.Archive).ConfigureAwait(false);
                return primaryResult;
            }

            return ResolveLoadFallback(primaryResult, backupResult);
        }

        private async Task<LoadedArchiveRecord> TryLoadByKeyAsync(string key)
        {
            if (!_persistence.Exists(key))
            {
                return new LoadedArchiveRecord
                {
                    Key = key,
                    Result = new HolmasPlayerArchiveLoadResult
                    {
                        Status = HolmasPlayerArchiveLoadStatus.Missing,
                        FailureReason = $"档案不存在: {key}",
                    }
                };
            }

            byte[] bytes = await _persistence.LoadAsync(key).ConfigureAwait(false);
            if (bytes == null || bytes.Length == 0)
            {
                return new LoadedArchiveRecord
                {
                    Key = key,
                    Result = new HolmasPlayerArchiveLoadResult
                    {
                        Status = HolmasPlayerArchiveLoadStatus.InvalidData,
                        FailureReason = $"档案为空: {key}",
                    }
                };
            }

            string json = System.Text.Encoding.UTF8.GetString(bytes);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new LoadedArchiveRecord
                {
                    Key = key,
                    Result = new HolmasPlayerArchiveLoadResult
                    {
                        Status = HolmasPlayerArchiveLoadStatus.InvalidData,
                        FailureReason = $"档案 JSON 为空: {key}",
                    }
                };
            }

            HolmasPlayerArchiveRoot archive;
            try
            {
                archive = JsonUtility.FromJson<HolmasPlayerArchiveRoot>(json);
            }
            catch (Exception ex)
            {
                return new LoadedArchiveRecord
                {
                    Key = key,
                    Result = new HolmasPlayerArchiveLoadResult
                    {
                        Status = HolmasPlayerArchiveLoadStatus.InvalidData,
                        FailureReason = $"解析档案失败: {key}, {ex.Message}",
                    }
                };
            }

            if (archive == null)
            {
                return new LoadedArchiveRecord
                {
                    Key = key,
                    Result = new HolmasPlayerArchiveLoadResult
                    {
                        Status = HolmasPlayerArchiveLoadStatus.InvalidData,
                        FailureReason = $"解析档案结果为空: {key}",
                    }
                };
            }

            if (!string.Equals(archive.SchemaVersion, _schemaVersion, StringComparison.Ordinal))
            {
                return new LoadedArchiveRecord
                {
                    Key = key,
                    Result = new HolmasPlayerArchiveLoadResult
                    {
                        Status = HolmasPlayerArchiveLoadStatus.SchemaMismatch,
                        FailureReason = $"档案版本不兼容: {archive.SchemaVersion}",
                    }
                };
            }

            archive.PlayerId = string.IsNullOrWhiteSpace(archive.PlayerId) ? DefaultPlayerId : archive.PlayerId;
            archive.Progression = archive.Progression ?? new HolmasProgressionArchiveData();
            archive.TaskBar = archive.TaskBar ?? new HolmasTaskBarArchiveData();

            return new LoadedArchiveRecord
            {
                Key = key,
                Bytes = bytes,
                Result = new HolmasPlayerArchiveLoadResult
                {
                    Status = HolmasPlayerArchiveLoadStatus.Success,
                    Archive = archive,
                }
            };
        }

        private async Task TryHealReplicaAsync(string targetKey, byte[] sourceBytes, HolmasPlayerArchiveRoot sourceArchive)
        {
            if (sourceBytes == null || sourceBytes.Length == 0 || sourceArchive == null)
            {
                return;
            }

            bool saved = await _persistence.SaveAsync(targetKey, sourceBytes).ConfigureAwait(false);
            if (!saved)
            {
                _logger?.LogWarning(
                    "HolmasLocalMockServerGateway: 自愈副本失败。target={0}, revision={1}",
                    targetKey,
                    sourceArchive.Revision);
                return;
            }

            _logger?.LogInfo(
                "HolmasLocalMockServerGateway: 已自愈副本。target={0}, revision={1}",
                targetKey,
                sourceArchive.Revision);
        }

        private static LoadedArchiveRecord SelectAuthoritativeRecord(LoadedArchiveRecord primaryRecord, LoadedArchiveRecord backupRecord)
        {
            HolmasPlayerArchiveRoot primaryArchive = primaryRecord?.Result?.Archive;
            HolmasPlayerArchiveRoot backupArchive = backupRecord?.Result?.Archive;
            if (IsArchiveNewer(backupArchive, primaryArchive))
            {
                return backupRecord;
            }

            return primaryRecord;
        }

        private static bool IsArchiveNewer(HolmasPlayerArchiveRoot candidate, HolmasPlayerArchiveRoot baseline)
        {
            if (candidate == null)
            {
                return false;
            }

            if (baseline == null)
            {
                return true;
            }

            if (candidate.Revision != baseline.Revision)
            {
                return candidate.Revision > baseline.Revision;
            }

            return candidate.SavedAtUtcMilliseconds > baseline.SavedAtUtcMilliseconds;
        }

        private static HolmasPlayerArchiveLoadResult ResolveLoadFallback(
            HolmasPlayerArchiveLoadResult primaryResult,
            HolmasPlayerArchiveLoadResult backupResult)
        {
            if (primaryResult != null && primaryResult.Status == HolmasPlayerArchiveLoadStatus.SchemaMismatch)
            {
                return primaryResult;
            }

            if (backupResult != null && backupResult.Status == HolmasPlayerArchiveLoadStatus.SchemaMismatch)
            {
                return backupResult;
            }

            if ((primaryResult != null && primaryResult.Status == HolmasPlayerArchiveLoadStatus.InvalidData) ||
                (backupResult != null && backupResult.Status == HolmasPlayerArchiveLoadStatus.InvalidData))
            {
                return primaryResult != null && primaryResult.Status == HolmasPlayerArchiveLoadStatus.InvalidData
                    ? primaryResult
                    : backupResult;
            }

            return new HolmasPlayerArchiveLoadResult
            {
                Status = HolmasPlayerArchiveLoadStatus.Missing,
                FailureReason = primaryResult?.FailureReason ?? backupResult?.FailureReason ?? "本地模拟服务器档案不存在。",
            };
        }
    }
}

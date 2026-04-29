using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.Progression;
using App.Shared.Contracts;
using App.Shared.Holmas.PlayerData;

namespace App.HotUpdate.Holmas.PlayerData
{
    /// <summary>
    /// 将 gameplay runtime 的状态变化合并成单飞持久化写入。
    /// </summary>
    public sealed class HolmasPlayerArchiveSyncService : IHolmasPlayerArchiveDrain, IDisposable
    {
        private readonly HolmasGameplayRuntime _runtime;
        private readonly IHolmasPlayerArchiveGateway _gateway;
        private readonly HolmasPlayerArchiveMapper _mapper;
        private readonly IHolmasUtcClock _clock;
        private readonly IAppLogger _logger;
        private readonly string _playerId;
        private readonly string _schemaVersion;

        private bool _dirty;
        private bool _saveInFlight;
        private bool _pendingDirty;
        private bool _disposed;
        private long _lastSavedRevision;
        private Task<bool> _activeFlushTask = Task.FromResult(true);
        private HolmasTutorialSuspendedSessionArchiveData _tutorialSuspendedSession;

        public HolmasPlayerArchiveSyncService(
            HolmasGameplayRuntime runtime,
            IHolmasPlayerArchiveGateway gateway,
            HolmasPlayerArchiveMapper mapper,
            IHolmasUtcClock clock,
            IAppLogger logger,
            string playerId,
            string schemaVersion,
            long initialRevision)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _logger = logger;
            _playerId = string.IsNullOrWhiteSpace(playerId) ? HolmasLocalMockServerGateway.DefaultPlayerId : playerId;
            _schemaVersion = string.IsNullOrWhiteSpace(schemaVersion) ? HolmasLocalMockServerGateway.DefaultSchemaVersion : schemaVersion;
            _lastSavedRevision = Math.Max(0L, initialRevision);

            _runtime.StateChanged += OnRuntimeStateChanged;
        }

        public long LastSavedRevision => _lastSavedRevision;

        public bool IsDirty => _dirty || _pendingDirty || _saveInFlight;

        public HolmasTutorialSuspendedSessionArchiveData TutorialSuspendedSession =>
            _mapper.CloneTutorialSuspendedSession(_tutorialSuspendedSession);

        public Task<bool> SaveTutorialSuspendedSessionAsync(HolmasTutorialSuspendedSessionArchiveData session, string reason)
        {
            _tutorialSuspendedSession = _mapper.CloneTutorialSuspendedSession(session);
            MarkDirty(reason);
            return FlushIfNeededAsync();
        }

        public Task<bool> ClearTutorialSuspendedSessionAsync(string reason)
        {
            _tutorialSuspendedSession = null;
            MarkDirty(reason);
            return FlushIfNeededAsync();
        }

        public void SetTutorialSuspendedSession(HolmasTutorialSuspendedSessionArchiveData session)
        {
            _tutorialSuspendedSession = _mapper.CloneTutorialSuspendedSession(session);
        }

        public Task<bool> MarkDirtyAndSaveAsync(string reason)
        {
            MarkDirty(reason);
            return FlushIfNeededAsync();
        }

        public void MarkDirty(string reason)
        {
            _dirty = true;
            if (_saveInFlight)
            {
                _pendingDirty = true;
            }

            _logger?.LogInfo("HolmasPlayerArchiveSyncService: 标记档案已脏。reason={0}", reason);
        }

        public Task<bool> FlushAsync()
        {
            return FlushIfNeededAsync();
        }

        public Task<bool> FlushIfNeededAsync()
        {
            if (_saveInFlight)
            {
                return _activeFlushTask;
            }

            if (!_dirty)
            {
                return Task.FromResult(true);
            }

            _activeFlushTask = FlushLoopAsync();
            return _activeFlushTask;
        }

        private async Task<bool> FlushLoopAsync()
        {
            _saveInFlight = true;
            bool saveSucceeded = true;
            try
            {
                do
                {
                    _pendingDirty = false;
                    _dirty = false;
                    HolmasPlayerArchiveRoot archive = _mapper.ExportArchive(
                        _runtime,
                        _playerId,
                        _schemaVersion,
                        _lastSavedRevision + 1,
                        _clock.UtcNowMilliseconds);
                    archive.TutorialSuspendedSession = _mapper.CloneTutorialSuspendedSession(_tutorialSuspendedSession);
                    HolmasPlayerArchiveSaveResult result = await _gateway.SaveAsync(archive).ConfigureAwait(false);
                    if (result == null || !result.Success)
                    {
                        _dirty = true;
                        saveSucceeded = false;
                        _logger?.LogWarning(
                            "HolmasPlayerArchiveSyncService: 保存档案失败，保留 dirty。reason={0}",
                            result?.FailureReason ?? "unknown");
                        break;
                    }

                    _lastSavedRevision = archive.Revision;
                }
                while (_pendingDirty || _dirty);

                return saveSucceeded && !_dirty && !_pendingDirty;
            }
            catch (Exception ex)
            {
                _dirty = true;
                _logger?.LogError("HolmasPlayerArchiveSyncService: Flush 期间出现异常，保留 dirty。{0}", ex);
                return false;
            }
            finally
            {
                _saveInFlight = false;
                _activeFlushTask = Task.FromResult(!_dirty && !_pendingDirty);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _runtime.StateChanged -= OnRuntimeStateChanged;
        }

        private void OnRuntimeStateChanged(HolmasGameplayRuntimeStateChangeReason reason)
        {
            _ = QueueBackgroundFlushAsync(reason.ToString());
        }

        private async Task QueueBackgroundFlushAsync(string reason)
        {
            bool flushed = await MarkDirtyAndSaveAsync(reason).ConfigureAwait(false);
            if (!flushed && !_disposed)
            {
                _logger?.LogWarning("HolmasPlayerArchiveSyncService: 后台冲刷未完全成功，等待下次重试。reason={0}", reason);
            }
        }
    }
}

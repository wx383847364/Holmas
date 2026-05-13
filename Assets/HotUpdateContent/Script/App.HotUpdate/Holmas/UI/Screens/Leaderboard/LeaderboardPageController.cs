using System;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Leaderboards;
using App.HotUpdate.Holmas.UI.Core;
using App.Shared.Contracts;
using App.Shared.Holmas.Leaderboards;

namespace App.HotUpdate.Holmas.UI.Screens.Leaderboard
{
    public sealed class LeaderboardPageController : UiPageController
    {
        private LeaderboardView _view;
        private LeaderboardBindings _bindings;
        private HolmasLeaderboardType _selectedType = HolmasLeaderboardType.Level;
        private LeaderboardVm _lastVm;
        private int _refreshRequestVersion;

        protected override void OnCreate()
        {
            _view = RootObject != null ? RootObject.GetComponent<LeaderboardView>() : null;
            if (_view == null)
            {
                throw new System.InvalidOperationException("LeadbroadPanel prefab 缺少 LeaderboardView，请在 prefab 静态挂载。");
            }
        }

        protected override void OnBind()
        {
            _bindings = LeaderboardBindings.Resolve(BindingResolver);
            if (_bindings == null || !_bindings.HasRequiredBindings)
            {
                throw new System.InvalidOperationException("LeadbroadPanel 缺少完整 UiReferenceCollector 静态绑定，请先在 prefab 侧补齐 LeaderboardGeneratedBindings.Manifest 对应节点。");
            }

            _view?.Bind(_bindings);
            INetClient netClient = Root != null && Root.Context != null && Root.Context.ServiceContainer != null
                ? Root.Context.ServiceContainer.Get<INetClient>()
                : null;
            _view?.SetAssetsRuntime(Root != null && Root.Context != null ? Root.Context.AssetsRuntime : null, netClient);
            _view?.SetBackAction(OnBackClicked);
            _view?.SetRewardAction(OnRewardClicked);
            _view?.SetTabActions(OnLevelToggleChanged, OnWeeklyToggleChanged, OnDailyToggleChanged);
        }

        protected override void OnOpen(object payload)
        {
            _selectedType = payload is HolmasLeaderboardType type && type != HolmasLeaderboardType.Unknown
                ? type
                : HolmasLeaderboardType.Level;
            _ = RefreshAsync("加载中...");
        }

        protected override void OnResume()
        {
            _ = RefreshAsync(null);
        }

        protected override void OnDestroy()
        {
            _refreshRequestVersion++;
            _view?.SetBackAction(null);
            _view?.SetRewardAction(null);
            _view?.SetTabActions(null, null, null);
            _view?.ReleaseAssets();
        }

        private void OnBackClicked()
        {
            _ = ScreenService.BackAsync();
        }

        private void OnRewardClicked()
        {
            if (_lastVm != null)
            {
                _lastVm.Status = "排名奖励暂未开放。";
                _view?.Render(_lastVm);
            }
        }

        private void OnLevelToggleChanged(bool isOn)
        {
            if (isOn && _view != null && !_view.IsSyncingToggles)
            {
                SetType(HolmasLeaderboardType.Level);
            }
        }

        private void OnWeeklyToggleChanged(bool isOn)
        {
            if (isOn && _view != null && !_view.IsSyncingToggles)
            {
                SetType(HolmasLeaderboardType.WeeklyCatsFound);
            }
        }

        private void OnDailyToggleChanged(bool isOn)
        {
            if (isOn && _view != null && !_view.IsSyncingToggles)
            {
                SetType(HolmasLeaderboardType.DailyTaskIncome);
            }
        }

        private void SetType(HolmasLeaderboardType type)
        {
            if (_selectedType == type)
            {
                return;
            }

            _selectedType = type;
            _ = RefreshAsync("加载中...");
        }

        private async Task RefreshAsync(string status)
        {
            int requestVersion = ++_refreshRequestVersion;
            HolmasLeaderboardType requestType = _selectedType;
            HolmasLeaderboardTrackerService tracker = Root?.Context?.ServiceContainer != null
                ? Root.Context.ServiceContainer.Get<HolmasLeaderboardTrackerService>()
                : null;

            if (!string.IsNullOrWhiteSpace(status))
            {
                _view?.Render(BuildVm(null, status));
            }

            if (tracker == null)
            {
                if (IsCurrentRequest(requestVersion, requestType))
                {
                    _view?.Render(BuildVm(null, "排行榜服务不可用。"));
                }

                return;
            }

            try
            {
                HolmasLeaderboardResponse response = await tracker.GetLeaderboardAsync(requestType);
                if (!IsCurrentRequest(requestVersion, requestType))
                {
                    return;
                }

                if (response == null || !response.Success)
                {
                    _view?.Render(BuildVm(_lastVm, response?.FailureReason ?? "排行榜请求失败。"));
                    return;
                }

                _lastVm = BuildVm(response, string.Empty);
                _view?.Render(_lastVm);
            }
            catch (Exception ex)
            {
                if (IsCurrentRequest(requestVersion, requestType))
                {
                    _view?.Render(BuildVm(_lastVm, "排行榜请求失败：" + ex.Message));
                }
            }
        }

        private bool IsCurrentRequest(int requestVersion, HolmasLeaderboardType requestType)
        {
            return requestVersion == _refreshRequestVersion && requestType == _selectedType;
        }

        private LeaderboardVm BuildVm(object source, string status)
        {
            if (source is HolmasLeaderboardResponse response)
            {
                return new LeaderboardVm
                {
                    SelectedType = response.Type,
                    Title = string.IsNullOrWhiteSpace(response.DisplayName) ? GetFallbackTitle(response.Type) : response.DisplayName,
                    PeriodText = BuildPeriodText(response),
                    Status = status ?? string.Empty,
                    Entries = response.Entries ?? Array.Empty<HolmasLeaderboardEntry>(),
                    SelfEntry = response.SelfEntry,
                };
            }

            LeaderboardVm fallback = source as LeaderboardVm ?? _lastVm;
            return new LeaderboardVm
            {
                SelectedType = _selectedType,
                Title = fallback != null ? fallback.Title : GetFallbackTitle(_selectedType),
                PeriodText = fallback != null ? fallback.PeriodText : string.Empty,
                Status = status ?? string.Empty,
                Entries = fallback != null ? fallback.Entries : Array.Empty<HolmasLeaderboardEntry>(),
                SelfEntry = fallback?.SelfEntry,
            };
        }

        private static string BuildPeriodText(HolmasLeaderboardResponse response)
        {
            if (response == null)
            {
                return string.Empty;
            }

            if (response.Type == HolmasLeaderboardType.Level)
            {
                return "长期榜";
            }

            return string.IsNullOrWhiteSpace(response.PeriodKey) ? string.Empty : "周期：" + response.PeriodKey;
        }

        private static string GetFallbackTitle(HolmasLeaderboardType type)
        {
            switch (type)
            {
                case HolmasLeaderboardType.WeeklyCatsFound:
                    return "寻猫周榜";
                case HolmasLeaderboardType.DailyTaskIncome:
                    return "收入日榜";
                default:
                    return "等级总榜";
            }
        }
    }
}

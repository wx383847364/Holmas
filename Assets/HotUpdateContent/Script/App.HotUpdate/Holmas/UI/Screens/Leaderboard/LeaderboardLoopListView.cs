using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Tool;
using App.Shared.Contracts;
using App.Shared.Holmas.Leaderboards;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace App.HotUpdate.Holmas.UI.Screens.Leaderboard
{
    public sealed class LeaderboardLoopListView : MonoBehaviour
    {
        private const int BufferBefore = 2;
        private const int BufferAfter = 2;
        private const float FallbackItemHeight = 100f;
        private const float FallbackSpacing = 60f;
        private const float TopPadding = 12f;

        private readonly List<HolmasLeaderboardEntry> _items = new List<HolmasLeaderboardEntry>();
        private readonly List<PooledItem> _pool = new List<PooledItem>();
        private ScrollRect _scrollRect;
        private RectTransform _content;
        private RectTransform _itemTemplate;
        private float _itemHeight = FallbackItemHeight;
        private float _itemStride = FallbackItemHeight + FallbackSpacing;
        private float _firstItemX;
        private float _firstItemY;
        private int _firstVisibleIndex = -1;
        private bool _configured;
        private HolmasCatSpriteLoader _catSpriteLoader;
        private LeaderboardAvatarSpriteLoader _avatarSpriteLoader;

        public int ItemCount => _items.Count;

        public int PoolCapacity => _pool.Count;

        public int ActivePoolItemCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _pool.Count; i++)
                {
                    if (_pool[i].Rect != null && _pool[i].Rect.gameObject.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int FirstVisibleIndex => Mathf.Max(0, _firstVisibleIndex);

        public void Configure(ScrollRect scrollRect, RectTransform content, RectTransform itemTemplate)
        {
            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
            }

            _scrollRect = scrollRect;
            _content = content;
            _itemTemplate = itemTemplate;
            _configured = _scrollRect != null && _content != null && _itemTemplate != null;
            if (!_configured)
            {
                return;
            }

            RectTransform scrollRectRect = _scrollRect.transform as RectTransform;
            _scrollRect.content = _content;
            _scrollRect.viewport = scrollRectRect;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.onValueChanged.AddListener(OnScrollValueChanged);

            EnsureViewportClip(scrollRectRect);
            DisableLayoutDrivers(_content);
            DisableContentClip(_content);
            NormalizeContentTransform(scrollRectRect, _content);
            ResolveMetrics();
            HideDesignTimePlayerRows();
            _itemTemplate.gameObject.SetActive(false);
            UpdateContentHeight();
            EnsurePool();
            _firstVisibleIndex = -1;
            RefreshVisible(force: true);
        }

        public void SetItems(IReadOnlyList<HolmasLeaderboardEntry> entries, bool resetScroll = true)
        {
            _items.Clear();
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i] != null)
                    {
                        _items.Add(entries[i]);
                    }
                }
            }

            if (!_configured)
            {
                return;
            }

            UpdateContentHeight();
            EnsurePool();
            if (resetScroll)
            {
                _content.anchoredPosition = Vector2.zero;
            }

            _firstVisibleIndex = -1;
            RefreshVisible(force: true);
        }

        public void SetCatSpriteLoader(HolmasCatSpriteLoader catSpriteLoader)
        {
            if (catSpriteLoader == null)
            {
                ReleaseAvatarItems();
            }

            _catSpriteLoader = catSpriteLoader;
            for (int i = 0; i < _pool.Count; i++)
            {
                _pool[i].View.SetCatSpriteLoader(_catSpriteLoader);
                _pool[i].BoundIndex = -1;
            }

            RefreshVisible(force: true);
        }

        public void ReleaseAvatarItems()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                _pool[i].View.ReleaseAvatar();
            }
        }

        public void SetAvatarSpriteLoader(LeaderboardAvatarSpriteLoader avatarSpriteLoader)
        {
            _avatarSpriteLoader = avatarSpriteLoader;
            for (int i = 0; i < _pool.Count; i++)
            {
                _pool[i].View.SetAvatarSpriteLoader(_avatarSpriteLoader);
                _pool[i].BoundIndex = -1;
            }

            RefreshVisible(force: true);
        }

        public void RefreshVisible(bool force = false)
        {
            if (!_configured)
            {
                return;
            }

            if (_items.Count == 0)
            {
                for (int i = 0; i < _pool.Count; i++)
                {
                    _pool[i].SetActive(false);
                }

                _firstVisibleIndex = 0;
                return;
            }

            int firstIndex = CalculateFirstVisibleIndex();
            if (!force && firstIndex == _firstVisibleIndex)
            {
                return;
            }

            _firstVisibleIndex = firstIndex;
            for (int i = 0; i < _pool.Count; i++)
            {
                int dataIndex = firstIndex + i;
                PooledItem pooled = _pool[i];
                if (dataIndex < 0 || dataIndex >= _items.Count)
                {
                    pooled.SetActive(false);
                    continue;
                }

                pooled.SetActive(true);
                pooled.Rect.anchoredPosition = new Vector2(_firstItemX, _firstItemY - dataIndex * _itemStride);
                if (force || pooled.BoundIndex != dataIndex)
                {
                    pooled.View.Bind(_items[dataIndex], showRank: true);
                    pooled.BoundIndex = dataIndex;
                }
            }
        }

        public void SetScrollOffsetForTests(float scrollY)
        {
            if (_content == null)
            {
                return;
            }

            _content.anchoredPosition = new Vector2(0f, Mathf.Max(0f, scrollY));
            RefreshVisible(force: true);
        }

        private void OnScrollValueChanged(Vector2 _)
        {
            RefreshVisible();
        }

        private int CalculateFirstVisibleIndex()
        {
            float scrollY = _content != null ? Mathf.Max(0f, _content.anchoredPosition.y) : 0f;
            int first = Mathf.FloorToInt(scrollY / Mathf.Max(1f, _itemStride)) - BufferBefore;
            int maxFirst = Mathf.Max(0, _items.Count - Mathf.Max(1, _pool.Count));
            return Mathf.Clamp(first, 0, maxFirst);
        }

        private void EnsurePool()
        {
            int desiredCount = CalculateDesiredPoolCount();
            while (_pool.Count < desiredCount)
            {
                GameObject instance = Object.Instantiate(_itemTemplate.gameObject, _content, false);
                instance.name = "PlayerInfo_Runtime_" + _pool.Count.ToString("00");
                instance.AddComponent<LeaderboardPooledItemMarker>();

                RectTransform rect = instance.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = _itemTemplate.pivot;
                rect.sizeDelta = _itemTemplate.sizeDelta;
                rect.localScale = Vector3.one;

                var pooled = new PooledItem(rect, new LeaderboardItemView(rect));
                pooled.View.SetCatSpriteLoader(_catSpriteLoader);
                pooled.View.SetAvatarSpriteLoader(_avatarSpriteLoader);
                pooled.SetActive(false);
                _pool.Add(pooled);
            }

            for (int i = 0; i < _pool.Count; i++)
            {
                _pool[i].SetActive(i < desiredCount);
                if (i >= desiredCount)
                {
                    _pool[i].BoundIndex = -1;
                }
            }
        }

        private int CalculateDesiredPoolCount()
        {
            if (_items.Count == 0)
            {
                return 0;
            }

            float viewportHeight = GetViewportHeight();
            int visibleCount = Mathf.Max(1, Mathf.CeilToInt(viewportHeight / Mathf.Max(1f, _itemStride)));
            return Mathf.Min(_items.Count, visibleCount + BufferBefore + BufferAfter);
        }

        private void UpdateContentHeight()
        {
            if (_content == null)
            {
                return;
            }

            Vector2 size = _content.sizeDelta;
            size.x = 0f;
            size.y = Mathf.Max(GetViewportHeight(), _items.Count * Mathf.Max(1f, _itemStride));
            _content.sizeDelta = size;
        }

        private float GetViewportHeight()
        {
            RectTransform viewport = _scrollRect != null && _scrollRect.viewport != null ? _scrollRect.viewport : _scrollRect.transform as RectTransform;
            if (viewport == _content && _scrollRect != null)
            {
                viewport = _scrollRect.transform as RectTransform;
            }
            float height = viewport != null ? viewport.rect.height : 0f;
            if (height <= 1f && viewport != null)
            {
                height = Mathf.Abs(viewport.sizeDelta.y);
            }

            return height > 1f ? height : 818f;
        }

        private void ResolveMetrics()
        {
            _itemHeight = _itemTemplate.rect.height > 1f ? _itemTemplate.rect.height : Mathf.Abs(_itemTemplate.sizeDelta.y);
            if (_itemHeight <= 1f)
            {
                _itemHeight = FallbackItemHeight;
            }

            _itemStride = ResolveStrideFromDesignRows();
            if (_itemStride <= 1f)
            {
                _itemStride = _itemHeight + FallbackSpacing;
            }

            _firstItemX = 0f;
            _firstItemY = -TopPadding - ResolveTemplateVisualTop();
        }

        private float ResolveStrideFromDesignRows()
        {
            var yPositions = new List<float>();
            for (int i = 0; i < _content.childCount; i++)
            {
                RectTransform child = _content.GetChild(i) as RectTransform;
                if (child == null || !child.name.StartsWith("PlayerInfo", StringComparison.Ordinal))
                {
                    continue;
                }

                yPositions.Add(child.anchoredPosition.y);
            }

            yPositions.Sort();
            float best = 0f;
            for (int i = 1; i < yPositions.Count; i++)
            {
                float delta = Mathf.Abs(yPositions[i] - yPositions[i - 1]);
                if (delta > 1f && (best <= 1f || delta < best))
                {
                    best = delta;
                }
            }

            return best;
        }

        private float ResolveTemplateVisualTop()
        {
            if (_itemTemplate == null)
            {
                return _itemHeight * (1f - 0.5f);
            }

            float visualTop = float.NegativeInfinity;
            var corners = new Vector3[4];
            for (int i = 0; i < _itemTemplate.childCount; i++)
            {
                RectTransform child = _itemTemplate.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                child.GetLocalCorners(corners);
                for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    Vector3 world = child.TransformPoint(corners[cornerIndex]);
                    Vector3 local = _itemTemplate.InverseTransformPoint(world);
                    if (local.y > visualTop)
                    {
                        visualTop = local.y;
                    }
                }
            }

            if (float.IsNegativeInfinity(visualTop))
            {
                return _itemHeight * (1f - _itemTemplate.pivot.y);
            }

            return Mathf.Max(0f, visualTop);
        }

        private static void DisableLayoutDrivers(RectTransform content)
        {
            LayoutGroup[] groups = content.GetComponents<LayoutGroup>();
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i].enabled = false;
            }

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
            }
        }

        private static void EnsureViewportClip(RectTransform viewport)
        {
            if (viewport == null)
            {
                return;
            }

            RectMask2D rectMask = viewport.GetComponent<RectMask2D>() ?? viewport.gameObject.AddComponent<RectMask2D>();
            rectMask.enabled = true;
        }

        private static void DisableContentClip(RectTransform content)
        {
            if (content == null)
            {
                return;
            }

            Mask mask = content.GetComponent<Mask>();
            if (mask != null)
            {
                mask.enabled = false;
            }

            RectMask2D rectMask = content.GetComponent<RectMask2D>();
            if (rectMask != null)
            {
                rectMask.enabled = false;
            }
        }

        private static void NormalizeContentTransform(RectTransform viewport, RectTransform content)
        {
            if (viewport == null || content == null)
            {
                return;
            }

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.localScale = Vector3.one;
        }

        private void HideDesignTimePlayerRows()
        {
            for (int i = 0; i < _content.childCount; i++)
            {
                Transform child = _content.GetChild(i);
                if (child == null || child.GetComponent<LeaderboardPooledItemMarker>() != null)
                {
                    continue;
                }

                if (child == _itemTemplate || child.name.StartsWith("PlayerInfo", StringComparison.Ordinal))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private sealed class PooledItem
        {
            public PooledItem(RectTransform rect, LeaderboardItemView view)
            {
                Rect = rect;
                View = view;
                BoundIndex = -1;
            }

            public RectTransform Rect { get; }

            public LeaderboardItemView View { get; }

            public int BoundIndex { get; set; }

            public void SetActive(bool active)
            {
                if (Rect != null && Rect.gameObject.activeSelf != active)
                {
                    Rect.gameObject.SetActive(active);
                }
            }
        }
    }

    public sealed class LeaderboardPooledItemMarker : MonoBehaviour
    {
    }

    public sealed class LeaderboardItemView
    {
        private readonly RectTransform _root;
        private readonly LeaderboardTextSlot _rankText;
        private readonly LeaderboardTextSlot _nameText;
        private readonly LeaderboardTextSlot _scoreText;
        private readonly Image _headIcon;
        private readonly Image _frameIcon;
        private readonly Image _leadIcon;
        private HolmasCatSpriteLoader _catSpriteLoader;
        private LeaderboardAvatarSpriteLoader _avatarSpriteLoader;

        public LeaderboardItemView(RectTransform root)
        {
            _root = root;
            LeaderboardItemBindingSurface surface = root != null ? root.GetComponent<LeaderboardItemBindingSurface>() : null;
            if (surface == null)
            {
                _rankText = default;
                _nameText = default;
                _scoreText = default;
                _headIcon = null;
                _frameIcon = null;
                _leadIcon = null;
                return;
            }

            _rankText = surface.RankText;
            _nameText = surface.NameText;
            _scoreText = surface.ScoreText;
            _headIcon = surface.HeadIcon;
            _frameIcon = surface.FrameIcon;
            _leadIcon = surface.LeadIcon;
        }

        public void SetCatSpriteLoader(HolmasCatSpriteLoader catSpriteLoader)
        {
            _catSpriteLoader = catSpriteLoader;
        }

        public void SetAvatarSpriteLoader(LeaderboardAvatarSpriteLoader avatarSpriteLoader)
        {
            _avatarSpriteLoader = avatarSpriteLoader;
        }

        public void ReleaseAvatar()
        {
            if (_avatarSpriteLoader != null)
            {
                _avatarSpriteLoader.Clear(_headIcon);
                return;
            }

            _catSpriteLoader?.Clear(_headIcon);
        }

        public void Bind(HolmasLeaderboardEntry entry, bool showRank)
        {
            if (_root == null)
            {
                return;
            }

            bool hasEntry = entry != null;
            if (!hasEntry)
            {
                _rankText.SetText(showRank ? "未上榜" : string.Empty);
                _nameText.SetText("--");
                _scoreText.SetText("0");
                BindAvatar(null);
                return;
            }

            _rankText.SetText(showRank ? FormatRank(entry.Rank) : string.Empty);
            _nameText.SetText(ResolveDisplayName(entry));
            _scoreText.SetText(entry.Score.ToString());
            BindAvatar(entry);
            SetImageVisible(_frameIcon, true);
            SetImageVisible(_leadIcon, true);
        }

        private void BindAvatar(HolmasLeaderboardEntry entry)
        {
            if (_headIcon == null)
            {
                return;
            }

            string iconPath = ResolveAvatarIconPath(entry);
            if (_avatarSpriteLoader != null)
            {
                _avatarSpriteLoader.Bind(_headIcon, entry);
                return;
            }

            _catSpriteLoader?.Bind(
                _headIcon,
                new HolmasCatVisualVm
                {
                    CatId = entry != null && !string.IsNullOrWhiteSpace(entry.PlayerId) ? entry.PlayerId : "leaderboard-default-avatar",
                    CatName = entry != null ? entry.DisplayName ?? string.Empty : string.Empty,
                    IconPath = iconPath,
                });
        }

        private static string ResolveAvatarIconPath(HolmasLeaderboardEntry entry)
        {
            if (entry == null)
            {
                return HolmasLeaderboardAvatarDefaults.DefaultAvatarIconPath;
            }

            if (!string.IsNullOrWhiteSpace(entry.WechatAvatarUrl))
            {
                return string.IsNullOrWhiteSpace(entry.AvatarIconPath)
                    ? HolmasLeaderboardAvatarDefaults.DefaultAvatarIconPath
                    : entry.AvatarIconPath;
            }

            return string.IsNullOrWhiteSpace(entry.AvatarIconPath)
                ? HolmasLeaderboardAvatarDefaults.DefaultAvatarIconPath
                : entry.AvatarIconPath;
        }

        private static void SetImageVisible(Image image, bool visible)
        {
            if (image != null)
            {
                image.enabled = visible;
            }
        }

        private static string ResolveDisplayName(HolmasLeaderboardEntry entry)
        {
            if (entry == null)
            {
                return "--";
            }

            if (!string.IsNullOrWhiteSpace(entry.DisplayName))
            {
                return entry.DisplayName;
            }

            return !string.IsNullOrWhiteSpace(entry.PlayerId) ? entry.PlayerId : "--";
        }

        private static string FormatRank(int rank)
        {
            return rank > 0 ? "No." + rank : "未上榜";
        }

    }

    public sealed class LeaderboardAvatarSpriteLoader : IDisposable
    {
        private readonly HolmasCatSpriteLoader _fallbackLoader;
        private readonly INetClient _netClient;
        private readonly Dictionary<string, Sprite> _loadedSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> _loadedTextures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, Task<Sprite>> _loadingTasks = new Dictionary<string, Task<Sprite>>(StringComparer.Ordinal);
        private int _requestGeneration;
        private bool _disposed;

        public LeaderboardAvatarSpriteLoader(HolmasCatSpriteLoader fallbackLoader, INetClient netClient)
        {
            _fallbackLoader = fallbackLoader;
            _netClient = netClient;
        }

        public void Bind(Image image, HolmasLeaderboardEntry entry)
        {
            if (image == null)
            {
                return;
            }

            string avatarUrl = entry != null ? entry.WechatAvatarUrl : string.Empty;
            int generation = _requestGeneration;
            string requestKey = generation + "|" + (entry != null ? entry.PlayerId ?? string.Empty : string.Empty) + "|" + avatarUrl + "|" + ResolveAvatarIconPath(entry);
            LeaderboardAvatarImageRequest request =
                image.GetComponent<LeaderboardAvatarImageRequest>() ?? image.gameObject.AddComponent<LeaderboardAvatarImageRequest>();
            request.RequestKey = requestKey;

            BindFallback(image, entry);
            if (string.IsNullOrWhiteSpace(avatarUrl) || _netClient == null || _disposed)
            {
                return;
            }

            InvalidateFallbackRequest(image, requestKey);

            if (_loadedSprites.TryGetValue(avatarUrl, out Sprite loadedSprite) && loadedSprite != null)
            {
                ApplyRemoteSprite(image, loadedSprite);
                return;
            }

            _ = LoadAndApplyAsync(image, request, requestKey, avatarUrl, entry, generation);
        }

        public void Clear(Image image)
        {
            if (image == null)
            {
                return;
            }

            LeaderboardAvatarImageRequest request = image.GetComponent<LeaderboardAvatarImageRequest>();
            if (request != null)
            {
                request.RequestKey = "invalid|" + _requestGeneration;
            }

            _fallbackLoader?.Clear(image);
        }

        public void Dispose()
        {
            _requestGeneration++;
            _disposed = true;
            _loadingTasks.Clear();
            foreach (Sprite sprite in _loadedSprites.Values)
            {
                if (sprite != null)
                {
                    DestroyUnityObject(sprite);
                }
            }

            foreach (Texture2D texture in _loadedTextures.Values)
            {
                if (texture != null)
                {
                    DestroyUnityObject(texture);
                }
            }

            _loadedSprites.Clear();
            _loadedTextures.Clear();
        }

        private void BindFallback(Image image, HolmasLeaderboardEntry entry)
        {
            string iconPath = ResolveAvatarIconPath(entry);
            _fallbackLoader?.Bind(
                image,
                new HolmasCatVisualVm
                {
                    CatId = entry != null && !string.IsNullOrWhiteSpace(entry.PlayerId) ? entry.PlayerId : "leaderboard-default-avatar",
                    CatName = entry != null ? entry.DisplayName ?? string.Empty : string.Empty,
                    IconPath = iconPath,
                });
        }

        private static string ResolveAvatarIconPath(HolmasLeaderboardEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.AvatarIconPath))
            {
                return HolmasLeaderboardAvatarDefaults.DefaultAvatarIconPath;
            }

            return entry.AvatarIconPath;
        }

        private async Task LoadAndApplyAsync(
            Image image,
            LeaderboardAvatarImageRequest request,
            string requestKey,
            string avatarUrl,
            HolmasLeaderboardEntry entry,
            int generation)
        {
            Sprite sprite;
            try
            {
                sprite = await GetOrLoadSpriteAsync(avatarUrl, generation);
            }
            catch (Exception)
            {
                sprite = null;
            }

            if (image == null ||
                request == null ||
                request.RequestKey != requestKey ||
                generation != _requestGeneration ||
                _disposed ||
                sprite == null)
            {
                if (image != null &&
                    request != null &&
                    request.RequestKey == requestKey &&
                    generation == _requestGeneration &&
                    !_disposed &&
                    sprite == null)
                {
                    BindFallback(image, entry);
                }

                return;
            }

            ApplyRemoteSprite(image, sprite);
        }

        private Task<Sprite> GetOrLoadSpriteAsync(string avatarUrl, int generation)
        {
            if (_loadedSprites.TryGetValue(avatarUrl, out Sprite sprite) && sprite != null)
            {
                return Task.FromResult(sprite);
            }

            string taskKey = generation + "|" + avatarUrl;
            if (_loadingTasks.TryGetValue(taskKey, out Task<Sprite> existingTask))
            {
                return existingTask;
            }

            Task<Sprite> task = LoadRemoteSpriteAsync(avatarUrl, generation, taskKey);
            _loadingTasks[taskKey] = task;
            return task;
        }

        private async Task<Sprite> LoadRemoteSpriteAsync(string avatarUrl, int generation, string taskKey)
        {
            try
            {
                if (_netClient == null || generation != _requestGeneration || _disposed)
                {
                    return null;
                }

                TransportResponse response = await _netClient.SendRequestAsync(avatarUrl, "GET");
                if (response == null || !response.IsSuccess || response.Data == null || response.Data.Length == 0)
                {
                    return null;
                }

                if (generation != _requestGeneration || _disposed)
                {
                    return null;
                }

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(response.Data))
                {
                    DestroyUnityObject(texture);
                    return null;
                }

                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));
                _loadedTextures[avatarUrl] = texture;
                _loadedSprites[avatarUrl] = sprite;
                return sprite;
            }
            finally
            {
                _loadingTasks.Remove(taskKey);
            }
        }

        private static void DestroyUnityObject(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }

        private static void ApplyRemoteSprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = Color.white;
            image.enabled = true;
        }

        private static void InvalidateFallbackRequest(Image image, string requestKey)
        {
            HolmasCatImageRequest fallbackRequest = image != null ? image.GetComponent<HolmasCatImageRequest>() : null;
            if (fallbackRequest != null)
            {
                fallbackRequest.RequestKey = "leaderboard-remote|" + requestKey;
            }
        }
    }

    public sealed class LeaderboardAvatarImageRequest : MonoBehaviour
    {
        public string RequestKey = string.Empty;
    }
}

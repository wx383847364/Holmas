using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.HotUpdate.Holmas.Tasks.Config;
using App.Shared.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Core
{
    public sealed class HolmasCatVisualVm
    {
        public static readonly IReadOnlyDictionary<string, HolmasCatVisualVm> EmptyLookup =
            new Dictionary<string, HolmasCatVisualVm>(StringComparer.Ordinal);

        public string CatId = string.Empty;
        public string CatName = string.Empty;
        public string IconPath = string.Empty;

        public static HolmasCatVisualVm CreateFallback(string catId)
        {
            return new HolmasCatVisualVm
            {
                CatId = catId ?? string.Empty,
                CatName = string.Empty,
                IconPath = string.Empty,
            };
        }
    }

    public sealed class HolmasCatVisualResolver
    {
        private readonly IHolmasTaskCatalog _taskCatalog;

        public HolmasCatVisualResolver(IHolmasTaskCatalog taskCatalog)
        {
            _taskCatalog = taskCatalog;
        }

        public HolmasCatVisualVm Resolve(string catId)
        {
            string normalizedCatId = catId ?? string.Empty;
            if (_taskCatalog != null &&
                !string.IsNullOrWhiteSpace(normalizedCatId) &&
                _taskCatalog.TryGetCat(normalizedCatId, out HolmasCatDefinition definition) &&
                definition != null)
            {
                return new HolmasCatVisualVm
                {
                    CatId = normalizedCatId,
                    CatName = definition.CatName ?? string.Empty,
                    IconPath = definition.IconPath ?? string.Empty,
                };
            }

            return HolmasCatVisualVm.CreateFallback(normalizedCatId);
        }
    }

    public sealed class HolmasCatSpriteLoader : IDisposable
    {
        private static Sprite _fallbackSprite;

        private readonly Dictionary<string, LoadedSpriteEntry> _loadedSprites =
            new Dictionary<string, LoadedSpriteEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, Task<Sprite>> _loadingTasks =
            new Dictionary<string, Task<Sprite>>(StringComparer.Ordinal);
        private IAssetsRuntime _assetsRuntime;

        public HolmasCatSpriteLoader(IAssetsRuntime assetsRuntime)
        {
            _assetsRuntime = assetsRuntime;
        }

        public void SetAssetsRuntime(IAssetsRuntime assetsRuntime)
        {
            if (ReferenceEquals(_assetsRuntime, assetsRuntime))
            {
                return;
            }

            DisposeCachedHandles();
            _assetsRuntime = assetsRuntime;
        }

        public void Bind(Image image, HolmasCatVisualVm visual, bool dimmed = false)
        {
            if (image == null)
            {
                return;
            }

            string catId = visual != null ? visual.CatId : string.Empty;
            string iconPath = visual != null ? visual.IconPath : string.Empty;
            string requestKey = $"{catId}|{iconPath}|{dimmed}";
            HolmasCatImageRequest request = image.GetComponent<HolmasCatImageRequest>() ?? image.gameObject.AddComponent<HolmasCatImageRequest>();
            request.RequestKey = requestKey;
            image.enabled = true;

            if (TryGetLoadedSprite(iconPath, out Sprite sprite))
            {
                ApplyLoadedSprite(image, sprite, dimmed);
                return;
            }

            ApplyFallback(image, catId, dimmed);
            if (string.IsNullOrWhiteSpace(iconPath) || _assetsRuntime == null)
            {
                return;
            }

            _ = LoadAndApplyAsync(image, request, requestKey, iconPath, catId, dimmed);
        }

        public void Clear(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = null;
            image.enabled = false;
            image.color = Color.white;
            image.preserveAspect = true;
        }

        public void Dispose()
        {
            DisposeCachedHandles();
            _assetsRuntime = null;
        }

        private bool TryGetLoadedSprite(string iconPath, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrWhiteSpace(iconPath))
            {
                return false;
            }

            if (!_loadedSprites.TryGetValue(iconPath, out LoadedSpriteEntry entry) || entry == null || entry.Sprite == null)
            {
                return false;
            }

            sprite = entry.Sprite;
            return true;
        }

        private async Task LoadAndApplyAsync(
            Image image,
            HolmasCatImageRequest request,
            string requestKey,
            string iconPath,
            string catId,
            bool dimmed)
        {
            Sprite sprite = await GetOrLoadSpriteAsync(iconPath);
            if (image == null || request == null || request.RequestKey != requestKey)
            {
                return;
            }

            if (sprite != null)
            {
                ApplyLoadedSprite(image, sprite, dimmed);
                return;
            }

            ApplyFallback(image, catId, dimmed);
        }

        private Task<Sprite> GetOrLoadSpriteAsync(string iconPath)
        {
            if (TryGetLoadedSprite(iconPath, out Sprite sprite))
            {
                return Task.FromResult(sprite);
            }

            if (_loadingTasks.TryGetValue(iconPath, out Task<Sprite> existingTask))
            {
                return existingTask;
            }

            Task<Sprite> loadTask = LoadSpriteAsync(iconPath);
            _loadingTasks[iconPath] = loadTask;
            return loadTask;
        }

        private async Task<Sprite> LoadSpriteAsync(string iconPath)
        {
            try
            {
                if (_assetsRuntime == null || string.IsNullOrWhiteSpace(iconPath))
                {
                    return null;
                }

                IAssetHandle handle = await _assetsRuntime.LoadAssetAsync(iconPath);
                if (handle == null || handle.AssetObject == null)
                {
                    handle?.Release();
                    return null;
                }

                Sprite sprite = ExtractSprite(handle.AssetObject);
                if (sprite == null)
                {
                    handle.Release();
                    return null;
                }

                _loadedSprites[iconPath] = new LoadedSpriteEntry
                {
                    Handle = handle,
                    Sprite = sprite,
                };

                return sprite;
            }
            finally
            {
                _loadingTasks.Remove(iconPath);
            }
        }

        private static Sprite ExtractSprite(UnityEngine.Object assetObject)
        {
            if (assetObject is Sprite sprite)
            {
                return sprite;
            }

            if (assetObject is Texture2D texture)
            {
                return Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));
            }

            return null;
        }

        private static void ApplyLoadedSprite(Image image, Sprite sprite, bool dimmed)
        {
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = dimmed ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        }

        private static void ApplyFallback(Image image, string catId, bool dimmed)
        {
            image.sprite = GetFallbackSprite();
            image.preserveAspect = false;
            image.color = GetFallbackColor(catId, dimmed);
        }

        private static Sprite GetFallbackSprite()
        {
            if (_fallbackSprite == null)
            {
                Texture2D texture = Texture2D.whiteTexture;
                _fallbackSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));
            }

            return _fallbackSprite;
        }

        private static Color GetFallbackColor(string catId, bool dimmed)
        {
            int hash = string.IsNullOrWhiteSpace(catId) ? 0 : catId.GetHashCode();
            float r = 0.45f + ((hash & 0xFF) / 255f) * 0.35f;
            float g = 0.45f + (((hash >> 8) & 0xFF) / 255f) * 0.35f;
            float b = 0.45f + (((hash >> 16) & 0xFF) / 255f) * 0.35f;
            float alpha = dimmed ? 0.3f : 0.95f;
            return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), alpha);
        }

        private void DisposeCachedHandles()
        {
            foreach (LoadedSpriteEntry entry in _loadedSprites.Values)
            {
                entry?.Handle?.Release();
            }

            _loadedSprites.Clear();
            _loadingTasks.Clear();
        }

        private sealed class LoadedSpriteEntry
        {
            public IAssetHandle Handle;
            public Sprite Sprite;
        }
    }

    public sealed class HolmasCatImageRequest : MonoBehaviour
    {
        public string RequestKey = string.Empty;
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Shared.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.Holmas.UI.Screens.Tutorial
{
    public sealed class TutorialVisualSpriteLoader : IDisposable
    {
        private static Sprite _fallbackSprite;

        private readonly Dictionary<string, LoadedSpriteEntry> _loadedSprites =
            new Dictionary<string, LoadedSpriteEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, Task<Sprite>> _loadingTasks =
            new Dictionary<string, Task<Sprite>>(StringComparer.Ordinal);
        private IAssetsRuntime _assetsRuntime;

        public TutorialVisualSpriteLoader(IAssetsRuntime assetsRuntime)
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

        public void Bind(Image image, string assetPath, Color fallbackColor)
        {
            if (image == null)
            {
                return;
            }

            string requestKey = assetPath ?? string.Empty;
            TutorialVisualImageRequest request = image.GetComponent<TutorialVisualImageRequest>() ??
                                                 image.gameObject.AddComponent<TutorialVisualImageRequest>();
            request.RequestKey = requestKey;
            image.enabled = true;
            ApplyFallback(image, fallbackColor);

            if (string.IsNullOrWhiteSpace(assetPath) || _assetsRuntime == null)
            {
                return;
            }

            if (TryGetLoadedSprite(assetPath, out Sprite loaded))
            {
                ApplyLoadedSprite(image, loaded);
                return;
            }

            _ = LoadAndApplyAsync(image, request, requestKey, assetPath, fallbackColor);
        }

        public void Dispose()
        {
            DisposeCachedHandles();
            _assetsRuntime = null;
        }

        private bool TryGetLoadedSprite(string assetPath, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            if (!_loadedSprites.TryGetValue(assetPath, out LoadedSpriteEntry entry) || entry == null || entry.Sprite == null)
            {
                return false;
            }

            sprite = entry.Sprite;
            return true;
        }

        private async Task LoadAndApplyAsync(
            Image image,
            TutorialVisualImageRequest request,
            string requestKey,
            string assetPath,
            Color fallbackColor)
        {
            Sprite sprite = await GetOrLoadSpriteAsync(assetPath);
            if (image == null || request == null || request.RequestKey != requestKey)
            {
                return;
            }

            if (sprite != null)
            {
                ApplyLoadedSprite(image, sprite);
                return;
            }

            ApplyFallback(image, fallbackColor);
        }

        private Task<Sprite> GetOrLoadSpriteAsync(string assetPath)
        {
            if (TryGetLoadedSprite(assetPath, out Sprite sprite))
            {
                return Task.FromResult(sprite);
            }

            if (_loadingTasks.TryGetValue(assetPath, out Task<Sprite> existing))
            {
                return existing;
            }

            Task<Sprite> task = LoadSpriteAsync(assetPath);
            _loadingTasks[assetPath] = task;
            return task;
        }

        private async Task<Sprite> LoadSpriteAsync(string assetPath)
        {
            try
            {
                IAssetHandle handle = await _assetsRuntime.LoadAssetAsync(assetPath);
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

                _loadedSprites[assetPath] = new LoadedSpriteEntry
                {
                    Handle = handle,
                    Sprite = sprite,
                };
                return sprite;
            }
            catch
            {
                return null;
            }
            finally
            {
                _loadingTasks.Remove(assetPath);
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

        private static void ApplyLoadedSprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
        }

        private static void ApplyFallback(Image image, Color fallbackColor)
        {
            image.sprite = GetFallbackSprite();
            image.color = fallbackColor;
            image.preserveAspect = false;
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

    public sealed class TutorialVisualImageRequest : MonoBehaviour
    {
        public string RequestKey = string.Empty;
    }
}

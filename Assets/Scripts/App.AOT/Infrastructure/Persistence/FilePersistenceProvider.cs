using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using App.Shared.Contracts;

namespace App.AOT.Infrastructure.Persistence
{
    /// <summary>
    /// 文件系统持久化提供者
    /// </summary>
    public class FilePersistenceProvider : IPersistence
    {
        private readonly string _basePath;

        public FilePersistenceProvider(string basePath = null)
        {
            _basePath = basePath ?? Application.persistentDataPath;
        }

        private string GetFilePath(string key)
        {
            return Path.Combine(_basePath, $"{key}.dat");
        }

        public async Task<bool> SaveAsync(string key, byte[] data)
        {
            try
            {
                var filePath = GetFilePath(key);
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(filePath, data).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"FilePersistenceProvider: Failed to save {key}: {ex}");
                return false;
            }
        }

        public async Task<byte[]> LoadAsync(string key)
        {
            try
            {
                var filePath = GetFilePath(key);
                if (!File.Exists(filePath))
                {
                    return null;
                }

                return await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"FilePersistenceProvider: Failed to load {key}: {ex}");
                return null;
            }
        }

        public async Task<bool> DeleteAsync(string key)
        {
            try
            {
                var filePath = GetFilePath(key);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"FilePersistenceProvider: Failed to delete {key}: {ex}");
                return false;
            }
        }

        public bool Exists(string key)
        {
            var filePath = GetFilePath(key);
            return File.Exists(filePath);
        }
    }
}

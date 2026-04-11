using System.Threading.Tasks;
using App.Shared.Holmas.RuntimeData;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Core
{
    /// <summary>
    /// 为 Battle 预留轻量 2D 世界宿主，不引入独立场景切换。
    /// </summary>
    public sealed class HolmasBattleWorldHost : IBattleWorldHost
    {
        private const string AnchorRootName = "HolmasBattleWorldAnchor";
        private const string SessionRootPrefix = "BattleWorldSession";

        private GameObject _anchorRoot;
        private GameObject _sessionRoot;

        public Task PrepareAsync(LevelSnapshot snapshot)
        {
            GameObject anchorRoot = EnsureAnchorRoot();
            ReleaseSessionRoot();

            string mapId = snapshot != null && !string.IsNullOrWhiteSpace(snapshot.MapId)
                ? snapshot.MapId
                : "unknown";
            _sessionRoot = new GameObject(SessionRootPrefix + "_" + mapId);
            _sessionRoot.transform.SetParent(anchorRoot.transform, false);
            _sessionRoot.SetActive(false);
            return Task.CompletedTask;
        }

        public void Show()
        {
            GameObject anchorRoot = EnsureAnchorRoot();
            anchorRoot.SetActive(true);
            if (_sessionRoot != null)
            {
                _sessionRoot.SetActive(true);
            }
        }

        public void Hide()
        {
            if (_sessionRoot != null)
            {
                _sessionRoot.SetActive(false);
            }
        }

        public void Release()
        {
            ReleaseSessionRoot();
            if (_anchorRoot != null)
            {
                _anchorRoot.SetActive(false);
            }
        }

        private GameObject EnsureAnchorRoot()
        {
            if (_anchorRoot != null)
            {
                return _anchorRoot;
            }

            _anchorRoot = GameObject.Find(AnchorRootName);
            if (_anchorRoot == null)
            {
                _anchorRoot = new GameObject(AnchorRootName);
                Object.DontDestroyOnLoad(_anchorRoot);
            }

            _anchorRoot.SetActive(false);
            return _anchorRoot;
        }

        private void ReleaseSessionRoot()
        {
            if (_sessionRoot == null)
            {
                return;
            }

            Object.Destroy(_sessionRoot);
            _sessionRoot = null;
        }
    }
}

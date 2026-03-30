using System;
using System.Collections.Generic;
using System.Linq;

namespace App.HotUpdate.Holmas.Tasks.Config
{
    /// <summary>
    /// 地图配置定义。
    /// 这里只描述静态配置输入，不承载运行时状态。
    /// </summary>
    [Serializable]
    public sealed class HolmasMapDefinition
    {
        public string MapId = string.Empty;
        public string TerrainPath = string.Empty;
        public int CatCountMin;
        public int CatCountMax;
    }

    /// <summary>
    /// 地图配置仓库接口。
    /// 地图选择与请求生成只通过这个接口读取配置，不直接依赖表格或 AssetObject。
    /// </summary>
    public interface IHolmasMapCatalog
    {
        bool TryGetMap(string mapId, out HolmasMapDefinition definition);
    }

    /// <summary>
    /// 纯内存版地图配置仓库。
    /// </summary>
    public sealed class HolmasMapCatalog : IHolmasMapCatalog
    {
        private readonly Dictionary<string, HolmasMapDefinition> _maps = new Dictionary<string, HolmasMapDefinition>(StringComparer.Ordinal);

        public HolmasMapCatalog()
        {
        }

        public HolmasMapCatalog(IEnumerable<HolmasMapDefinition> maps)
        {
            SetMaps(maps);
        }

        public void SetMaps(IEnumerable<HolmasMapDefinition> maps)
        {
            _maps.Clear();
            if (maps == null)
            {
                return;
            }

            foreach (var map in maps.Where(item => item != null && !string.IsNullOrEmpty(item.MapId)))
            {
                _maps[map.MapId] = map;
            }
        }

        public bool TryGetMap(string mapId, out HolmasMapDefinition definition)
        {
            return _maps.TryGetValue(mapId ?? string.Empty, out definition);
        }
    }
}

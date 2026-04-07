using System.Collections.Generic;
using UnityEngine;

namespace Zeus.Framework.UI
{
    public class UISpriteList : MonoBehaviour
    {
        //记录了Inspector中当前List的数量
        public int Count = 0;
        //记录了Inspector中List的改动前数量
        public int LastCount = 0;

        public List<UISpriteElement> List = new List<UISpriteElement>();
        private Dictionary<string, Sprite> elementsDict;

        private void Awake()
        {
            elementsDict = new Dictionary<string, Sprite>();
            for (int i= 0;i<List.Count;i++)
            {
                elementsDict[List[i].name] = List[i].sprite;
            }
        }

        /// <summary>
        /// 获得指定的Sprite
        /// </summary>
        /// <param name="name">Sprite的名字</param>
        /// <returns></returns>
        public Sprite GetSprite(string name)
        {
            Sprite returnSprite = null;
            if (elementsDict.ContainsKey(name))
            {
                returnSprite = elementsDict[name];
            }
            return returnSprite;
        }

    }

    [System.Serializable]
    public class UISpriteElement
    {
        public string name;
        public UnityEngine.Sprite sprite;
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Zeus.Framework.UI
{
    public class UIObjectList : MonoBehaviour
    {
        //记录了Inspector中当前List的数量
        public int Count = 0;
        //记录了Inspector中List的改动前数量
        public int LastCount = 0;

        public List<UIObjectElement> List = new List<UIObjectElement>();
        private Dictionary<string, UnityEngine.Object> elementsDict;

        private void Awake()
        {
            elementsDict = new Dictionary<string, UnityEngine.Object>();
            for (int i = 0; i < List.Count; i++)
            {
                elementsDict[List[i].name] = List[i].obj;
            }
        }

        /// <summary>
        /// 获得指定的Object
        /// </summary>
        /// <param name="name">Object的名字</param>
        /// <returns></returns>
        public UnityEngine.Object GetObject(string name)
        {
            UnityEngine.Object returnObject = null;
            if (elementsDict.ContainsKey(name))
            {
                returnObject = elementsDict[name];
            }
            return returnObject;
        }

    }

    [System.Serializable]
    public class UIObjectElement
    {
        public string name;
        public UnityEngine.Object obj;
    }
}

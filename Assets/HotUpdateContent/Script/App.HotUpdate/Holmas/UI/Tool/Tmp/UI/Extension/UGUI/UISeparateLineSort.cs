using System.Collections.Generic;
using UnityEngine;

namespace Zeus.Framework.UI
{
    //根据传入RectTransform长度计算分隔条的位置
    //使用此脚本为了适配不同分辨率下的屏幕
    public class UISeparateLineSort : MonoBehaviour
    {
        //分隔条的数量
        [SerializeField]
        private int count;
        //分隔条prefab
        [SerializeField]
        private Transform lineTransform;

        void Start()
        {
            var rectTransform = gameObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                int singleWidth = (int)(rectTransform.rect.width / (count+1));
                if (lineTransform != null)
                {
                    lineTransform.localPosition = new Vector3(singleWidth , 0, 0);
                    for (int i = 1; i < count; i++)
                    {
                        GameObject gameObj = UnityEngine.Object.Instantiate(lineTransform.gameObject, transform) as GameObject;
                        gameObj.transform.localPosition = new Vector3(singleWidth * (i + 1), 0, 0);                        
                    }
                }

            }

        }
    }
}

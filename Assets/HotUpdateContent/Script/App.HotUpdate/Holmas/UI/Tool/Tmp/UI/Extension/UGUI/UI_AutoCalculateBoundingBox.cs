using System.Collections.Generic;
using UnityEngine;

//根据所有子节点的宽高 计算 父节点包围盒的宽高
public class UI_AutoCalculateBoundingBox : UI_CalculateBoundingBox
{
    //是否要底端对齐(新增后显示最后一个)
    public bool BottomAlign = false;
    private Dictionary<int, Vector2> cacheChildSize = new Dictionary<int, Vector2>();
    public void CalculateBoundingBox(int instanceid , Vector2 addChildSize)
    {
        if (cacheChildSize.ContainsKey(instanceid) && cacheChildSize[instanceid] == addChildSize)
            return;
        cacheChildSize[instanceid] = addChildSize;
        switch (needCalcType)
        {
            case NeedCalcType.All:
                break;
            case NeedCalcType.Horizontal:
                break;
            case NeedCalcType.Vertical:
                Vector2 vector2 = rectTransform.sizeDelta;
                vector2.y = 0;
                foreach (var kvp in cacheChildSize)
                {
                    vector2.y += kvp.Value.y;
                }
                rectTransform.sizeDelta = vector2;

                if (BottomAlign && vector2.y > StandardValue.y)
                {
                    Vector2 anchoredPosition = rectTransform.anchoredPosition;
                    anchoredPosition.y = vector2.y - StandardValue.y;
                    rectTransform.anchoredPosition = anchoredPosition;
                }

                break;
        }
    }

    public void ReleaseCalculateBoundingBox(int instanceid)
    {
        if (cacheChildSize.ContainsKey(instanceid))
        {
            cacheChildSize.Remove(instanceid);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

//根据某些选择的子节点的宽高 计算 父节点包围盒的宽高
public class UI_AutoCalculateBoundingBoxByChild : UI_CalculateBoundingBox
{
    //横向边框
    public float H_Boder = 0f;
    //纵向边框
    public float V_Boder = 0f;
    //元素间距
    public float CellSpace = 0f;
    //需要计算的子元素
    public List<RectTransform> needCalcRectList = new List<RectTransform>();
    //需要监听边框改变的元素
    public List<Custom_ContentSizeFilter> changesRectList = new List<Custom_ContentSizeFilter>();
    //父节点上的脚本
    UI_AutoCalculateBoundingBox ui_AutoCalculateBoundingBox = null;

    public override void Awake()
    {
        base.Awake();
        for (int i = 0; i < changesRectList.Count; i++)
        {
            changesRectList[i].OnRectTransformDimensionsChangeAction = OnRectTransformDimensionsChanges;
        }
    }

    private void OnRectTransformDimensionsChanges()
    {
        switch (needCalcType)
        {
            case NeedCalcType.All:
                break;
            case NeedCalcType.Horizontal:
                break;
            case NeedCalcType.Vertical:
                Vector2 vector2 = rectTransform.sizeDelta;
                vector2.y = 0f;
                vector2.y += V_Boder * 2;
                Vector2 tempV2 = Vector2.zero;
                for (int i = 0; i < needCalcRectList.Count; i++)
                {
                    tempV2 = needCalcRectList[i].sizeDelta;
                    vector2.y += tempV2.y;
                }
                vector2.y += CellSpace * Mathf.Max(0, (needCalcRectList.Count - 1));

                vector2.y = Mathf.Max(vector2.y, StandardValue.y);
                rectTransform.sizeDelta = vector2;
                break;
        }
        
        ui_AutoCalculateBoundingBox = transform.GetComponentInParent<UI_AutoCalculateBoundingBox>();
        if (ui_AutoCalculateBoundingBox != null)
        {
            ui_AutoCalculateBoundingBox.CalculateBoundingBox(gameObject.GetInstanceID(),rectTransform.sizeDelta);
        }        
    }

    public override void OnDestroy()
    {        
        for (int i = 0; i < changesRectList.Count; i++)
        {
            changesRectList[i].OnRectTransformDimensionsChangeAction = null;
        }
        if (ui_AutoCalculateBoundingBox != null)
        {
            ui_AutoCalculateBoundingBox.ReleaseCalculateBoundingBox(gameObject.GetInstanceID());
        }
        base.OnDestroy();
    }
}
using System.Collections.Generic;
using UnityEngine;

//根据子节点的宽高 计算 父节点包围盒的宽高
public class UI_CalculateBoundingBox : MonoBehaviour
{
    public enum NeedCalcType
    {
        Horizontal,
        Vertical,
        All
    }

    //标准宽高
    public Vector2 StandardValue; 
    //宽高具体需要计算的类型
    public NeedCalcType needCalcType = NeedCalcType.Vertical;


    protected RectTransform rectTransform;
    public virtual void Awake()
    {
        rectTransform = GetComponent<RectTransform>();      
    }

    public virtual void OnDestroy()
    {
        
    }

}

using UnityEngine;
using UnityEngine.UI;

//使用其他物体的高设置自身的高
//并且去刷新LayoutGroup脚本(如果有设置)
public class UIResetSizeHeightByOther : MonoBehaviour
{
    //其他物体
    public RectTransform OtherObj;
    //要刷新的Layout
    public HorizontalOrVerticalLayoutGroup HvLayout;

    //最小高度
    public float MinHeight = 0f;
    //上下边框
    public float Border = 0f;

    private RectTransform rectTransform;
    private Custom_ContentSizeFilter ccsizeFilter;
    void Awake()
    {
        rectTransform = transform as RectTransform;
        if (OtherObj != null)
        {
            ccsizeFilter = OtherObj.GetComponent<Custom_ContentSizeFilter>();
            if (ccsizeFilter != null)
            {
                ccsizeFilter.OnRectTransformDimensionsChangeAction = OnRectTransformDimensionsChangeAction;
            }
            OnRectTransformDimensionsChangeAction();
        }
    }

    //重新设置自身的高
    void OnRectTransformDimensionsChangeAction()
    {
        if (OtherObj == null) return;
        float newValue = OtherObj.sizeDelta.y;
        newValue += Border * 2;
        newValue = Mathf.Max(newValue, MinHeight);
        Vector2 v2 = rectTransform.sizeDelta;
        v2.y = newValue;
        rectTransform.sizeDelta = v2;
        if (HvLayout != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(HvLayout.transform as RectTransform);
        }
    }
}

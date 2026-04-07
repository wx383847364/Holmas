using System;
using UnityEngine;
using UnityEngine.UI;
[RequireComponent(typeof(ContentSizeFitter))]
public class CopySize : MonoBehaviour
{
    public RectTransform needCopyRectTransform;
    public float appendWidth = 10;
    public float appendHeight = 11;
    private RectTransform rectTransform;
    private ContentSizeFitter contentSizeFitter;
    private void OnEnable()
    {
        rectTransform = GetComponent<RectTransform>();
        contentSizeFitter = GetComponent<ContentSizeFitter>();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (rectTransform == null) return;
        float width = 0;
        if (contentSizeFitter.horizontalFit == ContentSizeFitter.FitMode.PreferredSize)
        {
            width = LayoutUtility.GetPreferredWidth(rectTransform) + appendWidth;
        }
        else
        {
            width = rectTransform.sizeDelta.x + appendWidth;
        }

        float height = 0;
        if (contentSizeFitter.verticalFit == ContentSizeFitter.FitMode.PreferredSize)
        {
            height = LayoutUtility.GetPreferredHeight(rectTransform) + appendHeight;
        }
        else
        {
            height = rectTransform.sizeDelta.y + appendHeight;
        }
        if (needCopyRectTransform != null)
        {
            needCopyRectTransform.sizeDelta = new Vector2(width, height);
        }
    }

#if UNITY_EDITOR
    private void Update()
    {
        OnRectTransformDimensionsChange();
    }
#endif
}

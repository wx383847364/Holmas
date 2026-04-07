using System;
using UnityEngine;
using UnityEngine.UI;
public class Custom_ContentSizeFilter : ContentSizeFitter
{
    public enum UseType
    {
        None,
        ChatBoxWidth,
        CityIconList,
    }
    private const int CHATBOXWIDTH_MAXWIDTH = 350;

    public int CITYICONLIST_HEIGHT_OFFSET = 20;
    public int CITYICONLIST_COLUMN_MAX = 3;

    public UseType useType = UseType.None;
    public System.Action OnRectTransformDimensionsChangeAction = null;
    private RectTransform rectTransform;
    private GridLayoutGroup gridLayoutGroup;
    private FitMode oldHorizontalFit = FitMode.PreferredSize;
    protected override void OnRectTransformDimensionsChange()
    {
        if (useType == UseType.ChatBoxWidth)
        {
            if (rectTransform == null)
                rectTransform = gameObject.GetComponent<RectTransform>();
            if (rectTransform.sizeDelta.x > CHATBOXWIDTH_MAXWIDTH)
            {
                oldHorizontalFit = horizontalFit;
                horizontalFit = FitMode.Unconstrained;
                Vector2 vector2 = rectTransform.sizeDelta;
                vector2.x = CHATBOXWIDTH_MAXWIDTH;
                rectTransform.sizeDelta = vector2;
                SetLayoutHorizontal();
                Canvas.ForceUpdateCanvases();
                horizontalFit = oldHorizontalFit;
            }
        }
        else if (useType == UseType.CityIconList)
        {
            if (rectTransform == null)
            {
                rectTransform = gameObject.GetComponent<RectTransform>();
            }
            if (gridLayoutGroup == null)
            {
                gridLayoutGroup = gameObject.GetComponent<GridLayoutGroup>();
            }

            int activeChildCount = GetActiveChildCount();
            if (activeChildCount < CITYICONLIST_COLUMN_MAX)
            {
                gridLayoutGroup.constraintCount = activeChildCount;
            }
            else
            {
                gridLayoutGroup.constraintCount = CITYICONLIST_COLUMN_MAX;
            }

            if (verticalFit != FitMode.PreferredSize)
            {
                return;
            }
            Vector2 vector2;
            verticalFit = FitMode.Unconstrained;
            vector2 = rectTransform.sizeDelta;
            vector2.y += CITYICONLIST_HEIGHT_OFFSET;
            rectTransform.sizeDelta = vector2;
            SetLayoutVertical();
            Canvas.ForceUpdateCanvases();
            verticalFit = FitMode.PreferredSize;
        }
        if (OnRectTransformDimensionsChangeAction != null)
        {
            OnRectTransformDimensionsChangeAction();
        }
    }

    private int GetActiveChildCount()
    {
        int i = 0;
        foreach (Transform child in transform)
        {
            GameObject obj = child.gameObject;
            if (obj.activeInHierarchy)
            {
                i++;
            }
        }

        return i;
    }
}

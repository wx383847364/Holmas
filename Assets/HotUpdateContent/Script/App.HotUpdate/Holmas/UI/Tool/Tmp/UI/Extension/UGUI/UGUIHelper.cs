using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class UGUIHelper
{
    //拿到RectTransform真正的宽高
    //适配锚点，锚线，锚框等情况
    public static Vector2 GetRealSize(this RectTransform rect)
    {
        Vector2 realSize = rect.sizeDelta;
        bool width_Stretch = false;
        bool height_Stretch = false;
        if (rect.anchorMin.x != rect.anchorMax.x)
        {
            width_Stretch = true;
        }
        if (rect.anchorMin.y != rect.anchorMax.y)
        {
            height_Stretch = true;
        }

        if (!width_Stretch && !height_Stretch)
        {
            //进入此代表anchor为九宫格锚点
            return realSize;
        }

        RectTransform rootCanvasRectTransform = GetRootRectTransform(rect);
        if (rootCanvasRectTransform == null)
            return Vector2.zero;

        if (width_Stretch)
        {
            realSize.x += rootCanvasRectTransform.sizeDelta.x;
        }
        if (height_Stretch)
        {
            realSize.y += rootCanvasRectTransform.sizeDelta.y;
        }
        return realSize;
    }

    public static Vector2 GetScreenSpaceSize(this RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        canvas = canvas.rootCanvas;
        var size = canvas.scaleFactor * rect.sizeDelta;
        return size;
    }

    //拿到根Canvas的RectTransform
    public static RectTransform GetRootRectTransform(this RectTransform rect)
    {
        if (rect == null) return null;
        if (rect.parent == null) return null;
        Canvas canvas = rect.parent.GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        return canvas.rootCanvas.GetComponent<RectTransform>();
    }

    //拿到根Canvas的RectTransform
    public static RectTransform GetRootRectTransform(this Transform rect)
    {
        if (rect == null) return null;
        if (rect.parent == null) return null;
        Canvas canvas = rect.parent.GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        return canvas.rootCanvas.GetComponent<RectTransform>();
    }
}


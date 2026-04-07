using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 设计安全区域面板（适配iPhone X）
/// 文件名 SafeAreaPanel.cs
/// </summary>
public class SafeAreaPanel : MonoBehaviour
{
    private DeviceOrientation curDeviceOrientation = DeviceOrientation.Unknown;
    private RectTransform target;
    [SerializeField]
    private List<RectTransform> excludeTransform;

#if UNITY_EDITOR
    //[SerializeField]
    private bool Simulate_X = false;
#endif


    void Awake()
    {
        target = GetComponent<RectTransform>();        
    }
    private void Start()
    {
        ApplySafeArea(DeviceOrientation.LandscapeLeft);
    }

    void Update()
    {
        if (Input.deviceOrientation == DeviceOrientation.LandscapeLeft || Input.deviceOrientation == DeviceOrientation.LandscapeRight)
        {
            if (Input.deviceOrientation != curDeviceOrientation)
            {
                curDeviceOrientation = Input.deviceOrientation;
                ApplySafeArea(curDeviceOrientation);
            }
        }

    }

    void ApplySafeArea(DeviceOrientation deviceOrientation)
    {
        curDeviceOrientation = deviceOrientation;
        var area = SafeAreaUtils.Get();

#if UNITY_EDITOR

        /*
        iPhone X 横持手机方向:
        iPhone X 分辨率
        2436 x 1125 px

        safe area
        2172 x 1062 px

        左右边距分别
        132px

        底边距 (有Home条)
        63px

        顶边距
        0px
        */

        float Xwidth = 2436f;
        float Xheight = 1125f;
        float Margin = 132f;
        //float InsetsBottom = 63f;

        if ((Screen.width == (int)Xwidth && Screen.height == (int)Xheight)
        || (Screen.width == 812 && Screen.height == 375))
        {
            Simulate_X = true;
        }

        if (Simulate_X)
        {
            var insets = area.width * Margin / Xwidth;
            var positionOffset = new Vector2(insets, 0);
            var sizeOffset = new Vector2(insets * 2, 0);
            area.position = area.position + positionOffset;
            area.size = area.size - sizeOffset;
        }
#endif

        var anchorMin = area.position;
        var anchorMax = area.position + area.size;
        if (deviceOrientation == DeviceOrientation.LandscapeLeft)
        {
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x = 1f;
            anchorMax.y = 1f;
        }
        if (deviceOrientation == DeviceOrientation.LandscapeRight)
        {
            anchorMin.x = 0f;
            anchorMin.y = 0f;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;
        }
        target.anchorMin = anchorMin;
        target.anchorMax = anchorMax;

        float resultX = 1.0f / Mathf.Abs(anchorMax.x - anchorMin.x);
        float resultY = 1.0f / Mathf.Abs(anchorMax.y - anchorMin.y);

        //排除项，不受刘海影响的UI
        for (int i = 0; i < excludeTransform.Count; i++)
        {
            if (excludeTransform[i] == null)
            {
                continue;
            }
            //修改位移
            Vector2 anchoredPosition = excludeTransform[i].anchoredPosition;
            anchoredPosition += new Vector2(target.localPosition.x * -1, target.localPosition.y * -1);
            excludeTransform[i].anchoredPosition = anchoredPosition;
            //修改大小
            Vector3 scale = excludeTransform[i].localScale;
            scale.x = resultX;
            scale.y = resultY;
            excludeTransform[i].localScale = scale;
        }
    }
}
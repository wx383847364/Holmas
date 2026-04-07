using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// iPhone X适配工具类
/// 文件名 SafeAreaUtils.cs
/// </summary>
public class SafeAreaUtils
{
#if UNITY_IOS
    [DllImport("__Internal")]
    private static extern void GetSafeArea(out float x, out float y, out float w, out float h);
#endif


    /// <summary>
    /// 获取iPhone X 等苹果未来的异性屏幕的安全区域Safe are
    /// </summary>
    /// <param name="showInsetsBottom"></param>
    /// <returns></returns>
    public static Rect Get()
    {
        float x, y, w, h;
#if UNITY_IOS && !UNITY_EDITOR
        GetSafeArea(out x, out y, out w, out h);
        if (IsIPhoneX())
        {
            //iPhoneX系列刘海高度占用屏幕和SafeArea之差(单边距)的2/3，
            w = (Screen.width - w) / 3.0f + w;
            x = x * 2 / 3;
        }
#else
        x = 0;
        y = 0;
        w = Screen.width;
        h = Screen.height;
#endif
        return new Rect(x, y, w, h);
    }

#if UNITY_IOS && !UNITY_EDITOR
    public static bool IsIPhoneX()
    {
        //X XS XS XSMAX
        if (Screen.width == 2436 ||
            Screen.width == 1792 ||
            Screen.width == 2688 )
        {
            return true;
        }
        return false;      
    }
#endif
}
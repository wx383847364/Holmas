using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UGUI根节点
/// </summary>
[ExecuteInEditMode]
public class UGUIRoot : MonoBehaviour
{
    [SerializeField]
    private bool updateMode = false;
    public static UGUIRoot Instance;

    [SerializeField]
    private Camera camera;
    public static Camera UICamera
    {
        get
        {
            return UGUIRoot.Instance.camera;
        }
    }

    [SerializeField]
    private Vector2 designResolutionRatio = new Vector2(1280f, 720f);
    public static Vector2 DesignResolutionRatio
    {
        get
        {
            if (UGUIRoot.Instance == null) return new Vector2(1280f, 720f);
            return UGUIRoot.Instance.designResolutionRatio;
        }
    }

    [Range(0f, 100f)]
    [SerializeField]
    private float safeWidth = 70f;
    public static float SafeWidth
    {
        get
        {
            if (UGUIRoot.Instance == null) return 70f;
            return UGUIRoot.Instance.safeWidth;
        }
        set
        {
            UGUIRoot.Instance.safeWidth = value;
        }
    }

    public static float ScaleFactor
    {
        get {
            if (ScreenAspectRatio < DesignAspectRatio) {
                return ScreenAspectRatio / DesignAspectRatio;
            }
            else {
                return 1f;
            }
        }
    }

    public static float DesignAspectRatio
    {
        get
        {
            if (UGUIRoot.Instance == null) return 1280f / 720f;
            return UGUIRoot.Instance.designResolutionRatio.x / UGUIRoot.Instance.designResolutionRatio.y;
        }
    }

    public static float ScreenAspectRatio
    {
        get
        {
            return (float)Screen.width / (float)Screen.height;
        }
    }

    [SerializeField]
    private float scaleFactor = 1f;

    void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning("UGUIRoot should be singleton, instance will set to the lastest");
        }
        Instance = this;
        scaleFactor = UGUIRoot.ScaleFactor;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!updateMode) return;
        scaleFactor = UGUIRoot.ScaleFactor;
    }
    void Update()
    {
        if (!updateMode) return;
        scaleFactor = UGUIRoot.ScaleFactor;
    }
#endif
}

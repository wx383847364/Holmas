using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
[ExecuteInEditMode]
[AddComponentMenu("Layout/UI Match", 101)]
public class UIMatch : UIBehaviour
{
    [SerializeField]
    private bool updateMode = false;
    private Vector2 matchSize;
    private float matchAspectRatio;

    private RectTransform rectTransform;
    private Vector3 pos = Vector3.zero;
    private Vector2 size;
    private float aspectRatio;
    private float aspectRatioSafe;

    [Range(-1f, 100f), Tooltip("小于0时，使用UGUIRoot.SafeWidth适配")]
    [SerializeField]
    private float safeWidth = -1f;

    [Tooltip("是否只执行左侧安全距离适配")]
    [SerializeField]
    private bool OnlyLeftSafe = false;

    [Tooltip("是否根据RootCanvas的RectTransform适配，否则根据父级RectTransform适配")]
    [SerializeField]
    private bool MatchByRootCanvas = true;
    private RectTransform parentRectTransform;
    private RectTransform rootCanvasRectTransform;

    protected override void Start()
    {
        aspectRatio = UGUIRoot.DesignAspectRatio;
        aspectRatioSafe = (UGUIRoot.DesignResolutionRatio.x + UGUIRoot.SafeWidth * 2f) / UGUIRoot.DesignResolutionRatio.y;

        rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.one / 2;
        rectTransform.anchorMax = Vector2.one / 2;
        rectTransform.localPosition = pos;
        rectTransform.sizeDelta = UGUIRoot.DesignResolutionRatio;
        OnRectTransformDimensionsChange();
    }

    protected override void OnTransformParentChanged()
    {
        OnRectTransformDimensionsChange();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        if (transform.parent == null) return;
        if (MatchByRootCanvas)
        {
            if (rootCanvasRectTransform == null)
                rootCanvasRectTransform = rectTransform.GetRootRectTransform();
            if (rootCanvasRectTransform == null) return;
            matchSize = rootCanvasRectTransform.sizeDelta;
        }
        else
        {
            if (parentRectTransform == null)
                parentRectTransform = transform.parent.GetComponent<RectTransform>();
            if (parentRectTransform == null) return;
            matchSize = parentRectTransform.GetRealSize();
        }
        matchAspectRatio = matchSize.x / matchSize.y;

        size = rectTransform.sizeDelta;

        if (matchAspectRatio <= aspectRatio)
        {
            size.x = matchSize.x;
        }
        else
        {
            if (OnlyLeftSafe)
            {
                if (matchAspectRatio > aspectRatioSafe)
                {
                    float safew = safeWidth;
                    safew = safeWidth;
                    if (safew < 0)
                        safew = UGUIRoot.SafeWidth;

                    size.x = matchSize.x - safew;

                    pos.x = safew / 2;
                    rectTransform.localPosition = pos;
                }
                else
                {
                    float offset = (matchSize.x - UGUIRoot.DesignResolutionRatio.x) / 2;
                    size.x = UGUIRoot.DesignResolutionRatio.x + offset;

                    pos.x = offset / 2;
                    rectTransform.localPosition = pos;
                }
            }
            else
            {
                if (matchAspectRatio > aspectRatioSafe)
                {
                    float safew = safeWidth * 2f;
                    if (safew < 0)
                        safew = UGUIRoot.SafeWidth * 2f;

                    size.x = matchSize.x - safew;
                }
                else
                {
                    size.x = UGUIRoot.DesignResolutionRatio.x;
                }
#if UNITY_EDITOR
                pos.x = 0;
                rectTransform.localPosition = pos;
#endif
            }
        }
        size.y = matchSize.y;
        rectTransform.sizeDelta = size;
    }

    public void SetSafeWidth(float width)
    {
        safeWidth = width;
        if (safeWidth >= 0)
        {
            aspectRatioSafe = (UGUIRoot.DesignResolutionRatio.x + safeWidth * 2f) / UGUIRoot.DesignResolutionRatio.y;
        }
        else
        {
            aspectRatioSafe = (UGUIRoot.DesignResolutionRatio.x + UGUIRoot.SafeWidth * 2f) / UGUIRoot.DesignResolutionRatio.y;
        }
        OnRectTransformDimensionsChange();
    }

#if UNITY_EDITOR
    void Update()
    {
        if (!updateMode) return;
        OnRectTransformDimensionsChange();
    }

    protected override void OnValidate()
    {
        if (!updateMode) return;
        Awake();
    }
#endif
}

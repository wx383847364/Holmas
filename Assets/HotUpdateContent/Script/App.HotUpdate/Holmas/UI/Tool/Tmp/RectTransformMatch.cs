using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
[ExecuteInEditMode]
[AddComponentMenu("Layout/RectTransform Match", 101)]
public class RectTransformMatch : UIBehaviour 
{
    [SerializeField]
    private bool updateMode = false;

    private enum MatchMode {
        /// <summary>
        /// Expand the RectTransform area either horizontally or vertically, so the size of the RectTransform will never be smaller than the MatchRectTransform.
        /// </summary>
        Expand = 1,
        /// <summary>
        /// Shrink the RectTransform area either horizontally or vertically, so the size of the RectTransform will never be larger than the MatchRectTransform.
        /// </summary>
        Shrink = 2
    }

    [SerializeField]
    private MatchMode matchMode = MatchMode.Expand;

    private RectTransform rectTransform;

    [SerializeField]
    private Vector2 minSize = new Vector2(1280f, 720f);
    [SerializeField]
    private Vector2 maxSize;

    private Vector2 size;
    private float aspectRatio;

    private RectTransform matchRectTransform;
    private float matchW;
    private float matchH;
    private float matchAspectRatio;

    [Tooltip("通过根Canvas的RectTransform进行适配，否则通过父级的RectTransform进行适配")]
    [SerializeField]
    private bool matchByRootRectTransform = true;

    protected override void Start() {
        if (maxSize == Vector2.zero) maxSize = minSize;
        rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.one / 2;
        rectTransform.anchorMax = Vector2.one / 2;
        rectTransform.localPosition = Vector3.zero;
        rectTransform.sizeDelta = maxSize;
        OnRectTransformDimensionsChange();
    }

    public void IsExpand(bool isExpand)
    {
        matchMode = isExpand ? MatchMode.Expand : MatchMode.Shrink;
        OnRectTransformDimensionsChange();
    }

    protected override void OnRectTransformDimensionsChange() {
        if (matchRectTransform == null)
        {
            if (matchByRootRectTransform)
            {
                matchRectTransform = rectTransform.GetRootRectTransform();
            }
            else
            {
                matchRectTransform = transform.parent.GetComponent<RectTransform>();
            }
        }
        if (matchRectTransform == null) return;

        matchW = matchRectTransform.rect.width;
        matchH = matchRectTransform.rect.height;

        if (matchW <= maxSize.x && matchH <= maxSize.y && matchW >= minSize.x && matchH >= minSize.y)
        {
            rectTransform.sizeDelta = maxSize;
            return;
        }

        matchAspectRatio = matchW / matchH;

        size = rectTransform.sizeDelta;
        aspectRatio = size.x/size.y;

        if (aspectRatio == matchAspectRatio){
            size.x = matchW;
            size.y = matchH;
        } else if (aspectRatio > matchAspectRatio) {
            if (matchMode == MatchMode.Expand) {
                size.x = size.x / size.y * matchH;
                size.y = matchH;
            } else if (matchMode == MatchMode.Shrink) {
                size.y = size.y / size.x * matchW;
                size.x = matchW;
           }
        } else if (aspectRatio < matchAspectRatio) {
            if (matchMode == MatchMode.Expand) {
                size.y = size.y / size.x * matchW;
                size.x = matchW;
            } else if (matchMode == MatchMode.Shrink) {
                size.x = size.x / size.y * matchH;
                size.y = matchH;
            }
        }
        rectTransform.sizeDelta = size;
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
        Start();
    }
#endif
}
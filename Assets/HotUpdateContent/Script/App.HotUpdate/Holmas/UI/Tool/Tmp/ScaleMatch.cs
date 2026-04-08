using UnityEngine;
using UnityEngine.EventSystems;

[ExecuteInEditMode]
[AddComponentMenu("Layout/ScaleMatch", 101)]
public class ScaleMatch : UIBehaviour 
{
    [SerializeField]
    private bool updateMode = false;

    private Vector2 origin;

    [SerializeField]
    private Vector2 size = new Vector2(1280f, 720f);

    private Vector2 temp = new Vector2();

    private RectTransform matchRectTransform;
    private float matchW;
    private float matchH;

    [Tooltip("通过根Canvas的RectTransform进行适配，否则通过父级的RectTransform进行适配")]
    [SerializeField]
    private bool matchByRootRectTransform = true;

    protected override void Start() {
        origin = transform.localScale;
        OnRectTransformDimensionsChange();
    }

    protected override void OnRectTransformDimensionsChange() {
        if (matchRectTransform == null)
        {
            if (matchByRootRectTransform)
            {
                matchRectTransform = transform.GetRootRectTransform();
            }
            else
            {
                matchRectTransform = transform.parent.GetComponent<RectTransform>();
            }
        }
        if (matchRectTransform == null) return;

        matchW = matchRectTransform.rect.width;
        matchH = matchRectTransform.rect.height;

        temp.x = matchW / size.x * origin.x;
        temp.y = matchH / size.y * origin.y;
        transform.localScale = temp;
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
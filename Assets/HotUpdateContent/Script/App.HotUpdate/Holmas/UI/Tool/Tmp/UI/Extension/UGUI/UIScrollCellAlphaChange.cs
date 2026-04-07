//----------------------------------------------
//滑动列表中cell渐隐排列效果
//----------------------------------------------
using UnityEngine;
using UnityEngine.UI;
public class UIScrollCellAlphaChange : MonoBehaviour
{
    public enum Direction
    {
        Vertical = 0,
        Horizontal = 1
    }
    public Direction direction = Direction.Vertical;
    public CanvasGroup canvasGroup;

    private RectTransform rectTransformScroll;
    private Vector2 screenPos;
    private Vector2 localPos;
    private float alhpa = 1;

    // Start is called before the first frame update
    void Start()
    {
        ScrollRect scrollRect = GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            rectTransformScroll = scrollRect.GetComponent<RectTransform>();
            Vector2 oldPovit = rectTransformScroll.pivot;
            switch (direction)
            {
                case Direction.Vertical:
                    rectTransformScroll.pivot = new Vector2(oldPovit.x, 1f);
                    break;
                case Direction.Horizontal:
                    rectTransformScroll.pivot = new Vector2(1f, oldPovit.y);
                    break;
            }
        }
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (rectTransformScroll != null && canvasGroup != null)
        {
            screenPos = UGUIRoot.UICamera.WorldToScreenPoint(transform.position);
            if (RectTransformUtility.RectangleContainsScreenPoint(rectTransformScroll, screenPos, UGUIRoot.UICamera))
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransformScroll, screenPos, UGUIRoot.UICamera, out localPos);
                switch (direction)
                {
                    case Direction.Vertical:
                        alhpa = 1 - Mathf.Abs(localPos.y) / rectTransformScroll.rect.size.y;
                        break;
                    case Direction.Horizontal:
                        alhpa = 1 - Mathf.Abs(localPos.x) / rectTransformScroll.rect.size.x;
                        break;
                }
                alhpa = Mathf.Clamp(alhpa, 0f, 1f);
                canvasGroup.alpha = alhpa;
            }
        }
    }
}

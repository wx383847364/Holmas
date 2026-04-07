using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections;

public class DragPreview : EventTrigger
{
    private DragPreviewEvent m_OnClick = new DragPreviewEvent();
    public DragPreviewEvent onClick { get { return m_OnClick; } set { m_OnClick = value; } }

    private DragPreviewEvent m_OnDrag = new DragPreviewEvent();
    public DragPreviewEvent onDrag { get { return m_OnDrag; } set { m_OnDrag = value; } }

    private DragPreviewEvent m_OnBeginDrag = new DragPreviewEvent();
    public DragPreviewEvent onBeginDrag { get { return m_OnBeginDrag; } set { m_OnBeginDrag = value; } }

    private DragPreviewEvent m_OnEndDrag = new DragPreviewEvent();
    public DragPreviewEvent onEndDrag { get { return m_OnEndDrag; } set { m_OnEndDrag = value; } }

    bool allowClick = true;

    public override void OnBeginDrag(PointerEventData eventData)
    {
        allowClick = false;
        if (m_OnBeginDrag != null)
            m_OnBeginDrag.Invoke(eventData);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        allowClick = false;
        if (m_OnDrag != null)
            m_OnDrag.Invoke(eventData);

    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        StartCoroutine(NextFrame());
        if (m_OnEndDrag != null)
            m_OnEndDrag.Invoke(eventData);
    }

    IEnumerator NextFrame()
    {
        yield return null;
        allowClick = true;
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (!allowClick) return;
        if (m_OnClick != null)
            m_OnClick.Invoke(eventData);
    }

    public class DragPreviewEvent : UnityEvent<PointerEventData> { }
}

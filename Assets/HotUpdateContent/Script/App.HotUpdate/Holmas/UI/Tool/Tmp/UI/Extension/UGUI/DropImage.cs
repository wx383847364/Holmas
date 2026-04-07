using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DropImage : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public int IndexID;
	public Image containerImage;
	public Image receivingImage;
	private Color normalColor;
	public Color highlightColor = Color.yellow;

    public event Action<int> DropEvent;

    public void OnEnable ()
	{
		if (containerImage != null)
			normalColor = containerImage.color;
	}
	
    //拖拽松手放入区域
	public void OnDrop(PointerEventData data)
	{
		containerImage.color = normalColor;
		
		if (receivingImage == null)
			return;

        //Sprite dropSprite = GetDropSprite (data);
        //if (dropSprite != null)
        //	receivingImage.overrideSprite = dropSprite;
        if (DropEvent != null)
            DropEvent(IndexID);
    }

    //拖拽进入放置区域
	public void OnPointerEnter(PointerEventData data)
	{
		if (containerImage == null)
			return;

        Sprite dropSprite = GetDropSprite (data);
        if (dropSprite != null)
        {
            containerImage.color = highlightColor;
        }
	}

	public void OnPointerExit(PointerEventData data)
	{
		if (containerImage == null)
			return;
		
		containerImage.color = normalColor;
	}
	
	private Sprite GetDropSprite(PointerEventData data)
	{
        //var originalObj = data.pointerDrag;
        //if (originalObj == null)
        //	return null;

        //var srcImage = originalObj.GetComponent<Image>();
        //if (srcImage == null)
        //	return null;
        if (DragImage.DragIcon == null)
            return null;
        var srcImage = DragImage.DragIcon.GetComponent<Image>();
        if (srcImage == null)
            return null;

        return srcImage.sprite;
	}
}

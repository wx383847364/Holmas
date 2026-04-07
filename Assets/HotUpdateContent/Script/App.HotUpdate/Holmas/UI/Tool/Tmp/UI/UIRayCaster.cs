using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIRayCaster : GraphicRaycaster
{
    public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
    {
        base.Raycast(eventData, resultAppendList);
        
        if (eventData.clickCount > 0 && eventData.eligibleForClick)
        {
            Debug.Log("Hit ------------------------------");
            foreach (RaycastResult result in resultAppendList)
            {
                Debug.Log("Hit " + result.gameObject.name);
            }
        }

    }

}

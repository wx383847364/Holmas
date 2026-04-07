using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zeus.Framework.Lua;

namespace Zeus.Framework.UI
{
    public class UIBlockPanel : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {

        public const string EVENT_ID_TEMPLATE = "UI_EVENT|{0}|__blockingPanel_onclick";

        public bool blockEvent = true;
        public bool outsideEvent = false;
        public string eventId = "";
        public Image image;

        public void BlockUI(Transform targetTf, string eventId, bool blockEvent, bool outsideEvent,int sortingOrder)
        {
            if (targetTf != null)
            {
                this.eventId = eventId;
                this.blockEvent = blockEvent;
                this.outsideEvent = outsideEvent;
                image.enabled = true;
                transform.SetParent(targetTf.parent);
                transform.SetSiblingIndex(targetTf.GetSiblingIndex());
                Canvas canvas = transform.gameObject.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = sortingOrder;
                transform.gameObject.AddComponent<GraphicRaycaster>();
            }
        }
        static public string GetEventID(string uiName)
        {
            return string.Format(EVENT_ID_TEMPLATE, uiName);
        }

        static public string _GetEventID(string uiName)
        {
            return string.Format(EVENT_ID_TEMPLATE, uiName);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!blockEvent)
            {
                PassEvent(eventData, ExecuteEvents.pointerDownHandler);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!blockEvent)
            {
                PassEvent(eventData, ExecuteEvents.pointerUpHandler);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (outsideEvent)
            {
                LuaEventBridge.Instance.SendToLuaEvent(eventId, this, null);
            }
            if (!blockEvent)
            {
                PassEvent(eventData, ExecuteEvents.pointerClickHandler);
            }
        }

        public void PassEvent<T>(PointerEventData data, ExecuteEvents.EventFunction<T> function)
            where T : IEventSystemHandler
        {
            if (EventSystem.current == null)
                return;
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);
            int index = 0;
            while (index < results.Count)
            {
                if (gameObject == results[index].gameObject)
                {
                    index++;
                    break;
                }
                index++;
            }
            GameObject resultObj = null;
            for (int i = index; i < results.Count; i++)
            {
                resultObj = results[i].gameObject;
                if (resultObj != null && ExecuteEvents.Execute(resultObj, data, function))
                {
                    break;
                }
            }
        }
    }
}


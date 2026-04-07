using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Zeus.Framework.UI
{
    public class UIEventTrigger : EventTrigger
    {
        private const float INVALID_DISTANCE = -999;

        [SerializeField]
        [Tooltip("How long must pointer be down on this object to trigger a long press")]
        private float holdTime = 1f;
        public bool Hold = false;

        [Tooltip("How long between in two click on this object to trigger a double click")]
        public float ClickedInterval = 0.3f;
        public int ClickedCount = 2;
        private float lastClickedTime = 0;
        private float count = 0;

        public UnityEvent onBeginLongPress = new UnityEvent();
        public UnityEvent onEndLongPress = new UnityEvent();
        public UnityEvent onDoubleClick = new UnityEvent();

        public class ZoomEvent : UnityEvent<float> { }
        public UnityEvent<float> onZoom = new ZoomEvent();
        public bool enabledZoom = false;
        private float curTouchDis;
        private float lastTouchDis = INVALID_DISTANCE;

        public class DragEvent : UnityEvent<PointerEventData> { }
        public DragEvent onDrag = new DragEvent();
        private bool isZoomIng = false; //正在缩放

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            CancelInvoke("OnBeginLongPress");
            OnEndLongPress();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            Invoke("OnBeginLongPress", holdTime);
            OnCheckDoubleClick();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            CancelInvoke("OnBeginLongPress");
            OnEndLongPress();
        }

        private void OnBeginLongPress()
        {
            Hold = true;
            onBeginLongPress.Invoke();
        }

        private void OnEndLongPress()
        {
            if (Hold)
            {
                onEndLongPress.Invoke();
            }
            Hold = false;
        }
        public override void OnDrag(PointerEventData eventData)
        {            
            if (!isZoomIng)
            {
                //base.OnDrag(eventData);                
                onDrag.Invoke(eventData);         
            }            
        }
        private void OnCheckDoubleClick()
        {
            float interval = Time.realtimeSinceStartup - lastClickedTime;
            if (interval <= ClickedInterval)
            {
                count++;
                if (count == ClickedCount - 1)
                {
                    onDoubleClick.Invoke();
                }
            }
            else
            {
                count = 0;
            }
            lastClickedTime = Time.realtimeSinceStartup;
        }
        private void Update()
        {
            if (!enabledZoom)
            {
                return;
            }
            if (Application.isMobilePlatform)
            {

                if (Input.touchCount == 2/* && (Input.GetTouch(0).phase == TouchPhase.Moved || Input.GetTouch(1).phase == TouchPhase.Moved)*/)
                {                    
                    isZoomIng = true;
                    Touch touch1 = Input.GetTouch(0);
                    Touch touch2 = Input.GetTouch(1);
                    curTouchDis = Vector2.Distance(touch1.position, touch2.position);
                    if (lastTouchDis != INVALID_DISTANCE)
                    {
                        float distance = curTouchDis - lastTouchDis;
                        onZoom.Invoke(distance);
                    }
                    lastTouchDis = curTouchDis;
                }
                else if (Input.touchCount != 2)
                {                    
                    lastTouchDis = INVALID_DISTANCE;
                    isZoomIng = false;
                }
            }
            else
            {
                float delta = Input.GetAxis("Mouse ScrollWheel");
                if (delta > float.Epsilon || delta < float.Epsilon * -1)
                {
                    onZoom.Invoke(delta);
                }
            }
        }
    }
}

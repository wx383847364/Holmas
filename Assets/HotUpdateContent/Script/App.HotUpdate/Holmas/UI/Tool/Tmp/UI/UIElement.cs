using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Zeus.Framework.Lua;

namespace Zeus.Framework.UI
{
    [System.Serializable]
    public class UIElement
    {
        /// <summary>
        /// Attention ！！！
        /// 添加新类型只能顺序往后添加
        /// （中间插入会导致既有数据错误）
        /// </summary>
        public enum ElementType
        {
            TEXT,
            BUTTON,
            IMAGE,
            RAWIMAGE,
            CANVAS,
            SLIDER,
            DROPDOWN,
            GRID,
            INPUTFIELD,
            TOGGLE,
            TOGGLEGROUP,
            ANIMATION,
            TRANSFORM,
            ANIMATOR,
            CAMERA,
            NESTPREFAB,

            TEXTMESH,
            RECTTRANSFORM,
            PARTICLESYSTEM,
            VBUTTON,
            VSTICK,
        }

        public ElementType type;
        public string name;
        public Component reference;
        public bool isEvent { get { return eventType != UIEventType.None; } }
        public string eventId;
        public UIEventType eventType = UIEventType.None;
        public object tag = null;

        public UIElement() : this("", ElementType.TRANSFORM, UIEventType.None)
        { }
        public UIElement(string name, ElementType type, UIEventType eventType)
        {
            this.name = name;
            this.type = type;
            this.eventType = eventType;
        }

        public void SetTag(object tag)
        {
            this.tag = tag;
        }

        public void OnButtonClick()
        {
            LuaEventBridge.Instance.SendToLuaEvent(eventId, reference, null, tag);
        }

        public void OnSliderValueCHanged(float value)
        {
            LuaEventBridge.Instance.SendToLuaEvent(eventId, reference, value, tag);
        }

        public void OnDropdownValueCHanged(int value)
        {
            LuaEventBridge.Instance.SendToLuaEvent(eventId, reference, value, tag);
        }

        public void OnToggleValueCHanged(bool value)
        {
            LuaEventBridge.Instance.SendToLuaEvent(eventId, reference, value, tag);
        }

        public void OnEndEdit(string value)
        {
            LuaEventBridge.Instance.SendToLuaEvent(eventId, reference, value, tag);
        }

        public void OnInputValueChanged(string value)
        {
            LuaEventBridge.Instance.SendToLuaEvent(eventId, reference, value, tag);
        }

        public void OnRawEvent(PointerEventData eventData)
        {
            LuaEventBridge.Instance.SendToLuaEvent(eventId, reference, eventData, tag);
        }
    }
}


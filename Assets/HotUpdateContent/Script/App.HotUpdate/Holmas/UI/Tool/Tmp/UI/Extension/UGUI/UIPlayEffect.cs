using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Zeus.Framework.Lua;

//挂载在要播放特效的UI上
//EffectID：Config_EffectAll中的id
public class UIPlayEffect : MonoBehaviour, IPointerDownHandler
{
    public enum PlayTime
    {
        Click = 0,
        //Start = 1,
    }
    public int EffectID;
    public PlayTime playTime = PlayTime.Click;
    public void OnPointerDown(PointerEventData eventData)
    {
        switch (playTime)
        {
            case PlayTime.Click:
                LuaEventBridge.Instance.SendToLuaEvent("GLOBAL|Play_UIEffect", gameObject, EffectID);
                break;
        }

    }
}

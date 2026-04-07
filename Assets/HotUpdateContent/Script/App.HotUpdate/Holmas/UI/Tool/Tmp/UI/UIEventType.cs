using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace Zeus.Framework.UI
{
    public enum UIEventType
    {
        None,
        Default,

        PointerDown = EventTriggerType.PointerDown+100,
        PointerUp = EventTriggerType.PointerUp+100,
        PointerClick = EventTriggerType.PointerClick+100,
        PointerDoubleClick = EventTriggerType.PointerClick + 200,
        Drag = EventTriggerType.Drag+100,
        BeginDrag = EventTriggerType.BeginDrag+100,
        EndDrag = EventTriggerType.EndDrag+100,
        BeginLongPress,
        EndLongPress,
        InputValueChanged,
        Zoom,
    }
}

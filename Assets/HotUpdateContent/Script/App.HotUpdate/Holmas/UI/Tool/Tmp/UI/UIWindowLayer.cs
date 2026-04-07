using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zeus.Framework.UI
{
    public enum UIWindowLayer
    {
        BaseLayer0 = 1,
        BaseLayer1 = 2,
        CommonLayer = 10,
        TopLayer0 = 20,
        TopLayer1 = 21,
        Overlay = 999,//额外的层，永远在最上面
    }
}

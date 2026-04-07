using UnityEngine;

namespace Zeus.Framework.UI
{
    //无限循环Item
    public class UIMultiScrollIndex : MonoBehaviour
    {
        private UIMultiScroller _scroller;
        private int _index;

        public int Index
        {
            get { return _index; }
            set
            {
                _index = value;
                transform.localPosition = _scroller.GetPosition(_index);
                gameObject.name = "Scroll_" + (_index < 10 ? "0" + _index : _index.ToString());
            }
        }

        public UIMultiScroller Scroller
        {
            set { _scroller = value; }
        }
    }
}
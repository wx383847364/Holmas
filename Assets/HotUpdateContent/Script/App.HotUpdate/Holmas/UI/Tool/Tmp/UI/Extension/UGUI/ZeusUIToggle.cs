using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zeus.Framework.UI
{
    public class ZeusUIToggle : Toggle
    {
        //未选中文本
        public Text NormalText;
        //高亮文本
        public Text HigelightText;
        public override void OnSelect(BaseEventData eventData)
        {
            return;
            base.OnSelect(eventData);
            HigelightText.gameObject.SetActive(true);
            NormalText.gameObject.SetActive(false);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            return;
            base.OnDeselect(eventData);
            PointerEventData pointEventData = eventData as PointerEventData;

            UnityEngine.GameObject pointObj = pointEventData.pointerCurrentRaycast.gameObject;
            if (pointObj != null )
            {
                ZeusUIToggle toggleClick = pointObj.GetComponent<ZeusUIToggle>();
                if(toggleClick != null && toggleClick.group.GetInstanceID()== this.group.GetInstanceID())
                {
                    HigelightText.gameObject.SetActive(false);
                    NormalText.gameObject.SetActive(true);
                }
            }

        }

        public void OnValueChangedForText(bool active)
        {
            if (active)
            {
                HigelightText.gameObject.SetActive(true);
                NormalText.gameObject.SetActive(false);
            }
            else
            {
                HigelightText.gameObject.SetActive(false);
                NormalText.gameObject.SetActive(true);
            }
        }
    }
}

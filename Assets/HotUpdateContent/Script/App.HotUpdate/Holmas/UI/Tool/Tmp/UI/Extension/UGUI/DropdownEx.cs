using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;

public class DropdownEx : DropDownNew
{
    [SerializeField]
    [Tooltip("if true, onValueChanged is always triggered even if choose the same option")]
    private bool m_IsAlwaysTrigger = false;
    public bool isAlwaysTrigger { get { return m_IsAlwaysTrigger; } set { m_IsAlwaysTrigger = value; } }

    [SerializeField]
    private DropdownExEvent m_OnCreateDropdownListItem = new DropdownExEvent();
    public DropdownExEvent onCreateDropdownListItem {get{return m_OnCreateDropdownListItem;}set{m_OnCreateDropdownListItem = value;}}
    private int ItemIndex = 0;

    new public void Show()
    {
        base.Show();
        var toggleRoot = transform.Find("Dropdown List/Viewport/Content");
        var toggles = toggleRoot.GetComponentsInChildren<Toggle>(false);
        foreach (var item in toggles)
        {
            item.onValueChanged.RemoveAllListeners();
            //item.isOn = false;
            item.onValueChanged.AddListener(x => OnSelectItemEx(item));
        }
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        ItemIndex = 0;
        Show();
    }

    public void OnSelectItemEx(Toggle toggle)
    {
        if (!toggle.isOn)
        {
            toggle.isOn = true;
            return;
        }

        var selectIndex = GetChildIndexOf(toggle.transform) - 1;

        if (selectIndex < 0) return;

        if (value == selectIndex && m_IsAlwaysTrigger)
        {
            onValueChanged.Invoke(value);
            RefreshShownValue();
        }
        else
            value = selectIndex;

        Hide();
    }

    protected override DropdownItem CreateItem(DropdownItem itemTemplate)
    {
        var di = base.CreateItem(itemTemplate);
        m_OnCreateDropdownListItem.Invoke(di.gameObject, ItemIndex);
        ItemIndex++;
        return di;
    }

    public static int GetChildIndexOf(Transform tr)
    {
        var parent_tr = tr.parent;
        if (parent_tr == null) return -1;

        for (int i = 0; i < parent_tr.childCount; i++)
        {
            if (parent_tr.GetChild(i) == tr)
            {
                return i;
            }
        }

        return -1;
    }

    [Serializable]
    public class DropdownExEvent : UnityEvent<GameObject,int> { }

}

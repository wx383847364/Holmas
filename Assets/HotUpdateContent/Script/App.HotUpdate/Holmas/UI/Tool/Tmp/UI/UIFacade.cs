using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Zeus.Framework.UI
{
    public class UIFacade : MonoBehaviour
    {
        public const string EVENT_ID_TEMPLATE = "UI_EVENT|{0}.{1}|__{2}_{3}";
        public string moduleName;
        public string uiName;
        public List<UIElement> uiElements = new List<UIElement>();
        public List<UIAtlasElement> uiAtlasElements = new List<UIAtlasElement>();
        public List<string> lineTags = new List<string>();
        public List<int> lineAnchors = new List<int>();
        public Dictionary<string, UIElement> elementsDict = new Dictionary<string, UIElement>();
        public UIWindowLayer windowLayer = UIWindowLayer.CommonLayer;
        public bool exclusion = false;
        public bool isModel = false;
        public bool outsideClickEvent = false;
        public bool blurBackground = false;
        public bool customBlurColor = false;
        public Color blurColor = new Color(140f / 255f, 140f / 255f, 140f / 255f, 1f);

        private bool init = false;
        private void Awake()
        {
           Init();
        }

        private void Init()
        {
            if (!init)
            {
                init = true;
                foreach (UIElement uiElement in uiElements)
                {
                    if (string.IsNullOrEmpty(uiElement.name) || uiElement.reference == null)
                    {
                        continue;
                    }
                    else
                    {
                        if (!elementsDict.ContainsKey(uiElement.name))
                        {
                            elementsDict.Add(uiElement.name, uiElement);
                        }
                    }
                }
                InitUIEvent();
            }
        }

        public Object GetReferenceByName(string name)
        {
            Init();
            if (!string.IsNullOrEmpty(name))
            {
                UIElement uiElement = null;
                if (elementsDict.TryGetValue(name, out uiElement))
                {
                    return uiElement.reference;
                }
            }
            return null;
        }

        public Object GetReferenceComponentByName(string name, System.Type type)
        {
            Init();
            if (!string.IsNullOrEmpty(name))
            {
                UIElement uiElement = null;
                if (elementsDict.TryGetValue(name, out uiElement))
                {
                    return uiElement.reference.GetComponent(type);
                }
            }
            return null;
        }

        public Object[] GetReferenceByNames(string[] names)
        {
            Object[] os = new Object[names.Length];
            for(int i = 0; i < os.Length; i++)
            {
                os[i] = GetReferenceByName(names[i]);
            }
            return os;
        }

        public void SetEventTagByName(string name, object tag)
        {
            Init();
            if(!string.IsNullOrEmpty(name))
            {
                UIElement uiElement = null;
                if(elementsDict.TryGetValue(name, out uiElement))
                {
                    uiElement.SetTag(tag);
                }
            }
        }

        private void InitUIEvent()
        {
            for(int i = 0; i < uiElements.Count; i++)
            {
                UIElement uiElement = uiElements[i];
                if (!string.IsNullOrEmpty(uiElement.name) && uiElement.reference != null && uiElement.isEvent)
                {
                    uiElement.eventId = GetElementEventId(uiElement);
                    SetEvent(uiElement);
                }
            }
        }

        private void SetEvent(UIElement element)
        {
            UIEventTrigger trigger = null;
            switch (element.eventType)
            {
                case UIEventType.Default:
                    SetDefaultEvent(element);
                    break;
                case UIEventType.PointerDown:
                case UIEventType.PointerUp:
                case UIEventType.PointerClick:
                case UIEventType.PointerDoubleClick:
                case UIEventType.Drag:
                case UIEventType.BeginDrag:
                case UIEventType.EndDrag:
                case UIEventType.BeginLongPress:
                case UIEventType.EndLongPress:
                case UIEventType.Zoom:
                    trigger = element.reference.GetComponent<UIEventTrigger>();
                    if (trigger == null)
                    {
                        trigger = element.reference.gameObject.AddComponent<UIEventTrigger>();
                    }

                    if (trigger != null)
                    {
                        if (element.eventType == UIEventType.BeginLongPress)
                        {
                            trigger.onBeginLongPress.AddListener(() => { element.OnRawEvent(null); });
                        }
                        else if (element.eventType == UIEventType.EndLongPress)
                        {
                            trigger.onEndLongPress.AddListener(() => { element.OnRawEvent(null); });
                        }
                        else if (element.eventType == UIEventType.PointerDoubleClick)
                        {
                            trigger.onDoubleClick.AddListener(() => { element.OnRawEvent(null); });
                        }
                        else if (element.eventType == UIEventType.Zoom)
                        {
                            trigger.onZoom.AddListener((float dis) => { element.OnSliderValueCHanged(dis); });
                            trigger.enabledZoom = true;
                        }
                        else if (element.eventType == UIEventType.Drag)
                        {
                            trigger.onDrag.AddListener((data) =>
                            {
                                element.OnRawEvent((PointerEventData)data);
                            });
                        }
                        else
                        {
                            EventTrigger.Entry entry = new EventTrigger.Entry();
                            entry.eventID = (EventTriggerType)((int)element.eventType - 100);
                            entry.callback.AddListener((data) =>
                            {
                                element.OnRawEvent((PointerEventData)data);
							});
                            trigger.triggers.Add(entry);
                        }
                    }
                    break;
                case UIEventType.InputValueChanged:
                    InputField input = element.reference as InputField;
                    input.onValueChanged.AddListener(element.OnInputValueChanged);
                    break;
            }
        }

        private void SetDefaultEvent(UIElement uiElement)
        {
            switch (uiElement.type)
            {
                case UIElement.ElementType.BUTTON:
                    Button button = uiElement.reference as Button;
                    button.onClick.AddListener(uiElement.OnButtonClick);
                    break;
                case UIElement.ElementType.SLIDER:
                    Slider slider = uiElement.reference as Slider;
                    slider.onValueChanged.AddListener(uiElement.OnSliderValueCHanged);
                    break;
                case UIElement.ElementType.DROPDOWN:
                    Dropdown drop = uiElement.reference as Dropdown;
                    drop.onValueChanged.AddListener(uiElement.OnDropdownValueCHanged);
                    break;
                case UIElement.ElementType.TOGGLE:
                    Toggle toggle = uiElement.reference as Toggle;
                    toggle.onValueChanged.AddListener(uiElement.OnToggleValueCHanged);
                    break;
                case UIElement.ElementType.INPUTFIELD:
                    InputField input = uiElement.reference as InputField;
                    input.onEndEdit.AddListener(uiElement.OnEndEdit);
                    break;
            }
        }

        public string GetElementEventId(UIElement element)
        {
            return string.Format(EVENT_ID_TEMPLATE, moduleName, uiName, element.name, element.eventType.ToString());
        }

        public string GetElementEventFunctionName(UIElement element)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendFormat("On_{0}_", element.name);
            switch (element.eventType)
            {
                case UIEventType.Default:
                    if (element.type == UIElement.ElementType.BUTTON)
                    {
                        builder.Append("Click");
                    }
                    else if (element.type == UIElement.ElementType.INPUTFIELD)
                    {
                        builder.Append("EndEdit");
                    }
                    else
                    {
                        builder.Append("ValueChanged");
                    }
                    break;
                case UIEventType.PointerDown:
                case UIEventType.PointerUp:
                case UIEventType.PointerClick:
                case UIEventType.PointerDoubleClick:
                case UIEventType.Drag:
                case UIEventType.BeginDrag:
                case UIEventType.EndDrag:
                case UIEventType.BeginLongPress:
                case UIEventType.EndLongPress:
                case UIEventType.InputValueChanged:
                case UIEventType.Zoom:
                    builder.Append(element.eventType.ToString());
                    break;
            }
            return builder.ToString();
        }

        public Component DuplicateElement(string sourceElement, string elementName, Transform parentTf)
        {
            Component newComponent = null;
            if (elementsDict.ContainsKey(sourceElement))
            {
                UIElement element = elementsDict[sourceElement];
                GameObject newObj = GameObject.Instantiate(element.reference.gameObject);
                newObj.transform.parent = parentTf;
                newObj.transform.localScale = Vector3.one;
                newObj.SetActive(true);
                if (elementName == null)
                {
                    elementName = element.name;
                }
                newComponent = newObj.GetComponent(element.reference.GetType());
                AddElement(newComponent, element.type, elementName, element.eventType);
            }
            return newComponent;
        }

        public void AddElement(Component elementRef, UIElement.ElementType elementType, string elementName, UIEventType eventType)
        {
            UIElement element = new UIElement();
            element.type = elementType;
            element.reference = elementRef;
            element.name = elementName;
            element.eventType = eventType;
            uiElements.Add(element);
            if (elementsDict.ContainsKey(element.name))
            {
                Debug.Log("AddElement repeated: "+element.name);
            }
            else
            {
                elementsDict.Add(element.name, element);
            }
            if (element.reference != null && element.isEvent)
            {
                element.eventId = GetElementEventId(element);
                SetEvent(element);
            }
        }

        public void RemoveElement(string elementName)
        {
            if (uiElements != null)
            {
                for (int i = uiElements.Count - 1; i >= 0; i--)
                {
                    if(uiElements[i].name == elementName)
                    {
                        uiElements.RemoveAt(i);
                    }
                }
            }

            if (elementsDict != null && elementsDict.ContainsKey(elementName))
            {
                elementsDict.Remove(elementName);
            }

        }

        public Sprite GetSprite(string spriteName)
        {
            Sprite sprite = null;
            for (int i = 0; i < uiAtlasElements.Count; i++)
            {
                if (uiAtlasElements[i].reference != null)
                {
                    sprite = uiAtlasElements[i].reference.GetSprite(spriteName);
                    if (sprite != null)
                    {
                        break;
                    }
                }
            }
            return sprite;
        }

        public void GetAll(out Component[] components)
        {
            List<Component> listComponents = new List<Component>();
            for(int i =0;i < uiElements.Count; i++)
            {
                UIElement ue = uiElements[i];
                listComponents.Add(ue.reference);
            }
            components = listComponents.ToArray();
        }

        public void SetModuleUIName(string moduleName, string uiName)
        {
            this.moduleName = moduleName;
            this.uiName = uiName;
            InitUIEvent();
        }
    }
}


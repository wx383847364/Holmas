using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEngine.U2D;
using UnityEditor.SceneManagement;
using System.Text;
using System;

namespace Zeus.Framework.UI
{
    [CustomEditor(typeof(UIFacade))]
    public class UIFacadeInspector : Editor
    {
        private string _lineTag = string.Empty;
        private int _lineAnchor = 0;
        private int _elementAnchor = -1;

        public override void OnInspectorGUI()
        {
            UIFacade facade = target as UIFacade;
            //base.DrawDefaultInspector();
            EditorGUILayout.Space();
            facade.moduleName = EditorGUILayout.TextField("Module Name: ", string.IsNullOrEmpty(facade.moduleName) ? facade.gameObject.name : facade.moduleName);
            facade.uiName = EditorGUILayout.TextField("UI Name: ", string.IsNullOrEmpty(facade.uiName) ? facade.gameObject.name : facade.uiName);
            //layer setting
            facade.windowLayer = (UIWindowLayer)EditorGUILayout.EnumPopup("UI Layer: ", facade.windowLayer);
            //exclusion
            facade.exclusion = EditorGUILayout.Toggle("Exclusion: ", facade.exclusion);
            //model window
            facade.isModel = EditorGUILayout.Toggle("Model: ", facade.isModel);
            //is generate outsideClick event
            facade.outsideClickEvent = EditorGUILayout.Toggle("OutsideClickEvent: ", facade.outsideClickEvent);
            //is have blur background
            facade.blurBackground = EditorGUILayout.Toggle("BlurBackground: ", facade.blurBackground);
            //is custom blur background color
            if (facade.blurBackground)
            {
                facade.customBlurColor = EditorGUILayout.Toggle("CustomBlurColor: ", facade.customBlurColor);
            }
            if (facade.customBlurColor)
            {
                facade.blurColor = EditorGUILayout.ColorField("blurColor:", facade.blurColor);
            }
            EditorGUILayout.Space();
            Stack<int> delIndexes = null;
            Stack<int> lineDelIndexes = null;
            List<int> moveUpIndexes = null;
            List<int> moveDownIndexes = null;
            foreach (UIElement element in facade.uiElements)
            {
                int eleIndex = facade.uiElements.IndexOf(element);

                for (int lineIndex = 0; lineIndex < facade.lineAnchors.Count; lineIndex++)
                {
                    if (facade.lineAnchors[lineIndex] == eleIndex)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.BeginHorizontal();

                        EditorGUILayout.LabelField(" ----------------- " + facade.lineTags[lineIndex] + " -----------------");
                        if (GUILayout.Button("↑", GUILayout.Width(17f)))
                        {
                            facade.lineAnchors[lineIndex] = Mathf.Max(0, facade.lineAnchors[lineIndex] - 1);
                        }
                        if (GUILayout.Button("↓", GUILayout.Width(17f)))
                        {
                            facade.lineAnchors[lineIndex] = Mathf.Min(facade.uiElements.Count - 1, facade.lineAnchors[lineIndex] + 1);
                        }
                        if (GUILayout.Button("-", GUILayout.Width(17f)))
                        {
                            if (lineDelIndexes == null)
                            {
                                lineDelIndexes = new Stack<int>();
                            }
                            lineDelIndexes.Push(lineIndex);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }

                if (lineDelIndexes != null)
                {
                    while (lineDelIndexes.Count > 0)
                    {
                        int delIndex = lineDelIndexes.Pop();
                        facade.lineAnchors.RemoveAt(delIndex);
                        facade.lineTags.RemoveAt(delIndex);
                    }
                    lineDelIndexes.Clear();
                }

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(eleIndex + ".", GUILayout.Width(22f));
                element.name = EditorGUILayout.TextField(element.name).Trim();
                element.type = (UIElement.ElementType)EditorGUILayout.EnumPopup(element.type);
                GetComponentByElementType(ref element.reference, element.type);



				if (string.IsNullOrEmpty(element.name) && element.reference != null)
                {
                    element.name = element.reference.name;
                }

                element.eventType = (UIEventType)EditorGUILayout.EnumPopup(element.eventType);

                if (GUILayout.Button("↑") && eleIndex > 0)
                {
                    if (moveUpIndexes == null)
                    {
                        moveUpIndexes = new List<int>();
                    }
                    moveUpIndexes.Add(eleIndex);
                }
                if (GUILayout.Button("↓") && eleIndex < (facade.uiElements.Count - 1))
                {
                    if (moveDownIndexes == null)
                    {
                        moveDownIndexes = new List<int>();
                    }
                    moveDownIndexes.Add(eleIndex);
                }
                if (GUILayout.Button("-"))
                {
                    if (delIndexes == null)
                    {
                        delIndexes = new Stack<int>();
                    }
                    delIndexes.Push(eleIndex);
                }
                EditorGUILayout.LabelField("."+eleIndex, GUILayout.Width(22f));

                EditorGUILayout.EndHorizontal();
            }

            if (delIndexes != null)
            {
                while (delIndexes.Count > 0)
                {
                    int delIndex = delIndexes.Pop();
                    facade.uiElements.RemoveAt(delIndex);
                    _elementAnchor = facade.uiElements.Count;
                }
                delIndexes.Clear();
            }

            if (moveUpIndexes != null)
            {
                foreach (int moveUpIndex in moveUpIndexes)
                {
                    bool realMoveUp = true;
                    for (int lineIndex = 0; lineIndex < facade.lineAnchors.Count; lineIndex++)
                    {
                        if (facade.lineAnchors[lineIndex] == moveUpIndex)
                        {
                            facade.lineAnchors[lineIndex] = facade.lineAnchors[lineIndex] + 1;
                            if (facade.lineAnchors[lineIndex] >= facade.uiElements.Count)
                            {
                                if (lineDelIndexes == null)
                                {
                                    lineDelIndexes = new Stack<int>();
                                }
                                lineDelIndexes.Push(lineIndex);
                            }
                            realMoveUp = false;
                        }
                    }
                    if (realMoveUp)
                    {
                        UIElement tempEle = facade.uiElements[moveUpIndex];
                        facade.uiElements.RemoveAt(moveUpIndex);
                        facade.uiElements.Insert(moveUpIndex - 1, tempEle);
                    }
                }
                moveUpIndexes.Clear();
            }
            if (moveDownIndexes != null)
            {
                foreach (int moveDownIndex in moveDownIndexes)
                {
                    bool realMoveDown = true;
                    for (int lineIndex = 0; lineIndex < facade.lineAnchors.Count; lineIndex++)
                    {
                        if (facade.lineAnchors[lineIndex] == moveDownIndex + 1)
                        {
                            facade.lineAnchors[lineIndex] = facade.lineAnchors[lineIndex] - 1;

                            realMoveDown = false;
                        }
                    }

                    if (realMoveDown)
                    {
                        UIElement tempEle = facade.uiElements[moveDownIndex];
                        facade.uiElements.RemoveAt(moveDownIndex);
                        facade.uiElements.Insert(moveDownIndex + 1, tempEle);
                    }
                }
                moveDownIndexes.Clear();
            }
            if (lineDelIndexes != null)
            {
                while (lineDelIndexes.Count > 0)
                {
                    int delIndex = lineDelIndexes.Pop();
                    facade.lineAnchors.RemoveAt(delIndex);
                    facade.lineTags.RemoveAt(delIndex);
                }
                lineDelIndexes.Clear();
            }

            EditorGUILayout.Space();
            GUI.contentColor = Color.white;
            var dragArea = GUILayoutUtility.GetRect(0f, 35f, GUILayout.ExpandWidth(true));
            Event aEvent = Event.current;
            switch (aEvent.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dragArea.Contains(aEvent.mousePosition))
                    {
                        break;
                    }
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (aEvent.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        for (int i = 0; i < DragAndDrop.objectReferences.Length; ++i)
                        {
                            GameObject temp = DragAndDrop.objectReferences[i] as GameObject;
                            UIElement element = new UIElement();
                            element.reference = temp.transform;
                            facade.uiElements.Add(element);
                            _elementAnchor = facade.uiElements.Count;
                            if (temp == null)
                            {
                                break;
                            }
                        }
                    }

                    Event.current.Use();
                    break;
                default:
                break;
            }
            GUIContent title = new GUIContent("拖动至此添加element");
            GUI.Box(dragArea, title);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Index", GUILayout.Width(40f));
            _lineAnchor = EditorGUILayout.IntField(_lineAnchor, GUILayout.Width(50f));
            EditorGUILayout.LabelField("Tag", GUILayout.Width(30f));
            _lineTag = EditorGUILayout.TextField(_lineTag, GUILayout.Width(100f));
            if (GUILayout.Button("Add Tag"))
            {
                facade.lineTags.Add(_lineTag);
                facade.lineAnchors.Add(_lineAnchor);
                _lineTag = string.Empty;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Index", GUILayout.Width(40f));

            if (_elementAnchor == -1) _elementAnchor = facade.uiElements.Count;
            _elementAnchor = EditorGUILayout.IntField(_elementAnchor, GUILayout.Width(50f));
            if (GUILayout.Button("Add Element"))
            {
                UIElement element = new UIElement();
                if (facade.uiElements != null && facade.uiElements.Count > 0)
                {
                    UIElement lastEle = facade.uiElements[facade.uiElements.Count - 1];
                    element = new UIElement("", lastEle.type,lastEle.eventType);
                }
                facade.uiElements.Insert(_elementAnchor, element);
                _elementAnchor = facade.uiElements.Count;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            foreach (UIAtlasElement element in facade.uiAtlasElements)
            {
                int eleIndex = facade.uiAtlasElements.IndexOf(element);
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(eleIndex + ".", GUILayout.Width(22f));
                element.name = EditorGUILayout.TextField(element.name).Trim();
                element.reference = EditorGUILayout.ObjectField(element.reference, typeof(SpriteAtlas), true) as SpriteAtlas;

                if (string.IsNullOrEmpty(element.name) && element.reference != null)
                {
                    element.name = element.reference.name;
                }
                if (GUILayout.Button("-"))
                {
                    if (delIndexes == null)
                    {
                        delIndexes = new Stack<int>();
                    }
                    delIndexes.Push(eleIndex);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (delIndexes != null)
            {
                while (delIndexes.Count > 0)
                {
                    int delIndex = delIndexes.Pop();
                    facade.uiAtlasElements.RemoveAt(delIndex);
                }
                delIndexes.Clear();
            }

            if (GUILayout.Button("Add Atlas Element"))
            {
                UIAtlasElement element = new UIAtlasElement();
                facade.uiAtlasElements.Add(element);
            }

            EditorGUILayout.Space();

            //generate controller code function
            if (GUILayout.Button("Gen luaControllerCode"))
            {
                UILuaTemplateGenerator.GenerateControllerLuaCode(facade);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Gen LeoToClipboard"))
            {
                UILuaTemplateGenerator.CopyLeoCode(facade);
            }

            delIndexes = null;
            lineDelIndexes = null;
            moveUpIndexes = null;
            moveDownIndexes = null;

            GUILayout.Space(10);
            GUILayout.Label("修改后点击Apply保存Prefab");
            if (GUILayout.Button("Apply"))
            {
                UILuaTemplateGenerator.ApplyPrefab(facade);
            }
        }
        private static void SetObjectField<T>(ref Component reference) where T :Component
		{
            if (reference == null)
			{
				reference = EditorGUILayout.ObjectField(reference, typeof(T), true) as T;
				return;
			}
			if (reference is T)
				reference = EditorGUILayout.ObjectField(reference, typeof(T), true) as T;
			else
				reference = EditorGUILayout.ObjectField(reference.GetComponent<T>(), typeof(T), true) as T;
		}
        private static void GetComponentByElementType(ref Component reference, UIElement.ElementType etype)
		{
            switch (etype)
			{
                case UIElement.ElementType.TEXT:
                    SetObjectField<Text>(ref reference);
                    return;
                case UIElement.ElementType.BUTTON:
					SetObjectField<Button>(ref reference);
                    return;
				case UIElement.ElementType.IMAGE:
					SetObjectField<Image>(ref reference);
                    return;
				case UIElement.ElementType.RAWIMAGE:
					SetObjectField<RawImage>(ref reference);
                    return;
				case UIElement.ElementType.TOGGLE:
					SetObjectField<Toggle>(ref reference);
                    return;
				case UIElement.ElementType.TOGGLEGROUP:
					SetObjectField<ToggleGroup>(ref reference);
                    return;
                case UIElement.ElementType.TEXTMESH:
					SetObjectField<TMPro.TextMeshProUGUI>(ref reference);
                    return;
				case UIElement.ElementType.ANIMATOR:
					SetObjectField<Animator>(ref reference);
                    return;
				case UIElement.ElementType.ANIMATION:
					SetObjectField<Animation>(ref reference);
                    return;
				case UIElement.ElementType.CAMERA:
					SetObjectField<Camera>(ref reference);
                    return;
				case UIElement.ElementType.NESTPREFAB:
					SetObjectField<NestPrefab>(ref reference);
                    return;
				case UIElement.ElementType.RECTTRANSFORM:
					SetObjectField<RectTransform>(ref reference);
                    return;
				case UIElement.ElementType.PARTICLESYSTEM:
					SetObjectField<ParticleSystem>(ref reference);
                    return;
				case UIElement.ElementType.SLIDER:
					SetObjectField<Slider>(ref reference);
                    return;
				case UIElement.ElementType.CANVAS:
					SetObjectField<Canvas>(ref reference);
                    return;
				case UIElement.ElementType.DROPDOWN:
					SetObjectField<Dropdown>(ref reference);
                    return;
				case UIElement.ElementType.GRID:
					SetObjectField<GridLayoutGroup>(ref reference);
                    return;
				case UIElement.ElementType.INPUTFIELD:
					SetObjectField<InputField>(ref reference);
                    return;
			}
			SetObjectField<Transform>(ref reference);
		}
    }
}


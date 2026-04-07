using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Zeus.Framework.UI
{
    [CustomEditor(typeof(UIObjectList))]
    public class UIObjectListInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            UIObjectList uiObjectList = target as UIObjectList;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Count:", GUILayout.Width(46f));
            uiObjectList.Count = EditorGUILayout.IntField(uiObjectList.Count);
            EditorGUILayout.EndHorizontal();
            if (uiObjectList.Count > -1 && uiObjectList.Count != uiObjectList.LastCount)
            {
                //计算出本次操作是增加结点还是删除
                int addOrRemoveCount = uiObjectList.Count - uiObjectList.LastCount;
                if (addOrRemoveCount > 0)
                {
                    //新增
                    for (int i = 0; i < addOrRemoveCount; i++)
                    {
                        uiObjectList.List.Add(new UIObjectElement());
                    }
                }
                else
                {
                    //删除
                    uiObjectList.List.RemoveRange(uiObjectList.Count, Mathf.Abs(addOrRemoveCount));
                }
                uiObjectList.LastCount = uiObjectList.Count;
            }

            if (uiObjectList.List.Count > 0)
            {
                EditorGUILayout.LabelField("--------------------------------------------------------------------------------------------------");
            }

            //绘制出集合元素
            foreach (var element in uiObjectList.List)
            {
                int eleIndex = uiObjectList.List.IndexOf(element);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(eleIndex + ".", GUILayout.Width(22f));
                element.name = EditorGUILayout.TextField(element.name);
                element.obj = EditorGUILayout.ObjectField(element.obj, typeof(UnityEngine.Object), true) as UnityEngine.Object;

                if (string.IsNullOrEmpty(element.name) && element.obj != null)
                {
                    element.name = element.obj.name;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

    }
}

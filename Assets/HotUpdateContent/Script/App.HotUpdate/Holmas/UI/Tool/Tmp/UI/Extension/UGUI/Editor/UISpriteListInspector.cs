using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Zeus.Framework.UI
{
    [CustomEditor(typeof(UISpriteList))]
    public class UISpriteListInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            UISpriteList uiSpriteList = target as UISpriteList;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Count:", GUILayout.Width(46f));
            uiSpriteList.Count = EditorGUILayout.IntField(uiSpriteList.Count);
            EditorGUILayout.EndHorizontal();
            if (uiSpriteList.Count > -1 && uiSpriteList.Count != uiSpriteList.LastCount)
            {
                //计算出本次操作是增加结点还是删除
                int addOrRemoveCount = uiSpriteList.Count - uiSpriteList.LastCount;
                if (addOrRemoveCount > 0)
                {
                    //新增
                    for (int i = 0;i<addOrRemoveCount;i++)
                    {
                        uiSpriteList.List.Add(new UISpriteElement());
                    }
                }
                else
                {
                    //删除
                    uiSpriteList.List.RemoveRange(uiSpriteList.Count, Mathf.Abs(addOrRemoveCount));
                }
                uiSpriteList.LastCount = uiSpriteList.Count;
            }

            if (uiSpriteList.List.Count > 0)
            {
                EditorGUILayout.LabelField("--------------------------------------------------------------------------------------------------");
            }

            //绘制出集合元素
            foreach (var element in uiSpriteList.List)
            {
                int eleIndex = uiSpriteList.List.IndexOf(element);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(eleIndex+".", GUILayout.Width(22f));
                element.name = EditorGUILayout.TextField(element.name);
                element.sprite = EditorGUILayout.ObjectField(element.sprite, typeof(Sprite), true) as Sprite;

                if (string.IsNullOrEmpty(element.name) && element.sprite != null)
                {
                    element.name = element.sprite.name;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

    }
}

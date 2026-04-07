using System.IO;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.UI;
#if UNITY_2018_3_OR_NEWER
using UnityEditor.Experimental.SceneManagement;
#endif

namespace Zeus.Framework.UI
{
	[InitializeOnLoad]
	public class UILuaTemplateGenerator
	{
#if UNITY_2018_3_OR_NEWER
		private readonly static string AssetRes = "Assets/Resources/";
		private static PrefabStage _prefabStage;
		private readonly static Dictionary<string, bool> allowed_module_name = new Dictionary<string, bool>() {
			{"City", true },
			{"Battle", true },
			{"Common", true },
		};
		static UILuaTemplateGenerator()
		{
			PrefabStage.prefabStageOpened += OnPrefabStageOpened;
			PrefabStage.prefabStageClosing += OnPrefabStageClosing;
		}
		/*
        ~UILuaTemplateGenerator()
        {
            PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
            PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
        }
        */

		static void OnPrefabStageOpened(PrefabStage prefabStage)
		{
			_prefabStage = prefabStage;
		}

		static void OnPrefabStageClosing(PrefabStage prefabStage)
		{
			_prefabStage = null;
		}

#endif

		static public void ApplyPrefab(UIFacade facade)
		{
#if UNITY_2018_3_OR_NEWER
			if (_prefabStage != null)
			{
				PrefabUtility.SaveAsPrefabAsset(_prefabStage.prefabContentsRoot, _prefabStage.prefabAssetPath);
			}
			else
			{
				_ApplyPrefab(facade);
			}
#else
            _ApplyPrefab(facade);
#endif
		}
		static void _ApplyPrefab(UIFacade facade)
		{
			PrefabType type = PrefabUtility.GetPrefabType(facade.gameObject);
			if (type == PrefabType.PrefabInstance || type == PrefabType.Prefab)
			{
				UnityEngine.Object parentObject = facade.gameObject;
				if (type == PrefabType.PrefabInstance)
				{
					parentObject = PrefabUtility.GetPrefabParent(facade.gameObject);
				}
				string path = AssetDatabase.GetAssetPath(parentObject);
				PrefabUtility.SaveAsPrefabAsset(facade.gameObject, path);
			}
		}

		// add by chezheng for common prefab usage
		static public void ApplyPrefab(GameObject go)
		{
#if UNITY_2018_3_OR_NEWER
			if (_prefabStage != null)
			{
				PrefabUtility.SaveAsPrefabAsset(_prefabStage.prefabContentsRoot, _prefabStage.prefabAssetPath);
			}
			else
			{
				_ApplyPrefab(go);
			}
#else
            _ApplyPrefab(facade);
#endif
		}

		static void _ApplyPrefab(GameObject go)
		{
			PrefabType type = PrefabUtility.GetPrefabType(go);
			if (type == PrefabType.PrefabInstance || type == PrefabType.Prefab)
			{
				UnityEngine.Object parentObject = go;
				if (type == PrefabType.PrefabInstance)
				{
					parentObject = PrefabUtility.GetPrefabParent(go);
				}
				string path = AssetDatabase.GetAssetPath(parentObject);
				PrefabUtility.SaveAsPrefabAsset(go, path);
			}
		}

		static public void GenerateViewLuaCode(UIFacade facade)
		{
			EditorUtility.DisplayDialog("Error", "不要点我\n新框架只用UIController就可以了\n可以参考UI_Logo\n如果还有什么问题找萌萌", "OK");
			return;
			if (string.IsNullOrEmpty(PlayerPrefs.GetString(UISettingEditor.LuaGeneratePathStoreKey)))
			{
				EditorUtility.DisplayDialog("Error", "Lua SavePath is empty \n\nPlease browse in Zeus->Framework->UIFacade Setting.", "OK");
				return;
			}
			string generateViewLuaPath = PlayerPrefs.GetString(UISettingEditor.LuaGeneratePathStoreKey) + "/" + facade.moduleName.Trim().Replace('\\', '/') + "/";
			CheckLuaSavePathExists(generateViewLuaPath);

			string className = "UI_" + facade.uiName + "_View";
			string fileName = className + ".lua";
			string destPath = Path.Combine(generateViewLuaPath, fileName);
			string comparePaths = null;

			if (File.Exists(destPath))
			{
				//文件存在 0:Replace 1:Merge 2:Cancel
				int result = EditorUtility.DisplayDialogComplex("has exist file, Replace?", "has exist file: " + destPath, "Replace", "Merge", "Cancel");
				switch (result)
				{
					case 1:
						if (CheckCompareExePathEmpty())
							return;
						comparePaths = destPath;
						string newClassName = className + "_New";
						fileName = newClassName + ".lua";
						generateViewLuaPath = Application.persistentDataPath + "/LuaProject/View/";

						if (!Directory.Exists(generateViewLuaPath))
							Directory.CreateDirectory(generateViewLuaPath);

						destPath = Path.Combine(generateViewLuaPath, fileName);
						//新文件目录 旧文件目录
						comparePaths = string.Format(" {0} {1}", destPath, comparePaths);
						break;
					case 0:
						break;
					default:
						return;
				}
			}

			System.Text.StringBuilder strBuilder = new System.Text.StringBuilder();
			strBuilder.AppendLine("local UIViewBase = require(\"UI.UIViewBase\")");
			strBuilder.AppendLine();
			strBuilder.AppendFormat("local rtn = middleclass(\"{0}\", UIViewBase)", className);
			strBuilder.AppendLine();
			strBuilder.AppendLine();
			strBuilder.AppendFormat("function rtn:initialize(obj)\n");
			strBuilder.AppendFormat("    rtn.super.initialize(self, obj)\n");
			foreach (UIElement element in facade.uiElements)
			{
				strBuilder.AppendFormat("    self.{0} = self._uiFacade:GetReferenceByName(\"{1}\")\n", element.name, element.name);
			}
			strBuilder.AppendLine("end");
			strBuilder.AppendLine();
			strBuilder.AppendLine("------------------------------自定义方法 BEGIN------------------------------");
			strBuilder.AppendLine();
			strBuilder.AppendFormat("return rtn\n");
			File.WriteAllText(destPath, strBuilder.ToString(), new System.Text.UTF8Encoding(false));

			if (!string.IsNullOrEmpty(comparePaths))
			{
				string fileNameAndParameter = string.Format("{0}&{1}", PlayerPrefs.GetString(UISettingEditor.LuaCompareExePathStoreKey), comparePaths);
				UnityExecuteOutExe.Run(fileNameAndParameter);
			}
			Debug.Log("Generate complete: " + fileName);
		}

		static public void GenerateControllerLuaCode(UIFacade facade)
		{
			if (string.IsNullOrEmpty(PlayerPrefs.GetString(UISettingEditor.LuaGeneratePathStoreKey)))
			{
				EditorUtility.DisplayDialog("Error", "Lua SavePath is empty \n\nPlease browse in Zeus->Framework->UIFacade Setting.", "OK");
				return;
			}

			string generateControllerLuaPath = PlayerPrefs.GetString(UISettingEditor.LuaGeneratePathStoreKey) + "/" + facade.moduleName.Trim().Replace('\\', '/') + "/";
			CheckLuaSavePathExists(generateControllerLuaPath);

			string uiName = facade.uiName;
			string viewClassName = "UI_" + uiName + "_View";
			string viewClassPath = "UI." + GetModuleClassPath(facade.moduleName);
			string className = "UI_" + uiName + "_Controller";
			string fileName = className + ".lua";
			string destPath = Path.Combine(generateControllerLuaPath, fileName);
			string comparePaths = null;

			if (File.Exists(destPath))
			{
				//文件存在 0:Replace 1:Merge 2:Cancel
				int result = EditorUtility.DisplayDialogComplex("has exist file, Replace?", "has exist file: " + destPath, "Replace", "Merge", "Cancel");
				switch (result)
				{
					case 1:
						if (CheckCompareExePathEmpty())
							return;
						comparePaths = destPath;
						string newClassName = className + "_New";
						fileName = newClassName + ".lua";
						generateControllerLuaPath = Application.persistentDataPath + "/LuaProject/Controller/";

						if (!Directory.Exists(generateControllerLuaPath))
							Directory.CreateDirectory(generateControllerLuaPath);

						destPath = Path.Combine(generateControllerLuaPath, fileName);
						comparePaths = string.Format(" {0} {1}", destPath, comparePaths);
						break;
					case 0:
						break;
					default:
						return;
				}
			}
			else
			{
				if (!allowed_module_name.ContainsKey(facade.moduleName))
				{
					EditorUtility.DisplayDialog("Error", "module name 只允许City,Common,Battle", "OK");
					return;
				}
			}

			System.Text.StringBuilder strBuilder = new System.Text.StringBuilder();
			strBuilder.AppendLine("local UIControllerBase = require(\"UI.UIControllerBase\")");
			strBuilder.AppendLine();
			strBuilder.AppendFormat("local rtn = middleclass(\"{0}\", UIControllerBase)", className);
			strBuilder.AppendLine();
			strBuilder.AppendFormat("rtn.assetPath = \"{0}\"\n", GetControllerAssetPath(facade.gameObject));
			strBuilder.AppendLine();
			strBuilder.AppendLine("rtn.useUIViewBase = true");
			strBuilder.AppendLine();
            strBuilder.AppendLine("rtn.ignoreMainCamera = true");
            strBuilder.AppendLine();
            strBuilder.AppendFormat("function rtn:initialize(param)\n");
			strBuilder.AppendFormat("    rtn.super.initialize(self, \"{0}.{1}\", {2}.{3}, {4}, {5}, {6}, {7}, param)\n",
				GetModuleClassPath(facade.moduleName),
				uiName,
				"UIWindowLayer",
				facade.windowLayer.ToString(),
				facade.exclusion.ToString().ToLower(),
				facade.isModel.ToString().ToLower(),
				facade.outsideClickEvent.ToString().ToLower(),
				facade.blurBackground.ToString().ToLower()
			);
			if (facade.customBlurColor)
			{
				strBuilder.AppendFormat("    self._blurColor = UnityEngine.Color({0}, {1}, {2}, {3})\n", facade.blurColor.r, facade.blurColor.g, facade.blurColor.b, facade.blurColor.a);
			}
			strBuilder.AppendLine("end");
			if (facade.outsideClickEvent)
			{
				strBuilder.AppendLine();
				strBuilder.AppendFormat("function rtn:__blockingPanel_onclick()\n");
				strBuilder.AppendFormat("    PanelMgr:Close(self._name)\n");
				strBuilder.AppendLine("end");
			}

			strBuilder.AppendLine();
			foreach (UIElement element in facade.uiElements)
			{
				if (element.isEvent)
				{
					strBuilder.AppendFormat("function rtn:__{0}_{1}(sender, param)\n",
						element.name,
						element.eventType.ToString()
					);
					strBuilder.AppendLine("end\n");
				}
			}
			strBuilder.AppendLine("------------------------------自定义方法 BEGIN------------------------------");
			strBuilder.AppendLine();
			strBuilder.AppendFormat("function rtn:Init(param)\n");
			var listTexts = new List<UIElement>();
			var listHandlers = new List<string>();
			foreach (UIElement element in facade.uiElements)
			{
				if (element.type == UIElement.ElementType.TEXT)
				{
					listTexts.Add(element);
					continue;
				}
				if (element.type == UIElement.ElementType.BUTTON)
				{
					var button = element.reference as Button;
					var text = button.transform.Find("Text");
					if (text != null)
						strBuilder.AppendFormat("    self.{0} = self:GetLeo(\"{1}\", {2}, gettext\"@{3}\")\n", element.name, element.name, GetLeoTranslate(element.type), text.GetComponent<Text>().text);
					else
						strBuilder.AppendFormat("    self.{0} = self:GetLeo(\"{1}\", {2})\n", element.name, element.name, GetLeoTranslate(element.type));
					strBuilder.AppendLine($"    self.{element.name}.onClick = self:__handler(\"OnClick_{element.name}\")");
					listHandlers.Add($"OnClick_{element.name}");
					continue;
				}
				strBuilder.AppendFormat("    self:GetLeo(\"{1}\", {2})\n", element.name, element.name, GetLeoTranslate(element.type));
			}
			strBuilder.AppendLine();
			if (listTexts.Count > 0)
			{
				strBuilder.AppendLine("    local texts = {");
				foreach (var t in listTexts)
				{
					strBuilder.AppendLine($"        {t.name} = gettext\"@{(t.reference as Text).text}\",");
				}
				strBuilder.AppendLine("    }");
				strBuilder.AppendLine($"    for k, v in pair(texts) do");
				strBuilder.AppendLine("        self:GetLeo(k, Text, v)");
				strBuilder.AppendLine($"    end");
				strBuilder.AppendLine();
			}
			strBuilder.AppendLine("end");
			strBuilder.AppendLine();
			foreach (var func in listHandlers)
			{
				strBuilder.AppendLine($"function rtn:{func}()");
				strBuilder.AppendLine("end");
			}
			strBuilder.AppendLine();
			strBuilder.AppendLine("return rtn");

			File.WriteAllText(destPath, strBuilder.ToString(), new System.Text.UTF8Encoding(false));

			if (!string.IsNullOrEmpty(comparePaths))
			{
				string fileNameAndParameter = string.Format("{0}&{1}", PlayerPrefs.GetString(UISettingEditor.LuaCompareExePathStoreKey), comparePaths);
				UnityExecuteOutExe.Run(fileNameAndParameter);
			}
			Debug.Log("Generate complete: " + fileName);
		}

		static public void CopyLeoCode(UIFacade facade)
		{
		    System.Text.StringBuilder strBuilder = new System.Text.StringBuilder();
		    var listTexts = new List<UIElement>();
        	var listHandlers = new List<string>();
        	foreach (UIElement element in facade.uiElements)
        	{
        		strBuilder.AppendFormat("    {0}:GetUI(\"{1}\", {2})\n", facade.uiName, element.name, GetLeoTranslate(element.type));
        	}
		    GUIUtility.systemCopyBuffer = strBuilder.ToString();
		}

		private static string GetLeoTranslate(UIElement.ElementType elementType)
		{
			switch (elementType)
			{
				case UIElement.ElementType.TRANSFORM:
					return "Container";
				case UIElement.ElementType.GRID:
					return "EasyList";
				case UIElement.ElementType.BUTTON:
					return "Btn";
				case UIElement.ElementType.IMAGE:
					return "Image";
				case UIElement.ElementType.RAWIMAGE:
					return "RawImage";
			}
			return elementType.ToString();
		}
		static public string GetControllerAssetPath(Object obj)
		{
			var type = PrefabUtility.GetPrefabAssetType(obj);
			if (type == PrefabAssetType.Regular || type == PrefabAssetType.Variant)
			{
				UnityEngine.Object parentObject = obj;
				if (type == PrefabAssetType.Variant)
				{
					parentObject = PrefabUtility.GetCorrespondingObjectFromSource(obj);
				}
				string path = AssetDatabase.GetAssetPath(parentObject);
				path = path.Substring(AssetRes.Length);
				path = path.Replace(".prefab", "");
				return path;
			}
#if UNITY_2018_3_OR_NEWER
			else if (_prefabStage != null)
			{
				string path = _prefabStage.prefabAssetPath.Substring(AssetRes.Length);
				path = path.Replace(".prefab", "");
				return path;
			}
#endif
			else
			{
				Debug.LogError("GetControllerAssetPath error");
				return "";
			}
		}

		//检测Controller Lua存储路径是否为空
		static public bool CheckControllerLuaSavePathEmpty(string path)
		{
			string luaControllerSavePath = path;
			if (string.IsNullOrEmpty(luaControllerSavePath))
			{
				EditorUtility.DisplayDialog("Error", "ControllerLua SavePath is empty \n\nPlease browse in Menu:Zeus/Framework/UIFacadeSetting.", "OK");
				return true;
			}
			return false;
		}
		//检测存储路径是否存在
		//不存在则创建
		static public void CheckLuaSavePathExists(string path)
		{
			string luaViewSavePath = path;
			if (!Directory.Exists(luaViewSavePath))
			{
				Directory.CreateDirectory(luaViewSavePath);
				//EditorUtility.DisplayDialog("Error", "ViewLua SavePath is empty \n\nPlease browse in Menu:Zeus/Framework/UIFacadeSetting.", "OK");
				//return true;
			}
			//return false;
		}

		//检测对比工具路径是否为空
		static public bool CheckCompareExePathEmpty()
		{
			string compareExePath = PlayerPrefs.GetString(UISettingEditor.LuaCompareExePathStoreKey);
			if (string.IsNullOrEmpty(compareExePath))
			{
				EditorUtility.DisplayDialog("Error", "LuaCompareExePath is empty \n\nPlease browse one Compare.exe in Zeus->UI->UIFacade Setting.", "OK");
				return true;
			}
			return false;
		}

		//将moduleName从"\"转为"."
		static public string GetModuleClassPath(string moduleName)
		{
			string returnStr = "";
			string[] temp = moduleName.Trim().Replace('\\', '/').Split('/');
			for (int i = 0; i < temp.Length; i++)
			{
				if (!string.IsNullOrEmpty(temp[i]))
				{
					returnStr += (temp[i] + ".");
				}
			}
			if (returnStr.EndsWith("."))
			{
				returnStr = returnStr.Substring(0, returnStr.LastIndexOf('.'));
			}
			return returnStr;
		}
	}
}

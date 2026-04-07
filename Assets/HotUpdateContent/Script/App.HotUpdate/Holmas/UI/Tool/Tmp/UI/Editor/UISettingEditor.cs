using UnityEngine;
using UnityEditor;
using System.IO;

namespace Zeus.Framework.UI
{
    public class UISettingEditor : EditorWindow
    {
        public static string LuaGeneratePathStoreKey = Application.dataPath + "/luaGeneratePath";
        public static string LuaCompareExePathStoreKey = Application.dataPath + "/luaCompareExePath";
        private string _generateLuaPath = null;
        private string _compareExePath = null;

        [MenuItem("Zeus/Setting/UI/UI Setting")]
        private static void Open()
        {
            GetWindow<UISettingEditor>();
        }

        [MenuItem("Zeus/Setting/UI/Gen UIRequirList.lua")]
        private static void GenUIRequirList()
        {
            string generateLuaPath = PlayerPrefs.GetString(LuaGeneratePathStoreKey);
            if (string.IsNullOrEmpty(generateLuaPath))
            {
                EditorUtility.DisplayDialog("Error", "Lua文件保存目录为空，请设置保存目录(Zeus->UI->UIFacade Setting)", "OK");
                return;
            }
            string destPath = generateLuaPath + "/UIRequireList.lua";

            string tempGenerateLuaPath = generateLuaPath.Replace('/', '\\');

            //e.g:UI
            string parentDirectoryName = tempGenerateLuaPath.Substring(tempGenerateLuaPath.LastIndexOf('\\') + 1) + "\\";

            DirectoryInfo dInfo = new DirectoryInfo(generateLuaPath);
            var controllerFileInfos = dInfo.GetFiles("UI_*_Controller.lua", SearchOption.AllDirectories);
            var managerFileInfos = dInfo.GetFiles("UI*Manager.lua", SearchOption.AllDirectories);
            System.Collections.Generic.List<FileInfo> fileList = new System.Collections.Generic.List<FileInfo>(controllerFileInfos);
            fileList.AddRange(managerFileInfos);
            var fileInfos = fileList.ToArray();
            var controllerCount = controllerFileInfos.Length;

            System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder();
            stringBuilder.AppendLine("UIControllerList = {}");
            stringBuilder.AppendLine();

            string fileNameWithoutExtension = "";
            string moduleDirectory = "";
            FileInfo fi = null;

            string lineText = string.Empty;
            for (int i = 0; i < fileInfos.Length; i++)
            {
                fi = fileInfos[i];
                //e.g:UI_test_Text_Controller
                fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fi.FullName);

                moduleDirectory = fi.DirectoryName.Substring(fi.DirectoryName.LastIndexOf(parentDirectoryName));
                //e.g:UI.Test.TestText
                moduleDirectory = moduleDirectory.Replace('/', '.').Replace('\\', '.');

                if (i + 1 > controllerCount)
                {
                    //e.g:UIxxxxManager = require("UI.xxxxx.UIxxxxController")
                    lineText = string.Format("require(\"{0}.{1}\")", moduleDirectory, fileNameWithoutExtension);
                }
                else
                {
                    //e.g:UI_test_Text_Controller = require("UI.Test.TestText.UI_test_Text_Controller")
                    lineText = string.Format("UIControllerList.{0} = require(\"{1}.{2}\")",
                        fileNameWithoutExtension, moduleDirectory, fileNameWithoutExtension);
                }

                stringBuilder.AppendLine(lineText);
            }
            File.WriteAllText(destPath, stringBuilder.ToString());
            EditorUtility.DisplayDialog("Complete", "UIRequireList.lua Generate Complete!", "OK");
        }

        private void OnEnable()
        {
            titleContent.text = "UISetting";

            if (_compareExePath == null)
            {
                _compareExePath = PlayerPrefs.GetString(LuaCompareExePathStoreKey);
            }
            if (_generateLuaPath == null)
            {
                _generateLuaPath = PlayerPrefs.GetString(LuaGeneratePathStoreKey);
            }         
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Source Compare.exe Path:(支持的对比合并工具:Araxis Merge、Beyond Compare、DiffMerge、KDiff3等)");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField(_compareExePath);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string compareExePath = EditorUtility.OpenFilePanelWithFilters("Choose compare.exe", Application.dataPath, new string[] { "EXE", "exe" });
                if (!string.IsNullOrEmpty(compareExePath))
                {
                    PlayerPrefs.SetString(LuaCompareExePathStoreKey, compareExePath);
                    _compareExePath = compareExePath;
                    EditorGUILayout.TextField(_compareExePath);

                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("");


            EditorGUILayout.LabelField("Lua Path Main Directory:");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField(_generateLuaPath);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string directory = _generateLuaPath.Contains("LuaProject") ? _generateLuaPath : Application.dataPath;
                string generateViewLuaPath = EditorUtility.OpenFolderPanel("Load the main directory where the lua files are stored", directory, "Select Directory");
                if (!string.IsNullOrEmpty(generateViewLuaPath))
                {
                    PlayerPrefs.SetString(LuaGeneratePathStoreKey, generateViewLuaPath);
                    _generateLuaPath = generateViewLuaPath;
                    EditorGUILayout.TextField(_generateLuaPath);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(""); 
        }

    }
}

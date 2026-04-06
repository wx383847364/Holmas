using System;
using System.IO;
using UiPrefabGenerator.Core.Profile;
using UiPrefabGenerator.Core.Request;
using UiPrefabGenerator.Core.Result;
using UiPrefabGenerator.Editor.Bridge;
using UiPrefabGenerator.Editor.Generation;
using UiPrefabGenerator.Editor.Template;
using UnityEditor;
using UnityEngine;

namespace UiPrefabGenerator.Editor.Window
{
    public sealed class UiPrefabGenerationWindow : EditorWindow
    {
        private Texture2D _sourceImage;
        private UiGenerationTemplate _template;
        private string[] _templatePaths = Array.Empty<string>();
        private string[] _templateLabels = Array.Empty<string>();
        private int _selectedTemplateIndex;
        private string _pageId = string.Empty;
        private string _pageTitle = string.Empty;
        private string _prefabName = string.Empty;
        private string _notes = string.Empty;
        private string _currentTaskDirectory = string.Empty;
        private UiGenerationAnalysisResult _analysisResult;
        private UiGenerationExecutionResult _executionResult;
        private Vector2 _scrollPosition;

        [MenuItem("UiPrefabGenerator/Window/Portrait Generator")]
        public static void Open()
        {
            var window = GetWindow<UiPrefabGenerationWindow>("UI Portrait Generator");
            window.minSize = new Vector2(560f, 520f);
        }

        private void OnEnable()
        {
            ReloadTemplates();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.Space(4f);
            DrawHeader();
            EditorGUILayout.Space(8f);
            DrawTemplateSection();
            EditorGUILayout.Space(8f);
            DrawInputSection();
            EditorGUILayout.Space(8f);
            DrawTaskSection();
            EditorGUILayout.Space(8f);
            DrawAnalysisSection();
            EditorGUILayout.Space(8f);
            DrawExecutionSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("竖屏 UI 生成工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "这条链路固定为：图片 -> request.json -> 自动分析或手动回写 -> 人工确认 -> 自动生成 prefab。",
                MessageType.Info);
        }

        private void DrawTemplateSection()
        {
            EditorGUILayout.LabelField("模板", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            int newIndex = EditorGUILayout.Popup("当前模板", _selectedTemplateIndex, _templateLabels);
            if (newIndex != _selectedTemplateIndex)
            {
                _selectedTemplateIndex = newIndex;
                LoadSelectedTemplate();
            }

            if (GUILayout.Button("刷新模板", GUILayout.Width(90f)))
            {
                ReloadTemplates();
            }
            EditorGUILayout.EndHorizontal();

            if (_template == null)
            {
                _template = UiGenerationTemplateStore.BuildPortraitWechatDefault();
            }

            _template.TemplateName = EditorGUILayout.TextField("模板名", _template.TemplateName);
            _template.ProfileId = EditorGUILayout.TextField("ProfileId", _template.ProfileId);
            _template.TargetPlatform = EditorGUILayout.TextField("目标平台", _template.TargetPlatform);
            _template.Orientation = EditorGUILayout.TextField("方向", _template.Orientation);
            _template.ReferenceResolutionWidth = EditorGUILayout.IntField("参考宽度", _template.ReferenceResolutionWidth);
            _template.ReferenceResolutionHeight = EditorGUILayout.IntField("参考高度", _template.ReferenceResolutionHeight);
            _template.MatchMode = EditorGUILayout.TextField("适配模式", _template.MatchMode);
            _template.MatchWidthOrHeight = EditorGUILayout.Slider("宽高匹配", _template.MatchWidthOrHeight, 0f, 1f);
            _template.AssetRoot = EditorGUILayout.TextField("资源根目录", _template.AssetRoot);
            _template.DraftPrefabRoot = EditorGUILayout.TextField("Prefab 输出目录", _template.DraftPrefabRoot);
            _template.ManualReviewRequired = EditorGUILayout.Toggle("生成前人工确认", _template.ManualReviewRequired);
            _template.AutoPingPrefabAfterGeneration = EditorGUILayout.Toggle("生成后自动定位 Prefab", _template.AutoPingPrefabAfterGeneration);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("保存当前模板", GUILayout.Height(24f)))
            {
                SaveCurrentTemplate();
            }

            if (GUILayout.Button("恢复竖屏默认模板", GUILayout.Height(24f)))
            {
                _template = UiGenerationTemplateStore.BuildPortraitWechatDefault();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawInputSection()
        {
            EditorGUILayout.LabelField("输入", EditorStyles.boldLabel);
            _sourceImage = (Texture2D)EditorGUILayout.ObjectField("设计图", _sourceImage, typeof(Texture2D), false);
            _pageId = EditorGUILayout.TextField("PageId", _pageId);
            _pageTitle = EditorGUILayout.TextField("PageTitle", _pageTitle);
            _prefabName = EditorGUILayout.TextField("PrefabName", _prefabName);
            _notes = EditorGUILayout.TextField("备注", _notes);

            if (GUILayout.Button("生成请求", GUILayout.Height(28f)))
            {
                CreateTaskRequest();
            }
        }

        private void DrawTaskSection()
        {
            EditorGUILayout.LabelField("任务", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(
                string.IsNullOrWhiteSpace(_currentTaskDirectory) ? "尚未生成请求。" : _currentTaskDirectory,
                EditorStyles.textField,
                GUILayout.Height(36f));

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_currentTaskDirectory)))
            {
                if (GUILayout.Button("刷新分析结果", GUILayout.Height(24f)))
                {
                    RefreshAnalysisResult();
                }

                if (GUILayout.Button("打开任务目录", GUILayout.Height(24f)))
                {
                    EditorUtility.RevealInFinder(UiGenerationDataPaths.ToAbsolutePath(_currentTaskDirectory));
                }
            }
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_currentTaskDirectory)))
            {
                if (GUILayout.Button("自动分析并回写结果", GUILayout.Height(26f)))
                {
                    RunAutoAnalysisAndRefresh();
                }
            }
        }

        private void DrawAnalysisSection()
        {
            EditorGUILayout.LabelField("分析结果", EditorStyles.boldLabel);
            if (_analysisResult == null)
            {
                EditorGUILayout.HelpBox("还没有读取到分析结果。可以点“自动分析并回写结果”，或手动回写 analysis_result.json / design_packet.json / ui_prefab_spec.json / resource_match_report.json 后再点“刷新分析结果”。", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("TaskId", _analysisResult.TaskId);
            EditorGUILayout.LabelField("Spec 节点数", _analysisResult.UiPrefabSpec != null ? _analysisResult.UiPrefabSpec.Nodes.Count.ToString() : "0");
            EditorGUILayout.LabelField("资源匹配数", _analysisResult.ResourceMatchReport != null ? _analysisResult.ResourceMatchReport.Matches.Count.ToString() : "0");

            if (_analysisResult.UiPrefabSpec != null && _analysisResult.UiPrefabSpec.Nodes != null)
            {
                EditorGUILayout.LabelField("节点树");
                for (int i = 0; i < _analysisResult.UiPrefabSpec.Nodes.Count; i++)
                {
                    var node = _analysisResult.UiPrefabSpec.Nodes[i];
                    if (node != null)
                    {
                        EditorGUILayout.LabelField("- " + node.NodeName + " [" + node.NodeId + "]");
                    }
                }
            }

            if (_analysisResult.ResourceMatchReport != null && _analysisResult.ResourceMatchReport.Matches != null)
            {
                EditorGUILayout.LabelField("资源候选");
                for (int i = 0; i < _analysisResult.ResourceMatchReport.Matches.Count; i++)
                {
                    var match = _analysisResult.ResourceMatchReport.Matches[i];
                    if (match != null)
                    {
                        EditorGUILayout.LabelField("- " + match.AssetSlot + " -> " + match.SelectedAssetPath);
                    }
                }
            }

            string summary = UiGenerationTaskStorage.LoadAnalysisSummary(_currentTaskDirectory);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                EditorGUILayout.LabelField("分析摘要");
                EditorGUILayout.TextArea(summary, GUILayout.MinHeight(80f));
            }

            if (_analysisResult.Warnings != null && _analysisResult.Warnings.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", _analysisResult.Warnings.ToArray()), MessageType.Warning);
            }

            if (_analysisResult.Errors != null && _analysisResult.Errors.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", _analysisResult.Errors.ToArray()), MessageType.Error);
            }

            using (new EditorGUI.DisabledScope(_template != null && _template.ManualReviewRequired && _analysisResult == null))
            {
                if (GUILayout.Button("确认并生成 Prefab", GUILayout.Height(28f)))
                {
                    GeneratePrefab();
                }
            }
        }

        private void DrawExecutionSection()
        {
            EditorGUILayout.LabelField("生成结果", EditorStyles.boldLabel);
            if (_executionResult == null)
            {
                EditorGUILayout.HelpBox("还没有执行生成。", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("生成状态", _executionResult.Success ? "成功" : "失败");
            EditorGUILayout.LabelField("Prefab 路径", _executionResult.PrefabPath);
            EditorGUILayout.LabelField("Manifest 校验", _executionResult.ManifestValidationPassed ? "通过" : "失败");
            EditorGUILayout.LabelField("结构校验", _executionResult.StructureValidationPassed ? "通过" : "失败");

            if (_executionResult.AutoBoundAssets.Count > 0)
            {
                EditorGUILayout.LabelField("自动绑定资源");
                for (int i = 0; i < _executionResult.AutoBoundAssets.Count; i++)
                {
                    EditorGUILayout.LabelField("- " + _executionResult.AutoBoundAssets[i]);
                }
            }

            if (_executionResult.UnmatchedAssetSlots.Count > 0)
            {
                EditorGUILayout.HelpBox("未匹配资源位：\n" + string.Join("\n", _executionResult.UnmatchedAssetSlots.ToArray()), MessageType.Warning);
            }

            if (_executionResult.Errors.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", _executionResult.Errors.ToArray()), MessageType.Error);
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_executionResult.PrefabPath)))
            {
                if (GUILayout.Button("定位生成的 Prefab", GUILayout.Height(24f)))
                {
                    UnityEngine.Object prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_executionResult.PrefabPath);
                    if (prefab != null)
                    {
                        EditorGUIUtility.PingObject(prefab);
                        Selection.activeObject = prefab;
                    }
                }
            }
        }

        private void ReloadTemplates()
        {
            _templatePaths = UiGenerationTemplateStore.GetTemplateAssetPaths();
            _templateLabels = new string[_templatePaths.Length];
            for (int i = 0; i < _templatePaths.Length; i++)
            {
                UiGenerationTemplate template = UiGenerationTemplateStore.LoadTemplate(_templatePaths[i]);
                _templateLabels[i] = string.IsNullOrWhiteSpace(template.TemplateName)
                    ? Path.GetFileNameWithoutExtension(_templatePaths[i])
                    : template.TemplateName;
            }

            if (_templatePaths.Length == 0)
            {
                _templatePaths = new[] { UiGenerationDataPaths.DefaultPortraitTemplatePath };
                _templateLabels = new[] { "holmas_portrait_wechat_default" };
                _selectedTemplateIndex = 0;
            }
            else
            {
                _selectedTemplateIndex = Mathf.Clamp(_selectedTemplateIndex, 0, _templatePaths.Length - 1);
            }

            LoadSelectedTemplate();
        }

        private void LoadSelectedTemplate()
        {
            if (_templatePaths.Length == 0)
            {
                _template = UiGenerationTemplateStore.BuildPortraitWechatDefault();
                return;
            }

            _template = UiGenerationTemplateStore.LoadTemplate(_templatePaths[_selectedTemplateIndex]);
        }

        private void SaveCurrentTemplate()
        {
            if (_template == null)
            {
                _template = UiGenerationTemplateStore.BuildPortraitWechatDefault();
            }

            string fileName = string.IsNullOrWhiteSpace(_template.TemplateName)
                ? "ui_generation_template"
                : _template.TemplateName;
            string assetPath = UiGenerationDataPaths.ProjectDefaultsRoot + "/" + fileName + ".json";
            UiGenerationTemplateStore.SaveTemplate(assetPath, _template);
            ReloadTemplates();
        }

        private void CreateTaskRequest()
        {
            if (_sourceImage == null)
            {
                EditorUtility.DisplayDialog("缺少图片", "请先拖入一张设计图。", "确定");
                return;
            }

            if (string.IsNullOrWhiteSpace(_pageId) || string.IsNullOrWhiteSpace(_prefabName))
            {
                EditorUtility.DisplayDialog("缺少字段", "PageId 和 PrefabName 不能为空。", "确定");
                return;
            }

            UiGenerationTaskRequest request = UiGenerationTaskStorage.BuildRequest(
                _template,
                _pageId,
                _pageTitle,
                _prefabName,
                _notes);
            _currentTaskDirectory = UiGenerationTaskStorage.CreateTask(request, _sourceImage);
            _analysisResult = null;
            _executionResult = null;
            EditorUtility.DisplayDialog("请求已生成", "已写入任务目录：\n" + _currentTaskDirectory, "确定");
        }

        private void RefreshAnalysisResult()
        {
            string error;
            if (!TryRefreshAnalysisResult(out error))
            {
                EditorUtility.DisplayDialog("读取失败", error, "确定");
            }
        }

        private bool TryRefreshAnalysisResult(out string error)
        {
            UiGenerationAnalysisResult result;
            if (!UiGenerationTaskStorage.TryLoadAnalysisResult(_currentTaskDirectory, out result, out error))
            {
                return false;
            }

            _analysisResult = result;
            _executionResult = null;
            return true;
        }

        private void RunAutoAnalysisAndRefresh()
        {
            if (string.IsNullOrWhiteSpace(_currentTaskDirectory))
            {
                EditorUtility.DisplayDialog("缺少任务", "请先生成请求。", "确定");
                return;
            }

            if (!UiGenerationTaskStorage.TryLoadTaskRequest(_currentTaskDirectory, out _))
            {
                EditorUtility.DisplayDialog("缺少请求", "请先生成 request.json，再执行自动分析。", "确定");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("自动分析并回写结果", "正在执行本地自动分析脚本...", 0.1f);
                UiPrefabGeneratorAutoAnalysisResult result = UiPrefabGeneratorAutoAnalysisBridge.RunTaskAutoAnalysis(_currentTaskDirectory);
                if (!result.Success)
                {
                    EditorUtility.DisplayDialog("自动分析失败", result.GetFailureMessage(), "确定");
                    return;
                }

                string refreshError;
                if (!TryRefreshAnalysisResult(out refreshError))
                {
                    EditorUtility.DisplayDialog(
                        "自动分析成功，但刷新失败",
                        refreshError + "\n\n日志：\n" + result.LogPath,
                        "确定");
                    return;
                }

                Debug.Log("自动分析并回写结果完成: " + _currentTaskDirectory + "\n日志: " + result.LogPath);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void GeneratePrefab()
        {
            if (_analysisResult == null)
            {
                EditorUtility.DisplayDialog("缺少分析结果", "请先刷新分析结果。", "确定");
                return;
            }

            var service = new UiGenerationExecutionService();
            _executionResult = service.Execute(_currentTaskDirectory, _template, _analysisResult);
            if (_executionResult.Success && _template != null && _template.AutoPingPrefabAfterGeneration)
            {
                UnityEngine.Object prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_executionResult.PrefabPath);
                if (prefab != null)
                {
                    EditorGUIUtility.PingObject(prefab);
                    Selection.activeObject = prefab;
                }
            }
        }
    }
}

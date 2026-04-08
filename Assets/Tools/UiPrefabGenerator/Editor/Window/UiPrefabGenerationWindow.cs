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
        private const string LastTaskDirectorySessionKey = "UiPrefabGenerator.Editor.Window.LastTaskDirectory";

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
        private Texture2D _previewRenderTexture;
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
            RestoreLastTaskIfPossible();
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
            if (GUILayout.Button("导入已有任务", GUILayout.Height(24f)))
            {
                ImportTaskFromPicker();
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(SessionState.GetString(LastTaskDirectorySessionKey, string.Empty))))
            {
                if (GUILayout.Button("恢复上次任务", GUILayout.Height(24f)))
                {
                    RestoreLastTaskIfPossible(true);
                }
            }
            EditorGUILayout.EndHorizontal();

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
                EditorGUILayout.HelpBox("还没有读取到分析结果。可以点“自动分析并回写结果”，或手动回写 analysis_result.json / design_packet.json / design_packet_intake_assessment.json / gating_report.json / ui_prefab_spec.json / resource_match_report.json，以及 visual_understanding.json / visual_review_report.json / preview_render_plan.json / preview_render.png / preview_diff_report.json 后再点“刷新分析结果”。", MessageType.None);
                return;
            }

            UiGenerationAnalysisStatusSummary statusSummary = UiGenerationAnalysisStatusSummarizer.Build(_analysisResult);

            EditorGUILayout.LabelField("分析状态", statusSummary.StatusLabel);
            EditorGUILayout.LabelField(
                "问题计数",
                string.Format(
                    "未解决项 {0} | blocking {1} | review {2} | 未匹配资源槽 {3} | 警告 {4} | 错误 {5}",
                    statusSummary.UnresolvedItemCount,
                    statusSummary.BlockingReviewIssueCount,
                    statusSummary.ReviewIssueCount,
                    statusSummary.UnresolvedSlotCount,
                    statusSummary.WarningCount,
                    statusSummary.ErrorCount));
            EditorGUILayout.LabelField(
                "结果覆盖",
                string.Format(
                    "节点 {0} | Bindings {1} | 交互 {2} | 证据元素 {3} | preview {4}/{5} | 资源已选中 {6}/{7}",
                    statusSummary.NodeCount,
                    statusSummary.BindingCount,
                    statusSummary.InteractionCount,
                    _analysisResult.VisualUnderstanding != null && _analysisResult.VisualUnderstanding.Elements != null
                        ? _analysisResult.VisualUnderstanding.Elements.Count
                        : 0,
                    statusSummary.PreviewNodeCount,
                    statusSummary.PreviewDiffRegionCount,
                    statusSummary.SelectedMatchCount,
                    statusSummary.MatchCount));
            EditorGUILayout.LabelField("结果强度", statusSummary.IsWeakResult ? "偏弱" : "正常");
            EditorGUILayout.HelpBox(statusSummary.StatusSummary, statusSummary.StatusMessageType);

            if (statusSummary.IsWeakResult)
            {
                EditorGUILayout.HelpBox("弱结果提示： " + statusSummary.WeakResultSummary, MessageType.Warning);
            }

            EditorGUILayout.LabelField("TaskId", _analysisResult.TaskId);
            EditorGUILayout.LabelField("Spec 节点数", statusSummary.NodeCount.ToString());
            EditorGUILayout.LabelField("资源匹配数", statusSummary.MatchCount.ToString());
            EditorGUILayout.LabelField("低置信元素", statusSummary.LowConfidenceElementCount.ToString());

            if (statusSummary.UnresolvedSummaries.Count > 0)
            {
                EditorGUILayout.HelpBox("未解决摘要：\n" + string.Join("\n", statusSummary.UnresolvedSummaries.ToArray()), MessageType.Warning);
            }

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

            if (_analysisResult.VisualUnderstanding != null && _analysisResult.VisualUnderstanding.Elements != null && _analysisResult.VisualUnderstanding.Elements.Count > 0)
            {
                EditorGUILayout.LabelField("视觉证据");
                for (int i = 0; i < _analysisResult.VisualUnderstanding.Elements.Count; i++)
                {
                    var element = _analysisResult.VisualUnderstanding.Elements[i];
                    if (element == null)
                    {
                        continue;
                    }

                    string text = element.Text != null && !string.IsNullOrWhiteSpace(element.Text.NormalizedText)
                        ? " text=" + element.Text.NormalizedText
                        : string.Empty;
                    EditorGUILayout.LabelField(
                        "- " + element.SemanticRole + " [" + element.ElementId + "] conf=" + element.Confidence.ToString("0.00") + text);
                }
            }

            if (_previewRenderTexture != null)
            {
                EditorGUILayout.LabelField("Preview Render");
                float maxWidth = Mathf.Min(position.width - 48f, 280f);
                float aspect = _previewRenderTexture.width > 0
                    ? _previewRenderTexture.height / (float)_previewRenderTexture.width
                    : 1f;
                Rect previewRect = GUILayoutUtility.GetRect(maxWidth, maxWidth * aspect, GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(previewRect, _previewRenderTexture, null, ScaleMode.ScaleToFit);
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

            if (_analysisResult.PreviewRenderPlan != null && _analysisResult.PreviewRenderPlan.Nodes != null && _analysisResult.PreviewRenderPlan.Nodes.Count > 0)
            {
                EditorGUILayout.LabelField("结构化 Preview");
                for (int i = 0; i < _analysisResult.PreviewRenderPlan.Nodes.Count; i++)
                {
                    var node = _analysisResult.PreviewRenderPlan.Nodes[i];
                    if (node == null)
                    {
                        continue;
                    }

                    EditorGUILayout.LabelField(
                        "- " + node.NodeType + " [" + node.NodeId + "] bounds=("
                        + node.Bounds.X.ToString("0.00") + ", "
                        + node.Bounds.Y.ToString("0.00") + ", "
                        + node.Bounds.Width.ToString("0.00") + ", "
                        + node.Bounds.Height.ToString("0.00") + ")");
                }
            }

            if (_analysisResult.PreviewDiffReport != null && _analysisResult.PreviewDiffReport.Regions != null && _analysisResult.PreviewDiffReport.Regions.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Preview diff：\n"
                    + string.Join(
                        "\n",
                        _analysisResult.PreviewDiffReport.Regions.ConvertAll(region =>
                            "- " + region.DiffKind + ": " + region.Summary).ToArray()),
                    MessageType.Warning);
            }

            string summary = UiGenerationTaskStorage.LoadAnalysisSummary(_currentTaskDirectory);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                EditorGUILayout.LabelField("分析摘要");
                EditorGUILayout.TextArea(summary, GUILayout.MinHeight(80f));
            }

            if (statusSummary.WarningSummaries.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", statusSummary.WarningSummaries.ToArray()), MessageType.Warning);
            }

            if (statusSummary.ErrorSummaries.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", statusSummary.ErrorSummaries.ToArray()), MessageType.Error);
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
            _previewRenderTexture = null;
            RememberLastTaskDirectory(_currentTaskDirectory);
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
            _previewRenderTexture = UiGenerationTaskStorage.LoadPreviewRenderTexture(_currentTaskDirectory);
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

        private void ImportTaskFromPicker()
        {
            string initialDirectory = UiGenerationDataPaths.ToAbsolutePath(UiGenerationDataPaths.TasksRoot);
            if (!string.IsNullOrWhiteSpace(_currentTaskDirectory))
            {
                initialDirectory = UiGenerationDataPaths.ToAbsolutePath(_currentTaskDirectory);
            }

            string selectedDirectory = EditorUtility.OpenFolderPanel("选择已有任务目录", initialDirectory, string.Empty);
            if (string.IsNullOrWhiteSpace(selectedDirectory))
            {
                return;
            }

            string taskDirectory = NormalizeTaskDirectoryAssetPath(selectedDirectory);
            if (string.IsNullOrWhiteSpace(taskDirectory))
            {
                EditorUtility.DisplayDialog("导入失败", "请选择项目内 Assets/UiPrefabGeneratorData/Tasks 下的任务目录。", "确定");
                return;
            }

            string error;
            if (!ImportTask(taskDirectory, out error))
            {
                EditorUtility.DisplayDialog("导入失败", error, "确定");
                return;
            }

            EditorUtility.DisplayDialog("导入完成", "已恢复任务：\n" + taskDirectory, "确定");
        }

        private void RestoreLastTaskIfPossible()
        {
            RestoreLastTaskIfPossible(false);
        }

        private void RestoreLastTaskIfPossible(bool showDialogOnFailure)
        {
            if (!string.IsNullOrWhiteSpace(_currentTaskDirectory))
            {
                return;
            }

            string taskDirectory = SessionState.GetString(LastTaskDirectorySessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(taskDirectory))
            {
                return;
            }

            string error;
            if (ImportTask(taskDirectory, out error))
            {
                return;
            }

            SessionState.EraseString(LastTaskDirectorySessionKey);
            if (showDialogOnFailure)
            {
                EditorUtility.DisplayDialog("恢复失败", error, "确定");
            }
        }

        private bool ImportTask(string taskDirectory, out string error)
        {
            error = string.Empty;
            UiGenerationTaskRequest request;
            if (!UiGenerationTaskStorage.TryLoadTaskRequest(taskDirectory, out request) || request == null)
            {
                error = "任务目录中缺少可读取的 request.json。";
                return false;
            }

            _currentTaskDirectory = taskDirectory;
            RememberLastTaskDirectory(taskDirectory);
            ApplyTemplateFromRequest(request);
            _sourceImage = LoadSourceImageForRequest(request);
            _pageId = request.PageId ?? string.Empty;
            _pageTitle = request.PageTitle ?? string.Empty;
            _prefabName = request.PrefabName ?? string.Empty;
            _notes = request.Notes ?? string.Empty;

            string analysisError;
            if (!TryRefreshAnalysisResult(out analysisError))
            {
                _analysisResult = null;
            }

            UiGenerationExecutionResult executionResult;
            string executionError;
            if (UiGenerationTaskStorage.TryLoadExecutionResult(taskDirectory, out executionResult, out executionError))
            {
                _executionResult = executionResult;
            }
            else
            {
                _executionResult = null;
            }

            return true;
        }

        private void ApplyTemplateFromRequest(UiGenerationTaskRequest request)
        {
            int templateIndex = FindTemplateIndexByName(request.TemplateName);
            if (templateIndex >= 0)
            {
                _selectedTemplateIndex = templateIndex;
                LoadSelectedTemplate();
            }
            else
            {
                _template = UiGenerationTemplateStore.BuildPortraitWechatDefault();
            }

            if (_template == null)
            {
                _template = UiGenerationTemplateStore.BuildPortraitWechatDefault();
            }

            _template.TemplateName = request.TemplateName ?? string.Empty;
            _template.ProfileId = request.ProfileId ?? string.Empty;
            _template.TargetPlatform = request.TargetPlatform ?? string.Empty;
            _template.Orientation = request.Orientation ?? string.Empty;
            _template.ReferenceResolutionWidth = request.ReferenceResolutionWidth;
            _template.ReferenceResolutionHeight = request.ReferenceResolutionHeight;
            _template.AssetRoot = request.AssetRoot ?? string.Empty;
            _template.DraftPrefabRoot = request.DraftPrefabRoot ?? string.Empty;
            _template.PageType = request.PageType ?? string.Empty;
            _template.MustHaveNodes.Clear();
            _template.MustHaveInteractions.Clear();
            if (request.MustHaveNodes != null)
            {
                _template.MustHaveNodes.AddRange(request.MustHaveNodes);
            }

            if (request.MustHaveInteractions != null)
            {
                _template.MustHaveInteractions.AddRange(request.MustHaveInteractions);
            }
        }

        private int FindTemplateIndexByName(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
            {
                return -1;
            }

            for (int i = 0; i < _templateLabels.Length; i++)
            {
                if (string.Equals(_templateLabels[i], templateName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            for (int i = 0; i < _templatePaths.Length; i++)
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(_templatePaths[i]), templateName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static Texture2D LoadSourceImageForRequest(UiGenerationTaskRequest request)
        {
            if (request == null)
            {
                return null;
            }

            Texture2D taskCopy = LoadTextureAtPath(request.SourceImageTaskAssetPath);
            if (taskCopy != null)
            {
                return taskCopy;
            }

            return LoadTextureAtPath(request.SourceImageAssetPath);
        }

        private static Texture2D LoadTextureAtPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static string NormalizeTaskDirectoryAssetPath(string selectedDirectory)
        {
            string normalized = selectedDirectory.Replace('\\', '/').TrimEnd('/');
            string assetPath = UiGenerationDataPaths.ToAssetPath(normalized);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            return assetPath.StartsWith(UiGenerationDataPaths.TasksRoot + "/", StringComparison.Ordinal)
                || string.Equals(assetPath, UiGenerationDataPaths.TasksRoot, StringComparison.Ordinal)
                ? assetPath
                : string.Empty;
        }

        private static void RememberLastTaskDirectory(string taskDirectory)
        {
            if (!string.IsNullOrWhiteSpace(taskDirectory))
            {
                SessionState.SetString(LastTaskDirectorySessionKey, taskDirectory);
            }
        }
    }
}

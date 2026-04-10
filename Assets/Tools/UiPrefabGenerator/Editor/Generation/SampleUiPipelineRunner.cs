using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UiPrefabGenerator.Core.Manifest;
using UiPrefabGenerator.Core.Profile;
using UiPrefabGenerator.Core.Schema;
using UiPrefabGenerator.Editor.Validation;

namespace UiPrefabGenerator.Editor.Generation
{
    public interface ISampleUiPipelineRunner
    {
        SampleUiPipelineReport Run(SampleUiPipelineRequest request);
    }

    [Serializable]
    public sealed class SampleUiPipelineRequest
    {
        public DesignPacket DesignPacket;
        public IProjectUiProfile Profile;
    }

    [Serializable]
    public sealed class SampleUiPipelineStageReport
    {
        public string StageId = string.Empty;
        public bool Success;
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
    }

    [Serializable]
    public sealed class SampleUiPipelineReport
    {
        public string ProfileId = string.Empty;
        public string PageId = string.Empty;
        public string PrefabName = string.Empty;
        public string PrefabDraftPath = string.Empty;
        public object IntakeAssessment;
        public string IntakeAssessmentText = string.Empty;
        public UiPrefabSpec Spec = new UiPrefabSpec();
        public UiPrefabGenerationResult PreviewGeneration = new UiPrefabGenerationResult();
        public UiPrefabDraftWriteResult DraftWrite = new UiPrefabDraftWriteResult();
        public UiPrefabValidationResult ManifestValidation = new UiPrefabValidationResult();
        public UiPrefabValidationResult DraftStructureValidation = new UiPrefabValidationResult();
        public object AdapterResult;
        public string ManifestText = string.Empty;
        public string AdapterPlanText = string.Empty;
        public List<SampleUiPipelineStageReport> Stages = new List<SampleUiPipelineStageReport>();
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        public bool Success;
    }

    public sealed class DefaultSampleUiPipelineRunner : ISampleUiPipelineRunner
    {
        private readonly object _intakeAnalyzer;
        private readonly object _interpreter;
        private readonly IUnityPrefabGenerator _previewGenerator;
        private readonly IUnityPrefabDraftWriter _draftWriter;
        private readonly IPrefabBindingManifestValidator _manifestValidator;
        private readonly IPrefabDraftStructureValidator _draftStructureValidator;
        private readonly object _generatedResultConsumer;

        public DefaultSampleUiPipelineRunner(
            object intakeAnalyzer = null,
            object interpreter = null,
            IUnityPrefabGenerator previewGenerator = null,
            IUnityPrefabDraftWriter draftWriter = null,
            IPrefabBindingManifestValidator manifestValidator = null,
            IPrefabDraftStructureValidator draftStructureValidator = null,
            object generatedResultConsumer = null)
        {
            _intakeAnalyzer = intakeAnalyzer ?? CreateDefaultCollaborator(
                "UiPrefabGenerator.Core.Intake.DefaultDesignPacketIntakeAnalyzer, UiPrefabGenerator.Core.Intake");
            _interpreter = interpreter ?? CreateDefaultCollaborator(
                "UiPrefabGenerator.Core.Intake.DefaultDesignPacketToUiPrefabSpecInterpreter, UiPrefabGenerator.Core.Intake");
            _previewGenerator = previewGenerator ?? new PreviewUnityPrefabGenerator();
            _draftWriter = draftWriter ?? new DefaultUnityPrefabDraftWriter();
            _manifestValidator = manifestValidator ?? new DefaultPrefabBindingManifestValidator();
            _draftStructureValidator = draftStructureValidator ?? new DefaultPrefabDraftStructureValidator();
            _generatedResultConsumer = generatedResultConsumer ?? CreateDefaultCollaborator(
                "UiPrefabGenerator.HolmasAdapter.HolmasGeneratedResultConsumer, UiPrefabGenerator.HolmasAdapter");
        }

        public SampleUiPipelineReport Run(SampleUiPipelineRequest request)
        {
            var report = new SampleUiPipelineReport();
            if (request == null)
            {
                AddError(report, "request", "SampleUiPipelineRequest 不能为空。");
                return Finish(report, false);
            }

            if (request.DesignPacket == null)
            {
                AddError(report, "design_packet", "DesignPacket 不能为空。");
                return Finish(report, false);
            }

            if (request.Profile == null)
            {
                AddError(report, "profile", "Profile 不能为空。");
                return Finish(report, false);
            }

            report.ProfileId = request.Profile.ProfileId ?? string.Empty;
            report.PageId = request.DesignPacket.PageId ?? string.Empty;
            report.PrefabName = request.DesignPacket.PrefabName ?? string.Empty;

            object intakeAssessment = InvokeMethod(_intakeAnalyzer, "Analyze", request.DesignPacket);
            report.IntakeAssessment = intakeAssessment;
            report.IntakeAssessmentText = intakeAssessment != null ? JsonUtility.ToJson(intakeAssessment, true) : string.Empty;
            AppendIntake(report, intakeAssessment);
            bool hasBlockingIssues = GetBoolProperty(intakeAssessment, "HasBlockingIssues");
            AddStage(report, "intake", !hasBlockingIssues, hasBlockingIssues ? new[] { "DesignPacket 存在 blocking issues。" } : null, null);
            if (hasBlockingIssues)
            {
                return Finish(report, false);
            }

            UiPrefabSpec spec = InvokeMethod<UiPrefabSpec>(_interpreter, "Interpret", request.DesignPacket);
            report.Spec = spec ?? new UiPrefabSpec();
            if (spec == null || string.IsNullOrWhiteSpace(report.Spec.PrefabName))
            {
                AddError(report, "spec", "UiPrefabSpec 解释失败。");
                AddStage(report, "spec", false, new[] { "UiPrefabSpec 解释失败。" }, null);
                return Finish(report, false);
            }

            AddStage(report, "spec", true, null, null);

            UiPrefabGenerationResult previewGeneration = _previewGenerator.GenerateDraft(new UiPrefabGenerationRequest
            {
                Spec = spec,
                Profile = request.Profile,
            });
            report.PreviewGeneration = previewGeneration ?? new UiPrefabGenerationResult();
            AppendStrings(report, previewGeneration != null ? previewGeneration.Errors : null, true);
            AppendStrings(report, previewGeneration != null ? previewGeneration.Warnings : null, false);
            bool previewOk = previewGeneration != null && previewGeneration.Success;
            AddStage(report, "preview_generation", previewOk, previewGeneration != null ? previewGeneration.Errors : new[] { "Preview generation failed." }, previewGeneration != null ? previewGeneration.Warnings : null);
            if (!previewOk)
            {
                return Finish(report, false);
            }

            UiPrefabDraftWriteResult draftWrite = _draftWriter.WriteDraft(new UiPrefabDraftWriteRequest
            {
                Spec = spec,
                Profile = request.Profile,
                PrefabDraftPath = previewGeneration.PrefabDraftPath,
            });
            report.DraftWrite = draftWrite ?? new UiPrefabDraftWriteResult();
            report.PrefabDraftPath = !string.IsNullOrWhiteSpace(report.DraftWrite.PrefabDraftPath)
                ? report.DraftWrite.PrefabDraftPath
                : previewGeneration.PrefabDraftPath;
            AppendStrings(report, draftWrite != null ? draftWrite.Errors : null, true);
            AppendStrings(report, draftWrite != null ? draftWrite.Warnings : null, false);
            AddStage(report, "draft_write", draftWrite != null && draftWrite.Success, draftWrite != null ? draftWrite.Errors : new[] { "Draft writer failed." }, draftWrite != null ? draftWrite.Warnings : null);

            PrefabBindingManifest manifest = previewGeneration.Manifest ?? new PrefabBindingManifest();
            UiPrefabValidationResult manifestValidation = _manifestValidator.Validate(manifest, request.Profile);
            report.ManifestValidation = manifestValidation ?? new UiPrefabValidationResult();
            AppendValidation(report, report.ManifestValidation);
            AddStage(report, "manifest_validation", report.ManifestValidation.IsValid, null, null);

            if (draftWrite != null && draftWrite.Success)
            {
                UiPrefabValidationResult structureValidation = _draftStructureValidator.Validate(report.PrefabDraftPath, spec);
                report.DraftStructureValidation = structureValidation ?? new UiPrefabValidationResult();
                AppendValidation(report, report.DraftStructureValidation);
                AddStage(report, "prefab_structure_validation", report.DraftStructureValidation.IsValid, null, null);
            }
            else
            {
                report.DraftStructureValidation = new UiPrefabValidationResult();
                AddStage(report, "prefab_structure_validation", false, new[] { "Draft write failed; skipped prefab structure validation." }, null);
            }

            object adapterResult = InvokeMethod(_generatedResultConsumer, "Consume", manifest);
            report.AdapterResult = adapterResult;
            AppendObjectList(report, adapterResult, "Errors", true);
            AppendObjectList(report, adapterResult, "Warnings", false);
            bool adapterSuccess = GetBoolProperty(adapterResult, "Success");
            AddStage(report, "adapter_consumption", adapterSuccess, GetObjectList(adapterResult, "Errors"), GetObjectList(adapterResult, "Warnings"));

            object adapterPlan = GetObjectProperty(adapterResult, "Plan");
            report.ManifestText = PrefabBindingManifestFixtureSerializer.Serialize(manifest);
            report.AdapterPlanText = adapterPlan != null ? JsonUtility.ToJson(adapterPlan, true) : string.Empty;

            report.Success = !hasBlockingIssues &&
                             report.PreviewGeneration != null &&
                             report.PreviewGeneration.Success &&
                             report.DraftWrite != null &&
                             report.DraftWrite.Success &&
                             report.ManifestValidation != null &&
                             report.ManifestValidation.IsValid &&
                             report.DraftStructureValidation != null &&
                             report.DraftStructureValidation.IsValid &&
                             adapterSuccess &&
                             report.Errors.Count == 0;

            return Finish(report, report.Success);
        }

        private static SampleUiPipelineReport Finish(SampleUiPipelineReport report, bool success)
        {
            if (report != null)
            {
                report.Success = success;
            }

            return report;
        }

        private static void AddError(SampleUiPipelineReport report, string fieldPath, string message)
        {
            if (report == null)
            {
                return;
            }

            report.Errors.Add(message ?? string.Empty);
            report.Stages.Add(new SampleUiPipelineStageReport
            {
                StageId = fieldPath ?? string.Empty,
                Success = false,
                Errors = new List<string> { message ?? string.Empty },
            });
        }

        private static void AppendIntake(SampleUiPipelineReport report, object intakeAssessment)
        {
            if (report == null || intakeAssessment == null)
            {
                return;
            }

            IEnumerable<object> unresolvedItems = GetObjectEnumerable(intakeAssessment, "UnresolvedItems");
            if (unresolvedItems == null)
            {
                return;
            }

            foreach (object issue in unresolvedItems)
            {
                if (issue == null)
                {
                    continue;
                }

                string severity = GetStringProperty(issue, "Severity");
                string summary = GetStringProperty(issue, "Summary");
                if (string.Equals(severity, "Blocking", StringComparison.Ordinal))
                {
                    report.Errors.Add(summary ?? string.Empty);
                }
                else
                {
                    report.Warnings.Add(summary ?? string.Empty);
                }
            }
        }

        private static void AppendValidation(SampleUiPipelineReport report, UiPrefabValidationResult validation)
        {
            if (report == null || validation == null || validation.Issues == null)
            {
                return;
            }

            for (int i = 0; i < validation.Issues.Count; i++)
            {
                UiPrefabValidationIssue issue = validation.Issues[i];
                if (issue == null)
                {
                    continue;
                }

                if (issue.Severity == UiPrefabValidationIssueSeverity.Error)
                {
                    report.Errors.Add(issue.Message ?? string.Empty);
                }
                else
                {
                    report.Warnings.Add(issue.Message ?? string.Empty);
                }
            }
        }

        private static void AppendStrings(SampleUiPipelineReport report, List<string> messages, bool errors)
        {
            if (report == null || messages == null)
            {
                return;
            }

            for (int i = 0; i < messages.Count; i++)
            {
                string message = messages[i];
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                if (errors)
                {
                    report.Errors.Add(message);
                }
                else
                {
                    report.Warnings.Add(message);
                }
            }
        }

        private static void AddStage(
            SampleUiPipelineReport report,
            string stageId,
            bool success,
            IEnumerable<string> errors,
            IEnumerable<string> warnings)
        {
            if (report == null)
            {
                return;
            }

            var stage = new SampleUiPipelineStageReport
            {
                StageId = stageId ?? string.Empty,
                Success = success,
            };

            if (errors != null)
            {
                foreach (string error in errors)
                {
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        stage.Errors.Add(error);
                    }
                }
            }

            if (warnings != null)
            {
                foreach (string warning in warnings)
                {
                    if (!string.IsNullOrWhiteSpace(warning))
                    {
                        stage.Warnings.Add(warning);
                    }
                }
            }

            report.Stages.Add(stage);
        }

        private static object InvokeMethod(object target, string methodName, params object[] args)
        {
            if (target == null)
            {
                return null;
            }

            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                return null;
            }

            return method.Invoke(target, args);
        }

        private static T InvokeMethod<T>(object target, string methodName, params object[] args)
        {
            object value = InvokeMethod(target, methodName, args);
            if (value is T)
            {
                return (T)value;
            }

            return default(T);
        }

        private static object CreateDefaultCollaborator(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            Type type = Type.GetType(typeName, false);
            if (type != null)
            {
                return CreateInstanceWithFallback(type);
            }

            string fullName = typeName.Split(',')[0].Trim();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                {
                    continue;
                }

                type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return CreateInstanceWithFallback(type);
                }
            }

            return null;
        }

        private static object CreateInstanceWithFallback(Type type)
        {
            if (type == null)
            {
                return null;
            }

            try
            {
                return Activator.CreateInstance(type);
            }
            catch (MissingMethodException)
            {
                if (string.Equals(type.Name, "DefaultDesignPacketToUiPrefabSpecInterpreter", StringComparison.Ordinal))
                {
                    return Activator.CreateInstance(type, new object[] { null, "holmas_ugui_portrait" });
                }

                throw;
            }
        }

        private static string GetStringProperty(object target, string propertyName)
        {
            object value = GetObjectProperty(target, propertyName);
            return value == null ? string.Empty : value.ToString();
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            object value = GetObjectProperty(target, propertyName);
            if (value is bool)
            {
                return (bool)value;
            }

            bool parsed;
            return value != null && bool.TryParse(value.ToString(), out parsed) && parsed;
        }

        private static object GetObjectProperty(object target, string propertyName)
        {
            if (target == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return property.GetValue(target, null);
            }

            FieldInfo field = target.GetType().GetField(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return field.GetValue(target);
            }

            return null;
        }

        private static IEnumerable<object> GetObjectEnumerable(object target, string propertyName)
        {
            object value = GetObjectProperty(target, propertyName);
            if (value is IEnumerable enumerable)
            {
                var items = new List<object>();
                foreach (object item in enumerable)
                {
                    items.Add(item);
                }

                return items;
            }

            return null;
        }

        private static List<string> GetObjectList(object target, string propertyName)
        {
            IEnumerable<object> values = GetObjectEnumerable(target, propertyName);
            if (values == null)
            {
                return null;
            }

            var strings = new List<string>();
            foreach (object value in values)
            {
                if (value != null)
                {
                    strings.Add(value.ToString());
                }
            }

            return strings;
        }

        private static void AppendObjectList(SampleUiPipelineReport report, object target, string propertyName, bool errors)
        {
            List<string> values = GetObjectList(target, propertyName);
            if (values == null)
            {
                return;
            }

            AppendStrings(report, values, errors);
        }
    }

    public static class SampleUiPipelineBatch
    {
        private const string ReportPathEnvName = "UI_PREFAB_SAMPLE_PIPELINE_REPORT_PATH";
        private const string DefaultReportFileName = "ui_prefab_sample_pipeline_report.json";
        private const string SampleDesignPacketPath = "Assets/Tools/UiPrefabGenerator/Samples~/Holmas/sample_design_packet.json";
        private const string HolmasProfileTypeName = "UiPrefabGenerator.HolmasAdapter.HolmasUiProjectProfile, UiPrefabGenerator.HolmasAdapter";

        [MenuItem("UiPrefabGenerator/Validation/Run Holmas Sample Pipeline")]
        public static void RunHolmasSamplePipelineFromMenu()
        {
            string reportPath = Path.Combine(Path.GetTempPath(), DefaultReportFileName);
            RunHolmasSamplePipeline(reportPath);
        }

        public static void RunHolmasSamplePipelineBatch()
        {
            string reportPath = Environment.GetEnvironmentVariable(ReportPathEnvName);
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                reportPath = Path.Combine(Path.GetTempPath(), DefaultReportFileName);
            }

            RunHolmasSamplePipeline(reportPath);
        }

        private static void RunHolmasSamplePipeline(string reportPath)
        {
            var runner = new DefaultSampleUiPipelineRunner();
            IProjectUiProfile profile = CreateProfile();
            if (profile == null)
            {
                throw new InvalidOperationException("无法创建 Holmas sample pipeline 使用的 ProjectUiProfile。");
            }

            DesignPacket designPacket = SampleUiPipelineFixtureLoader.LoadDesignPacket(SampleDesignPacketPath);
            SampleUiPipelineReport report = runner.Run(new SampleUiPipelineRequest
            {
                DesignPacket = designPacket,
                Profile = profile,
            });

            string normalizedReportPath = Path.GetFullPath(reportPath);
            string reportDirectory = Path.GetDirectoryName(normalizedReportPath);
            if (!string.IsNullOrWhiteSpace(reportDirectory))
            {
                Directory.CreateDirectory(reportDirectory);
            }

            File.WriteAllText(normalizedReportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            Debug.Log(string.Format(
                "UiPrefabGenerator sample pipeline finished. success={0}, report={1}",
                report.Success,
                normalizedReportPath));

            if (!report.Success)
            {
                throw new InvalidOperationException("UiPrefabGenerator sample pipeline failed.");
            }
        }

        private static IProjectUiProfile CreateProfile()
        {
            Type type = Type.GetType(HolmasProfileTypeName, false);
            if (type == null)
            {
                return null;
            }

            return Activator.CreateInstance(type) as IProjectUiProfile;
        }
    }

    internal static class SampleUiPipelineFixtureLoader
    {
        public static DesignPacket LoadDesignPacket(string assetPath)
        {
            string absolutePath = ToAbsolutePath(assetPath);
            string text = File.ReadAllText(absolutePath);
            DesignPacketJson json = JsonUtility.FromJson<DesignPacketJson>(text);
            if (json == null)
            {
                throw new InvalidOperationException("无法读取 sample DesignPacket: " + assetPath);
            }

            var packet = new DesignPacket
            {
                PageId = json.page_id ?? string.Empty,
                PageTitle = json.page_title ?? string.Empty,
                PrefabName = json.prefab_name ?? string.Empty,
                Notes = json.notes ?? string.Empty,
            };

            if (json.design_images != null)
            {
                for (int i = 0; i < json.design_images.Count; i++)
                {
                    DesignImageReferenceJson image = json.design_images[i] ?? new DesignImageReferenceJson();
                    packet.DesignImages.Add(new DesignImageReference
                    {
                        ImageId = image.image_id ?? string.Empty,
                        ImagePath = image.image_path ?? string.Empty,
                        StateId = image.state_id ?? string.Empty,
                    });
                }
            }

            if (json.states != null)
            {
                for (int i = 0; i < json.states.Count; i++)
                {
                    DesignStateDefinitionJson state = json.states[i] ?? new DesignStateDefinitionJson();
                    packet.States.Add(new DesignStateDefinition
                    {
                        StateId = state.state_id ?? string.Empty,
                        Description = state.description ?? string.Empty,
                    });
                }
            }

            if (json.rules != null)
            {
                for (int i = 0; i < json.rules.Count; i++)
                {
                    DesignRuleDefinitionJson rule = json.rules[i] ?? new DesignRuleDefinitionJson();
                    packet.Rules.Add(new DesignRuleDefinition
                    {
                        RuleId = rule.rule_id ?? string.Empty,
                        Description = rule.description ?? string.Empty,
                    });
                }
            }

            if (json.asset_slot_hints != null)
            {
                for (int i = 0; i < json.asset_slot_hints.Count; i++)
                {
                    DesignAssetSlotHintJson hint = json.asset_slot_hints[i] ?? new DesignAssetSlotHintJson();
                    packet.AssetSlotHints.Add(new DesignAssetSlotHint
                    {
                        SlotId = hint.slot_id ?? string.Empty,
                        Usage = hint.usage ?? string.Empty,
                    });
                }
            }

            return packet;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        [Serializable]
        private sealed class DesignPacketJson
        {
            public string page_id = string.Empty;
            public string page_title = string.Empty;
            public string prefab_name = string.Empty;
            public List<DesignImageReferenceJson> design_images = new List<DesignImageReferenceJson>();
            public List<DesignStateDefinitionJson> states = new List<DesignStateDefinitionJson>();
            public List<DesignRuleDefinitionJson> rules = new List<DesignRuleDefinitionJson>();
            public List<DesignAssetSlotHintJson> asset_slot_hints = new List<DesignAssetSlotHintJson>();
            public string notes = string.Empty;
        }

        [Serializable]
        private sealed class DesignImageReferenceJson
        {
            public string image_id = string.Empty;
            public string image_path = string.Empty;
            public string state_id = string.Empty;
        }

        [Serializable]
        private sealed class DesignStateDefinitionJson
        {
            public string state_id = string.Empty;
            public string description = string.Empty;
        }

        [Serializable]
        private sealed class DesignRuleDefinitionJson
        {
            public string rule_id = string.Empty;
            public string description = string.Empty;
        }

        [Serializable]
        private sealed class DesignAssetSlotHintJson
        {
            public string slot_id = string.Empty;
            public string usage = string.Empty;
        }
    }
}

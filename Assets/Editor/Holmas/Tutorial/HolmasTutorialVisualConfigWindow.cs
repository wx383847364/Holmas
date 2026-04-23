using System.Collections.Generic;
using App.HotUpdate.Holmas.Tutorial;
using UnityEditor;
using UnityEngine;

namespace Holmas.Editor.Tutorial
{
    public sealed class HolmasTutorialVisualConfigWindow : EditorWindow
    {
        private const string ResourceRoot = "Assets/HotUpdateContent/Res/";

        private TutorialVisualConfig _config;
        private Vector2 _scroll;

        [MenuItem("Holmas/Tutorial/Visual Config")]
        public static void Open()
        {
            GetWindow<HolmasTutorialVisualConfigWindow>("Tutorial Visuals");
        }

        private void OnEnable()
        {
            LoadConfig();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Core Find Cat Tutorial Visuals", EditorStyles.boldLabel);
            _config = (TutorialVisualConfig)EditorGUILayout.ObjectField("Config", _config, typeof(TutorialVisualConfig), false);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create / Repair"))
                {
                    CreateOrRepairConfig();
                }

                if (GUILayout.Button("Generate Steps"))
                {
                    GenerateStepEntries();
                }

                if (GUILayout.Button("Fill Placeholder"))
                {
                    FillPlaceholderPaths();
                }

                if (GUILayout.Button("Validate"))
                {
                    ValidateConfig(logSuccess: true);
                }
            }

            if (_config == null)
            {
                EditorGUILayout.HelpBox("Create the config asset before editing tutorial visuals.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            SerializedObject serializedObject = new SerializedObject(_config);
            SerializedProperty steps = serializedObject.FindProperty("steps");
            EditorGUILayout.PropertyField(steps, includeChildren: true);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();
        }

        private void LoadConfig()
        {
            _config = AssetDatabase.LoadAssetAtPath<TutorialVisualConfig>(TutorialVisualConfig.DefaultAssetPath);
        }

        private void CreateOrRepairConfig()
        {
            EnsureDirectories();
            _config = AssetDatabase.LoadAssetAtPath<TutorialVisualConfig>(TutorialVisualConfig.DefaultAssetPath);
            if (_config == null)
            {
                _config = CreateInstance<TutorialVisualConfig>();
                AssetDatabase.CreateAsset(_config, TutorialVisualConfig.DefaultAssetPath);
            }

            GenerateStepEntries();
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void GenerateStepEntries()
        {
            if (_config == null)
            {
                CreateOrRepairConfig();
                return;
            }

            var existing = new Dictionary<string, TutorialStepVisualDefinition>();
            foreach (TutorialStepVisualDefinition visual in _config.Steps)
            {
                if (visual != null && !string.IsNullOrWhiteSpace(visual.StepId))
                {
                    existing[visual.StepId] = visual;
                }
            }

            var next = new List<TutorialStepVisualDefinition>();
            foreach (var step in CoreFindCatTutorialSteps.All)
            {
                if (!existing.TryGetValue(step.StepId, out TutorialStepVisualDefinition visual))
                {
                    visual = NewPlaceholderVisual(step.StepId);
                }

                visual.StepId = step.StepId;
                next.Add(visual);
            }

            _config.ReplaceSteps(next);
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
        }

        private void FillPlaceholderPaths()
        {
            if (_config == null)
            {
                CreateOrRepairConfig();
            }

            var next = new List<TutorialStepVisualDefinition>();
            foreach (var step in CoreFindCatTutorialSteps.All)
            {
                next.Add(NewPlaceholderVisual(step.StepId));
            }

            _config.ReplaceSteps(next);
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
        }

        private bool ValidateConfig(bool logSuccess)
        {
            if (_config == null)
            {
                Debug.LogError("HolmasTutorialVisualConfigWindow: Tutorial visual config is missing.");
                return false;
            }

            bool ok = true;
            foreach (var step in CoreFindCatTutorialSteps.All)
            {
                TutorialStepVisualDefinition visual = _config.Find(step.StepId);
                if (visual == null)
                {
                    Debug.LogError($"HolmasTutorialVisualConfigWindow: Missing visual entry for step {step.StepId}.");
                    ok = false;
                    continue;
                }

                ok &= ValidatePath(step.StepId, nameof(visual.MainImagePath), visual.MainImagePath, required: false);
                ok &= ValidatePath(step.StepId, nameof(visual.DialogBackgroundPath), visual.DialogBackgroundPath, required: false);
                ok &= ValidatePath(step.StepId, nameof(visual.TipsIconPath), visual.TipsIconPath, required: false);
                ok &= ValidatePath(step.StepId, nameof(visual.FingerIconPath), visual.FingerIconPath, required: false);
                ok &= ValidatePath(step.StepId, nameof(visual.HighlightSpritePath), visual.HighlightSpritePath, required: false);
                ok &= ValidatePath(step.StepId, nameof(visual.ArrowSpritePath), visual.ArrowSpritePath, required: false);
            }

            if (ok && logSuccess)
            {
                Debug.Log("HolmasTutorialVisualConfigWindow: Tutorial visual config validation passed.");
            }

            return ok;
        }

        private static bool ValidatePath(string stepId, string fieldName, string path, bool required)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                if (required)
                {
                    Debug.LogError($"HolmasTutorialVisualConfigWindow: {stepId}.{fieldName} is empty.");
                    return false;
                }

                return true;
            }

            if (!path.StartsWith(ResourceRoot, System.StringComparison.Ordinal))
            {
                Debug.LogError($"HolmasTutorialVisualConfigWindow: {stepId}.{fieldName} must be under {ResourceRoot}: {path}");
                return false;
            }

            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset == null)
            {
                Debug.LogError($"HolmasTutorialVisualConfigWindow: {stepId}.{fieldName} asset not found: {path}");
                return false;
            }

            return true;
        }

        private static TutorialStepVisualDefinition NewPlaceholderVisual(string stepId)
        {
            return new TutorialStepVisualDefinition
            {
                StepId = stepId,
                MainImagePath = TutorialVisualConfig.PlaceholderSpritePath,
                DialogBackgroundPath = TutorialVisualConfig.PlaceholderSpritePath,
                TipsIconPath = TutorialVisualConfig.PlaceholderSpritePath,
                FingerIconPath = TutorialVisualConfig.PlaceholderSpritePath,
                HighlightSpritePath = TutorialVisualConfig.PlaceholderSpritePath,
                ArrowSpritePath = TutorialVisualConfig.PlaceholderSpritePath,
            };
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/HotUpdateContent/Res/Tutorial"))
            {
                AssetDatabase.CreateFolder("Assets/HotUpdateContent/Res", "Tutorial");
            }

            if (!AssetDatabase.IsValidFolder("Assets/HotUpdateContent/Res/Tutorial/Placeholder"))
            {
                AssetDatabase.CreateFolder("Assets/HotUpdateContent/Res/Tutorial", "Placeholder");
            }
        }
    }
}

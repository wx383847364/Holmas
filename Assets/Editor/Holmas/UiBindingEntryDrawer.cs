using System.Collections.Generic;
using System.Text;
using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Generated;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Holmas.Editor
{
    [CustomPropertyDrawer(typeof(UiBindingEntry))]
    public sealed class UiBindingEntryDrawer : PropertyDrawer
    {
        private const float LineGap = 2f;

        private static readonly UiRuntimeScreenDescriptor[] Descriptors =
        {
            MainGeneratedBindings.Descriptor,
            BattleGeneratedBindings.Descriptor,
            LoadingGeneratedBindings.Descriptor,
            LeaderboardGeneratedBindings.Descriptor,
            AgencyMainGeneratedBindings.Descriptor,
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty bindingKey = property.FindPropertyRelative("_bindingKey");
            SerializedProperty componentType = property.FindPropertyRelative("_componentType");
            SerializedProperty nodePath = property.FindPropertyRelative("_nodePath");
            SerializedProperty eventName = property.FindPropertyRelative("_eventName");
            SerializedProperty target = property.FindPropertyRelative("_target");

            Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            string title = string.IsNullOrWhiteSpace(bindingKey.stringValue) ? "Binding Entry" : bindingKey.stringValue;
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, title, true);
            if (!property.isExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                Rect line = NextLine(position, 1);
                EditorGUI.PropertyField(line, bindingKey, new GUIContent("Binding Key"));

                line = NextLine(position, 2);
                EditorGUI.PropertyField(line, componentType, new GUIContent("Component Type"));

                line = NextLine(position, 3);
                EditorGUI.PropertyField(line, nodePath, new GUIContent("Node Path"));

                line = NextLine(position, 4);
                EditorGUI.PropertyField(line, eventName, new GUIContent("Event Name"));

                line = NextLine(position, 5);
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(line, target, new GUIContent("Target"));
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyTargetMetadata(property, bindingKey, componentType, nodePath, eventName, target);
                }
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int lineCount = property.isExpanded ? 6 : 1;
            return lineCount * EditorGUIUtility.singleLineHeight + (lineCount - 1) * LineGap;
        }

        private static Rect NextLine(Rect position, int lineIndex)
        {
            float y = position.y + lineIndex * (EditorGUIUtility.singleLineHeight + LineGap);
            return new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        }

        private static void ApplyTargetMetadata(
            SerializedProperty entryProperty,
            SerializedProperty bindingKey,
            SerializedProperty componentType,
            SerializedProperty nodePath,
            SerializedProperty eventName,
            SerializedProperty targetProperty)
        {
            Component target = targetProperty.objectReferenceValue as Component;
            if (target == null)
            {
                bindingKey.stringValue = string.Empty;
                componentType.stringValue = string.Empty;
                nodePath.stringValue = string.Empty;
                eventName.stringValue = string.Empty;
                return;
            }

            UiReferenceCollector collector = entryProperty.serializedObject.targetObject as UiReferenceCollector;
            string resolvedComponentType = target.GetType().Name;
            string resolvedNodePath = BuildNodePath(collector, target.transform);

            if (IsTransformComponent(target) &&
                !TryFindManifestEntry(collector, resolvedNodePath, resolvedComponentType, out _) &&
                TryFindManifestEntry(collector, resolvedNodePath, out UiBindingManifestEntry componentMatch) &&
                TryGetComponent(target, componentMatch.ComponentType, out Component resolvedTarget))
            {
                target = resolvedTarget;
                targetProperty.objectReferenceValue = resolvedTarget;
                resolvedComponentType = resolvedTarget.GetType().Name;
            }

            componentType.stringValue = resolvedComponentType;
            nodePath.stringValue = resolvedNodePath;

            if (TryFindManifestEntry(collector, resolvedNodePath, resolvedComponentType, out UiBindingManifestEntry manifestEntry))
            {
                bindingKey.stringValue = manifestEntry.BindingKey;
                eventName.stringValue = manifestEntry.EventName;
                return;
            }

            bindingKey.stringValue = BuildFallbackBindingKey(collector, target.transform);
            eventName.stringValue = GetDefaultEventName(target);
        }

        private static string BuildNodePath(UiReferenceCollector collector, Transform target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            Transform root = collector != null ? collector.transform : target.root;
            if (root == null || !target.IsChildOf(root))
            {
                root = target.root;
            }

            var names = new Stack<string>();
            Transform current = target;
            while (current != null)
            {
                names.Push(current.name);
                if (current == root)
                {
                    break;
                }

                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private static bool TryFindManifestEntry(
            UiReferenceCollector collector,
            string nodePath,
            string componentType,
            out UiBindingManifestEntry match)
        {
            string rootName = NormalizeObjectName(collector != null ? collector.name : string.Empty);
            match = null;

            for (int i = 0; i < Descriptors.Length; i++)
            {
                UiRuntimeScreenDescriptor descriptor = Descriptors[i];
                if (!string.IsNullOrWhiteSpace(rootName) && !string.Equals(descriptor.PrefabName, rootName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryFindManifestEntry(descriptor, nodePath, componentType, out match))
                {
                    return true;
                }
            }

            for (int i = 0; i < Descriptors.Length; i++)
            {
                if (TryFindManifestEntry(Descriptors[i], nodePath, componentType, out match))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindManifestEntry(
            UiReferenceCollector collector,
            string nodePath,
            out UiBindingManifestEntry match)
        {
            string rootName = NormalizeObjectName(collector != null ? collector.name : string.Empty);
            match = null;

            for (int i = 0; i < Descriptors.Length; i++)
            {
                UiRuntimeScreenDescriptor descriptor = Descriptors[i];
                if (!string.IsNullOrWhiteSpace(rootName) && !string.Equals(descriptor.PrefabName, rootName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryFindManifestEntry(descriptor, nodePath, out match))
                {
                    return true;
                }
            }

            for (int i = 0; i < Descriptors.Length; i++)
            {
                if (TryFindManifestEntry(Descriptors[i], nodePath, out match))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindManifestEntry(
            UiRuntimeScreenDescriptor descriptor,
            string nodePath,
            string componentType,
            out UiBindingManifestEntry match)
        {
            IReadOnlyList<UiBindingManifestEntry> entries = descriptor.BindingManifest.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                UiBindingManifestEntry entry = entries[i];
                if (entry != null &&
                    string.Equals(entry.NodePath, nodePath, System.StringComparison.Ordinal) &&
                    string.Equals(entry.ComponentType, componentType, System.StringComparison.Ordinal))
                {
                    match = entry;
                    return true;
                }
            }

            match = null;
            return false;
        }

        private static bool TryFindManifestEntry(
            UiRuntimeScreenDescriptor descriptor,
            string nodePath,
            out UiBindingManifestEntry match)
        {
            IReadOnlyList<UiBindingManifestEntry> entries = descriptor.BindingManifest.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                UiBindingManifestEntry entry = entries[i];
                if (entry != null &&
                    string.Equals(entry.NodePath, nodePath, System.StringComparison.Ordinal))
                {
                    match = entry;
                    return true;
                }
            }

            match = null;
            return false;
        }

        private static bool TryGetComponent(Component source, string componentType, out Component component)
        {
            Component[] components = source.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component current = components[i];
                if (current != null && string.Equals(current.GetType().Name, componentType, System.StringComparison.Ordinal))
                {
                    component = current;
                    return true;
                }
            }

            component = null;
            return false;
        }

        private static bool IsTransformComponent(Component component)
        {
            return component is Transform;
        }

        private static string BuildFallbackBindingKey(UiReferenceCollector collector, Transform target)
        {
            string prefix = BuildScreenPrefix(collector);
            string suffix = BuildBindingSuffix(collector, target);
            return string.IsNullOrWhiteSpace(prefix) ? suffix : prefix + "/" + suffix;
        }

        private static string BuildScreenPrefix(UiReferenceCollector collector)
        {
            string rootName = NormalizeObjectName(collector != null ? collector.name : string.Empty);
            if (rootName.EndsWith("Panel", System.StringComparison.Ordinal))
            {
                rootName = rootName.Substring(0, rootName.Length - "Panel".Length);
            }

            return ToSnakeCase(rootName);
        }

        private static string BuildBindingSuffix(UiReferenceCollector collector, Transform target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            Transform root = collector != null ? collector.transform : target.root;
            string targetName = ToSnakeCase(target.name);
            string parentName = target.parent != null ? ToSnakeCase(target.parent.name) : string.Empty;

            if (target == root)
            {
                return string.IsNullOrWhiteSpace(targetName) ? "root" : targetName;
            }

            if (!string.IsNullOrWhiteSpace(parentName) && ContainsDigit(parentName) && parentName != targetName)
            {
                return parentName + "_" + targetName;
            }

            return targetName;
        }

        private static string GetDefaultEventName(Component target)
        {
            if (target is Button)
            {
                return "on_click";
            }

            if (target is Toggle)
            {
                return "on_value_changed";
            }

            return string.Empty;
        }

        private static string NormalizeObjectName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Replace("(Clone)", string.Empty).Trim();
        }

        private static string ToSnakeCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length + 8);
            bool previousWasSeparator = true;
            bool previousWasLowerOrDigit = false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLetterOrDigit(c))
                {
                    AppendSeparator(builder, ref previousWasSeparator);
                    previousWasLowerOrDigit = false;
                    continue;
                }

                if (char.IsUpper(c) && previousWasLowerOrDigit && !previousWasSeparator)
                {
                    AppendSeparator(builder, ref previousWasSeparator);
                }

                builder.Append(char.ToLowerInvariant(c));
                previousWasSeparator = false;
                previousWasLowerOrDigit = char.IsLower(c) || char.IsDigit(c);
            }

            return builder.ToString().Trim('_');
        }

        private static void AppendSeparator(StringBuilder builder, ref bool previousWasSeparator)
        {
            if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append('_');
                previousWasSeparator = true;
            }
        }

        private static bool ContainsDigit(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsDigit(value[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

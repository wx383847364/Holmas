using System;
using System.Collections.Generic;

namespace UiPrefabGenerator.Editor.Validation
{
    public enum UiPrefabValidationIssueSeverity
    {
        Warning = 0,
        Error = 1,
    }

    public enum UiPrefabValidationIssueCategory
    {
        Schema = 0,
        Generator = 1,
        Adapter = 2,
        Fixture = 3,
    }

    [Serializable]
    public sealed class UiPrefabValidationIssue
    {
        public UiPrefabValidationIssueSeverity Severity;
        public UiPrefabValidationIssueCategory Category;
        public string FieldPath = string.Empty;
        public string Message = string.Empty;
    }

    [Serializable]
    public sealed class UiPrefabValidationResult
    {
        public List<UiPrefabValidationIssue> Issues = new List<UiPrefabValidationIssue>();

        public bool IsValid
        {
            get
            {
                for (int i = 0; i < Issues.Count; i++)
                {
                    UiPrefabValidationIssue issue = Issues[i];
                    if (issue != null && issue.Severity == UiPrefabValidationIssueSeverity.Error)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}

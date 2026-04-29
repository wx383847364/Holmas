using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools.TestRunner.GUI;

public static class HolmasEditModeTestRunner
{
    private static TestRunResult s_LastResult;
    private static string s_CurrentTestName = string.Empty;

    [MenuItem("Holmas/Validation/Run Holmas EditMode Tests")]
    public static void RunHolmasEditModeTests()
    {
        s_LastResult = null;
        s_CurrentTestName = string.Empty;

        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new ResultCallbacks());

        var filter = new Filter
        {
            testMode = TestMode.EditMode,
            assemblyNames = new[] { "Holmas.Tests", "UiPrefabGenerator.Tests", "Assembly-CSharp-Editor" }
        };

        var settings = new ExecutionSettings(filter)
        {
            runSynchronously = true
        };

        api.Execute(settings);

        if (s_LastResult == null)
        {
            throw new InvalidOperationException("Holmas EditMode tests did not report a result.");
        }

        Debug.Log(
            $"Holmas EditMode tests finished. passed={s_LastResult.PassCount}, failed={s_LastResult.FailCount}, skipped={s_LastResult.SkipCount}, inconclusive={s_LastResult.InconclusiveCount}, total={s_LastResult.TestCaseCount}");

        if (s_LastResult.FailCount > 0)
        {
            if (!string.IsNullOrWhiteSpace(s_LastResult.FailureSummary))
            {
                Debug.LogError($"Holmas EditMode failed cases:\n{s_LastResult.FailureSummary}");
            }

            throw new InvalidOperationException($"Holmas EditMode tests failed: {s_LastResult.FailCount} failing cases.");
        }
    }

    private sealed class ResultCallbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
            Debug.Log($"Holmas EditMode tests started. total={testsToRun?.TestCaseCount ?? 0}");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            if (result == null)
            {
                return;
            }

            s_LastResult = new TestRunResult
            {
                PassCount = result.PassCount,
                FailCount = result.FailCount,
                SkipCount = result.SkipCount,
                InconclusiveCount = result.InconclusiveCount,
                TestCaseCount = result.Test.TestCaseCount,
                FailureSummary = BuildFailureSummary(result),
            };
        }

        public void TestStarted(ITestAdaptor test)
        {
            s_CurrentTestName = test?.FullName ?? test?.Name ?? string.Empty;
            Debug.Log($"Holmas EditMode test started: {s_CurrentTestName}");
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            string testName = result?.Test?.FullName ?? result?.Name ?? string.Empty;
            Debug.Log($"Holmas EditMode test finished: {testName} status={result?.TestStatus}");
            if (string.Equals(testName, s_CurrentTestName, StringComparison.Ordinal))
            {
                s_CurrentTestName = string.Empty;
            }
        }
    }

    private sealed class TestRunResult
    {
        public int PassCount;
        public int FailCount;
        public int SkipCount;
        public int InconclusiveCount;
        public int TestCaseCount;
        public string FailureSummary = string.Empty;
    }

    private static string BuildFailureSummary(ITestResultAdaptor result)
    {
        if (result == null)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        CollectFailures(result, lines);
        return string.Join(Environment.NewLine, lines);
    }

    private static void CollectFailures(ITestResultAdaptor result, List<string> lines)
    {
        if (result == null)
        {
            return;
        }

        if (result.Test != null && !result.HasChildren && result.TestStatus == TestStatus.Failed)
        {
            var builder = new StringBuilder();
            builder.Append(result.Name ?? "(unnamed test)");
            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                builder.Append(" -> ").Append(result.Message.Trim());
            }

            lines.Add(builder.ToString());
        }

        if (!result.HasChildren || result.Children == null)
        {
            return;
        }

        foreach (ITestResultAdaptor child in result.Children)
        {
            CollectFailures(child, lines);
        }
    }
}

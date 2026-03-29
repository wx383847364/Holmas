using System;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools.TestRunner.GUI;

public static class HolmasEditModeTestRunner
{
    private static TestRunResult s_LastResult;

    [MenuItem("Holmas/Validation/Run Holmas EditMode Tests")]
    public static void RunHolmasEditModeTests()
    {
        s_LastResult = null;

        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new ResultCallbacks());

        var filter = new Filter
        {
            testMode = TestMode.EditMode,
            assemblyNames = new[] { "Holmas.Tests" }
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
            throw new InvalidOperationException($"Holmas EditMode tests failed: {s_LastResult.FailCount} failing cases.");
        }
    }

    private sealed class ResultCallbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
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
            };
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
        }
    }

    private sealed class TestRunResult
    {
        public int PassCount;
        public int FailCount;
        public int SkipCount;
        public int InconclusiveCount;
        public int TestCaseCount;
    }
}

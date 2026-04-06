using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace UiPrefabGenerator.Editor.Bridge
{
    public static class UiPrefabGeneratorAutoAnalysisBridge
    {
        private const string ScriptOverrideEnvironmentVariable = "UI_PREFAB_GENERATOR_AUTO_ANALYSIS_SCRIPT";

        public static UiPrefabGeneratorAutoAnalysisResult RunTaskAutoAnalysis(string taskDirectory)
        {
            var result = new UiPrefabGeneratorAutoAnalysisResult
            {
                TaskDirectory = taskDirectory ?? string.Empty,
                TaskId = Path.GetFileName(taskDirectory) ?? string.Empty,
                RequestPath = string.IsNullOrWhiteSpace(taskDirectory)
                    ? string.Empty
                    : UiGenerationTaskStorage.GetRequestPath(taskDirectory),
                ScriptPath = ResolveScriptPath(),
                LogPath = BuildLogPath(taskDirectory),
            };

            if (string.IsNullOrWhiteSpace(taskDirectory))
            {
                result.ErrorSummary = "任务目录为空。";
                WriteLog(result, "任务目录为空，无法执行自动分析。");
                return result;
            }

            if (!UiGenerationTaskStorage.TryLoadTaskRequest(taskDirectory, out _))
            {
                result.ErrorSummary = "未找到 request.json，请先生成请求。";
                WriteLog(result, "未找到 request.json，自动分析未启动。");
                return result;
            }

            if (string.IsNullOrWhiteSpace(result.ScriptPath) || !File.Exists(result.ScriptPath))
            {
                result.ErrorSummary = "未找到自动分析脚本: " + result.ScriptPath;
                WriteLog(result, result.ErrorSummary);
                return result;
            }

            try
            {
                string repoRoot = ResolveRepoRoot();
                Directory.CreateDirectory(Path.GetDirectoryName(result.LogPath) ?? Application.temporaryCachePath);

                ProcessStartInfo startInfo = BuildStartInfo(result.ScriptPath, taskDirectory, repoRoot, result.LogPath);
                result.CommandLine = startInfo.FileName + " " + startInfo.Arguments;
                WriteLog(result, "command: " + result.CommandLine);

                var stdout = new StringBuilder();
                var stderr = new StringBuilder();
                using (var process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.EnableRaisingEvents = false;
                    process.OutputDataReceived += (_, args) =>
                    {
                        if (args != null && args.Data != null)
                        {
                            stdout.AppendLine(args.Data);
                        }
                    };
                    process.ErrorDataReceived += (_, args) =>
                    {
                        if (args != null && args.Data != null)
                        {
                            stderr.AppendLine(args.Data);
                        }
                    };

                    if (!process.Start())
                    {
                        result.ErrorSummary = "无法启动自动分析脚本。";
                        WriteLog(result, result.ErrorSummary);
                        return result;
                    }

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    process.WaitForExit();

                    result.ExitCode = process.ExitCode;
                }

                result.StandardOutput = stdout.ToString();
                result.StandardError = stderr.ToString();
                result.Success = result.ExitCode == 0;
                if (result.Success)
                {
                    string validationError;
                    if (!ValidateGeneratedArtifacts(taskDirectory, out validationError))
                    {
                        result.Success = false;
                        result.ErrorSummary = validationError;
                    }
                }
                else
                {
                    result.ErrorSummary = BuildFailureSummary(result);
                }

                WriteLog(result, BuildProcessLog(result));
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorSummary = ex.Message;
                result.StandardError = ex.ToString();
                WriteLog(result, BuildExceptionLog(result, ex));
                return result;
            }
        }

        private static ProcessStartInfo BuildStartInfo(string scriptPath, string taskDirectory, string repoRoot, string logPath)
        {
            string escapedScriptPath = Quote(scriptPath);
            string escapedTaskDirectory = Quote(taskDirectory);

            bool useCmd = Application.platform == RuntimePlatform.WindowsEditor && scriptPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
            string fileName = useCmd ? "cmd.exe" : "/bin/bash";
            string arguments = useCmd
                ? "/c " + escapedScriptPath + " --task-dir " + escapedTaskDirectory
                : escapedScriptPath + " --task-dir " + escapedTaskDirectory;

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = string.IsNullOrWhiteSpace(repoRoot) ? Environment.CurrentDirectory : repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            startInfo.EnvironmentVariables["UI_PREFAB_GENERATOR_AUTO_ANALYSIS_TASK_DIR"] = taskDirectory ?? string.Empty;
            startInfo.EnvironmentVariables["UI_PREFAB_GENERATOR_AUTO_ANALYSIS_LOG_PATH"] = logPath ?? string.Empty;
            startInfo.EnvironmentVariables["UI_PREFAB_GENERATOR_AUTO_ANALYSIS_REPO_ROOT"] = repoRoot ?? string.Empty;
            return startInfo;
        }

        private static string ResolveRepoRoot()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return string.IsNullOrWhiteSpace(projectRoot) ? Environment.CurrentDirectory : projectRoot;
        }

        private static string ResolveScriptPath()
        {
            string overridePath = Environment.GetEnvironmentVariable(ScriptOverrideEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                string normalizedOverride = overridePath.Trim();
                if (File.Exists(normalizedOverride))
                {
                    return normalizedOverride;
                }
            }

            string repoRoot = ResolveRepoRoot();
            string shPath = Path.Combine(repoRoot, "scripts", "ui_prefab_generator", "run_task_auto_analysis.sh");
            if (File.Exists(shPath))
            {
                return shPath;
            }

            string batPath = Path.ChangeExtension(shPath, ".bat");
            if (File.Exists(batPath))
            {
                return batPath;
            }

            return shPath;
        }

        private static string BuildLogPath(string taskDirectory)
        {
            string taskId = Path.GetFileName(taskDirectory) ?? "unknown_task";
            string safeTaskId = SanitizeFileName(taskId);
            string logDirectory = Path.Combine(Application.temporaryCachePath, "UiPrefabGenerator", "AutoAnalysisLogs");
            return Path.Combine(logDirectory, safeTaskId + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff") + ".log");
        }

        private static string BuildProcessLog(UiPrefabGeneratorAutoAnalysisResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[ui-prefab-generator] auto analysis completed");
            builder.AppendLine("task_id=" + (result.TaskId ?? string.Empty));
            builder.AppendLine("task_dir=" + (result.TaskDirectory ?? string.Empty));
            builder.AppendLine("script=" + (result.ScriptPath ?? string.Empty));
            builder.AppendLine("command=" + (result.CommandLine ?? string.Empty));
            builder.AppendLine("exit_code=" + result.ExitCode);
            builder.AppendLine("success=" + result.Success);
            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                builder.AppendLine("stdout:");
                builder.AppendLine(result.StandardOutput.TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                builder.AppendLine("stderr:");
                builder.AppendLine(result.StandardError.TrimEnd());
            }

            return builder.ToString();
        }

        private static string BuildExceptionLog(UiPrefabGeneratorAutoAnalysisResult result, Exception exception)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[ui-prefab-generator] auto analysis failed with exception");
            builder.AppendLine("task_id=" + (result.TaskId ?? string.Empty));
            builder.AppendLine("task_dir=" + (result.TaskDirectory ?? string.Empty));
            builder.AppendLine("script=" + (result.ScriptPath ?? string.Empty));
            builder.AppendLine("command=" + (result.CommandLine ?? string.Empty));
            builder.AppendLine(exception.ToString());
            return builder.ToString();
        }

        private static string BuildFailureSummary(UiPrefabGeneratorAutoAnalysisResult result)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(result.ErrorSummary))
            {
                builder.AppendLine(result.ErrorSummary.Trim());
            }

            builder.AppendLine("退出码: " + result.ExitCode);
            if (!string.IsNullOrWhiteSpace(result.CommandLine))
            {
                builder.AppendLine("命令: " + result.CommandLine);
            }

            string snippet = TrimForDialog(!string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardError : result.StandardOutput);
            if (!string.IsNullOrWhiteSpace(snippet))
            {
                builder.AppendLine(snippet);
            }

            builder.AppendLine("日志: " + result.LogPath);
            return builder.ToString().TrimEnd();
        }

        private static bool ValidateGeneratedArtifacts(string taskDirectory, out string error)
        {
            error = string.Empty;
            string[] requiredFiles =
            {
                UiGenerationDataPaths.DesignPacketFileName,
                UiGenerationDataPaths.UiPrefabSpecFileName,
                UiGenerationDataPaths.ResourceMatchReportFileName,
                UiGenerationDataPaths.AnalysisResultFileName,
                UiGenerationDataPaths.AnalysisSummaryFileName,
            };

            for (int i = 0; i < requiredFiles.Length; i++)
            {
                string assetPath = taskDirectory + "/" + requiredFiles[i];
                if (!UiGenerationJsonFileUtility.Exists(assetPath) && !string.Equals(requiredFiles[i], UiGenerationDataPaths.AnalysisSummaryFileName, StringComparison.Ordinal))
                {
                    error = "自动分析成功退出，但缺少产物: " + requiredFiles[i];
                    return false;
                }

                if (string.Equals(requiredFiles[i], UiGenerationDataPaths.AnalysisSummaryFileName, StringComparison.Ordinal) &&
                    string.IsNullOrWhiteSpace(UiGenerationTaskStorage.LoadAnalysisSummary(taskDirectory)))
                {
                    error = "自动分析成功退出，但缺少产物: " + requiredFiles[i];
                    return false;
                }
            }

            if (!UiGenerationTaskStorage.TryLoadAnalysisResult(taskDirectory, out _, out error))
            {
                error = "自动分析成功退出，但分析结果校验失败: " + error;
                return false;
            }

            return true;
        }

        private static string TrimForDialog(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            int start = Math.Max(0, lines.Length - 12);
            var builder = new StringBuilder();
            for (int i = start; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                builder.AppendLine(lines[i]);
            }

            string trimmed = builder.ToString().TrimEnd();
            if (trimmed.Length > 1600)
            {
                return trimmed.Substring(trimmed.Length - 1600);
            }

            return trimmed;
        }

        private static void WriteLog(UiPrefabGeneratorAutoAnalysisResult result, string content)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.LogPath))
            {
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(result.LogPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(result.LogPath, content ?? string.Empty, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("写入自动分析日志失败: " + ex.Message);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown_task";
            }

            var builder = new StringBuilder(value.Length);
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                bool isInvalid = false;
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (invalid[j] == ch)
                    {
                        isInvalid = true;
                        break;
                    }
                }

                builder.Append(isInvalid ? '_' : ch);
            }

            string sanitized = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "unknown_task" : sanitized;
        }
    }

    public sealed class UiPrefabGeneratorAutoAnalysisResult
    {
        public bool Success;
        public string TaskId = string.Empty;
        public string TaskDirectory = string.Empty;
        public string RequestPath = string.Empty;
        public string ScriptPath = string.Empty;
        public string CommandLine = string.Empty;
        public string LogPath = string.Empty;
        public int ExitCode = -1;
        public string ErrorSummary = string.Empty;
        public string StandardOutput = string.Empty;
        public string StandardError = string.Empty;

        public string GetFailureMessage()
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(ErrorSummary))
            {
                builder.AppendLine(ErrorSummary.Trim());
            }

            builder.AppendLine("退出码: " + ExitCode);
            if (!string.IsNullOrWhiteSpace(CommandLine))
            {
                builder.AppendLine("命令: " + CommandLine);
            }

            string snippet = TrimForDialog(!string.IsNullOrWhiteSpace(StandardError) ? StandardError : StandardOutput);
            if (!string.IsNullOrWhiteSpace(snippet))
            {
                builder.AppendLine(snippet);
            }

            if (!string.IsNullOrWhiteSpace(LogPath))
            {
                builder.AppendLine("日志: " + LogPath);
            }

            return builder.ToString().TrimEnd();
        }

        private static string TrimForDialog(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            int start = Math.Max(0, lines.Length - 12);
            var builder = new StringBuilder();
            for (int i = start; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                builder.AppendLine(lines[i]);
            }

            string trimmed = builder.ToString().TrimEnd();
            if (trimmed.Length > 1600)
            {
                return trimmed.Substring(trimmed.Length - 1600);
            }

            return trimmed;
        }
    }
}

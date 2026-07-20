using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Sheng.GameFramework.Editor.Luban
{
    /// <summary>
    /// 外部工具进程执行结果
    /// </summary>
    public sealed class LubanProcessResult
    {
        public int ExitCode;
        public string StandardOutput;
        public string StandardError;
        public string Command;
        public bool Success => ExitCode == 0;
    }

    /// <summary>
    /// 统一执行 dotnet Luban 和解压命令
    /// </summary>
    public static class LubanProcessRunner
    {
        public static LubanProcessResult Run(
            string executable,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            IReadOnlyDictionary<string, string> environment = null)
        {
            StringBuilder command = new StringBuilder(Quote(executable));
            if (arguments != null)
            {
                for (int i = 0; i < arguments.Count; i++)
                {
                    command.Append(' ').Append(Quote(arguments[i]));
                }
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = BuildArguments(arguments),
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            if (environment != null)
            {
                foreach (KeyValuePair<string, string> pair in environment)
                {
                    startInfo.EnvironmentVariables[pair.Key] = pair.Value;
                }
            }

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                Task.WaitAll(outputTask, errorTask);
                return new LubanProcessResult
                {
                    ExitCode = process.ExitCode,
                    StandardOutput = outputTask.Result,
                    StandardError = errorTask.Result,
                    Command = command.ToString()
                };
            }
        }

        private static string BuildArguments(IReadOnlyList<string> arguments)
        {
            if (arguments == null || arguments.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < arguments.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(Quote(arguments[i]));
            }

            return builder.ToString();
        }

        private static string Quote(string value)
        {
            string text = value ?? string.Empty;
            if (text.Length > 0
                && text.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '\"' }) < 0)
            {
                return text;
            }

            StringBuilder builder = new StringBuilder(text.Length + 2);
            builder.Append('\"');
            int slashCount = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                if (character == '\\')
                {
                    slashCount++;
                    continue;
                }

                if (character == '\"')
                {
                    builder.Append('\\', slashCount * 2 + 1);
                    builder.Append('\"');
                    slashCount = 0;
                    continue;
                }

                builder.Append('\\', slashCount);
                slashCount = 0;
                builder.Append(character);
            }

            builder.Append('\\', slashCount * 2);
            builder.Append('\"');
            return builder.ToString();
        }
    }
}

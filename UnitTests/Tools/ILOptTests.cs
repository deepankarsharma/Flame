using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Loyc.MiniTest;

namespace UnitTests
{
    [TestFixture]
    public sealed class ILOptTests
    {
        [Test]
        public void RunTests()
        {
            foreach (var file in Directory.GetFiles(
                Path.Combine(ProjectPath, "ToolTests", "ILOpt"),
                "*.cs",
                SearchOption.TopDirectoryOnly))
            {
                Console.WriteLine(" - " + Path.GetFileName(file));
                CompileOptimizeAndRun(file, (cmd, exe) =>
                {
                    if (cmd.Command == "run")
                    {
                        string stdout, stderr;
                        Assert.AreEqual(0, RunExe(exe, cmd.Argument, out stdout, out stderr));
                        return stdout;
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                });
            }
        }

        /// <summary>
        /// Compiles, optimizes and runs a file at a particular location.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file to compile, optimize and run.
        /// </param>
        /// <param name="runCommand">
        /// Runs a command, taking the command itself and a
        /// path to an executable as arguments.
        /// </param>
        public static void CompileOptimizeAndRun(
            string fileName,
            Func<ToolCommand, string, string> runCommand)
        {
            var prefix = Path.GetTempPath() + Guid.NewGuid().ToString();
            var exePath = prefix + ".exe";
            var optExePath = prefix + ".opt.exe";
            try
            {
                CompileCSharp(fileName, exePath);
                OptimizeIL(exePath, optExePath);
                var commands = ReadCommands(fileName);
                foreach (var command in commands)
                {
                    var regularOutput = runCommand(command, exePath);
                    var optOutput = runCommand(command, optExePath);
                    Assert.AreEqual(regularOutput, optOutput);
                }
            }
            finally
            {
                File.Delete(exePath);
                File.Delete(optExePath);
            }
        }

        /// <summary>
        /// Compiles the C# file at a particular path to an
        /// executable.
        /// </summary>
        /// <param name="inputPath">The file to compile.</param>
        /// <param name="outputPath">The path to store the exe at.</param>
        /// <param name="compilerName">The name of the compiler to use.</param>
        public static void CompileCSharp(
            string inputPath,
            string outputPath,
            string compilerName = "csc")
        {
            string stdout, stderr;
            int exitCode = RunProcess(
                compilerName,
                $"\"{inputPath}\" \"/out:{outputPath}\" /optimize+",
                out stdout,
                out stderr);

            if (exitCode != 0)
            {
                throw new Exception($"Error while compiling {inputPath}: {stderr}");
            }
        }

        private static readonly string ProjectPath = Directory.GetParent(
            System.Reflection.Assembly.GetExecutingAssembly().Location).Parent.Parent.Parent.FullName;

        private static readonly string ILOptPath = Path.Combine(
            ProjectPath,
            "ILOpt",
            "bin",
            "Debug",
            "ilopt.exe");

        /// <summary>
        /// Optimizes an IL assembly at a particular path and
        /// writes the optimized version to the output path.
        /// </summary>
        /// <param name="inputPath">The assembly to optimize.</param>
        /// <param name="outputPath">The path to store the optimized assembly at.</param>
        public static void OptimizeIL(
            string inputPath,
            string outputPath)
        {
            string stdout, stderr;
            int exitCode = RunExe(
                ILOptPath,
                $"\"{inputPath}\" \"-o{outputPath}\"",
                out stdout,
                out stderr);

            if (exitCode != 0)
            {
                throw new Exception($"Error while optimizing {inputPath}: {stderr}");
            }
        }

        /// <summary>
        /// Runs a process to completion.
        /// </summary>
        /// <param name="processName">The name of the process to run.</param>
        /// <param name="arguments">The process' arguments.</param>
        /// <param name="stdout">The output produced by the process.</param>
        /// <returns>The process' exit code.</returns>
        public static int RunProcess(
            string processName,
            string arguments,
            out string stdout,
            out string stderr)
        {
            var process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.FileName = processName;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.Start();
            process.WaitForExit();
            stdout = process.StandardOutput.ReadToEnd();
            stderr = process.StandardError.ReadToEnd();
            return process.ExitCode;
        }

        /// <summary>
        /// Runs an executable to completion.
        /// </summary>
        /// <param name="processName">The name of the process to run.</param>
        /// <param name="arguments">The process' arguments.</param>
        /// <param name="stdout">The output produced by the process.</param>
        /// <returns>The process' exit code.</returns>
        public static int RunExe(
            string exePath,
            string arguments,
            out string stdout,
            out string stderr)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                case PlatformID.MacOSX:
                    return RunProcess("mono", $"\"{exePath}\" {arguments}", out stdout, out stderr);
                default:
                    return RunProcess(exePath, arguments, out stdout, out stderr);
            }
        }

        /// <summary>
        /// Reads all tool commands from a file.
        /// </summary>
        /// <param name="fileName">The file to read.</param>
        /// <returns>A list of tool commands.</returns>
        public static IReadOnlyList<ToolCommand> ReadCommands(string fileName)
        {
            var commands = new List<ToolCommand>();
            foreach (var line in File.ReadAllLines(fileName))
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("//!", StringComparison.Ordinal))
                {
                    var splitLine = trimmedLine.Substring("//!".Length).Split(new[] { ':' }, 2);
                    if (splitLine.Length > 1)
                    {
                        commands.Add(new ToolCommand(splitLine[0].Trim(), splitLine[1].Trim()));
                    }
                    else
                    {
                        commands.Add(new ToolCommand(splitLine[0].Trim(), ""));
                    }
                }
            }
            return commands;
        }
    }
}

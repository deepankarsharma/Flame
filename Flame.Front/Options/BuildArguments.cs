﻿using Flame.Front.Target;
using Flame.Compiler;
using Flame.Compiler.Projects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flame.Recompilation;

namespace Flame.Front.Options
{
    public class BuildArguments : ICompilerOptions, IEnumerable<KeyValuePair<string, string[]>>
    {
        public BuildArguments(IOptionParser<string[]> OptionParser)
        {
            this.OptionParser = OptionParser;
            this.args = new Dictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase);
        }

        public IOptionParser<string[]> OptionParser { get; private set; }

        #region ICompilerOptions Implementation

        public T GetOption<T>(string Key, T Default)
        {
            if (args.ContainsKey(Key) && OptionParser.CanParse<T>())
            {
                return OptionParser.ParseValue<T>(args[Key]);
            }
            else
            {
                return Default;
            }
        }

        public bool HasOption(string Key)
        {
            return args.ContainsKey(Key);
        }

        #endregion

        #region IEnumerable<KeyValuePair<string, string[]>> Implementation

        public IEnumerator<KeyValuePair<string, string[]>> GetEnumerator()
        {
            return args.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region Options as properties

        public bool HasSourcePath
        {
            get
            {
                return HasOption("source");
            }
        }

        public bool HasTargetPath
        {
            get
            {
                return HasOption("target");
            }
        }

        public PathIdentifier SourcePath
        {
            get
            {
                return GetOption<PathIdentifier>("source", new PathIdentifier(""));
            }
        }
        public PathIdentifier TargetPath
        {
            get
            {
                return GetOption<PathIdentifier>("target", new PathIdentifier(""));
            }
        }
        public bool? CompileSingleFile
        {
            get
            {
                bool? singleFile = GetOptionOrNull<bool>("file");
                if (singleFile.HasValue)
                {
                    return singleFile;
                }
                else
                {
                    return !GetOptionOrNull<bool>("project");
                }
            }
        }
        public bool? CompileProject
        {
            get
            {
                return !CompileSingleFile;
            }
        }
        public string TargetPlatform
        {
            get
            {
                return GetOption<string>("platform", "");
            }
        }
        public bool CompileAll
        {
            get
            {
                return GetOption<bool>("compileall", true);
            }
        }
        public bool MakeProject
        {
            get
            {
                return GetOption<bool>("make-project", false);
            }
        }
        public bool VerifyAssembly
        {
            get
            {
                return GetOption<bool>("verify", true);
            }
        }

        public IMethodOptimizer Optimizer
        {
            get
            {
                return GetOption<IMethodOptimizer>("optimize", null) ?? new DefaultOptimizer();
            }
        }

        public ILogFilter LogFilter
        {
            get
            {
                return GetOption<ILogFilter>("chat", null) ?? new ChatLogFilter(ChatLevel.Silent);
            }
        }

        /// <summary>
        /// Gets a boolean value that tells if the compiler should print its version number.
        /// </summary>
        public bool PrintVersion
        {
            get
            {
                return GetOption<bool>("version", false);
            }
        }

        /// <summary>
        /// Gets a boolean value that tells if the compiler has anything to compile.
        /// </summary>
        public bool CanCompile
        {
            get
            {
                return !SourcePath.IsEmpty;
            }
        }

        #endregion

        #region Helper Methods

        public bool InSingleFileMode(PathIdentifier Path, IEnumerable<string> SingleFileExtensions)
        {
            if (CompileSingleFile.HasValue)
            {
                return CompileSingleFile.Value;
            }
            else
            {
                return SingleFileExtensions.Contains(Path.Extension);
            }
        }

        public PathIdentifier GetTargetPathWithoutExtension(PathIdentifier CurrentPath, IProject Project)
        {
            PathIdentifier relUri;
            if (!TargetPath.IsEmpty)
            {
                relUri = TargetPath.ChangeExtension(null);
            }
            else
            {
                relUri = new PathIdentifier("bin", Project.Name);
            }
            return CurrentPath.GetAbsolutePath(relUri);
        }

        public PathIdentifier GetTargetPath(PathIdentifier CurrentPath, IProject Project, BuildTarget Target)
        {
            PathIdentifier relUri;
            if (!TargetPath.IsEmpty)
            {
                relUri = TargetPath;
            }
            else
            {
                relUri = new PathIdentifier("bin", Project.Name).AppendExtension(Target.Extension);
            }
            return CurrentPath.GetAbsolutePath(relUri);
        }

        public string GetTargetPlatform(IProject Project)
        {
            string platform = TargetPlatform;
            if (string.IsNullOrWhiteSpace(platform))
            {
                return Project.BuildTargetIdentifier;
            }
            else
            {
                return platform;
            }
        }

        #endregion

        #region Build Arguments

        private Dictionary<string, string[]> args;

        public T? GetOptionOrNull<T>(string Name)
            where T : struct
        {
            if (HasOption(Name))
            {
                return GetOption<T>(Name, default(T));
            }
            else
            {
                return null;
            }
        }

        public void AddBuildArgument(string Key, params string[] Value)
        {
            args[Key] = Value;
        }

        #endregion

        #region Static

        private static bool IsOption(string Argument)
        {
            return !string.IsNullOrEmpty(Argument) && Argument[0] == '-';
        }

        private static string GetOptionParameterName(string Argument)
        {
            return Argument.TrimStart('-');
        }

        private static string[] ParseArguments(ArgumentStream<string> ArgStream)
        {
            List<string> results = new List<string>();
            string peek = ArgStream.Peek();
            while (peek != null && !IsOption(peek))
            {
                ArgStream.MoveNext();
                results.Add(ArgStream.Current);
                peek = ArgStream.Peek();
            }
            return results.ToArray();
        }

        public static BuildArguments Parse(IOptionParser<string> OptionParser, ICompilerLog Log, params string[] Arguments)
        {
            return Parse(new StringArrayOptionParser(OptionParser), Log, Arguments);
        }

        public static BuildArguments Parse(IOptionParser<string[]> OptionParser, ICompilerLog Log, params string[] Arguments)
        {
            BuildArguments result = new BuildArguments(OptionParser);
            string[] defaultParameters = new string[]
            {
                "source",
                "target",
                "platform"
            };

            int defaultIndex = 0;
            var argStream = new ArgumentStream<string>(Arguments);
            while (argStream.MoveNext())
            {
                string item = argStream.Current;
                string param;
                if (!IsOption(item))
                {
                    if (defaultIndex < defaultParameters.Length)
                    {
                        param = defaultParameters[defaultIndex];
                        defaultIndex++;
                        argStream.Move(-1);
                    }
                    else
                    {
                        Log.LogWarning(new LogEntry("Build parameter mismatch", "Could not guess the meaning of default build parameter #" + defaultIndex + ": '" + item + "'."));
                        param = null;
                        continue;
                    }
                }
                else
                {
                    param = item;
                }

                // Parse arguments
                string[] args = ParseArguments(argStream);
                result.AddBuildArgument(GetOptionParameterName(param), args);
            }

            return result;
        }

        #endregion
    }
}

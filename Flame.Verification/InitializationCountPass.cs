﻿using Flame.Analysis;
using Flame.Compiler;
using Flame.Compiler.Build;
using Flame.Compiler.Visitors;
using Flame.Syntax;
using Pixie;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flame.Verification
{
    /// <summary>
    /// A pass that checks initialization counts and reports irregularities.
    /// </summary>
    public class InitializationCountPass : IPass<Tuple<IStatement, IMethod>, Tuple<IStatement, IMethod>>
    {
        public InitializationCountPass(ICompilerLog Log)
        {
            this.Log = Log;
        }

        public ICompilerLog Log { get; private set; }

        /// <summary>
        /// A warning name for potentially uninitialized values.
        /// </summary>
        public const string UninitializedWarningName = "uninitialized";

        /// <summary>
        /// A warning name for potentially multiply initialized values.
        /// </summary>
        public const string MultipleInitializationWarningName = "multiple-initialization";

        /// <summary>
        /// Gets a boolean value that tells if this pass is useful for the given log,
        /// i.e. not all of its warnings have been suppressed.
        /// </summary>
        /// <param name="Log"></param>
        /// <returns></returns>
        public static bool IsUseful(ICompilerLog Log)
        {
            return Log.UsePedanticWarnings(UninitializedWarningName) || Log.UsePedanticWarnings(MultipleInitializationWarningName);
        }

        private static LogEntry AppendInitialization(LogEntry Entry, NodeCountVisitor Visitor)
        {
            var nodes = Visitor.MatchLocations.Select(item => RedefinitionHelpers.Instance.CreateNeutralDiagnosticsNode("Initialization at: ", item));
            return new LogEntry(Entry.Name, new MarkupNode("entry", new IMarkupNode[] { Entry.Contents }.Concat(nodes)));
        }

        public Tuple<IStatement, IMethod> Apply(Tuple<IStatement, IMethod> Value)
        {
            if (Value.Item2.IsConstructor && !Value.Item2.IsStatic)
            {
                var visitor = InitializationCountHelpers.CreateVisitor();
                visitor.Visit(Value.Item1);
                if (visitor.CurrentFlow.Min == 0)
                {
                    if (Log.UsePedanticWarnings(UninitializedWarningName))
                    {
                        var msg = new LogEntry("Instance possibly uninitialized", 
                                               "Not all control flow paths may initialize the constructed instance. " + 
                                               Warnings.Instance.GetWarningNameMessage(UninitializedWarningName),
                                               Value.Item2.GetSourceLocation());
                        Log.LogWarning(AppendInitialization(msg, visitor));
                    }
                }
                else if (visitor.CurrentFlow.Max > 1)
                {
                    if (Log.UsePedanticWarnings(MultipleInitializationWarningName))
                    {
                        var msg = new LogEntry("Instance possibly initialized more than once",
                                               "The constructed instance may be initialized more than once in some control flow paths. " +
                                               Warnings.Instance.GetWarningNameMessage(MultipleInitializationWarningName),
                                               Value.Item2.GetSourceLocation());
                        Log.LogWarning(AppendInitialization(msg, visitor));
                    }
                }
            }
            return Value;
        }
    }
}

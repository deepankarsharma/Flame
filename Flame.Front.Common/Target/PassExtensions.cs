﻿using Flame.Compiler;
using Flame.Compiler.Visitors;
using Flame.Optimization;
using Flame.Recompilation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flame.Front.Target
{
    using MethodPassInfo = PassInfo<BodyPassArgument, IStatement>;
    using SignaturePassInfo = PassInfo<MemberSignaturePassArgument<IMember>, MemberSignaturePassResult>;
    using StatementPassInfo = PassInfo<IStatement, IStatement>;
    using RootPassInfo = PassInfo<BodyPassArgument, IEnumerable<IMember>>;
    using IRootPass = IPass<BodyPassArgument, IEnumerable<IMember>>;
    using ISignaturePass = IPass<MemberSignaturePassArgument<IMember>, MemberSignaturePassResult>;
    using Flame.Front.Passes;

    public static class PassExtensions
    {
        static PassExtensions()
        {
			GlobalPassManager = new PassManager();

            GlobalPassManager.RegisterMethodPass(new StatementPassInfo(NodeOptimizationPass.Instance, NodeOptimizationPass.NodeOptimizationPassName));

            // Activate -fstrip-assert whenever -g is turned off, and the
            // optimization level is at least -O1.
			GlobalPassManager.RegisterMethodPass(new MethodPassInfo(StripAssertPass.Instance, StripAssertPass.StripAssertPassName));
			GlobalPassManager.RegisterPassCondition(new PassCondition(StripAssertPass.StripAssertPassName, optInfo => optInfo.OptimizeMinimal && !optInfo.OptimizeDebug));

            GlobalPassManager.RegisterMethodPass(new MethodPassInfo(SlimLambdaPass.Instance, SlimLambdaPass.SlimLambdaPassName));
            GlobalPassManager.RegisterMethodPass(new MethodPassInfo(LowerLambdaPass.Instance, LowerLambdaPassName));
            GlobalPassManager.RegisterMethodPass(new MethodPassInfo(LowerContractPass.Instance, LowerContractPass.LowerContractPassName));
            GlobalPassManager.RegisterMethodPass(new MethodPassInfo(Flame.Optimization.FlattenInitializationPass.Instance, Flame.Optimization.FlattenInitializationPass.FlattenInitializationPassName));
            GlobalPassManager.RegisterMethodPass(new MethodPassInfo(TailRecursionPass.Instance, TailRecursionPass.TailRecursionPassName));
            GlobalPassManager.RegisterPassCondition(TailRecursionPass.TailRecursionPassName, optInfo => optInfo.OptimizeNormal);

            GlobalPassManager.RegisterMethodPass(new MethodPassInfo(InliningPass.Instance, InliningPass.InliningPassName));
            GlobalPassManager.RegisterPassCondition(InliningPass.InliningPassName, optInfo => optInfo.OptimizeAggressive);
            GlobalPassManager.RegisterMethodPass(new StatementPassInfo(SimplifyFlowPass.Instance, SimplifyFlowPassName));
            GlobalPassManager.RegisterPassCondition(SimplifyFlowPassName, optInfo => optInfo.OptimizeNormal);
            GlobalPassManager.RegisterPassCondition(SimplifyFlowPassName, optInfo => optInfo.OptimizeSize);
            GlobalPassManager.RegisterMethodPass(new StatementPassInfo(Flame.Optimization.Variables.DefinitionPropagationPass.Instance, PropagateLocalsName));
            // GlobalPassManager.RegisterPassCondition(PropagateLocalsName, optInfo => optInfo.OptimizeAggressive);
            GlobalPassManager.RegisterMethodPass(new StatementPassInfo(Flame.Optimization.ImperativeCodePass.Instance, Flame.Optimization.ImperativeCodePass.ImperativeCodePassName));

            // Note: these CFG/SSA passes are -O3 for now
            GlobalPassManager.RegisterMethodPass(new StatementPassInfo(ConstructFlowGraphPass.Instance, ConstructFlowGraphPass.ConstructFlowGraphPassName));
            GlobalPassManager.RegisterPassCondition(ConstructFlowGraphPass.ConstructFlowGraphPassName, optInfo => optInfo.OptimizeAggressive);
            GlobalPassManager.RegisterMethodPass(new StatementPassInfo(SimplifySelectFlowPass.Instance, SimplifySelectFlowPass.SimplifySelectFlowPassName));
            GlobalPassManager.RegisterPassCondition(SimplifySelectFlowPass.SimplifySelectFlowPassName, optInfo => optInfo.OptimizeAggressive);
            GlobalPassManager.RegisterMethodPass(new StatementPassInfo(JumpThreadingPass.Instance, JumpThreadingPass.JumpThreadingPassName));
            GlobalPassManager.RegisterPassCondition(JumpThreadingPass.JumpThreadingPassName, optInfo => optInfo.OptimizeAggressive);
			GlobalPassManager.RegisterMethodPass(new MethodPassInfo(DeadBlockEliminationPass.Instance, DeadBlockEliminationPass.DeadBlockEliminationPassName));
            GlobalPassManager.RegisterPassCondition(DeadBlockEliminationPass.DeadBlockEliminationPassName, optInfo => optInfo.OptimizeAggressive);
            GlobalPassManager.RegisterMethodPass(new StatementPassInfo(ConstructSSAPass.Instance, ConstructSSAPass.ConstructSSAPassName));
            GlobalPassManager.RegisterPassCondition(ConstructSSAPass.ConstructSSAPassName, optInfo => optInfo.OptimizeAggressive);
            GlobalPassManager.RegisterMethodPass(new StatementPassInfo(RemoveTrivialPhiPass.Instance, RemoveTrivialPhiPass.RemoveTrivialPhiPassName));
            GlobalPassManager.RegisterPassCondition(RemoveTrivialPhiPass.RemoveTrivialPhiPassName, optInfo => optInfo.OptimizeAggressive);
            GlobalPassManager.RegisterMethodPass(new StatementPassInfo(ConstantPropagationPass.Instance, ConstantPropagationPass.ConstantPropagationPassName));
            GlobalPassManager.RegisterPassCondition(ConstantPropagationPass.ConstantPropagationPassName, optInfo => optInfo.OptimizeAggressive);
            GlobalPassManager.RegisterMethodPass(new StatementPassInfo(CopyPropagationPass.Instance, CopyPropagationPass.CopyPropagationPassName));
            GlobalPassManager.RegisterPassCondition(CopyPropagationPass.CopyPropagationPassName, optInfo => optInfo.OptimizeAggressive);
            GlobalPassManager.RegisterMethodPass(new StatementPassInfo(DeadStoreEliminationPass.Instance, DeadStoreEliminationPass.DeadStoreEliminationPassName));
            GlobalPassManager.RegisterPassCondition(DeadStoreEliminationPass.DeadStoreEliminationPassName, optInfo => optInfo.OptimizeAggressive);
            GlobalPassManager.RegisterMethodPass(new StatementPassInfo(ConcatBlocksPass.Instance, ConcatBlocksPass.ConcatBlocksPassName));
            GlobalPassManager.RegisterPassCondition(ConcatBlocksPass.ConcatBlocksPassName, optInfo => optInfo.OptimizeAggressive);

            // Watch out with -fstack-intrinsics
            GlobalPassManager.RegisterLoweringPass(new StatementPassInfo(StackIntrinsicsPass.Instance, StackIntrinsicsPass.StackIntrinsicsPassName));
            GlobalPassManager.RegisterPassCondition(StackIntrinsicsPass.StackIntrinsicsPassName, optInfo => optInfo.OptimizeExperimental);
            
			GlobalPassManager.RegisterLoweringPass(new StatementPassInfo(DeconstructSSAPass.Instance, DeconstructSSAPass.DeconstructSSAPassName));
            GlobalPassManager.RegisterPassCondition(DeconstructSSAPass.DeconstructSSAPassName, optInfo => optInfo.OptimizeAggressive);
			GlobalPassManager.RegisterLoweringPass(new StatementPassInfo(DeconstructFlowGraphPass.Instance, DeconstructFlowGraphPass.DeconstructFlowGraphPassName));
            GlobalPassManager.RegisterPassCondition(DeconstructFlowGraphPass.DeconstructFlowGraphPassName, optInfo => optInfo.OptimizeAggressive);

			GlobalPassManager.RegisterRootPass(new RootPassInfo(GenerateStaticPass.Instance, GenerateStaticPass.GenerateStaticPassName));

            // Register -fnormalize-names-clr here, because the IR back-end could also use
            // this pass when targeting the CLR platform indirectly.
			GlobalPassManager.RegisterSignaturePass(new SignaturePassInfo(Flame.Cecil.NormalizeNamesPass.Instance, Flame.Cecil.NormalizeNamesPass.NormalizeNamesPassName));

            // -fwrap-extension-properties is actually a set of two passes which are
            // always on or off at the same time.
			GlobalPassManager.RegisterRootPass(new RootPassInfo(WrapExtensionPropertiesPass.RootPassInstance, WrapExtensionPropertiesPass.WrapExtensionPropertiesPassName));
			GlobalPassManager.RegisterSignaturePass(new SignaturePassInfo(WrapExtensionPropertiesPass.SignaturePassInstance, WrapExtensionPropertiesPass.WrapExtensionPropertiesPassName));
        }

		/// <summary>
		/// Gets the global pass manager.
		/// </summary>
		/// <value>The global pass manager.</value>
		public static PassManager GlobalPassManager { get; private set; }

        public const string EliminateDeadCodePassName = "dead-code-elimination";
        public const string InitializationPassName = "initialization";
        public const string LowerYieldPassName = "lower-yield";
        public const string LowerLambdaPassName = "lower-lambda";
        public const string SimplifyFlowPassName = "simplify-flow";
        public const string PropagateLocalsName = "propagate-locals";

		private static PassManager WithPreferences(PassPreferences Preferences)
		{
			var newPassManager = new PassManager(GlobalPassManager);
			newPassManager.Prepend(Preferences);
			return newPassManager;
		}

        /// <summary>
        /// Gets the names of all passes that are selected by the
        /// given compiler log and pass preferences. The
        /// results are returned as a dictionary that maps pass types
        /// to a sequence of selected pass names.
        /// </summary>
        /// <param name="Log"></param>
        /// <param name="Preferences"></param>
        /// <returns></returns>
        public static IReadOnlyDictionary<string, IEnumerable<string>> GetSelectedPassNames(ICompilerLog Log, PassPreferences Preferences)
        {
			return WithPreferences(Preferences).GetSelectedPassNames(Log);
        }

        /// <summary>
        /// Creates a pass suite from the given compiler log and
        /// pass preferences.
        /// </summary>
        /// <param name="Log"></param>
        /// <param name="Preferences"></param>
        /// <returns></returns>
        public static PassSuite CreateSuite(ICompilerLog Log, PassPreferences Preferences)
        {
			return WithPreferences(Preferences).CreateSuite(Log);
        }
    }
}

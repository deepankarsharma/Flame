using System;
using System.Collections.Generic;
using Flame.Compiler.Instructions;

namespace Flame.Compiler.Analysis
{
    /// <summary>
    /// Maps instruction prototypes to exception specifications.
    /// </summary>
    public abstract class PrototypeExceptionSpecs
    {
        /// <summary>
        /// Gets the exception specification for a particular instruction
        /// prototype.
        /// </summary>
        /// <param name="prototype">The prototype to examine.</param>
        /// <returns>An exception specification for <paramref name="prototype"/>.</returns>
        public abstract ExceptionSpecification GetExceptionSpecification(InstructionPrototype prototype);
    }

    /// <summary>
    /// Assigns exception specifications to prototypes based
    /// on a set of user-configurable rules.
    /// </summary>
    public sealed class RuleBasedPrototypeExceptionSpecs : PrototypeExceptionSpecs
    {
        /// <summary>
        /// Creates an empty set of prototype exception spec rules.
        /// </summary>
        public RuleBasedPrototypeExceptionSpecs()
        {
            this.instructionSpecs = new Dictionary<Type, Func<InstructionPrototype, ExceptionSpecification>>();
            this.intrinsicSpecs = new Dictionary<string, Func<IntrinsicPrototype, ExceptionSpecification>>();
        }

        /// <summary>
        /// Creates a copy of another set of prototype exception spec rules.
        /// </summary>
        public RuleBasedPrototypeExceptionSpecs(RuleBasedPrototypeExceptionSpecs other)
        {
            this.instructionSpecs = new Dictionary<Type, Func<InstructionPrototype, ExceptionSpecification>>(
                other.instructionSpecs);
            this.intrinsicSpecs = new Dictionary<string, Func<IntrinsicPrototype, ExceptionSpecification>>(
                other.intrinsicSpecs);
        }

        private Dictionary<Type, Func<InstructionPrototype, ExceptionSpecification>> instructionSpecs;
        private Dictionary<string, Func<IntrinsicPrototype, ExceptionSpecification>> intrinsicSpecs;

        /// <summary>
        /// Registers a function that computes exception specifications
        /// for a particular type of instruction prototype.
        /// </summary>
        /// <param name="getExceptionSpec">
        /// A function that computes exception specifications for all
        /// instruction prototypes of type T.
        /// </param>
        /// <typeparam name="T">
        /// The type of instruction prototypes to which
        /// <paramref name="getExceptionSpec"/> is applicable.
        /// </typeparam>
        public void Register<T>(Func<T, ExceptionSpecification> getExceptionSpec)
            where T : InstructionPrototype
        {
            instructionSpecs[typeof(T)] = proto => getExceptionSpec((T)proto);
        }

        /// <summary>
        /// Maps a particular type of instruction prototype
        /// to an exception specification.
        /// </summary>
        /// <param name="exceptionSpec">
        /// The exception specification to register.
        /// </param>
        /// <typeparam name="T">
        /// The type of instruction prototypes to which
        /// <paramref name="exceptionSpec"/> is applicable.
        /// </typeparam>
        public void Register<T>(ExceptionSpecification exceptionSpec)
            where T : InstructionPrototype
        {
            instructionSpecs[typeof(T)] = proto => exceptionSpec;
        }

        /// <summary>
        /// Registers a function that computes exception specifications
        /// for a particular type of intrinsic.
        /// </summary>
        /// <param name="intrinsicName">
        /// The name of the intrinsic to compute exception
        /// specifications for.
        /// </param>
        /// <param name="getExceptionSpec">
        /// A function that takes an intrinsic prototype with
        /// name <paramref name="intrinsicName"/> and computes
        /// the exception specification for that prototype.
        /// </param>
        public void Register(string intrinsicName, Func<IntrinsicPrototype, ExceptionSpecification> getExceptionSpec)
        {
            intrinsicSpecs[intrinsicName] = getExceptionSpec;
        }

        /// <summary>
        /// Registers a function that assigns a fixed exception specification
        /// to a particular type of intrinsic.
        /// </summary>
        /// <param name="intrinsicName">
        /// The name of the intrinsic to assign the exception
        /// specifications to.
        /// </param>
        /// <param name="exceptionSpec">
        /// The exception specification for all intrinsic prototypes with
        /// name <paramref name="intrinsicName"/>.
        /// </param>
        public void Register(string intrinsicName, ExceptionSpecification exceptionSpec)
        {
            Register(intrinsicName, proto => exceptionSpec);
        }

        /// <inheritdoc/>
        public override ExceptionSpecification GetExceptionSpecification(InstructionPrototype prototype)
        {
            if (prototype is IntrinsicPrototype)
            {
                var intrinsic = (IntrinsicPrototype)prototype;
                Func<IntrinsicPrototype, ExceptionSpecification> getIntrinsicSpec;
                if (intrinsicSpecs.TryGetValue(intrinsic.Name, out getIntrinsicSpec))
                {
                    return getIntrinsicSpec(intrinsic);
                }
            }

            Func<InstructionPrototype, ExceptionSpecification> getInstructionSpec;
            if (instructionSpecs.TryGetValue(prototype.GetType(), out getInstructionSpec))
            {
                return getInstructionSpec(prototype);
            }

            return ExceptionSpecification.ThrowAny;
        }

        /// <summary>
        /// Gets the default prototype exception spec rules.
        /// </summary>
        /// <value>The default prototype exception spec rules.</value>
        public static readonly RuleBasedPrototypeExceptionSpecs Default;

        static RuleBasedPrototypeExceptionSpecs()
        {
            Default = new RuleBasedPrototypeExceptionSpecs();

            // Instruction prototypes that never throw.
            Default.Register<AllocaArrayPrototype>(ExceptionSpecification.NoThrow);
            Default.Register<AllocaPrototype>(ExceptionSpecification.NoThrow);
            Default.Register<BoxPrototype>(ExceptionSpecification.NoThrow);
            Default.Register<ConstantPrototype>(ExceptionSpecification.NoThrow);
            Default.Register<CopyPrototype>(ExceptionSpecification.NoThrow);
            Default.Register<DynamicCastPrototype>(ExceptionSpecification.NoThrow);
            Default.Register<GetStaticFieldPointerPrototype>(ExceptionSpecification.NoThrow);
            Default.Register<LoadPrototype>(ExceptionSpecification.NoThrow);
            Default.Register<ReinterpretCastPrototype>(ExceptionSpecification.NoThrow);
            Default.Register<StorePrototype>(ExceptionSpecification.NoThrow);

            // Instruction prototypes that may throw because of implicit null checks.
            Default.Register<GetFieldPointerPrototype>(
                new NullCheckExceptionSpecification(0, ExceptionSpecification.ThrowAny));
            Default.Register<NewDelegatePrototype>(
                proto => proto.Lookup == MethodLookup.Virtual
                    ? new NullCheckExceptionSpecification(0, ExceptionSpecification.ThrowAny)
                    : ExceptionSpecification.NoThrow);
            Default.Register<UnboxPrototype>(
                new NullCheckExceptionSpecification(0, ExceptionSpecification.ThrowAny));

            // Call-like instruction prototypes.
            // TODO: use the callee's exception specification.
            Default.Register<CallPrototype>(ExceptionSpecification.ThrowAny);
            Default.Register<NewObjectPrototype>(ExceptionSpecification.ThrowAny);
            Default.Register<IndirectCallPrototype>(ExceptionSpecification.ThrowAny);

            // Arithmetic intrinsics never throw.
            foreach (var name in ArithmeticIntrinsics.Operators.All)
            {
                Default.Register(
                    ArithmeticIntrinsics.GetArithmeticIntrinsicName(name),
                    ExceptionSpecification.NoThrow);
            }

            // Array intrinsics are a little more complicated.
            // TODO: model bounds checks somehow.
            Default.Register(
                ArrayIntrinsics.Namespace.GetIntrinsicName(ArrayIntrinsics.Operators.GetElementPointer),
                ExceptionSpecification.ThrowAny);
            Default.Register(
                ArrayIntrinsics.Namespace.GetIntrinsicName(ArrayIntrinsics.Operators.LoadElement),
                ExceptionSpecification.ThrowAny);
            Default.Register(
                ArrayIntrinsics.Namespace.GetIntrinsicName(ArrayIntrinsics.Operators.StoreElement),
                ExceptionSpecification.ThrowAny);
            Default.Register(
                ArrayIntrinsics.Namespace.GetIntrinsicName(ArrayIntrinsics.Operators.GetLength),
                new NullCheckExceptionSpecification(0, ExceptionSpecification.ThrowAny));
            Default.Register(
                ArrayIntrinsics.Namespace.GetIntrinsicName(ArrayIntrinsics.Operators.NewArray),
                ExceptionSpecification.NoThrow);

            // Exception intrinsics.
            Default.Register(
                ExceptionIntrinsics.Namespace.GetIntrinsicName(ExceptionIntrinsics.Operators.Capture),
                ExceptionSpecification.NoThrow);
            Default.Register(
                ExceptionIntrinsics.Namespace.GetIntrinsicName(ExceptionIntrinsics.Operators.GetCapturedException),
                ExceptionSpecification.NoThrow);
            Default.Register(
                ExceptionIntrinsics.Namespace.GetIntrinsicName(ExceptionIntrinsics.Operators.Rethrow),
                ExceptionSpecification.ThrowAny);
            Default.Register(
                ExceptionIntrinsics.Namespace.GetIntrinsicName(ExceptionIntrinsics.Operators.Throw),
                proto => ExceptionSpecification.Exactly(proto.ParameterTypes[0]));

            // Object intrinsics.
            // TODO: model exception thrown by type check.
            Default.Register(
                ObjectIntrinsics.Namespace.GetIntrinsicName(ObjectIntrinsics.Operators.UnboxAny),
                ExceptionSpecification.ThrowAny);
        }
    }

    /// <summary>
    /// An exception specification for an instruction parameter null check.
    /// </summary>
    public sealed class NullCheckExceptionSpecification : ExceptionSpecification
    {
        /// <summary>
        /// Creates a null-check exception specification.
        /// </summary>
        /// <param name="parameterIndex">
        /// The index of the instruction parameter that is null checked.
        /// </param>
        /// <param name="nullCheckSpec">
        /// The exception specification of the exception thrown
        /// if and when a null check fails.
        /// </param>
        public NullCheckExceptionSpecification(
            int parameterIndex,
            ExceptionSpecification nullCheckSpec)
        {
            this.ParameterIndex = parameterIndex;
            this.NullCheckSpec = nullCheckSpec;
        }

        /// <summary>
        /// Creates a null-check exception specification.
        /// </summary>
        /// <param name="parameterIndex">
        /// The index of the instruction parameter that is null checked.
        /// </param>
        /// <param name="exceptionType">
        /// The type of exception that is thrown if and when
        /// a null check fails.
        /// </param>
        public NullCheckExceptionSpecification(
            int parameterIndex,
            IType exceptionType)
            : this(parameterIndex, ExceptionSpecification.Exactly(exceptionType))
        { }

        /// <summary>
        /// Gets the index of the null-checked parameter,
        /// </summary>
        /// <value>The index of the null-checked parameter.</value>
        public int ParameterIndex { get; private set; }

        /// <summary>
        /// Gets the exception specification of the exception thrown
        /// if and when a null check fails.
        /// </summary>
        /// <value>An exception specification.</value>
        public ExceptionSpecification NullCheckSpec { get; private set; }

        /// <inheritdoc/>
        public override bool CanThrowSomething => NullCheckSpec.CanThrowSomething;

        /// <inheritdoc/>
        public override bool CanThrow(IType exceptionType)
        {
            return NullCheckSpec.CanThrow(exceptionType);
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace Flame
{
    /// <summary>
    /// Specifies the exception throwing behavior of a method or instruction.
    /// </summary>
    public abstract class ExceptionSpecification
    {
        /// <summary>
        /// Tells if this exception specification allows for any exceptions
        /// at all to be thrown.
        /// </summary>
        /// <returns>
        /// <c>true</c> if this exception specification allows for
        /// at least one type of exception to be thrown; otherwise, <c>false</c>.
        /// </returns>
        public abstract bool CanThrowSomething { get; }

        /// <summary>
        /// Tells if this exception specification allows for an exception
        /// of a particular type or a derived type to be thrown.
        /// </summary>
        /// <param name="exceptionType">The type of exception to examine.</param>
        /// <returns>
        /// <c>true</c> if an exception of type <paramref name="exceptionType"/>
        /// or a derived type can be thrown; otherwise, <c>false</c>.
        /// </returns>
        public abstract bool CanThrow(IType exceptionType);

        /// <summary>
        /// An exception specification that does not allow for any exceptions
        /// to be thrown.
        /// </summary>
        /// <returns>A no-throw exception specification.</returns>
        public static readonly ExceptionSpecification NoThrow
            = new NoThrowExceptionSpecification();

        /// <summary>
        /// An exception specification that allows for an exception of any type
        /// to be thrown.
        /// </summary>
        /// <returns>A throw-any exception specification.</returns>
        public static readonly ExceptionSpecification ThrowAny
            = new ThrowAnyExceptionSpecification();

        /// <summary>
        /// Creates an exception specification that can throw
        /// exactly one type of exception.
        /// </summary>
        /// <param name="exceptionType">
        /// The single exception type that can be thrown.
        /// </param>
        public static ExactExceptionSpecification Exactly(
            IType exceptionType)
        {
            return new ExactExceptionSpecification(exceptionType);
        }

        /// <summary>
        /// Takes the union of a sequence of exception specifications.
        /// The union can throw an exception iff said
        /// exception is throwable by any of the operands.
        /// </summary>
        /// <param name="operands">
        /// The exception specifications to which the union operator
        /// is applied.
        /// </param>
        /// <returns>A union exception specification.</returns>
        public static UnionExceptionSpecification Union(
            params ExceptionSpecification[] operands)
        {
            return new UnionExceptionSpecification(operands);
        }
    }

    internal sealed class NoThrowExceptionSpecification : ExceptionSpecification
    {
        public override bool CanThrowSomething => false;

        public override bool CanThrow(IType exceptionType)
        {
            return false;
        }
    }

    internal sealed class ThrowAnyExceptionSpecification : ExceptionSpecification
    {
        public override bool CanThrowSomething => true;

        public override bool CanThrow(IType exceptionType)
        {
            return true;
        }
    }

    /// <summary>
    /// An exception specification that can throw an exception
    /// of exactly one type.
    /// </summary>
    public sealed class ExactExceptionSpecification : ExceptionSpecification
    {
        /// <summary>
        /// Creates an exception specification that can throw
        /// exactly one type of exception.
        /// </summary>
        /// <param name="exceptionType">
        /// The single exception type that can be thrown.
        /// </param>
        internal ExactExceptionSpecification(IType exceptionType)
        {
            this.ExceptionType = exceptionType;
        }

        /// <summary>
        /// Gets the type of exception that can be thrown by this
        /// exception specification.
        /// </summary>
        /// <value>The type of exception that can be thrown.</value>
        public IType ExceptionType { get; private set; }

        /// <inheritdoc/>
        public override bool CanThrowSomething => true;

        /// <inheritdoc/>
        public override bool CanThrow(IType exceptionType)
        {
            return exceptionType == this.ExceptionType;
        }
    }

    /// <summary>
    /// An exception specification that is the union of a sequence of other
    /// exception specifications: the union can throw an exception iff said
    /// exception is throwable by any of the operands.
    /// </summary>
    public sealed class UnionExceptionSpecification : ExceptionSpecification
    {
        /// <summary>
        /// Creates an exception specification that is the union of
        /// a list of operands.
        /// </summary>
        /// <param name="operands">
        /// The exception specifications to which the union operator
        /// is applied.
        /// </param>
        internal UnionExceptionSpecification(IReadOnlyList<ExceptionSpecification> operands)
        {
            this.Operands = operands;
        }

        /// <summary>
        /// Gets the list of exception specifications to which the
        /// union operator is applied.
        /// </summary>
        /// <value>A list of exception specifications.</value>
        public IReadOnlyList<ExceptionSpecification> Operands { get; private set; }

        /// <inheritdoc/>
        public override bool CanThrowSomething => Operands.Any(op => op.CanThrowSomething);

        /// <inheritdoc/>
        public override bool CanThrow(IType exceptionType)
        {
            return Operands.Any(op => op.CanThrow(exceptionType));
        }
    }
}

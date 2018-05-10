using System.Collections.Generic;
using System.Linq;
using Flame.Collections;
using Flame.TypeSystem;
using Loyc;
using Loyc.Syntax;
using Pixie.Markup;

namespace Flame.Ir
{
    /// <summary>
    /// A codec for type member references.
    /// </summary>
    public sealed class TypeMemberCodec : Codec<ITypeMember, LNode>
    {
        private TypeMemberCodec()
        { }

        /// <summary>
        /// A type member reference codec instance.
        /// </summary>
        public static readonly Codec<ITypeMember, LNode> Instance =
            new TypeMemberCodec();

        private readonly Symbol accessorSymbol = GSymbol.Get("#accessor");

        private readonly Dictionary<AccessorKind, string> accessorKindEncodings =
            new Dictionary<AccessorKind, string>()
        {
            { AccessorKind.Get, "get" },
            { AccessorKind.Set, "set" }
        };

        /// <inheritdoc/>
        public override ITypeMember Decode(LNode data, DecoderState state)
        {
            if (data.Calls(accessorSymbol))
            {
                if (!FeedbackHelpers.AssertArgCount(data, 2, state.Log)
                    || !FeedbackHelpers.AssertIsId(data.Args[1], state.Log))
                {
                    return null;
                }

                var property = state.DecodeProperty(data.Args[0]);
                if (property == null)
                {
                    return null;
                }
                else
                {
                    var kindName = data.Args[1].Name.Name;
                    var accessor = property.Accessors.FirstOrDefault(
                        acc => accessorKindEncodings[acc.Kind] == kindName);

                    if (accessor == null)
                    {
                        FeedbackHelpers.LogSyntaxError(
                            state.Log,
                            data.Args[1],
                            Quotation.QuoteEvenInBold(
                                "property ",
                                FeedbackHelpers.Print(data.Args[0]),
                                " does not define a ",
                                kindName,
                                " accessor."));
                    }
                    return accessor;
                }
            }
            else if (data.Calls(CodeSymbols.Dot, 2))
            {
                // Simple dot indicates a field.
                var parentType = state.DecodeType(data.Args[0]);

                if (parentType == ErrorType.Instance)
                {
                    return null;
                }

                var name = state.DecodeSimpleName(data.Args[1]);

                var candidates = state.TypeMemberIndex
                    .GetAll(parentType, name)
                    .OfType<IField>()
                    .ToArray();

                if (candidates.Length == 0)
                {
                    state.Log.LogSyntaxError(
                        data,
                        Quotation.QuoteEvenInBold(
                            "type ",
                            FeedbackHelpers.Print(data.Args[0]),
                            " does not define a field called ",
                            FeedbackHelpers.Print(data.Args[1]),
                            "."));
                    return null;
                }
                else if (candidates.Length > 1)
                {
                    state.Log.LogSyntaxError(
                        data,
                        Quotation.QuoteEvenInBold(
                            "type ",
                            FeedbackHelpers.Print(data.Args[0]),
                            " defines more than one field called ",
                            FeedbackHelpers.Print(data.Args[1]),
                            "."));
                    return null;
                }
                else
                {
                    return candidates[0];
                }
            }

            // TODO: handle methods and properties.

            throw new System.NotImplementedException();
        }

        /// <inheritdoc/>
        public override LNode Encode(ITypeMember value, EncoderState state)
        {
            if (value is IAccessor)
            {
                var acc = (IAccessor)value;

                return state.Factory.Call(
                    accessorSymbol,
                    state.Encode(acc.ParentProperty),
                    state.Factory.Id(accessorKindEncodings[acc.Kind]));
            }
            else if (value is IField)
            {
                return EncodeTypeAndName(value, state);
            }
            else if (value is IProperty)
            {
                var property = (IProperty)value;

                return state.Factory.Call(
                    CodeSymbols.IndexBracks,
                    new[]
                    {
                        EncodeTypeAndName(property, state)
                    }.Concat(
                        property.IndexerParameters.EagerSelect(
                            p => state.Encode(p.Type))));
            }

            // TODO: handle methods.

            throw new System.NotImplementedException();
        }

        private LNode EncodeTypeAndName(ITypeMember member, EncoderState state)
        {
            return state.Factory.Call(
                CodeSymbols.Dot,
                state.Encode(member.ParentType),
                state.Encode(member.Name));
        }
    }
}
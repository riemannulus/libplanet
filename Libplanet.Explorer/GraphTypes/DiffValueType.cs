using GraphQL.Types;

namespace Libplanet.Explorer.GraphTypes;

public class DiffValueType : ObjectGraphType<DiffValue>
{
    public DiffValueType()
    {
        Name = "DiffValue";
        Field<NonNullGraphType<StringGraphType>>(
            "key",
            "The corresponding key where values are stored.",
            resolve: ctx => ctx.Source.Key.Hex);
        Field<StringGraphType>(
            "targetValue",
            "The value stored in the target.",
            resolve: ctx => ctx.Source.TargetValue?.ToString());
        Field<NonNullGraphType<StringGraphType>>(
            "sourceValue",
            "The value stored in the source.",
            resolve: ctx => ctx.Source.SourceValue.ToString());
    }
}

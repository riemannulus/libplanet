using Bencodex.Types;
using Libplanet.Store.Trie;

namespace Libplanet.Explorer.GraphTypes;

public record DiffValue(KeyBytes Key, IValue? TargetValue, IValue SourceValue);

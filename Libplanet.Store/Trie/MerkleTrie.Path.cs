using Bencodex.Types;
using Libplanet.Store.Trie.Nodes;

namespace Libplanet.Store.Trie
{
    public partial class MerkleTrie
    {
        private IValue? ResolveToValue(INode? node, PathCursor cursor)
        {
            while (cursor.RemainingAnyNibbles)
            {
                switch (node)
                {
                    case null:
                        return null;
                    case ValueNode _:
                        return null;
                    case ShortNode shortNode:
                        if (cursor.RemainingNibblesStartWith(shortNode.Key))
                        {
                            node = shortNode.Value;
                            cursor = cursor.Next(shortNode.Key.Length);
                            break;
                        }
                        else
                        {
                            return null;
                        }

                    case FullNode fullNode:
                        node = fullNode.Children[cursor.NextNibble];
                        cursor = cursor.Next(1);
                        break;

                    case HashNode hashNode:
                        node = UnhashNode(hashNode);
                        break;

                    default:
                        throw new InvalidTrieNodeException(
                            $"Invalid node value: {node.ToBencodex().Inspect(false)}");
                }
            }

            return ResolveToValueBaseCase(node);
        }

        private IValue? ResolveToValueBaseCase(INode? node)
        {
            switch (node)
            {
                case null:
                    return null;
                case ValueNode valueNode:
                    return valueNode.Value;
                case ShortNode _:
                    return null;
                case FullNode fullNode:
                    return fullNode.Value is ValueNode fullNodeValue
                        ? fullNodeValue.Value
                        : null;
                case HashNode hashNode:
                    return ResolveToValueBaseCase(UnhashNode(hashNode));
                default:
                    throw new InvalidTrieNodeException(
                        $"Invalid node value: {node.ToBencodex().Inspect(false)}");
            }
        }

        private INode? ResolveToNode(INode? node, in PathCursor cursor)
        {
            if (cursor.RemainingAnyNibbles)
            {
                switch (node)
                {
                    case null:
                    case ValueNode _:
                        return null;
                    case ShortNode shortNode:
                        return cursor.RemainingNibblesStartWith(shortNode.Key)
                            ? ResolveToNode(shortNode.Value, cursor.Next(shortNode.Key.Length))
                            : null;
                    case FullNode fullNode:
                        return ResolveToNode(fullNode.Children[cursor.NextNibble], cursor.Next(1));
                    case HashNode hashNode:
                        return ResolveToNode(UnhashNode(hashNode), cursor);
                    default:
                        throw new InvalidTrieNodeException(
                            $"An unknown type of node was encountered " +
                            $"at {cursor.GetConsumedNibbles().Hex}: {node.GetType()}");
                }
            }
            else
            {
                return node;
            }
        }
    }
}

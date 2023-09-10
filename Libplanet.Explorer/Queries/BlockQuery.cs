#nullable disable
using System.Linq;
using System.Security.Cryptography;
using GraphQL;
using GraphQL.Types;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Types.Blocks;

namespace Libplanet.Explorer.Queries
{
    public class BlockQuery : ObjectGraphType
    {
        public BlockQuery()
        {
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<BlockType>>>>(
                "blocks",
                arguments: new QueryArguments(
                    new QueryArgument<BooleanGraphType>
                    {
                        Name = "desc",
                        DefaultValue = false,
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "offset",
                        DefaultValue = 0,
                    },
                    new QueryArgument<IntGraphType> { Name = "limit" },
                    new QueryArgument<BooleanGraphType>
                    {
                        Name = "excludeEmptyTxs",
                        DefaultValue = false,
                    },
                    new QueryArgument<AddressType> { Name = "miner" }
                ),
                resolve: context =>
                {
                    bool desc = context.GetArgument<bool>("desc");
                    long offset = context.GetArgument<long>("offset");
                    int? limit = context.GetArgument<int?>("limit", null);
                    bool excludeEmptyTxs = context.GetArgument<bool>("excludeEmptyTxs");
                    Address? miner = context.GetArgument<Address?>("miner", null);
                    return ExplorerQuery.ListBlocks(desc, offset, limit, excludeEmptyTxs, miner);
                }
            );

            Field<BlockType>(
                "block",
                arguments: new QueryArguments(
                    new QueryArgument<IdGraphType> { Name = "hash" },
                    new QueryArgument<IdGraphType> { Name = "index" }
                ),
                resolve: context =>
                {
                    string hash = context.GetArgument<string>("hash");
                    long? index = context.GetArgument<long?>("index", null);

                    if (!(hash is null ^ index is null))
                    {
                        throw new GraphQL.ExecutionError(
                            "The parameters hash and index are mutually exclusive; " +
                            "give only one at a time.");
                    }

                    if (hash is string hashNotNull)
                    {
                        return ExplorerQuery.GetBlockByHash(BlockHash.FromString(hashNotNull));
                    }

                    if (index is long indexNotNull)
                    {
                        return ExplorerQuery.GetBlockByIndex(indexNotNull);
                    }

                    throw new GraphQL.ExecutionError("Unexpected block query");
                }
            );

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<DiffValueType>>>>(
                "diff",
                arguments: new QueryArguments(
                    new QueryArgument<IdGraphType> { Name = "source" },
                    new QueryArgument<IdGraphType> { Name = "target" }),
                resolve: context =>
                {
                    string source = context.GetArgument<string>("source");
                    string target = context.GetArgument<string>("target");

                    var sourceTrie =
                        ExplorerQuery.GetTrieByHash(HashDigest<SHA256>.FromString(source));
                    var targetTrie =
                        ExplorerQuery.GetTrieByHash(HashDigest<SHA256>.FromString(target));
                    return sourceTrie.Diff(targetTrie)
                        .Select(dv => new DiffValue(dv.Path, dv.TargetValue, dv.SourceValue));
                }
            );

            Name = "BlockQuery";
        }
    }
}

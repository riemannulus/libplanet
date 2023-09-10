using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL;
using GraphQL.Execution;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Explorer.Queries;
using Libplanet.Store.Trie;
using Xunit;
using static Libplanet.Explorer.Tests.GraphQLTestUtils;
using Libplanet.Blockchain;
using Libplanet.Store;
using Libplanet.Common;

namespace Libplanet.Explorer.Tests.Queries;

public class BlockQueryTest
{
    protected readonly BlockChain Chain;
    protected MockBlockChainContext Source;

    public BlockQueryTest()
    {
        Chain = Libplanet.Tests.TestUtils.MakeBlockChain<NullAction>(
            new BlockPolicy(),
            new MemoryStore(),
            new TrieStateStore(new MemoryKeyValueStore()),
            privateKey: new PrivateKey(),
            timestamp: DateTimeOffset.UtcNow);
        Source = new MockBlockChainContext(Chain);
        _ = new ExplorerQuery(Source);
    }

    [Fact]
    public async Task Diff()
    {
        Address addr1 = new Address("0000000000000000000000000000000000000001");
        Address addr2 = new Address("0000000000000000000000000000000000000002");
        Address addr3 = new Address("0000000000000000000000000000000000000003");
        Address addr4 = new Address("0000000000000000000000000000000000001000");

        ITrie targetTrie = Source.BlockChain.StateStore.GetStateRoot(null);
        ITrie sourceTrie = Source.BlockChain.StateStore.GetStateRoot(null);

        targetTrie = targetTrie.Set(new KeyBytes(addr3.ByteArray), new Text("foo"));
        targetTrie = Source.BlockChain.StateStore.Commit(targetTrie);

        sourceTrie = sourceTrie.Set(new KeyBytes(addr1.ByteArray), new Integer(123));
        sourceTrie = sourceTrie.Set(new KeyBytes(addr3.ByteArray), new Text("bar"));
        sourceTrie = Source.BlockChain.StateStore.Commit(sourceTrie);

        var targetHex = targetTrie.Hash.ToString();
        var sourceHex = sourceTrie.Hash.ToString();

        ExecutionResult result = await ExecuteQueryAsync<BlockQuery>(@$"
        {{
            diff(source: ""{sourceHex}"", target: ""{targetHex}"")
            {{
                key
                targetValue
                sourceValue
            }}
        }}
        ", source: Source);

        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData.ToValue());
        IList<object> diffValues =
            Assert.IsAssignableFrom<IList<object>>(resultDict["diff"]);

        Assert.Equal(2, diffValues.Count);
        List<Dictionary<string, string>> values = diffValues
            .Select(diffValue =>
                Assert.IsAssignableFrom<IDictionary<string, object>>(diffValue)
                    .ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value is { } v ? Assert.IsAssignableFrom<string>(v) : null))
            .OrderBy(diffValue => diffValue["key"])
            .ToList();

        Assert.Equal(ByteUtil.Hex(addr1.ByteArray), values[0]["key"]);
        Assert.Null(values[0]["targetValue"]);
        Assert.Equal(new Integer(123).ToString(), values[0]["sourceValue"]);

        Assert.Equal(ByteUtil.Hex(addr3.ByteArray), values[1]["key"]);
        Assert.Equal(new Text("foo").ToString(), values[1]["targetValue"]);
        Assert.Equal(new Text("bar").ToString(), values[1]["sourceValue"]);
    }
}

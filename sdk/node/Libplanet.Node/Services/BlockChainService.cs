using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Numerics;
using System.Security.Cryptography;
using Bencodex;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Action.Sys;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blockchain.Renderers;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Node.DependencyInjection;
using Libplanet.Node.Options;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Tx;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Services;

[Singleton]
[Singleton<IBlockChainService>]
internal sealed class BlockChainService : IBlockChainService, IActionRenderer
{
    private readonly SynchronizationContext _synchronizationContext;
    private readonly ILogger<BlockChainService> _logger;
    private readonly BlockChain _blockChain;
    private readonly ConcurrentDictionary<TxId, ManualResetEvent> _eventByTxId = [];
    private readonly ConcurrentDictionary<IValue, Exception> _exceptionByAction = [];

    public BlockChainService(
        IOptions<GenesisOptions> genesisOptions,
        IOptions<StoreOptions> storeOptions,
        PolicyService policyService,
        IActionLoader actionLoader,
        IPolicyActionsRegistry policyActions,
        ILogger<BlockChainService> logger)
    {
        _synchronizationContext = SynchronizationContext.Current ?? new();
        _logger = logger;
        _blockChain = CreateBlockChain(
            genesisOptions: genesisOptions.Value.Verify(),
            storeOptions: storeOptions.Value.Verify(),
            stagePolicy: policyService.StagePolicy,
            renderers: [this],
            actionLoader: actionLoader,
            policyActions: policyActions);
    }

    public event EventHandler<BlockEventArgs>? BlockAppended;

    public BlockChain BlockChain => _blockChain;

    void IRenderer.RenderBlock(Block oldTip, Block newTip)
    {
    }

    void IActionRenderer.RenderAction(
        IValue action, ICommittedActionContext context, HashDigest<SHA256> nextState)
    {
    }

    void IActionRenderer.RenderActionError(
        IValue action, ICommittedActionContext context, Exception exception)
    {
        _exceptionByAction.AddOrUpdate(action, exception, (_, _) => exception);
    }

    void IActionRenderer.RenderBlockEnd(Block oldTip, Block newTip)
    {
        _synchronizationContext.Post(Action, state: null);

        void Action(object? state)
        {
            foreach (var transaction in newTip.Transactions)
            {
                if (_eventByTxId.TryGetValue(transaction.Id, out var manualResetEvent))
                {
                    manualResetEvent.Set();
                }
            }

            _logger.LogInformation("#{Height}: Block appended", newTip.Index);
            BlockAppended?.Invoke(this, new(newTip));
        }
    }

    private static BlockChain CreateBlockChain(
        GenesisOptions genesisOptions,
        StoreOptions storeOptions,
        IStagePolicy stagePolicy,
        IRenderer[] renderers,
        IActionLoader actionLoader,
        IPolicyActionsRegistry policyActions)
    {
        var (store, stateStore) = CreateStore(storeOptions);
        var actionEvaluator = new ActionEvaluator(
            policyActionsRegistry: policyActions,
            stateStore,
            actionLoader);

        Block genesisBlock;
        if (genesisOptions.GenesisBlockPath is null)
        {
            genesisBlock = CreateGenesisBlock(genesisOptions);
        }
        else
        {
            genesisBlock = LoadGenesisBlock(genesisOptions.GenesisBlockPath);
        }

        var policy = new BlockPolicy(
            policyActionsRegistry: policyActions,
            blockInterval: TimeSpan.FromSeconds(8),
            validateNextBlockTx: (chain, transaction) => null,
            validateNextBlock: (chain, block) => null,
            getMaxTransactionsBytes: l => long.MaxValue,
            getMinTransactionsPerBlock: l => 0,
            getMaxTransactionsPerBlock: l => int.MaxValue,
            getMaxTransactionsPerSignerPerBlock: l => int.MaxValue
        );

        var blockChainStates = new BlockChainStates(store, stateStore);
        if (store.GetCanonicalChainId() is null)
        {
            return BlockChain.Create(
                policy: policy,
                stagePolicy: stagePolicy,
                store: store,
                stateStore: stateStore,
                genesisBlock: genesisBlock,
                actionEvaluator: actionEvaluator,
                renderers: renderers,
                blockChainStates: blockChainStates);
        }

        return new BlockChain(
            policy: policy,
            stagePolicy: stagePolicy,
            store: store,
            stateStore: stateStore,
            genesisBlock: genesisBlock,
            blockChainStates: blockChainStates,
            actionEvaluator: actionEvaluator,
            renderers: renderers);
    }

    private static (IStore, IStateStore) CreateStore(StoreOptions storeOptions)
    {
        return storeOptions.Type switch
        {
            StoreType.Disk => CreateDiskStore(),
            StoreType.Memory => CreateMemoryStore(),
            _ => throw new NotSupportedException($"Unsupported store type: {storeOptions.Type}"),
        };

        (MemoryStore, TrieStateStore) CreateMemoryStore()
        {
            var store = new MemoryStore();
            var stateStore = new TrieStateStore(new MemoryKeyValueStore());
            return (store, stateStore);
        }

        (RocksDBStore.RocksDBStore, TrieStateStore) CreateDiskStore()
        {
            var store = new RocksDBStore.RocksDBStore(storeOptions.StoreName);
            var keyValueStore = new RocksDBKeyValueStore(storeOptions.StateStoreName);
            var stateStore = new TrieStateStore(keyValueStore);
            return (store, stateStore);
        }
    }

    private static Block CreateGenesisBlock(GenesisOptions genesisOptions)
    {
        var genesisKey = PrivateKey.FromString(genesisOptions.GenesisKey);
        var validators = genesisOptions.Validators.Select(PublicKey.FromHex)
            .Select(item => new Validator(item, new BigInteger(1000)))
            .ToArray();
        var validatorSet = new ValidatorSet(validators: [.. validators]);
        var nonce = 0L;
        IAction[] actions =
        [
            new Initialize(
                validatorSet: validatorSet,
                states: ImmutableDictionary.Create<Address, IValue>()),
        ];

        var transaction = Transaction.Create(
            nonce: nonce,
            privateKey: genesisKey,
            genesisHash: null,
            actions: [.. actions.Select(item => item.PlainValue)],
            timestamp: DateTimeOffset.MinValue);
        var transactions = ImmutableList.Create(transaction);
        return BlockChain.ProposeGenesisBlock(
            privateKey: genesisKey,
            transactions: transactions,
            timestamp: DateTimeOffset.MinValue);
    }

    private static Block LoadGenesisBlock(string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            byte[] rawBlock;
            if (File.Exists(Path.GetFullPath(path)))
            {
                rawBlock = File.ReadAllBytes(Path.GetFullPath(path));
            }
            else
            {
                var uri = new Uri(path);
                using var client = new HttpClient();
                rawBlock = client.GetByteArrayAsync(uri).Result;
            }

            var blockDict = (Dictionary)new Codec().Decode(rawBlock);
            return BlockMarshaler.UnmarshalBlock(blockDict);
        }
        else
        {
            throw new ArgumentException("GenesisBlockPath or GenesisBlock must be set.");
        }
    }
}

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Action.State;
using Libplanet.Action.Tests.Common;
using Libplanet.Action.Tests.Mocks;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Tx;
using Xunit;
using Xunit.Abstractions;

namespace Libplanet.Tests.Action
{
    public abstract class WorldTest
    {
        protected readonly PrivateKey[] _keys;
        protected readonly Address[] _addr;
        protected readonly Currency[] _currencies;
        protected readonly IWorld _initWorld;
        protected readonly IActionContext _initContext;
        protected readonly Address _accountAddress = ReservedAddresses.LegacyAccount;

        protected WorldTest(ITestOutputHelper output)
        {
            _keys = new[]
            {
                new PrivateKey(),
                new PrivateKey(),
                new PrivateKey(),
            };

            _addr = _keys.Select(key => key.Address).ToArray();

            _currencies = new[]
            {
#pragma warning disable CS0618  // must test obsoleted Currency.Legacy() for backwards compatibility
                Currency.Legacy("FOO", 0, _addr[0]),
                Currency.Legacy("BAR", 0, _addr.Take(2).ToImmutableHashSet()),
                Currency.Legacy("BAZ", 0, null),
#pragma warning restore CS0618  // must test obsoleted Currency.Legacy() for backwards compatibility
                Currency.Uncapped("QUX", 0, minter: _addr[0]),
                Currency.Capped("QUUX", 0, (100, 0), minter: _addr[0]),
            };

            IStateStore stateStore = new TrieStateStore(new MemoryKeyValueStore());
            _initWorld = new World(new MockWorldState(stateStore, null))
                .SetAccount(
                    ReservedAddresses.LegacyAccount,
                    new Account(new MockAccountState(stateStore, null)
                        .SetBalance(_addr[0], _currencies[0], 5)
                        .SetBalance(_addr[0], _currencies[1], 10)
                        .SetBalance(_addr[0], _currencies[3], 5)
                        .SetBalance(_addr[1], _currencies[1], 15)
                        .SetBalance(_addr[1], _currencies[2], 20)
                        .SetValidator(new Validator(_keys[0].PublicKey, 1))
                        .SetValidator(new Validator(_keys[1].PublicKey, 1))
                        .SetValidator(new Validator(_keys[2].PublicKey, 1))));

            output.WriteLine("Fixtures  {0,-42}  FOO  BAR  BAZ  QUX  State  Validators", "Address");
            int i = 0;
            foreach (Address a in _addr)
            {
                output.WriteLine(
                    "_addr[{0}]  {1}  {2,3}  {3,3}  {4,3}  {5,3}",
                    i++,
                    a,
                    _initWorld
                        .GetAccount(ReservedAddresses.LegacyAccount).GetBalance(a, _currencies[0]),
                    _initWorld
                        .GetAccount(ReservedAddresses.LegacyAccount).GetBalance(a, _currencies[1]),
                    _initWorld
                        .GetAccount(ReservedAddresses.LegacyAccount).GetBalance(a, _currencies[2]),
                    _initWorld
                        .GetAccount(ReservedAddresses.LegacyAccount).GetBalance(a, _currencies[3]));
            }

            _initContext = CreateContext(_initWorld, _addr[0]);
        }

        public abstract int ProtocolVersion { get; }

        public abstract IActionContext CreateContext(IWorld world, Address signer);

        [Fact]
        public void InitialSetup()
        {
            Assert.Equal(
                Value(2, 20),
                _initWorld
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[1], _currencies[2]));
            Assert.Equal(
                Value(2, 0),
                _initWorld
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[2], _currencies[2]));
            Assert.Equal(
                Value(0, 5),
                _initWorld
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[0], _currencies[0]));
            Assert.Equal(
                Value(1, 10),
                _initWorld
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[0], _currencies[1]));
            Assert.Equal(
                Zero(2),
                _initWorld
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[0], _currencies[2]));
        }

        [Fact]
        public virtual void FungibleAssets()
        {
            IWorld a = _initWorld.TransferAsset(
                _initContext, _addr[1], _addr[2], Value(2, 5));
            Assert.Equal(
                Value(2, 15),
                a.GetAccount(ReservedAddresses.LegacyAccount).GetBalance(_addr[1], _currencies[2]));
            Assert.Equal(
                Value(2, 5),
                a.GetAccount(ReservedAddresses.LegacyAccount).GetBalance(_addr[2], _currencies[2]));
            Assert.Equal(
                Value(0, 5),
                a.GetAccount(ReservedAddresses.LegacyAccount).GetBalance(_addr[0], _currencies[0]));
            Assert.Equal(
                Value(1, 10),
                a.GetAccount(ReservedAddresses.LegacyAccount).GetBalance(_addr[0], _currencies[1]));
            Assert.Equal(
                Zero(2),
                a.GetAccount(ReservedAddresses.LegacyAccount).GetBalance(_addr[0], _currencies[2]));
            Assert.Equal(
                Zero(0),
                a.GetAccount(ReservedAddresses.LegacyAccount).GetBalance(_addr[1], _currencies[0]));
            Assert.Equal(
                Value(1, 15),
                a.GetAccount(ReservedAddresses.LegacyAccount).GetBalance(_addr[1], _currencies[1]));
            Assert.Equal(
                Zero(0),
                a.GetAccount(ReservedAddresses.LegacyAccount).GetBalance(_addr[2], _currencies[0]));
            Assert.Equal(
                Zero(1),
                a.GetAccount(ReservedAddresses.LegacyAccount).GetBalance(_addr[2], _currencies[1]));

            var accountDiff = AccountDiff.Create(
                _initContext.PreviousState.GetAccount(_accountAddress).Trie,
                a.GetAccount(ReservedAddresses.LegacyAccount).Trie);
            Assert.Equal(
                new[] { (_addr[1], _currencies[2].Hash), (_addr[2], _currencies[2].Hash) }
                    .ToImmutableHashSet(),
                accountDiff.FungibleAssetValueDiffs.Select(kv => (kv.Key.Item1, kv.Key.Item2))
                    .ToImmutableHashSet());
            Assert.Empty(accountDiff.StateDiffs);
        }

        [Fact]
        public virtual void TransferAsset()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _initWorld.TransferAsset(_initContext, _addr[0], _addr[1], Zero(0))
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _initWorld.TransferAsset(_initContext, _addr[0], _addr[1], Value(0, -1))
            );
            Assert.Throws<InsufficientBalanceException>(() =>
                _initWorld.TransferAsset(_initContext, _addr[0], _addr[1], Value(0, 6))
            );

            IWorld a = _initWorld.TransferAsset(
                _initContext,
                _addr[0],
                _addr[1],
                Value(0, 6),
                allowNegativeBalance: true
            );
            Assert.Equal(
                Value(0, -1),
                a.GetAccount(ReservedAddresses.LegacyAccount).GetBalance(_addr[0], _currencies[0]));
            Assert.Equal(
                Value(0, 6),
                a.GetAccount(ReservedAddresses.LegacyAccount).GetBalance(_addr[1], _currencies[0]));
        }

        [Fact]
        public virtual BlockChain TransferAssetInBlock()
        {
            var store = new MemoryStore();
            var stateStore = new TrieStateStore(new MemoryKeyValueStore());
            var privateKey = new PrivateKey();
            BlockChain chain = TestUtils.MakeBlockChain(
                new NullBlockPolicy(),
                store,
                stateStore,
                new SingleActionLoader(typeof(DumbAction)),
                protocolVersion: ProtocolVersion,
                privateKey: privateKey
            );

            DumbAction action = new DumbAction(_addr[0], "a", _addr[1], _addr[0], 5);
            Transaction tx = Transaction.Create(
                0,
                _keys[0],
                chain.Genesis.Hash,
                new[] { action }.ToPlainValues()
            );
            var preEvalBlock = TestUtils.ProposeNext(
                chain.Tip,
                new[] { tx },
                miner: privateKey.PublicKey,
                protocolVersion: ProtocolVersion);
            var stateRootHash = chain.DetermineBlockStateRootHash(preEvalBlock, out _);
            var hash = preEvalBlock.Header.DeriveBlockHash(stateRootHash, null);
            Block block = ProtocolVersion < 2
                ? new Block(preEvalBlock, (stateRootHash, null, hash))
                : chain.EvaluateAndSign(preEvalBlock, privateKey);
            chain.Append(
                block,
                TestUtils.CreateBlockCommit(block)
            );
            Assert.Equal(
                DumbAction.DumbCurrency * 5,
                chain
                    .GetWorldState()
                    .GetAccountState(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[0], DumbAction.DumbCurrency)
            );
            Assert.Equal(
                DumbAction.DumbCurrency * -5,
                chain
                    .GetWorldState()
                    .GetAccountState(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[1], DumbAction.DumbCurrency)
            );

            return chain;
        }

        [Fact]
        public virtual void MintAsset()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _initWorld.MintAsset(_initContext, _addr[0], Zero(0))
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _initWorld.MintAsset(_initContext, _addr[0], Value(0, -1))
            );

            IWorld delta0 = _initWorld;
            IActionContext context0 = _initContext;
            // currencies[0] (FOO) allows only _addr[0] to mint
            delta0 = delta0.MintAsset(context0, _addr[0], Value(0, 10));
            Assert.Equal(
                Value(0, 15),
                delta0
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[0], _currencies[0]));

            // currencies[1] (BAR) allows _addr[0] & _addr[1] to mint
            delta0 = delta0.MintAsset(context0, _addr[1], Value(1, 10));
            Assert.Equal(
                Value(1, 25),
                delta0
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[1], _currencies[1]));

            // currencies[2] (BAZ) allows everyone to mint
            delta0 = delta0.MintAsset(context0, _addr[2], Value(2, 10));
            Assert.Equal(
                Value(2, 10),
                delta0
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[2], _currencies[2]));

            IWorld delta1 = _initWorld;
            IActionContext context1 = CreateContext(delta1, _addr[1]);
            // currencies[0] (FOO) disallows _addr[1] to mint
            Assert.Throws<CurrencyPermissionException>(() =>
                delta1.MintAsset(context1, _addr[1], Value(0, 10)));

            // currencies[1] (BAR) allows _addr[0] & _addr[1] to mint
            delta1 = delta1.MintAsset(context1, _addr[0], Value(1, 20));
            Assert.Equal(
                Value(1, 30),
                delta1
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[0], _currencies[1]));

            // currencies[2] (BAZ) allows everyone to mint
            delta1 = delta1.MintAsset(context1, _addr[2], Value(2, 10));
            Assert.Equal(
                Value(2, 10),
                delta1
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[2], _currencies[2]));
        }

        [Fact]
        public virtual void BurnAsset()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _initWorld.BurnAsset(_initContext, _addr[0], Zero(0))
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _initWorld.BurnAsset(_initContext, _addr[0], Value(0, -1))
            );
            Assert.Throws<InsufficientBalanceException>(() =>
                _initWorld.BurnAsset(_initContext, _addr[0], Value(0, 6))
            );

            IWorld delta0 = _initWorld;
            IActionContext context0 = _initContext;
            // currencies[0] (FOO) allows only _addr[0] to burn
            delta0 = delta0.BurnAsset(context0, _addr[0], Value(0, 4));
            Assert.Equal(
                Value(0, 1),
                delta0
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[0], _currencies[0]));

            // currencies[1] (BAR) allows _addr[0] & _addr[1] to burn
            delta0 = delta0.BurnAsset(context0, _addr[1], Value(1, 10));
            Assert.Equal(
                Value(1, 5),
                delta0
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[1], _currencies[1]));

            // currencies[2] (BAZ) allows everyone to burn
            delta0 = delta0.BurnAsset(context0, _addr[1], Value(2, 10));
            Assert.Equal(
                Value(2, 10),
                delta0
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[1], _currencies[2]));

            IWorld delta1 = _initWorld;
            IActionContext context1 = CreateContext(delta1, _addr[1]);
            // currencies[0] (FOO) disallows _addr[1] to burn
            Assert.Throws<CurrencyPermissionException>(() =>
                delta1.BurnAsset(context1, _addr[0], Value(0, 5)));

            // currencies[1] (BAR) allows _addr[0] & _addr[1] to burn
            delta1 = delta1.BurnAsset(context1, _addr[1], Value(1, 10));
            Assert.Equal(
                Value(1, 5),
                delta1
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[1], _currencies[1]));

            // currencies[2] (BAZ) allows everyone to burn
            delta1 = delta1.BurnAsset(context1, _addr[1], Value(2, 10));
            Assert.Equal(
                Value(2, 10),
                delta1
                    .GetAccount(ReservedAddresses.LegacyAccount)
                    .GetBalance(_addr[1], _currencies[2]));
        }

        protected FungibleAssetValue Value(int currencyIndex, BigInteger quantity) =>
            new FungibleAssetValue(_currencies[currencyIndex], quantity, 0);

        protected FungibleAssetValue Zero(int currencyIndex) => Value(currencyIndex, 0);
    }
}

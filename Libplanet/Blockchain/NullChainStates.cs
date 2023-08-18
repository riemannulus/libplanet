using System.Collections.Generic;
using System.Collections.Immutable;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Store.Trie;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;

namespace Libplanet.Blockchain
{
    internal class NullChainStates : IBlockChainStates
    {
        public static readonly NullChainStates Instance = new NullChainStates();

        private NullChainStates()
        {
        }

        public IValue? GetState(
            Address address, BlockHash? offset) =>
            GetAccount(offset).GetState(address);

        public IReadOnlyList<IValue?> GetStates(
            IReadOnlyList<Address> addresses, BlockHash? offset) =>
            GetAccount(offset).GetStates(addresses);

        public FungibleAssetValue GetBalance(
            Address address, Currency currency, BlockHash? offset) =>
            GetAccount(offset).GetBalance(address, currency);

        public FungibleAssetValue GetTotalSupply(Currency currency, BlockHash? offset) =>
            GetAccount(offset).GetTotalSupply(currency);

        public ValidatorSet GetValidatorSet(BlockHash? offset) =>
            GetAccount(offset).GetValidatorSet();

        public IAccount GetAccount(BlockHash? offset) => new NullAccount(offset);
    }

#pragma warning disable SA1402  // File may only contain a single type
    internal class NullAccount : IAccount
#pragma warning restore SA1402
    {
        public NullAccount(BlockHash? blockHash)
        {
            BlockHash = blockHash ?? default;
            TotalUpdatedFungibleAssets = ImmutableHashSet<(Address, Currency)>.Empty;
            Trie = new MerkleTrie(new MemoryKeyValueStore());
            Delta = new AccountDelta();
        }

        public IAccountDelta Delta { get; }

        public ITrie Trie { get; }

        public BlockHash BlockHash { get; }

        public IImmutableSet<(Address, Currency)> TotalUpdatedFungibleAssets { get; }

        public IAccount SetState(Address address, IValue state)
        {
            throw new System.NotSupportedException();
        }

        public IAccount MintAsset(
            IActionContext context,
            Address recipient,
            FungibleAssetValue value)
        {
            throw new System.NotSupportedException();
        }

        public IAccount TransferAsset(
            IActionContext context,
            Address sender,
            Address recipient,
            FungibleAssetValue value,
            bool allowNegativeBalance = false)
        {
            throw new System.NotSupportedException();
        }

        public IAccount BurnAsset(IActionContext context, Address owner, FungibleAssetValue value)
        {
            throw new System.NotSupportedException();
        }

        public IAccount SetValidator(Validator validator)
        {
            throw new System.NotSupportedException();
        }

        public IValue? GetState(Address address) => null;

        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses) =>
            new IValue?[addresses.Count];

        public FungibleAssetValue GetBalance(Address address, Currency currency) =>
            currency * 0;

        public FungibleAssetValue GetTotalSupply(Currency currency)
        {
            if (!currency.TotalSupplyTrackable)
            {
                throw TotalSupplyNotTrackableException.WithDefaultMessage(currency);
            }

            return currency * 0;
        }

        public ValidatorSet GetValidatorSet() =>
            new ValidatorSet();
    }
}

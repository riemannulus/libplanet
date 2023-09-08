using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Store.Trie;
using Libplanet.Types.Assets;
using Libplanet.Types.Consensus;
using static Libplanet.Action.State.KeyConverters;

namespace Libplanet.Action.State
{
    /// <summary>
    /// A default implementation of <see cref="IAccountState"/> interface.
    /// </summary>
    public class AccountState : IAccountState
    {
        private readonly ITrie _trie;
        private readonly AccountStateCache _cache;

        public AccountState(ITrie trie)
            : this(trie, new AccountStateCache())
        {
        }

        public AccountState(ITrie trie, AccountStateCache cache)
        {
            _trie = trie;
            _cache = cache;
        }

        /// <inheritdoc cref="IAccountState.Trie"/>
        public ITrie Trie => _trie;

        /// <inheritdoc cref="IAccountState.GetState"/>
        public IValue? GetState(Address address)
        {
            if (_cache.TryGetValue(address, out IValue? cachedValue))
            {
                return cachedValue;
            }
            else
            {
                IValue? fetched = Trie.Get(ToStateKey(address));
                _cache.AddOrUpdate(address, fetched);
                return fetched;
            }
        }

        /// <inheritdoc cref="IAccountState.GetStates"/>
        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses) =>
            addresses.Select(address => GetState(address)).ToList();

        /// <inheritdoc cref="IAccountState.GetBalance"/>
        public FungibleAssetValue GetBalance(Address address, Currency currency)
        {
            KeyBytes[] keys = new[] { ToFungibleAssetKey(address, currency) };
            IReadOnlyList<IValue?> rawValues = Trie.Get(keys);
            return rawValues.Count > 0 && rawValues[0] is Bencodex.Types.Integer i
                ? FungibleAssetValue.FromRawValue(currency, i)
                : currency * 0;
        }

        /// <inheritdoc cref="IAccountState.GetTotalSupply"/>
        public FungibleAssetValue GetTotalSupply(Currency currency)
        {
            if (!currency.TotalSupplyTrackable)
            {
                throw TotalSupplyNotTrackableException.WithDefaultMessage(currency);
            }

            KeyBytes[] keys = new[] { ToTotalSupplyKey(currency) };
            IReadOnlyList<IValue?> rawValues = Trie.Get(keys);
            return rawValues.Count > 0 && rawValues[0] is Bencodex.Types.Integer i
                ? FungibleAssetValue.FromRawValue(currency, i)
                : currency * 0;
        }

        /// <inheritdoc cref="IAccountState.GetValidatorSet"/>
        public ValidatorSet GetValidatorSet()
        {
            KeyBytes[] keys = new[] { ValidatorSetKey };
            IReadOnlyList<IValue?> rawValues = Trie.Get(keys);
            return rawValues.Count > 0 && rawValues[0] is List list
                ? new ValidatorSet(list)
                : new ValidatorSet();
        }
    }
}

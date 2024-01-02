using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly ActivitySource _activitySource;
        private ITrie _trie;

        public AccountState(ITrie trie)
        {
            _trie = trie;
            _activitySource = new ActivitySource("Libplanet.Action.State");
        }

        /// <inheritdoc cref="IAccountState.Trie"/>
        public ITrie Trie => _trie;

        /// <inheritdoc cref="IAccountState.GetState"/>
        public IValue? GetState(Address address)
        {
            using Activity? a = _activitySource
                .StartActivity(ActivityKind.Internal)?
                .AddTag("Address", address.ToString());
            return Trie.Get(ToStateKey(address));
        }

        /// <inheritdoc cref="IAccountState.GetStates"/>
        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses) =>
            addresses.Select(GetState).ToList();

        /// <inheritdoc cref="IAccountState.GetBalance"/>
        public FungibleAssetValue GetBalance(Address address, Currency currency)
        {
            IValue? value = Trie.Get(ToFungibleAssetKey(address, currency));
            return value is Integer i
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

            IValue? value = Trie.Get(ToTotalSupplyKey(currency));
            return value is Integer i
                ? FungibleAssetValue.FromRawValue(currency, i)
                : currency * 0;
        }

        /// <inheritdoc cref="IAccountState.GetValidatorSet"/>
        public ValidatorSet GetValidatorSet()
        {
            IValue? value = Trie.Get(ValidatorSetKey);
            return value is List list
                ? new ValidatorSet(list)
                : new ValidatorSet();
        }
    }
}

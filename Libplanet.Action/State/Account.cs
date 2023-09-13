using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Numerics;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Store.Trie;
using Libplanet.Types.Assets;
using Libplanet.Types.Consensus;

namespace Libplanet.Action.State
{
    /// <summary>
    /// An internal implementation of <see cref="IAccount"/>.
    /// </summary>
    [Pure]
    public class Account : IAccount
    {
        public Account(ITrie trie)
            : this(trie, AccountDelta.Empty)
        {
        }

        internal Account(ITrie trie, IAccountDelta delta)
            : this(trie, delta, ImmutableDictionary<(Address, Currency), BigInteger>.Empty)
        {
        }

        internal Account(
            ITrie trie,
            IAccountDelta delta,
            IImmutableDictionary<(Address, Currency), BigInteger> totalUpdatedFungibles)
        {
            Trie = trie;
            Delta = delta;
            TotalUpdatedFungibles = totalUpdatedFungibles;
        }

        /// <inheritdoc cref="IAccountState.Trie"/>
        public ITrie Trie { get; }

        /// <inheritdoc/>
        public IAccountDelta Delta { get; }

        /// <inheritdoc/>
        public IImmutableSet<(Address, Currency)> TotalUpdatedFungibleAssets =>
            TotalUpdatedFungibles.Keys.ToImmutableHashSet();

        public IImmutableDictionary<(Address, Currency), BigInteger> TotalUpdatedFungibles
            { get; }

        /// <inheritdoc/>
        [Pure]
        public IValue? GetState(Address address)
        {
            AccountMetrics.GetStateTimer.Value?.Start();
            AccountMetrics.GetStateCount.Value += 1;
            IValue? state = Trie.Get(KeyConverters.ToStateKey(address));
            AccountMetrics.GetStateTimer.Value?.Stop();
            return state;
        }

        /// <inheritdoc cref="IAccountState.GetStates(IReadOnlyList{Address})"/>
        [Pure]
        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses)
        {
            AccountMetrics.GetStateTimer.Value?.Start();
            int length = addresses.Count;
            AccountMetrics.GetStateCount.Value += length;
            var values = addresses.Select(address => GetState(address)).ToArray();
            AccountMetrics.GetStateTimer.Value?.Stop();
            return values;
        }

        /// <inheritdoc/>
        [Pure]
        public IAccount SetState(Address address, IValue state) => UpdateState(address, state);

        /// <inheritdoc/>
        [Pure]
        public FungibleAssetValue GetBalance(Address address, Currency currency)
        {
             return Trie.Get(KeyConverters.ToFungibleAssetKey(address, currency)) is Integer i
                 ? FungibleAssetValue.FromRawValue(currency, i)
                 : currency * 0;
        }

        /// <inheritdoc/>
        [Pure]
        public FungibleAssetValue GetTotalSupply(Currency currency)
        {
            if (!currency.TotalSupplyTrackable)
            {
                throw TotalSupplyNotTrackableException.WithDefaultMessage(currency);
            }

            return Trie.Get(KeyConverters.ToTotalSupplyKey(currency)) is Integer i
                ? FungibleAssetValue.FromRawValue(currency, i)
                : currency * 0;
        }

        /// <inheritdoc/>
        [Pure]
        public ValidatorSet GetValidatorSet() =>
            Trie.Get(KeyConverters.ValidatorSetKey) is List list
                 ? new ValidatorSet(list)
                 : new ValidatorSet();

        /// <inheritdoc/>
        [Pure]
        public IAccount MintAsset(
            IActionContext context, Address recipient, FungibleAssetValue value)
        {
            if (value.Sign <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "The value to mint has to be greater than zero."
                );
            }

            Currency currency = value.Currency;
            if (!currency.AllowsToMint(context.Signer))
            {
                throw new CurrencyPermissionException(
                    $"The account {context.Signer} has no permission to mint currency {currency}.",
                    context.Signer,
                    currency
                );
            }

            FungibleAssetValue balance = GetBalance(recipient, currency);
            BigInteger rawBalance = (balance + value).RawValue;

            if (currency.TotalSupplyTrackable)
            {
                var currentTotalSupply = GetTotalSupply(currency);
                if (currency.MaximumSupply < currentTotalSupply + value)
                {
                    var msg = $"The amount {value} attempted to be minted added to the current"
                              + $" total supply of {currentTotalSupply} exceeds the"
                              + $" maximum allowed supply of {currency.MaximumSupply}.";
                    throw new SupplyOverflowException(msg, value);
                }

                return UpdateFungibleAssets(
                    recipient,
                    currency,
                    rawBalance,
                    (currentTotalSupply + value).RawValue);
            }
            else
            {
                return UpdateFungibleAssets(recipient, currency, rawBalance);
            }
        }

        /// <inheritdoc/>
        [Pure]
        public IAccount TransferAsset(
            IActionContext context,
            Address sender,
            Address recipient,
            FungibleAssetValue value,
            bool allowNegativeBalance = false) => context.BlockProtocolVersion > 0
                ? TransferAssetV1(sender, recipient, value, allowNegativeBalance)
                : TransferAssetV0(sender, recipient, value, allowNegativeBalance);

        /// <inheritdoc/>
        [Pure]
        public IAccount BurnAsset(
            IActionContext context, Address owner, FungibleAssetValue value)
        {
            string msg;

            if (value.Sign <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "The value to burn has to be greater than zero."
                );
            }

            Currency currency = value.Currency;
            if (!currency.AllowsToMint(context.Signer))
            {
                msg = $"The account {context.Signer} has no permission to burn assets of " +
                      $"the currency {currency}.";
                throw new CurrencyPermissionException(msg, context.Signer, currency);
            }

            FungibleAssetValue balance = GetBalance(owner, currency);

            if (balance < value)
            {
                msg = $"The account {owner}'s balance of {currency} is insufficient to burn: " +
                      $"{balance} < {value}.";
                throw new InsufficientBalanceException(msg, owner, balance);
            }

            BigInteger rawBalance = (balance - value).RawValue;
            if (currency.TotalSupplyTrackable)
            {
                var currentTotalSupply = GetTotalSupply(currency);
                return UpdateFungibleAssets(
                    owner,
                    currency,
                    rawBalance,
                    (currentTotalSupply - value).RawValue);
            }
            else
            {
                return UpdateFungibleAssets(owner, currency, rawBalance);
            }
        }

        /// <inheritdoc/>
        [Pure]
        public IAccount SetValidator(Validator validator) =>
            UpdateValidatorSet(GetValidatorSet().Update(validator));

        /// <summary>
        /// Creates a null account while inheriting <paramref name="account"/>s
        /// total updated fungibles.
        /// </summary>
        /// <param name="account">The previous <see cref="IAccount"/> to use.</param>
        /// <returns>A null account that is of the same type as <paramref name="account"/>.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if given <paramref name="account"/>
        /// is not <see cref="Account"/>.
        /// </exception>
        /// <remarks>
        /// This inherits <paramref name="account"/>'s
        /// <see cref="IAccount.TotalUpdatedFungibleAssets"/>.
        /// </remarks>
        internal static IAccount Flush(IAccount account) =>
            account is Account impl
                ? new Account(impl.Trie, AccountDelta.Empty, impl.TotalUpdatedFungibles)
                : throw new ArgumentException(
                    $"Unknown type for {nameof(account)}: {account.GetType()}");

        [Pure]
        private Account UpdateState(
            Address address,
            IValue value) =>
            new Account(
                Trie.Set(KeyConverters.ToStateKey(address), value),
                AccountDelta.Empty,
                TotalUpdatedFungibles);

        [Pure]
        private Account UpdateFungibleAssets(
            Address address,
            Currency currency,
            BigInteger amount,
            BigInteger? supplyAmount = null) => supplyAmount is { } sa
            ? new Account(
                Trie
                    .Set(KeyConverters.ToFungibleAssetKey(address, currency), new Integer(amount))
                    .Set(KeyConverters.ToTotalSupplyKey(currency), new Integer(sa)),
                AccountDelta.Empty,
                TotalUpdatedFungibles.SetItem((address, currency), amount))
            : new Account(
                Trie
                    .Set(KeyConverters.ToFungibleAssetKey(address, currency), new Integer(amount)),
                AccountDelta.Empty,
                TotalUpdatedFungibles.SetItem((address, currency), amount));

        [Pure]
        private Account UpdateValidatorSet(ValidatorSet validatorSet) =>
            new Account(
                Trie.Set(KeyConverters.ValidatorSetKey, validatorSet.Bencoded),
                AccountDelta.Empty,
                TotalUpdatedFungibles);

        [Pure]
        private IAccount TransferAssetV0(
            Address sender,
            Address recipient,
            FungibleAssetValue value,
            bool allowNegativeBalance = false)
        {
            if (value.Sign <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "The value to transfer has to be greater than zero."
                );
            }

            Currency currency = value.Currency;
            FungibleAssetValue senderBalance = GetBalance(sender, currency);
            FungibleAssetValue recipientBalance = GetBalance(recipient, currency);

            if (!allowNegativeBalance && senderBalance < value)
            {
                var msg = $"The account {sender}'s balance of {currency} is insufficient to " +
                          $"transfer: {senderBalance} < {value}.";
                throw new InsufficientBalanceException(msg, sender, senderBalance);
            }

            return UpdateFungibleAssets(sender, currency, (senderBalance - value).RawValue)
                .UpdateFungibleAssets(recipient, currency, (recipientBalance + value).RawValue);
        }

        [Pure]
        private IAccount TransferAssetV1(
            Address sender,
            Address recipient,
            FungibleAssetValue value,
            bool allowNegativeBalance = false)
        {
            if (value.Sign <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "The value to transfer has to be greater than zero."
                );
            }

            Currency currency = value.Currency;
            FungibleAssetValue senderBalance = GetBalance(sender, currency);

            if (!allowNegativeBalance && senderBalance < value)
            {
                var msg = $"The account {sender}'s balance of {currency} is insufficient to " +
                          $"transfer: {senderBalance} < {value}.";
                throw new InsufficientBalanceException(msg, sender, senderBalance);
            }

            BigInteger senderRawBalance = (senderBalance - value).RawValue;
            Account intermediate = UpdateFungibleAssets(sender, currency, senderRawBalance);
            FungibleAssetValue recipientBalance = intermediate.GetBalance(recipient, currency);
            BigInteger recipientRawBalance = (recipientBalance + value).RawValue;

            return intermediate.UpdateFungibleAssets(recipient, currency, recipientRawBalance);
        }
    }
}

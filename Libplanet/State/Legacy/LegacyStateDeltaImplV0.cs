using System;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Numerics;
using Bencodex.Types;
using Libplanet.Assets;

namespace Libplanet.State.Legacy
{
    /// <summary>
    /// Equivalent to <see cref="LegacyStateDeltaImpl"/> except that it maintains its old (v0)
    /// incorrect behavior of <see cref="TransferAsset"/>.
    /// </summary>
    [Pure]
    internal class LegacyStateDeltaImplV0 : LegacyStateDeltaImpl
    {
        internal LegacyStateDeltaImplV0(
            AccountStateGetter accountStateGetter,
            AccountBalanceGetter accountBalanceGetter,
            TotalSupplyGetter totalSupplyGetter,
            ValidatorSetGetter validatorSetGetter,
            Address signer
        )
            : base(
                accountStateGetter,
                accountBalanceGetter,
                totalSupplyGetter,
                validatorSetGetter,
                signer)
        {
        }

        /// <inheritdoc/>
        public override FungibleAssetValue GetBalance(Address address, Currency currency) =>
            UpdatedFungibles.TryGetValue((address, currency), out BigInteger balance)
                ? FungibleAssetValue.FromRawValue(currency, balance)
                : BalanceGetter(address, currency);

        /// <inheritdoc/>
        [Pure]
        public override ILegacyStateDelta TransferAsset(
            Address sender,
            Address recipient,
            FungibleAssetValue value,
            bool allowNegativeBalance = false
        )
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

            return UpdateFungibleAssets(
                UpdatedFungibles
                    .SetItem((sender, currency), (senderBalance - value).RawValue)
                    .SetItem((recipient, currency), (recipientBalance + value).RawValue)
            );
        }

        [Pure]
        protected override LegacyStateDeltaImpl UpdateStates(
            IImmutableDictionary<Address, IValue> updatedStates
        ) =>
            new LegacyStateDeltaImplV0(
                StateGetter,
                BalanceGetter,
                TotalSupplyGetter,
                ValidatorSetGetter,
                Signer)
            {
                UpdatedStates = updatedStates,
                UpdatedFungibles = UpdatedFungibles,
            };

        [Pure]
        protected override LegacyStateDeltaImpl UpdateFungibleAssets(
            IImmutableDictionary<(Address, Currency), BigInteger> updatedFungibleAssets
        ) =>
            new LegacyStateDeltaImplV0(
                StateGetter,
                BalanceGetter,
                TotalSupplyGetter,
                ValidatorSetGetter,
                Signer)
            {
                UpdatedStates = UpdatedStates,
                UpdatedFungibles = updatedFungibleAssets,
            };
    }
}

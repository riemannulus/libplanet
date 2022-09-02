using System.Numerics;
using Bencodex.Types;
using Libplanet.Assets;
using Libplanet.Crypto;

namespace Libplanet.PoS
{
    public class Validator
    {
        private FungibleAssetValue _delegatorShares;

        public Validator(Address operatorAddress, PublicKey operatorPublicKey)
        {
            Address = DeriveAddress(operatorAddress);
            OperatorAddress = operatorAddress;
            OperatorPublicKey = operatorPublicKey;
            Jailed = false;
            Status = BondingStatus.Unbonded;
            UnbondingCompletionBlockHeight = -1;
            DelegatorShares = Asset.Share * 0;
        }

        public Validator(IValue serialized)
        {
            List serializedList = (List)serialized;
            Address = serializedList[0].ToAddress();
            OperatorAddress = serializedList[1].ToAddress();
            OperatorPublicKey = serializedList[2].ToPublicKey();
            Jailed = serializedList[3].ToBoolean();
            Status = serializedList[4].ToEnum<BondingStatus>();
            UnbondingCompletionBlockHeight = serializedList[5].ToLong();
            DelegatorShares = serializedList[6].ToFungibleAssetValue();
        }

        public Validator(Validator validator)
        {
            Address = validator.Address;
            OperatorAddress = validator.OperatorAddress;
            OperatorPublicKey = validator.OperatorPublicKey;
            Jailed = validator.Jailed;
            Status = validator.Status;
            UnbondingCompletionBlockHeight = validator.UnbondingCompletionBlockHeight;
            DelegatorShares = validator.DelegatorShares;
        }

        // TODO: Better structure
        // This hard coding will cause some problems when it's modified
        // May be it would be better to be serialized
        public static FungibleAssetValue MinSelfDelegation => Asset.ConsensusToken * 1;

        public static BigInteger CommissionRateNumerator => 1;

        public static BigInteger CommissionRateDenominator => 10;

        public static BigInteger CommissionMaxRateNumerator => 2;

        public static BigInteger CommissionMaxRateDenominator => 10;

        public static BigInteger CommissionMaxChangeRateNumerator => 1;

        public static BigInteger CommissionMaxRateChangeDenominator => 100;

        public Address Address { get; set; }

        public Address OperatorAddress { get; set; }

        public PublicKey OperatorPublicKey { get; set; }

        public bool Jailed { get; set; }

        public BondingStatus Status { get; set; }

        public long UnbondingCompletionBlockHeight { get; set; }

        public FungibleAssetValue DelegatorShares
        {
            get => _delegatorShares;
            set
            {
                if (!value.Currency.Equals(Asset.Share))
                {
                    throw new InvalidCurrencyException(Asset.Share, value.Currency);
                }

                _delegatorShares = value;
            }
        }

        public static Address DeriveAddress(Address operatorAddress)
        {
            return operatorAddress.Derive("ValidatorAddress");
        }

        public bool IsMatured(long blockHeight)
            => UnbondingCompletionBlockHeight > 0
            && Status != BondingStatus.Bonded
            && blockHeight >= UnbondingCompletionBlockHeight;

        public IValue Serialize()
        {
            return List.Empty
                .Add(Address.Serialize())
                .Add(OperatorAddress.Serialize())
                .Add(OperatorPublicKey.Serialize())
                .Add(Jailed.Serialize())
                .Add(Status.Serialize())
                .Add(UnbondingCompletionBlockHeight.Serialize())
                .Add(DelegatorShares.Serialize());
        }
    }
}
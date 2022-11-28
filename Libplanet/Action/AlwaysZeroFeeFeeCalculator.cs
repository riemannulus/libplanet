using Libplanet.Assets;
using Libplanet.Crypto;

namespace Libplanet.Action
{
    internal class AlwaysZeroFeeFeeCalculator : IFeeCalculator
    {
        public FungibleAssetValue CalculateFee(IAction action)
        {
            Currency currency = Currency.Uncapped(
                "ZEROFEE",
                byte.MaxValue,
                new PrivateKey().ToAddress());
            return new FungibleAssetValue(currency, 0);
        }
    }
}

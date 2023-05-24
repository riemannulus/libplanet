using System.Security.Cryptography;
using Bencodex.Types;

namespace Libplanet.State
{
    public interface IStorageDelta
    {
        IAccount Owner { get; }

        HashDigest<SHA256> RootHash { get; }

        IValue? Get(Address account);

        IStorageDelta Set(Address account, IValue value);
    }
}

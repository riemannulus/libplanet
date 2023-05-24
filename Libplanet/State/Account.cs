using System.Security.Cryptography;
using Bencodex.Types;

namespace Libplanet.State
{
    /// <summary>
    /// An implementation of <see cref="IAccount"/> interface.
    /// </summary>
    public class Account : IAccount
    {
        public Account(
            Address id,
            long nonce,
            HashDigest<SHA256> stateRootHash)
        {
            Id = id;
            Nonce = nonce;
            StateRootHash = stateRootHash;
        }

        public Account(Bencodex.Types.List serialized)
        {
            Id = new Address(serialized[0]);
            Nonce = (Integer)serialized[1];
            StateRootHash = new HashDigest<SHA256>((Binary)serialized[2]);
        }

        public long Nonce { get; }

        public Address Id { get; }

        public HashDigest<SHA256> StateRootHash { get; }

        public IAccount ChangeStateRoot(HashDigest<SHA256> stateRootHash)
        {
            return new Account(Id, Nonce, stateRootHash);
        }

        public IValue Serialize()
        {
             var list = Bencodex.Types.List.Empty
                .Add(Id.Bencoded)
                .Add(Nonce)
                .Add(StateRootHash.ByteArray);

             return list;
        }
    }
}

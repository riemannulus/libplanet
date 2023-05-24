using System.Security.Cryptography;

namespace Libplanet.State
{
    /// <summary>
    /// An interface for account.
    /// <see cref="IAccount"/> is a represent of an account in Libplanet.
    /// It contains the account's nonce, address, state root hash, and memo.
    /// </summary>
    public interface IAccount
    {
        /// <summary>
        /// Account's address.
        /// </summary>
        public Address Id { get; }

        /// <summary>
        /// Account's nonce.
        /// </summary>
        public long Nonce { get; }

        /// <summary>
        /// Account's state root hash.
        /// </summary>
        public HashDigest<SHA256> StateRootHash { get; }

        public IAccount ChangeStateRoot(HashDigest<SHA256> stateRootHash);
    }
}

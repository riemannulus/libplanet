using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Store.Trie;

namespace Libplanet.State
{
    internal class StorageDelta : IStorageDelta
    {
        private readonly ITrie _trie;

        public StorageDelta(IAccount owner, ITrie trie)
        {
            Owner = owner;
            _trie = trie;
        }

        public IAccount Owner { get; }

        public HashDigest<SHA256> RootHash => _trie.Hash;

        public IValue? Get(Address account) =>
            _trie.Get(new[] { new KeyBytes(account.ByteArray) })[0];

        public IStorageDelta Set(Address account, IValue value) =>
            new StorageDelta(Owner, _trie.Set(new KeyBytes(account.ByteArray), value).Commit());
    }
}

using System;
using System.Security.Cryptography;
using Bencodex.Types;
using Jitbit.Utils;
using Libplanet.Common;
using Libplanet.Store.Trie;

namespace Libplanet.Store
{
    /// <summary>
    /// A class used for internally caching hashed nodes of <see cref="MerkleTrie"/>s.
    /// </summary>
    public class HashNodeCache
    {
        private FastCache<HashDigest<SHA256>, IValue> _fastCache;

        internal HashNodeCache()
        {
            _fastCache = new FastCache<HashDigest<SHA256>, IValue>();
        }

        public bool TryGetValue(HashDigest<SHA256> hash, out IValue? value)
        {
            if (_fastCache.TryGet(hash, out value))
            {
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public void AddOrUpdate(HashDigest<SHA256> hash, IValue value)
        {
            _fastCache.AddOrUpdate(hash, value, TimeSpan.FromMinutes(1));
        }
    }
}

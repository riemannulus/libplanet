using System.Security.Cryptography;
using Libplanet.Common;
using LruCacheNet;
using Serilog;

namespace Libplanet.Action.State
{
    internal class BlockChainStatesCache
    {
        public const int CacheSize = 8;

        private LruCache<HashDigest<SHA256>, AccountStateCache> _cache;

        public BlockChainStatesCache()
            : this(new LruCache<HashDigest<SHA256>, AccountStateCache>(CacheSize))
        {
        }

        private BlockChainStatesCache(LruCache<HashDigest<SHA256>, AccountStateCache> cache)
        {
            _cache = cache;
        }

        public bool TryGetValue(HashDigest<SHA256> hash, out AccountStateCache value)
        {
            bool result;
            if (_cache.TryGetValue(hash, out value))
            {
                result = true;
            }
            else
            {
                value = new AccountStateCache();
                result = false;
            }

            return result;
        }

        public void AddOrUpdate(HashDigest<SHA256> hash, AccountStateCache value)
        {
            _cache.AddOrUpdate(hash, value);

            Log
                .ForContext("Source", nameof(BlockChainStatesCache))
                .ForContext("Tag", "Metric")
                .ForContext("Subtag", "ChainStatesCacheReport")
                .Debug(
                    "Added {Count} cached values for {Hash}",
                    value.Count,
                    hash);
        }
    }
}

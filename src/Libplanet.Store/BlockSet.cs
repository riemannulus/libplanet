using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Caching;
using Jitbit.Utils;
using Libplanet.Types.Blocks;

namespace Libplanet.Store
{
    public class BlockSet : IReadOnlyDictionary<BlockHash, Block>
    {
        private readonly IStore _store;
        private readonly FastCache<BlockHash, Block> _fastCache;

        public BlockSet(IStore store, int cacheSize = 4096)
        {
            _store = store;
            _fastCache = new FastCache<BlockHash, Block>();
        }

        public IEnumerable<BlockHash> Keys =>
            _store.IterateBlockHashes().ToList();

        public IEnumerable<Block> Values =>
            _store.IterateBlockHashes()
                .Select(GetBlock)
                .Where(block => block is { })
                .Select(block => block!)
                .ToList();

        public int Count => (int)_store.CountBlocks();

        public bool IsReadOnly => false;

        public Block this[BlockHash key]
        {
            get
            {
                Block? block = GetBlock(key);
                if (block is null)
                {
                    throw new KeyNotFoundException(
                        $"The given hash[{key}] was not found in this set."
                    );
                }

                if (block.ProtocolVersion < BlockMetadata.PBFTProtocolVersion)
                {
                    // Skip verifying BlockHash of PoW blocks due to change of the block structure.
                    // If verification is required, use older version of LibPlanet(<0.43).
                }
                else if (!block.Hash.Equals(key))
                {
                    throw new InvalidBlockHashException(
                        $"The given hash[{key}] was not equal to actual[{block.Hash}].");
                }

                return block;
            }

            set
            {
                if (!value.Hash.Equals(key))
                {
                    throw new InvalidBlockHashException(
                        $"{value}.hash does not match to {key}");
                }

                value.ValidateTimestamp();
                _store.PutBlock(value);
                _fastCache.AddOrUpdate(value.Hash, value, TimeSpan.FromMinutes(1));
            }
        }

        public bool Contains(KeyValuePair<BlockHash, Block> item) =>
            _store.ContainsBlock(item.Key);

        public bool ContainsKey(BlockHash key) =>
            _store.ContainsBlock(key);

        public bool Remove(BlockHash key)
        {
            bool deleted = _store.DeleteBlock(key);

            _fastCache.Remove(key);

            return deleted;
        }

        public void Add(BlockHash key, Block value)
        {
            this[key] = value;
        }

        public bool TryGetValue(BlockHash key, out Block value)
        {
            try
            {
                value = this[key];
                return true;
            }
            catch (KeyNotFoundException)
            {
                value = default!;
                return false;
            }
        }

        public void Add(KeyValuePair<BlockHash, Block> item) => Add(item.Key, item.Value);

        public void Clear()
        {
            foreach (BlockHash key in Keys)
            {
                Remove(key);
            }
        }

        public void CopyTo(KeyValuePair<BlockHash, Block>[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (arrayIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            }

            if (Count > array.Length + arrayIndex)
            {
                throw new ArgumentException();
            }

            foreach (KeyValuePair<BlockHash, Block> kv in this)
            {
                array[arrayIndex++] = kv;
            }
        }

        public bool Remove(KeyValuePair<BlockHash, Block> item) => Remove(item.Key);

        public IEnumerator<KeyValuePair<BlockHash, Block>> GetEnumerator()
        {
            foreach (var key in Keys)
            {
                yield return new KeyValuePair<BlockHash, Block>(key, this[key]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private Block? GetBlock(BlockHash key)
        {
            if (_fastCache.TryGet(key, out Block cached))
            {
                return cached;
            }

            Block? fetched = _store.GetBlock(key);
            if (fetched is { })
            {
                _fastCache.AddOrUpdate(key, fetched, TimeSpan.FromMinutes(1));
            }

            return fetched;
        }
    }
}

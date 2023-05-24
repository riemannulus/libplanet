using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using Libplanet.Blockchain;
using Libplanet.State.Legacy;
using Libplanet.Store.Trie;

namespace Libplanet.State
{
    internal class AccountStateDeltaImpl : IAccountStateDelta
    {
        private readonly IBlockChainStates _blockChainStates;
        private readonly ILegacyStateDelta _legacyStateDelta;
        private readonly ITrie _accountTrie;
        private readonly IImmutableDictionary<IAccount, IStorageDelta> _storageDeltas;

        public AccountStateDeltaImpl(
            IBlockChainStates blockContent,
            ILegacyStateDelta legacyStateDelta,
            ITrie accountTrie,
            IImmutableDictionary<IAccount, IStorageDelta>? storageDeltas = null)
        {
            _blockChainStates = blockContent;
            _legacyStateDelta = legacyStateDelta;
            _accountTrie = accountTrie;
            _storageDeltas = storageDeltas
                ?? new Dictionary<IAccount, IStorageDelta>().ToImmutableDictionary();
        }

        public HashDigest<SHA256> RootHash => _accountTrie.Hash;

        public IReadOnlyList<HashDigest<SHA256>> UpdatedStorageRootHashes =>
            _storageDeltas
                .Select(x => x.Value)
                .Select(x => x.RootHash)
                .ToList();

        public ILegacyStateDelta GetLegacy() => _legacyStateDelta;

        public IAccountStateDelta SetLegacy(ILegacyStateDelta legacyStateDelta) =>
            new AccountStateDeltaImpl(_blockChainStates, legacyStateDelta, _accountTrie);

        public IStorageDelta GetStorage(IAccount account) =>
            _storageDeltas.TryGetValue(account, out var storageDelta)
                ? storageDelta
                : new StorageDelta(account, _blockChainStates.GetTrie(account.StateRootHash));

        public IAccountStateDelta SetStorage(IStorageDelta storageDelta)
        {
            IAccount nextAccount = storageDelta.Owner.ChangeStateRoot(storageDelta.RootHash);
            return new AccountStateDeltaImpl(
                _blockChainStates,
                _legacyStateDelta,
                _accountTrie.Set(new KeyBytes(nextAccount.Id.ByteArray), nextAccount.Serialize()).Commit(),
                _storageDeltas.SetItem(nextAccount, storageDelta));
        }

        public IAccount GetAccount(Address address) => _blockChainStates.GetAccount();
    }
}

using System.Collections.Immutable;
using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Store.Trie.Nodes;

namespace Libplanet.Tests.Store
{
    public sealed class StateStoreTracker : BaseTracker, IStateStore
    {
        private readonly IStateStore _stateStore;
        private bool _disposed = false;

        public StateStoreTracker(IStateStore stateStore)
        {
            _stateStore = stateStore;
        }

        public IUnRecordableTrie GetUnRecordableStateRoot(HashDigest<SHA256>? stateRootHash)
        {
            return _stateStore.GetUnRecordableStateRoot(stateRootHash);
        }

        public ITrie GetStateRoot(HashDigest<SHA256>? stateRootHash)
        {
            Log(nameof(GetStateRoot), stateRootHash);
            return _stateStore.GetStateRoot(stateRootHash);
        }

        public ITrie GetStateRoot(INode rootNode)
        {
            return _stateStore.GetStateRoot(rootNode);
        }

        public void PruneStates(IImmutableSet<HashDigest<SHA256>> survivingStateRootHashes)
        {
            Log(nameof(PruneStates), survivingStateRootHashes);
            _stateStore.PruneStates(survivingStateRootHashes);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _stateStore?.Dispose();
                _disposed = true;
            }
        }
    }
}

using System.Collections.Generic;
using System.Security.Cryptography;
using Libplanet.State.Legacy;

namespace Libplanet.State
{
    public interface IAccountStateDelta
    {
        HashDigest<SHA256> RootHash { get; }

        IReadOnlyList<HashDigest<SHA256>> UpdatedStorageRootHashes { get; }

        public ILegacyStateDelta GetLegacy();

        public IAccountStateDelta SetLegacy(ILegacyStateDelta legacyStateDelta);

        public IStorageDelta GetStorage(IAccount account);

        public IAccountStateDelta SetStorage(IStorageDelta storageDelta);

        public IAccount GetAccount(Address address);
    }
}

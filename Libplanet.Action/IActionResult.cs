using System;
using System.Collections.Immutable;
using System.Numerics;
using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Tx;

namespace Libplanet.Action
{
    public interface IActionResult
    {
        public IValue Action { get; }
        
        public Address Signer { get; }
        
        public TxId? TxId { get; }
        
        public Address Miner { get; }
        
        public long BlockIndex { get; }
        
        public int BlockProtocolVersion { get; }
        
        public HashDigest<SHA256> PreviousRootHash { get; }
        
        public HashDigest<SHA256> OutputRootHash { get; }
        
        public bool BlockAction { get; }
        
        public IImmutableDictionary<(Address, Currency), BigInteger> TotalUpdatedFungibles { get; }

        public Exception? Exception { get; }
    }
}

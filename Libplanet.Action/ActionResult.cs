using System;
using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Action
{
    public class ActionResult : IActionResult
    {
        public ActionResult(
            IValue action,
            Address signer,
            TxId? txId,
            Address miner,
            long blockIndex,
            int blockProtocolVersion,
            HashDigest<SHA256> previousRootHash,
            HashDigest<SHA256> outputRootHash,
            bool blockAction,
            Exception? exception = null)
        {
            Action = action;
            Signer = signer;
            TxId = txId;
            Miner = miner;
            BlockIndex = blockIndex;
            BlockProtocolVersion = blockProtocolVersion;
            PreviousRootHash = previousRootHash;
            OutputRootHash = outputRootHash;
            BlockAction = blockAction;
            Exception = exception;
        }

        public ActionResult(IActionEvaluation evaluation)
            : this(
                evaluation.Action,
                evaluation.InputContext.Signer,
                evaluation.InputContext.TxId,
                evaluation.InputContext.Miner,
                evaluation.InputContext.BlockIndex,
                evaluation.InputContext.BlockProtocolVersion,
                evaluation.InputContext.PreviousState.Trie.Hash,
                evaluation.OutputState.Trie.Hash,
                evaluation.InputContext.BlockAction,
                evaluation.Exception)
        {
        }

        
        public IValue Action
        {
            get;
        }
        public Address Signer
        {
            get;
        }
        public TxId? TxId
        {
            get;
        }
        public Address Miner
        {
            get;
        }
        public long BlockIndex
        {
            get;
        }
        public int BlockProtocolVersion
        {
            get;
        }
        public HashDigest<SHA256> PreviousRootHash
        {
            get;
        }
        public HashDigest<SHA256> OutputRootHash
        {
            get;
        }
        public bool BlockAction
        {
            get;
        }
        public Exception? Exception
        {
            get;
        }
    }
}

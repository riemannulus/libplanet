using System;
using System.Collections.Generic;
using Bencodex;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Net.Messages;
using Serilog;

namespace Libplanet.Net.Consensus
{
    public static class PbftMethod<T>
        where T : IAction, new()
    {
        public static ILogger Logger { get; } = Log
            .ForContext("Tag", "Consensus");

        public static (PbftStruct<T>, ProposeStruct<T>, bool)
            DoseNotPoLCRoundOrPoLCBlockIsSameToTarget(
                (PbftStruct<T> PbftStruct,
                    ProposeStruct<T> ProposeStruct) t)
        {
            var (_, ps) = t;
            return t switch
            {
                (PbftStruct: { LockedRound: -1 }, _) => (t.PbftStruct, t.ProposeStruct, true),
                (PbftStruct: { LockedValue: -1 }, _) => (t.PbftStruct, t.ProposeStruct, true),
                _ => (t.PbftStruct, t.ProposeStruct, false),
            };

        }

        public static (PbftStruct<T>, ProposeStruct<T>, bool)
            IsTheFirstProposedAndStepIsPropose(
                (PbftStruct<T> PbftStruct,
                    ProposeStruct<T> ProposeStruct) t)
        {
            var (pbftStruct, proposeStruct) = t;
            if (proposeStruct.ValidRound == -1 &&
                pbftStruct.Step == PbftStruct<T>.PbftStep.Propose)
            {
                return (pbftStruct, proposeStruct, true);
            }

            return (pbftStruct, proposeStruct, false);
        }

        public static (PbftStruct<T>, ConsensusPropose?) GetProposeFromProposer(
            (
                PbftStruct<T> PbftStruct,
                PublicKey Proposer,
                Dictionary<int, List<ConsensusMessage>> MessagesInRound) t)
        {
            var (pbftStruct, proposer, messagesInRound) = t;
            ConsensusMessage? msg = messagesInRound[pbftStruct.Round].Find(
                msg =>
                    msg is ConsensusPropose { Remote: { } } propose &&
                    propose.Remote.PublicKey.Equals(proposer));
            if (msg is ConsensusPropose propose)
            {
                return (pbftStruct, propose);
            }

            return (pbftStruct, null);
        }

        public static (PbftStruct<T>, ProposeStruct<T>) GetProposeStructFromProposeMessage(
            (
                PbftStruct<T> PbftStruct,
                ConsensusPropose Propose,
                Codec Codec,
                HashAlgorithmGetter GetHashAlgorithm) t)
        {
            var (pbftStruct, propose, codec, getHashAlgorithm) = t;
            var block = BlockMarshaler.UnmarshalBlock<T>(
                getHashAlgorithm,
                (Dictionary)codec.Decode(propose.Payload));
            var proposeStruct = new ProposeStruct<T>
            {
                TargetBlock = block,
                ValidRound = propose.ValidRound,
            };

            return (pbftStruct, proposeStruct);
        }

        public static (PbftStruct<T>, ProposeStruct<T>?) HasProposeFromProposer(
            (
                PbftStruct<T> PbftStruct,
                PublicKey Proposer,
                Codec Codec,
                Dictionary<int, List<ConsensusMessage>> MessagesInRound,
                HashAlgorithmGetter GetHashAlgorithm) t)
        {
            var (pbftStruct, proposer, codec, messagesInRound, getHashAlgorithm) = t;
            return GetProposeFromProposer((pbftStruct, proposer, messagesInRound)) switch
            {
                (_, null) => (pbftStruct, null),
                (var p1, { } p2) =>
                    GetProposeStructFromProposeMessage((
                            p1,
                            p2,
                            codec,
                            getHashAlgorithm)),
            };
        }

        public (PbftStruct<T>, ProposeStruct<T>, bool) BlockIsValid(
            (PbftStruct<T> PbftStruct,
            ProposeStruct<T> ProposeStruct,
            Func<Block<T>, bool> ) t)
        {
            var (pbftStruct, proposeStruct) = t;
            return (pbftStruct, proposeStruct, IsValid(proposeStruct.TargetBlock));
        }
    }
}

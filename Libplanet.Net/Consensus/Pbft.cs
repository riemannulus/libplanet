using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Consensus;
using Libplanet.Crypto;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;

namespace Libplanet.Net
{
    public class Pbft<T>
        where T : IAction, new()
    {
        private long _id;
        private Dictionary<int, List<ConsensusMessage>> _messagesInRound;
        private List<int> _preVoteFlags;
        private List<int> _preCommitFlags;
        private Dictionary<long, Block<T>> _decisions;
        private List<PublicKey> _validators;
        private PbftStruct<T> _pbftStruct;

        private BlockChain<T> _blockChain;
        private Codec _codec;

        public Pbft(BlockChain<T> blockChain, long id, PrivateKey consensusKey, List<PublicKey> validators)
        {
            _id = id;
            _validators = validators;
            _messagesInRound = new Dictionary<int, List<ConsensusMessage>>();
            _preVoteFlags = new List<int>();
            _preCommitFlags = new List<int>();
            _decisions = new Dictionary<long, Block<T>>();
            _blockChain = blockChain;
            _pbftStruct = new PbftStruct<T>(id, 0, 0, consensusKey);
            _codec = new Codec();
        }

        public bool ProcessChecker(
            IEnumerable<
                    Func<
                        (PbftStruct<T>, ProposeStruct<T>), (PbftStruct<T>, ProposeStruct<T>, bool)>>
                checkerList,
            (PbftStruct<T>, ProposeStruct<T>) closer)
        {
            foreach (var func in checkerList)
            {
                var (_, _, result) = func(closer);
                if (!result)
                {
                    return false;
                }
            }

            return true;
        }

        public PbftStruct<T> HandleMessage(ConsensusMessage message)
        {
            var process = new List<T>()
            {
                GetProposeFirstTime,
                GetProposePreviousRound,
                CollecteTwoThirdPrevote,
            };

            return Fire(process);
        }

        public PbftStruct<T> GetProposeFirstTime(PbftStruct<T> pbftStruct)
        {
            return GetProposedCurrentRoundMessageFromMessageQueue(pbftStruct) switch
            {
                (var p1, { } p2) => HandleProposeFirstTime(p1, p2),
                _ => pbftStruct,
            };
        }

        public PbftStruct<T> GetProposePreviousRound(PbftStruct<T> pbftStruct)
        {
            return GetProposedCurrentRoundMessageFromMessageQueue(pbftStruct) switch
            {
                (var p1, { } p2) => HandleProposePreviousRound(p1, p2),
                _ => pbftStruct,
            };
        }

        public PbftStruct<T> HandleProposePreviousRound(
            PbftStruct<T> pbftStruct,
            ProposeStruct<T> proposeStruct)
        {
            var (p1, p2, result) =
                IsTheFirstProposedAndStepIsPropose((pbftStruct, proposeStruct));
            if (!result)
            {
                return p1;
            }

            var process =
                new List<Func<(PbftStruct<T>, ProposeStruct<T>), (PbftStruct<T>, ProposeStruct<T>,
                    bool)>>
                {
                    BlockIsValid,
                };
        }

        public PbftStruct<T> HandleProposeFirstTime(
            PbftStruct<T> pbftStruct,
            ProposeStruct<T> proposeStruct)
        {
            var (p1, p2, result) =
                IsTheFirstProposedAndStepIsPropose((pbftStruct, proposeStruct));
            if (!result)
            {
                return p1;
            }

            var process = new List<Func<
                (PbftStruct<T>, ProposeStruct<T>), (PbftStruct<T>, ProposeStruct<T>, bool)>>()
            {
                BlockIsValid,
                DoseNotPoLCRoundOrPoLCBlockIsSameToTarget,
            };

            if (ProcessChecker(process, (p1, p2)))
            {
                _ = BroadcastMessage(
                    new ConsensusVote(
                        Voting(
                            p1.Height,
                            p1.Round,
                            p2.TargetBlock.Hash,
                            VoteFlag.Absent)));
            }
            else
            {
                _ = BroadcastMessage(
                    new ConsensusVote(
                        Voting(
                            p1.Height,
                            p1.Round,
                            null,
                            VoteFlag.Absent)));
            }

            p1.Step = PbftStruct<T>.PbftStep.Prevote;
            return p1;
        }

        public (PbftStruct<T>, ProposeStruct<T>?) GetProposedCurrentRoundMessageFromMessageQueue(
            PbftStruct<T> pbftStruct)
        {
            return HasProposeFromProposer(Proposer(pbftStruct)) switch
            {
                ({ } h1, { } h2) => (h1, h2),
                _ => (pbftStruct, null),
            };
        }


        public (PbftStruct<T>, ProposeStruct<T>, bool) BlockIsValid(
            (PbftStruct<T> PbftStruct,
            ProposeStruct<T> ProposeStruct) t)
        {
            var (pbftStruct, proposeStruct) = t;
            return (pbftStruct, proposeStruct, IsValid(proposeStruct.TargetBlock));
        }

        public (PbftStruct<T>, ProposeStruct<T>, bool) IsProposePreviousRoundAgain(
            (PbftStruct<T> PbftStruct,
            ProposeStruct<T> ProposeStruct) t)
        {
            var (pbftStruct, proposeStruct) = t;
            return (pbftStruct, proposeStruct,
                pbftStruct.LockedRound == -1 ||
                pbftStruct.LockedValue == proposeStruct.TargetBlock);
        }

        public (PbftStruct<T>, PublicKey) Proposer(PbftStruct<T> pbftStruct)
        {
            // return designated proposer for the height round pair.
            return (
                pbftStruct,
                _validators[(int)((pbftStruct.Height + pbftStruct.Round) % _validators.Count)]);
        }

        private bool IsValid(Block<T> block)
        {
            var exception = _blockChain.ValidateNextBlock(block);
            return exception is null;
        }

        private async Task BroadcastMessage(ConsensusMessage message)
        {
            // broadcast message
            await Task.Yield();
        }

        private Vote Voting(long height, int round, BlockHash? hash, VoteFlag flag)
        {
            return new Vote(
                height,
                round,
                hash,
                DateTimeOffset.Now,
                _pbftStruct.ConsensusKey.PublicKey,
                flag,
                _id,
                null).Sign(_pbftStruct.ConsensusKey);
        }
    }
}

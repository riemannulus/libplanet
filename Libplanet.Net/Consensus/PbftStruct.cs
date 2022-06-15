using Libplanet.Action;
using Libplanet.Blocks;
using Libplanet.Crypto;

namespace Libplanet.Net.Consensus
{
    public struct PbftStruct<T>
        where T : IAction, new()
    {
        public PbftStruct(long id, long height, int round, PrivateKey consensusKey)
        {
            Id = id;
            Height = height;
            Round = round;
            ConsensusKey = consensusKey;
            Step = PbftStep.Default;
            LockedValue = null;
            LockedRound = -1;
            ValidValue = null;
            ValidRound = -1;
        }

        public void Deconstruct(
            out long height,
            out int round,
            out PbftStep step)
        {
            height = Height;
            round = Round;
            step = Step;
        }

        public void Deconstruct(
            Block<T>? lockedValue,
            int lockedRound,
            Block<T>? validValue,
            int validRound)
        {
            lockedValue = LockedValue;
            lockedRound = LockedRound;
            validValue = ValidValue;
            validRound = ValidRound;
        }

        public enum PbftStep
        {
            Default,
            Propose,
            Prevote,
            PreCommit,
        }

        public long Id { get; }

        public long Height { get; }

        public int Round { get; }

        public PbftStep Step { get; set; }

        public Block<T>? LockedValue { get; set; }

        public int LockedRound { get; set; }

        public Block<T>? ValidValue { get; set; }

        public int ValidRound { get; set; }

        public PrivateKey ConsensusKey { get; }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Consensus;
using Libplanet.Crypto;
using Libplanet.Net.Messages;
using Serilog;

namespace Libplanet.Net.Consensus
{
    public class Context<T> : IDisposable
        where T : IAction, new()
    {
        private const int TimeoutProposeBase = 5;
        private const int TimeoutPreVoteBase = 5;
        private const int TimeoutPreCommitBase = 5;
        private const int TimeoutProposeMultiplier = 1;
        private const int TimeoutPreVoteMultiplier = 1;
        private const int TimeoutPreCommitMultiplier = 1;
        private readonly ILogger _logger;

        private long _id;
        private PrivateKey _privateKey;
        private Block<T>? _lockedValue;
        private int _lockedRound;
        private Block<T>? _validValue;
        private int _validRound;
        private ConcurrentDictionary<int, ConcurrentBag<ConsensusMessage>> _messagesInRound;
        private ConcurrentBag<int> _preVoteFlags;
        private ConcurrentBag<int> _hasTwoThirdsPreVoteFlags;
        private ConcurrentBag<int> _preCommitFlags;

        private BlockChain<T> _blockChain;
        private Codec _codec;
        private List<PublicKey> _validators;

        private CancellationTokenSource _cancellationTokenSource;

        public Context(
            ConsensusContext<T> consensusContext,
            BlockChain<T> blockChain,
            long id,
            long height,
            PrivateKey privateKey,
            List<PublicKey> validators)
        {
            _id = id;
            _privateKey = privateKey;
            Height = height;
            Round = 0;
            Step = Step.Default;
            _lockedValue = null;
            _lockedRound = -1;
            _validValue = null;
            _validRound = -1;
            _blockChain = blockChain;
            _codec = new Codec();
            _messagesInRound = new ConcurrentDictionary<int, ConcurrentBag<ConsensusMessage>>();
            _preVoteFlags = new ConcurrentBag<int>();
            _hasTwoThirdsPreVoteFlags = new ConcurrentBag<int>();
            _preCommitFlags = new ConcurrentBag<int>();
            _validators = validators;
            _cancellationTokenSource = new CancellationTokenSource();
            ConsensusContext = consensusContext;

            _logger = Log
                .ForContext("Tag", "Consensus")
                .ForContext("SubTag", "Context")
                .ForContext<Context<T>>()
                .ForContext("Source", nameof(Context<T>));
        }

        public long Height { get; }

        public int Round { get; private set; }

        public Step Step { get; private set; }

        private ConsensusContext<T> ConsensusContext { get; }

        private int TotalValidators => _validators.Count;

        public void Start()
        {
            _ = StartRound(0);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
        }

        public VoteSet VoteSet(int round)
        {
            var (block, _) = GetPropose(round);
            var voteSet = new VoteSet(Height, round, block?.Hash, _validators);
            var roundVotes =
                _messagesInRound[round].Where(
                    x => x is ConsensusVote).Cast<ConsensusVote>().ToList();
            var roundCommits =
                _messagesInRound[round].Where(
                    x => x is ConsensusCommit).Cast<ConsensusCommit>().ToList();

            foreach (var vote in roundVotes)
            {
                voteSet.Add(vote.ProposeVote);
            }

            foreach (var commit in roundCommits)
            {
                voteSet.Add(commit.CommitVote);
            }

            return voteSet;
        }

        public void HandleMessage(ConsensusMessage message)
        {
            try
            {
                AddMessage(message);
            }
            catch (Exception e)
            {
                _logger.Error(e, "AddMessageException: {E}", e);
                throw;
            }

            _logger.Debug(
                "{FName}: Message: {Message} => " +
                "Height: {Height}, Round: {Round}, NodeId: {NodeId}. " +
                "MessageCount: {Count}. (context: {Context})",
                nameof(HandleMessage),
                message,
                message.Height,
                message.Round,
                message.NodeId,
                _messagesInRound[Round].Count,
                ToString());

            if (GetPropose(Round) is
                    (Block<T> block1, int validRound1) &&
                validRound1 == -1 &&
                Step == Step.Propose)
            {
                _logger.Debug(
                    "Entering PreVote step due to proposal message with " +
                    "valid round -1. (context: {Context})",
                    ToString());
                Step = Step.PreVote;

                if (IsValid(block1) && (_lockedRound == -1 || _lockedValue == block1))
                {
                    BroadcastMessage(
                        new ConsensusVote(Voting(Height, Round, block1.Hash, VoteFlag.Absent)));
                }
                else
                {
                    BroadcastMessage(
                        new ConsensusVote(Voting(Height, Round, null, VoteFlag.Absent)));
                }
            }

            if (GetPropose(Round) is
                    (Block<T> block2, int validRound2) &&
                validRound2 >= 0 &&
                validRound2 < Round &&
                HasTwoThirdsPreVote(validRound2, block2.Hash) &&
                Step == Step.Propose)
            {
                _logger.Debug(
                    "Entering PreVote step due to proposal message and have collected " +
                    "2/3+ PreVote for valid round {ValidRound}. (context: {Context})",
                    validRound2,
                    ToString());
                Step = Step.PreVote;

                if (IsValid(block2) && (_lockedRound <= validRound2 || _lockedValue == block2))
                {
                    BroadcastMessage(
                        new ConsensusVote(Voting(Height, Round, block2.Hash, VoteFlag.Absent)));
                }
                else
                {
                    BroadcastMessage(
                        new ConsensusVote(Voting(Height, Round, null, VoteFlag.Absent)));
                }
            }

            if (HasTwoThirdsPreVote(Round, null, true) &&
                Step == Step.PreVote &&
                !_preVoteFlags.Contains(Round))
            {
                _logger.Debug(
                    "PreVote step in round {Round} is scheduled to be timed out because " +
                    "2/3+ PreVotes are collected for the round. (context: {Context})",
                    Round,
                    ToString());
                _preVoteFlags.Add(Round);
                _ = OnTimeoutPreVote(Height, Round);
            }

            if (GetPropose(Round) is (Block<T> block3, _) &&
                HasTwoThirdsPreVote(Round, block3.Hash) &&
                IsValid(block3) &&
                Step >= Step.PreVote &&
                !_hasTwoThirdsPreVoteFlags.Contains(Round))
            {
                _logger.Debug(
                    "2/3+ PreVotes for the current round {Round} have collected. " +
                    "(context: {Context})",
                    Round,
                    ToString());
                _hasTwoThirdsPreVoteFlags.Add(Round);
                if (Step == Step.PreVote)
                {
                    _logger.Debug(
                        "Entering PreCommit step due to proposal message and have collected " +
                        "2/3+ PreVote for current round {Round}. (context: {Context})",
                        Round,
                        ToString());
                    Step = Step.PreCommit;
                    _lockedValue = block3;
                    _lockedRound = Round;
                    BroadcastMessage(
                        new ConsensusCommit(Voting(Height, Round, block3.Hash, VoteFlag.Commit)));
                }

                _validValue = block3;
                _validRound = Round;
            }

            if (HasTwoThirdsPreVote(Round, null) && Step == Step.PreVote)
            {
                _logger.Debug(
                    "PreCommit nil for the round {Round} because 2/3+ PreVotes were collected. " +
                    "(context: {Context})",
                    Round,
                    ToString());
                Step = Step.PreCommit;
                BroadcastMessage(
                    new ConsensusCommit(Voting(Height, Round, null, VoteFlag.Commit)));
            }

            if (HasTwoThirdsPreCommit(Round, null, true) && !_preCommitFlags.Contains(Round))
            {
                _logger.Debug(
                    "PreCommit step in round {Round} is scheduled to be timed out because " +
                    "2/3+ PreCommits are collected for the round. (context: {Context})",
                    Round,
                    ToString());
                _preCommitFlags.Add(Round);
                _ = OnTimeoutPreCommit(Height, Round);
            }

            if (message is ConsensusPropose || message is ConsensusCommit)
            {
                int round = message.Round;
                if (GetPropose(round) is (Block<T> block4, _) &&
                    HasTwoThirdsPreCommit(round, block4.Hash) &&
                    Step != Step.EndCommit &&
                    IsValid(block4))
                {
                    Step = Step.EndCommit;
                    _logger.Debug(
                        "Committed block in round {Round}. (context: {Context})",
                        Round,
                        ToString());

                    ConsensusContext.Commit(block4);
                }
            }

            // FIXME: _messagesInRound should not contain any duplicated messages for this.
            if (message.Round > Round &&
                _messagesInRound[message.Round].Count > TotalValidators / 3)
            {
                _logger.Debug(
                    "1/3+ messages from the round {Round} > current round {CurrentRound}. " +
                    "(context: {Context})",
                    message.Round,
                    Round,
                    ToString());
                _ = StartRound(message.Round);
            }
        }

        public override string ToString()
        {
            var dict = new Dictionary<string, object>
            {
                { "node_id", _id },
                { "number_of_validator", _validators!.Count },
                { "height", Height },
                { "round", Round },
                { "step", Step.ToString() },
                { "locked_value", _lockedValue?.Hash.ToString() ?? string.Empty },
                { "locked_round", _lockedRound },
                { "valid_value", _validValue?.Hash.ToString() ?? string.Empty },
                { "valid_round", _validRound },
            };
            return JsonSerializer.Serialize(dict);
        }

        private async Task<Block<T>> GetValue()
        {
            Block<T> block = await _blockChain.MineBlock(
                _privateKey,
                append: false,
                cancellationToken: _cancellationTokenSource.Token);
            _blockChain.Store.PutBlock(block);
            return block;
        }

        private PublicKey Proposer(int round)
        {
            // return designated proposer for the height round pair.
            return _validators[(int)((Height + round) % TotalValidators)];
        }

        private async Task StartRound(int round)
        {
            _logger.Debug(
                "Starting round {NewRound} (was {PrevRound}). (context: {Context})",
                round,
                Round,
                ToString());
            Round = round;
            Step = Step.Propose;
            if (Proposer(Round) == _privateKey.PublicKey)
            {
                _logger.Debug(
                    "Starting round {NewRound} and is a proposer. (context: {Context})",
                    round,
                    ToString());
                Block<T> proposal;
                if (_validValue is null)
                {
                    proposal = await GetValue();
                }
                else
                {
                    proposal = _validValue;
                }

                BroadcastMessage(
                    new ConsensusPropose(
                        _id,
                        Height,
                        Round,
                        proposal.Hash,
                        _codec.Encode(proposal.MarshalBlock()),
                        _validRound));
            }
            else
            {
                _logger.Debug(
                    "Starting round {NewRound} and is not a proposer. (context: {Context})",
                    round,
                    ToString());
                _ = OnTimeoutPropose(Height, Round);
            }
        }

        private void AddMessage(ConsensusMessage message)
        {
            if (message.Height != Height)
            {
                throw new InvalidHeightMessageException(
                    "Height of message differs with working height.  " +
                    $"(expected: {Height}, actual: {message.Height})",
                    message);
            }

            if (message is ConsensusPropose propose)
            {
                if (!propose.Remote!.PublicKey.Equals(Proposer(message.Round)))
                {
                    throw new InvalidProposerProposeMessageException(
                        "Proposer for the height " +
                        $"{message.Height} and round {message.Round} is invalid.  " +
                        $"(expected: {Proposer(message.Round)}, " +
                        $"actual: {propose.Remote!.PublicKey})",
                        message);
                }

                if (message.BlockHash.Equals(default(BlockHash)))
                {
                    throw new InvalidBlockProposeMessageException(
                        "Cannot propose a null block.",
                        message);
                }
            }

            if (!_messagesInRound.ContainsKey(message.Round))
            {
                _messagesInRound.TryAdd(message.Round, new ConcurrentBag<ConsensusMessage>());
            }

            // TODO: Prevent duplicated messages adding.
            _messagesInRound[message.Round].Add(message);
        }

        private void BroadcastMessage(ConsensusMessage message)
        {
            ConsensusContext.BroadcastMessage(message);
        }

        private bool IsValid(Block<T> block)
        {
            /*
            var exception = _blockChain.ValidateNextBlock(block)
            return exception is null
            */
            return true;
        }

        private Vote Voting(long height, int round, BlockHash? hash, VoteFlag flag)
        {
            return new Vote(
                height,
                round,
                hash,
                DateTimeOffset.Now,
                _privateKey.PublicKey,
                flag,
                _id,
                null).Sign(_privateKey);
        }

        // Predicates
        private (Block<T>?, int?) GetPropose(int round)
        {
            ConsensusMessage? msg = _messagesInRound[round].FirstOrDefault(
                msg =>
                    msg is ConsensusPropose);

            if (msg is ConsensusPropose propose)
            {
                var block = BlockMarshaler.UnmarshalBlock<T>(
                    _blockChain.Policy.GetHashAlgorithm,
                    (Dictionary)_codec.Decode(propose.Payload));
                return (block, propose.ValidRound);
            }

            return (null, null);
        }

        private bool HasTwoThirdsPreVote(int round, BlockHash? hash, bool any = false)
        {
            int count = _messagesInRound[round].Count(
                msg => msg is ConsensusVote preVote &&
                       (any || preVote.BlockHash.Equals(hash)));
            return count > TotalValidators * 2 / 3;
        }

        private bool HasTwoThirdsPreCommit(int round, BlockHash? hash, bool any = false)
        {
            int count = _messagesInRound[round].Count(
                msg => msg is ConsensusCommit preCommit &&
                       (any || preCommit.BlockHash.Equals(hash)));
            return count > TotalValidators * 2 / 3;
        }

        // Timeouts
        private async Task OnTimeoutPropose(long height, int round)
        {
            TimeSpan timeout = TimeoutPropose(round);
            await Task.Delay(timeout, _cancellationTokenSource.Token);
            if (height == Height && round == Round && Step == Step.Propose)
            {
                _logger.Debug(
                    "TimeoutPropose has occurred in {Timeout}. {Info}",
                    timeout,
                    ToString());
                BroadcastMessage(
                    new ConsensusVote(Voting(Height, Round, null, VoteFlag.Absent)));
                Step = Step.PreVote;
            }
        }

        private async Task OnTimeoutPreVote(long height, int round)
        {
            TimeSpan timeout = TimeoutPreVote(round);
            await Task.Delay(timeout, _cancellationTokenSource.Token);
            if (height == Height && round == Round && Step == Step.PreVote)
            {
                _logger.Debug(
                    "TimeoutPreVote has occurred in {Timeout}. {Info}",
                    timeout,
                    ToString());
                BroadcastMessage(
                    new ConsensusCommit(Voting(Height, Round, null, VoteFlag.Commit)));
                Step = Step.PreCommit;
            }
        }

        private async Task OnTimeoutPreCommit(long height, int round)
        {
            TimeSpan timeout = TimeoutPreCommit(round);
            await Task.Delay(timeout, _cancellationTokenSource.Token);
            if (height == Height && round == Round)
            {
                _logger.Debug(
                    "TimeoutPreCommit has occurred in {Timeout}. {Info}",
                    timeout,
                    ToString());
                await StartRound(Round + 1);
            }
        }

        private TimeSpan TimeoutPropose(long round)
        {
            return TimeSpan.FromSeconds(TimeoutProposeBase + round * TimeoutProposeMultiplier);
        }

        private TimeSpan TimeoutPreVote(long round)
        {
            return TimeSpan.FromSeconds(TimeoutPreVoteBase + round + TimeoutPreVoteMultiplier);
        }

        private TimeSpan TimeoutPreCommit(long round)
        {
            return TimeSpan.FromSeconds(TimeoutPreCommitBase + round + TimeoutPreCommitMultiplier);
        }
    }
}

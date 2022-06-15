using Libplanet.Action;
using Libplanet.Blocks;

namespace Libplanet.Net.Consensus
{
    public struct ProposeStruct<T>
        where T : IAction, new()
    {
        public Block<T> TargetBlock { get; set; }

        public int ValidRound { get; set; }
    }
}

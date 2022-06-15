using Libplanet.Action;

namespace Libplanet.Net.Consensus.Type
{
    public class PbftPredicate<T> : PbftType<T>
        where T : IAction, new()
    {
        public PbftPredicate(
            PbftStruct<T> pbftStruct,
            ProposeStruct<T> proposeStruct)
        : base(pbftStruct, proposeStruct)
        {
        }
    }
}

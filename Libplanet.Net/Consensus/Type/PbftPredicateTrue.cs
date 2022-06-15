using Libplanet.Action;

namespace Libplanet.Net.Consensus.Type
{
    public class PbftPredicateTrue<T> : PbftType<T>
        where T : IAction, new()
    {
        public PbftPredicateTrue(
            PbftStruct<T> pbftStruct,
            ProposeStruct<T> proposeStruct)
        : base(pbftStruct, proposeStruct)
        {
        }
    }
}

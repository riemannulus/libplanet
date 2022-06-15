using Libplanet.Action;

namespace Libplanet.Net.Consensus.Type
{
    public class PbftPredicateFalse<T> : PbftType<T>
        where T : IAction, new()
    {
        public PbftPredicateFalse(
            PbftStruct<T> pbftStruct,
            ProposeStruct<T> proposeStruct)
        : base(pbftStruct, proposeStruct)
        {
        }

        public Exception Reason
    }
}

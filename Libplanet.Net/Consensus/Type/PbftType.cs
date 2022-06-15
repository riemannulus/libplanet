using Libplanet.Action;
using Serilog;

namespace Libplanet.Net.Consensus.Type
{
    public class PbftType<T>
        where T : IAction, new()
    {
        public PbftType(
            PbftStruct<T> pbftStruct,
            ProposeStruct<T> proposeStruct)
        {
            PbftStruct = pbftStruct;
            ProposeStruct = proposeStruct;
        }

        public PbftStruct<T> PbftStruct { get; set; }

        public ProposeStruct<T> ProposeStruct { get; set; }

        public virtual void Deconstruct(
            out PbftStruct<T> pbftStruct,
            out ProposeStruct<T> proposeStruct)
        {
            pbftStruct = PbftStruct;
            proposeStruct = ProposeStruct;
        }
    }
}

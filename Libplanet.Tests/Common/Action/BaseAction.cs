using Bencodex.Types;
using Libplanet.Action;
using Libplanet.State;

namespace Libplanet.Tests.Common.Action
{
    public abstract class BaseAction : IAction
    {
        public abstract IValue PlainValue { get; }

        public abstract IAccountStateDelta Execute(IActionContext context);

        public abstract void LoadPlainValue(IValue plainValue);
    }
}

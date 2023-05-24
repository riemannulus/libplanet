using System;
using Libplanet.Consensus;

namespace Libplanet.State.Legacy
{
    public static class ValidatorStateExtensions
    {
        public static ValidatorSet GetValidatorSet(this ILegacyStateDelta delta)
        {
            if (delta is IValidatorSupportStateDelta impl)
            {
                return impl.GetValidatorSet();
            }

            throw new NotSupportedException();
        }

        /// <summary>
        /// Sets <paramref name="validator"/> to the stored <see cref="ValidatorSet"/>.
        /// If 0 is given as its power, removes the validator from the <see cref="ValidatorSet"/>.
        /// </summary>
        /// <param name="delta">The target <see cref="ILegacyStateDelta"/> instance.</param>
        /// <param name="validator">The <see cref="Validator"/> instance to write.</param>
        /// <returns>A new <see cref="ILegacyStateDelta"/> instance with
        /// <paramref name="validator"/> set.</returns>
        public static ILegacyStateDelta SetValidator(
            this ILegacyStateDelta delta,
            Validator validator)
        {
            if (delta is IValidatorSupportStateDelta impl)
            {
                return impl.SetValidator(validator);
            }

            throw new NotSupportedException();
        }
    }
}

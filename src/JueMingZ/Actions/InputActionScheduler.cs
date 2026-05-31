using System.Collections.Generic;

namespace JueMingZ.Actions
{
    public static class InputActionScheduler
    {
        public static InputActionRequest SelectNext(IEnumerable<InputActionRequest> pending)
        {
            if (pending == null)
            {
                return null;
            }

            InputActionRequest best = null;
            foreach (var request in pending)
            {
                if (request == null)
                {
                    continue;
                }

                if (best == null ||
                    request.Priority > best.Priority ||
                    (request.Priority == best.Priority && request.CreatedUtc < best.CreatedUtc))
                {
                    best = request;
                }
            }

            return best;
        }

        public static int ComparePriorityThenCreated(InputActionRequest left, InputActionRequest right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            if (left.Priority != right.Priority)
            {
                return right.Priority.CompareTo(left.Priority);
            }

            return left.CreatedUtc.CompareTo(right.CreatedUtc);
        }
    }
}

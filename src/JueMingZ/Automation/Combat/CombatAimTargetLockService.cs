namespace JueMingZ.Automation.Combat
{
    public static class CombatAimTargetLockService
    {
        private const int HoldTicksAfterDisqualified = 18;
        private static readonly object SyncRoot = new object();
        private static int _lockedTargetId = -1;
        private static int _lockedTargetType;
        private static int _lockAgeTicks;
        private static int _holdTicksRemaining;

        public static int LockedTargetId
        {
            get { lock (SyncRoot) { return _lockedTargetId; } }
        }

        public static int LockedTargetType
        {
            get { lock (SyncRoot) { return _lockedTargetType; } }
        }

        public static int LockAgeTicks
        {
            get { lock (SyncRoot) { return _lockAgeTicks; } }
        }

        public static int HoldTicksRemaining
        {
            get { lock (SyncRoot) { return _holdTicksRemaining; } }
        }

        public static bool IsLockedTarget(int whoAmI)
        {
            return IsLockedTarget(whoAmI, 0, false);
        }

        public static bool IsLockedTarget(int whoAmI, int type)
        {
            return IsLockedTarget(whoAmI, type, true);
        }

        private static bool IsLockedTarget(int whoAmI, int type, bool requireType)
        {
            lock (SyncRoot)
            {
                return _lockedTargetId >= 0 &&
                       _lockedTargetId == whoAmI &&
                       (!requireType || _lockedTargetType == type) &&
                       _holdTicksRemaining > 0;
            }
        }

        public static void MarkAttackQualified(CombatTargetSnapshot target)
        {
            if (target == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_lockedTargetId == target.WhoAmI && _lockedTargetType == target.Type)
                {
                    _lockAgeTicks++;
                }
                else
                {
                    _lockedTargetId = target.WhoAmI;
                    _lockedTargetType = target.Type;
                    _lockAgeTicks = 0;
                }

                _holdTicksRemaining = HoldTicksAfterDisqualified;
            }
        }

        public static void MarkAttackDisqualified()
        {
            lock (SyncRoot)
            {
                if (_lockedTargetId < 0)
                {
                    return;
                }

                if (_holdTicksRemaining > 0)
                {
                    _holdTicksRemaining--;
                }

                if (_holdTicksRemaining <= 0)
                {
                    _lockedTargetId = -1;
                    _lockedTargetType = 0;
                    _lockAgeTicks = 0;
                    _holdTicksRemaining = 0;
                }
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _lockedTargetId = -1;
                _lockedTargetType = 0;
                _lockAgeTicks = 0;
                _holdTicksRemaining = 0;
            }
        }
    }
}

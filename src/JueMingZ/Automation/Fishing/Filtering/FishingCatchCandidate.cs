using System.Globalization;

namespace JueMingZ.Automation.Fishing.Filtering
{
    // Candidates are filter/display identities, not proof that a catch item was moved or stored.
    internal sealed class FishingCatchCandidate
    {
        public string Kind { get; set; }

        public int Id { get; set; }

        public string DisplayName { get; set; }

        public string DisplayNameSnapshot { get; set; }

        public bool IsCrate { get; set; }

        public bool IsQuestFish { get; set; }

        public bool IsEnemy { get; set; }

        public string Key
        {
            get
            {
                return (string.IsNullOrWhiteSpace(Kind) ? FishingCatchKinds.Unknown : Kind) + ":" +
                       Id.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}

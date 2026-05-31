using System;
using JueMingZ.Config;

namespace JueMingZ.Automation.Movement
{
    public static class MovementSafeLandingOptionCatalog
    {
        public const string DoubleJump = "double_jump";
        public const string RocketBoots = "rocket_boots";
        public const string FlyingCarpet = "flying_carpet";
        public const string Wings = "wings";
        public const string Horseshoe = "horseshoe";
        public const string Umbrella = "umbrella";
        public const string Grapple = "grapple";
        public const string FlyingMount = "flying_mount";
        public const string FairyBoots = "fairy_boots";
        public const string DamageReductionMount = "damage_reduction_mount";
        public const string TeleportRod = "teleport_rod";
        public const string GravityGlobe = "gravity_globe";

        public static readonly MovementSafeLandingOptionDefinition[] Options =
        {
            new MovementSafeLandingOptionDefinition { Id = DoubleJump, Label = "二段跳", DefaultEnabled = true },
            new MovementSafeLandingOptionDefinition { Id = RocketBoots, Label = "火箭靴", DefaultEnabled = true },
            new MovementSafeLandingOptionDefinition { Id = FlyingCarpet, Label = "飞毯", DefaultEnabled = true },
            new MovementSafeLandingOptionDefinition { Id = Wings, Label = "翅膀", DefaultEnabled = true },
            new MovementSafeLandingOptionDefinition { Id = Horseshoe, Label = "马掌", DefaultEnabled = true },
            new MovementSafeLandingOptionDefinition { Id = Umbrella, Label = "雨伞", DefaultEnabled = true },
            new MovementSafeLandingOptionDefinition { Id = Grapple, Label = "抓钩", DefaultEnabled = true, Tooltip = "不包稳定，这抓钩太难了长官" },
            new MovementSafeLandingOptionDefinition { Id = FlyingMount, Label = "飞行坐骑", DefaultEnabled = true },
            new MovementSafeLandingOptionDefinition { Id = FairyBoots, Label = "精灵腿", DefaultEnabled = true },
            new MovementSafeLandingOptionDefinition { Id = DamageReductionMount, Label = "减伤坐骑", DefaultEnabled = true },
            new MovementSafeLandingOptionDefinition { Id = TeleportRod, Label = "传送杖", DefaultEnabled = true },
            new MovementSafeLandingOptionDefinition { Id = GravityGlobe, Label = "重力球", DefaultEnabled = true }
        };

        public static bool IsKnown(string id)
        {
            return Find(id) != null;
        }

        public static MovementSafeLandingOptionDefinition Find(string id)
        {
            for (var index = 0; index < Options.Length; index++)
            {
                if (string.Equals(Options[index].Id, id, StringComparison.Ordinal))
                {
                    return Options[index];
                }
            }

            return null;
        }

        public static bool GetEnabled(AppSettings settings, string id)
        {
            settings = settings ?? AppSettings.CreateDefault();
            switch (id)
            {
                case DoubleJump:
                    return settings.MovementSafeLandingDoubleJumpEnabled;
                case RocketBoots:
                    return settings.MovementSafeLandingRocketBootsEnabled;
                case FlyingCarpet:
                    return settings.MovementSafeLandingFlyingCarpetEnabled;
                case Wings:
                    return settings.MovementSafeLandingWingsEnabled;
                case Horseshoe:
                    return settings.MovementSafeLandingHorseshoeEnabled;
                case Umbrella:
                    return settings.MovementSafeLandingUmbrellaEnabled;
                case Grapple:
                    return settings.MovementSafeLandingGrappleEnabled;
                case FlyingMount:
                    return settings.MovementSafeLandingFlyingMountEnabled;
                case FairyBoots:
                    return settings.MovementSafeLandingFairyBootsEnabled;
                case DamageReductionMount:
                    return settings.MovementSafeLandingDamageReductionMountEnabled;
                case TeleportRod:
                    return settings.MovementSafeLandingTeleportRodEnabled;
                case GravityGlobe:
                    return settings.MovementSafeLandingGravityGlobeEnabled || settings.MovementSafeLandingGravityPotionEnabled;
                default:
                    return false;
            }
        }

        public static bool SetEnabled(AppSettings settings, string id, bool enabled)
        {
            if (settings == null)
            {
                return false;
            }

            switch (id)
            {
                case DoubleJump:
                    settings.MovementSafeLandingDoubleJumpEnabled = enabled;
                    return true;
                case RocketBoots:
                    settings.MovementSafeLandingRocketBootsEnabled = enabled;
                    return true;
                case FlyingCarpet:
                    settings.MovementSafeLandingFlyingCarpetEnabled = enabled;
                    return true;
                case Wings:
                    settings.MovementSafeLandingWingsEnabled = enabled;
                    return true;
                case Horseshoe:
                    settings.MovementSafeLandingHorseshoeEnabled = enabled;
                    return true;
                case Umbrella:
                    settings.MovementSafeLandingUmbrellaEnabled = enabled;
                    return true;
                case Grapple:
                    settings.MovementSafeLandingGrappleEnabled = enabled;
                    return true;
                case FlyingMount:
                    settings.MovementSafeLandingFlyingMountEnabled = enabled;
                    return true;
                case FairyBoots:
                    settings.MovementSafeLandingFairyBootsEnabled = enabled;
                    return true;
                case DamageReductionMount:
                    settings.MovementSafeLandingDamageReductionMountEnabled = enabled;
                    return true;
                case TeleportRod:
                    settings.MovementSafeLandingTeleportRodEnabled = enabled;
                    return true;
                case GravityGlobe:
                    settings.MovementSafeLandingGravityGlobeEnabled = enabled;
                    settings.MovementSafeLandingGravityPotionEnabled = enabled;
                    return true;
                default:
                    return false;
            }
        }

        public static void ApplyDefaultOptions(AppSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            for (var index = 0; index < Options.Length; index++)
            {
                SetEnabled(settings, Options[index].Id, Options[index].DefaultEnabled);
            }

            settings.MovementSafeLandingOptionsDefaultMigrated = true;
        }

        public static string BuildConfigSummary(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var enabled = 0;
            for (var index = 0; index < Options.Length; index++)
            {
                if (GetEnabled(settings, Options[index].Id))
                {
                    enabled++;
                }
            }

            return enabled.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/" +
                   Options.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}

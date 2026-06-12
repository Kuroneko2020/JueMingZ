using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace Terraria
{
    internal static class Main
    {
        public static bool mouseLeft;
        public static bool mouseLeftRelease;
        public static bool mouseRight;
        public static bool mouseRightRelease;
        public static bool mouseInterface;
        public static bool blockMouse;
        public static bool SmartInteractShowingGenuine;
        public static bool SmartInteractShowingFake;
        public static bool SmartCursorShowing;
        public static bool SmartCursorWanted_Mouse;
        public static bool SmartCursorWanted_GamePad;
        public static int SmartInteractNPC = -1;
        public static int SmartInteractProj = -1;
        public static bool mouseText;
        public static object mouseItem;
        public static string hoverItemName;
        public static string hoverItemName2;
        public static object HoverItem;
        public static object hoverItem;
        public static object[,] tile;
        public static object[] projectile = new object[0];
        public static object[] recipe = new object[0];
        public static object FishDropsDB;
        public static int[] anglerQuestItemNetIDs = new int[0];
        public static int anglerQuest;
        public static bool anglerQuestFinished;
        public static bool[] tileSolid = new bool[1000];
        public static bool[] tileSolidTop = new bool[1000];
        public static int mouseX;
        public static int mouseY;
        public static int mouseScrollWheel;
        public static int oldMouseScrollWheel;
        public static bool gameMenu;
        public static bool chatMode;
        public static bool drawingPlayerChat;
        public static string npcChatText = string.Empty;
        public static bool playerInventory;
        public static bool ingameOptionsWindow;
        public static bool inFancyUI;
        public static bool gamePaused;
        public static bool SettingsEnabled_AutoReuseAllItems;
        public static object CreativeMenu;
        public static int netMode;
        public static bool dedServ;
        public static int screenWidth = 1280;
        public static int screenHeight = 800;
        public static float inventoryScale = 1f;
        public static long GameUpdateCount;
        public static TestVector2 screenPosition = new TestVector2();
        public static int myPlayer;
        public static object LocalPlayer;
        public static object[] player = new object[256];
    }

    internal sealed class TestVector2
    {
        public float X;
        public float Y;
    }

    internal sealed class Player
    {
        public static int tileTargetX;
        public static int tileTargetY;
        public int whoAmI;
        public bool active = true;
        public bool dead;
        public bool ghost;
        public TestVector2 position = new TestVector2();
        public int width = 20;
        public int height = 42;
        public object[] inventory = new object[59];
        public int selectedItem;
        public bool controlUseItem;
        public bool releaseUseItem = true;
        public bool channel;
        public bool mouseInterface;
        public int altFunctionUse;
        public bool controlUseTile;
        public bool tileInteractionHappened;
        public bool tileInteractAttempted;
        public int itemAnimation;
        public int itemTime;
        public int reuseDelay;
        public bool delayUseItem;
        public int statLife;
        public int statLifeMax2;
        public int statMana;
        public int statManaMax2;
        public int[] buffType = new int[22];
        public int[] buffTime = new int[22];
        public bool magicQuiver;
        public bool hasMoltenQuiver;
        public bool archery;
    }

    internal sealed class TestRecipeItem
    {
        public int type;
        public int stack = 1;
    }

    internal sealed class Recipe
    {
        public static int numRecipes;
        public object createItem = new TestRecipeItem();
        public object[] requiredItem = new object[0];
        public int[] acceptedGroups = new int[] { -1 };
    }

    internal sealed class RecipeGroup
    {
        public static Dictionary<int, RecipeGroup> recipeGroups = new Dictionary<int, RecipeGroup>();
        public List<int> ValidItems = new List<int>();
        public List<int> Items = new List<int>();
        public string Name = string.Empty;
        public string Key = string.Empty;

        public string GetText()
        {
            if (!string.IsNullOrWhiteSpace(Name))
            {
                return Name;
            }

            return Key ?? string.Empty;
        }
    }

    internal sealed class Projectile
    {
        public int type;
        public string Name = string.Empty;
        public int extraUpdates;
        public bool arrow;
        public int aiStyle;
        public int width = 2;
        public int height = 2;
        public bool tileCollide = true;
        public bool noGravity;
        public bool friendly;
        public bool hostile;

        public void SetDefaults(int projectileType)
        {
            type = projectileType;
            Name = projectileType.ToString(CultureInfo.InvariantCulture);
            extraUpdates = 0;
            arrow = false;
            aiStyle = 1;
            width = 2;
            height = 2;
            tileCollide = true;
            noGravity = false;
            friendly = true;
            hostile = false;

            if (projectileType == Terraria.ID.ProjectileID.WoodenArrowFriendly)
            {
                Name = "Wooden Arrow";
                arrow = true;
                width = 10;
                height = 10;
                return;
            }

            if (projectileType == Terraria.ID.ProjectileID.Bullet)
            {
                Name = "Bullet";
                width = 4;
                height = 4;
                noGravity = true;
                return;
            }

            if (projectileType == 1058)
            {
                Name = "Flairon";
                aiStyle = 15;
                width = 18;
                height = 18;
                noGravity = true;
                return;
            }

            if (projectileType == 9000)
            {
                Name = "Specific Launcher Projectile";
                width = 12;
                height = 12;
                noGravity = true;
                return;
            }

            if (projectileType == 9001)
            {
                Name = "Fast Test Arrow";
                arrow = true;
                extraUpdates = 2;
                width = 10;
                height = 10;
                return;
            }

            if (projectileType == 9002)
            {
                Name = "Slow Test Magic";
                aiStyle = 1;
                width = 12;
                height = 12;
                noGravity = true;
                return;
            }

            if (projectileType == 9003)
            {
                Name = "Returning Test Projectile";
                aiStyle = 3;
                width = 16;
                height = 16;
                noGravity = true;
                return;
            }

            if (projectileType == 9004)
            {
                Name = "Beam Test Projectile";
                aiStyle = 4;
                width = 4;
                height = 4;
                noGravity = true;
                return;
            }

            if (projectileType == 9005)
            {
                Name = "Homing Test Projectile";
                aiStyle = 99;
                width = 12;
                height = 12;
                noGravity = true;
            }
        }
    }

    internal sealed class CreativeMenuStub
    {
        public bool Enabled;
    }

    internal sealed class Tile
    {
        public bool activeValue;
        public int type;
        public int frameX;
        public int frameY;

        public bool active()
        {
            return activeValue;
        }
    }

    internal static class Lang
    {
        public static readonly Dictionary<int, string> ItemNames = new Dictionary<int, string>();
        public static readonly Dictionary<int, string> NpcNames = new Dictionary<int, string>();
        public static readonly Dictionary<int, string> MapObjectNames = new Dictionary<int, string>();

        public static string GetItemNameValue(int itemId)
        {
            string name;
            return ItemNames.TryGetValue(itemId, out name) ? name : itemId.ToString(CultureInfo.InvariantCulture);
        }

        public static string GetNPCNameValue(int npcId)
        {
            string name;
            return NpcNames.TryGetValue(npcId, out name) ? name : npcId.ToString(CultureInfo.InvariantCulture);
        }

        public static string GetMapObjectName(int lookup)
        {
            string name;
            return MapObjectNames.TryGetValue(lookup, out name) ? name : string.Empty;
        }
    }
}

namespace Terraria.DataStructures
{
    internal struct PlacementDetails
    {
        public int tileType;
        public short tileStyle;
    }

    internal struct TileReachCheckSettings
    {
        public static TileReachCheckSettings Simple
        {
            get { return new TileReachCheckSettings(); }
        }

        public void GetTileRegion(Terraria.Player player, out int LX, out int LY, out int HX, out int HY, int TB)
        {
            var extraX = 5 + TB;
            var extraY = 4 + TB;
            LX = (int)Math.Floor(player.position.X / 16f) - extraX;
            HX = (int)Math.Ceiling((player.position.X + player.width) / 16f) - 1 + extraX;
            LY = (int)Math.Floor(player.position.Y / 16f) - extraY;
            HY = (int)Math.Ceiling((player.position.Y + player.height) / 16f) - 1 + extraY;
        }
    }

    public sealed class FishingAttempt
    {
        public object playerFishingConditions;
        public int X;
        public int Y;
        public int bobberType;
        public bool common;
        public bool uncommon;
        public bool rare;
        public bool veryrare;
        public bool legendary;
        public bool crate;
        public bool junk;
        public bool inLava;
        public bool inHoney;
        public int waterTilesCount;
        public int waterNeededToFish;
        public float waterQuality;
        public int chumsInWater;
        public int fishingLevel;
        public bool CanFishInLava;
        public float atmo;
        public int questFish;
        public int heightLevel;
        public int rolledItemDrop;
        public int rolledEnemySpawn;
    }
}

namespace Terraria.GameContent.FishDropRules
{
    public sealed class FishingContext
    {
        public object Player;
        public object Fisher;
        public bool RolledCorruption;
        public bool RolledCrimson;
        public bool RolledJungle;
        public bool RolledSnow;
        public bool RolledDesert;
        public bool RolledInfectedDesert;
        public bool RolledRemixOcean;
    }
}

namespace Terraria.ID
{
    internal static class ItemID
    {
        public const short Worm = 2002;
        public const short Bunny = 2019;
        public const short TruffleWorm = 2673;
        public const short GoldWorm = 2895;
        public const short FairyCritterBlue = 4070;
        public const short GemBunnyRuby = 4842;
        public const short EmpressButterfly = 4961;

        internal static TestItemIdSearch Search = new TestItemIdSearch();

        internal static class Sets
        {
            public static bool[] gunProj = new bool[6000];
            public static bool[] HasRightFire = new bool[6000];
            public static bool[] ItemsThatAllowRepeatedRightClick = new bool[6000];
            public static bool[] ShootsOnUseRelease = new bool[6000];
            public static bool[] IsFishingCrate = new bool[6000];
            public static bool[] IsFishingCrateHardmode = new bool[6000];
            public static bool[] CanFishInLava = new bool[6000];
            public static bool[] IsLavaBait = new bool[6000];
            public static bool[] IsAMaterial = new bool[6000];
            public static int[] ShimmerTransformToItem = CreateDefaultShimmerTransforms();
            public static Terraria.DataStructures.PlacementDetails[] DerivedPlacementDetails =
                CreateDefaultPlacementDetails();

            public static void ResetPlacementDetailsForTesting()
            {
                DerivedPlacementDetails = CreateDefaultPlacementDetails();
            }

            private static Terraria.DataStructures.PlacementDetails[] CreateDefaultPlacementDetails()
            {
                var details = new Terraria.DataStructures.PlacementDetails[6000];
                for (var index = 0; index < details.Length; index++)
                {
                    details[index].tileType = -1;
                    details[index].tileStyle = 0;
                }

                return details;
            }

            private static int[] CreateDefaultShimmerTransforms()
            {
                var transforms = new int[6000];
                for (var index = 0; index < transforms.Length; index++)
                {
                    transforms[index] = -1;
                }

                return transforms;
            }
        }
    }

    internal static class ContentSamples
    {
        public static Dictionary<int, TestContentSampleItem> ItemsByType =
            new Dictionary<int, TestContentSampleItem>();
    }

    internal sealed class TestContentSampleItem
    {
        public int type;
        public int maxStack = 1;
        public int rare;
        public int value;
        public bool consumable;
        public int createTile = -1;
        public int createWall = -1;
        public int placeStyle;
    }

    internal static class AmmoID
    {
        public static int Arrow = 40;
        public static int Bullet = 97;
        public static int Rocket = 771;
        public static int Solution = 780;
        public static int Dart = 283;
        public static int Snowball = 949;
        public static int StyngerBolt = 1261;
        public static int CandyCorn = 1783;
        public static int JackOLantern = 1785;
        public static int Stake = 1836;
        public static int Coin = 71;

        internal static class Sets
        {
            public static Dictionary<int, Dictionary<int, int>> SpecificLauncherAmmoProjectileMatches = new Dictionary<int, Dictionary<int, int>>();
            public static bool[] IsArrow = new bool[6000];
            public static bool[] IsBullet = new bool[6000];

            static Sets()
            {
                IsArrow[Arrow] = true;
                IsArrow[Stake] = true;
                IsBullet[Bullet] = true;
                IsBullet[CandyCorn] = true;
            }
        }
    }

    internal static class ProjectileID
    {
        public static int WoodenArrowFriendly = 1;
        public static int FireArrow = 2;
        public static int Bullet = 14;
    }

    internal static class BuffID
    {
        public static int Archery = 16;
    }

    internal static class NPCID
    {
        public const short Bunny = 46;
        public const short Worm = 357;
        public const short TruffleWorm = 374;
        public const short GoldWorm = 448;
        public const short ZombieMerman = 586;
        public const short EyeballFlyingFish = 587;
        public const short FairyCritterBlue = 585;
        public const short BloodNautilus = 618;
        public const short GoblinShark = 620;
        public const short BloodEelHead = 621;
        public const short GemBunnyRuby = 650;
        public const short EmpressButterfly = 661;
        public const short TownSlimeRed = 682;

        internal static TestNpcIdSearch Search = new TestNpcIdSearch();
    }

    internal sealed class TestItemIdSearch
    {
        private readonly Dictionary<int, string> _names = new Dictionary<int, string>();

        public string GetName(int itemId)
        {
            string name;
            return _names.TryGetValue(itemId, out name) ? name : string.Empty;
        }

        public void SetName(int itemId, string name)
        {
            _names[itemId] = name ?? string.Empty;
        }

        public void Clear()
        {
            _names.Clear();
        }
    }

    internal sealed class TestNpcIdSearch
    {
        private readonly Dictionary<int, string> _names = new Dictionary<int, string>();

        public string GetName(int npcId)
        {
            string name;
            return _names.TryGetValue(npcId, out name) ? name : string.Empty;
        }

        public void SetName(int npcId, string name)
        {
            _names[npcId] = name ?? string.Empty;
        }

        public void Clear()
        {
            _names.Clear();
        }
    }
}

namespace Terraria.Map
{
    internal static class MapHelper
    {
        public static ushort[] wallLookup = new ushort[1000];
        public static readonly Dictionary<string, int> TileLookups = new Dictionary<string, int>();

        public static int TileToLookup(int tileType, int option)
        {
            int lookup;
            return TileLookups.TryGetValue(BuildTileKey(tileType, option), out lookup) ? lookup : -1;
        }

        public static void ResetForTesting()
        {
            wallLookup = new ushort[1000];
            TileLookups.Clear();
        }

        public static string BuildTileKey(int tileType, int option)
        {
            return tileType.ToString(CultureInfo.InvariantCulture) +
                   ":" +
                   option.ToString(CultureInfo.InvariantCulture);
        }
    }
}

namespace Terraria.GameInput
{
    internal static class PlayerInput
    {
        public static TestTriggersPack Triggers = new TestTriggersPack();
    }

    internal sealed class TestTriggersPack
    {
        public TestTriggersSet Current = new TestTriggersSet();
        public TestTriggersSet JustPressed = new TestTriggersSet();
    }

    internal sealed class TestTriggersSet
    {
        public bool MouseLeft;
        public bool MouseRight;
    }
}

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private const int TestFishingRod = 50000;
        private const int TestLavaproofTackleBag = 5064;
        private const int TestAnglerTackleBag = 3721;
        private const int TestHighTestFishingLine = 2373;
        private const int TestAnglerEarring = 2374;
        private const int TestTackleBox = 2375;
        private const int TestLavaFishingHook = 4881;
        private const int TestFishingBobber = 5139;
        private const int TestFishingBobberRainbow = 5146;
        private const int TestLuckyCoin = 855;
        private const int TestCoinRing = 3034;
        private const int TestGreedyRing = 3035;
        private const int TestLuckyHorseshoe = 158;
        private const int TestBlueHorseshoeBalloon = 1250;
        private const int TestHorseshoeBundle = 5331;

        private static int Main()
        {
            InstallProcessConfigDirectoryIsolation();
            var failed = 0;
            Run("test process config directory is isolated from user Documents", ref failed, TestProcessConfigDirectoryIsolatedFromUserDocuments);
            Run("test config isolation guard rejects real Documents directory", ref failed, TestConfigIsolationGuardRejectsRealDocumentsDirectory);
            Run("config service SaveAll writes only test config directory", ref failed, ConfigServiceSaveAllWritesOnlyTestConfigDirectory);
            Run("config service Initialize writes only test config directory", ref failed, ConfigServiceInitializeWritesOnlyTestConfigDirectory);
            Run("operation window state save writes only test config directory", ref failed, OperationWindowStateSaveWritesOnlyTestConfigDirectory);
            Run("legacy UI feature toggle save writes only test config directory", ref failed, LegacyUiFeatureToggleSaveWritesOnlyTestConfigDirectory);
            Run("config service Initialize saves migrated config after read stream closes", ref failed, ConfigServiceInitializeMigratesExistingConfigAfterReadStreamCloses);
            Run("config service bad JSON keeps original and protects features", ref failed, ConfigServiceInitializeBadJsonKeepsOriginalAndProtectsFeatures);
            Run("config service busy config keeps original and protects features", ref failed, ConfigServiceInitializeBusyConfigKeepsOriginalAndProtectsFeatures);
            Run("config service missing appsettings does not clear existing features", ref failed, ConfigServiceInitializeMissingAppSettingsDoesNotClearExistingFeatures);
            Run("config service SaveAll busy target does not break original", ref failed, ConfigServiceSaveAllBusyTargetDoesNotBreakOriginal);
            Run("config service SaveAll temp write failure does not break original", ref failed, ConfigServiceSaveAllTempWriteFailureDoesNotBreakOriginal);
            Run("config service load failure does not pollute next temporary directory", ref failed, ConfigServiceLoadFailureDoesNotPolluteNextTemporaryDirectory);
            Run("dependency resolver loads compressed embedded Harmony", ref failed, DependencyResolverLoadsCompressedEmbeddedHarmony);
            Run("temporary double jump requires a real air jump flag", ref failed, TemporaryDoubleJumpRequiresAirJump);
            Run("temporary rocket boots require rocket ability after equip", ref failed, TemporaryRocketBootsRequireCapability);
            Run("temporary flying carpet requires carpet availability after equip", ref failed, TemporaryFlyingCarpetRequiresCapability);
            Run("temporary flying carpet accepts post-apply capability evidence", ref failed, TemporaryFlyingCarpetAcceptsPostApplyCapabilityEvidence);
            Run("temporary mounts require equipped mount opportunity after equip", ref failed, TemporaryMountsRequireCapability);
            Run("temporary gravity globe requires flip opportunity after equip", ref failed, TemporaryGravityGlobeRequiresCapability);
            Run("temporary gravity globe accepts post-apply capability evidence", ref failed, TemporaryGravityGlobeAcceptsPostApplyCapabilityEvidence);
            Run("equipped double jump waits for near ground distance", ref failed, EquippedDoubleJumpWaitsForNearGroundDistance);
            Run("temporary double jump apply keeps preactivation window", ref failed, TemporaryDoubleJumpApplyKeepsPreactivationWindow);
            Run("temporary double jump activation waits for near ground distance", ref failed, TemporaryDoubleJumpActivationWaitsForNearGroundDistance);
            Run("flying carpet activation waits for lower near ground distance", ref failed, FlyingCarpetActivationWaitsForLowerNearGroundDistance);
            Run("gravity globe activation starts before landing", ref failed, GravityGlobeActivationStartsBeforeLanding);
            Run("safe landing jump pulse can start from press when release is primed", ref failed, SafeLandingJumpPulseCanStartFromPressWhenReleasePrimed);
            Run("safe landing quick mount pulse starts from press with immediate cancel", ref failed, SafeLandingQuickMountPulseStartsFromPressWithImmediateCancel);
            Run("safe landing grapple pulse starts from press with target", ref failed, SafeLandingGrapplePulseStartsFromPressWithTarget);
            Run("temporary double jump apply refreshes functional effect", ref failed, TemporaryDoubleJumpApplyRefreshesFunctionalEffect);
            Run("temporary passive accessory apply refreshes functional effect", ref failed, TemporaryPassiveAccessoryApplyRefreshesFunctionalEffect);
            Run("temporary passive accessory apply waits for tighter near ground window", ref failed, TemporaryPassiveAccessoryApplyWaitsForTighterNearGroundWindow);
            Run("temporary umbrella plan targets selected hotbar slot", ref failed, TemporaryUmbrellaPlanTargetsSelectedHotbarSlot);
            Run("temporary umbrella does not trigger while already held", ref failed, TemporaryUmbrellaDoesNotTriggerWhileAlreadyHeld);
            Run("temporary umbrella apply waits for near ground distance", ref failed, TemporaryUmbrellaApplyWaitsForNearGroundDistance);
            Run("temporary umbrella apply swaps into selected hotbar slot", ref failed, TemporaryUmbrellaApplySwapsIntoSelectedHotbarSlot);
            Run("safe landing treats wing flight state as original safe without equipped wing", ref failed, SafeLandingTreatsWingFlightStateAsOriginalSafeWithoutEquippedWing);
            Run("safe landing treats current equipped wing as already safe", ref failed, SafeLandingTreatsCurrentEquippedWingAsAlreadySafe);
            Run("safe landing active grapple is not already safe while falling fast", ref failed, SafeLandingActiveGrappleIsNotAlreadySafeWhileFallingFast);
            Run("safe landing ignores wing in locked accessory slot", ref failed, SafeLandingIgnoresWingInLockedAccessorySlot);
            Run("safe landing cheap precheck skips slow fall", ref failed, SafeLandingCheapPrecheckSkipsSlowFall);
            Run("safe landing cheap precheck opens full analysis when fast", ref failed, SafeLandingCheapPrecheckOpensFullAnalysisWhenFast);
            Run("safe landing cheap precheck fails open when velocity unavailable", ref failed, SafeLandingCheapPrecheckFailsOpenWhenVelocityUnavailable);
            Run("safe landing cheap precheck fails open when player state unavailable", ref failed, SafeLandingCheapPrecheckFailsOpenWhenPlayerStateUnavailable);
            Run("safe landing config summary cache hits and dirties", ref failed, SafeLandingConfigSummaryCacheHitsAndDirties);
            Run("safe landing cheap skip suppresses repeated diagnostics", ref failed, SafeLandingCheapSkipSuppressesRepeatedDiagnostics);
            Run("safe landing cheap skip reason change writes diagnostics", ref failed, SafeLandingCheapSkipReasonChangeWritesDiagnostics);
            Run("safe landing cheap skip writes after exception", ref failed, SafeLandingCheapSkipWritesAfterException);
            Run("safe landing submitted path keeps full diagnostics", ref failed, SafeLandingSubmittedPathKeepsFullDiagnostics);
            Run("movement input frame cache reuses profiles within frame", ref failed, MovementInputFrameCacheReusesProfilesWithinFrame);
            Run("continuous dash double tap hold survives brief direction gap", ref failed, ContinuousDashDoubleTapHoldSurvivesBriefDirectionGap);
            Run("continuous dash double tap hold cancels after release grace", ref failed, ContinuousDashDoubleTapHoldCancelsAfterReleaseGrace);
            Run("continuous dash double tap hold cancels on direction switch", ref failed, ContinuousDashDoubleTapHoldCancelsOnDirectionSwitch);
            Run("continuous dash double tap hold cancels when uncontrollable", ref failed, ContinuousDashDoubleTapHoldCancelsWhenUncontrollable);
            Run("continuous dash double tap requires double tap for opposite direction", ref failed, ContinuousDashDoubleTapRequiresDoubleTapForOppositeDirection);
            Run("continuous dash hold mode uses later direction when both held", ref failed, ContinuousDashHoldModeUsesLaterDirectionWhenBothHeld);
            Run("continuous dash requested direction accepts both keys", ref failed, ContinuousDashDirectionHeldAcceptsBothKeys);
            Run("safe landing cheap precheck uses cached motion within frame", ref failed, SafeLandingCheapPrecheckUsesCachedMotionWithinFrame);
            Run("safe landing projects horizontal impact probe", ref failed, SafeLandingProjectsHorizontalImpactProbe);
            Run("safe landing manual probe detects sloped platform", ref failed, SafeLandingManualProbeDetectsSlopedPlatform);
            Run("safe landing collision fast path records manual fallback", ref failed, SafeLandingCollisionFastPathRecordsManualFallback);
            Run("safe landing landing surface reports slope contact", ref failed, SafeLandingLandingSurfaceReportsSlopeContact);
            Run("safe landing landing surface reports moving into slope", ref failed, SafeLandingLandingSurfaceReportsMovingIntoSlope);
            Run("safe landing landing surface handles three tile foot coverage", ref failed, SafeLandingLandingSurfaceHandlesThreeTileFootCoverage);
            Run("safe landing landing surface projects horizontal motion", ref failed, SafeLandingLandingSurfaceProjectsHorizontalMotion);
            Run("safe landing landing surface prefers projected motion over current column", ref failed, SafeLandingLandingSurfacePrefersProjectedMotionOverCurrentColumn);
            Run("safe landing jump input profile reads grapple shoot speed", ref failed, SafeLandingJumpInputProfileReadsGrappleShootSpeed);
            Run("safe landing strategy catalog preserves priority order", ref failed, SafeLandingStrategyCatalogPreservesPriorityOrder);
            Run("priority 1 equipped double jump generates jump plan", ref failed, PriorityOneEquippedDoubleJumpGeneratesJumpPlan);
            Run("priority 1 equipped rocket boots generates jump plan", ref failed, PriorityOneEquippedRocketBootsGeneratesJumpPlan);
            Run("priority 1 equipped flying carpet generates jump plan", ref failed, PriorityOneEquippedFlyingCarpetGeneratesJumpPlan);
            Run("priority 1 equipped flying mount generates quick mount plan", ref failed, PriorityOneEquippedFlyingMountGeneratesQuickMountPlan);
            Run("priority 1 equipped gravity globe generates gravity flip plan", ref failed, PriorityOneEquippedGravityGlobeGeneratesGravityFlipPlan);
            Run("priority 1 gravity globe uses dedicated option", ref failed, PriorityOneGravityGlobeUsesDedicatedOption);
            Run("priority 1 gravity globe outranks mounts", ref failed, PriorityOneGravityGlobeOutranksMounts);
            Run("safe landing mount cancel clears when already unmounted", ref failed, SafeLandingMountCancelClearsWhenAlreadyUnmounted);
            Run("safe landing mount cancel imminent collision bypasses stable wait", ref failed, SafeLandingMountCancelImminentCollisionBypassesStableWait);
            Run("safe landing mount cancel waits when collision is still distant", ref failed, SafeLandingMountCancelWaitsWhenCollisionIsDistant);
            Run("priority 2 temporary horseshoe generates inventory apply plan", ref failed, PriorityTwoTemporaryHorseshoeGeneratesInventoryApplyPlan);
            Run("priority 2 temporary wings generates inventory apply plan", ref failed, PriorityTwoTemporaryWingsGeneratesInventoryApplyPlan);
            Run("priority 2 temporary fairy boots targets leg equipment slot", ref failed, PriorityTwoTemporaryFairyBootsTargetsLegEquipmentSlot);
            Run("priority 2 temporary double jump records refresh expectation", ref failed, PriorityTwoTemporaryDoubleJumpRecordsRefreshExpectation);
            Run("priority 2 temporary rocket boots detects terraspark fallback id", ref failed, PriorityTwoTemporaryRocketBootsDetectsTerrasparkFallbackId);
            Run("priority 2 temporary rocket boots records post apply verification", ref failed, PriorityTwoTemporaryRocketBootsRecordsPostApplyVerification);
            Run("temporary rocket boots pulse keeps active hold ticks", ref failed, TemporaryRocketBootsPulseKeepsActiveHoldTicks);
            Run("gravity flip pulse starts from press", ref failed, GravityFlipPulseStartsFromPress);
            Run("simulated jump pulse presses after release without final release", ref failed, SimulatedJumpPulsePressesAfterReleaseWithoutFinalRelease);
            Run("priority 2 temporary gravity globe records restore expectation", ref failed, PriorityTwoTemporaryGravityGlobeRecordsRestoreExpectation);
            Run("gravity globe default migration enables option", ref failed, GravityGlobeDefaultMigrationEnablesOption);
            Run("priority 3 umbrella request targets hotbar without activation pulse", ref failed, PriorityThreeUmbrellaRequestTargetsHotbarWithoutActivationPulse);
            Run("priority 4 inventory grapple generates grapple plan", ref failed, PriorityFourInventoryGrappleGeneratesGrapplePlan);
            Run("priority 4 grapple waits behind priority 1", ref failed, PriorityFourGrappleWaitsBehindPriorityOne);
            Run("priority 5 teleport rod generates use plan", ref failed, PriorityFiveTeleportRodGeneratesUsePlan);
            Run("priority 5 teleport rod uses direct inventory slot", ref failed, PriorityFiveTeleportRodUsesDirectInventorySlot);
            Run("priority 5 teleport rod waits behind grapple", ref failed, PriorityFiveTeleportRodWaitsBehindGrapple);
            Run("priority 4 grapple targets landing surface slope contact", ref failed, PriorityFourGrappleTargetsLandingSurfaceSlopeContact);
            Run("priority 4 grapple flat surface follows right motion", ref failed, PriorityFourGrappleFlatSurfaceFollowsRightMotion);
            Run("priority 4 grapple flat surface follows left motion", ref failed, PriorityFourGrappleFlatSurfaceFollowsLeftMotion);
            Run("priority 4 grapple with slope uses gentle motion bias", ref failed, PriorityFourGrappleWithSlopeUsesGentleMotionBias);
            Run("priority 4 grapple unknown surface uses motion lead and contact y", ref failed, PriorityFourGrappleUnknownSurfaceUsesMotionLeadAndContactY);
            Run("priority 5 teleport rod does not run when grapple too early", ref failed, PriorityFiveTeleportRodDoesNotRunWhenGrappleTooEarly);
            Run("priority 5 teleport rod waits behind valid grapple", ref failed, PriorityFiveTeleportRodWaitsBehindValidGrapple);
            Run("priority 5 teleport rod can fallback when grapple too late", ref failed, PriorityFiveTeleportRodCanFallbackWhenGrappleTooLate);
            Run("safe landing resolve grapple hook speed prefers equipped shoot speed over table", ref failed, SafeLandingResolveGrappleHookSpeedPrefersEquippedShootSpeedOverFallbackTable);
            Run("safe landing resolve grapple hook speed falls back to table only when shoot speed missing", ref failed, SafeLandingResolveGrappleHookSpeedFallsBackToTableOnlyWhenShootSpeedMissing);
            Run("safe landing resolve grapple hook speed uses equipped fallback before inventory shoot speed", ref failed, SafeLandingResolveGrappleHookSpeedUsesEquippedFallbackBeforeInventoryShootSpeed);
            Run("safe landing resolve grapple hook speed uses inventory shoot speed when no equipped grapple", ref failed, SafeLandingResolveGrappleHookSpeedUsesInventoryShootSpeedWhenNoEquippedGrapple);
            Run("safe landing resolve grapple hook speed falls back to default only when unknown", ref failed, SafeLandingResolveGrappleHookSpeedFallsBackToDefaultOnlyWhenUnknown);
            Run("safe landing grapple too early blocks lower priority but not ready", ref failed, SafeLandingGrappleTooEarlyBlocksLowerPriorityButNotReady);
            Run("safe landing grapple too late allows teleport rod fallback", ref failed, SafeLandingGrappleTooLateAllowsTeleportRodFallback);
            Run("safe landing grapple too slow allows priority 5 fallback", ref failed, SafeLandingGrappleTooSlowAllowsPriority5Fallback);
            Run("safe landing grapple only too late still submits when teleport rod disabled", ref failed, SafeLandingGrappleOnlyTooLateStillSubmitsWhenTeleportRodDisabled);
            Run("safe landing grapple only too slow still submits when teleport rod unavailable", ref failed, SafeLandingGrappleOnlyTooSlowStillSubmitsWhenTeleportRodUnavailable);
            Run("safe landing grapple speed unavailable allows priority 5", ref failed, SafeLandingGrappleSpeedUnavailableAllowsPriority5);
            Run("safe landing p1 to p3 block lower priority not destroyed", ref failed, SafeLandingP1toP3BlockLowerPriorityNotDestroyed);
            Run("priority 1 still blocks lower priority when timing not ready", ref failed, PriorityOneStillBlocksLowerPriorityWhenTimingNotReady);
            Run("priority lower strategy waits behind higher timing window", ref failed, PriorityLowerStrategyWaitsBehindHigherTimingWindow);
            Run("safe landing clears repeated rescue when landing changes", ref failed, SafeLandingClearsRepeatedRescueWhenLandingChanges);
            Run("safe landing teleport rod request stale when landing changes", ref failed, SafeLandingTeleportRodRequestStaleWhenLandingChanges);
            Run("safe landing teleport rod request keeps same landing", ref failed, SafeLandingTeleportRodRequestKeepsSameLanding);
            Run("safe landing request builder preserves old metadata keys", ref failed, SafeLandingRequestBuilderPreservesOldMetadataKeys);
            Run("safe landing recovery keeps item when restore target changed", ref failed, SafeLandingRecoveryKeepsItemWhenRestoreTargetChanged);
            Run("channel resolver maps unknown action to global exclusive", ref failed, ChannelResolverUnknownActionDefaultsGlobalExclusive);
            Run("channel resolver maps diagnostic noop to none", ref failed, ChannelResolverDiagnosticNoopUsesNoChannel);
            Run("channel resolver maps use hotbar item to use item and hotbar", ref failed, ChannelResolverUseHotbarItemUsesItemAndHotbar);
            Run("channel resolver maps inventory slot and conflicts with use item", ref failed, ChannelResolverInventorySlotConflictsWithUseItem);
            Run("channel resolver maps safe landing quick mount", ref failed, ChannelResolverSafeLandingQuickMount);
            Run("channel resolver maps safe landing gravity flip", ref failed, ChannelResolverSafeLandingGravityFlip);
            Run("channel resolver maps safe landing grapple", ref failed, ChannelResolverSafeLandingGrapple);
            Run("channel resolver maps dash to dash and direction", ref failed, ChannelResolverDashUsesDashAndDirection);
            Run("channel resolver maps magic string raw input", ref failed, ChannelResolverMagicStringUsesPulseBridge);
            Run("channel resolver maps auto harvest sustained raw input", ref failed, ChannelResolverAutoHarvestSustainedUse);
            Run("channel resolver maps auto mining sustained raw input", ref failed, ChannelResolverAutoMiningSustainedUse);
            Run("channel resolver maps auto capture sustained raw input", ref failed, ChannelResolverAutoCaptureCritterSustainedUse);
            Run("channel resolver maps phaseblade quick switch raw input", ref failed, ChannelResolverPhasebladeQuickSwitch);
            Run("channel resolver maps shop to npc and inventory", ref failed, ChannelResolverShopUsesNpcAndInventory);
            Run("channel resolver maps trash slot to inventory", ref failed, ChannelResolverTrashSlotUsesInventory);
            Run("channel resolver maps reforge to npc and inventory", ref failed, ChannelResolverReforgeUsesNpcAndInventory);
            Run("quick rename increments trailing numeric suffix", ref failed, QuickRenameIncrementsTrailingNumericSuffix);
            Run("auto stack detects only increased item types", ref failed, AutoStackDetectsOnlyIncreasedItemTypes);
            Run("auto stack ignores favorite toggle and unstackable moves", ref failed, AutoStackIgnoresFavoriteToggleAndUnstackableMoves);
            Run("auto stack final signature excludes unstackable items", ref failed, AutoStackFinalSignatureExcludesUnstackableItems);
            Run("auto stack request uses chest selective quick stack metadata", ref failed, AutoStackRequestUsesChestSelectiveQuickStackMetadata);
            Run("auto stack allows player inventory open", ref failed, AutoStackAllowsPlayerInventoryOpen);
            Run("auto stack still blocks chest UI", ref failed, AutoStackStillBlocksChestUi);
            Run("auto stack uses short inventory-open settle window", ref failed, AutoStackUsesShortInventoryOpenSettleWindow);
            Run("auto stack unsafe UI retains pending transaction", ref failed, AutoStackUnsafeUiRetainsPendingTransaction);
            Run("auto stack successful action result clears pending transaction", ref failed, AutoStackSuccessfulActionResultClearsPendingTransaction);
            Run("auto stack unverified action result keeps retry pending", ref failed, AutoStackUnverifiedActionResultKeepsRetryPending);
            Run("auto sell default list is conservative fishing junk", ref failed, AutoSellDefaultListIsConservativeFishingJunk);
            Run("auto sell request uses shop metadata", ref failed, AutoSellRequestUsesShopMetadata);
            Run("auto sell allows player inventory open", ref failed, AutoSellAllowsPlayerInventoryOpen);
            Run("auto sell candidates use inventory snapshot", ref failed, AutoSellCandidatesUseInventorySnapshot);
            Run("auto discard default list is empty", ref failed, AutoDiscardDefaultListIsEmpty);
            Run("auto discard request uses trash metadata", ref failed, AutoDiscardRequestUsesTrashMetadata);
            Run("auto discard allows player inventory open", ref failed, AutoDiscardAllowsPlayerInventoryOpen);
            Run("auto discard candidates use inventory snapshot", ref failed, AutoDiscardCandidatesUseInventorySnapshot);
            Run("quick bag open request uses inventory slot metadata", ref failed, QuickBagOpenRequestUsesInventorySlotMetadata);
            Run("quick item hotkey request uses fresh click metadata", ref failed, QuickItemHotkeyRequestUsesFreshClickMetadata);
            Run("quick bag open yields after batch when cleanup enabled", ref failed, QuickBagOpenYieldsAfterBatchWhenCleanupEnabled);
            Run("auto deposit coins request uses chest metadata", ref failed, AutoDepositCoinsRequestUsesChestMetadata);
            Run("auto deposit coins candidates use inventory snapshot", ref failed, AutoDepositCoinsCandidatesUseInventorySnapshot);
            Run("auto extractinator request uses item use metadata", ref failed, AutoExtractinatorRequestUsesItemUseMetadata);
            Run("keep favorited request uses inventory slot metadata", ref failed, KeepFavoritedRequestUsesInventorySlotMetadata);
            Run("keep favorited manual unfavorite clears tracking", ref failed, KeepFavoritedManualUnfavoriteClearsTracking);
            Run("keep favorited restores armor slot", ref failed, KeepFavoritedRestoresArmorSlot);
            Run("keep favorited restores same inventory slot after leaving", ref failed, KeepFavoritedRestoresSameInventorySlotAfterLeaving);
            Run("keep favorited restores trash round trip", ref failed, KeepFavoritedRestoresTrashRoundTrip);
            Run("keep favorited restores bucket transform same slot", ref failed, KeepFavoritedRestoresBucketTransformSameSlot);
            Run("quick reforge prefixes normalize blanks and duplicates", ref failed, QuickReforgePrefixesNormalizeBlanksAndDuplicates);
            Run("quick reforge prefix matching accepts full affix names", ref failed, QuickReforgePrefixMatchingAcceptsFullAffixNames);
            Run("quick reforge request uses reforge metadata", ref failed, QuickReforgeRequestUsesReforgeMetadata);
            Run("auto tax collect request uses npc metadata", ref failed, AutoTaxCollectRequestUsesNpcMetadata);
            Run("feature catalog exposes travel menu", ref failed, FeatureCatalogExposesTravelMenu);
            Run("travel menu runtime path is resumed", ref failed, TravelMenuRuntimePathIsResumed);
            Run("feature catalog exposes auto discard", ref failed, FeatureCatalogExposesAutoDiscard);
            Run("feature catalog exposes quick reforge", ref failed, FeatureCatalogExposesQuickReforge);
            Run("feature catalog exposes auto tax collect", ref failed, FeatureCatalogExposesAutoTaxCollect);
            Run("feature catalog exposes auto mining", ref failed, FeatureCatalogExposesAutoMining);
            Run("feature catalog exposes auto capture critter", ref failed, FeatureCatalogExposesAutoCaptureCritter);
            Run("feature catalog exposes auto harvest", ref failed, FeatureCatalogExposesAutoHarvest);
            Run("feature catalog exposes map quick announcement config", ref failed, FeatureCatalogExposesMapQuickAnnouncementConfig);
            Run("feature catalog exposes search query UI", ref failed, FeatureCatalogExposesSearchQueryUi);
            Run("map quick announcement capture rejects invalid mouse wheel and duplicates", ref failed, MapQuickAnnouncementCaptureRulesRejectInvalidMouseWheelAndDuplicates);
            Run("map quick announcement hotkey state fires once per chord hold", ref failed, MapQuickAnnouncementHotkeyStateMachineFiresOnTriggerEdgeOnceUntilRelease);
            Run("map quick announcement hotkey state supports mouse trigger", ref failed, MapQuickAnnouncementHotkeyStateMachineSupportsMouseTrigger);
            Run("map quick announcement runtime skips disabled and blocked contexts", ref failed, MapQuickAnnouncementRuntimeSkipsDisabledAndBlockedContexts);
            Run("map quick announcement runtime triggers consumes and delivers", ref failed, MapQuickAnnouncementRuntimeTriggersConsumesAndDelivers);
            Run("map quick announcement mouse pending waits for next UI hover snapshot", ref failed, MapQuickAnnouncementRuntimeMousePendingWaitsForNextUiHoverSnapshot);
            Run("map quick announcement mouse pending empty UI slot does not fall through", ref failed, MapQuickAnnouncementRuntimeMousePendingEmptyUiSlotDoesNotFallThrough);
            Run("map quick announcement mouse pending expires then falls back to world", ref failed, MapQuickAnnouncementRuntimeMousePendingExpiresThenFallsBackToWorld);
            Run("map quick announcement runtime consumes before cooldown block", ref failed, MapQuickAnnouncementRuntimeConsumesBeforeCooldownBlock);
            Run("map quick announcement runtime keyboard trigger does not consume mouse", ref failed, MapQuickAnnouncementRuntimeKeyboardTriggerDoesNotConsumeMouse);
            Run("map quick announcement runtime consumes right and side mouse triggers", ref failed, MapQuickAnnouncementRuntimeConsumesRightAndSideMouseTriggers);
            Run("map quick announcement mouse trigger compat clears matching main pulse", ref failed, MapQuickAnnouncementMouseTriggerCompatClearsMatchingMainPulse);
            Run("auto mining scanner links three-tile gaps", ref failed, AutoMiningScannerLinksThreeTileGaps);
            Run("auto mining scanner keeps inactive mined seed connectivity", ref failed, AutoMiningScannerKeepsInactiveMinedSeedConnectivity);
            Run("auto mining scanner groups gem cluster tiles", ref failed, AutoMiningScannerGroupsGemClusterTiles);
            Run("auto mining scanner keeps normal ore single type", ref failed, AutoMiningScannerKeepsNormalOreSingleType);
            Run("auto mining target uses actual tile type for pick power", ref failed, AutoMiningTargetUsesActualTileTypeForPickPower);
            Run("auto mining fallback recognizes extra ore and gravity tiles", ref failed, AutoMiningFallbackRecognizesExtraOreAndGravityTiles);
            Run("auto mining refresh tracks nearby gravity tile after vanilla fall", ref failed, AutoMiningRefreshTracksNearbyGravityTileAfterVanillaFall);
            Run("auto mining refresh relocates gravity tile beyond old three tile radius", ref failed, AutoMiningRefreshRelocatesGravityTileBeyondOldThreeTileRadius);
            Run("auto mining refresh keeps shifted gravity column marked", ref failed, AutoMiningRefreshKeepsShiftedGravityColumnMarked);
            Run("auto mining refresh expires out of range gravity relocation", ref failed, AutoMiningRefreshExpiresOutOfRangeGravityRelocation);
            Run("auto mining refresh keeps normal ore from gravity rescan", ref failed, AutoMiningRefreshKeepsNormalOreFromGravityRescan);
            Run("auto mining selected slot switch interrupts selection", ref failed, AutoMiningSelectedSlotSwitchInterruptsSelection);
            Run("auto mining manual observation can reselect outside active vein", ref failed, AutoMiningManualObservationCanReselectOutsideActiveVein);
            Run("auto mining request uses sustained raw input metadata", ref failed, AutoMiningRequestUsesSustainedRawInputMetadata);
            Run("auto capture critter request uses sustained raw input metadata", ref failed, AutoCaptureCritterRequestUsesSustainedRawInputMetadata);
            Run("auto capture critter range uses bug net reach", ref failed, AutoCaptureCritterRangeUsesBugNetReach);
            Run("auto capture critter restore pole keeps fishing slot selected", ref failed, AutoCaptureCritterRestorePoleKeepsFishingSlotSelected);
            Run("selected item state force selection updates hotbar state", ref failed, SelectedItemStateForceSelectionUpdatesHotbarState);
            Run("selected item state request allows deferred selection", ref failed, SelectedItemStateRequestAllowsDeferredSelection);
            Run("fishing loadout restore attempted keeps session for retry", ref failed, FishingLoadoutRestoreAttemptedKeepsSessionForRetry);
            Run("auto capture critter recognizes bug net item type", ref failed, AutoCaptureCritterRecognizesBugNetItemType);
            Run("auto capture critter manual mode requires held bug net", ref failed, AutoCaptureCritterManualModeRequiresHeldBugNet);
            Run("auto capture critter category defaults enable all options", ref failed, AutoCaptureCritterCategoryDefaultsEnableAllOptions);
            Run("auto capture critter categories separate special bait", ref failed, AutoCaptureCritterCategoriesSeparateSpecialBait);
            Run("auto capture critter disabled category blocks request", ref failed, AutoCaptureCritterDisabledCategoryBlocksRequest);
            Run("auto capture critter tick enqueues request when nearby", ref failed, AutoCaptureCritterTickEnqueuesRequestWhenNearby);
            Run("auto harvest maps exact herb seeds", ref failed, AutoHarvestMapsExactHerbSeeds);
            Run("auto harvest request uses sustained raw input metadata", ref failed, AutoHarvestRequestUsesSustainedRawInputMetadata);
            Run("auto harvest replant request uses exact seed metadata", ref failed, AutoHarvestReplantRequestUsesExactSeedMetadata);
            Run("auto mining targets nearest reachable frontier tile", ref failed, AutoMiningTargetsNearestReachableFrontierTile);
            Run("auto mining skips reach checks for interior tiles", ref failed, AutoMiningSkipsReachChecksForInteriorTiles);
            Run("auto mining refuses reachable interior fallback", ref failed, AutoMiningRefusesReachableInteriorFallback);
            Run("auto mining sustained use validates exact mineable target", ref failed, AutoMiningSustainedUseValidatesExactMineableTarget);
            Run("auto mining reach excludes rectangle corners outside helper radius", ref failed, AutoMiningReachExcludesRectangleCornersOutsideHelperRadius);
            Run("auto mining reach uses vanilla tile region when available", ref failed, AutoMiningReachUsesVanillaTileRegionWhenAvailable);
            Run("auto mining takeover rejects vanilla edge outside strict radius", ref failed, AutoMiningTakeoverRejectsVanillaEdgeOutsideStrictRadius);
            Run("auto mining takeover preserves negative tile boost", ref failed, AutoMiningTakeoverPreservesNegativeTileBoost);
            Run("auto mining reach keeps fallback detectable when vanilla region unavailable", ref failed, AutoMiningReachKeepsFallbackDetectableWhenVanillaRegionUnavailable);
            Run("auto mining green reach respects pick power", ref failed, AutoMiningGreenReachRespectsPickPower);
            Run("auto mining itemcheck override syncs exact tile target", ref failed, AutoMiningItemCheckOverrideSyncsExactTileTarget);
            Run("auto mining overlay uses low alpha green red style", ref failed, AutoMiningOverlayUsesLowAlphaGreenRedStyle);
            Run("worldgen debug viewer and developer menu are always available", ref failed, WorldGenDebugViewerAndDeveloperMenuAlwaysAvailable);
            Run("diagnostic snapshot writes worldgen debug state", ref failed, DiagnosticSnapshotWritesWorldGenDebugState);
            Run("diagnostic snapshot writes action queue admission state", ref failed, DiagnosticSnapshotWritesActionQueueAdmissionState);
            Run("diagnostic snapshot writes ItemCheck writer state", ref failed, DiagnosticSnapshotWritesItemCheckWriterState);
            Run("diagnostic snapshot writes auto stack state", ref failed, DiagnosticSnapshotWritesAutoStackState);
            Run("diagnostic snapshot writes auto deposit coins state", ref failed, DiagnosticSnapshotWritesAutoDepositCoinsState);
            Run("diagnostic snapshot writes auto tax collect state", ref failed, DiagnosticSnapshotWritesAutoTaxCollectState);
            Run("diagnostic snapshot writes auto capture critter state", ref failed, DiagnosticSnapshotWritesAutoCaptureCritterState);
            Run("diagnostic snapshot writes auto harvest state", ref failed, DiagnosticSnapshotWritesAutoHarvestState);
            Run("diagnostic snapshot writes combat ItemCheck auto clicker state", ref failed, DiagnosticSnapshotWritesCombatItemCheckAutoClickerState);
            Run("diagnostic snapshot writes combat flail combo state", ref failed, DiagnosticSnapshotWritesCombatFlailComboState);
            Run("diagnostic snapshot writes combat phaseblade quick switch state", ref failed, DiagnosticSnapshotWritesCombatPhasebladeQuickSwitchState);
            Run("diagnostic snapshot writes fishing idle pipeline state", ref failed, DiagnosticSnapshotWritesFishingIdlePipelineState);
            Run("performance hitch recorder detects runtime gaps", ref failed, PerformanceHitchRecorderDetectsRuntimeGaps);
            Run("performance operation recorder uses scenario thresholds", ref failed, PerformanceOperationRecorderUsesScenarioThresholds);
            Run("diagnostic snapshot writes performance hitch state", ref failed, DiagnosticSnapshotWritesPerformanceHitchState);
            Run("feature catalog exposes implemented items inventory automation", ref failed, FeatureCatalogExposesImplementedItemsInventoryAutomation);
            Run("feature catalog exposes goblin execution", ref failed, FeatureCatalogExposesGoblinExecution);
            Run("feature catalog exposes phaseblade quick switch config", ref failed, FeatureCatalogExposesPhasebladeQuickSwitchConfig);
            Run("map quick announcement settings normalize slots and defaults", ref failed, MapQuickAnnouncementSettingsNormalizeSlotsAndDefaults);
            Run("first-run app settings defaults match requested UI baseline", ref failed, FirstRunAppSettingsDefaultsMatchRequestedUiBaseline);
            Run("auto capture critter mode aliases preserve legacy bool", ref failed, AutoCaptureCritterModeAliasesPreserveLegacyBool);
            Run("app settings code-domain aliases preserve misc storage", ref failed, AppSettingsCodeDomainAliasesPreserveMiscStorage);
            Run("game state read options map coin automation to coins profile", ref failed, GameStateReadOptionsMapCoinAutomationToCoinsProfile);
            Run("game state read options keep auto tax collect lightweight", ref failed, GameStateReadOptionsKeepAutoTaxCollectLightweight);
            Run("game state read options merge capture and stack profiles", ref failed, GameStateReadOptionsMergeCaptureAndStackProfiles);
            Run("game state read options keep diagnostics full profile", ref failed, GameStateReadOptionsKeepDiagnosticsFullProfile);
            Run("diagnostic snapshot writes game state read profiles", ref failed, DiagnosticSnapshotWritesGameStateReadProfiles);
            Run("runtime settings snapshot normalizes hot path fields", ref failed, RuntimeSettingsSnapshotNormalizesHotPathFields);
            Run("runtime settings snapshot carries map quick announcement config", ref failed, RuntimeSettingsSnapshotCarriesMapQuickAnnouncementConfig);
            Run("runtime settings snapshot builds game state profile", ref failed, RuntimeSettingsSnapshotBuildsGameStateProfile);
            Run("runtime settings snapshot splits fishing dispatch layers", ref failed, RuntimeSettingsSnapshotSplitsFishingDispatchLayers);
            Run("runtime fishing dispatch skips filter-only settings", ref failed, RuntimeFishingDispatchSkipsFilterOnlySettings);
            Run("fishing residual state keeps runtime dispatch alive", ref failed, FishingResidualStateKeepsRuntimeDispatchAlive);
            Run("runtime fishing dispatch uses idle watchdog cadence", ref failed, RuntimeFishingDispatchUsesIdleWatchdogCadence);
            Run("runtime fishing dispatch promotes fresh active bobber", ref failed, RuntimeFishingDispatchPromotesFreshActiveBobber);
            Run("fishing idle fast path skips bait and equipment details", ref failed, FishingIdleFastPathSkipsBaitAndEquipmentDetails);
            Run("runtime settings snapshot provider rebuilds after config mutation", ref failed, RuntimeSettingsSnapshotProviderRebuildsAfterConfigMutation);
            Run("runtime settings snapshot provider skips disabled list hashes", ref failed, RuntimeSettingsSnapshotProviderSkipsDisabledListHashes);
            Run("runtime service scheduler honors cadence and disabled cleanup", ref failed, RuntimeServiceSchedulerHonorsCadenceAndDisabledCleanup);
            Run("runtime automation dispatcher preserves dispatch contract", ref failed, RuntimeAutomationDispatcherPreservesDispatchContract);
            Run("runtime input focus guard uses game state focus", ref failed, RuntimeInputFocusGuardUsesGameStateFocus);
            Run("movement teleport correction requires vanilla use frame", ref failed, MovementTeleportCorrectionRequiresVanillaUseFrame);
            Run("combat perfect revolver ItemCheck takeover mirrors helper cadence", ref failed, CombatPerfectRevolverItemCheckTakeoverMirrorsHelperCadence);
            Run("combat perfect revolver schedules only in fire window", ref failed, CombatPerfectRevolverSchedulesOnlyInFireWindow);
            Run("combat auto clicker input probe targets reported items", ref failed, CombatAutoClickerInputProbeTargetsReportedItems);
            Run("combat ItemCheck auto clicker core presses ready item", ref failed, CombatItemCheckAutoClickerCorePressesReadyItem);
            Run("combat ItemCheck auto clicker core releases cooldown item", ref failed, CombatItemCheckAutoClickerCoreReleasesCooldownItem);
            Run("combat ItemCheck auto clicker core disabled no-op", ref failed, CombatItemCheckAutoClickerCoreDisabledNoOp);
            Run("combat ItemCheck auto clicker four quadrants", ref failed, CombatItemCheckAutoClickerFourQuadrants);
            Run("combat ItemCheck auto clicker respects vanilla auto reuse", ref failed, CombatItemCheckAutoClickerRespectsVanillaAutoReuse);
            Run("combat ItemCheck auto clicker samples and hard excludes", ref failed, CombatItemCheckAutoClickerSamplesAndHardExcludes);
            Run("combat ItemCheck auto clicker reads tool fields", ref failed, CombatItemCheckAutoClickerReadsToolFields);
            Run("combat ItemCheck auto clicker reads shoots-on-release set", ref failed, CombatItemCheckAutoClickerReadsShootsOnReleaseSet);
            Run("combat ItemCheck auto clicker fails closed when vanilla switch unavailable", ref failed, CombatItemCheckAutoClickerFailsClosedWhenVanillaSwitchUnavailable);
            Run("combat ItemCheck auto clicker reads mouse item slot", ref failed, CombatItemCheckAutoClickerReadsMouseItemSlot);
            Run("combat ItemCheck auto clicker yields to adjacent scoped use", ref failed, CombatItemCheckAutoClickerYieldsToAdjacentScopedUse);
            Run("combat ItemCheck auto clicker takeover restores input state", ref failed, CombatItemCheckAutoClickerTakeoverRestoresInputState);
            Run("combat ItemCheck auto clicker diagnostics record scoped decision", ref failed, CombatItemCheckAutoClickerDiagnosticsRecordScopedDecision);
            Run("combat phaseblade quick switch recognizes fixed item list", ref failed, CombatPhasebladeQuickSwitchRecognizesFixedItemList);
            Run("combat phaseblade quick switch scans hotbar only", ref failed, CombatPhasebladeQuickSwitchScansHotbarOnly);
            Run("combat phaseblade quick switch state machine cycles actions", ref failed, CombatPhasebladeQuickSwitchStateMachineCyclesActions);
            Run("combat phaseblade quick switch state machine resets and clamps", ref failed, CombatPhasebladeQuickSwitchStateMachineResetsAndClamps);
            Run("combat phaseblade quick switch raw input executor lifecycle", ref failed, CombatPhasebladeQuickSwitchRawInputExecutorLifecycle);
            Run("combat phaseblade quick switch bridge applies scoped input and leaves last slot", ref failed, CombatPhasebladeQuickSwitchBridgeAppliesScopedInputAndLeavesLastSlot);
            Run("combat phaseblade quick switch runtime guard submits right held request", ref failed, CombatPhasebladeQuickSwitchRuntimeGuardSubmitsRightHeldRequest);
            Run("combat phaseblade quick switch runtime guard blocks unsafe context", ref failed, CombatPhasebladeQuickSwitchRuntimeGuardBlocksUnsafeContext);
            Run("combat phaseblade quick switch diagnostics record profile summary", ref failed, CombatPhasebladeQuickSwitchDiagnosticsRecordProfileSummary);
            Run("combat flail combo core launches releases and recalls", ref failed, CombatFlailComboCoreLaunchesReleasesAndRecalls);
            Run("combat flail combo blocks vanilla right click semantics", ref failed, CombatFlailComboBlocksVanillaRightClickSemantics);
            Run("combat flail combo item set guard fails closed", ref failed, CombatFlailComboItemSetGuardFailsClosed);
            Run("combat flail combo scoped takeover suppresses and restores right click", ref failed, CombatFlailComboScopedTakeoverSuppressesAndRestoresRightClick);
            Run("combat flail combo world right click guard allows raw right click intent", ref failed, CombatFlailComboWorldRightClickGuardAllowsRawRightClickIntent);
            Run("combat flail combo allows plain inventory open", ref failed, CombatFlailComboAllowsPlainInventoryOpen);
            Run("combat flail combo yields to adjacent scoped use", ref failed, CombatFlailComboYieldsToAdjacentScopedUse);
            Run("ItemCheck writer arbiter prioritizes bridge over combat writers", ref failed, ItemCheckWriterArbiterPrioritizesBridgeOverCombatWriters);
            Run("ItemCheck writer arbiter selects single world automation writer", ref failed, ItemCheckWriterArbiterSelectsSingleWorldAutomationWriter);
            Run("ItemCheck writer arbiter owns phaseblade quick switch after adjacent writers", ref failed, ItemCheckWriterArbiterOwnsPhasebladeQuickSwitchAfterAdjacentWriters);
            Run("combat aim flail release yields to active ItemCheck writer", ref failed, CombatAimFlailReleaseYieldsToActiveItemCheckWriter);
            Run("world automation fairness coordinator rotates runtime winners", ref failed, WorldAutomationFairnessCoordinatorRotatesRuntimeWinners);
            Run("combat flail combo diagnostics record scoped decision", ref failed, CombatFlailComboDiagnosticsRecordScopedDecision);
            Run("combat flail combo release remembers flail aim tail", ref failed, CombatFlailComboReleaseRemembersFlailAimTail);
            Run("combat flail combo press aim feeds release tail", ref failed, CombatFlailComboPressAimFeedsReleaseTail);
            Run("combat goblin execution allows only tinkerer when enabled", ref failed, CombatGoblinExecutionAllowsOnlyTinkererWhenEnabled);
            Run("travel menu diagnostics clone keeps scoped hook fields", ref failed, TravelMenuDiagnosticsCloneKeepsScopedHookFields);
            Run("travel menu ItemCheck guard suppresses world use and restores click", ref failed, TravelMenuItemCheckGuardSuppressesWorldUseAndRestoresClick);
            Run("travel menu CreativeUI world input guard does not override mouse state", ref failed, TravelMenuCreativeUiWorldInputGuardDoesNotOverrideMouseState);
            Run("travel menu CreativeUI world input guard skips native journey scope", ref failed, TravelMenuCreativeUiWorldInputGuardSkipsNativeJourneyScope);
            Run("travel menu CreativeUI world input guard requires inventory", ref failed, TravelMenuCreativeUiWorldInputGuardRequiresInventory);
            Run("travel menu CreativeUI Draw restore preserves vanilla mouse interface", ref failed, TravelMenuCreativeUiDrawRestorePreservesVanillaMouseInterface);
            Run("travel menu CreativeUI Draw release pulse ignores other inventory buttons", ref failed, TravelMenuCreativeUiDrawReleasePulseIgnoresOtherInventoryButtons);
            Run("travel menu CreativeUI input bypass clears using item gate", ref failed, TravelMenuCreativeUiInputBypassClearsUsingItemGate);
            Run("travel menu CreativeUI release pulse is once per mouse hold", ref failed, TravelMenuCreativeUiReleasePulseIsOncePerMouseHold);
            Run("travel menu reads godmode power fallback from creative flag", ref failed, TravelMenuGodmodePowerReadUsesCreativeFlagFallback);
            Run("travel menu reports disabled godmode power fallback from creative flag", ref failed, TravelMenuGodmodePowerReadDisabledWhenCreativeFlagOff);
            Run("travel menu state store recovers active marker from backup", ref failed, TravelMenuStateStoreRecoversActiveMarkerFromBackup);
            Run("channel arbiter acquires and releases a lease", ref failed, ChannelArbiterAcquireRelease);
            Run("channel arbiter blocks conflicting channels", ref failed, ChannelArbiterBlocksConflicts);
            Run("channel arbiter allows non-conflicting channels", ref failed, ChannelArbiterAllowsNonConflicting);
            Run("try enqueue accepts normal request", ref failed, TryEnqueueAcceptsNormalRequest);
            Run("try enqueue rejects duplicate pending admission key", ref failed, TryEnqueueRejectsDuplicatePendingAdmissionKey);
            Run("try enqueue rejects duplicate running admission key", ref failed, TryEnqueueRejectsDuplicateRunningAdmissionKey);
            Run("try enqueue supersedes pending user request", ref failed, TryEnqueueSupersedesPendingUserRequest);
            Run("try enqueue coalesces pending background request", ref failed, TryEnqueueCoalescesPendingBackgroundRequest);
            Run("try enqueue user request supersedes background pending", ref failed, TryEnqueueUserRequestSupersedesBackgroundPending);
            Run("input action queue finds terminal result by request id", ref failed, InputActionQueueFindsTerminalResultByRequestId);
            Run("cleanup lease blocks same resource admission", ref failed, CleanupLeaseBlocksSameResourceAdmission);
            Run("try enqueue reports item use bridge busy", ref failed, TryEnqueueReportsItemUseBridgeBusy);
            Run("try enqueue bridge busy keeps pending count unchanged", ref failed, TryEnqueueBridgeBusyKeepsPendingCountUnchanged);
            Run("try enqueue snapshot records denied admission details", ref failed, TryEnqueueSnapshotRecordsDeniedAdmissionDetails);
            Run("try enqueue derives queue expiration", ref failed, TryEnqueueDerivesQueueExpiration);
            Run("try enqueue allows distinct empty-source default keys", ref failed, TryEnqueueAllowsDistinctEmptySourceDefaultKeys);
            Run("legacy enqueue still accepts while channel busy", ref failed, LegacyEnqueueStillAcceptsWhileChannelBusy);
            Run("legacy enqueue records direct entry diagnostics", ref failed, LegacyEnqueueRecordsDirectEntryDiagnostics);
            Run("pending queue timeout expires before start", ref failed, PendingQueueTimeoutExpiresBeforeStart);
            Run("pending expiration does not cancel executor", ref failed, PendingExpirationDoesNotCancelExecutor);
            Run("pending queue timeout expires while running", ref failed, PendingQueueTimeoutExpiresWhileRunning);
            Run("pending expiration while running does not start or cancel pending executor", ref failed, PendingExpirationWhileRunningDoesNotStartOrCancelPendingExecutor);
            Run("running lease survives pending expiration", ref failed, RunningLeaseSurvivesPendingExpiration);
            Run("scheduler keeps priority then created order", ref failed, SchedulerKeepsPriorityThenCreatedOrder);
            Run("scheduler prefers user bucket over earlier background", ref failed, SchedulerPrefersUserBucketOverEarlierBackground);
            Run("pending lower priority same channel does not block admission", ref failed, PendingLowerPrioritySameChannelDoesNotBlockAdmission);
            Run("simulated jump request has queue timeout", ref failed, SimulatedJumpRequestHasQueueTimeout);
            Run("continuous dash request has queue timeout", ref failed, ContinuousDashRequestHasQueueTimeout);
            Run("auto facing request has queue timeout", ref failed, AutoFacingRequestHasQueueTimeout);
            Run("combat aim diagnostics metadata keeps stable field names", ref failed, CombatAimDiagnosticsMetadataKeepsStableFieldNames);
            Run("combat aim projectile profile resolves bow and arrow", ref failed, CombatAimProjectileProfileResolvesBowAndArrow);
            Run("combat aim projectile profile applies quiver and archery speed", ref failed, CombatAimProjectileProfileAppliesQuiverAndArcherySpeed);
            Run("combat aim projectile profile keeps gunProj weapon speed", ref failed, CombatAimProjectileProfileKeepsGunProjWeaponSpeed);
            Run("combat aim projectile profile resolves specific launcher mapping", ref failed, CombatAimProjectileProfileResolvesSpecificLauncherMapping);
            Run("combat aim projectile profile carries effective extra updates", ref failed, CombatAimProjectileProfileCarriesEffectiveExtraUpdates);
            Run("combat aim target motion profile classifies stable linear", ref failed, CombatAimTargetMotionProfileClassifiesStableLinear);
            Run("combat aim target motion profile resets on teleport", ref failed, CombatAimTargetMotionProfileResetsOnTeleport);
            Run("combat aim target motion profile marks aiStyle1 jumping grounded", ref failed, CombatAimTargetMotionProfileMarksAiStyleOneGrounded);
            Run("combat aim target motion profile tick gap avoids huge measured velocity", ref failed, CombatAimTargetMotionProfileTickGapAvoidsHugeMeasuredVelocity);
            Run("combat aim target motion profile clamps acceleration", ref failed, CombatAimTargetMotionProfileClampsAcceleration);
            Run("combat aim ballistic solver uses point solver for beams", ref failed, CombatAimBallisticSolverUsesPointSolverForBeams);
            Run("combat aim ballistic solver uses short lead for homing", ref failed, CombatAimBallisticSolverUsesShortLeadForHoming);
            Run("combat aim ballistic solver keeps high speed lead short", ref failed, CombatAimBallisticSolverKeepsHighSpeedLeadShort);
            Run("combat aim ballistic solver allows trusted slow projectile lead", ref failed, CombatAimBallisticSolverAllowsTrustedSlowProjectileLead);
            Run("combat aim ballistic solver clamps slow projectile low trust lead", ref failed, CombatAimBallisticSolverClampsSlowProjectileLowTrustLead);
            Run("combat aim ballistic solver uses gravity profile inputs", ref failed, CombatAimBallisticSolverUsesGravityProfileInputs);
            Run("combat aim ballistic solver classifies returning outbound", ref failed, CombatAimBallisticSolverClassifiesReturningOutbound);
            Run("combat aim ballistic solver classifies spread coverage", ref failed, CombatAimBallisticSolverClassifiesSpreadCoverage);
            Run("combat aim ballistic solver falls back without projectile", ref failed, CombatAimBallisticSolverFallsBackWithoutProjectile);
            Run("combat aim predicted sampler leads small moving hitbox", ref failed, CombatAimPredictedSamplerLeadsSmallMovingHitbox);
            Run("combat aim predicted sampler chooses visible large hitbox sample", ref failed, CombatAimPredictedSamplerChoosesVisibleLargeHitboxSample);
            Run("combat aim predicted sampler keeps low confidence current", ref failed, CombatAimPredictedSamplerKeepsLowConfidenceCurrent);
            Run("combat aim predicted sampler keeps center over nearest", ref failed, CombatAimPredictedSamplerKeepsCenterOverNearest);
            Run("combat aim decision cache reuses attack selection within TTL", ref failed, CombatAimDecisionCacheReusesAttackSelectionWithinTtl);
            Run("combat aim decision cache expires stale selection", ref failed, CombatAimDecisionCacheExpiresStaleSelection);
            Run("combat aim cached selection validation rejects stale target", ref failed, CombatAimCachedSelectionValidationRejectsStaleTarget);
            Run("combat aim selector explains marker attack mismatch", ref failed, CombatAimSelectorExplainsMarkerAttackMismatch);
            Run("flail diagnostics publisher keeps metadata field names", ref failed, FlailDiagnosticsPublisherKeepsMetadataFieldNames);
            Run("flail diagnostics publisher suppresses duplicate inactive snapshots", ref failed, FlailDiagnosticsPublisherSuppressesDuplicateInactiveSnapshots);
            Run("combat aim weapon family resolver classifies requested families", ref failed, CombatAimWeaponFamilyResolverClassifiesRequestedFamilies);
            Run("combat aim weapon family diagnostics emits metadata fields", ref failed, CombatAimWeaponFamilyDiagnosticsEmitsMetadataFields);
            Run("combat aim skip reasons normalize to stable strings", ref failed, CombatAimSkipReasonsNormalizeToStableStrings);
            Run("persistent cursor policy rejects ordinary projectile weapons", ref failed, PersistentCursorPolicyRejectsOrdinaryProjectileWeapons);
            Run("persistent cursor policy allows channel projectile scoped only", ref failed, PersistentCursorPolicyAllowsChannelProjectileScopedOnly);
            Run("persistent cursor policy allows special projectile scoped only", ref failed, PersistentCursorPolicyAllowsSpecialProjectileScopedOnly);
            Run("persistent cursor policy rejects placement summons and sentries", ref failed, PersistentCursorPolicyRejectsPlacementSummonsAndSentries);
            Run("persistent cursor policy preserves yoyo eligibility", ref failed, PersistentCursorPolicyPreservesYoyoEligibility);
            Run("projectile cursor match accepts only local channel projectile", ref failed, ProjectileCursorMatchAcceptsOnlyLocalChannelProjectile);
            Run("projectile cursor match accepts only local special weapon projectile", ref failed, ProjectileCursorMatchAcceptsOnlyLocalSpecialWeaponProjectile);
            Run("projectile cursor match accepts only local flail release projectile", ref failed, ProjectileCursorMatchAcceptsOnlyLocalFlailReleaseProjectile);
            Run("combat aim scoped cursor diagnostics keeps ownership fields", ref failed, CombatAimScopedCursorDiagnosticsKeepsOwnershipFields);
            Run("flail policy only accepts non-yoyo channel aiStyle 15", ref failed, FlailPolicyOnlyAcceptsNonYoyoChannelAiStyle15);
            Run("flail update disabled stops before local player read", ref failed, FlailUpdateDisabledStopsBeforeLocalPlayerRead);
            Run("flail update UI blocked stops before weapon profile", ref failed, FlailUpdateUiBlockedStopsBeforeWeaponProfile);
            Run("flail update idle stops before weapon profile", ref failed, FlailUpdateIdleStopsBeforeWeaponProfile);
            Run("flail control preserves hold spin and releases on physical release", ref failed, FlailControlPreservesHoldSpinAndReleasesOnPhysicalRelease);
            Run("flail release state machine keeps stable reasons", ref failed, FlailReleaseStateMachineKeepsStableReasons);
            Run("flail ItemCheck takeover skips hold spin", ref failed, FlailItemCheckTakeoverSkipsHoldSpin);
            Run("flail ReleaseHold ItemCheck takeover arms projectile tail before runtime update", ref failed, FlailReleaseHoldItemCheckTakeoverArmsProjectileTailBeforeRuntimeUpdate);
            Run("flail ItemCheck takeover applies physical release scope", ref failed, FlailItemCheckTakeoverAppliesPhysicalReleaseScope);
            Run("flail stuck projectile retries release after physical release", ref failed, FlailStuckProjectileRetriesReleaseAfterPhysicalRelease);
            Run("flail projectile tracker accepts only local active friendly flail", ref failed, FlailProjectileTrackerAcceptsOnlyLocalActiveFriendlyFlail);
            Run("flail projectile tracker keeps non expected fallback", ref failed, FlailProjectileTrackerKeepsNonExpectedFallback);
            Run("flail hit cache resets on projectile identity change", ref failed, FlailHitCacheResetsOnProjectileIdentityChange);
            Run("flail stuck tracking reaches recovery tick", ref failed, FlailStuckTrackingReachesRecoveryTick);
            Run("flail TileCollision detector fails closed and caches MethodInfo", ref failed, FlailTileCollisionDetectorFailsClosedAndCachesMethodInfo);
            Run("flail cached release aims after target selection loss", ref failed, FlailCachedReleaseAimsAfterTargetSelectionLoss);
            Run("flail cached release aim respects age and profile bounds", ref failed, FlailCachedReleaseAimRespectsAgeAndProfileBounds);
            Run("flail release cursor tail keeps ProjectileAI scoped aim", ref failed, FlailReleaseCursorTailKeepsProjectileAiScopedAim);
            Run("flail combo press aim expires after tail window", ref failed, FlailComboPressAimExpiresAfterTailWindow);
            Run("flail cached release rejects yoyo and normal channel", ref failed, FlailCachedReleaseRejectsYoyoAndNormalChannel);
            Run("special projectile rules distinguish weapon and ammo projectiles", ref failed, SpecialProjectileRulesDistinguishWeaponAndAmmoProjectiles);
            Run("special dual projectile rejects Vortex ammo bullet scoped cursor", ref failed, SpecialDualProjectileRejectsVortexAmmoBulletScopedCursor);
            Run("special dual projectile matches Vortex controller and rocket scoped cursor", ref failed, SpecialDualProjectileMatchesVortexControllerAndRocketScopedCursor);
            Run("Onyx Blaster stays ItemCheck spread path without special scoped cursor", ref failed, OnyxBlasterStaysItemCheckSpreadPathWithoutSpecialScopedCursor);
            Run("ordinary shotgun family stays out of special projectile scoped cursor", ref failed, OrdinaryShotgunFamilyStaysOutOfSpecialProjectileScopedCursor);
            Run("special projectile tail keeps scoped aim after use window", ref failed, SpecialProjectileTailKeepsScopedAimAfterUseWindow);
            Run("special projectile tail matches Xenopopper bubble scoped cursor", ref failed, SpecialProjectileTailMatchesXenopopperBubbleScopedCursor);
            Run("special projectile tail uses Xenopopper bubble ProjectileKill scoped cursor", ref failed, SpecialProjectileTailUsesXenopopperBubbleProjectileKillScopedCursor);
            Run("special projectile tail active bubble refreshes fixed tail window", ref failed, SpecialProjectileTailActiveBubbleRefreshesFixedTailWindow);
            Run("special projectile tail uses recomputed aim after target moves", ref failed, SpecialProjectileTailUsesRecomputedAimAfterTargetMoves);
            Run("special dual projectile tail recomputes aim for moving assist target", ref failed, SpecialDualProjectileTailRecomputesAimForMovingAssistTarget);
            Run("special projectile tail expires inactive bubble and ignores ammo bullet", ref failed, SpecialProjectileTailExpiresInactiveBubbleAndIgnoresAmmoBullet);
            Run("combat aim itemcheck log throttle keeps independent keys", ref failed, CombatAimItemCheckLogThrottleKeepsIndependentKeys);
            Run("release hold pending expiration clears state before held input check", ref failed, ReleaseHoldPendingExpirationClearsStateBeforeHeldInputCheck);
            Run("release hold target dummy validation respects track dummy", ref failed, ReleaseHoldTargetDummyValidationRespectsTrackDummy);
            Run("input action queue releases channel after terminal start", ref failed, InputActionQueueReleasesChannelAfterTerminalStart);
            Run("input action queue releases channel after start exception", ref failed, InputActionQueueReleasesChannelAfterStartException);
            Run("input action queue releases channel after cancel", ref failed, InputActionQueueReleasesChannelAfterCancel);
            Run("input action queue clear releases pending and running leases", ref failed, InputActionQueueClearReleasesPendingAndRunningLeases);
            Run("diagnostic noop does not acquire channel lease", ref failed, DiagnosticNoopDoesNotAcquireChannelLease);
            Run("fishing bobber observer selects latest observation", ref failed, FishingBobberObserverSelectsLatestObservation);
            Run("fishing bobber observer tie-breaks by WhoAmI", ref failed, FishingBobberObserverTieBreaksByWhoAmI);
            Run("fishing bobber observer remove missing rebuilds latest", ref failed, FishingBobberObserverRemoveMissingRebuildsLatest);
            Run("fishing bobber observer empty scan clears observations", ref failed, FishingBobberObserverEmptyScanClearsObservations);
            Run("information fishing bobber uses fresh observer", ref failed, InformationFishingBobberUsesFreshObserver);
            Run("fishing fallback scan gate skips fresh hook observations", ref failed, FishingFallbackScanGateSkipsFreshHookObservations);
            Run("fishing fallback scan gate skips fresh inactive observer", ref failed, FishingFallbackScanGateSkipsFreshInactiveObserver);
            Run("fishing fallback scan gate keeps old fallback for sensitive stages", ref failed, FishingFallbackScanGateKeepsOldFallbackForSensitiveStages);
            Run("fishing session waits for bobber liquid", ref failed, FishingSessionWaitsForBobberLiquid);
            Run("fishing session damage exit compares life drop", ref failed, FishingSessionDamageExitComparesLifeDrop);
            Run("fishing filter special rules respect opposite list overrides", ref failed, FishingFilterSpecialRulesRespectOppositeListOverrides);
            Run("fishing filter skip holds selection until bobber gone", ref failed, FishingFilterSkipHoldsSelectionUntilBobberGone);
            Run("fishing filter natural wait does not force timeout pull", ref failed, FishingFilterNaturalWaitDoesNotForceTimeoutPull);
            Run("fishing filter natural wait clears after bite expires", ref failed, FishingFilterNaturalWaitClearsAfterBiteExpires);
            Run("fishing filter cut rod skip keeps timeout protection", ref failed, FishingFilterCutRodSkipKeepsTimeoutProtection);
            Run("fishing auto equipment water skips lava hook and covered parts", ref failed, FishingAutoEquipmentWaterSkipsLavaHookAndCoveredParts);
            Run("fishing auto equipment lava prefers lavaproof bag over hook", ref failed, FishingAutoEquipmentLavaPrefersLavaproofBagOverHook);
            Run("fishing auto equipment keeps stackable tackle bags", ref failed, FishingAutoEquipmentKeepsStackableTackleBags);
            Run("fishing auto equipment lava uses hook without lavaproof bag", ref failed, FishingAutoEquipmentLavaUsesHookWithoutLavaproofBag);
            Run("fishing auto equipment deduplicates luck and bobbers", ref failed, FishingAutoEquipmentDeduplicatesLuckAndBobbers);
            Run("fishing auto equipment capacity keeps highest scores", ref failed, FishingAutoEquipmentCapacityKeepsHighestScores);
            Run("fishing auto equipment replaces covered lower accessory first", ref failed, FishingAutoEquipmentReplacesCoveredLowerAccessoryFirst);
            Run("fishing auto equipment manual inventory interaction restores after bobber gone", ref failed, FishingAutoEquipmentManualInventoryInteractionStopsKeepingRodAppliedWithoutBobber);
            Run("fishing auto equipment restore keeps pending when original moved", ref failed, FishingAutoEquipmentRestoreKeepsPendingWhenOriginalMovedByUser);
            Run("fishing auto equipment restore completes when original already back", ref failed, FishingAutoEquipmentRestoreCompletesWhenOriginalAlreadyBack);
            Run("fishing filter requires sonar buff", ref failed, FishingFilterRequiresSonarBuff);
            Run("enemy segment labels hide middle segments", ref failed, EnemySegmentLabelsHideMiddleSegments);
            Run("known worm segment labels use Terraria NPC roles", ref failed, KnownWormSegmentLabelsUseTerrariaNpcRoles);
            Run("skeleton merchant counts as information NPC label", ref failed, SkeletonMerchantCountsAsInformationNpcLabel);
            Run("enemy health label uses compact health line", ref failed, EnemyHealthLabelUsesCompactHealthLine);
            Run("enemy health label snapshot tracks life text", ref failed, EnemyHealthLabelSnapshotTracksLifeText);
            Run("information sign text all mode keeps vanilla line cap", ref failed, InformationSignTextAllModeKeepsVanillaLineCap);
            Run("information sign text line mode respects configured lines", ref failed, InformationSignTextLineModeRespectsConfiguredLines);
            Run("information sign text character mode truncates before wrapping", ref failed, InformationSignTextCharacterModeTruncatesBeforeWrapping);
            Run("information sign text centers each displayed line on sign", ref failed, InformationSignTextCentersEachDisplayedLineOnSign);
            Run("information sign text layout cache reuses wrapped lines", ref failed, InformationSignTextLayoutCacheReusesWrappedLines);
            Run("information page content height includes limit rows", ref failed, InformationPageContentHeightIncludesLimitRows);
            Run("UI clipped single line text avoids bottom stick", ref failed, UiClippedSingleLineTextAvoidsBottomStick);
            Run("information world context cache scopes status profile", ref failed, InformationWorldContextCacheScopesStatusProfile);
            Run("information overlay context profiles route status and world record", ref failed, InformationOverlayContextProfilesRouteStatusAndWorldRecord);
            Run("information status line cache tracks context identity", ref failed, InformationStatusLineCacheTracksContextIdentity);
            Run("information fishing status line builder keeps display rows", ref failed, InformationFishingStatusLineBuilderKeepsDisplayRows);
            Run("information status panel layout cache reuses prepared rows", ref failed, InformationStatusPanelLayoutCacheReusesPreparedRows);
            Run("information under vanilla UI anchor prefers map minimap", ref failed, InformationUnderVanillaUiAnchorPrefersMapMinimap);
            Run("information under vanilla UI anchor falls back through vanilla UI layers", ref failed, InformationUnderVanillaUiAnchorFallsBackThroughVanillaUiLayers);
            Run("information under vanilla UI insertion keeps dispatchers before anchor", ref failed, InformationUnderVanillaUiInsertionKeepsDispatchersBeforeAnchor);
            Run("information under vanilla UI anchor missing is safe", ref failed, InformationUnderVanillaUiAnchorMissingIsSafe);
            Run("legacy final MouseText guard runs after Mouse Over", ref failed, LegacyFinalMouseTextGuardRunsAfterMouseOver);
            Run("information world overlay routes information under vanilla UI and automining above", ref failed, InformationWorldOverlayRoutesInformationUnderVanillaUiAndAutoMiningAbove);
            Run("information status panel routes under vanilla UI and legacy main window stays above", ref failed, InformationStatusPanelRoutesUnderVanillaUiAndLegacyMainWindowStaysAbove);
            Run("UI text renderer fast path keeps safe fallbacks", ref failed, UiTextRendererFastPathKeepsSafeFallbacks);
            Run("UI text renderer font signature change clears caches", ref failed, UiTextRendererFontSignatureChangeClearsCaches);
            Run("information tombstone text defaults to red and splits tile type", ref failed, InformationTombstoneTextDefaultsToRedAndSplitsTileType);
            Run("information mana crystal highlight defaults to off and uses tile id", ref failed, InformationManaCrystalHighlightDefaultsToOffAndUsesTileId);
            Run("information tile access reads cached tile members", ref failed, InformationTileAccessReadsCachedTileMembers);
            Run("auto station buff cooldown fast skip avoids scan", ref failed, AutoStationBuffCooldownFastSkipAvoidsScan);
            Run("auto station buff active buff fast skip avoids scan", ref failed, AutoStationBuffActiveBuffFastSkipAvoidsScan);
            Run("station buff scan cache reuses reachable target", ref failed, StationBuffScanCacheReusesReachableTarget);
            Run("station buff tile read fallback preserves scan result", ref failed, StationBuffTileReadFallbackPreservesScanResult);
            Run("auto station buff requests active buff snapshot", ref failed, AutoStationBuffRequestsActiveBuffSnapshot);
            Run("information tile highlight cache signature tracks bounds settings and world", ref failed, InformationTileHighlightCacheSignatureTracksBoundsSettingsAndWorld);
            Run("information tile highlight cache keeps safety refresh", ref failed, InformationTileHighlightCacheKeepsSafetyRefresh);
            Run("information tile highlight scanner groups adjacent enabled tiles", ref failed, InformationTileHighlightScannerGroupsAdjacentEnabledTiles);
            Run("information fishing catch query key tracks environment", ref failed, InformationFishingCatchQueryKeyTracksEnvironment);
            Run("information fishing catch query key tracks full baseline fields", ref failed, InformationFishingCatchQueryKeyTracksFullBaselineFields);
            Run("information fishing catch early key tracks environment", ref failed, InformationFishingCatchEarlyKeyTracksEnvironment);
            Run("information fishing catch early cache hit skips heavy counters", ref failed, InformationFishingCatchEarlyCacheHitSkipsHeavyCounters);
            Run("information fishing catch caches keep configured limits", ref failed, InformationFishingCatchCachesKeepConfiguredLimits);
            Run("information fishing catch query cache hit backfills early cache", ref failed, InformationFishingCatchQueryCacheHitBackfillsEarlyCache);
            Run("information fishing catch reset clears caches and counters", ref failed, InformationFishingCatchResetClearsCachesAndCounters);
            Run("information fishing water penalty keeps source formula", ref failed, InformationFishingWaterPenaltyKeepsSourceFormula);
            Run("information fishing liquid kind keeps priority", ref failed, InformationFishingLiquidKindKeepsPriority);
            Run("information fishing lava capability reads environment", ref failed, InformationFishingLavaCapabilityReadsEnvironment);
            Run("information fishing water scan increments once per scan", ref failed, InformationFishingWaterScanIncrementsOncePerScan);
            Run("information fishing condition height rolls keep order", ref failed, InformationFishingConditionHeightRollsKeepOrder);
            Run("information fishing condition corruption rolls keep order", ref failed, InformationFishingConditionCorruptionRollsKeepOrder);
            Run("information fishing condition boolean rolls keep order", ref failed, InformationFishingConditionBooleanRollsKeepOrder);
            Run("information fishing condition enumeration keeps order and stops", ref failed, InformationFishingConditionEnumerationKeepsOrderAndStops);
            Run("information fishing condition junk disabled stays false", ref failed, InformationFishingConditionJunkDisabledStaysFalse);
            Run("information fishing context factory maps attempt spec", ref failed, InformationFishingContextFactoryMapsAttemptSpec);
            Run("information fish rule evaluator keeps order and deduplicates", ref failed, InformationFishRuleEvaluatorKeepsOrderAndDeduplicates);
            Run("information fish rule evaluator respects max catch items", ref failed, InformationFishRuleEvaluatorRespectsMaxCatchItems);
            Run("information fish rule evaluator caches meets conditions lookup", ref failed, InformationFishRuleEvaluatorCachesMeetsConditionsLookup);
            Run("information fishing global empty query keeps heavy counters idle", ref failed, InformationFishingGlobalEmptyQueryKeepsHeavyCountersIdle);
            Run("information fishing search helpers keep stable semantics", ref failed, InformationFishingSearchHelpersKeepStableSemantics);
            Run("information fishing item name resolver keeps cache boundaries", ref failed, InformationFishingItemNameResolverKeepsCacheBoundaries);
            Run("information fishing global search keeps result semantics", ref failed, InformationFishingGlobalSearchKeepsResultSemantics);
            Run("search query unknown item degrades cleanly", ref failed, SearchQueryUnknownItemDegradesCleanly);
            Run("search query candidates match names and IDs", ref failed, SearchQueryCandidatesMatchNamesAndIds);
            Run("search query builds crafting source summaries", ref failed, SearchQueryBuildsCraftingSourceSummaries);
            Run("search query indexes direct and recipe group usages", ref failed, SearchQueryIndexesDirectAndRecipeGroupUsages);
            Run("search query indexes direct shimmer relations", ref failed, SearchQueryIndexesDirectShimmerRelations);
            Run("search query empty facts stay empty", ref failed, SearchQueryEmptyFactsStayEmpty);
            Run("search query formats base value as coins", ref failed, SearchQueryFormatsBaseValueAsCoins);
            Run("search query basic facts use Chinese labels and placement values", ref failed, SearchQueryBasicFactsUseChineseLabelsAndPlacementValues);
            Run("search query basic panel height tracks column count", ref failed, SearchQueryBasicPanelHeightTracksColumnCount);
            Run("search query recipe layout wraps ingredients and keeps all rows", ref failed, SearchQueryRecipeLayoutWrapsIngredientsAndKeepsAllRows);
            Run("search query shimmer layout keeps all reverse sources", ref failed, SearchQueryShimmerLayoutKeepsAllReverseSources);
            Run("search query UI state selects candidate and clears", ref failed, SearchQueryUiStateSelectsCandidateAndClears);
            Run("search query UI candidate scroll keeps own viewport", ref failed, SearchQueryUiCandidateScrollKeepsOwnViewport);
            Run("search query page layout tracks UI state", ref failed, SearchQueryPageLayoutTracksUiState);
            Run("search query layout rhythm keeps sections consistent", ref failed, SearchQueryLayoutRhythmKeepsSectionsConsistent);
            Run("search query layout cache tracks result detail changes", ref failed, SearchQueryLayoutCacheTracksResultDetailChanges);
            Run("search query pick entry uses selection wording", ref failed, SearchQueryPickEntryUsesSelectionWording);
            Run("search query pick command starts pending selection and hides window", ref failed, SearchQueryPickCommandStartsPendingSelectionAndHidesWindow);
            Run("search query pick state waits release before arming", ref failed, SearchQueryPickStateWaitsReleaseBeforeArming);
            Run("search query pick runtime consumes left click and selects item", ref failed, SearchQueryPickRuntimeConsumesLeftClickAndSelectsItem);
            Run("search query pick runtime consumes after PlayerInput rewrite", ref failed, SearchQueryPickRuntimeConsumesAfterPlayerInputRewrite);
            Run("search query pick runtime uses pre-consume UI slot snapshot", ref failed, SearchQueryPickRuntimeUsesPreConsumeUiSlotSnapshot);
            Run("search query pick runtime blocks pre-consume empty UI slot", ref failed, SearchQueryPickRuntimeBlocksPreConsumeEmptyUiSlot);
            Run("search query pick target resolver uses UI item", ref failed, SearchQueryPickTargetResolverUsesUiItem);
            Run("search query pick target resolver uses world item", ref failed, SearchQueryPickTargetResolverUsesWorldItem);
            Run("search query pick target resolver uses tile item id", ref failed, SearchQueryPickTargetResolverUsesTileItemId);
            Run("search query pick target resolver uses wall item id", ref failed, SearchQueryPickTargetResolverUsesWallItemId);
            Run("search query pick runtime consumes failed target and restores", ref failed, SearchQueryPickRuntimeConsumesFailedTargetAndRestores);
            Run("search query pick runtime does not let world fallback race UI pending", ref failed, SearchQueryPickRuntimeDoesNotLetWorldFallbackRaceUiPending);
            Run("search query pick runtime uses frozen click for fallback", ref failed, SearchQueryPickRuntimeUsesFrozenClickForFallback);
            Run("search query pick runtime blocks world fallback on UI empty slot", ref failed, SearchQueryPickRuntimeBlocksWorldFallbackOnUiEmptySlot);
            Run("search query pick runtime waits delayed UI item", ref failed, SearchQueryPickRuntimeWaitsDelayedUiItem);
            Run("search query hover entry requires fresh hover snapshot", ref failed, SearchQueryHoverEntryRequiresFreshHoverSnapshot);
            Run("search query hover entry ignores fresh empty UI slot snapshot", ref failed, SearchQueryHoverEntryIgnoresFreshEmptyUiSlotSnapshot);
            Run("search query hover command selects current hover item and closes candidates", ref failed, SearchQueryHoverCommandSelectsCurrentHoverItemAndClosesCandidates);
            Run("search query related item command tracks history and closes candidates", ref failed, SearchQueryRelatedItemCommandTracksHistoryAndClosesCandidates);
            Run("information fishing diagnostics snapshot keeps stable field mapping", ref failed, InformationFishingDiagnosticsSnapshotKeepsStableFieldMapping);
            Run("information fishing bobber fresh inactive skips projectile fallback", ref failed, InformationFishingBobberFreshInactiveSkipsProjectileFallback);
            Run("legacy UI page layout cache ignores window position", ref failed, LegacyUiPageLayoutCacheIgnoresWindowPosition);
            Run("legacy UI page layout cache dirties on scroll size and state", ref failed, LegacyUiPageLayoutCacheDirtiesOnScrollSizeAndState);
            Run("legacy UI page layout cache dirties on font generation", ref failed, LegacyUiPageLayoutCacheDirtiesOnFontGeneration);
            Run("legacy potion grid fits six buttons per default buff pane", ref failed, LegacyPotionGridFitsSixButtonsPerDefaultBuffPane);
            Run("auto recovery item filter defaults allow all and toggles blocked", ref failed, AutoRecoveryItemFilterDefaultsAllowAllAndTogglesBlocked);
            Run("recovery potion selection skips blocked heal candidates", ref failed, RecoveryPotionSelectionSkipsBlockedHealCandidates);
            Run("recovery potion selection skips blocked mana candidates", ref failed, RecoveryPotionSelectionSkipsBlockedManaCandidates);
            Run("runtime settings snapshot carries recovery item filters", ref failed, RuntimeSettingsSnapshotCarriesRecoveryItemFilters);
            Run("feature catalog exposes recovery item config windows", ref failed, FeatureCatalogExposesRecoveryItemConfigWindows);
            Run("legacy items and misc content heights include bottom action rows", ref failed, LegacyItemsAndMiscContentHeightsIncludeBottomActionRows);
            Run("legacy map enhancement page layout tracks quick announcement state", ref failed, LegacyMapEnhancementPageLayoutTracksQuickAnnouncementState);
            Run("legacy map quick announcement button tooltips match requested wording", ref failed, LegacyMapQuickAnnouncementButtonTooltipsMatchRequestedWording);
            Run("map quick announcement hover snapshot tracks item slot freshness", ref failed, MapQuickAnnouncementHoverSnapshotTracksItemSlotFreshness);
            Run("map quick announcement item slot hook candidate summary covers forwarding overloads", ref failed, MapQuickAnnouncementItemSlotHookCandidateSummaryCoversForwardingOverloads);
            Run("map quick announcement hover snapshot read status distinguishes failure modes", ref failed, MapQuickAnnouncementHoverSnapshotReadStatusDistinguishesFailureModes);
            Run("map quick announcement resolver uses fresh UI hover snapshots", ref failed, MapQuickAnnouncementResolverUsesFreshUiHoverSnapshots);
            Run("map quick announcement stale hover snapshot falls back to tile", ref failed, MapQuickAnnouncementStaleHoverSnapshotFallsBackToTile);
            Run("map quick announcement placement names prefer item localization", ref failed, MapQuickAnnouncementPlacementNamesPreferItemLocalization);
            Run("map quick announcement multi-tile furniture styles resolve consistently", ref failed, MapQuickAnnouncementMultiTileFurnitureStylesResolveConsistently);
            Run("map quick announcement resolver prefers UI item over world targets", ref failed, MapQuickAnnouncementResolverPrefersUiItemOverWorldTargets);
            Run("map quick announcement resolver lists players and NPCs at mouse", ref failed, MapQuickAnnouncementResolverListsPlayersAndNpcsAtMouse);
            Run("map quick announcement resolver aggregates nearby dropped items", ref failed, MapQuickAnnouncementResolverAggregatesNearbyDroppedItems);
            Run("map quick announcement resolver combines tile and circuit layers", ref failed, MapQuickAnnouncementResolverCombinesTileAndCircuitLayers);
            Run("map quick announcement resolver treats liquid as tile layer", ref failed, MapQuickAnnouncementResolverTreatsLiquidAsTileLayer);
            Run("map quick announcement resolver uses wall before air", ref failed, MapQuickAnnouncementResolverUsesWallBeforeAir);
            Run("map quick announcement resolver falls back to air phrase", ref failed, MapQuickAnnouncementResolverFallsBackToAirPhrase);
            Run("map quick announcement text safety wraps color and blocks injection", ref failed, MapQuickAnnouncementTextSafetyWrapsColorAndBlocksInjection);
            Run("map quick announcement delivery honors cooldowns and prompt throttle", ref failed, MapQuickAnnouncementDeliveryHonorsCooldownsAndPromptThrottle);
            Run("map quick announcement delivery does not cooldown failed send", ref failed, MapQuickAnnouncementDeliveryDoesNotCooldownFailedSend);
            Run("map quick announcement runtime records recent diagnostics", ref failed, MapQuickAnnouncementRuntimeRecordsRecentDiagnostics);
            Run("map quick announcement runtime diagnostics explain target sources", ref failed, MapQuickAnnouncementRuntimeDiagnosticsExplainTargetSources);
            Run("map quick announcement runtime idle path keeps diagnostics cheap", ref failed, MapQuickAnnouncementRuntimeIdlePathDoesNotResolveOrRecordDiagnostics);
            Run("map quick announcement runtime records blocked trigger diagnostics", ref failed, MapQuickAnnouncementRuntimeRecordsBlockedTriggerDiagnostics);
            Run("diagnostic snapshot writes map quick announcement state", ref failed, DiagnosticSnapshotWritesMapQuickAnnouncementState);
            Run("legacy UI hover layout token ignores window and content position", ref failed, LegacyUiHoverLayoutTokenIgnoresWindowAndContentPosition);
            Run("legacy UI hover layout token dirties on page size scroll settings and font", ref failed, LegacyUiHoverLayoutTokenDirtiesOnPageSizeScrollSettingsAndFont);
            Run("legacy UI empty page prompt and item tab icon match current UI", ref failed, LegacyUiEmptyPagePromptAndItemTabIconMatchCurrentUi);
            Run("legacy UI tabs ignore content scroll clip", ref failed, LegacyUiTabsIgnoreContentScrollClip);
            Run("legacy UI selected button content offset requires enabled selection", ref failed, LegacyUiSelectedButtonContentOffsetRequiresEnabledSelection);
            Run("legacy UI retained frame model reuses window translation", ref failed, LegacyUiRetainedFrameModelReusesWindowTranslation);
            Run("legacy UI retained frame model dirties on scroll settings and font", ref failed, LegacyUiRetainedFrameModelDirtiesOnScrollSettingsAndFont);
            Run("legacy UI retained frame model falls back on element mismatch", ref failed, LegacyUiRetainedFrameModelFallsBackOnElementMismatch);
            Run("legacy UI element frame reuses pooled elements", ref failed, LegacyUiElementFrameReusesPooledElements);
            Run("legacy UI hover cache reuses stable hover id", ref failed, LegacyUiHoverCacheReusesStableHoverId);
            Run("legacy UI context hover uses cached element id", ref failed, LegacyUiContextHoverUsesCachedElementId);
            Run("legacy UI tooltip cache reuses stable hover model", ref failed, LegacyUiTooltipCacheReusesStableHoverModel);
            Run("legacy UI tooltip cache dirties on content change", ref failed, LegacyUiTooltipCacheDirtiesOnContentChange);
            Run("legacy combat aim radius status text reflects zero disabled", ref failed, LegacyCombatAimRadiusStatusTextReflectsZeroDisabled);
            Run("legacy UI overlay coordinator draws after page content", ref failed, LegacyUiOverlayCoordinatorDrawsAfterPageContent);
            Run("legacy UI overlay request rejects invalid contract", ref failed, LegacyUiOverlayRequestRejectsInvalidContract);
            Run("legacy UI overlay modal blocker stops lower hover and click", ref failed, LegacyUiOverlayModalBlockerStopsLowerHoverAndClick);
            Run("legacy UI overlay child controls beat modal blocker", ref failed, LegacyUiOverlayChildControlsBeatModalBlocker);
            Run("legacy UI overlay modal blocks main scroll", ref failed, LegacyUiOverlayModalBlocksMainScroll);
            Run("legacy UI overlay stack dirties hover token and retained frame", ref failed, LegacyUiOverlayStackDirtiesHoverTokenAndRetainedFrame);
            Run("legacy auto capture config overlay blocks lower hover and click", ref failed, LegacyAutoCaptureConfigOverlayBlocksLowerHoverAndClick);
            Run("legacy auto capture config overlay checkbox stays clickable", ref failed, LegacyAutoCaptureConfigOverlayCheckboxStaysClickable);
            Run("legacy information style overlay blocks lower hover and keeps button clickable", ref failed, LegacyInformationStyleOverlayBlocksLowerHoverAndKeepsButtonClickable);
            Run("legacy auto recovery config overlay blocks lower hover and keeps close clickable", ref failed, LegacyAutoRecoveryConfigOverlayBlocksLowerHoverAndKeepsCloseClickable);
            Run("legacy movement safe landing overlay blocks lower hover and keeps option clickable", ref failed, LegacyMovementSafeLandingOverlayBlocksLowerHoverAndKeepsOptionClickable);
            Run("legacy fishing picker overlay blocks lower hover and keeps nested scroll", ref failed, LegacyFishingPickerOverlayBlocksLowerHoverAndKeepsNestedScroll);
            Run("legacy fishing preset overlay blocks lower hover and keeps rows clickable", ref failed, LegacyFishingPresetOverlayBlocksLowerHoverAndKeepsRowsClickable);
            Run("legacy search candidate overlay blocks lower hover keeps rows clickable and consumes scroll", ref failed, LegacySearchCandidateOverlayBlocksLowerHoverKeepsRowsClickableAndConsumesScroll);
            Run("legacy UI action update gate skips idle frames", ref failed, LegacyUiActionUpdateGateSkipsIdleFrames);
            Run("legacy UI action update gate runs pending commands when hidden", ref failed, LegacyUiActionUpdateGateRunsPendingCommandsWhenHidden);
            Run("legacy UI action update gate skips drag dispatch without commands", ref failed, LegacyUiActionUpdateGateSkipsDragDispatchWithoutCommands);
            Run("legacy UI update prefix skips scroll snapshot when wheel idle", ref failed, LegacyUiUpdatePrefixSkipsScrollSnapshotWhenWheelIdle);
            Run("legacy UI scroll action event coalesces stable wheel diagnostics", ref failed, LegacyUiScrollActionEventCoalescesStableWheelDiagnostics);
            Run("legacy main UI scale keeps high UI scale when screen fits", ref failed, LegacyMainUiScaleKeepsHighUiScaleWhenScreenFits);
            Run("legacy main UI scale caps high UI scale only to screen fit", ref failed, LegacyMainUiScaleCapsHighUiScaleOnlyToScreenFit);
            Run("legacy main UI scale keeps sub-default UI scale", ref failed, LegacyMainUiScaleKeepsSubDefaultUiScale);
            Run("legacy main UI drag bounds keep title recoverable", ref failed, LegacyMainUiDragBoundsKeepTitleRecoverable);
            Run("UI draw transform scales rectangles and text scale", ref failed, UiDrawTransformScalesRectanglesAndTextScale);
            Run("diagnostic mouse state reader reuses snapshot within draw frame", ref failed, DiagnosticMouseStateReaderReusesSnapshotWithinDrawFrame);
            Run("diagnostic mouse state reader refreshes on new fast draw frame", ref failed, DiagnosticMouseStateReaderRefreshesOnNewFastDrawFrame);
            Run("diagnostic mouse state reader refreshes when draw frame changes under same update", ref failed, DiagnosticMouseStateReaderRefreshesWhenDrawFrameChangesUnderSameUpdate);
            Run("UI mouse capture service short-circuits within draw frame", ref failed, UiMouseCaptureServiceShortCircuitsWithinDrawFrame);
            Run("UI mouse capture service rewrites capture and suppress on next draw frame", ref failed, UiMouseCaptureServiceRewritesCaptureAndSuppressOnNextDrawFrame);
            Run("UI mouse capture service clears pending MouseText and NPC hover", ref failed, UiMouseCaptureServiceClearsPendingMouseTextAndNpcHover);
            Run("legacy MouseText guard suppresses inside F5 only", ref failed, LegacyMouseTextGuardSuppressesInsideF5Only);
            Run("combat performance caches stable metadata only", ref failed, CombatPerformanceCachesStableMetadataOnly);
            Run("runtime performance diagnostics records slowest operation", ref failed, RuntimePerformanceDiagnosticsRecordsSlowestOperation);
            Run("information overlay diagnostics writer preserves section counts", ref failed, InformationOverlayDiagnosticsWriterPreservesSectionCounts);
            Run("information NPC label snapshot reuses movement only", ref failed, InformationNpcLabelSnapshotReusesMovementOnly);
            Run("information chest labels cache signature changes with mode and player-world records", ref failed, InformationChestLabelsCacheSignatureChangesWithModeAndKnownKeys);
            Run("information chest always dirty cache tracks movement world and style", ref failed, InformationChestAlwaysDirtyCacheTracksMovementWorldAndStyle);
            Run("information chest always dirty cache keeps safe refresh", ref failed, InformationChestAlwaysDirtyCacheKeepsSafeRefresh);
            Run("information chest always cache counters ignore opened mode", ref failed, InformationChestAlwaysCacheCountersIgnoreOpenedMode);
            Run("information chest always typed scan diagnostics track fallback tiles", ref failed, InformationChestAlwaysTypedScanDiagnosticsTrackFallbackTiles);
            Run("information chest always name cache reuses across dirty scans", ref failed, InformationChestAlwaysNameCacheReusesAcrossDirtyScans);
            Run("information chest always partial scan publishes stable snapshots", ref failed, InformationChestAlwaysPartialScanPublishesStableSnapshots);
            Run("player-world behavior records isolate opened chests", ref failed, PlayerWorldBehaviorRecordsIsolateOpenedChests);
            Run("legacy opened chest keys migrate to current player-world only", ref failed, LegacyOpenedChestKeysMigrateToCurrentPlayerWorldOnly);
            Run("information chest key parsing survives world rename with same id", ref failed, InformationChestKeyParsingSurvivesWorldRenameWithSameId);
            Run("information chest tile fallback detects basic container ids", ref failed, InformationChestTileFallbackDetectsBasicContainerIds);
            Run("information chest tile fallback includes dressers and excludes display containers", ref failed, InformationChestTileFallbackIncludesDressersAndExcludesDisplayContainers);
            Run("information chest tile fallback normalizes two by two frame origin", ref failed, InformationChestTileFallbackNormalizesTwoByTwoFrameOrigin);
            Run("information dresser chest labels use three by two frame rules", ref failed, InformationDresserChestLabelsUseThreeByTwoFrameRules);
            Run("information dresser display name avoids map object option bleed", ref failed, InformationDresserDisplayNameAvoidsMapObjectOptionBleed);
            Run("information chest display name avoids map object option bleed", ref failed, InformationChestDisplayNameAvoidsMapObjectOptionBleed);
            Run("information chest labels frame limit allows dense rooms", ref failed, InformationChestLabelsFrameLimitAllowsDenseRooms);
            Run("information chest labels draw order prioritizes screen center", ref failed, InformationChestLabelsDrawOrderPrioritizesScreenCenter);
            Run("information chest label sort cache dirties on source and movement threshold", ref failed, InformationChestLabelSortCacheDirtiesOnSourceAndMovementThreshold);
            Run("information chest label cache cull covers bucket movement", ref failed, InformationChestLabelCacheCullCoversBucketMovement);
            Run("information luck breakdown follows Terraria source formula", ref failed, InformationLuckBreakdownFollowsTerrariaSourceFormula);
            Run("information luck breakdown wraps source details", ref failed, InformationLuckBreakdownWrapsSourceDetails);
            Run("combat equipment warning matches requested equipment names", ref failed, CombatEquipmentWarningMatchesRequestedEquipmentNames);
            Run("combat equipment warning prompts only on hazard entry", ref failed, CombatEquipmentWarningPromptsOnlyOnHazardEntry);
            Run("fishing filter nested scroll bubbles when list cannot move", ref failed, FishingFilterNestedScrollBubblesWhenListCannotMove);
            Run("legacy UI window capture accepts scaled screen coordinates", ref failed, LegacyUiWindowCaptureAcceptsScaledScreenCoordinates);

            if (failed == 0)
            {
                Console.WriteLine("All JueMingZ tests passed.");
                return 0;
            }

            Console.WriteLine("JueMingZ tests failed: " + failed);
            return 1;
        }

        private static void Run(string name, ref int failed, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS " + name);
            }
            catch (Exception error)
            {
                failed++;
                Console.WriteLine("FAIL " + name + ": " + error.Message);
            }
        }

    }
}

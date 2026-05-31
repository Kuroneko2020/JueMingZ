using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.Combat
{
    internal static class CombatEquipmentWarningService
    {
        private const string PromptText = "当前装备非战斗饰品";
        private const long HazardScanIntervalTicks = 12;
        private static readonly TimeSpan PromptReadableDuration = TimeSpan.FromSeconds(2d);
        private static readonly TimeSpan PromptFadeDuration = TimeSpan.FromSeconds(0.25d);
        private static readonly object SyncRoot = new object();
        private static readonly HashSet<string> NonCombatEquipmentNames = BuildNameSet();
        private static readonly HashSet<string> NonCombatEquipmentNamePrefixes = BuildNamePrefixSet();
        private static readonly Dictionary<int, string> ItemNameCache = new Dictionary<int, string>();
        private static readonly Dictionary<int, string> ItemInternalNameCache = new Dictionary<int, string>();
        private static string _lastHazardKey = string.Empty;
        private static string _text = string.Empty;
        private static DateTime _shownUtc = DateTime.MinValue;
        private static DateTime _expiresUtc = DateTime.MinValue;
        private static long _nextHazardScanTick;

        public static void Tick(GameStateSnapshot snapshot, RuntimeState runtimeState)
        {
            Tick(snapshot, runtimeState, RuntimeSettingsSnapshotProvider.GetCurrent());
        }

        internal static void Tick(GameStateSnapshot snapshot, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            try
            {
                settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
                if (!settingsSnapshot.CombatEquipmentWarningEnabled)
                {
                    ResetHazardState();
                    return;
                }

                if (snapshot == null || !snapshot.IsInWorld)
                {
                    ResetHazardState();
                    return;
                }

                var tick = runtimeState == null ? 0 : runtimeState.UpdateCount;
                if (!ShouldRunHazardScan(tick))
                {
                    return;
                }

                string hazardKey;
                if (!TryReadCombatHazard(out hazardKey))
                {
                    ClearHazardKey();
                    return;
                }

                var enteringHazard = ShouldPromptForHazardEntry(_lastHazardKey, hazardKey, true);
                _lastHazardKey = hazardKey;
                if (!enteringHazard)
                {
                    return;
                }

                int ignoredItemType;
                string ignoredItemName;
                if (!TryFindEquippedNonCombatItem(out ignoredItemType, out ignoredItemName))
                {
                    return;
                }

                Show(tick);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("CombatEquipmentWarningService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "combat-equipment-warning-tick-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatEquipmentWarningService",
                    "Combat equipment warning tick failed; exception swallowed.", error);
            }
        }

        public static bool TryGetPrompt(out string text, out double progress, out double alpha)
        {
            lock (SyncRoot)
            {
                text = _text;
                progress = 0d;
                alpha = 0d;
                if (string.IsNullOrWhiteSpace(_text))
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                if (now >= _expiresUtc)
                {
                    _text = string.Empty;
                    return false;
                }

                var elapsed = (now - _shownUtc).TotalMilliseconds;
                var readableDuration = Math.Max(1d, PromptReadableDuration.TotalMilliseconds);
                progress = Math.Max(0d, Math.Min(1d, elapsed / readableDuration));
                alpha = CalculatePromptAlpha(elapsed);
                return true;
            }
        }

        internal static bool IsNonCombatEquipmentNameForTesting(string name)
        {
            return IsNonCombatEquipmentName(name);
        }

        internal static bool IsCombatHazardForTesting(bool bossActive, bool nonBloodMoonEventActive)
        {
            return bossActive || nonBloodMoonEventActive;
        }

        internal static bool IsNonBloodMoonEventForTesting(
            int invasionType,
            bool pumpkinMoon,
            bool snowMoon,
            bool eclipse,
            bool slimeRain,
            bool dd2Ongoing,
            bool dd2ReadyToFindBartender,
            bool lunarApocalypse,
            bool towerSolar,
            bool towerVortex,
            bool towerNebula,
            bool towerStardust)
        {
            return invasionType > 0 ||
                   pumpkinMoon ||
                   snowMoon ||
                   eclipse ||
                   slimeRain ||
                   dd2Ongoing ||
                   lunarApocalypse ||
                   towerSolar ||
                   towerVortex ||
                   towerNebula ||
                   towerStardust;
        }

        internal static bool ShouldPromptForHazardEntryForTesting(string previousHazardKey, string currentHazardKey, bool hasNonCombatEquipment)
        {
            return ShouldPromptForHazardEntry(previousHazardKey, currentHazardKey, hasNonCombatEquipment);
        }

        internal static double PromptDurationSecondsForTesting
        {
            get { return PromptReadableDuration.TotalSeconds; }
        }

        internal static double PromptFadeDurationSecondsForTesting
        {
            get { return PromptFadeDuration.TotalSeconds; }
        }

        internal static double PromptTotalDurationSecondsForTesting
        {
            get { return (PromptReadableDuration + PromptFadeDuration).TotalSeconds; }
        }

        internal static long HazardScanIntervalTicksForTesting
        {
            get { return HazardScanIntervalTicks; }
        }

        internal static bool ShouldRunHazardScanForTesting(long tick, long nextHazardScanTick)
        {
            long ignoredNextTick;
            return ShouldRunHazardScan(tick, nextHazardScanTick, out ignoredNextTick);
        }

        internal static double CalculatePromptAlphaForTesting(double elapsedSeconds)
        {
            return CalculatePromptAlpha(Math.Max(0d, elapsedSeconds) * 1000d);
        }

        private static void Show(long tick)
        {
            lock (SyncRoot)
            {
                _text = PromptText;
                _shownUtc = DateTime.UtcNow;
                _expiresUtc = _shownUtc + PromptReadableDuration + PromptFadeDuration;
            }

            Logger.Info(
                "CombatEquipmentWarningService",
                "Combat equipment warning prompt shown; readableSeconds=" +
                PromptReadableDuration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) +
                ", fadeSeconds=" +
                PromptFadeDuration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) +
                ", tick=" +
                tick.ToString(CultureInfo.InvariantCulture) +
                ".");
        }

        private static double CalculatePromptAlpha(double elapsedMs)
        {
            if (elapsedMs <= PromptReadableDuration.TotalMilliseconds)
            {
                return 1d;
            }

            var fadeMs = Math.Max(1d, PromptFadeDuration.TotalMilliseconds);
            var fadeProgress = (elapsedMs - PromptReadableDuration.TotalMilliseconds) / fadeMs;
            return Math.Max(0d, Math.Min(1d, 1d - fadeProgress));
        }

        private static bool TryReadCombatHazard(out string hazardKey)
        {
            hazardKey = string.Empty;
            string bossKey;
            var bossActive = TryAnyActiveBoss(out bossKey);
            string eventKey;
            var eventActive = TryAnyNonBloodMoonEvent(out eventKey);
            if (!IsCombatHazardForTesting(bossActive, eventActive))
            {
                return false;
            }

            hazardKey = bossActive ? bossKey : eventKey;
            return true;
        }

        private static bool TryAnyActiveBoss(out string bossKey)
        {
            bossKey = string.Empty;
            var mainType = TerrariaRuntimeTypes.MainType;
            var npcs = GameStateReflection.AsList(GameStateReflection.GetStaticMember(mainType, "npc"));
            if (npcs == null)
            {
                return false;
            }

            for (var index = 0; index < npcs.Count; index++)
            {
                var npc = npcs[index];
                if (npc == null)
                {
                    continue;
                }

                bool active;
                bool boss;
                int life;
                if (GameStateReflection.TryGetBool(npc, "active", out active) &&
                    active &&
                    GameStateReflection.TryGetBool(npc, "boss", out boss) &&
                    boss &&
                    (!GameStateReflection.TryGetInt(npc, "life", out life) || life > 0))
                {
                    int type;
                    GameStateReflection.TryGetInt(npc, "type", out type);
                    bossKey = "boss:" + type.ToString(CultureInfo.InvariantCulture);
                    return true;
                }
            }

            return false;
        }

        private static bool TryAnyNonBloodMoonEvent(out string eventKey)
        {
            eventKey = string.Empty;
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return false;
            }

            var invasionType = ReadStaticInt(mainType, "invasionType");
            var pumpkinMoon = ReadStaticBool(mainType, "pumpkinMoon");
            var snowMoon = ReadStaticBool(mainType, "snowMoon");
            var eclipse = ReadStaticBool(mainType, "eclipse");
            var slimeRain = ReadStaticBool(mainType, "slimeRain");
            var dd2Ongoing = ReadStaticBool(FindType("Terraria.GameContent.Events.DD2Event"), "Ongoing");
            var npcType = FindType("Terraria.NPC");
            var lunarApocalypse = ReadStaticBool(npcType, "LunarApocalypseIsUp");
            var towerSolar = ReadStaticBool(npcType, "TowerActiveSolar");
            var towerVortex = ReadStaticBool(npcType, "TowerActiveVortex");
            var towerNebula = ReadStaticBool(npcType, "TowerActiveNebula");
            var towerStardust = ReadStaticBool(npcType, "TowerActiveStardust");

            if (!IsNonBloodMoonEventForTesting(
                    invasionType,
                    pumpkinMoon,
                    snowMoon,
                    eclipse,
                    slimeRain,
                    dd2Ongoing,
                    false,
                    lunarApocalypse,
                    towerSolar,
                    towerVortex,
                    towerNebula,
                    towerStardust))
            {
                return false;
            }

            eventKey = BuildEventKey(
                invasionType,
                pumpkinMoon,
                snowMoon,
                eclipse,
                slimeRain,
                dd2Ongoing,
                lunarApocalypse,
                towerSolar,
                towerVortex,
                towerNebula,
                towerStardust);
            return true;
        }

        private static string BuildEventKey(
            int invasionType,
            bool pumpkinMoon,
            bool snowMoon,
            bool eclipse,
            bool slimeRain,
            bool dd2Ongoing,
            bool lunarApocalypse,
            bool towerSolar,
            bool towerVortex,
            bool towerNebula,
            bool towerStardust)
        {
            if (invasionType > 0)
            {
                return "event:invasion:" + invasionType.ToString(CultureInfo.InvariantCulture);
            }

            if (pumpkinMoon) return "event:pumpkinMoon";
            if (snowMoon) return "event:snowMoon";
            if (eclipse) return "event:eclipse";
            if (slimeRain) return "event:slimeRain";
            if (dd2Ongoing) return "event:dd2";
            if (lunarApocalypse) return "event:lunarApocalypse";
            if (towerSolar) return "event:towerSolar";
            if (towerVortex) return "event:towerVortex";
            if (towerNebula) return "event:towerNebula";
            if (towerStardust) return "event:towerStardust";
            return "event:unknown";
        }

        private static bool ShouldPromptForHazardEntry(string previousHazardKey, string currentHazardKey, bool hasNonCombatEquipment)
        {
            return hasNonCombatEquipment &&
                   !string.IsNullOrWhiteSpace(currentHazardKey) &&
                   !string.Equals(previousHazardKey, currentHazardKey, StringComparison.Ordinal);
        }

        private static bool ShouldRunHazardScan(long tick)
        {
            long nextTick;
            var shouldRun = ShouldRunHazardScan(tick, _nextHazardScanTick, out nextTick);
            _nextHazardScanTick = nextTick;
            return shouldRun;
        }

        private static bool ShouldRunHazardScan(long tick, long nextHazardScanTick, out long nextTick)
        {
            nextTick = nextHazardScanTick;
            if (tick <= 0)
            {
                return true;
            }

            if (nextHazardScanTick > 0 && tick < nextHazardScanTick)
            {
                return false;
            }

            nextTick = tick + HazardScanIntervalTicks;
            return true;
        }

        private static void ResetHazardState()
        {
            _lastHazardKey = string.Empty;
            _nextHazardScanTick = 0;
        }

        private static void ClearHazardKey()
        {
            _lastHazardKey = string.Empty;
        }

        private static bool TryFindEquippedNonCombatItem(out int itemType, out string itemName)
        {
            itemType = 0;
            itemName = string.Empty;
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return false;
            }

            var armor = GameStateReflection.AsList(GameStateReflection.GetMember(player, "armor"));
            if (TryFindNonCombatItemInRange(armor, 0, 10, out itemType, out itemName))
            {
                return true;
            }

            var miscEquips = GameStateReflection.AsList(GameStateReflection.GetMember(player, "miscEquips"));
            return TryFindNonCombatItemInRange(miscEquips, 0, 5, out itemType, out itemName);
        }

        private static bool TryFindNonCombatItemInRange(IList items, int start, int endExclusive, out int itemType, out string itemName)
        {
            itemType = 0;
            itemName = string.Empty;
            if (items == null)
            {
                return false;
            }

            var end = Math.Min(items.Count, endExclusive);
            for (var index = Math.Max(0, start); index < end; index++)
            {
                var item = items[index];
                int stack;
                int type;
                if (item == null ||
                    !GameStateReflection.TryGetInt(item, "type", out type) ||
                    type <= 0 ||
                    (GameStateReflection.TryGetInt(item, "stack", out stack) && stack <= 0))
                {
                    continue;
                }

                var displayName = ResolveItemName(type);
                var internalName = ResolveItemInternalName(type);
                var itemFieldName = ReadItemName(item);
                if (IsNonCombatEquipmentName(displayName) ||
                    IsNonCombatEquipmentName(internalName) ||
                    IsNonCombatEquipmentName(itemFieldName))
                {
                    itemType = type;
                    itemName = FirstNonEmpty(displayName, itemFieldName, internalName, type.ToString(CultureInfo.InvariantCulture));
                    return true;
                }
            }

            return false;
        }

        private static bool IsNonCombatEquipmentName(string name)
        {
            var normalized = NormalizeName(name);
            if (normalized.Length <= 0)
            {
                return false;
            }

            if (NonCombatEquipmentNames.Contains(normalized))
            {
                return true;
            }

            foreach (var prefix in NonCombatEquipmentNamePrefixes)
            {
                if (normalized.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static HashSet<string> BuildNameSet()
        {
            var values = new[]
            {
                "远古凿子", "Ancient Chisel",
                "加长握爪", "Extendo Grip",
                "工具腰带", "Toolbelt",
                "工具箱", "Toolbox",
                "砌砖刀", "Brick Layer",
                "便携式水泥搅拌机", "Portable Cement Mixer",
                "喷漆器", "Paint Sprayer",
                "建筑师发明背包", "Architect Gizmo Pack",
                "自动安放器", "Presserator",
                "幽灵护目镜", "Spectre Goggles",
                "创造之手", "Hand Of Creation", "Hand of Creation",
                "FPV飞行眼镜", "FPV Goggles",
                "梯凳", "Step Stool",
                "宝藏磁石", "Treasure Magnet",
                "优质钓鱼线", "High Test Fishing Line",
                "钓具箱", "Tackle Box",
                "渔夫耳环", "Angler Earring",
                "渔夫渔具袋", "Angler Tackle Bag",
                "防熔岩钓钩", "Lavaproof Fishing Hook",
                "防熔岩渔具袋", "Lavaproof Tackle Bag",
                "钓鱼浮标", "Fishing Bobber",
                "发光钓鱼浮标", "Glowing Fishing Bobber",
                "熔岩苔藓钓鱼浮标", "Lava Moss Fishing Bobber",
                "氦苔藓钓鱼浮标", "Helium Moss Fishing Bobber",
                "氖苔藓钓鱼浮标", "Neon Moss Fishing Bobber",
                "氩苔藓钓鱼浮标", "Argon Moss Fishing Bobber",
                "氪苔藓钓鱼浮标", "Krypton Moss Fishing Bobber",
                "氙苔藓钓鱼浮标", "Xenon Moss Fishing Bobber",
                "铜表", "Copper Watch",
                "锡表", "Tin Watch",
                "银表", "Silver Watch",
                "钨表", "Tungsten Watch",
                "金表", "Gold Watch",
                "铂金表", "Platinum Watch",
                "深度计", "Depth Meter",
                "罗盘", "Compass",
                "全球定位系统", "GPS",
                "渔民袖珍宝典", "Fisherman's Pocket Guide",
                "天气收音机", "Weather Radio",
                "六分仪", "Sextant",
                "探鱼器", "Fish Finder",
                "金属探测器", "Metal Detector",
                "秒表", "Stopwatch",
                "每秒伤害计数器", "DPS Meter",
                "哥布林数据仪", "Goblin Tech",
                "生命体分析机", "Lifeform Analyzer",
                "杀怪计数器", "Tally Counter",
                "雷达", "Radar",
                "R.E.K.3000", "R.E.K. 3000", "REK 3000",
                "个人数字助手", "PDA",
                "机械标尺", "Mechanical Ruler",
                "机械透镜", "Mechanical Lens",
                "向导巫毒娃娃", "Guide Voodoo Doll",
                "服装商巫毒娃娃", "Clothier Voodoo Doll",
                "优惠卡", "Discount Card",
                "幸运币", "Lucky Coin",
                "金戒指", "Gold Ring",
                "钱币戒指", "Coin Ring",
                "贪婪戒指", "Greedy Ring",
                "花靴", "Flower Boots",
                "植物纤维绳索宝典", "Guide to Plant Fiber Cordage",
                "高尔夫球", "Golf Ball",
                "水母项链", "Jellyfish Necklace",
                "八音盒", "Music Box",
                "收音机", "Radio Thing", "Radio",
                "烈焰靴", "Flame Waker Boots",
                "渔夫帽", "Angler Hat",
                "渔夫背心", "Angler Vest",
                "渔夫裤", "Angler Pants",
                "挖矿头盔", "Mining Helmet",
                "超亮头盔", "Ultrabright Helmet",
                "夜视头盔", "Night Vision Helmet",
                "潜水头盔", "Diving Helmet",
                "挖矿衣", "Mining Shirt",
                "挖矿裤", "Mining Pants"
            };

            var set = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < values.Length; index++)
            {
                var normalized = NormalizeName(values[index]);
                if (normalized.Length > 0)
                {
                    set.Add(normalized);
                }
            }

            return set;
        }

        private static HashSet<string> BuildNamePrefixSet()
        {
            var values = new[]
            {
                "八音盒",
                "Music Box"
            };

            var set = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < values.Length; index++)
            {
                var normalized = NormalizeName(values[index]);
                if (normalized.Length > 0)
                {
                    set.Add(normalized);
                }
            }

            return set;
        }

        private static string NormalizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var chars = new List<char>(value.Length);
            for (var index = 0; index < value.Length; index++)
            {
                var ch = value[index];
                if (char.IsLetterOrDigit(ch))
                {
                    chars.Add(char.ToLowerInvariant(ch));
                }
            }

            return new string(chars.ToArray());
        }

        private static string ResolveItemName(int itemType)
        {
            if (itemType <= 0)
            {
                return string.Empty;
            }

            string cached;
            if (ItemNameCache.TryGetValue(itemType, out cached))
            {
                return cached;
            }

            var value = string.Empty;
            object raw;
            var langType = FindType("Terraria.Lang") ?? FindType("Terraria.Localization.Lang");
            if (TryInvokeStatic(langType, "GetItemNameValue", new object[] { itemType }, out raw))
            {
                value = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            ItemNameCache[itemType] = value;
            return value;
        }

        private static string ResolveItemInternalName(int itemType)
        {
            if (itemType <= 0)
            {
                return string.Empty;
            }

            string cached;
            if (ItemInternalNameCache.TryGetValue(itemType, out cached))
            {
                return cached;
            }

            var value = string.Empty;
            var itemIdType = FindType("Terraria.ID.ItemID");
            var search = GameStateReflection.GetStaticMember(itemIdType, "Search");
            object raw;
            if (TryInvokeInstance(search, "GetName", new object[] { itemType }, out raw))
            {
                value = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            ItemInternalNameCache[itemType] = value;
            return value;
        }

        private static string ReadItemName(object item)
        {
            var raw = GameStateReflection.GetMember(item, "Name") ??
                      GameStateReflection.GetMember(item, "HoverName") ??
                      GameStateReflection.GetMember(item, "name");
            return raw == null ? string.Empty : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static bool ReadStaticBool(Type type, string name)
        {
            var raw = GameStateReflection.GetStaticMember(type, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                return Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return false;
            }
        }

        private static int ReadStaticInt(Type type, string name)
        {
            var raw = GameStateReflection.GetStaticMember(type, name);
            if (raw == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private static bool TryInvokeStatic(Type type, string methodName, object[] args, out object value)
        {
            value = null;
            if (type == null || string.IsNullOrWhiteSpace(methodName))
            {
                return false;
            }

            try
            {
                var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (method == null)
                {
                    return false;
                }

                value = method.Invoke(null, args);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        private static bool TryInvokeInstance(object instance, string methodName, object[] args, out object value)
        {
            value = null;
            if (instance == null || string.IsNullOrWhiteSpace(methodName))
            {
                return false;
            }

            try
            {
                var method = instance.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method == null)
                {
                    return false;
                }

                value = method.Invoke(instance, args);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        private static Type FindType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            return TerrariaTypeCache.Find(fullName);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (var index = 0; values != null && index < values.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                {
                    return values[index];
                }
            }

            return string.Empty;
        }

    }
}

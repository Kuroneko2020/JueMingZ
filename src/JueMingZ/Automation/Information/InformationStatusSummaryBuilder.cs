using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Config;
using Terraria;

namespace JueMingZ.Automation.Information
{
    // Status summaries read the current context only; failed world/player facts become omitted lines, not actions.
    internal static class InformationStatusSummaryBuilder
    {
        private static readonly EnabledSummaryDescriptor[] EnabledSummaryDescriptors =
        {
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return settings.InformationEnemyNameLabelsEnabled; },
                delegate(AppSettings settings) { return "enemy"; }),
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return settings.InformationCritterNameLabelsEnabled; },
                delegate(AppSettings settings) { return "critter"; }),
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return !string.Equals(NormalizeNpcMode(settings.InformationNpcNameLabelsMode), "Off", StringComparison.OrdinalIgnoreCase); },
                delegate(AppSettings settings) { return "npc:" + NormalizeNpcMode(settings.InformationNpcNameLabelsMode); }),
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return !string.Equals(InformationChestLabelService.NormalizeMode(settings), InformationChestLabelService.ModeOff, StringComparison.OrdinalIgnoreCase); },
                delegate(AppSettings settings) { return "chest:" + InformationChestLabelService.NormalizeMode(settings); }),
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return !string.Equals(NormalizeSignTextMode(settings.InformationSignTextLabelsMode), InformationSignTextModes.Off, StringComparison.OrdinalIgnoreCase); },
                delegate(AppSettings settings) { return "signText:" + NormalizeSignTextMode(settings.InformationSignTextLabelsMode); }),
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return !string.Equals(NormalizeSignTextMode(settings.InformationTombstoneTextLabelsMode), InformationSignTextModes.Off, StringComparison.OrdinalIgnoreCase); },
                delegate(AppSettings settings) { return "tombstoneText:" + NormalizeSignTextMode(settings.InformationTombstoneTextLabelsMode); }),
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return settings.InformationHighlightLifeCrystalEnabled; },
                delegate(AppSettings settings) { return "lifeCrystal"; }),
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return settings.InformationHighlightManaCrystalEnabled; },
                delegate(AppSettings settings) { return "manaCrystal"; }),
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return settings.InformationHighlightDigtoiseEnabled; },
                delegate(AppSettings settings) { return "digtoise"; }),
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return settings.InformationHighlightLifeFruitEnabled; },
                delegate(AppSettings settings) { return "lifeFruit"; }),
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return settings.InformationHighlightDragonEggEnabled; },
                delegate(AppSettings settings) { return "dragonEgg"; }),
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return settings.InformationBiomeDisplayEnabled; },
                delegate(AppSettings settings) { return "biome"; }),
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return settings.InformationWorldInfectionEnabled; },
                delegate(AppSettings settings) { return "infection"; }),
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return settings.InformationLuckValueEnabled; },
                delegate(AppSettings settings) { return "luck"; }),
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return settings.InformationFishingCatchesEnabled; },
                delegate(AppSettings settings) { return "fishing"; }),
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return settings.InformationFishingFilteredCatchesEnabled; },
                delegate(AppSettings settings) { return "filteredFishing"; }),
            new EnabledSummaryDescriptor(
                delegate(AppSettings settings) { return settings.InformationAnglerQuestEnabled; },
                delegate(AppSettings settings) { return "angler"; })
        };

        internal static string BuildEnabledSummary(AppSettings settings)
        {
            if (settings == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            for (var index = 0; index < EnabledSummaryDescriptors.Length; index++)
            {
                var descriptor = EnabledSummaryDescriptors[index];
                if (descriptor.IsEnabled(settings))
                {
                    parts.Add(descriptor.BuildSummary(settings));
                }
            }

            return string.Join(",", parts.ToArray());
        }

        internal static void AddBiomeLine(ICollection<InformationStatusLine> lines, int order, InformationWorldContext context, AppSettings settings)
        {
            InformationStatusLineService.AddLine(
                lines,
                order,
                BuildBiomeLine(context == null ? null : context.LocalPlayer),
                InformationColorHelper.BiomeText(settings),
                InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.BiomeDisplayFeatureId));
        }

        internal static void AddWorldInfectionLine(ICollection<InformationStatusLine> lines, int order, InformationWorldContext context, AppSettings settings)
        {
            InformationStatusLineService.AddLine(
                lines,
                order,
                BuildWorldInfectionLine(context),
                InformationColorHelper.WorldInfectionText(settings),
                InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.WorldInfectionFeatureId));
        }

        internal static void AddLuckLines(ICollection<InformationStatusLine> lines, int order, InformationWorldContext context, AppSettings settings)
        {
            if (!HasSavedNpc(context, "savedWizard", "Wizard", 108))
            {
                return;
            }

            IList<string> luckLines;
            if (!InformationLuckBreakdownBuilder.TryBuildDisplayLines(context, out luckLines))
            {
                return;
            }

            var color = InformationColorHelper.LuckText(settings);
            var fontScale = InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.LuckValueFeatureId);
            for (var index = 0; index < luckLines.Count; index++)
            {
                InformationStatusLineService.AddLine(lines, order + index, luckLines[index], color, fontScale);
            }
        }

        internal static void AddAnglerQuestLine(ICollection<InformationStatusLine> lines, int order, InformationWorldContext context, AppSettings settings)
        {
            InformationStatusLineService.AddLine(
                lines,
                order,
                BuildAnglerQuestLine(context),
                InformationColorHelper.AnglerQuestText(settings),
                InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.AnglerQuestFeatureId));
        }

        private static string BuildBiomeLine(object player)
        {
            var zones = new List<string>();
            AddZone(zones, player, "ZoneDesert", "沙漠");
            AddZone(zones, player, "ZoneUndergroundDesert", "地下沙漠");
            AddZone(zones, player, "ZoneSnow", "雪原");
            AddZone(zones, player, "ZoneJungle", "丛林");
            AddZone(zones, player, "ZoneDungeon", "地牢");
            AddZone(zones, player, "ZoneBeach", "海洋");
            AddZone(zones, player, "ZoneCorrupt", "腐化");
            AddZone(zones, player, "ZoneCrimson", "猩红");
            AddZone(zones, player, "ZoneHallow", "神圣");
            AddZone(zones, player, "ZoneHoly", "神圣");
            AddZone(zones, player, "ZoneGlowshroom", "发光蘑菇");
            AddZone(zones, player, "ZoneMeteor", "陨石");
            AddZone(zones, player, "ZoneGranite", "花岗岩");
            AddZone(zones, player, "ZoneMarble", "大理石");
            AddZone(zones, player, "ZoneHive", "蜂巢");
            AddZone(zones, player, "ZoneLihzhardTemple", "神庙");
            AddZone(zones, player, "ZoneGraveyard", "墓地");

            if (HasZone(player, "ZoneSkyHeight"))
            {
                AddUnique(zones, "天空");
            }
            else if (HasZone(player, "ZoneUnderworldHeight"))
            {
                AddUnique(zones, "地狱");
            }
            else if (HasZone(player, "ZoneRockLayerHeight"))
            {
                AddUnique(zones, "洞穴");
            }
            else if (HasZone(player, "ZoneDirtLayerHeight") || HasZone(player, "ShoppingZone_BelowSurface"))
            {
                AddUnique(zones, "地下");
            }
            else if (HasZone(player, "ZoneOverworldHeight") && zones.Count <= 0)
            {
                AddUnique(zones, "森林");
            }

            return "群系: " + (zones.Count <= 0 ? "N/A" : string.Join(" / ", zones.ToArray()));
        }

        private static string BuildWorldInfectionLine(InformationWorldContext context)
        {
            if (!HasSavedNpc(context, "savedDryad", "Dryad", 20))
            {
                return string.Empty;
            }

            var worldGen = InformationReflection.FindType("Terraria.WorldGen");
            double good;
            double evil;
            double blood;
            var hasGood = TryReadStaticNumber(worldGen, "tGood", out good);
            var hasEvil = TryReadStaticNumber(worldGen, "tEvil", out evil);
            var hasBlood = TryReadStaticNumber(worldGen, "tBlood", out blood);
            if (!hasGood && !hasEvil && !hasBlood)
            {
                return string.Empty;
            }

            return "感染信息 神圣:" + FormatPercentLike(good) +
                   " 腐化:" + FormatPercentLike(evil) +
                   " 猩红:" + FormatPercentLike(blood);
        }

        private static string BuildAnglerQuestLine(InformationWorldContext context)
        {
            if (!HasSavedNpc(context, "savedAngler", "Angler", 369))
            {
                return string.Empty;
            }

            int questIndex;
            InformationReflection.TryReadStaticInt(context.MainType, "anglerQuest", out questIndex);
            var itemIds = InformationReflection.GetStaticMember(context.MainType, "anglerQuestItemNetIDs");
            var rawItem = InformationReflection.GetIndexedValue(itemIds, questIndex);
            var itemId = ToInt(rawItem, questIndex);
            var itemName = ResolveItemName(itemId);
            int finishedCount;
            InformationReflection.TryReadInt(context.LocalPlayer, "anglerQuestsFinished", out finishedCount);
            var line = "渔夫任务: " + (string.IsNullOrWhiteSpace(itemName) ? itemId.ToString(CultureInfo.InvariantCulture) : itemName) +
                       " / 完成:" + finishedCount.ToString(CultureInfo.InvariantCulture);
            var location = ResolveAnglerQuestLocation(context);
            if (!string.IsNullOrWhiteSpace(location))
            {
                line += " / 位置:" + location;
            }

            if (ReadAnglerQuestFinished(context))
            {
                line += " / 今日已交";
            }

            return line;
        }

        private static string ResolveAnglerQuestLocation(InformationWorldContext context)
        {
            var byItemText = ResolveAnglerQuestLocationFromItemText(context);
            if (!string.IsNullOrWhiteSpace(byItemText))
            {
                return byItemText;
            }

            object raw;
            var langType = InformationReflection.FindType("Terraria.Lang") ??
                           InformationReflection.FindType("Terraria.Localization.Lang");
            if (!InformationReflection.TryInvokeStatic(langType, "AnglerQuestChat", new object[] { false }, out raw) || raw == null)
            {
                return string.Empty;
            }

            return ExtractAnglerQuestLocation(Convert.ToString(raw, CultureInfo.InvariantCulture));
        }

        private static string ResolveAnglerQuestLocationFromItemText(InformationWorldContext context)
        {
            int questIndex;
            InformationReflection.TryReadStaticInt(context.MainType, "anglerQuest", out questIndex);
            var itemIds = InformationReflection.GetStaticMember(context.MainType, "anglerQuestItemNetIDs");
            var rawItem = InformationReflection.GetIndexedValue(itemIds, questIndex);
            var itemId = ToInt(rawItem, 0);
            if (itemId <= 0)
            {
                return string.Empty;
            }

            var internalName = ResolveItemInternalName(itemId);
            if (string.IsNullOrWhiteSpace(internalName))
            {
                return string.Empty;
            }

            var questText = ReadLocalizedText("AnglerQuestText." + internalName);
            if (string.IsNullOrWhiteSpace(questText) ||
                string.Equals(questText, "AnglerQuestText." + internalName, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return ExtractAnglerQuestLocation(questText);
        }

        private static string ExtractAnglerQuestLocation(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var location = ExtractAfterMarker(text, "抓捕位置：", "）");
            if (!string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            location = ExtractAfterMarker(text, "抓捕位置:", ")");
            if (!string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            location = ExtractAfterMarker(text, "抓捕地点：", "）");
            if (!string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            location = ExtractAfterMarker(text, "捕获地点：", "）");
            if (!string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            location = ExtractAfterMarker(text, "钓鱼地点：", "）");
            if (!string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            location = ExtractAfterMarker(text, "位置：", "）");
            if (!string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            location = ExtractAfterMarker(text, "Caught in ", ")");
            if (!string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            location = ExtractLastParenthesizedLocation(text);
            return location ?? string.Empty;
        }

        private static string ExtractAfterMarker(string text, string marker, string endMarker)
        {
            var start = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return string.Empty;
            }

            start += marker.Length;
            var end = text.IndexOf(endMarker, start, StringComparison.OrdinalIgnoreCase);
            if (end < 0)
            {
                end = text.IndexOf('\n', start);
            }

            if (end < 0)
            {
                end = text.Length;
            }

            return text.Substring(start, Math.Max(0, end - start)).Trim();
        }

        private static string ExtractLastParenthesizedLocation(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var start = Math.Max(text.LastIndexOf('（'), text.LastIndexOf('('));
            if (start < 0 || start >= text.Length - 1)
            {
                return string.Empty;
            }

            var end = text.IndexOfAny(new[] { '）', ')' }, start + 1);
            if (end < 0)
            {
                end = text.Length;
            }

            var value = text.Substring(start + 1, Math.Max(0, end - start - 1)).Trim();
            value = StripLocationPrefix(value);
            return value.Trim('（', '(', '）', ')', ' ', '\t', '。', '.', '，', ',', '、', '；', ';');
        }

        private static string StripLocationPrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var prefixes = new[]
            {
                "抓捕位置：", "抓捕位置:", "抓捕地点：", "抓捕地点:",
                "捕获地点：", "捕获地点:", "钓鱼地点：", "钓鱼地点:",
                "位置：", "位置:", "Caught in ", "caught in "
            };
            for (var index = 0; index < prefixes.Length; index++)
            {
                if (value.StartsWith(prefixes[index], StringComparison.OrdinalIgnoreCase))
                {
                    return value.Substring(prefixes[index].Length).Trim();
                }
            }

            return value.Trim();
        }

        private static void AddZone(ICollection<string> zones, object player, string member, string label)
        {
            bool value;
            if (InformationReflection.TryReadBool(player, member, out value) && value && !Contains(zones, label))
            {
                zones.Add(label);
            }
        }

        private static bool HasZone(object player, string member)
        {
            bool value;
            return InformationReflection.TryReadBool(player, member, out value) && value;
        }

        private static void AddUnique(ICollection<string> zones, string label)
        {
            if (!Contains(zones, label))
            {
                zones.Add(label);
            }
        }

        private static bool Contains(IEnumerable<string> values, string needle)
        {
            foreach (var value in values)
            {
                if (string.Equals(value, needle, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasSavedNpc(InformationWorldContext context, string savedField, string npcIdName, int fallbackNpcId)
        {
            if (context == null || context.MainType == null)
            {
                return false;
            }

            var npcType = InformationReflection.FindType("Terraria.NPC");
            bool saved;
            if (InformationReflection.TryReadStaticBool(npcType, savedField, out saved) && saved)
            {
                return true;
            }

            return AnyActiveNpcOfType(context.MainType, ReadNpcId(npcIdName, fallbackNpcId));
        }

        private static int ReadNpcId(string name, int fallback)
        {
            var npcIdType = InformationReflection.FindType("Terraria.ID.NPCID");
            int value;
            return InformationReflection.TryReadStaticInt(npcIdType, name, out value) ? value : fallback;
        }

        private static bool AnyActiveNpcOfType(Type mainType, int npcType)
        {
            try
            {
                var typedNpcs = TerrariaMainCompat.Npcs;
                for (var index = 0; typedNpcs != null && index < typedNpcs.Length; index++)
                {
                    var npc = typedNpcs[index];
                    if (TerrariaNpcReadCompat.IsActive(npc) && TerrariaNpcReadCompat.Type(npc) == npcType)
                    {
                        return true;
                    }
                }

                if (typedNpcs != null)
                {
                    return false;
                }
            }
            catch
            {
            }

            var npcs = InformationReflection.GetStaticMember(mainType, "npc");
            var count = GetCollectionCount(npcs);
            for (var index = 0; index < count; index++)
            {
                var npc = InformationReflection.GetIndexedValue(npcs, index);
                int type;
                if (npc != null && IsNpcActive(npc) && InformationReflection.TryReadInt(npc, "type", out type) && type == npcType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsNpcActive(object npc)
        {
            var typedNpc = npc as NPC;
            if (typedNpc != null)
            {
                return TerrariaNpcReadCompat.IsActive(typedNpc);
            }

            bool active;
            return InformationReflection.TryReadBool(npc, "active", out active) && active;
        }

        private static bool TryReadStaticNumber(Type type, string name, out double value)
        {
            value = 0d;
            var raw = InformationReflection.GetStaticMember(type, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string FormatPercentLike(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture) + "%";
        }

        private static bool ReadAnglerQuestFinished(InformationWorldContext context)
        {
            var raw = InformationReflection.GetStaticMember(context.MainType, "anglerQuestFinished");
            bool direct;
            if (TryConvertBool(raw, out direct))
            {
                return direct;
            }

            int myPlayer;
            InformationReflection.TryReadStaticInt(context.MainType, "myPlayer", out myPlayer);
            object indexed = InformationReflection.GetIndexedValue(raw, myPlayer);
            return TryConvertBool(indexed, out direct) && direct;
        }

        private static string ResolveItemName(int itemId)
        {
            if (itemId <= 0)
            {
                return string.Empty;
            }

            object raw;
            var langType = InformationReflection.FindType("Terraria.Lang") ??
                           InformationReflection.FindType("Terraria.Localization.Lang");
            if (InformationReflection.TryInvokeStatic(langType, "GetItemNameValue", new object[] { itemId }, out raw))
            {
                return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            return itemId.ToString(CultureInfo.InvariantCulture);
        }

        private static string ResolveItemInternalName(int itemId)
        {
            if (itemId <= 0)
            {
                return string.Empty;
            }

            try
            {
                var itemIdType = InformationReflection.FindType("Terraria.ID.ItemID");
                var search = InformationReflection.GetStaticMember(itemIdType, "Search");
                object raw;
                if (TryInvokeInstance(search, "GetName", new object[] { itemId }, out raw) && raw != null)
                {
                    return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string ReadLocalizedText(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            try
            {
                var languageType = InformationReflection.FindType("Terraria.Localization.Language");
                object raw;
                if (InformationReflection.TryInvokeStatic(languageType, "GetTextValue", new object[] { key }, out raw) && raw != null)
                {
                    return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static bool TryInvokeInstance(object instance, string methodName, object[] args, out object result)
        {
            result = null;
            if (instance == null || string.IsNullOrWhiteSpace(methodName))
            {
                return false;
            }

            try
            {
                var methods = instance.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                for (var index = 0; index < methods.Length; index++)
                {
                    var method = methods[index];
                    if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length != (args == null ? 0 : args.Length))
                    {
                        continue;
                    }

                    result = method.Invoke(instance, args);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static int GetCollectionCount(object source)
        {
            var collection = source as ICollection;
            if (collection != null)
            {
                return collection.Count;
            }

            var array = source as Array;
            return array == null ? 0 : array.Length;
        }

        private static int ToInt(object raw, int fallback)
        {
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool TryConvertBool(object raw, out bool value)
        {
            value = false;
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeNpcMode(string mode)
        {
            return string.IsNullOrWhiteSpace(mode) ? "Off" : mode.Trim();
        }

        private static string NormalizeSignTextMode(string mode)
        {
            return InformationSignTextModes.Normalize(mode);
        }

        private sealed class EnabledSummaryDescriptor
        {
            private readonly Func<AppSettings, bool> _isEnabled;
            private readonly Func<AppSettings, string> _buildSummary;

            public EnabledSummaryDescriptor(Func<AppSettings, bool> isEnabled, Func<AppSettings, string> buildSummary)
            {
                _isEnabled = isEnabled;
                _buildSummary = buildSummary;
            }

            public bool IsEnabled(AppSettings settings)
            {
                return _isEnabled != null && _isEnabled(settings);
            }

            public string BuildSummary(AppSettings settings)
            {
                return _buildSummary == null ? string.Empty : _buildSummary(settings);
            }
        }
    }
}

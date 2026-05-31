using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static class DebugUiLocalizationCompat
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly object SyncRoot = new object();
        private static readonly HashSet<int> WrappedTooltipElementIds = new HashSet<int>();

        private static readonly Dictionary<string, string> WorldGenPassNameTranslations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Terrain", "地形" },
            { "Skyblock", "空岛" },
            { "Dunes", "沙丘" },
            { "Ocean Sand", "海洋沙地" },
            { "Sand Patches", "沙地补丁" },
            { "Tunnels", "隧道" },
            { "Mount Caves", "山体洞穴" },
            { "Dirt Wall Backgrounds", "泥土墙背景" },
            { "Rocks In Dirt", "泥中岩石" },
            { "Dirt In Rocks", "岩中泥土" },
            { "Clay", "黏土" },
            { "Small Holes", "小洞" },
            { "Dirt Layer Caves", "泥层洞穴" },
            { "Rock Layer Caves", "岩层洞穴" },
            { "Surface Caves", "地表洞穴" },
            { "Wavy Caves", "波浪洞穴" },
            { "Generate Ice Biome", "生成雪地" },
            { "Grass", "草地" },
            { "Jungle", "丛林" },
            { "Mud Caves To Grass", "泥穴转草地" },
            { "Full Desert", "完整沙漠" },
            { "Mushroom Patches", "蘑菇补丁" },
            { "Marble", "大理石" },
            { "Granite", "花岗岩" },
            { "Floating Islands", "浮空岛" },
            { "Dirt To Mud", "泥土转淤泥" },
            { "Silt", "淤泥" },
            { "Shinies", "矿脉" },
            { "Webs", "蛛网" },
            { "Underworld", "地狱" },
            { "Corruption", "腐化/猩红" },
            { "Lakes", "湖泊" },
            { "Slush", "雪泥" },
            { "Dual Dungeons Dither Snake", "双地牢蛇形抖动" },
            { "Dungeon", "地牢" },
            { "Mountain Caves", "山体洞穴开口" },
            { "Beaches", "海滩" },
            { "Gems", "宝石" },
            { "Gravitating Sand", "重力沙" },
            { "Create Ocean Caves", "生成海洋洞穴" },
            { "Shimmer", "微光" },
            { "Clean Up Dirt", "清理泥土" },
            { "Pyramids", "金字塔" },
            { "Dirt Rock Wall Runner", "泥石墙遍历" },
            { "Living Trees", "生命树" },
            { "Wood Tree Walls", "生命树木墙" },
            { "Altars", "祭坛" },
            { "Wet Jungle", "湿润丛林" },
            { "Jungle Temple", "丛林神庙" },
            { "Hives", "蜂巢" },
            { "Jungle Chests", "丛林宝箱" },
            { "Settle Liquids", "液体稳定" },
            { "Remove Water From Sand", "移除沙地积水" },
            { "Oasis", "绿洲" },
            { "Shell Piles", "贝壳堆" },
            { "Smooth World", "平滑地形" },
            { "Waterfalls", "瀑布" },
            { "Ice", "薄冰" },
            { "Wall Variety", "墙体变化" },
            { "Life Crystals", "生命水晶" },
            { "Statues", "雕像" },
            { "Buried Chests", "地下宝箱" },
            { "Surface Chests", "地表宝箱" },
            { "Jungle Chests Placement", "丛林宝箱放置" },
            { "Water Chests", "水下宝箱" },
            { "Spider Caves", "蜘蛛洞" },
            { "Gem Caves", "宝石洞" },
            { "Moss", "苔藓" },
            { "Temple", "神庙" },
            { "Cave Walls", "洞穴墙体" },
            { "Jungle Trees", "丛林树" },
            { "Floating Island Houses", "浮空岛房屋" },
            { "Quick Cleanup", "快速清理" },
            { "Pots", "陶罐" },
            { "Hellforge", "熔炉" },
            { "Spreading Grass", "铺草" },
            { "Surface Ore and Stone", "地表矿石与石块" },
            { "Place Fallen Log", "放置倒木" },
            { "Traps", "陷阱" },
            { "Piles", "堆积物" },
            { "Spawn Point", "出生点" },
            { "Grass Wall", "草墙" },
            { "Guide", "向导" },
            { "Sunflowers", "向日葵" },
            { "Planting Trees", "种树" },
            { "Herbs", "草药" },
            { "Dye Plants", "染料植物" },
            { "Webs And Honey", "蛛网与蜂蜜" },
            { "Weeds", "杂草" },
            { "Glowing Mushrooms and Jungle Plants", "发光蘑菇与丛林植物" },
            { "Jungle Plants", "丛林植物" },
            { "Vines", "藤蔓" },
            { "Flowers", "花朵" },
            { "Mushrooms", "蘑菇" },
            { "Gems In Ice Biome", "雪地宝石" },
            { "Random Gems", "随机宝石" },
            { "Moss Grass", "苔藓草" },
            { "Muds Walls In Jungle", "丛林淤泥墙" },
            { "Larva", "幼虫" },
            { "Settle Liquids Again", "再次稳定液体" },
            { "Cactus, Palm Trees, & Coral", "仙人掌/棕榈/珊瑚" },
            { "Tile Cleanup", "方块清理" },
            { "Lihzahrd Altars", "蜥蜴祭坛" },
            { "Micro Biomes", "微型生态" },
            { "Water Plants", "水生植物" },
            { "Stalac", "钟乳石" },
            { "Remove Broken Traps", "移除损坏陷阱" },
            { "Final Cleanup", "最终清理" }
        };

        private static readonly Dictionary<string, string> WorldGenExactTranslations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Reset", "重置" },
            { "Step Back", "后退一步" },
            { "Step Forward", "前进一步" },
            { "Pause", "暂停" },
            { "Play", "继续" },
            { "Toggle Map", "切换地图" },
            { "Toggle Chat", "切换聊天" },
            { "Cancel", "取消" },
            { "Delete all snapshots", "删除全部快照" },
            { "Reset to snapshot", "恢复到快照" },
            { "Take snapshot", "创建快照" },
            { "World Cleared", "世界已清空" },
            { "Debug Commands", "调试命令" }
        };

        private static readonly Dictionary<string, string> WorldGenTextReplacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Hotkey: Space", "快捷键：空格" },
            { "Hotkey: Up/Left", "快捷键：上/左" },
            { "Hotkey: Down/Right", "快捷键：下/右" },
            { "Left click to toggle the map display", "左键切换地图显示" },
            { "Left click to toggle the chat log", "左键切换聊天记录显示" },
            { "Click to clear all snapshots", "点击清空全部快照" },
            { "Estimated Disk Usage:", "预计磁盘占用：" },
            { "Must be paused to manipulate snapshots", "必须暂停后才能操作快照" },
            { "Must be paused to reset", "必须暂停后才能重置" },
            { "Must be paused to rerun or load snapshots", "必须暂停后才能重跑或加载快照" },
            { "Must be paused to load a snapshot", "必须暂停后才能加载快照" },
            { "Left click to enable\n", "左键启用\n" },
            { "Right click to disable\n", "右键禁用\n" },
            { "Shift to edit ranges\n", "按 Shift 编辑范围\n" },
            { "Alt click to toggle highlight\n", "Alt+左键切换高亮\n" },
            { "Hold shift to ignore snapshots\n", "按住 Shift 忽略快照\n" },
            { "Left click to load snapshot\n", "左键加载快照\n" },
            { "Right click to delete snapshot\n", "右键删除快照\n" },
            { "Left click to take snapshot\n", "左键创建快照\n" },
            { "Snapshot is outdated and will only be used for comparison when the pass is run again", "快照已过期，仅会在再次运行该步骤时用于比较" },
            { "Save current settings to ", "将当前设置保存到 " },
            { "Future launches of the game will automatically load the world\nfrom the most recent snapshot, and run to the current pass", "今后启动游戏会自动加载该世界，\n从最新快照运行到当前步骤" },
            { "Pause on gen pass change: On", "生成步骤变化时暂停：开" },
            { "Pause on gen pass change: Off", "生成步骤变化时暂停：关" },
            { "Stop the generator when the output of a pass is different\nto the last time it was run in the save, or current session", "当某步骤输出与上次存档/本次会话不同步时停止生成器" },
            { "Skipped: ", "已跳过：" },
            { "Disabled: ", "已禁用：" },
            { "Run to ", "运行到 " },
            { "Rerun to ", "重新运行到 " },
            { "Paused after ", "已暂停：" }
        };

        private sealed class DebugCommandTranslation
        {
            public DebugCommandTranslation(string description, string helpText = null)
            {
                Description = description ?? string.Empty;
                HelpText = helpText;
            }

            public string Description { get; private set; }
            public string HelpText { get; private set; }
        }

        private static readonly Dictionary<string, DebugCommandTranslation> DebugCommandTranslations = new Dictionary<string, DebugCommandTranslation>(StringComparer.OrdinalIgnoreCase)
        {
            { "hh", new DebugCommandTranslation("打开全部调试命令列表。") },
            { "memo", new DebugCommandTranslation("创建一个快捷命令文件（每行一条命令，支持 {0}/{1} 参数替换）。", "用法：/memo <自定义命令名>") },
            { "memonum", new DebugCommandTranslation("给数字小键盘 0-9 创建 memo（等同 /memo numpad{i}）。", "用法：/memonum <0-9>") },
            { "setserverping", new DebugCommandTranslation("设置服务器目标延迟，客户端会自动用 /latency 调整。", "用法：/setserverping <毫秒>") },
            { "latency", new DebugCommandTranslation("为当前客户端的收发包增加延迟。", "用法：/latency <毫秒>") },
            { "setdrawwait", new DebugCommandTranslation("设置每次 Draw 固定等待时间。", "用法：/setdrawwait <毫秒>") },
            { "setupdatewait", new DebugCommandTranslation("设置每次 Update 固定等待时间。", "用法：/setupdatewait <毫秒>") },
            { "toggleinactivewait", new DebugCommandTranslation("切换窗口失焦时主线程休眠（会保存）。") },
            { "quickload", new DebugCommandTranslation("启动时自动用当前角色重进该世界/服务器，并执行 /onquickload memo。", "用法：/quickload [stop]") },
            { "quickload-regen", new DebugCommandTranslation("启动时自动重生该世界，并执行 /onquickload memo。") },
            { "light", new DebugCommandTranslation("切换光照：正常 / 全亮。") },
            { "nolimits", new DebugCommandTranslation("取消边界限制。") },
            { "save", new DebugCommandTranslation("保存玩家（单机时也保存世界）。") },
            { "reload", new DebugCommandTranslation("重载最近一次存档。") },
            { "quit", new DebugCommandTranslation("不保存直接退出世界。") },
            { "reloadpacks", new DebugCommandTranslation("重载材质包。") },
            { "frame", new DebugCommandTranslation("重置全部帧数据。") },
            { "hash", new DebugCommandTranslation("输出所有已保存（非易失）Tile 数据的哈希。") },
            { "snapshot", new DebugCommandTranslation("为当前世界 Tile 状态创建快照。") },
            { "snapclear", new DebugCommandTranslation("清除已创建的快照。") },
            { "snapsave", new DebugCommandTranslation("把快照保存到 dev-snapshots。", "用法：/snapsave <名称>") },
            { "snapload", new DebugCommandTranslation("从 dev-snapshots 读取快照。", "用法：/snapload <名称>") },
            { "restore", new DebugCommandTranslation("把世界 Tile 恢复到已创建的快照。") },
            { "swap", new DebugCommandTranslation("把当前世界 Tile 与快照互换。") },
            { "snapshotdiff", new DebugCommandTranslation("比较当前地图与快照差异（可用 /next 逐个跳转）。") },
            { "find", new DebugCommandTranslation("查找世界内指定 Tile（可用 /next 逐个跳转）。", "用法：/find <id>") },
            { "findwall", new DebugCommandTranslation("查找世界内指定墙体（可用 /next 逐个跳转）。", "用法：/findwall <id>") },
            { "next", new DebugCommandTranslation("跳到下一个 find/findwall/snapshotdiff 结果。") },
            { "showsections", new DebugCommandTranslation("切换网络分区覆盖显示。") },
            { "nopause", new DebugCommandTranslation("失去焦点时游戏不暂停。") },
            { "map", new DebugCommandTranslation("揭示该世界完整地图。", "用法：/map [pretty]") },
            { "clearmap", new DebugCommandTranslation("清除该世界完整地图。") },
            { "hideall", new DebugCommandTranslation("停止绘制方块、墙和液体。") },
            { "hidetiles", new DebugCommandTranslation("停止绘制方块。") },
            { "hidetiles2", new DebugCommandTranslation("停止绘制非实心方块。") },
            { "hidewalls", new DebugCommandTranslation("停止绘制墙体。") },
            { "hidewater", new DebugCommandTranslation("停止绘制液体。") },
            { "showunbreakablewalls", new DebugCommandTranslation("强制显示被方块覆盖的不可破坏墙。") },
            { "showlinks", new DebugCommandTranslation("以 UI 覆盖层绘制手柄链接点。") },
            { "shownetoffset", new DebugCommandTranslation("绘制 netOffset 调试粒子。") },
            { "fakenetoffset", new DebugCommandTranslation("给全部实体设置 netOffset（像素）。", "用法：/fakenetoffset <dx> <dy>") },
            { "nodamagevar", new DebugCommandTranslation("移除伤害随机浮动（±15%）。") },
            { "hurtdummies", new DebugCommandTranslation("允许投射物瞄准木桩。") },
            { "practice", new DebugCommandTranslation("切换练习模式（致死伤害时重置 Boss 战）。") },
            { "showdebug", new DebugCommandTranslation("切换命令报告输出。") }
        };

        public static void EnsureWorldGenTooltipDelegatesWrapped(object tooltipElement)
        {
            if (tooltipElement == null)
            {
                return;
            }

            var uniqueId = ReadUiUniqueId(tooltipElement);
            if (uniqueId >= 0)
            {
                lock (SyncRoot)
                {
                    if (WrappedTooltipElementIds.Contains(uniqueId))
                    {
                        return;
                    }
                }
            }

            var type = tooltipElement.GetType();
            var titleField = type.GetField("_getTitle", InstanceFlags);
            var descriptionField = type.GetField("_getDescription", InstanceFlags);
            if (titleField == null && descriptionField == null)
            {
                return;
            }

            if (titleField != null)
            {
                var titleGetter = titleField.GetValue(tooltipElement) as Func<string>;
                if (titleGetter != null)
                {
                    titleField.SetValue(tooltipElement, WrapWorldGenDelegate(titleGetter));
                }
            }

            if (descriptionField != null)
            {
                var descriptionGetter = descriptionField.GetValue(tooltipElement) as Func<string>;
                if (descriptionGetter != null)
                {
                    descriptionField.SetValue(tooltipElement, WrapWorldGenDelegate(descriptionGetter));
                }
            }

            if (uniqueId >= 0)
            {
                lock (SyncRoot)
                {
                    WrappedTooltipElementIds.Add(uniqueId);
                }
            }
        }

        public static void LocalizeWorldGenUi(object uiState)
        {
            if (uiState == null)
            {
                return;
            }

            TraverseUiElements(uiState, element =>
            {
                TryTranslateUiText(element, TranslateWorldGenDisplayText);
                TryTranslateUiHeader(element, TranslateWorldGenDisplayText);
                TryTranslateStringTextPanel(element, TranslateWorldGenDisplayText);
            });
        }

        public static void LocalizeDebugCommandsListUi(object uiState)
        {
            TryLocalizeDebugCommands();
            if (uiState == null)
            {
                return;
            }

            TraverseUiElements(uiState, element =>
            {
                TryTranslateStringTextPanel(element, TranslateDebugCommandsDisplayText);
                TryTranslateUiText(element, TranslateDebugCommandsDisplayText);
            });
        }

        public static void TryLocalizeDebugCommands()
        {
            try
            {
                var chatManagerType = FindType("Terraria.UI.Chat.ChatManager");
                if (chatManagerType == null)
                {
                    return;
                }

                var debugCommandsField = chatManagerType.GetField("DebugCommands", StaticFlags);
                if (debugCommandsField == null)
                {
                    return;
                }

                var processor = debugCommandsField.GetValue(null);
                if (processor == null)
                {
                    return;
                }

                var commandsProperty = processor.GetType().GetProperty("Commands", InstanceFlags);
                var commands = commandsProperty == null ? null : commandsProperty.GetValue(processor, null) as IEnumerable;
                if (commands == null)
                {
                    return;
                }

                foreach (var command in commands)
                {
                    if (command == null)
                    {
                        continue;
                    }

                    var name = ReadStringProperty(command, "Name");
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    DebugCommandTranslation translation;
                    if (!DebugCommandTranslations.TryGetValue(name.Trim(), out translation) || translation == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(translation.Description))
                    {
                        TryWriteStringProperty(command, "Description", translation.Description);
                    }

                    if (!string.IsNullOrWhiteSpace(translation.HelpText))
                    {
                        TryWriteStringProperty(command, "HelpText", translation.HelpText);
                    }
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("DebugUiLocalizationCompat.TryLocalizeDebugCommands", error);
                LogThrottle.WarnThrottled(
                    "debug-ui-localization-debug-commands-failed",
                    TimeSpan.FromSeconds(10),
                    "DebugUiLocalizationCompat",
                    "Debug command localization failed: " + error.Message);
            }
        }

        private static string TranslateDebugCommandsDisplayText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text ?? string.Empty;
            }

            string translated;
            if (WorldGenExactTranslations.TryGetValue(text, out translated))
            {
                return translated;
            }

            if (string.Equals(text, "Debug Commands", StringComparison.Ordinal))
            {
                return "调试命令";
            }

            return text;
        }

        private static Func<string> WrapWorldGenDelegate(Func<string> source)
        {
            return delegate
            {
                string value;
                try
                {
                    value = source == null ? string.Empty : source();
                }
                catch
                {
                    value = string.Empty;
                }

                return TranslateWorldGenDisplayText(value);
            };
        }

        private static string TranslateWorldGenDisplayText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text ?? string.Empty;
            }

            string translated;
            if (WorldGenPassNameTranslations.TryGetValue(text, out translated))
            {
                return translated;
            }

            if (WorldGenExactTranslations.TryGetValue(text, out translated))
            {
                return translated;
            }

            if (text.StartsWith("Paused after ", StringComparison.Ordinal))
            {
                var passName = text.Substring("Paused after ".Length);
                return "已暂停：" + TranslateWorldGenPassName(passName);
            }

            if (text.StartsWith("Skipped: ", StringComparison.Ordinal))
            {
                var passName = text.Substring("Skipped: ".Length);
                return "已跳过：" + TranslateWorldGenPassName(passName);
            }

            if (text.StartsWith("Disabled: ", StringComparison.Ordinal))
            {
                var passName = text.Substring("Disabled: ".Length);
                return "已禁用：" + TranslateWorldGenPassName(passName);
            }

            if (text.StartsWith("Run to ", StringComparison.Ordinal))
            {
                var passName = text.Substring("Run to ".Length);
                return "运行到 " + TranslateWorldGenPassName(passName);
            }

            if (text.StartsWith("Rerun to ", StringComparison.Ordinal))
            {
                var passName = text.Substring("Rerun to ".Length);
                return "重新运行到 " + TranslateWorldGenPassName(passName);
            }

            var result = text;
            foreach (var pair in WorldGenTextReplacements)
            {
                if (result.IndexOf(pair.Key, StringComparison.Ordinal) >= 0)
                {
                    result = result.Replace(pair.Key, pair.Value);
                }
            }

            result = ReplaceKnownPassNames(result);
            return result;
        }

        private static string TranslateWorldGenPassName(string passName)
        {
            if (string.IsNullOrWhiteSpace(passName))
            {
                return passName ?? string.Empty;
            }

            string translated;
            if (WorldGenPassNameTranslations.TryGetValue(passName, out translated))
            {
                return translated;
            }

            return passName;
        }

        private static string ReplaceKnownPassNames(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text ?? string.Empty;
            }

            var result = text;
            foreach (var pair in WorldGenPassNameTranslations)
            {
                if (result.IndexOf(pair.Key, StringComparison.Ordinal) >= 0)
                {
                    result = result.Replace(pair.Key, pair.Value);
                }
            }

            return result;
        }

        private static void TraverseUiElements(object root, Action<object> visitor)
        {
            if (root == null || visitor == null)
            {
                return;
            }

            var visited = new HashSet<int>();
            TraverseUiElementsCore(root, visitor, visited);
        }

        private static void TraverseUiElementsCore(object element, Action<object> visitor, HashSet<int> visited)
        {
            if (element == null)
            {
                return;
            }

            var id = ReadUiUniqueId(element);
            if (id >= 0 && !visited.Add(id))
            {
                return;
            }

            visitor(element);

            var children = ReadChildren(element);
            if (children == null)
            {
                return;
            }

            foreach (var child in children)
            {
                TraverseUiElementsCore(child, visitor, visited);
            }
        }

        private static IEnumerable ReadChildren(object element)
        {
            try
            {
                var childrenProperty = element.GetType().GetProperty("Children", InstanceFlags);
                return childrenProperty == null ? null : childrenProperty.GetValue(element, null) as IEnumerable;
            }
            catch
            {
                return null;
            }
        }

        private static int ReadUiUniqueId(object element)
        {
            if (element == null)
            {
                return -1;
            }

            try
            {
                var property = element.GetType().GetProperty("UniqueId", InstanceFlags);
                if (property == null)
                {
                    return -1;
                }

                var value = property.GetValue(element, null);
                return value == null ? -1 : Convert.ToInt32(value);
            }
            catch
            {
                return -1;
            }
        }

        private static void TryTranslateUiText(object element, Func<string, string> translator)
        {
            if (element == null || translator == null)
            {
                return;
            }

            var type = element.GetType();
            if (!string.Equals(type.FullName, "Terraria.GameContent.UI.Elements.UIText", StringComparison.Ordinal))
            {
                return;
            }

            var textProperty = type.GetProperty("Text", InstanceFlags);
            var setText = type.GetMethod("SetText", InstanceFlags, null, new[] { typeof(string) }, null);
            if (textProperty == null || setText == null)
            {
                return;
            }

            var current = textProperty.GetValue(element, null) as string;
            var translated = translator(current);
            if (string.Equals(current, translated, StringComparison.Ordinal))
            {
                return;
            }

            setText.Invoke(element, new object[] { translated });
        }

        private static void TryTranslateUiHeader(object element, Func<string, string> translator)
        {
            if (element == null || translator == null)
            {
                return;
            }

            var type = element.GetType();
            if (!string.Equals(type.FullName, "Terraria.GameContent.UI.Elements.UIHeader", StringComparison.Ordinal))
            {
                return;
            }

            var textProperty = type.GetProperty("Text", InstanceFlags);
            if (textProperty == null || !textProperty.CanRead || !textProperty.CanWrite)
            {
                return;
            }

            var current = textProperty.GetValue(element, null) as string;
            var translated = translator(current);
            if (string.Equals(current, translated, StringComparison.Ordinal))
            {
                return;
            }

            textProperty.SetValue(element, translated, null);
        }

        private static void TryTranslateStringTextPanel(object element, Func<string, string> translator)
        {
            if (element == null || translator == null)
            {
                return;
            }

            var type = element.GetType();
            if (type == null ||
                !type.IsGenericType ||
                !string.Equals(type.GetGenericTypeDefinition().FullName, "Terraria.GameContent.UI.Elements.UITextPanel`1", StringComparison.Ordinal))
            {
                return;
            }

            var genericArgument = type.GetGenericArguments()[0];
            if (genericArgument != typeof(string))
            {
                return;
            }

            var textProperty = type.GetProperty("Text", InstanceFlags);
            var setText = type.GetMethod("SetText", InstanceFlags, null, new[] { genericArgument }, null);
            if (textProperty == null || setText == null)
            {
                return;
            }

            var current = textProperty.GetValue(element, null) as string;
            var translated = translator(current);
            if (string.Equals(current, translated, StringComparison.Ordinal))
            {
                return;
            }

            setText.Invoke(element, new object[] { translated });
        }

        private static string ReadStringProperty(object target, string propertyName)
        {
            if (target == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return string.Empty;
            }

            try
            {
                var property = target.GetType().GetProperty(propertyName, InstanceFlags);
                if (property == null || !property.CanRead)
                {
                    return string.Empty;
                }

                return property.GetValue(target, null) as string ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool TryWriteStringProperty(object target, string propertyName, string value)
        {
            if (target == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            var type = target.GetType();
            var property = type.GetProperty(propertyName, InstanceFlags);
            if (property != null)
            {
                try
                {
                    var setter = property.GetSetMethod(true);
                    if (setter != null)
                    {
                        setter.Invoke(target, new object[] { value ?? string.Empty });
                        return true;
                    }
                }
                catch
                {
                }
            }

            try
            {
                var backingField = type.GetField("<" + propertyName + ">k__BackingField", InstanceFlags);
                if (backingField != null)
                {
                    backingField.SetValue(target, value ?? string.Empty);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static Type FindType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                try
                {
                    var type = assemblies[index].GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }
    }
}

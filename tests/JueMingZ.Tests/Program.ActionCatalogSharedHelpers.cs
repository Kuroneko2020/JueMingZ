using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Bootstrap;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.GameState.Inventory;
using JueMingZ.GameState.Npcs;
using JueMingZ.GameState.Player;
using JueMingZ.GameState.Ui;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;
using Terraria.ID;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void AssertDefault(bool condition, string label)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Unexpected first-run default: " + label);
            }
        }

        private static bool ContainsTileType(IList<AutoMiningTile> tiles, int tileType)
        {
            if (tiles == null)
            {
                return false;
            }

            for (var index = 0; index < tiles.Count; index++)
            {
                if (tiles[index] != null && tiles[index].TileType == tileType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsTile(IList<AutoMiningTile> tiles, int x, int y)
        {
            if (tiles == null)
            {
                return false;
            }

            for (var index = 0; index < tiles.Count; index++)
            {
                if (tiles[index] != null && tiles[index].X == x && tiles[index].Y == y)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetTestTile(int x, int y, bool active, int tileType)
        {
            Terraria.Main.tile[x, y] = new Terraria.Tile
            {
                activeValue = active,
                type = tileType
            };
        }

    }
}

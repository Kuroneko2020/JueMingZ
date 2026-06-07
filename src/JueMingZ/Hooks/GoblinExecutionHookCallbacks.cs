using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using Terraria;

namespace JueMingZ.Hooks
{
    internal static class GoblinExecutionHookCallbacks
    {
        // Transpilers only reopen the vanilla friendly-NPC gates for the accepted
        // execution rule; they must not edit NPC state, player state, or damage data.
        private static readonly MethodInfo ShouldAllowMethod =
            typeof(CombatGoblinExecutionCompat).GetMethod(
                "ShouldAllowGoblinExecution",
                BindingFlags.Public | BindingFlags.Static);

        private static readonly FieldInfo NpcFriendlyField = typeof(NPC).GetField("friendly", BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo NpcTypeField = typeof(NPC).GetField("type", BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo ItemTypeField = typeof(Item).GetField("type", BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo ProjectileTypeField = typeof(Projectile).GetField("type", BindingFlags.Instance | BindingFlags.Public);
        private static readonly MethodInfo NpcIsLikeTownNpcGetter = ResolveNpcIsLikeTownNpcGetter();

        private static bool _playerProcessHitPatchApplied;
        private static bool _projectileDamagePatchApplied;

        internal static bool PlayerProcessHitPatchApplied
        {
            get { return _playerProcessHitPatchApplied; }
        }

        internal static bool ProjectileDamagePatchApplied
        {
            get { return _projectileDamagePatchApplied; }
        }

        internal static void ResetPatchState()
        {
            _playerProcessHitPatchApplied = false;
            _projectileDamagePatchApplied = false;
            CombatGoblinExecutionCompat.SetHookReady(false);
        }

        private static IEnumerable<CodeInstruction> PlayerProcessHitAgainstNpcTranspiler(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);
            var match = FindPlayerFriendlyGateReturn(codes);
            if (match.ReturnIndex < 0 || match.NpcLoad == null)
            {
                Logger.Warn("GoblinExecutionHookCallbacks", "Player.ProcessHitAgainstNPC friendly town-NPC gate anchor not found; goblin execution player hit patch skipped.");
                return codes;
            }

            var continueLabel = GetOrCreateLabel(generator, codes, match.ReturnIndex + 1);
            var npcLoad = CloneWithoutLabels(match.NpcLoad);
            MoveLabels(codes[match.ReturnIndex], npcLoad);

            codes.InsertRange(
                match.ReturnIndex,
                new[]
                {
                    npcLoad,
                    new CodeInstruction(OpCodes.Call, ShouldAllowMethod),
                    new CodeInstruction(OpCodes.Brtrue_S, continueLabel)
                });

            _playerProcessHitPatchApplied = true;
            return codes;
        }

        private static IEnumerable<CodeInstruction> ProjectileDamagePveInnerTranspiler(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);
            var flagStoreIndex = FindProjectileFriendlyFlagStore(codes);
            if (flagStoreIndex < 0)
            {
                Logger.Warn("GoblinExecutionHookCallbacks", "Projectile.Damage_PVE_Inner friendly flag anchor not found; goblin execution projectile patch skipped.");
                return codes;
            }

            var continueLabel = GetOrCreateLabel(generator, codes, flagStoreIndex + 1);
            var loadFlag = CreateLoadForStore(codes[flagStoreIndex]);
            var storeFlag = CloneWithoutLabels(codes[flagStoreIndex]);
            if (loadFlag == null || storeFlag == null)
            {
                Logger.Warn("GoblinExecutionHookCallbacks", "Projectile.Damage_PVE_Inner flag local opcode unsupported; goblin execution projectile patch skipped.");
                return codes;
            }

            codes.InsertRange(
                flagStoreIndex + 1,
                new[]
                {
                    loadFlag,
                    new CodeInstruction(OpCodes.Brtrue_S, continueLabel),
                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Call, ShouldAllowMethod),
                    storeFlag
                });

            _projectileDamagePatchApplied = true;
            return codes;
        }

        private static PlayerGateMatch FindPlayerFriendlyGateReturn(IList<CodeInstruction> codes)
        {
            for (var friendlyIndex = 1; friendlyIndex < codes.Count; friendlyIndex++)
            {
                if (!IsField(codes[friendlyIndex], NpcFriendlyField))
                {
                    continue;
                }

                var npcLoad = codes[friendlyIndex - 1];
                var limit = Math.Min(codes.Count, friendlyIndex + 140);
                for (var index = friendlyIndex + 1; index < limit; index++)
                {
                    if (!IsRet(codes[index]))
                    {
                        continue;
                    }

                    if (RangeContains(codes, friendlyIndex, index, instruction => IsMethod(instruction, NpcIsLikeTownNpcGetter)) &&
                        RangeContains(codes, friendlyIndex, index, instruction => IsField(instruction, ItemTypeField)) &&
                        RangeContains(codes, friendlyIndex, index, instruction => HasLdcI4(instruction, 5129)) &&
                        RangeContains(codes, friendlyIndex, index, instruction => HasLdcI4(instruction, 3351)))
                    {
                        return new PlayerGateMatch(index, npcLoad);
                    }
                }
            }

            return new PlayerGateMatch(-1, null);
        }

        private static int FindProjectileFriendlyFlagStore(IList<CodeInstruction> codes)
        {
            for (var friendlyIndex = 0; friendlyIndex < codes.Count; friendlyIndex++)
            {
                if (!IsField(codes[friendlyIndex], NpcFriendlyField))
                {
                    continue;
                }

                var limit = Math.Min(codes.Count, friendlyIndex + 100);
                for (var index = friendlyIndex + 1; index < limit; index++)
                {
                    if (!IsStloc(codes[index]))
                    {
                        continue;
                    }

                    if (RangeContains(codes, friendlyIndex, index, instruction => IsField(instruction, NpcTypeField)) &&
                        RangeContains(codes, friendlyIndex, index, instruction => IsField(instruction, ProjectileTypeField)) &&
                        RangeContains(codes, friendlyIndex, index, instruction => HasLdcI4(instruction, 318)) &&
                        RangeContains(codes, friendlyIndex, index, instruction => HasLdcI4(instruction, 22)) &&
                        RangeContains(codes, friendlyIndex, index, instruction => HasLdcI4(instruction, 54)))
                    {
                        return index;
                    }
                }
            }

            return -1;
        }

        private static bool RangeContains(IList<CodeInstruction> codes, int startInclusive, int endInclusive, Func<CodeInstruction, bool> predicate)
        {
            for (var index = startInclusive; index <= endInclusive && index < codes.Count; index++)
            {
                if (predicate(codes[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsField(CodeInstruction instruction, FieldInfo field)
        {
            return instruction != null &&
                   field != null &&
                   instruction.operand is FieldInfo &&
                   ReferenceEquals((FieldInfo)instruction.operand, field);
        }

        private static bool IsMethod(CodeInstruction instruction, MethodInfo method)
        {
            return instruction != null &&
                   method != null &&
                   instruction.operand is MethodInfo &&
                   ReferenceEquals((MethodInfo)instruction.operand, method);
        }

        private static MethodInfo ResolveNpcIsLikeTownNpcGetter()
        {
            var property = typeof(NPC).GetProperty("isLikeATownNPC", BindingFlags.Instance | BindingFlags.Public);
            return property == null ? null : property.GetGetMethod();
        }

        private static bool IsRet(CodeInstruction instruction)
        {
            return instruction != null && instruction.opcode == OpCodes.Ret;
        }

        private static bool IsStloc(CodeInstruction instruction)
        {
            if (instruction == null)
            {
                return false;
            }

            return instruction.opcode == OpCodes.Stloc ||
                   instruction.opcode == OpCodes.Stloc_S ||
                   instruction.opcode == OpCodes.Stloc_0 ||
                   instruction.opcode == OpCodes.Stloc_1 ||
                   instruction.opcode == OpCodes.Stloc_2 ||
                   instruction.opcode == OpCodes.Stloc_3;
        }

        private static CodeInstruction CreateLoadForStore(CodeInstruction storeInstruction)
        {
            if (storeInstruction == null)
            {
                return null;
            }

            if (storeInstruction.opcode == OpCodes.Stloc_0) return new CodeInstruction(OpCodes.Ldloc_0);
            if (storeInstruction.opcode == OpCodes.Stloc_1) return new CodeInstruction(OpCodes.Ldloc_1);
            if (storeInstruction.opcode == OpCodes.Stloc_2) return new CodeInstruction(OpCodes.Ldloc_2);
            if (storeInstruction.opcode == OpCodes.Stloc_3) return new CodeInstruction(OpCodes.Ldloc_3);
            if (storeInstruction.opcode == OpCodes.Stloc_S) return new CodeInstruction(OpCodes.Ldloc_S, storeInstruction.operand);
            if (storeInstruction.opcode == OpCodes.Stloc) return new CodeInstruction(OpCodes.Ldloc, storeInstruction.operand);
            return null;
        }

        private static bool HasLdcI4(CodeInstruction instruction, int value)
        {
            if (instruction == null)
            {
                return false;
            }

            if (instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int)
            {
                return (int)instruction.operand == value;
            }

            if (instruction.opcode == OpCodes.Ldc_I4_S && instruction.operand is sbyte)
            {
                return (sbyte)instruction.operand == value;
            }

            if (instruction.opcode == OpCodes.Ldc_I4_0) return value == 0;
            if (instruction.opcode == OpCodes.Ldc_I4_1) return value == 1;
            if (instruction.opcode == OpCodes.Ldc_I4_2) return value == 2;
            if (instruction.opcode == OpCodes.Ldc_I4_3) return value == 3;
            if (instruction.opcode == OpCodes.Ldc_I4_4) return value == 4;
            if (instruction.opcode == OpCodes.Ldc_I4_5) return value == 5;
            if (instruction.opcode == OpCodes.Ldc_I4_6) return value == 6;
            if (instruction.opcode == OpCodes.Ldc_I4_7) return value == 7;
            if (instruction.opcode == OpCodes.Ldc_I4_8) return value == 8;
            if (instruction.opcode == OpCodes.Ldc_I4_M1) return value == -1;
            return false;
        }

        private static Label GetOrCreateLabel(ILGenerator generator, IList<CodeInstruction> codes, int index)
        {
            if (index >= 0 && index < codes.Count && codes[index].labels.Count > 0)
            {
                return codes[index].labels[0];
            }

            var label = generator.DefineLabel();
            if (index >= 0 && index < codes.Count)
            {
                codes[index].labels.Add(label);
            }

            return label;
        }

        private static CodeInstruction CloneWithoutLabels(CodeInstruction source)
        {
            return source == null ? null : new CodeInstruction(source.opcode, source.operand);
        }

        private static void MoveLabels(CodeInstruction source, CodeInstruction destination)
        {
            if (source == null || destination == null || source.labels.Count == 0)
            {
                return;
            }

            destination.labels.AddRange(source.labels);
            source.labels.Clear();
        }

        private struct PlayerGateMatch
        {
            public PlayerGateMatch(int returnIndex, CodeInstruction npcLoad)
            {
                ReturnIndex = returnIndex;
                NpcLoad = npcLoad;
            }

            public int ReturnIndex { get; private set; }
            public CodeInstruction NpcLoad { get; private set; }
        }
    }
}

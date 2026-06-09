using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingZ.Automation.Combat
{
    // Target reads build snapshots only; inactive, unreadable, or unsafe entities are skipped rather than patched.
    public static class CombatAimTargetReader
    {
        private const int FallbackTargetDummyType = 488;
        private static bool _targetDummyTypeResolved;
        private static int _targetDummyType;
        private static bool _targetDummyFallbackLogged;

        public static CombatAimReadResult Read(bool trackDummy)
        {
            var result = new CombatAimReadResult();
            var skipCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            try
            {
                NPC[] typedNpcs;
                string typedSkipReason;
                if (TryReadTypedMainState(result, out typedNpcs, out typedSkipReason))
                {
                    var targetDummyType = ResolveTargetDummyType();
                    for (var index = 0; index < typedNpcs.Length; index++)
                    {
                        var npc = typedNpcs[index];
                        if (npc == null)
                        {
                            AddSkip(skipCounts, "nullNpc");
                            continue;
                        }

                        CombatTargetSnapshot target;
                        string skipReason;
                        if (TryReadTypedCandidate(npc, index, targetDummyType, trackDummy, skipCounts, out target, out skipReason))
                        {
                            result.Candidates.Add(target);
                        }
                        else
                        {
                            AddSkip(skipCounts, skipReason);
                        }
                    }

                    result.CanSearch = true;
                    result.SkipReason = BuildSkipSummary(skipCounts);
                    return result;
                }

                IList reflectedNpcs;
                string reflectionSkipReason;
                if (!TryReadReflectionMainState(result, out reflectedNpcs, out reflectionSkipReason))
                {
                    result.SkipReason = string.IsNullOrWhiteSpace(typedSkipReason)
                        ? reflectionSkipReason
                        : typedSkipReason + "|" + reflectionSkipReason;
                    return result;
                }

                var fallbackTargetDummyType = ResolveTargetDummyType();
                for (var index = 0; index < reflectedNpcs.Count; index++)
                {
                    var npc = reflectedNpcs[index];
                    if (npc == null)
                    {
                        AddSkip(skipCounts, "nullNpc");
                        continue;
                    }

                    CombatTargetSnapshot target;
                    string skipReason;
                    if (TryReadCandidate(npc, index, fallbackTargetDummyType, trackDummy, skipCounts, out target, out skipReason))
                    {
                        result.Candidates.Add(target);
                    }
                    else
                    {
                        AddSkip(skipCounts, skipReason);
                    }
                }

                result.CanSearch = true;
                result.SkipReason = BuildSkipSummary(skipCounts);
                return result;
            }
            catch (Exception error)
            {
                result.SkipReason = "readFailed:" + error.Message;
                LogThrottle.WarnThrottled(
                    "combat-aim-target-read-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatAimTargetReader",
                    "Combat aim target read failed: " + error.Message);
                return result;
            }
        }

        public static bool TryReadTargetByIdentity(
            int whoAmI,
            int type,
            bool trackDummy,
            out CombatTargetSnapshot target,
            out string skipReason)
        {
            target = null;
            skipReason = string.Empty;
            try
            {
                if (whoAmI < 0)
                {
                    skipReason = "invalidTargetIdentity";
                    return false;
                }

                var targetDummyType = ResolveTargetDummyType();
                var skipCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                NPC[] typedNpcs;
                string typedMainSkip;
                if (TryReadTypedMainState(null, out typedNpcs, out typedMainSkip))
                {
                    if (whoAmI >= 0 && whoAmI < typedNpcs.Length)
                    {
                        if (TryReadTypedCandidate(typedNpcs[whoAmI], whoAmI, targetDummyType, trackDummy, skipCounts, out target, out skipReason) &&
                            target != null &&
                            target.WhoAmI == whoAmI &&
                            (type <= 0 || target.Type == type))
                        {
                            return true;
                        }
                    }

                    for (var index = 0; index < typedNpcs.Length; index++)
                    {
                        if (index == whoAmI)
                        {
                            continue;
                        }

                        CombatTargetSnapshot candidate;
                        string candidateSkip;
                        if (!TryReadTypedCandidate(typedNpcs[index], index, targetDummyType, trackDummy, skipCounts, out candidate, out candidateSkip) ||
                            candidate == null)
                        {
                            continue;
                        }

                        if (candidate.WhoAmI == whoAmI && (type <= 0 || candidate.Type == type))
                        {
                            target = candidate;
                            skipReason = string.Empty;
                            return true;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(skipReason))
                    {
                        skipReason = "targetNotFound";
                    }

                    return false;
                }

                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    skipReason = string.IsNullOrWhiteSpace(typedMainSkip)
                        ? "runtimeTypesUnavailable:" + TerrariaRuntimeTypes.LastError
                        : typedMainSkip + "|runtimeTypesUnavailable:" + TerrariaRuntimeTypes.LastError;
                    return false;
                }

                var mainType = TerrariaRuntimeTypes.MainType;
                if (mainType == null)
                {
                    skipReason = string.IsNullOrWhiteSpace(typedMainSkip)
                        ? "mainTypeUnavailable"
                        : typedMainSkip + "|mainTypeUnavailable";
                    return false;
                }

                var npcs = GameStateReflection.AsList(GameStateReflection.GetStaticMember(mainType, "npc"));
                if (npcs == null)
                {
                    skipReason = string.IsNullOrWhiteSpace(typedMainSkip)
                        ? "npcListUnavailable"
                        : typedMainSkip + "|npcListUnavailable";
                    return false;
                }

                if (whoAmI >= 0 && whoAmI < npcs.Count)
                {
                    var npc = npcs[whoAmI];
                    if (TryReadCandidate(npc, whoAmI, targetDummyType, trackDummy, skipCounts, out target, out skipReason) &&
                        target != null &&
                        target.WhoAmI == whoAmI &&
                        (type <= 0 || target.Type == type))
                    {
                        return true;
                    }
                }

                for (var index = 0; index < npcs.Count; index++)
                {
                    if (index == whoAmI)
                    {
                        continue;
                    }

                    CombatTargetSnapshot candidate;
                    string candidateSkip;
                    if (!TryReadCandidate(npcs[index], index, targetDummyType, trackDummy, skipCounts, out candidate, out candidateSkip) ||
                        candidate == null)
                    {
                        continue;
                    }

                    if (candidate.WhoAmI == whoAmI && (type <= 0 || candidate.Type == type))
                    {
                        target = candidate;
                        skipReason = string.Empty;
                        return true;
                    }
                }

                if (string.IsNullOrWhiteSpace(skipReason))
                {
                    skipReason = "targetNotFound";
                }

                return false;
            }
            catch (Exception error)
            {
                skipReason = "targetRefreshFailed:" + error.Message;
                return false;
            }
        }

        public static bool TryReadScreenState(out float screenX, out float screenY, out int screenWidth, out int screenHeight)
        {
            screenX = 0f;
            screenY = 0f;
            screenWidth = 0;
            screenHeight = 0;

            if (!IsLateBootstrapCompleted())
            {
                return false;
            }

            try
            {
                var screenPosition = TerrariaMainCompat.ScreenPosition;
                screenX = screenPosition.X;
                screenY = screenPosition.Y;
                screenWidth = TerrariaMainCompat.ScreenWidth;
                screenHeight = TerrariaMainCompat.ScreenHeight;
                return true;
            }
            catch
            {
            }

            try
            {
                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    return false;
                }

                var mainType = TerrariaRuntimeTypes.MainType;
                if (mainType == null || !TryReadStaticVector(mainType, "screenPosition", out screenX, out screenY))
                {
                    return false;
                }

                TryReadStaticInt(mainType, "screenWidth", out screenWidth);
                TryReadStaticInt(mainType, "screenHeight", out screenHeight);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadTypedMainState(CombatAimReadResult result, out NPC[] npcs, out string skipReason)
        {
            npcs = null;
            skipReason = string.Empty;
            if (!IsLateBootstrapCompleted())
            {
                skipReason = "lateBootstrapNotCompleted";
                return false;
            }

            try
            {
                if (result != null)
                {
                    var screenPosition = TerrariaMainCompat.ScreenPosition;
                    var mouseX = TerrariaMainCompat.MouseX;
                    var mouseY = TerrariaMainCompat.MouseY;

                    result.MouseScreenX = mouseX;
                    result.MouseScreenY = mouseY;
                    result.ScreenPositionX = screenPosition.X;
                    result.ScreenPositionY = screenPosition.Y;
                    result.CursorWorldX = screenPosition.X + mouseX;
                    result.CursorWorldY = screenPosition.Y + mouseY;
                    result.HasCursorWorld = true;
                    result.ScreenWidth = TerrariaMainCompat.ScreenWidth;
                    result.ScreenHeight = TerrariaMainCompat.ScreenHeight;
                }

                npcs = TerrariaMainCompat.Npcs;
                if (npcs == null)
                {
                    skipReason = "npcListUnavailable";
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                skipReason = "typedMainReadFailed:" + error.GetType().Name;
                LogThrottle.WarnThrottled(
                    "combat-aim-target-typed-main-read-failed",
                    TimeSpan.FromSeconds(30),
                    "CombatAimTargetReader",
                    "Typed combat aim Main read failed, falling back to reflection: " + error.Message);
                return false;
            }
        }

        private static bool TryReadReflectionMainState(CombatAimReadResult result, out IList npcs, out string skipReason)
        {
            npcs = null;
            skipReason = string.Empty;

            if (!IsLateBootstrapCompleted())
            {
                skipReason = "lateBootstrapNotCompleted";
                return false;
            }

            if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
            {
                skipReason = "runtimeTypesUnavailable:" + TerrariaRuntimeTypes.LastError;
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                skipReason = "mainTypeUnavailable";
                return false;
            }

            int mouseX;
            int mouseY;
            if (!TryReadStaticInt(mainType, "mouseX", out mouseX) ||
                !TryReadStaticInt(mainType, "mouseY", out mouseY))
            {
                skipReason = "mouseUnavailable";
                return false;
            }

            float screenX;
            float screenY;
            if (!TryReadStaticVector(mainType, "screenPosition", out screenX, out screenY))
            {
                skipReason = "screenPositionUnavailable";
                return false;
            }

            int screenWidth;
            int screenHeight;
            TryReadStaticInt(mainType, "screenWidth", out screenWidth);
            TryReadStaticInt(mainType, "screenHeight", out screenHeight);

            result.MouseScreenX = mouseX;
            result.MouseScreenY = mouseY;
            result.ScreenPositionX = screenX;
            result.ScreenPositionY = screenY;
            result.CursorWorldX = screenX + mouseX;
            result.CursorWorldY = screenY + mouseY;
            result.HasCursorWorld = true;
            result.ScreenWidth = screenWidth;
            result.ScreenHeight = screenHeight;

            npcs = GameStateReflection.AsList(GameStateReflection.GetStaticMember(mainType, "npc"));
            if (npcs == null)
            {
                skipReason = "npcListUnavailable";
                return false;
            }

            return true;
        }

        private static bool IsLateBootstrapCompleted()
        {
            try
            {
                return JueMingZRuntime.State != null && JueMingZRuntime.State.LateBootstrapCompleted;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadTypedCandidate(
            NPC npc,
            int index,
            int targetDummyType,
            bool trackDummy,
            Dictionary<string, int> skipCounts,
            out CombatTargetSnapshot target,
            out string skipReason)
        {
            target = null;
            skipReason = string.Empty;
            if (npc == null)
            {
                skipReason = "nullNpc";
                return false;
            }

            var active = TerrariaNpcReadCompat.IsActive(npc);
            if (!active)
            {
                skipReason = "inactive";
                return false;
            }

            var type = TerrariaNpcReadCompat.Type(npc);
            var whoAmI = TerrariaNpcReadCompat.WhoAmI(npc);
            if (whoAmI < 0)
            {
                whoAmI = index;
                AddSkip(skipCounts, "missingWhoAmI");
            }

            var isTargetDummy = type == targetDummyType;
            if (isTargetDummy && !trackDummy)
            {
                skipReason = "targetDummyDisabled";
                return false;
            }

            var friendly = TerrariaNpcReadCompat.IsFriendly(npc);
            var townNpc = TerrariaNpcReadCompat.IsTownNpc(npc);
            var hide = TerrariaNpcReadCompat.IsHidden(npc);
            var chaseable = TerrariaNpcReadCompat.IsChaseable(npc);
            var dontTakeDamage = TerrariaNpcReadCompat.DontTakeDamage(npc);
            var immortal = TerrariaNpcReadCompat.IsImmortal(npc);
            var life = TerrariaNpcReadCompat.Life(npc);
            var lifeMax = TerrariaNpcReadCompat.LifeMax(npc);

            if (!isTargetDummy)
            {
                if (hide)
                {
                    skipReason = "hidden";
                    return false;
                }

                if (life <= 0)
                {
                    skipReason = "deadOrNoLife";
                    return false;
                }

                if (townNpc)
                {
                    skipReason = "townNpc";
                    return false;
                }

                if (friendly)
                {
                    skipReason = "friendly";
                    return false;
                }

                if (!chaseable)
                {
                    skipReason = "notChaseable";
                    return false;
                }

                if (lifeMax <= 5)
                {
                    skipReason = "lifeMaxTooLow";
                    return false;
                }

                if (dontTakeDamage)
                {
                    skipReason = "dontTakeDamage";
                    return false;
                }

                if (immortal)
                {
                    skipReason = "immortal";
                    return false;
                }
            }

            var position = TerrariaNpcReadCompat.Position(npc);
            var center = TerrariaNpcReadCompat.Center(npc);
            var velocity = TerrariaNpcReadCompat.Velocity(npc);
            var width = TerrariaNpcReadCompat.Width(npc);
            var height = TerrariaNpcReadCompat.Height(npc);
            var hitbox = TerrariaNpcReadCompat.Hitbox(npc);
            var ai = TerrariaNpcReadCompat.Ai(npc);
            var aiSummaryAvailable = ai != null && ai.Length >= 4;

            if (width <= 0)
            {
                width = Math.Max(1, hitbox.Width);
                AddSkip(skipCounts, "missingWidth");
            }

            if (height <= 0)
            {
                height = Math.Max(1, hitbox.Height);
                AddSkip(skipCounts, "missingHeight");
            }

            if (hitbox.Width <= 0 || hitbox.Height <= 0)
            {
                hitbox = new Rectangle((int)Math.Round(position.X), (int)Math.Round(position.Y), Math.Max(1, width), Math.Max(1, height));
                AddSkip(skipCounts, "hitboxFallbackToPosition");
            }

            target = new CombatTargetSnapshot
            {
                WhoAmI = whoAmI,
                Type = type,
                Name = TerrariaNpcReadCompat.Name(npc),
                Active = active,
                Friendly = friendly,
                TownNpc = townNpc,
                Hide = hide,
                Chaseable = chaseable,
                DontTakeDamage = dontTakeDamage,
                Immortal = immortal,
                IsTargetDummy = isTargetDummy,
                Life = life,
                LifeMax = lifeMax,
                PositionX = position.X,
                PositionY = position.Y,
                Width = width,
                Height = height,
                CenterX = center.X,
                CenterY = center.Y,
                VelocityX = velocity.X,
                VelocityY = velocity.Y,
                NpcAiStyle = TerrariaNpcReadCompat.AiStyle(npc),
                NoGravity = TerrariaNpcReadCompat.NoGravity(npc),
                CollideX = TerrariaNpcReadCompat.CollideX(npc),
                CollideY = TerrariaNpcReadCompat.CollideY(npc),
                Direction = TerrariaNpcReadCompat.Direction(npc),
                DirectionY = TerrariaNpcReadCompat.DirectionY(npc),
                TargetPlayer = TerrariaNpcReadCompat.TargetPlayer(npc),
                AiSummaryAvailable = aiSummaryAvailable,
                Ai0 = ReadAiValue(ai, 0),
                Ai1 = ReadAiValue(ai, 1),
                Ai2 = ReadAiValue(ai, 2),
                Ai3 = ReadAiValue(ai, 3),
                HitboxX = hitbox.X,
                HitboxY = hitbox.Y,
                HitboxWidth = hitbox.Width,
                HitboxHeight = hitbox.Height
            };

            return true;
        }

        private static bool TryReadCandidate(
            object npc,
            int index,
            int targetDummyType,
            bool trackDummy,
            Dictionary<string, int> skipCounts,
            out CombatTargetSnapshot target,
            out string skipReason)
        {
            target = null;
            skipReason = string.Empty;

            bool active;
            if (!TryGetBool(npc, "active", out active))
            {
                skipReason = "missingActive";
                return false;
            }

            if (!active)
            {
                skipReason = "inactive";
                return false;
            }

            int type;
            if (!TryGetInt(npc, "type", out type))
            {
                skipReason = "missingType";
                return false;
            }

            int whoAmI;
            if (!TryGetInt(npc, "whoAmI", out whoAmI))
            {
                whoAmI = index;
                AddSkip(skipCounts, "missingWhoAmI");
            }

            var isTargetDummy = type == targetDummyType;
            if (isTargetDummy && !trackDummy)
            {
                skipReason = "targetDummyDisabled";
                return false;
            }

            bool friendly;
            bool townNpc;
            bool hide;
            bool chaseable;
            bool dontTakeDamage;
            bool immortal;
            int life;
            int lifeMax;

            if (!TryGetBool(npc, "friendly", out friendly))
            {
                AddSkip(skipCounts, "missingFriendly");
            }

            if (!TryGetBool(npc, "townNPC", out townNpc))
            {
                AddSkip(skipCounts, "missingTownNpc");
            }

            if (!TryGetBool(npc, "hide", out hide))
            {
                AddSkip(skipCounts, "missingHide");
            }

            var hasChaseable = TryGetBool(npc, "chaseable", out chaseable);
            if (!hasChaseable)
            {
                AddSkip(skipCounts, "missingChaseable");
            }

            if (!TryGetBool(npc, "dontTakeDamage", out dontTakeDamage))
            {
                AddSkip(skipCounts, "missingDontTakeDamage");
            }

            if (!TryGetBool(npc, "immortal", out immortal))
            {
                AddSkip(skipCounts, "missingImmortal");
            }

            var hasLife = TryGetInt(npc, "life", out life);
            if (!hasLife)
            {
                AddSkip(skipCounts, "missingLife");
            }

            var hasLifeMax = TryGetInt(npc, "lifeMax", out lifeMax);
            if (!hasLifeMax)
            {
                AddSkip(skipCounts, "missingLifeMax");
            }

            if (!isTargetDummy)
            {
                if (hide)
                {
                    skipReason = "hidden";
                    return false;
                }

                if (!hasLife || life <= 0)
                {
                    skipReason = "deadOrNoLife";
                    return false;
                }

                if (townNpc)
                {
                    skipReason = "townNpc";
                    return false;
                }

                if (friendly)
                {
                    skipReason = "friendly";
                    return false;
                }

                if (hasChaseable && !chaseable)
                {
                    skipReason = "notChaseable";
                    return false;
                }

                if (hasLifeMax && lifeMax <= 5)
                {
                    skipReason = "lifeMaxTooLow";
                    return false;
                }

                if (dontTakeDamage)
                {
                    skipReason = "dontTakeDamage";
                    return false;
                }

                if (immortal)
                {
                    skipReason = "immortal";
                    return false;
                }
            }

            float positionX;
            float positionY;
            float centerX;
            float centerY;
            float velocityX;
            float velocityY;
            var hasPosition = TryReadVector(npc, "position", out positionX, out positionY);
            var hasCenter = TryReadVector(npc, "Center", out centerX, out centerY);
            TryReadVector(npc, "velocity", out velocityX, out velocityY);

            int width;
            int height;
            if (!TryGetInt(npc, "width", out width))
            {
                width = 0;
                AddSkip(skipCounts, "missingWidth");
            }

            if (!TryGetInt(npc, "height", out height))
            {
                height = 0;
                AddSkip(skipCounts, "missingHeight");
            }

            float hitboxX;
            float hitboxY;
            float hitboxWidth;
            float hitboxHeight;
            var hasHitbox = TryReadRectangle(npc, "Hitbox", out hitboxX, out hitboxY, out hitboxWidth, out hitboxHeight);

            if (!hasCenter && hasPosition && width > 0 && height > 0)
            {
                centerX = positionX + width / 2f;
                centerY = positionY + height / 2f;
                hasCenter = true;
            }

            if (!hasPosition && hasCenter && width > 0 && height > 0)
            {
                positionX = centerX - width / 2f;
                positionY = centerY - height / 2f;
                hasPosition = true;
            }

            if (!hasHitbox && hasPosition && width > 0 && height > 0)
            {
                hitboxX = positionX;
                hitboxY = positionY;
                hitboxWidth = width;
                hitboxHeight = height;
                hasHitbox = true;
            }

            if (!hasCenter)
            {
                skipReason = "missingCenter";
                return false;
            }

            if (!hasHitbox)
            {
                hitboxX = centerX;
                hitboxY = centerY;
                hitboxWidth = 1f;
                hitboxHeight = 1f;
                AddSkip(skipCounts, "hitboxFallbackToCenter");
            }

            int npcAiStyle;
            TryGetInt(npc, "aiStyle", out npcAiStyle);
            bool noGravity;
            TryGetBool(npc, "noGravity", out noGravity);
            bool collideX;
            TryGetBool(npc, "collideX", out collideX);
            bool collideY;
            TryGetBool(npc, "collideY", out collideY);
            int direction;
            TryGetInt(npc, "direction", out direction);
            int directionY;
            TryGetInt(npc, "directionY", out directionY);
            int targetPlayer;
            if (!TryGetInt(npc, "target", out targetPlayer))
            {
                targetPlayer = -1;
            }

            float ai0;
            float ai1;
            float ai2;
            float ai3;
            var aiSummaryAvailable = TryReadAiValue(npc, 0, out ai0) &
                                     TryReadAiValue(npc, 1, out ai1) &
                                     TryReadAiValue(npc, 2, out ai2) &
                                     TryReadAiValue(npc, 3, out ai3);

            target = new CombatTargetSnapshot
            {
                WhoAmI = whoAmI,
                Type = type,
                Name = ReadNpcName(npc),
                Active = active,
                Friendly = friendly,
                TownNpc = townNpc,
                Hide = hide,
                Chaseable = chaseable,
                DontTakeDamage = dontTakeDamage,
                Immortal = immortal,
                IsTargetDummy = isTargetDummy,
                Life = life,
                LifeMax = lifeMax,
                PositionX = positionX,
                PositionY = positionY,
                Width = width,
                Height = height,
                CenterX = centerX,
                CenterY = centerY,
                VelocityX = velocityX,
                VelocityY = velocityY,
                NpcAiStyle = npcAiStyle,
                NoGravity = noGravity,
                CollideX = collideX,
                CollideY = collideY,
                Direction = direction,
                DirectionY = directionY,
                TargetPlayer = targetPlayer,
                AiSummaryAvailable = aiSummaryAvailable,
                Ai0 = ai0,
                Ai1 = ai1,
                Ai2 = ai2,
                Ai3 = ai3,
                HitboxX = hitboxX,
                HitboxY = hitboxY,
                HitboxWidth = hitboxWidth,
                HitboxHeight = hitboxHeight
            };

            return true;
        }

        private static int ResolveTargetDummyType()
        {
            if (_targetDummyTypeResolved)
            {
                return _targetDummyType;
            }

            try
            {
                _targetDummyType = NPCID.TargetDummy;
                _targetDummyTypeResolved = true;
                return _targetDummyType;
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "combat-aim-target-dummy-typed-id-failed",
                    TimeSpan.FromSeconds(30),
                    "CombatAimTargetReader",
                    "TargetDummy typed id read failed: " + error.Message);
            }

            _targetDummyType = FallbackTargetDummyType;
            _targetDummyTypeResolved = true;
            if (!_targetDummyFallbackLogged)
            {
                _targetDummyFallbackLogged = true;
                Logger.Warn(
                    "CombatAimTargetReader",
                    "Terraria.ID.NPCID.TargetDummy unavailable; using fallback type " +
                    FallbackTargetDummyType.ToString(CultureInfo.InvariantCulture) + ".");
            }

            return _targetDummyType;
        }

        private static bool TryReadStaticInt(Type type, string name, out int value)
        {
            value = 0;
            var raw = GameStateReflection.GetStaticMember(type, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadStaticVector(Type type, string name, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            var raw = GameStateReflection.GetStaticMember(type, name);
            return GameStateReflection.TryReadVector2(raw, out x, out y);
        }

        private static bool TryReadVector(object source, string name, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            var raw = GameStateReflection.GetMember(source, name);
            return GameStateReflection.TryReadVector2(raw, out x, out y);
        }

        private static bool TryReadRectangle(object source, string name, out float x, out float y, out float width, out float height)
        {
            x = 0f;
            y = 0f;
            width = 0f;
            height = 0f;
            var raw = GameStateReflection.GetMember(source, name);
            if (raw == null)
            {
                return false;
            }

            int intX;
            int intY;
            int intWidth;
            int intHeight;
            if (!TryGetInt(raw, "X", out intX) ||
                !TryGetInt(raw, "Y", out intY) ||
                !TryGetInt(raw, "Width", out intWidth) ||
                !TryGetInt(raw, "Height", out intHeight))
            {
                return false;
            }

            x = intX;
            y = intY;
            width = intWidth;
            height = intHeight;
            return true;
        }

        private static bool TryGetBool(object source, string name, out bool value)
        {
            return GameStateReflection.TryGetBool(source, name, out value);
        }

        private static bool TryGetInt(object source, string name, out int value)
        {
            return GameStateReflection.TryGetInt(source, name, out value);
        }

        private static string ReadNpcName(object npc)
        {
            var name = ReadString(npc, "FullName");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            name = ReadString(npc, "GivenName");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            name = ReadString(npc, "TypeName");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            name = ReadString(npc, "name");
            return string.IsNullOrWhiteSpace(name) ? string.Empty : name;
        }

        private static string ReadString(object source, string name)
        {
            try
            {
                var raw = GameStateReflection.GetMember(source, name);
                return raw == null ? string.Empty : Convert.ToString(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static float ReadAiValue(float[] ai, int index)
        {
            return ai != null && index >= 0 && index < ai.Length ? ai[index] : 0f;
        }

        private static bool TryReadAiValue(object npc, int index, out float value)
        {
            value = 0f;
            if (npc == null || index < 0)
            {
                return false;
            }

            var ai = GameStateReflection.AsList(GameStateReflection.GetMember(npc, "ai"));
            if (ai == null || index >= ai.Count)
            {
                return false;
            }

            try
            {
                value = Convert.ToSingle(ai[index], CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                value = 0f;
                return false;
            }
        }

        private static void AddSkip(Dictionary<string, int> counts, string reason)
        {
            if (counts == null || string.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            int count;
            counts.TryGetValue(reason, out count);
            counts[reason] = count + 1;
        }

        private static string BuildSkipSummary(Dictionary<string, int> counts)
        {
            if (counts == null || counts.Count == 0)
            {
                return "none";
            }

            var parts = new List<string>();
            foreach (var pair in counts)
            {
                parts.Add(pair.Key + "=" + pair.Value.ToString(CultureInfo.InvariantCulture));
            }

            parts.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(";", parts.ToArray());
        }
    }
}

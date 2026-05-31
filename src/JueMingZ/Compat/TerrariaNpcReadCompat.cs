using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingZ.Compat
{
    internal static class TerrariaNpcReadCompat
    {
        public static bool IsActive(NPC npc)
        {
            return npc != null && npc.active;
        }

        public static int Type(NPC npc)
        {
            return npc == null ? 0 : npc.type;
        }

        public static int WhoAmI(NPC npc)
        {
            return npc == null ? -1 : npc.whoAmI;
        }

        public static string Name(NPC npc)
        {
            return npc == null ? string.Empty : npc.FullName ?? string.Empty;
        }

        public static int NetId(NPC npc)
        {
            return npc == null ? 0 : npc.netID;
        }

        public static int Life(NPC npc)
        {
            return npc == null ? 0 : npc.life;
        }

        public static int LifeMax(NPC npc)
        {
            return npc == null ? 0 : npc.lifeMax;
        }

        public static bool IsFriendly(NPC npc)
        {
            return npc != null && npc.friendly;
        }

        public static bool IsTownNpc(NPC npc)
        {
            return npc != null && npc.townNPC;
        }

        public static bool IsBoss(NPC npc)
        {
            return npc != null && npc.boss;
        }

        public static bool IsHidden(NPC npc)
        {
            return npc != null && npc.hide;
        }

        public static bool IsChaseable(NPC npc)
        {
            return npc != null && npc.chaseable;
        }

        public static bool DontTakeDamage(NPC npc)
        {
            return npc != null && npc.dontTakeDamage;
        }

        public static bool IsImmortal(NPC npc)
        {
            return npc != null && npc.immortal;
        }

        public static int RealLife(NPC npc)
        {
            return npc == null ? -1 : npc.realLife;
        }

        public static float[] Ai(NPC npc)
        {
            return npc == null ? null : npc.ai;
        }

        public static int CatchItem(NPC npc)
        {
            return npc == null ? 0 : npc.catchItem;
        }

        public static bool IsCritter(NPC npc)
        {
            return npc != null && (npc.CountsAsACritter || npc.catchItem > 0);
        }

        public static bool IsCatchable(NPC npc)
        {
            return IsActive(npc) && npc.catchItem > 0;
        }

        public static Vector2 Position(NPC npc)
        {
            return npc == null ? Vector2.Zero : npc.position;
        }

        public static Vector2 Velocity(NPC npc)
        {
            return npc == null ? Vector2.Zero : npc.velocity;
        }

        public static Vector2 Center(NPC npc)
        {
            return npc == null ? Vector2.Zero : npc.Center;
        }

        public static Rectangle Hitbox(NPC npc)
        {
            return npc == null ? Rectangle.Empty : npc.Hitbox;
        }

        public static int Width(NPC npc)
        {
            return npc == null ? 0 : npc.width;
        }

        public static int Height(NPC npc)
        {
            return npc == null ? 0 : npc.height;
        }
    }
}

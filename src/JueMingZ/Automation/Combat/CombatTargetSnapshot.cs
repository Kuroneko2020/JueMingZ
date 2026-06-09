namespace JueMingZ.Automation.Combat
{
    // Target snapshots are scoring evidence only; action paths must revalidate live state before steering input.
    public sealed class CombatTargetSnapshot
    {
        public int WhoAmI { get; set; }
        public int Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Active { get; set; }
        public bool Friendly { get; set; }
        public bool TownNpc { get; set; }
        public bool Hide { get; set; }
        public bool Chaseable { get; set; }
        public bool DontTakeDamage { get; set; }
        public bool Immortal { get; set; }
        public bool IsTargetDummy { get; set; }
        public int Life { get; set; }
        public int LifeMax { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        public bool SmoothedVelocityAvailable { get; set; }
        public float SmoothedVelocityX { get; set; }
        public float SmoothedVelocityY { get; set; }
        public int NpcAiStyle { get; set; }
        public bool NoGravity { get; set; }
        public bool CollideX { get; set; }
        public bool CollideY { get; set; }
        public int Direction { get; set; }
        public int DirectionY { get; set; }
        public int TargetPlayer { get; set; } = -1;
        public bool AiSummaryAvailable { get; set; }
        public float Ai0 { get; set; }
        public float Ai1 { get; set; }
        public float Ai2 { get; set; }
        public float Ai3 { get; set; }
        public long LastReadTick { get; set; }
        public CombatAimTargetMotionProfile MotionProfile { get; set; }
        public float HitboxX { get; set; }
        public float HitboxY { get; set; }
        public float HitboxWidth { get; set; }
        public float HitboxHeight { get; set; }

        public CombatTargetSnapshot CloneForAimSample(float centerX, float centerY)
        {
            return new CombatTargetSnapshot
            {
                WhoAmI = WhoAmI,
                Type = Type,
                Name = Name,
                Active = Active,
                Friendly = Friendly,
                TownNpc = TownNpc,
                Hide = Hide,
                Chaseable = Chaseable,
                DontTakeDamage = DontTakeDamage,
                Immortal = Immortal,
                IsTargetDummy = IsTargetDummy,
                Life = Life,
                LifeMax = LifeMax,
                PositionX = PositionX,
                PositionY = PositionY,
                Width = Width,
                Height = Height,
                CenterX = centerX,
                CenterY = centerY,
                VelocityX = SmoothedVelocityAvailable ? SmoothedVelocityX : VelocityX,
                VelocityY = SmoothedVelocityAvailable ? SmoothedVelocityY : VelocityY,
                SmoothedVelocityAvailable = SmoothedVelocityAvailable,
                SmoothedVelocityX = SmoothedVelocityX,
                SmoothedVelocityY = SmoothedVelocityY,
                NpcAiStyle = NpcAiStyle,
                NoGravity = NoGravity,
                CollideX = CollideX,
                CollideY = CollideY,
                Direction = Direction,
                DirectionY = DirectionY,
                TargetPlayer = TargetPlayer,
                AiSummaryAvailable = AiSummaryAvailable,
                Ai0 = Ai0,
                Ai1 = Ai1,
                Ai2 = Ai2,
                Ai3 = Ai3,
                LastReadTick = LastReadTick,
                MotionProfile = MotionProfile == null ? null : MotionProfile.Clone(),
                HitboxX = HitboxX,
                HitboxY = HitboxY,
                HitboxWidth = HitboxWidth,
                HitboxHeight = HitboxHeight
            };
        }
    }
}

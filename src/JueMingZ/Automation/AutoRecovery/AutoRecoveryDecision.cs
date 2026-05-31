using JueMingZ.Actions;
using JueMingZ.Compat;
using System.Collections.Generic;

namespace JueMingZ.Automation.AutoRecovery
{
    public sealed class AutoRecoveryDecision
    {
        public string Mode { get; set; }
        public string Scenario { get; set; }
        public string FeatureId { get; set; }
        public InputActionKind ActionKind { get; set; }
        public bool ShouldEnqueue { get; set; }
        public bool CooldownBlocked { get; set; }
        public bool PotionSicknessBlocked { get; set; }
        public bool ManaSicknessBlocked { get; set; }
        public string AutoHealMode { get; set; }
        public string AutoManaMode { get; set; }
        public string TriggerReason { get; set; }
        public int ThresholdPercent { get; set; }
        public int CurrentLife { get; set; }
        public int MaxLife { get; set; }
        public int MissingLife { get; set; }
        public int LifePercent { get; set; }
        public int CurrentMana { get; set; }
        public int MaxMana { get; set; }
        public int MissingMana { get; set; }
        public int ManaPercent { get; set; }
        public int SelectedItemType { get; set; }
        public string SelectedItemName { get; set; }
        public int SelectedItemManaCost { get; set; }
        public int RequiredMana { get; set; }
        public bool CheckManaAvailable { get; set; }
        public bool CheckManaResult { get; set; }
        public bool UsedFallbackManaCostCheck { get; set; }
        public string ManaCheckReason { get; set; }
        public int BuffCountBefore { get; set; }
        public string BuffTypesBeforeJson { get; set; }
        public int CooldownTicks { get; set; }
        public int PotionDelay { get; set; }
        public int ManaSickTime { get; set; }
        public bool AutoRecoveryCooldownBlocked { get; set; }
        public bool PlayerUsingItemBlocked { get; set; }
        public string SourceContainer { get; set; }
        public int SourceSlot { get; set; }
        public int ItemType { get; set; }
        public string ItemName { get; set; }
        public int BuffType { get; set; }
        public string BuffName { get; set; }
        public int BuffTime { get; set; }
        public bool ImmediateReconcile { get; set; }
        public string ImmediateTriggerReason { get; set; }
        public int NpcIndex { get; set; }
        public int NurseHealCost { get; set; }
        public int RemovableDebuffCount { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int TileType { get; set; }
        public List<StationBuffTarget> StationBuffTargets { get; set; }

        public AutoRecoveryDecision()
        {
            Mode = string.Empty;
            Scenario = string.Empty;
            FeatureId = string.Empty;
            AutoHealMode = string.Empty;
            AutoManaMode = string.Empty;
            TriggerReason = string.Empty;
            SelectedItemName = string.Empty;
            ManaCheckReason = string.Empty;
            BuffTypesBeforeJson = "[]";
            SourceContainer = string.Empty;
            SourceSlot = -1;
            ItemName = string.Empty;
            BuffName = string.Empty;
            ImmediateTriggerReason = string.Empty;
            NpcIndex = -1;
            TileX = -1;
            TileY = -1;
            StationBuffTargets = new List<StationBuffTarget>();
        }
    }
}

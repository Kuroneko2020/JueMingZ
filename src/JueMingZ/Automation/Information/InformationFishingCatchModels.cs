using System;
using System.Collections.Generic;
using JueMingZ.Automation.Fishing.Filtering;

namespace JueMingZ.Automation.Information
{
    internal struct FishingCatchQueryKey : IEquatable<FishingCatchQueryKey>
    {
        public string Signature { get; private set; }

        public FishingCatchQueryKey(string signature)
        {
            Signature = signature ?? string.Empty;
        }

        public bool Equals(FishingCatchQueryKey other)
        {
            return string.Equals(Signature, other.Signature, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is FishingCatchQueryKey && Equals((FishingCatchQueryKey)obj);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Signature ?? string.Empty);
        }

        public override string ToString()
        {
            return Signature ?? string.Empty;
        }
    }

    internal struct FishingCatchEarlyCacheKey : IEquatable<FishingCatchEarlyCacheKey>
    {
        public string Signature { get; private set; }

        public FishingCatchEarlyCacheKey(string signature)
        {
            Signature = signature ?? string.Empty;
        }

        public bool Equals(FishingCatchEarlyCacheKey other)
        {
            return string.Equals(Signature, other.Signature, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is FishingCatchEarlyCacheKey && Equals((FishingCatchEarlyCacheKey)obj);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Signature ?? string.Empty);
        }

        public override string ToString()
        {
            return Signature ?? string.Empty;
        }
    }

    internal sealed class FishingCatchCacheEntry
    {
        public IList<FishingCatchCandidate> Candidates { get; set; }
        public string Message { get; set; }

        public FishingCatchCacheEntry()
        {
            Candidates = new List<FishingCatchCandidate>();
            Message = string.Empty;
        }
    }

    internal sealed class FishingWaterScan
    {
        public int TotalTiles;
        public bool InLava;
        public bool InHoney;
        public int Chums;
    }

    internal struct CorruptionRoll
    {
        public bool Corrupt { get; private set; }
        public bool Crimson { get; private set; }

        public CorruptionRoll(bool corrupt, bool crimson)
        {
            Corrupt = corrupt;
            Crimson = crimson;
        }
    }

    internal struct FishingCatchQuerySpec
    {
        public InformationWorldContext Context;
        public int TileX;
        public int TileY;
        public string LiquidKind;
        public int WaterTiles;
        public int Chums;
        public int WaterNeeded;
        public bool JunkPossible;
        public int FinalFishingLevel;
        public int FishingLevel;
        public int PolePower;
        public int PoleItemType;
        public int BaitPower;
        public int BaitItemType;
        public bool CanFishInLava;
        public int QuestFish;
        public string FilterSignature;
    }

    internal struct FishingCatchEarlyQuerySpec
    {
        public InformationWorldContext Context;
        public int TileX;
        public int TileY;
        public int BobberIdentity;
        public int PolePower;
        public int PoleItemType;
        public int BaitPower;
        public int BaitItemType;
        public int QuestFish;
    }

    internal struct FishingEnvironmentSnapshot
    {
        public int FinalFishingLevel;
        public int PolePower;
        public int PoleItemType;
        public int BaitPower;
        public int BaitItemType;
        public int QuestFish;
        public bool CanFishInLava;
    }

    internal struct FishingWaterPenaltyResult
    {
        public int FishingLevel;
        public int WaterNeeded;
        public bool JunkPossible;
    }

    internal struct FishingConditionRoll
    {
        public int HeightLevel;
        public bool Corrupt;
        public bool Crimson;
        public bool Jungle;
        public bool InHoney;
        public bool Snow;
        public bool Desert;
        public bool InfectedDesert;
        public bool RemixOcean;
        public bool Crate;
        public bool Junk;
    }

    internal struct FishingAttemptSpec
    {
        public InformationWorldContext Context;
        public object FishingConditions;
        public int TileX;
        public int TileY;
        public bool InLava;
        public bool InHoney;
        public int WaterTilesCount;
        public int WaterNeeded;
        public int Chums;
        public int FishingLevel;
        public bool CanFishInLava;
        public int QuestFish;
        public FishingConditionRoll Roll;
    }

    internal struct FishingRuleEvaluationRequest
    {
        public InformationWorldContext Context;
        public object Rule;
        public object FishingContext;
    }

    internal struct FishingSearchRequest
    {
        public InformationWorldContext Context;
        public string Query;
        public int MaxResults;
    }
}

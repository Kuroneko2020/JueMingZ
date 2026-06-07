using System;

namespace JueMingZ.Automation.Information
{
    internal struct ChestScanCandidate
    {
        public int ChestX;
        public int ChestY;
        public long Key;
        public int TileType;
        public int TileStyle;
        public float WorldX;
        public float WorldY;
    }

    internal struct ChestNameCacheKey : IEquatable<ChestNameCacheKey>
    {
        private readonly string _worldKey;
        private readonly string _worldRecordKey;
        private readonly int _chestX;
        private readonly int _chestY;
        private readonly int _tileType;
        private readonly int _tileStyle;
        private readonly string _languageSignature;
        private readonly string _recordSignature;

        public ChestNameCacheKey(
            string worldKey,
            string worldRecordKey,
            int chestX,
            int chestY,
            int tileType,
            int tileStyle,
            string languageSignature,
            string recordSignature)
        {
            _worldKey = worldKey ?? string.Empty;
            _worldRecordKey = worldRecordKey ?? string.Empty;
            _chestX = chestX;
            _chestY = chestY;
            _tileType = tileType;
            _tileStyle = tileStyle;
            _languageSignature = languageSignature ?? string.Empty;
            _recordSignature = recordSignature ?? string.Empty;
        }

        public bool Equals(ChestNameCacheKey other)
        {
            return _chestX == other._chestX &&
                   _chestY == other._chestY &&
                   _tileType == other._tileType &&
                   _tileStyle == other._tileStyle &&
                   string.Equals(_worldKey, other._worldKey, StringComparison.Ordinal) &&
                   string.Equals(_worldRecordKey, other._worldRecordKey, StringComparison.Ordinal) &&
                   string.Equals(_languageSignature, other._languageSignature, StringComparison.Ordinal) &&
                   string.Equals(_recordSignature, other._recordSignature, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ChestNameCacheKey && Equals((ChestNameCacheKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_worldKey ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_worldRecordKey ?? string.Empty);
                hash = hash * 31 + _chestX;
                hash = hash * 31 + _chestY;
                hash = hash * 31 + _tileType;
                hash = hash * 31 + _tileStyle;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_languageSignature ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_recordSignature ?? string.Empty);
                return hash;
            }
        }
    }

    internal struct ChestLabelCacheSignature
    {
        public uint Hash;
        public string Mode;
        public string WorldKey;
        public string WorldRecordKey;
        public string PlayerRecordKey;
        public int ScreenChunkX;
        public int ScreenChunkY;
        public int ScreenWidth;
        public int ScreenHeight;
        public int PlayerChunkX;
        public int PlayerChunkY;
        public string StyleSignature;
        public string OpenedChestsHash;

        public ChestLabelCacheSignature(
            uint hash,
            string mode,
            string worldKey,
            string worldRecordKey,
            string playerRecordKey,
            int screenChunkX,
            int screenChunkY,
            int screenWidth,
            int screenHeight,
            int playerChunkX,
            int playerChunkY,
            string styleSignature,
            string openedChestsHash)
        {
            Hash = hash;
            Mode = mode ?? string.Empty;
            WorldKey = worldKey ?? string.Empty;
            WorldRecordKey = worldRecordKey ?? string.Empty;
            PlayerRecordKey = playerRecordKey ?? string.Empty;
            ScreenChunkX = screenChunkX;
            ScreenChunkY = screenChunkY;
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            PlayerChunkX = playerChunkX;
            PlayerChunkY = playerChunkY;
            StyleSignature = styleSignature ?? string.Empty;
            OpenedChestsHash = openedChestsHash ?? string.Empty;
        }
    }

    internal sealed class ChestLabel
    {
        public int TileX;
        public int TileY;
        public float WorldX;
        public float WorldY;
        public string Name;
    }

    internal sealed class SignTextLabel
    {
        public int TileX;
        public int TileY;
        public float WorldLeft;
        public float WorldTop;
        public float WorldRight;
        public string Text;
        public int TextHash;
    }

    internal sealed class SignTextLayout
    {
        public SignTextLayout(string[] displayLines, int[] lineWidths, int lineHeight, int totalHeight, float scale, bool hasVisibleText)
        {
            DisplayLines = displayLines ?? new string[0];
            LineWidths = lineWidths ?? new int[0];
            LineHeight = lineHeight;
            TotalHeight = totalHeight;
            Scale = scale;
            HasVisibleText = hasVisibleText;
        }

        public string[] DisplayLines { get; private set; }

        public int[] LineWidths { get; private set; }

        public int LineHeight { get; private set; }

        public int TotalHeight { get; private set; }

        public float Scale { get; private set; }

        public bool HasVisibleText { get; private set; }
    }

    internal struct SignTextLayoutKey : IEquatable<SignTextLayoutKey>
    {
        private readonly string _text;
        private readonly int _textHash;
        private readonly int _textLength;
        private readonly string _mode;
        private readonly int _maxLines;
        private readonly int _maxCharacters;
        private readonly int _scaleKey;
        private readonly string _fontSignature;
        private readonly int _cacheGeneration;

        public SignTextLayoutKey(
            string text,
            int textHash,
            string mode,
            int maxLines,
            int maxCharacters,
            int scaleKey,
            string fontSignature,
            int cacheGeneration)
        {
            _text = text ?? string.Empty;
            _textHash = textHash;
            _textLength = _text.Length;
            _mode = mode ?? string.Empty;
            _maxLines = maxLines;
            _maxCharacters = maxCharacters;
            _scaleKey = scaleKey;
            _fontSignature = fontSignature ?? string.Empty;
            _cacheGeneration = cacheGeneration;
        }

        public bool Equals(SignTextLayoutKey other)
        {
            return _textHash == other._textHash &&
                   _textLength == other._textLength &&
                   _maxLines == other._maxLines &&
                   _maxCharacters == other._maxCharacters &&
                   _scaleKey == other._scaleKey &&
                   _cacheGeneration == other._cacheGeneration &&
                   string.Equals(_text, other._text, StringComparison.Ordinal) &&
                   string.Equals(_mode, other._mode, StringComparison.Ordinal) &&
                   string.Equals(_fontSignature, other._fontSignature, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SignTextLayoutKey && Equals((SignTextLayoutKey)obj);
        }

        public override int GetHashCode()
        {
            var hash = AddHash(17, _textHash);
            hash = AddHash(hash, _textLength);
            hash = AddHash(hash, HashText(_mode));
            hash = AddHash(hash, _maxLines);
            hash = AddHash(hash, _maxCharacters);
            hash = AddHash(hash, _scaleKey);
            hash = AddHash(hash, HashText(_fontSignature));
            hash = AddHash(hash, _cacheGeneration);
            return hash;
        }

        private static int HashText(string text)
        {
            unchecked
            {
                var hash = (int)2166136261;
                if (text == null)
                {
                    return hash;
                }

                for (var index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 16777619;
                }

                return hash;
            }
        }

        private static int AddHash(int hash, int value)
        {
            unchecked
            {
                return (hash * 16777619) ^ value;
            }
        }
    }

    internal sealed class InformationSignTextLayoutSnapshot
    {
        public InformationSignTextLayoutSnapshot(
            int lineCount,
            string firstLineText,
            int firstLineWidth,
            int lineHeight,
            int totalHeight,
            int rebuildCount)
        {
            LineCount = lineCount;
            FirstLineText = firstLineText ?? string.Empty;
            FirstLineWidth = firstLineWidth;
            LineHeight = lineHeight;
            TotalHeight = totalHeight;
            RebuildCount = rebuildCount;
        }

        public int LineCount { get; private set; }

        public string FirstLineText { get; private set; }

        public int FirstLineWidth { get; private set; }

        public int LineHeight { get; private set; }

        public int TotalHeight { get; private set; }

        public int RebuildCount { get; private set; }
    }

    internal sealed class NpcLabel
    {
        public int Index;
        public int WhoAmI;
        public int Type;
        public float WorldX;
        public float WorldY;
        public int Life;
        public int LifeMax;
        public bool TownNpc;
        public bool Friendly;
        public bool Hidden;
        public bool Critter;
        public string Text;
        public string HealthText;
        public int HealthSourceIndex;
        public int HealthLife;
        public int HealthLifeMax;
        public InformationColor Color;
        public float MaxDistance;
        public float FontScale;
        public float HealthFontScale;
    }

    internal struct NpcLabelSnapshot
    {
        public int Type;
        public int WhoAmI;
        public int Life;
        public int LifeMax;
        public bool TownNpc;
        public bool Friendly;
        public bool Hidden;
        public bool Critter;
        public float WorldX;
        public float WorldY;
    }

    internal sealed class NpcSegmentInfo
    {
        public int Index;
        public int WhoAmI;
        public int RealLife;
        public int GroupKey;
        public int GroupSize;
        public int NeighborCount;
        public int[] References;
    }

    internal enum NpcSegmentRole
    {
        Unknown,
        Head,
        Body,
        Tail
    }

    internal struct TileHighlightScanSignature
    {
        public uint Hash { get; private set; }
        public TileHighlightScanBounds Bounds { get; private set; }

        public TileHighlightScanSignature(uint hash, TileHighlightScanBounds bounds)
        {
            Hash = hash;
            Bounds = bounds;
        }
    }

    internal struct TileHighlightScanBounds
    {
        public int MinX { get; private set; }
        public int MinY { get; private set; }
        public int MaxX { get; private set; }
        public int MaxY { get; private set; }

        public TileHighlightScanBounds(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }
    }

    internal struct TileHighlightColors
    {
        public InformationColor LifeCrystal { get; private set; }
        public InformationColor ManaCrystal { get; private set; }
        public InformationColor Digtoise { get; private set; }
        public InformationColor LifeFruit { get; private set; }
        public InformationColor DragonEgg { get; private set; }

        public TileHighlightColors(InformationColor lifeCrystal, InformationColor manaCrystal, InformationColor digtoise, InformationColor lifeFruit, InformationColor dragonEgg)
        {
            LifeCrystal = lifeCrystal;
            ManaCrystal = manaCrystal;
            Digtoise = digtoise;
            LifeFruit = lifeFruit;
            DragonEgg = dragonEgg;
        }
    }

    internal struct TileHighlight
    {
        private const int TileSize = 16;

        public int TileX { get; private set; }
        public int TileY { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int PixelWidth { get; private set; }
        public int PixelHeight { get; private set; }
        public InformationColor Color { get; private set; }

        public TileHighlight(int tileX, int tileY, int width, int height, InformationColor color)
        {
            TileX = tileX;
            TileY = tileY;
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            PixelWidth = Width * TileSize;
            PixelHeight = Height * TileSize;
            Color = color;
        }
    }

    internal struct TilePoint
    {
        public int X;
        public int Y;

        public TilePoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}

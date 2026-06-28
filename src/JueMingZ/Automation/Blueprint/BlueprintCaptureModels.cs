using System.Collections.Generic;

namespace JueMingZ.Automation.Blueprint
{
    internal static class BlueprintCaptureCoatingFlags
    {
        public const int Fullbright = 1;
        public const int Invisible = 2;
    }

    internal static class BlueprintCaptureWireFlags
    {
        public const int Red = 1;
        public const int Blue = 2;
        public const int Green = 4;
        public const int Yellow = 8;
    }

    internal static class BlueprintCaptureMissingCapabilities
    {
        public const string LiquidNotSupported = "liquid-not-supported";
        public const string TileReadUnavailable = "tile-read-unavailable";
        public const string ExternalTextNotCaptured = "external-text-not-captured";
        public const string ContainerContentNotCaptured = "container-content-not-captured";
        public const string EquipmentContentNotCaptured = "equipment-content-not-captured";
    }

    internal interface IBlueprintWorldTileReader
    {
        bool IsWorldReady { get; }
        bool TryReadTile(int tileX, int tileY, out BlueprintWorldTileSnapshot snapshot);
    }

    internal sealed class BlueprintWorldTileSnapshot
    {
        public BlueprintWorldTileSnapshot()
        {
            TileDisplayName = string.Empty;
            WallDisplayName = string.Empty;
            ExternalDataKind = string.Empty;
        }

        public int TileX { get; set; }
        public int TileY { get; set; }
        public bool Active { get; set; }
        public int TileType { get; set; }
        public int WallType { get; set; }
        public int FrameX { get; set; }
        public int FrameY { get; set; }
        public int WallFrameX { get; set; }
        public int WallFrameY { get; set; }
        public int TilePaintId { get; set; }
        public int WallPaintId { get; set; }
        public bool TileFullbright { get; set; }
        public bool WallFullbright { get; set; }
        public bool TileInvisible { get; set; }
        public bool WallInvisible { get; set; }
        public int Slope { get; set; }
        public bool HalfBrick { get; set; }
        public bool Inactive { get; set; }
        public bool FrameImportant { get; set; }
        public int ObjectOriginX { get; set; }
        public int ObjectOriginY { get; set; }
        public int ObjectWidth { get; set; }
        public int ObjectHeight { get; set; }
        public int ObjectStyle { get; set; }
        public bool HasRedWire { get; set; }
        public bool HasBlueWire { get; set; }
        public bool HasGreenWire { get; set; }
        public bool HasYellowWire { get; set; }
        public bool HasActuator { get; set; }
        public int LiquidAmount { get; set; }
        public int LiquidType { get; set; }
        public int TileMaterialItemId { get; set; }
        public int WallMaterialItemId { get; set; }
        public int WireMaterialItemId { get; set; }
        public int ActuatorMaterialItemId { get; set; }
        public string TileDisplayName { get; set; }
        public string WallDisplayName { get; set; }
        public string ExternalDataKind { get; set; }
    }

    internal sealed class BlueprintCaptureResult
    {
        private BlueprintCaptureResult()
        {
            ResultCode = string.Empty;
            Message = string.Empty;
            Template = new BlueprintTemplateRecord();
            SavedTemplate = new BlueprintTemplateRecord();
            StorageResult = BlueprintStorageOperationResult.Success("notSaved", "not saved", string.Empty);
        }

        public bool Succeeded { get; private set; }
        public string ResultCode { get; private set; }
        public string Message { get; private set; }
        public BlueprintTemplateRecord Template { get; private set; }
        public BlueprintTemplateRecord SavedTemplate { get; private set; }
        public BlueprintStorageOperationResult StorageResult { get; private set; }
        public int MaskSelectedCount { get; private set; }
        public int CapturedCellCount { get; private set; }
        public int CapturedLayerCount { get; private set; }
        public int SkippedAirCellCount { get; private set; }
        public int UnavailableCellCount { get; private set; }
        public bool UseAfterSave { get; private set; }

        public static BlueprintCaptureResult Success(
            string resultCode,
            string message,
            BlueprintTemplateRecord template,
            BlueprintTemplateRecord savedTemplate,
            BlueprintStorageOperationResult storageResult,
            int maskSelectedCount,
            int capturedCellCount,
            int capturedLayerCount,
            int skippedAirCellCount,
            int unavailableCellCount,
            bool useAfterSave)
        {
            return new BlueprintCaptureResult
            {
                Succeeded = true,
                ResultCode = resultCode ?? string.Empty,
                Message = message ?? string.Empty,
                Template = template == null ? new BlueprintTemplateRecord() : template.Clone(),
                SavedTemplate = savedTemplate == null ? new BlueprintTemplateRecord() : savedTemplate.Clone(),
                StorageResult = storageResult ?? BlueprintStorageOperationResult.Success("saved", "saved", string.Empty),
                MaskSelectedCount = maskSelectedCount,
                CapturedCellCount = capturedCellCount,
                CapturedLayerCount = capturedLayerCount,
                SkippedAirCellCount = skippedAirCellCount,
                UnavailableCellCount = unavailableCellCount,
                UseAfterSave = useAfterSave
            };
        }

        public static BlueprintCaptureResult Failure(
            string resultCode,
            string message,
            BlueprintTemplateRecord template,
            BlueprintStorageOperationResult storageResult,
            int maskSelectedCount,
            int capturedCellCount,
            int capturedLayerCount,
            int skippedAirCellCount,
            int unavailableCellCount,
            bool useAfterSave)
        {
            return new BlueprintCaptureResult
            {
                Succeeded = false,
                ResultCode = resultCode ?? string.Empty,
                Message = message ?? string.Empty,
                Template = template == null ? new BlueprintTemplateRecord() : template.Clone(),
                SavedTemplate = new BlueprintTemplateRecord(),
                StorageResult = storageResult ?? BlueprintStorageOperationResult.Failure(resultCode, message, string.Empty),
                MaskSelectedCount = maskSelectedCount,
                CapturedCellCount = capturedCellCount,
                CapturedLayerCount = capturedLayerCount,
                SkippedAirCellCount = skippedAirCellCount,
                UnavailableCellCount = unavailableCellCount,
                UseAfterSave = useAfterSave
            };
        }
    }

    internal sealed class BlueprintCaptureBuildState
    {
        public BlueprintCaptureBuildState()
        {
            Cells = new List<BlueprintCellRecord>();
            MaterialsByKey = new Dictionary<string, BlueprintMaterialEntry>(System.StringComparer.Ordinal);
            MissingFlags = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            CountedObjects = new HashSet<string>(System.StringComparer.Ordinal);
        }

        public List<BlueprintCellRecord> Cells { get; private set; }
        public Dictionary<string, BlueprintMaterialEntry> MaterialsByKey { get; private set; }
        public HashSet<string> MissingFlags { get; private set; }
        public HashSet<string> CountedObjects { get; private set; }
        public int LayerCount { get; set; }
        public int SkippedAirCount { get; set; }
        public int UnavailableCount { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace JueMingZ.Automation.Blueprint
{
    public static class BlueprintStorageConstants
    {
        public const int SchemaVersion = 1;
        public const string DefaultTemplateName = "新蓝图";
        public const string TemplatesFileName = "templates.json";
        public const string ExportSchemaKind = "JueMingZ.Blueprint.Template";
        public const string ImportDirectoryName = "imports";
        public const string ExportDirectoryName = "exports";
        public const string WorldInstancesDirectoryName = "worlds";

        public static string FormatUtc(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public static class BlueprintLayerKinds
    {
        public const string Tile = "tile";
        public const string Wall = "wall";
        public const string Wire = "wire";
        public const string Actuator = "actuator";
        public const string PaintCoating = "paintCoating";
        public const string Object = "object";
    }

    [DataContract]
    public sealed class BlueprintTemplateLibraryFile
    {
        [DataMember(Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Order = 2)]
        public List<BlueprintTemplateRecord> Templates { get; set; }

        [DataMember(Order = 3)]
        public string UpdatedUtc { get; set; }

        public BlueprintTemplateLibraryFile()
        {
            SchemaVersion = BlueprintStorageConstants.SchemaVersion;
            Templates = new List<BlueprintTemplateRecord>();
            UpdatedUtc = string.Empty;
        }
    }

    [DataContract]
    public sealed class BlueprintWorldInstanceFile
    {
        [DataMember(Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Order = 2)]
        public string WorldPairKey { get; set; }

        [DataMember(Order = 3)]
        public string WorldKey { get; set; }

        [DataMember(Order = 4)]
        public List<BlueprintWorldInstanceRecord> Instances { get; set; }

        [DataMember(Order = 5)]
        public string UpdatedUtc { get; set; }

        public BlueprintWorldInstanceFile()
        {
            SchemaVersion = BlueprintStorageConstants.SchemaVersion;
            WorldPairKey = string.Empty;
            WorldKey = string.Empty;
            Instances = new List<BlueprintWorldInstanceRecord>();
            UpdatedUtc = string.Empty;
        }
    }

    [DataContract]
    public sealed class BlueprintTemplateExportFile
    {
        [DataMember(Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Order = 2)]
        public string SchemaKind { get; set; }

        [DataMember(Order = 3)]
        public string ExportedUtc { get; set; }

        [DataMember(Order = 4)]
        public BlueprintTemplateRecord Template { get; set; }

        public BlueprintTemplateExportFile()
        {
            SchemaVersion = BlueprintStorageConstants.SchemaVersion;
            SchemaKind = BlueprintStorageConstants.ExportSchemaKind;
            ExportedUtc = string.Empty;
            Template = new BlueprintTemplateRecord();
        }
    }

    [DataContract]
    public sealed class BlueprintTemplateRecord
    {
        [DataMember(Order = 1)]
        public string TemplateId { get; set; }

        [DataMember(Order = 2)]
        public string Name { get; set; }

        [DataMember(Order = 3)]
        public string CreatedUtc { get; set; }

        [DataMember(Order = 4)]
        public string UpdatedUtc { get; set; }

        [DataMember(Order = 5)]
        public int Width { get; set; }

        [DataMember(Order = 6)]
        public int Height { get; set; }

        [DataMember(Order = 7)]
        public int AnchorX { get; set; }

        [DataMember(Order = 8)]
        public int AnchorY { get; set; }

        [DataMember(Order = 9)]
        public int FormatVersion { get; set; }

        [DataMember(Order = 10)]
        public List<string> MissingCapabilityFlags { get; set; }

        [DataMember(Order = 11)]
        public List<BlueprintCellRecord> Cells { get; set; }

        [DataMember(Order = 12)]
        public List<BlueprintMaterialEntry> Materials { get; set; }

        public BlueprintTemplateRecord()
        {
            TemplateId = string.Empty;
            Name = string.Empty;
            CreatedUtc = string.Empty;
            UpdatedUtc = string.Empty;
            FormatVersion = BlueprintStorageConstants.SchemaVersion;
            MissingCapabilityFlags = new List<string>();
            Cells = new List<BlueprintCellRecord>();
            Materials = new List<BlueprintMaterialEntry>();
        }

        public BlueprintTemplateRecord Clone()
        {
            return new BlueprintTemplateRecord
            {
                TemplateId = TemplateId ?? string.Empty,
                Name = Name ?? string.Empty,
                CreatedUtc = CreatedUtc ?? string.Empty,
                UpdatedUtc = UpdatedUtc ?? string.Empty,
                Width = Width,
                Height = Height,
                AnchorX = AnchorX,
                AnchorY = AnchorY,
                FormatVersion = FormatVersion,
                MissingCapabilityFlags = CloneStringList(MissingCapabilityFlags),
                Cells = CloneCellList(Cells),
                Materials = CloneMaterialList(Materials)
            };
        }

        private static List<string> CloneStringList(IList<string> source)
        {
            var clone = new List<string>();
            if (source == null)
            {
                return clone;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var value = string.IsNullOrWhiteSpace(source[index]) ? string.Empty : source[index].Trim();
                if (value.Length > 0)
                {
                    clone.Add(value);
                }
            }

            return clone;
        }

        private static List<BlueprintCellRecord> CloneCellList(IList<BlueprintCellRecord> source)
        {
            var clone = new List<BlueprintCellRecord>();
            if (source == null)
            {
                return clone;
            }

            for (var index = 0; index < source.Count; index++)
            {
                if (source[index] != null)
                {
                    clone.Add(source[index].Clone());
                }
            }

            return clone;
        }

        private static List<BlueprintMaterialEntry> CloneMaterialList(IList<BlueprintMaterialEntry> source)
        {
            var clone = new List<BlueprintMaterialEntry>();
            if (source == null)
            {
                return clone;
            }

            for (var index = 0; index < source.Count; index++)
            {
                if (source[index] != null)
                {
                    clone.Add(source[index].Clone());
                }
            }

            return clone;
        }
    }

    [DataContract]
    public sealed class BlueprintCellRecord
    {
        [DataMember(Order = 1)]
        public int X { get; set; }

        [DataMember(Order = 2)]
        public int Y { get; set; }

        [DataMember(Order = 3)]
        public List<BlueprintCellLayerRecord> Layers { get; set; }

        public BlueprintCellRecord()
        {
            Layers = new List<BlueprintCellLayerRecord>();
        }

        public BlueprintCellRecord Clone()
        {
            var clone = new BlueprintCellRecord
            {
                X = X,
                Y = Y,
                Layers = new List<BlueprintCellLayerRecord>()
            };

            for (var index = 0; Layers != null && index < Layers.Count; index++)
            {
                if (Layers[index] != null)
                {
                    clone.Layers.Add(Layers[index].Clone());
                }
            }

            return clone;
        }
    }

    [DataContract]
    public sealed class BlueprintCellLayerRecord
    {
        [DataMember(Order = 1)]
        public string LayerKind { get; set; }

        [DataMember(Order = 2)]
        public int ContentId { get; set; }

        [DataMember(Order = 3)]
        public int Style { get; set; }

        [DataMember(Order = 4)]
        public int FrameX { get; set; }

        [DataMember(Order = 5)]
        public int FrameY { get; set; }

        [DataMember(Order = 6)]
        public int PaintId { get; set; }

        [DataMember(Order = 7)]
        public int CoatingFlags { get; set; }

        [DataMember(Order = 8)]
        public int Slope { get; set; }

        [DataMember(Order = 9)]
        public bool HalfBrick { get; set; }

        [DataMember(Order = 10)]
        public bool Inactive { get; set; }

        [DataMember(Order = 11)]
        public int MaterialItemId { get; set; }

        [DataMember(Order = 12)]
        public int MaterialStack { get; set; }

        [DataMember(Order = 13)]
        public string Note { get; set; }

        [DataMember(Order = 14)]
        public string ObjectGroupId { get; set; }

        [DataMember(Order = 15)]
        public int ObjectOriginX { get; set; }

        [DataMember(Order = 16)]
        public int ObjectOriginY { get; set; }

        [DataMember(Order = 17)]
        public int ObjectWidth { get; set; }

        [DataMember(Order = 18)]
        public int ObjectHeight { get; set; }

        [DataMember(Order = 19)]
        public int ObjectSubTileX { get; set; }

        [DataMember(Order = 20)]
        public int ObjectSubTileY { get; set; }

        [DataMember(Order = 21)]
        public string ObjectGroupStatus { get; set; }

        [DataMember(Order = 22)]
        public string ObjectGroupReason { get; set; }

        public BlueprintCellLayerRecord()
        {
            LayerKind = string.Empty;
            Note = string.Empty;
            ObjectGroupId = string.Empty;
            ObjectGroupStatus = string.Empty;
            ObjectGroupReason = string.Empty;
        }

        public BlueprintCellLayerRecord Clone()
        {
            return new BlueprintCellLayerRecord
            {
                LayerKind = LayerKind ?? string.Empty,
                ContentId = ContentId,
                Style = Style,
                FrameX = FrameX,
                FrameY = FrameY,
                PaintId = PaintId,
                CoatingFlags = CoatingFlags,
                Slope = Slope,
                HalfBrick = HalfBrick,
                Inactive = Inactive,
                MaterialItemId = MaterialItemId,
                MaterialStack = MaterialStack,
                Note = Note ?? string.Empty,
                ObjectGroupId = ObjectGroupId ?? string.Empty,
                ObjectOriginX = ObjectOriginX,
                ObjectOriginY = ObjectOriginY,
                ObjectWidth = ObjectWidth,
                ObjectHeight = ObjectHeight,
                ObjectSubTileX = ObjectSubTileX,
                ObjectSubTileY = ObjectSubTileY,
                ObjectGroupStatus = ObjectGroupStatus ?? string.Empty,
                ObjectGroupReason = ObjectGroupReason ?? string.Empty
            };
        }
    }

    [DataContract]
    public sealed class BlueprintMaterialEntry
    {
        [DataMember(Order = 1)]
        public int ItemId { get; set; }

        [DataMember(Order = 2)]
        public int RequiredStack { get; set; }

        [DataMember(Order = 3)]
        public string DisplayNameSnapshot { get; set; }

        [DataMember(Order = 4)]
        public string LayerKind { get; set; }

        [DataMember(Order = 5)]
        public string Source { get; set; }

        public BlueprintMaterialEntry()
        {
            DisplayNameSnapshot = string.Empty;
            LayerKind = string.Empty;
            Source = string.Empty;
        }

        public BlueprintMaterialEntry Clone()
        {
            return new BlueprintMaterialEntry
            {
                ItemId = ItemId,
                RequiredStack = RequiredStack,
                DisplayNameSnapshot = DisplayNameSnapshot ?? string.Empty,
                LayerKind = LayerKind ?? string.Empty,
                Source = Source ?? string.Empty
            };
        }
    }

    [DataContract]
    public sealed class BlueprintEraseMaskCellRecord
    {
        [DataMember(Order = 1)]
        public int X { get; set; }

        [DataMember(Order = 2)]
        public int Y { get; set; }
    }

    [DataContract]
    public sealed class BlueprintCompletedLayerRecord
    {
        [DataMember(Order = 1)]
        public int X { get; set; }

        [DataMember(Order = 2)]
        public int Y { get; set; }

        [DataMember(Order = 3)]
        public string LayerKind { get; set; }

        [DataMember(Order = 4)]
        public string CoverageGroup { get; set; }

        [DataMember(Order = 5)]
        public int ContentId { get; set; }

        [DataMember(Order = 6)]
        public int Style { get; set; }

        public BlueprintCompletedLayerRecord()
        {
            LayerKind = string.Empty;
            CoverageGroup = string.Empty;
        }
    }

    [DataContract]
    public sealed class BlueprintWorldInstanceRecord
    {
        [DataMember(Order = 1)]
        public string InstanceId { get; set; }

        [DataMember(Order = 2)]
        public string Name { get; set; }

        [DataMember(Order = 3)]
        public string TemplateIdSnapshot { get; set; }

        [DataMember(Order = 4)]
        public string WorldPairKey { get; set; }

        [DataMember(Order = 5)]
        public string WorldKey { get; set; }

        [DataMember(Order = 6)]
        public int OriginTileX { get; set; }

        [DataMember(Order = 7)]
        public int OriginTileY { get; set; }

        [DataMember(Order = 8)]
        public bool Hidden { get; set; }

        [DataMember(Order = 9)]
        public int LayerOrder { get; set; }

        [DataMember(Order = 10)]
        public bool MaterialWindowVisible { get; set; }

        [DataMember(Order = 11)]
        public List<BlueprintEraseMaskCellRecord> EraseMask { get; set; }

        [DataMember(Order = 12)]
        public BlueprintTemplateRecord TemplateSnapshot { get; set; }

        [DataMember(Order = 13)]
        public string CreatedUtc { get; set; }

        [DataMember(Order = 14)]
        public string UpdatedUtc { get; set; }

        [DataMember(Order = 15)]
        public string AutoPlacementProgressState { get; set; }

        [DataMember(Order = 16)]
        public List<BlueprintCompletedLayerRecord> CompletedLayers { get; set; }

        public BlueprintWorldInstanceRecord()
        {
            InstanceId = string.Empty;
            Name = string.Empty;
            TemplateIdSnapshot = string.Empty;
            WorldPairKey = string.Empty;
            WorldKey = string.Empty;
            MaterialWindowVisible = true;
            EraseMask = new List<BlueprintEraseMaskCellRecord>();
            TemplateSnapshot = new BlueprintTemplateRecord();
            CreatedUtc = string.Empty;
            UpdatedUtc = string.Empty;
            AutoPlacementProgressState = string.Empty;
            CompletedLayers = new List<BlueprintCompletedLayerRecord>();
        }

        public BlueprintWorldInstanceRecord Clone()
        {
            return new BlueprintWorldInstanceRecord
            {
                InstanceId = InstanceId ?? string.Empty,
                Name = Name ?? string.Empty,
                TemplateIdSnapshot = TemplateIdSnapshot ?? string.Empty,
                WorldPairKey = WorldPairKey ?? string.Empty,
                WorldKey = WorldKey ?? string.Empty,
                OriginTileX = OriginTileX,
                OriginTileY = OriginTileY,
                Hidden = Hidden,
                LayerOrder = LayerOrder,
                MaterialWindowVisible = MaterialWindowVisible,
                EraseMask = CloneEraseMask(EraseMask),
                TemplateSnapshot = TemplateSnapshot == null ? new BlueprintTemplateRecord() : TemplateSnapshot.Clone(),
                CreatedUtc = CreatedUtc ?? string.Empty,
                UpdatedUtc = UpdatedUtc ?? string.Empty,
                AutoPlacementProgressState = AutoPlacementProgressState ?? string.Empty,
                CompletedLayers = CloneCompletedLayers(CompletedLayers)
            };
        }

        private static List<BlueprintEraseMaskCellRecord> CloneEraseMask(IList<BlueprintEraseMaskCellRecord> source)
        {
            var clone = new List<BlueprintEraseMaskCellRecord>();
            if (source == null)
            {
                return clone;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var cell = source[index];
                if (cell != null)
                {
                    clone.Add(new BlueprintEraseMaskCellRecord { X = cell.X, Y = cell.Y });
                }
            }

            return clone;
        }

        private static List<BlueprintCompletedLayerRecord> CloneCompletedLayers(IList<BlueprintCompletedLayerRecord> source)
        {
            var clone = new List<BlueprintCompletedLayerRecord>();
            if (source == null)
            {
                return clone;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var layer = source[index];
                if (layer != null)
                {
                    clone.Add(new BlueprintCompletedLayerRecord
                    {
                        X = layer.X,
                        Y = layer.Y,
                        LayerKind = layer.LayerKind ?? string.Empty,
                        CoverageGroup = layer.CoverageGroup ?? string.Empty,
                        ContentId = layer.ContentId,
                        Style = layer.Style
                    });
                }
            }

            return clone;
        }
    }

    public sealed class BlueprintTemplateLibrarySnapshot
    {
        private readonly List<BlueprintTemplateRecord> _templates;

        public BlueprintTemplateLibrarySnapshot(IList<BlueprintTemplateRecord> templates, string sourcePath, int revision)
        {
            _templates = CloneTemplates(templates);
            SourcePath = sourcePath ?? string.Empty;
            Revision = revision;
        }

        public IReadOnlyList<BlueprintTemplateRecord> Templates
        {
            get { return _templates; }
        }

        public string SourcePath { get; private set; }
        public int Revision { get; private set; }

        private static List<BlueprintTemplateRecord> CloneTemplates(IList<BlueprintTemplateRecord> source)
        {
            var clone = new List<BlueprintTemplateRecord>();
            if (source == null)
            {
                return clone;
            }

            for (var index = 0; index < source.Count; index++)
            {
                if (source[index] != null)
                {
                    clone.Add(source[index].Clone());
                }
            }

            return clone;
        }
    }

    public sealed class BlueprintWorldInstanceSnapshot
    {
        private readonly List<BlueprintWorldInstanceRecord> _instances;

        public BlueprintWorldInstanceSnapshot(string worldPairKey, string worldKey, IList<BlueprintWorldInstanceRecord> instances, string sourcePath, int revision)
        {
            WorldPairKey = worldPairKey ?? string.Empty;
            WorldKey = worldKey ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            Revision = revision;
            _instances = CloneInstances(instances);
        }

        public string WorldPairKey { get; private set; }
        public string WorldKey { get; private set; }
        public string SourcePath { get; private set; }
        public int Revision { get; private set; }

        public IReadOnlyList<BlueprintWorldInstanceRecord> Instances
        {
            get { return _instances; }
        }

        private static List<BlueprintWorldInstanceRecord> CloneInstances(IList<BlueprintWorldInstanceRecord> source)
        {
            var clone = new List<BlueprintWorldInstanceRecord>();
            if (source == null)
            {
                return clone;
            }

            for (var index = 0; index < source.Count; index++)
            {
                if (source[index] != null)
                {
                    clone.Add(source[index].Clone());
                }
            }

            return clone;
        }
    }

    public sealed class BlueprintStorageOperationResult
    {
        public bool Succeeded { get; private set; }
        public string ResultCode { get; private set; }
        public string Message { get; private set; }
        public string Path { get; private set; }

        private BlueprintStorageOperationResult(bool succeeded, string resultCode, string message, string path)
        {
            Succeeded = succeeded;
            ResultCode = resultCode ?? string.Empty;
            Message = message ?? string.Empty;
            Path = path ?? string.Empty;
        }

        public static BlueprintStorageOperationResult Success(string resultCode, string message, string path)
        {
            return new BlueprintStorageOperationResult(true, resultCode, message, path);
        }

        public static BlueprintStorageOperationResult Failure(string resultCode, string message, string path)
        {
            return new BlueprintStorageOperationResult(false, resultCode, message, path);
        }
    }
}

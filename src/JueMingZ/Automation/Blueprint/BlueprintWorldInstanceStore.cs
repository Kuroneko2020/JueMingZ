using System;
using System.Collections.Generic;
using System.IO;

namespace JueMingZ.Automation.Blueprint
{
    public sealed class BlueprintWorldInstanceStore
    {
        private readonly string _rootDirectory;
        private int _revision;

        public BlueprintWorldInstanceStore()
            : this(BlueprintStoragePaths.GetDefaultRootDirectory())
        {
        }

        public BlueprintWorldInstanceStore(string rootDirectory)
        {
            _rootDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(rootDirectory) ? BlueprintStoragePaths.GetDefaultRootDirectory() : rootDirectory);
        }

        public string RootDirectory
        {
            get { return _rootDirectory; }
        }

        public string BuildWorldPath(string worldPairKey)
        {
            return BlueprintStoragePaths.BuildWorldInstancesPath(_rootDirectory, worldPairKey);
        }

        public BlueprintStorageOperationResult TryLoadWorld(string worldPairKey, out BlueprintWorldInstanceSnapshot snapshot)
        {
            var normalizedPair = NormalizeWorldKey(worldPairKey);
            var path = BuildWorldPath(normalizedPair);
            snapshot = new BlueprintWorldInstanceSnapshot(normalizedPair, string.Empty, new List<BlueprintWorldInstanceRecord>(), path, _revision);

            if (string.IsNullOrWhiteSpace(normalizedPair))
            {
                return BlueprintStorageOperationResult.Failure("invalidWorldPair", "world pair key unavailable", path);
            }

            BlueprintWorldInstanceFile file;
            var read = BlueprintJsonSafeFileStore.TryRead(path, out file);
            if (!read.Succeeded)
            {
                return read;
            }

            if (string.Equals(read.ResultCode, "missing", StringComparison.Ordinal))
            {
                return read;
            }

            if (file == null ||
                (!string.IsNullOrWhiteSpace(file.WorldPairKey) &&
                 !string.Equals(file.WorldPairKey, normalizedPair, StringComparison.Ordinal)))
            {
                return BlueprintStorageOperationResult.Failure("worldPairMismatch", "world pair key mismatch", path);
            }

            var worldKey = NormalizeWorldKey(file.WorldKey);
            var instances = NormalizeInstances(normalizedPair, worldKey, file.Instances);
            snapshot = new BlueprintWorldInstanceSnapshot(normalizedPair, worldKey, instances, path, _revision);
            return read;
        }

        public BlueprintStorageOperationResult CreateInstanceFromTemplate(
            string worldPairKey,
            string worldKey,
            BlueprintTemplateRecord template,
            int originTileX,
            int originTileY,
            int layerOrder,
            out BlueprintWorldInstanceRecord instance)
        {
            instance = null;
            if (template == null)
            {
                return BlueprintStorageOperationResult.Failure("invalidTemplate", "template unavailable", BuildWorldPath(worldPairKey));
            }

            BlueprintWorldInstanceSnapshot snapshot;
            var load = TryLoadWorld(worldPairKey, out snapshot);
            if (!load.Succeeded)
            {
                return load;
            }

            var normalizedPair = NormalizeWorldKey(worldPairKey);
            var normalizedWorld = NormalizeWorldKey(worldKey);
            var instances = CloneInstances(snapshot.Instances);
            var now = BlueprintStorageConstants.FormatUtc(DateTime.UtcNow);
            var record = new BlueprintWorldInstanceRecord
            {
                InstanceId = "instance-" + Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(template.Name) ? BlueprintStorageConstants.DefaultTemplateName : template.Name.Trim(),
                TemplateIdSnapshot = template.TemplateId ?? string.Empty,
                WorldPairKey = normalizedPair,
                WorldKey = normalizedWorld,
                OriginTileX = originTileX,
                OriginTileY = originTileY,
                Hidden = false,
                LayerOrder = layerOrder,
                MaterialWindowVisible = true,
                // Placed instances own a snapshot copy. Later template rename,
                // delete, erase, move, or overlap resolution must not write back
                // to the blueprint library template.
                TemplateSnapshot = template.Clone(),
                CreatedUtc = now,
                UpdatedUtc = now
            };

            instances.Add(record);
            BlueprintWorldInstanceSnapshot saved;
            var save = SaveWorldInstances(normalizedPair, normalizedWorld, instances, out saved);
            if (!save.Succeeded)
            {
                return save;
            }

            instance = record.Clone();
            return save;
        }

        public BlueprintStorageOperationResult SaveWorldInstances(
            string worldPairKey,
            string worldKey,
            IList<BlueprintWorldInstanceRecord> instances,
            out BlueprintWorldInstanceSnapshot snapshot)
        {
            var normalizedPair = NormalizeWorldKey(worldPairKey);
            var normalizedWorld = NormalizeWorldKey(worldKey);
            var path = BuildWorldPath(normalizedPair);
            snapshot = new BlueprintWorldInstanceSnapshot(normalizedPair, normalizedWorld, new List<BlueprintWorldInstanceRecord>(), path, _revision);

            if (string.IsNullOrWhiteSpace(normalizedPair))
            {
                return BlueprintStorageOperationResult.Failure("invalidWorldPair", "world pair key unavailable", path);
            }

            var normalizedInstances = NormalizeInstances(normalizedPair, normalizedWorld, instances);
            var file = new BlueprintWorldInstanceFile
            {
                SchemaVersion = BlueprintStorageConstants.SchemaVersion,
                WorldPairKey = normalizedPair,
                WorldKey = normalizedWorld,
                Instances = normalizedInstances,
                UpdatedUtc = BlueprintStorageConstants.FormatUtc(DateTime.UtcNow)
            };
            var result = BlueprintJsonSafeFileStore.TryWrite(path, file);
            if (result.Succeeded)
            {
                _revision++;
                snapshot = new BlueprintWorldInstanceSnapshot(normalizedPair, normalizedWorld, normalizedInstances, path, _revision);
            }

            return result;
        }

        private static List<BlueprintWorldInstanceRecord> NormalizeInstances(
            string worldPairKey,
            string worldKey,
            IList<BlueprintWorldInstanceRecord> source)
        {
            var normalized = new List<BlueprintWorldInstanceRecord>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return normalized;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var instance = source[index];
                if (instance == null)
                {
                    continue;
                }

                var clone = instance.Clone();
                clone.InstanceId = BlueprintTemplateLibraryStore.NormalizeId(clone.InstanceId);
                if (string.IsNullOrEmpty(clone.InstanceId))
                {
                    clone.InstanceId = "instance-" + Guid.NewGuid().ToString("N");
                }

                if (!seenIds.Add(clone.InstanceId))
                {
                    continue;
                }

                clone.Name = string.IsNullOrWhiteSpace(clone.Name) ? BlueprintStorageConstants.DefaultTemplateName : clone.Name.Trim();
                clone.TemplateIdSnapshot = clone.TemplateIdSnapshot ?? string.Empty;
                clone.WorldPairKey = worldPairKey ?? string.Empty;
                clone.WorldKey = worldKey ?? string.Empty;
                clone.LayerOrder = Math.Max(0, clone.LayerOrder);
                clone.EraseMask = NormalizeEraseMask(clone.EraseMask);
                clone.CompletedLayers = NormalizeCompletedLayers(clone.CompletedLayers);
                clone.TemplateSnapshot = clone.TemplateSnapshot == null ? new BlueprintTemplateRecord() : clone.TemplateSnapshot.Clone();
                BlueprintObjectGroupNormalizer.NormalizeTemplateInPlace(clone.TemplateSnapshot);
                if (string.IsNullOrWhiteSpace(clone.TemplateSnapshot.Name))
                {
                    clone.TemplateSnapshot.Name = clone.Name;
                }

                clone.CreatedUtc = clone.CreatedUtc ?? string.Empty;
                clone.UpdatedUtc = clone.UpdatedUtc ?? string.Empty;
                normalized.Add(clone);
            }

            normalized.Sort((left, right) =>
            {
                var layer = left.LayerOrder.CompareTo(right.LayerOrder);
                return layer != 0 ? layer : string.Compare(left.InstanceId, right.InstanceId, StringComparison.Ordinal);
            });

            return normalized;
        }

        private static List<BlueprintEraseMaskCellRecord> NormalizeEraseMask(IList<BlueprintEraseMaskCellRecord> source)
        {
            var normalized = new List<BlueprintEraseMaskCellRecord>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (source == null)
            {
                return normalized;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var cell = source[index];
                if (cell == null)
                {
                    continue;
                }

                var key = cell.X.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                          cell.Y.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (seen.Add(key))
                {
                    normalized.Add(new BlueprintEraseMaskCellRecord { X = cell.X, Y = cell.Y });
                }
            }

            return normalized;
        }

        private static List<BlueprintCompletedLayerRecord> NormalizeCompletedLayers(IList<BlueprintCompletedLayerRecord> source)
        {
            var normalized = new List<BlueprintCompletedLayerRecord>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (source == null)
            {
                return normalized;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var layer = source[index];
                if (layer == null)
                {
                    continue;
                }

                var kind = string.IsNullOrWhiteSpace(layer.LayerKind) ? string.Empty : layer.LayerKind.Trim();
                var group = string.IsNullOrWhiteSpace(layer.CoverageGroup) ? string.Empty : layer.CoverageGroup.Trim();
                if (kind.Length <= 0 || group.Length <= 0)
                {
                    continue;
                }

                var key = layer.X.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                          layer.Y.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                          kind + ":" +
                          group + ":" +
                          layer.ContentId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                          layer.Style.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (seen.Add(key))
                {
                    normalized.Add(new BlueprintCompletedLayerRecord
                    {
                        X = layer.X,
                        Y = layer.Y,
                        LayerKind = kind,
                        CoverageGroup = group,
                        ContentId = layer.ContentId,
                        Style = layer.Style
                    });
                }
            }

            normalized.Sort((left, right) =>
            {
                var y = left.Y.CompareTo(right.Y);
                if (y != 0) return y;
                var x = left.X.CompareTo(right.X);
                if (x != 0) return x;
                var kind = string.Compare(left.LayerKind, right.LayerKind, StringComparison.Ordinal);
                if (kind != 0) return kind;
                var group = string.Compare(left.CoverageGroup, right.CoverageGroup, StringComparison.Ordinal);
                if (group != 0) return group;
                var content = left.ContentId.CompareTo(right.ContentId);
                return content != 0 ? content : left.Style.CompareTo(right.Style);
            });
            return normalized;
        }

        private static List<BlueprintWorldInstanceRecord> CloneInstances(IReadOnlyList<BlueprintWorldInstanceRecord> source)
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

        private static string NormalizeWorldKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}

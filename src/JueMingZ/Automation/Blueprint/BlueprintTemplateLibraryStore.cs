using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace JueMingZ.Automation.Blueprint
{
    public sealed class BlueprintTemplateLibraryStore
    {
        private readonly string _rootDirectory;
        private int _revision;

        public BlueprintTemplateLibraryStore()
            : this(BlueprintStoragePaths.GetDefaultRootDirectory())
        {
        }

        public BlueprintTemplateLibraryStore(string rootDirectory)
        {
            _rootDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(rootDirectory) ? BlueprintStoragePaths.GetDefaultRootDirectory() : rootDirectory);
        }

        public string RootDirectory
        {
            get { return _rootDirectory; }
        }

        public string LibraryPath
        {
            get { return BlueprintStoragePaths.BuildTemplateLibraryPath(_rootDirectory); }
        }

        public BlueprintStorageOperationResult TryLoad(out BlueprintTemplateLibrarySnapshot snapshot)
        {
            snapshot = new BlueprintTemplateLibrarySnapshot(new List<BlueprintTemplateRecord>(), LibraryPath, _revision);

            BlueprintTemplateLibraryFile file;
            var read = BlueprintJsonSafeFileStore.TryRead(LibraryPath, out file);
            if (!read.Succeeded)
            {
                return read;
            }

            var templates = string.Equals(read.ResultCode, "missing", StringComparison.Ordinal)
                ? new List<BlueprintTemplateRecord>()
                : NormalizeTemplates(file == null ? null : file.Templates);
            snapshot = new BlueprintTemplateLibrarySnapshot(templates, LibraryPath, _revision);
            return read;
        }

        public BlueprintStorageOperationResult CreateDefaultTemplate(int width, int height, out BlueprintTemplateRecord template)
        {
            return CreateTemplate(new BlueprintTemplateRecord
            {
                Width = width,
                Height = height,
                AnchorX = Math.Max(0, (width - 1) / 2),
                AnchorY = Math.Max(0, (height - 1) / 2)
            }, out template);
        }

        public BlueprintStorageOperationResult CreateTemplate(BlueprintTemplateRecord draft, out BlueprintTemplateRecord template)
        {
            template = null;
            BlueprintTemplateLibrarySnapshot snapshot;
            var load = TryLoad(out snapshot);
            if (!load.Succeeded)
            {
                return load;
            }

            var templates = CloneTemplates(snapshot.Templates);
            var normalized = NormalizeTemplateForCreate(draft, templates, DateTime.UtcNow);
            templates.Add(normalized);
            var save = SaveTemplates(templates);
            if (!save.Succeeded)
            {
                return save;
            }

            template = normalized.Clone();
            return save;
        }

        public BlueprintStorageOperationResult DeleteTemplate(string templateId)
        {
            BlueprintTemplateLibrarySnapshot snapshot;
            var load = TryLoad(out snapshot);
            if (!load.Succeeded)
            {
                return load;
            }

            var normalizedId = NormalizeId(templateId);
            if (string.IsNullOrEmpty(normalizedId))
            {
                return BlueprintStorageOperationResult.Failure("invalidTemplateId", "template id unavailable", LibraryPath);
            }

            var templates = CloneTemplates(snapshot.Templates);
            var removed = false;
            for (var index = templates.Count - 1; index >= 0; index--)
            {
                if (string.Equals(templates[index].TemplateId, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    templates.RemoveAt(index);
                    removed = true;
                }
            }

            if (!removed)
            {
                return BlueprintStorageOperationResult.Failure("missingTemplate", "template not found", LibraryPath);
            }

            return SaveTemplates(templates);
        }

        public BlueprintStorageOperationResult RenameTemplate(string templateId, string requestedName, out BlueprintTemplateRecord renamed)
        {
            renamed = null;
            BlueprintTemplateLibrarySnapshot snapshot;
            var load = TryLoad(out snapshot);
            if (!load.Succeeded)
            {
                return load;
            }

            var normalizedId = NormalizeId(templateId);
            if (string.IsNullOrEmpty(normalizedId))
            {
                return BlueprintStorageOperationResult.Failure("invalidTemplateId", "template id unavailable", LibraryPath);
            }

            var normalizedName = NormalizeRequiredName(requestedName);
            if (string.IsNullOrEmpty(normalizedName))
            {
                return BlueprintStorageOperationResult.Failure("invalidTemplateName", "template name unavailable", LibraryPath);
            }

            var templates = CloneTemplates(snapshot.Templates);
            var targetIndex = -1;
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < templates.Count; index++)
            {
                var template = templates[index];
                if (template == null)
                {
                    continue;
                }

                if (string.Equals(template.TemplateId, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    targetIndex = index;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(template.Name))
                {
                    existingNames.Add(template.Name);
                }
            }

            if (targetIndex < 0)
            {
                return BlueprintStorageOperationResult.Failure("missingTemplate", "template not found", LibraryPath);
            }

            var target = templates[targetIndex];
            var nextName = CreateUniqueTemplateName(normalizedName, existingNames);
            if (string.Equals(target.Name, nextName, StringComparison.Ordinal))
            {
                renamed = target.Clone();
                return BlueprintStorageOperationResult.Success("unchanged", "unchanged", LibraryPath);
            }

            target.Name = nextName;
            target.UpdatedUtc = BlueprintStorageConstants.FormatUtc(DateTime.UtcNow);
            var save = SaveTemplates(templates);
            if (save.Succeeded)
            {
                renamed = target.Clone();
            }

            return save;
        }

        public BlueprintStorageOperationResult ExportTemplate(string templateId, string exportPath, out string savedPath)
        {
            savedPath = string.Empty;
            BlueprintTemplateLibrarySnapshot snapshot;
            var load = TryLoad(out snapshot);
            if (!load.Succeeded)
            {
                return load;
            }

            var template = FindTemplate(snapshot.Templates, templateId);
            if (template == null)
            {
                return BlueprintStorageOperationResult.Failure("missingTemplate", "template not found", LibraryPath);
            }

            var path = string.IsNullOrWhiteSpace(exportPath)
                ? BlueprintStoragePaths.BuildDefaultExportPath(_rootDirectory, template)
                : Path.GetFullPath(exportPath);
            var file = new BlueprintTemplateExportFile
            {
                SchemaVersion = BlueprintStorageConstants.SchemaVersion,
                SchemaKind = BlueprintStorageConstants.ExportSchemaKind,
                ExportedUtc = BlueprintStorageConstants.FormatUtc(DateTime.UtcNow),
                Template = template.Clone()
            };

            var result = BlueprintJsonSafeFileStore.TryWrite(path, file);
            if (result.Succeeded)
            {
                savedPath = path;
            }

            return result;
        }

        public BlueprintStorageOperationResult ImportTemplate(string importPath, out BlueprintTemplateRecord imported)
        {
            imported = null;
            string path;
            var resolve = ResolveImportPath(importPath, out path);
            if (!resolve.Succeeded)
            {
                return resolve;
            }

            BlueprintTemplateExportFile file;
            var read = BlueprintJsonSafeFileStore.TryRead(path, out file);
            if (!read.Succeeded)
            {
                return string.Equals(read.ResultCode, "missing", StringComparison.Ordinal)
                    ? BlueprintStorageOperationResult.Failure("missingImportFile", "import file not found", path)
                    : read;
            }

            if (file == null ||
                file.Template == null ||
                file.SchemaVersion <= 0 ||
                file.SchemaVersion > BlueprintStorageConstants.SchemaVersion ||
                !string.Equals(file.SchemaKind, BlueprintStorageConstants.ExportSchemaKind, StringComparison.Ordinal))
            {
                return BlueprintStorageOperationResult.Failure("invalidImportSchema", "invalid blueprint template export schema", path);
            }

            var draft = file.Template.Clone();
            draft.TemplateId = string.Empty;
            draft.CreatedUtc = string.Empty;
            draft.UpdatedUtc = string.Empty;
            var create = CreateTemplate(draft, out imported);
            return create.Succeeded
                ? BlueprintStorageOperationResult.Success("imported", "imported", path)
                : create;
        }

        internal static void ResetTestingHooks()
        {
            BlueprintJsonSafeFileStore.ResetTestingHooks();
        }

        internal static void SetCommitFailurePredicateForTesting(Func<string, bool> predicate)
        {
            BlueprintJsonSafeFileStore.SetCommitFailurePredicateForTesting(predicate);
        }

        internal static BlueprintStorageOperationResult ReadExportForTesting(string path, out BlueprintTemplateExportFile export)
        {
            return BlueprintJsonSafeFileStore.TryRead(path, out export);
        }

        private BlueprintStorageOperationResult ResolveImportPath(string importPath, out string path)
        {
            path = string.Empty;
            if (!string.IsNullOrWhiteSpace(importPath))
            {
                try
                {
                    path = Path.GetFullPath(importPath);
                    return BlueprintStorageOperationResult.Success("resolvedImportPath", "resolved", path);
                }
                catch (Exception error)
                {
                    return BlueprintStorageOperationResult.Failure("invalidImportPath", error.GetType().Name + ": " + error.Message, importPath);
                }
            }

            var directory = BlueprintStoragePaths.BuildDefaultImportDirectory(_rootDirectory);
            try
            {
                if (!Directory.Exists(directory))
                {
                    return BlueprintStorageOperationResult.Failure("missingImportFile", "put one blueprint export json in the imports directory", directory);
                }

                var files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
                if (files == null || files.Length <= 0)
                {
                    return BlueprintStorageOperationResult.Failure("missingImportFile", "put one blueprint export json in the imports directory", directory);
                }

                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                if (files.Length > 1)
                {
                    return BlueprintStorageOperationResult.Failure("ambiguousImportFile", "imports directory must contain exactly one json file", directory);
                }

                path = files[0];
                return BlueprintStorageOperationResult.Success("resolvedImportPath", "resolved", path);
            }
            catch (Exception error)
            {
                return BlueprintStorageOperationResult.Failure("importDirectoryReadFailed", error.GetType().Name + ": " + error.Message, directory);
            }
        }

        private BlueprintStorageOperationResult SaveTemplates(IList<BlueprintTemplateRecord> templates)
        {
            var now = BlueprintStorageConstants.FormatUtc(DateTime.UtcNow);
            var file = new BlueprintTemplateLibraryFile
            {
                SchemaVersion = BlueprintStorageConstants.SchemaVersion,
                Templates = NormalizeTemplates(templates),
                UpdatedUtc = now
            };
            var result = BlueprintJsonSafeFileStore.TryWrite(LibraryPath, file);
            if (result.Succeeded)
            {
                _revision++;
            }

            return result;
        }

        private static BlueprintTemplateRecord NormalizeTemplateForCreate(
            BlueprintTemplateRecord draft,
            IList<BlueprintTemplateRecord> existing,
            DateTime now)
        {
            draft = draft == null ? new BlueprintTemplateRecord() : draft.Clone();
            var createdUtc = BlueprintStorageConstants.FormatUtc(now);
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; existing != null && index < existing.Count; index++)
            {
                if (existing[index] != null && !string.IsNullOrWhiteSpace(existing[index].Name))
                {
                    existingNames.Add(existing[index].Name);
                }
            }

            draft.TemplateId = string.IsNullOrWhiteSpace(draft.TemplateId)
                ? "template-" + Guid.NewGuid().ToString("N")
                : NormalizeId(draft.TemplateId);
            draft.Name = CreateUniqueTemplateName(NormalizeName(draft.Name), existingNames);
            draft.CreatedUtc = string.IsNullOrWhiteSpace(draft.CreatedUtc) ? createdUtc : draft.CreatedUtc;
            draft.UpdatedUtc = createdUtc;
            draft.Width = Math.Max(0, draft.Width);
            draft.Height = Math.Max(0, draft.Height);
            draft.AnchorX = Clamp(draft.AnchorX, 0, Math.Max(0, draft.Width - 1));
            draft.AnchorY = Clamp(draft.AnchorY, 0, Math.Max(0, draft.Height - 1));
            draft.FormatVersion = draft.FormatVersion <= 0 ? BlueprintStorageConstants.SchemaVersion : draft.FormatVersion;
            draft.Cells = NormalizeCells(draft.Cells);
            draft.Materials = NormalizeMaterials(draft.Materials);
            draft.MissingCapabilityFlags = NormalizeFlags(draft.MissingCapabilityFlags);
            BlueprintObjectGroupNormalizer.NormalizeTemplateInPlace(draft);
            draft.MissingCapabilityFlags = NormalizeFlags(draft.MissingCapabilityFlags);
            return draft;
        }

        private static List<BlueprintTemplateRecord> NormalizeTemplates(IList<BlueprintTemplateRecord> source)
        {
            var normalized = new List<BlueprintTemplateRecord>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return normalized;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var item = source[index];
                if (item == null)
                {
                    continue;
                }

                var clone = item.Clone();
                clone.TemplateId = NormalizeId(clone.TemplateId);
                if (string.IsNullOrEmpty(clone.TemplateId) || !seenIds.Add(clone.TemplateId))
                {
                    continue;
                }

                clone.Name = NormalizeName(clone.Name);
                clone.Width = Math.Max(0, clone.Width);
                clone.Height = Math.Max(0, clone.Height);
                clone.AnchorX = Clamp(clone.AnchorX, 0, Math.Max(0, clone.Width - 1));
                clone.AnchorY = Clamp(clone.AnchorY, 0, Math.Max(0, clone.Height - 1));
                clone.FormatVersion = clone.FormatVersion <= 0 ? BlueprintStorageConstants.SchemaVersion : clone.FormatVersion;
                clone.CreatedUtc = clone.CreatedUtc ?? string.Empty;
                clone.UpdatedUtc = clone.UpdatedUtc ?? string.Empty;
                clone.Cells = NormalizeCells(clone.Cells);
                clone.Materials = NormalizeMaterials(clone.Materials);
                clone.MissingCapabilityFlags = NormalizeFlags(clone.MissingCapabilityFlags);
                BlueprintObjectGroupNormalizer.NormalizeTemplateInPlace(clone);
                clone.MissingCapabilityFlags = NormalizeFlags(clone.MissingCapabilityFlags);
                normalized.Add(clone);
            }

            return normalized;
        }

        private static List<BlueprintCellRecord> NormalizeCells(IList<BlueprintCellRecord> source)
        {
            var normalized = new List<BlueprintCellRecord>();
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

                var clone = cell.Clone();
                clone.Layers = NormalizeLayers(clone.Layers);
                if (clone.Layers.Count > 0)
                {
                    normalized.Add(clone);
                }
            }

            return normalized;
        }

        private static List<BlueprintCellLayerRecord> NormalizeLayers(IList<BlueprintCellLayerRecord> source)
        {
            var normalized = new List<BlueprintCellLayerRecord>();
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

                var clone = layer.Clone();
                clone.LayerKind = string.IsNullOrWhiteSpace(clone.LayerKind) ? BlueprintLayerKinds.Tile : clone.LayerKind.Trim();
                clone.ContentId = Math.Max(0, clone.ContentId);
                clone.MaterialItemId = Math.Max(0, clone.MaterialItemId);
                clone.MaterialStack = Math.Max(0, clone.MaterialStack);
                clone.Note = clone.Note == null ? string.Empty : clone.Note.Trim();
                normalized.Add(clone);
            }

            return normalized;
        }

        private static List<BlueprintMaterialEntry> NormalizeMaterials(IList<BlueprintMaterialEntry> source)
        {
            var normalized = new List<BlueprintMaterialEntry>();
            if (source == null)
            {
                return normalized;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var material = source[index];
                if (material == null || material.ItemId <= 0 || material.RequiredStack <= 0)
                {
                    continue;
                }

                var clone = material.Clone();
                clone.DisplayNameSnapshot = clone.DisplayNameSnapshot == null ? string.Empty : clone.DisplayNameSnapshot.Trim();
                clone.LayerKind = clone.LayerKind == null ? string.Empty : clone.LayerKind.Trim();
                clone.Source = clone.Source == null ? string.Empty : clone.Source.Trim();
                normalized.Add(clone);
            }

            return normalized;
        }

        private static List<string> NormalizeFlags(IList<string> source)
        {
            var normalized = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return normalized;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var flag = string.IsNullOrWhiteSpace(source[index]) ? string.Empty : source[index].Trim();
                if (flag.Length > 0 && seen.Add(flag))
                {
                    normalized.Add(flag);
                }
            }

            return normalized;
        }

        private static BlueprintTemplateRecord FindTemplate(IReadOnlyList<BlueprintTemplateRecord> templates, string templateId)
        {
            var normalizedId = NormalizeId(templateId);
            for (var index = 0; templates != null && index < templates.Count; index++)
            {
                var template = templates[index];
                if (template != null && string.Equals(template.TemplateId, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    return template.Clone();
                }
            }

            return null;
        }

        private static List<BlueprintTemplateRecord> CloneTemplates(IReadOnlyList<BlueprintTemplateRecord> source)
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

        private static string CreateUniqueTemplateName(string requested, ISet<string> existing)
        {
            var baseName = string.IsNullOrWhiteSpace(requested) ? BlueprintStorageConstants.DefaultTemplateName : requested.Trim();
            if (existing == null || !existing.Contains(baseName))
            {
                return baseName;
            }

            for (var suffix = 2; suffix < 100000; suffix++)
            {
                var candidate = baseName + " " + suffix.ToString(CultureInfo.InvariantCulture);
                if (!existing.Contains(candidate))
                {
                    return candidate;
                }
            }

            return baseName + " " + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static string NormalizeName(string name)
        {
            var value = string.IsNullOrWhiteSpace(name) ? BlueprintStorageConstants.DefaultTemplateName : name.Trim();
            value = value.Replace("\r", " ").Replace("\n", " ");
            return value.Length > 80 ? value.Substring(0, 80) : value;
        }

        private static string NormalizeRequiredName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var value = name.Trim().Replace("\r", " ").Replace("\n", " ");
            return value.Length > 80 ? value.Substring(0, 80) : value;
        }

        internal static string NormalizeId(string id)
        {
            var value = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
            if (value.Length <= 0 || value.Length > 96)
            {
                return string.Empty;
            }

            for (var index = 0; index < value.Length; index++)
            {
                var c = value[index];
                var ok =
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '-' ||
                    c == '_';
                if (!ok)
                {
                    return string.Empty;
                }
            }

            return value;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}

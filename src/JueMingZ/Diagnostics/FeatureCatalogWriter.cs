using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Features;

namespace JueMingZ.Diagnostics
{
    public static class FeatureCatalogWriter
    {
        private static readonly object SyncRoot = new object();
        private static bool _written;

        public static string FeatureCatalogPath { get; private set; } = Path.Combine(
            DiagnosticSnapshotWriter.DiagnosticsDirectory,
            "feature-catalog.json");

        public static void WriteOnce(FeatureRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_written)
                {
                    return;
                }

                _written = true;
            }

            try
            {
                Directory.CreateDirectory(DiagnosticSnapshotWriter.DiagnosticsDirectory);
                var tempPath = FeatureCatalogPath + ".tmp-" + Guid.NewGuid().ToString("N");
                File.WriteAllText(tempPath, ToJson(registry.GetAll()), Encoding.UTF8);
                File.Copy(tempPath, FeatureCatalogPath, true);
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                }

                Logger.Info("FeatureCatalogWriter", "feature catalog written: " + FeatureCatalogPath);
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "feature-catalog-write-failed",
                    TimeSpan.FromSeconds(30),
                    "FeatureCatalogWriter",
                    "Feature catalog write failed: " + error.Message);
            }
        }

        private static string ToJson(IReadOnlyList<FeatureDefinition> definitions)
        {
            var publicDefinitions = FilterPublicDefinitions(definitions);
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.Append("  \"GeneratedAtUtc\": \"").Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).AppendLine("\",");
            builder.Append("  \"FeatureCount\": ").Append(publicDefinitions.Count.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.AppendLine("  \"Features\": [");

            for (var index = 0; index < publicDefinitions.Count; index++)
            {
                AppendFeature(builder, publicDefinitions[index], index < publicDefinitions.Count - 1);
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static List<FeatureDefinition> FilterPublicDefinitions(IReadOnlyList<FeatureDefinition> definitions)
        {
            var result = new List<FeatureDefinition>();
            if (definitions == null)
            {
                return result;
            }

            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition == null ||
                    definition.IsInternalPlatform ||
                    !definition.CodeDomain.IsPublicDomain())
                {
                    continue;
                }

                result.Add(definition);
            }

            return result;
        }

        private static void AppendFeature(StringBuilder builder, FeatureDefinition definition, bool comma)
        {
            builder.AppendLine("    {");
            Append(builder, "Id", definition.Id, true, 6);
            Append(builder, "DisplayName", definition.DisplayName, true, 6);
            Append(builder, "Description", definition.Description, true, 6);
            Append(builder, "CodeDomain", definition.CodeDomain.ToCanonicalName(), true, 6);
            Append(builder, "UserCategory", definition.UserCategory.ToString(), true, 6);
            Append(builder, "UserCategoryDisplayName", FeatureUserCategoryNames.GetDisplayName(definition.UserCategory), true, 6);
            AppendArray(builder, "RequiredActions", definition.RequiredActions, true, 6);
            AppendArray(builder, "RequiredGameState", definition.RequiredGameState, true, 6);
            Append(builder, "MultiplayerSupport", definition.MultiplayerSupport.ToString(), true, 6);
            Append(builder, "HasConfig", definition.HasConfig, true, 6);
            Append(builder, "ConfigUiKind", definition.ConfigUiKind.ToString(), true, 6);
            Append(builder, "HasHotkey", definition.HasHotkey, true, 6);
            Append(builder, "HotkeyListVisible", definition.HotkeyListVisible, true, 6);
            Append(builder, "HotkeyDisplayName", definition.HotkeyDisplayName, true, 6);
            Append(builder, "DefaultEnabled", definition.DefaultEnabled, true, 6);
            Append(builder, "VisibleInMainUi", definition.VisibleInMainUi, true, 6);
            Append(builder, "IsImplemented", definition.IsImplemented, true, 6);
            Append(builder, "LifecycleStatus", definition.LifecycleStatus.ToString(), true, 6);
            Append(builder, "ExclusiveGroup", definition.ExclusiveGroup, true, 6);
            Append(builder, "Priority", definition.Priority, true, 6);
            Append(builder, "Notes", string.IsNullOrWhiteSpace(definition.Notes) ? definition.DetailedNotes : definition.Notes, false, 6);
            builder.Append("    }");
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void AppendArray<T>(StringBuilder builder, string name, IEnumerable<T> values, bool comma, int indent)
        {
            builder.Append(new string(' ', indent)).Append("\"").Append(Escape(name)).Append("\": [");
            var first = true;
            if (values != null)
            {
                foreach (var value in values)
                {
                    if (!first)
                    {
                        builder.Append(", ");
                    }

                    builder.Append("\"").Append(Escape(value == null ? string.Empty : value.ToString())).Append("\"");
                    first = false;
                }
            }

            builder.Append("]");
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void Append(StringBuilder builder, string name, string value, bool comma, int indent)
        {
            builder.Append(new string(' ', indent)).Append("\"").Append(Escape(name)).Append("\": \"").Append(Escape(value ?? string.Empty)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void Append(StringBuilder builder, string name, bool value, bool comma, int indent)
        {
            builder.Append(new string(' ', indent)).Append("\"").Append(Escape(name)).Append("\": ").Append(value ? "true" : "false");
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void Append(StringBuilder builder, string name, int value, bool comma, int indent)
        {
            builder.Append(new string(' ', indent)).Append("\"").Append(Escape(name)).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}

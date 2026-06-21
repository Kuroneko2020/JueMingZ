using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JueMingZ.Config;

namespace JueMingZ.Automation.Blueprint
{
    public static class BlueprintStoragePaths
    {
        public static string GetDefaultRootDirectory()
        {
            return Path.Combine(ConfigService.ConfigDirectory, "blueprints");
        }

        public static string BuildTemplateLibraryPath(string rootDirectory)
        {
            return Path.Combine(NormalizeRoot(rootDirectory), BlueprintStorageConstants.TemplatesFileName);
        }

        public static string BuildWorldInstancesPath(string rootDirectory, string worldPairKey)
        {
            var fileName = BuildStableFileName(worldPairKey);
            return Path.Combine(
                NormalizeRoot(rootDirectory),
                BlueprintStorageConstants.WorldInstancesDirectoryName,
                fileName);
        }

        public static string BuildDefaultExportPath(string rootDirectory, BlueprintTemplateRecord template)
        {
            var name = NormalizeFileName(template == null ? string.Empty : template.Name);
            var id = NormalizeFileName(template == null ? string.Empty : template.TemplateId);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = BlueprintStorageConstants.DefaultTemplateName;
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                id = Guid.NewGuid().ToString("N");
            }

            return Path.Combine(NormalizeRoot(rootDirectory), BlueprintStorageConstants.ExportDirectoryName, name + "-" + id + ".json");
        }

        public static string BuildDefaultImportDirectory(string rootDirectory)
        {
            return Path.Combine(NormalizeRoot(rootDirectory), BlueprintStorageConstants.ImportDirectoryName);
        }

        private static string NormalizeRoot(string rootDirectory)
        {
            return Path.GetFullPath(string.IsNullOrWhiteSpace(rootDirectory) ? GetDefaultRootDirectory() : rootDirectory);
        }

        private static string BuildStableFileName(string worldPairKey)
        {
            var value = string.IsNullOrWhiteSpace(worldPairKey) ? "unknown" : worldPairKey.Trim();
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(bytes.Length * 2);
                for (var index = 0; index < bytes.Length; index++)
                {
                    builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString() + ".json";
            }
        }

        private static string NormalizeFileName(string value)
        {
            var source = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            var builder = new StringBuilder(source.Length);
            for (var index = 0; index < source.Length; index++)
            {
                var c = source[index];
                var invalid = c < 32 || Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0;
                builder.Append(invalid ? '_' : c);
            }

            var result = builder.ToString().Trim();
            return result.Length > 60 ? result.Substring(0, 60) : result;
        }
    }
}

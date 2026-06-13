using System;
using System.IO;
using System.Text;
using JueMingZ.Diagnostics;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void DiagnosticArtifactReplacementCreatesMissingTarget()
        {
            var directory = BuildDiagnosticArtifactTestDirectory();
            try
            {
                Directory.CreateDirectory(directory);
                var targetPath = Path.Combine(directory, "runtime-snapshot.json");
                var tempPath = targetPath + ".tmp-test";
                File.WriteAllText(tempPath, "new snapshot", Encoding.UTF8);

                DiagnosticArtifactFileReplacer.ReplaceCompletedTempFile(tempPath, targetPath);

                AssertStringEquals(File.ReadAllText(targetPath, Encoding.UTF8), "new snapshot", "missing target replacement content");
                if (File.Exists(tempPath))
                {
                    throw new InvalidOperationException("Temp file should be moved when target is missing.");
                }
            }
            finally
            {
                TryDeleteDiagnosticArtifactTestDirectory(directory);
            }
        }

        private static void DiagnosticArtifactReplacementReplacesExistingTarget()
        {
            var directory = BuildDiagnosticArtifactTestDirectory();
            try
            {
                Directory.CreateDirectory(directory);
                var targetPath = Path.Combine(directory, "feature-catalog.json");
                var tempPath = targetPath + ".tmp-test";
                File.WriteAllText(targetPath, "old catalog", Encoding.UTF8);
                File.WriteAllText(tempPath, "new catalog", Encoding.UTF8);

                DiagnosticArtifactFileReplacer.ReplaceCompletedTempFile(tempPath, targetPath);

                AssertStringEquals(File.ReadAllText(targetPath, Encoding.UTF8), "new catalog", "existing target replacement content");
                if (File.Exists(tempPath))
                {
                    throw new InvalidOperationException("Temp file should be consumed after successful replacement.");
                }
            }
            finally
            {
                TryDeleteDiagnosticArtifactTestDirectory(directory);
            }
        }

        private static void DiagnosticArtifactReplacementFailureKeepsExistingAndDeletesTemp()
        {
            var directory = BuildDiagnosticArtifactTestDirectory();
            try
            {
                Directory.CreateDirectory(directory);
                var targetPath = Path.Combine(directory, "runtime-snapshot.json");
                var tempPath = targetPath + ".tmp-test";
                File.WriteAllText(targetPath, "old snapshot", Encoding.UTF8);
                File.WriteAllText(tempPath, "new snapshot", Encoding.UTF8);

                var failed = false;
                using (new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    try
                    {
                        DiagnosticArtifactFileReplacer.ReplaceCompletedTempFile(tempPath, targetPath);
                    }
                    catch (Exception error)
                    {
                        if (error is IOException ||
                            error is UnauthorizedAccessException ||
                            error is PlatformNotSupportedException)
                        {
                            failed = true;
                        }
                        else
                        {
                            throw;
                        }
                    }
                }

                if (!failed)
                {
                    throw new InvalidOperationException("Expected locked target replacement to fail.");
                }

                AssertStringEquals(File.ReadAllText(targetPath, Encoding.UTF8), "old snapshot", "existing target content after failed replacement");
                if (File.Exists(tempPath))
                {
                    throw new InvalidOperationException("Temp file should be deleted after failed replacement.");
                }
            }
            finally
            {
                TryDeleteDiagnosticArtifactTestDirectory(directory);
            }
        }

        private static string BuildDiagnosticArtifactTestDirectory()
        {
            return Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "JueMingZ.Tests",
                "diagnostic-artifact-" + Guid.NewGuid().ToString("N")));
        }

        private static void TryDeleteDiagnosticArtifactTestDirectory(string directory)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
            }
        }
    }
}

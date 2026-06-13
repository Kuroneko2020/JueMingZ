using System;
using System.IO;

namespace JueMingZ.Diagnostics
{
    internal static class DiagnosticArtifactFileReplacer
    {
        internal static void ReplaceCompletedTempFile(string tempPath, string targetPath)
        {
            try
            {
                ReplaceOrMoveTempFile(tempPath, targetPath);
            }
            catch
            {
                TryDeleteTemp(tempPath);
                throw;
            }
        }

        private static void ReplaceOrMoveTempFile(string tempPath, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(tempPath))
            {
                throw new ArgumentException("Temp path is required.", "tempPath");
            }

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw new ArgumentException("Target path is required.", "targetPath");
            }

            if (!File.Exists(targetPath))
            {
                File.Move(tempPath, targetPath);
                return;
            }

            try
            {
                // Diagnostic artifacts are external troubleshooting files. If
                // replace fails, keep the previous artifact instead of falling
                // back to overwrite-copy semantics.
                File.Replace(tempPath, targetPath, null);
            }
            catch (FileNotFoundException)
            {
                if (!File.Exists(targetPath))
                {
                    File.Move(tempPath, targetPath);
                    return;
                }

                throw;
            }
        }

        private static void TryDeleteTemp(string tempPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
        }
    }
}

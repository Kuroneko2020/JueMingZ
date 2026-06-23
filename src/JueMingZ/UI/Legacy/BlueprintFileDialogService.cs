using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace JueMingZ.UI.Legacy
{
    internal sealed class BlueprintFileDialogResult
    {
        private BlueprintFileDialogResult(bool succeeded, bool cancelled, string resultCode, string message, string path)
        {
            Succeeded = succeeded;
            Cancelled = cancelled;
            ResultCode = resultCode ?? string.Empty;
            Message = message ?? string.Empty;
            Path = path ?? string.Empty;
        }

        public bool Succeeded { get; private set; }
        public bool Cancelled { get; private set; }
        public string ResultCode { get; private set; }
        public string Message { get; private set; }
        public string Path { get; private set; }

        public static BlueprintFileDialogResult Selected(string path)
        {
            return new BlueprintFileDialogResult(true, false, "selected", "selected", path);
        }

        public static BlueprintFileDialogResult CancelledResult(string resultCode, string message)
        {
            return new BlueprintFileDialogResult(true, true, resultCode, message, string.Empty);
        }

        public static BlueprintFileDialogResult Failed(string resultCode, string message)
        {
            return new BlueprintFileDialogResult(false, false, resultCode, message, string.Empty);
        }
    }

    internal interface IBlueprintFileDialogService
    {
        BlueprintFileDialogResult ChooseImportJsonPath(string initialDirectory);

        BlueprintFileDialogResult ChooseExportJsonPath(string initialDirectory, string defaultFileName);
    }

    internal sealed class BlueprintWindowsFileDialogService : IBlueprintFileDialogService
    {
        private const string JsonFilter = "JSON files (*.json)|*.json|All files (*.*)|*.*";

        public BlueprintFileDialogResult ChooseImportJsonPath(string initialDirectory)
        {
            try
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = "导入蓝图";
                    dialog.Filter = JsonFilter;
                    dialog.DefaultExt = "json";
                    dialog.CheckFileExists = true;
                    dialog.CheckPathExists = true;
                    dialog.Multiselect = false;
                    ApplyInitialDirectory(dialog, initialDirectory);

                    return dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName)
                        ? BlueprintFileDialogResult.Selected(dialog.FileName)
                        : BlueprintFileDialogResult.CancelledResult("dialogCancelled", "导入已取消。");
                }
            }
            catch (ThreadStateException error)
            {
                return BlueprintFileDialogResult.Failed("dialogUnavailable", error.GetType().Name + ": " + error.Message);
            }
            catch (Exception error)
            {
                return BlueprintFileDialogResult.Failed("dialogFailed", error.GetType().Name + ": " + error.Message);
            }
        }

        public BlueprintFileDialogResult ChooseExportJsonPath(string initialDirectory, string defaultFileName)
        {
            try
            {
                using (var dialog = new SaveFileDialog())
                {
                    dialog.Title = "导出蓝图";
                    dialog.Filter = JsonFilter;
                    dialog.DefaultExt = "json";
                    dialog.AddExtension = true;
                    dialog.OverwritePrompt = true;
                    dialog.CheckPathExists = true;
                    dialog.FileName = string.IsNullOrWhiteSpace(defaultFileName) ? "JM-blueprint.json" : defaultFileName;
                    ApplyInitialDirectory(dialog, initialDirectory);

                    return dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName)
                        ? BlueprintFileDialogResult.Selected(dialog.FileName)
                        : BlueprintFileDialogResult.CancelledResult("dialogCancelled", "导出已取消。");
                }
            }
            catch (ThreadStateException error)
            {
                return BlueprintFileDialogResult.Failed("dialogUnavailable", error.GetType().Name + ": " + error.Message);
            }
            catch (Exception error)
            {
                return BlueprintFileDialogResult.Failed("dialogFailed", error.GetType().Name + ": " + error.Message);
            }
        }

        private static void ApplyInitialDirectory(FileDialog dialog, string initialDirectory)
        {
            if (dialog == null || string.IsNullOrWhiteSpace(initialDirectory))
            {
                return;
            }

            var fullPath = Path.GetFullPath(initialDirectory);
            if (Directory.Exists(fullPath))
            {
                dialog.InitialDirectory = fullPath;
            }
        }
    }
}

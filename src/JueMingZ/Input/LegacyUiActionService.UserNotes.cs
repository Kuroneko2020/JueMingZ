using System;
using JueMingZ.Automation.Information.Notes;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    public static partial class LegacyUiActionService
    {
        private static void HandleUserNotesCommand(LegacyUiCommand command)
        {
            if (command == null)
            {
                return;
            }

            var before = UserNotesUiState.BuildUiStateJson();
            UserNotesOperationResult result;
            string scenario;
            string noteId = string.Empty;
            if (string.Equals(command.ElementId, UserNotesUiState.EditOutsideElementId, StringComparison.Ordinal))
            {
                result = UserNotesUiState.SaveActiveEditor("outsideClick");
                scenario = "Ui.Notes.Save";
            }
            else if (command.ElementId.StartsWith(UserNotesUiState.TitleElementPrefix, StringComparison.Ordinal))
            {
                noteId = command.ElementId.Substring(UserNotesUiState.TitleElementPrefix.Length);
                result = UserNotesUiState.HandleTitleEditCommand(noteId, command.IsDoubleClick, command.MouseX, command.MouseY, command.Rect);
                scenario = command.IsDoubleClick ? "Ui.Notes.EditTitle" : "Ui.Notes.CursorTitle";
            }
            else if (command.ElementId.StartsWith(UserNotesUiState.BodyElementPrefix, StringComparison.Ordinal))
            {
                noteId = command.ElementId.Substring(UserNotesUiState.BodyElementPrefix.Length);
                result = UserNotesUiState.HandleBodyEditCommand(noteId, command.IsDoubleClick, command.MouseX, command.MouseY, command.Rect);
                scenario = command.IsDoubleClick ? "Ui.Notes.EditBody" : "Ui.Notes.CursorBody";
            }
            else if (string.Equals(command.ElementId, UserNotesUiState.AddButtonId, StringComparison.Ordinal))
            {
                var save = UserNotesUiState.SaveActiveEditor("add");
                if (!save.Succeeded)
                {
                    result = save;
                    scenario = "Ui.Notes.Save";
                    RecordUserNotesResult(command, scenario, before, result, noteId);
                    return;
                }

                UserNoteSnapshot note;
                result = UserNotesUiState.CreateDefaultNote(out note);
                noteId = note == null ? string.Empty : note.NoteId;
                scenario = "Ui.Notes.Add";
            }
            else if (command.ElementId.StartsWith(UserNotesUiState.PinButtonPrefix, StringComparison.Ordinal))
            {
                noteId = command.ElementId.Substring(UserNotesUiState.PinButtonPrefix.Length);
                var save = UserNotesUiState.SaveActiveEditor("pin");
                if (!save.Succeeded)
                {
                    result = save;
                    scenario = "Ui.Notes.Save";
                    RecordUserNotesResult(command, scenario, before, result, noteId);
                    return;
                }

                UserNoteSnapshot note;
                result = UserNotesUiState.PinNote(noteId, out note);
                scenario = "Ui.Notes.Pin";
            }
            else if (command.ElementId.StartsWith(UserNotesUiState.DeleteButtonPrefix, StringComparison.Ordinal))
            {
                noteId = command.ElementId.Substring(UserNotesUiState.DeleteButtonPrefix.Length);
                var save = UserNotesUiState.SaveActiveEditor("delete");
                if (!save.Succeeded)
                {
                    result = save;
                    scenario = "Ui.Notes.Save";
                    RecordUserNotesResult(command, scenario, before, result, noteId);
                    return;
                }

                var wasConfirming = UserNotesUiState.IsDeleteConfirming(noteId);
                result = UserNotesUiState.RequestDeleteOrConfirm(noteId);
                scenario = wasConfirming ? "Ui.Notes.Delete" : "Ui.Notes.DeleteConfirm";
            }
            else
            {
                result = UserNotesOperationResult.Failure("unknownCommand", "unknown user notes command");
                scenario = "Ui.Notes.Command";
            }

            RecordUserNotesResult(command, scenario, before, result, noteId);
        }

        private static void RecordUserNotesResult(LegacyUiCommand command, string scenario, string before, UserNotesOperationResult result, string noteId)
        {
            Record(
                command,
                scenario,
                "UserNotes",
                result != null && result.Succeeded ? "Succeeded" : "Failed",
                result == null ? "User notes command failed." : result.Message,
                before,
                UserNotesUiState.BuildUiStateJson(),
                BuildUserNotesVerificationJson(result, noteId, command),
                "Button");
        }

        internal static void HandleUserNotesCommandForTesting(LegacyUiCommand command)
        {
            HandleUserNotesCommand(command);
        }

        private static string BuildUserNotesVerificationJson(UserNotesOperationResult result, string noteId, LegacyUiCommand command)
        {
            return "{" +
                   "\"featureId\":\"information.user_notes\"," +
                   "\"noteId\":\"" + EscapeJson(noteId) + "\"," +
                   "\"resultCode\":\"" + EscapeJson(result == null ? string.Empty : result.ResultCode) + "\"," +
                   "\"mouseCaptured\":" + BoolRaw(command != null && command.MouseCaptured) +
                   "}";
        }
    }
}

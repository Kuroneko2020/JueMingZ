using System;
using System.IO;
using JueMingZ.Automation.Information.Notes;
using JueMingZ.Compat;
using JueMingZ.Input;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void UserNotesTabKeepsHotkeysPageIdAndUsesNoteIcon()
        {
            if (!string.Equals(LegacyTabBar.GetDisplayName("hotkeys"), "笔记", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected hotkeys page id to display as 笔记.");
            }

            if (!string.Equals(LegacyTabBar.GetIconId("hotkeys"), "note", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected hotkeys page id to use the note vector icon.");
            }

            var hasHotkeys = false;
            for (var index = 0; index < LegacyTabBar.Tabs.Length; index++)
            {
                if (string.Equals(LegacyTabBar.Tabs[index].Id, "hotkeys", StringComparison.Ordinal))
                {
                    hasHotkeys = true;
                    break;
                }
            }

            if (!hasHotkeys)
            {
                throw new InvalidOperationException("Expected the stable hotkeys page id to remain present.");
            }
        }

        private static void UserNotesLayoutUsesTwoColumnsAndCapsCardHeight()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-ui-layout");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var cache = new UserNotesCache(new UserNotesStore());
                UserNoteSnapshot first;
                UserNoteSnapshot second;
                UserNoteSnapshot third;
                RequireSuccess(cache.CreateDefaultNote(out first), "create first note");
                RequireSuccess(cache.CreateDefaultNote(out second), "create second note");
                RequireSuccess(cache.CreateDefaultNote(out third), "create third note");
                RequireSuccess(cache.SaveNote(second.NoteId, "Long", BuildRepeatedText("长正文", 80), out second), "save long note");
                UserNotesUiState.SetCacheForTesting(cache, true);

                var layout = LegacyMainWindow.BuildNotesLayoutForTesting(560, 500);
                if (layout.ColumnCount != 2 || layout.Cards == null || layout.Cards.Count != 3)
                {
                    throw new InvalidOperationException("Expected notes layout to use two columns and include three cards.");
                }

                if (layout.Cards[0].X != 0 || layout.Cards[1].X <= layout.Cards[0].X)
                {
                    throw new InvalidOperationException("Expected second note card to be placed in the right column.");
                }

                for (var index = 0; index < layout.Cards.Count; index++)
                {
                    if (layout.Cards[index].Height > 250)
                    {
                        throw new InvalidOperationException("Notes card height must not exceed half of the F5 content viewport.");
                    }
                }

                var emptyLines = UserNotesUiState.BuildWrappedBodyLinesForTesting(string.Empty, 220);
                if (emptyLines.Length <= 0 || !string.Equals(emptyLines[0], UserNotesUiState.EmptyBodyPromptText, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected empty note body preview to show the edit prompt.");
                }
            }
            finally
            {
                UserNotesUiState.ResetForTesting();
                ResetUserNotesTestingHooks();
                restore();
            }
        }

        private static void UserNotesNestedScrollConsumesOnlyScrollableBody()
        {
            UserNotesUiState.ResetForTesting();
            UserNotesUiState.BeginBodyViewportFrame();
            var mouse = new LegacyMouseSnapshot
            {
                X = 20,
                Y = 20,
                ScrollDelta = -120
            };

            UserNotesUiState.SetBodyViewportForTesting("note-a", new LegacyUiRect(10, 10, 120, 50), 40);
            if (UserNotesUiState.TryConsumeNestedScroll(mouse, -120))
            {
                throw new InvalidOperationException("Expected non-scrollable note body to bubble to the F5 main scroll.");
            }

            UserNotesUiState.SetBodyViewportForTesting("note-a", new LegacyUiRect(10, 10, 120, 50), 200);
            if (!UserNotesUiState.TryConsumeNestedScroll(mouse, -120))
            {
                throw new InvalidOperationException("Expected scrollable note body to consume downward wheel.");
            }

            if (UserNotesUiState.GetBodyScrollOffset("note-a") <= 0)
            {
                throw new InvalidOperationException("Expected note body scroll offset to increase.");
            }

            if (!UserNotesUiState.TryConsumeNestedScroll(mouse, 120))
            {
                throw new InvalidOperationException("Expected scrollable note body to consume upward wheel before reaching the top.");
            }

            if (UserNotesUiState.GetBodyScrollOffset("note-a") != 0)
            {
                throw new InvalidOperationException("Expected note body scroll offset to return to the top.");
            }

            if (UserNotesUiState.TryConsumeNestedScroll(mouse, 120))
            {
                throw new InvalidOperationException("Expected wheel at the note body top edge to bubble to the main scroll.");
            }

            UserNotesUiState.ResetForTesting();
        }

        private static void UserNotesCommandsCreatePinAndTwoStepDelete()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-ui-commands");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var store = new UserNotesStore();
                var cache = new UserNotesCache(store);
                RequireSuccess(cache.Refresh(), "refresh notes cache");
                UserNotesUiState.SetCacheForTesting(cache, true);

                LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
                {
                    ElementId = UserNotesUiState.AddButtonId,
                    Label = "笔记:新增",
                    Kind = "button",
                    MouseCaptured = true
                });

                var snapshot = cache.Snapshot;
                if (snapshot.Notes.Count != 1)
                {
                    throw new InvalidOperationException("Expected + command to create one default note.");
                }

                var noteId = snapshot.Notes[0].NoteId;
                LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
                {
                    ElementId = UserNotesUiState.PinButtonPrefix + noteId,
                    Label = "笔记:悬挂",
                    Kind = "button",
                    MouseCaptured = true
                });

                snapshot = cache.Snapshot;
                if (snapshot.Notes[0].PinnedState == null || !snapshot.Notes[0].PinnedState.Pinned)
                {
                    throw new InvalidOperationException("Expected pin command to persist the pinned state.");
                }

                LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
                {
                    ElementId = UserNotesUiState.DeleteButtonPrefix + noteId,
                    Label = "笔记:删除",
                    Kind = "button",
                    MouseCaptured = true
                });

                if (cache.Snapshot.Notes.Count != 1 || !UserNotesUiState.IsDeleteConfirming(noteId))
                {
                    throw new InvalidOperationException("Expected first delete click to only arm confirmation.");
                }

                var bodyPath = store.GetBodyPath(noteId);
                UserNotesStore.SetDeleteFailurePredicateForTesting(path => string.Equals(path, bodyPath, StringComparison.OrdinalIgnoreCase));
                LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
                {
                    ElementId = UserNotesUiState.DeleteButtonPrefix + noteId,
                    Label = "笔记:确认",
                    Kind = "button",
                    MouseCaptured = true
                });

                if (cache.Snapshot.Notes.Count != 1 || !File.Exists(bodyPath) || !UserNotesUiState.IsDeleteConfirming(noteId))
                {
                    throw new InvalidOperationException("Expected failed confirmed delete to keep the note visible and confirmation armed.");
                }

                UserNotesStore.SetDeleteFailurePredicateForTesting(null);
                LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
                {
                    ElementId = UserNotesUiState.DeleteButtonPrefix + noteId,
                    Label = "笔记:确认",
                    Kind = "button",
                    MouseCaptured = true
                });

                if (cache.Snapshot.Notes.Count != 0 || UserNotesUiState.IsDeleteConfirming(noteId))
                {
                    throw new InvalidOperationException("Expected successful confirmed delete to remove the note and clear confirmation.");
                }
            }
            finally
            {
                UserNotesUiState.ResetForTesting();
                ResetUserNotesTestingHooks();
                restore();
            }
        }

        private static void UserNotesTitleEditorSavesAndCancels()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-title-editor");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var cache = new UserNotesCache(new UserNotesStore());
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create note");
                RequireSuccess(cache.SaveNote(note.NoteId, "原标题", "正文", out note), "seed note");
                UserNotesUiState.SetCacheForTesting(cache, true);

                var titleRect = new LegacyUiRect(10, 10, 240, 24);
                LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
                {
                    ElementId = UserNotesUiState.TitleElementPrefix + note.NoteId,
                    Label = "笔记:标题",
                    Kind = "button",
                    Rect = titleRect,
                    MouseX = titleRect.Right - 1,
                    MouseY = titleRect.Y + 8,
                    IsDoubleClick = true,
                    MouseCaptured = true
                });

                if (!UserNotesUiState.HasActiveEditor || !UserNotesUiState.IsEditingTitle(note.NoteId))
                {
                    throw new InvalidOperationException("Expected double-clicking the note title to open the title editor.");
                }

                var inputId = UserNotesUiState.BuildEditorInputIdForTesting(note.NoteId, "title");
                LegacyMultilineTextInput.SetCursorIndex(inputId, int.MaxValue);
                LegacyMultilineTextInput.InsertTextForTesting(inputId, " edited", false, 80);
                RequireSuccess(UserNotesUiState.SaveActiveEditor("test-title-save"), "save edited title");

                var saved = FindUserNote(cache.Snapshot, note.NoteId);
                if (saved == null ||
                    !string.Equals(saved.Title, "原标题 edited", StringComparison.Ordinal) ||
                    !string.Equals(saved.Body, "正文", StringComparison.Ordinal) ||
                    UserNotesUiState.HasActiveEditor)
                {
                    throw new InvalidOperationException("Expected title save to persist only the title and close the editor.");
                }

                LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
                {
                    ElementId = UserNotesUiState.TitleElementPrefix + note.NoteId,
                    Label = "笔记:标题",
                    Kind = "button",
                    Rect = titleRect,
                    MouseX = titleRect.Right - 1,
                    MouseY = titleRect.Y + 8,
                    IsDoubleClick = true,
                    MouseCaptured = true
                });
                LegacyMultilineTextInput.SetCursorIndex(inputId, int.MaxValue);
                LegacyMultilineTextInput.InsertTextForTesting(inputId, " discarded", false, 80);
                RequireSuccess(UserNotesUiState.CancelActiveEditor(), "cancel title edit");

                saved = FindUserNote(cache.Snapshot, note.NoteId);
                if (saved == null || !string.Equals(saved.Title, "原标题 edited", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected cancelling title edit to keep the persisted title.");
                }
            }
            finally
            {
                UserNotesUiState.ResetForTesting();
                ResetUserNotesTestingHooks();
                restore();
            }
        }

        private static void UserNotesBodyEditorSavesNewlinesAndKeepsDraftOnFailure()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-body-editor");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var store = new UserNotesStore();
                var cache = new UserNotesCache(store);
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create note");
                RequireSuccess(cache.SaveNote(note.NoteId, "标题", "abc", out note), "seed body");
                UserNotesUiState.SetCacheForTesting(cache, true);

                var bodyRect = new LegacyUiRect(10, 40, 260, 120);
                LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
                {
                    ElementId = UserNotesUiState.BodyElementPrefix + note.NoteId,
                    Label = "笔记:正文",
                    Kind = "button",
                    Rect = bodyRect,
                    MouseX = bodyRect.X + 12,
                    MouseY = bodyRect.Y + 12,
                    IsDoubleClick = true,
                    MouseCaptured = true
                });

                var inputId = UserNotesUiState.BuildEditorInputIdForTesting(note.NoteId, "body");
                LegacyMultilineTextInput.SetCursorIndex(inputId, int.MaxValue);
                LegacyMultilineTextInput.InsertTextForTesting(inputId, "\n中文", true, 0);
                RequireSuccess(UserNotesUiState.SaveActiveEditor("test-body-save"), "save edited body");

                var saved = FindUserNote(cache.Snapshot, note.NoteId);
                if (saved == null || !string.Equals(saved.Body, "abc\n中文", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected body editor to persist UTF-8 multiline content.");
                }

                LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
                {
                    ElementId = UserNotesUiState.BodyElementPrefix + note.NoteId,
                    Label = "笔记:正文",
                    Kind = "button",
                    Rect = bodyRect,
                    MouseX = bodyRect.X + 12,
                    MouseY = bodyRect.Y + 12,
                    IsDoubleClick = true,
                    MouseCaptured = true
                });
                LegacyMultilineTextInput.SetCursorIndex(inputId, int.MaxValue);
                LegacyMultilineTextInput.InsertTextForTesting(inputId, "\n失败草稿", true, 0);

                var bodyPath = store.GetBodyPath(note.NoteId);
                UserNotesStore.SetCommitFailurePredicateForTesting(path => string.Equals(path, bodyPath, StringComparison.OrdinalIgnoreCase));
                var failedSave = UserNotesUiState.SaveActiveEditor("test-body-save-failure");
                if (failedSave.Succeeded || !UserNotesUiState.HasActiveEditor)
                {
                    throw new InvalidOperationException("Expected failed body save to keep the editor active.");
                }

                saved = FindUserNote(cache.Snapshot, note.NoteId);
                var draft = LegacyMultilineTextInput.GetSnapshot(inputId).Draft;
                if (saved == null ||
                    !string.Equals(saved.Body, "abc\n中文", StringComparison.Ordinal) ||
                    draft == null ||
                    draft.IndexOf("失败草稿", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Expected failed body save to keep persisted body and unsaved draft separately.");
                }
            }
            finally
            {
                UserNotesUiState.ResetForTesting();
                ResetUserNotesTestingHooks();
                restore();
            }
        }

        private static void UserNotesBodyEditorDraftInvalidatesLayout()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-body-layout");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var cache = new UserNotesCache(new UserNotesStore());
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create note");
                RequireSuccess(cache.SaveNote(note.NoteId, "标题", "短正文", out note), "seed body");
                UserNotesUiState.SetCacheForTesting(cache, true);

                var before = LegacyMainWindow.BuildNotesLayoutForTesting(560, 500);
                var beforeCard = before.Cards[0];
                var bodyRect = new LegacyUiRect(10, 40, 260, 120);
                LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
                {
                    ElementId = UserNotesUiState.BodyElementPrefix + note.NoteId,
                    Label = "笔记:正文",
                    Kind = "button",
                    Rect = bodyRect,
                    MouseX = bodyRect.X + 12,
                    MouseY = bodyRect.Y + 12,
                    IsDoubleClick = true,
                    MouseCaptured = true
                });

                var inputId = UserNotesUiState.BuildEditorInputIdForTesting(note.NoteId, "body");
                LegacyMultilineTextInput.SetCursorIndex(inputId, int.MaxValue);
                LegacyMultilineTextInput.InsertTextForTesting(inputId, BuildRepeatedText("长正文", 80), true, 0);
                var after = LegacyMainWindow.BuildNotesLayoutForTesting(560, 500);
                var afterCard = after.Cards[0];
                if (afterCard.BodyContentHeight <= beforeCard.BodyContentHeight)
                {
                    throw new InvalidOperationException("Expected active body draft to invalidate layout and expand body content height.");
                }
            }
            finally
            {
                UserNotesUiState.ResetForTesting();
                ResetUserNotesTestingHooks();
                restore();
            }
        }

        private static void UserNotesMultilineTextInputHandlesCursorSubmitAndCancel()
        {
            var inputId = "notes:editor:test";
            try
            {
                LegacyMultilineTextInput.Focus(inputId, "ab", 1);
                LegacyMultilineTextInput.InsertTextForTesting(inputId, "中", false, 10);
                var snapshot = LegacyMultilineTextInput.GetSnapshot(inputId);
                if (!string.Equals(snapshot.Draft, "a中b", StringComparison.Ordinal) || snapshot.CursorIndex != 2)
                {
                    throw new InvalidOperationException("Expected editor text insertion to respect the cursor position.");
                }

                var enter = LegacyMultilineTextInput.ApplyControlForTesting(
                    inputId,
                    true,
                    0,
                    new TextInputControlState { EnterPressed = true });
                snapshot = LegacyMultilineTextInput.GetSnapshot(inputId);
                if (!enter.Changed || !string.Equals(snapshot.Draft, "a中\nb", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected body Enter to insert a newline at the cursor.");
                }

                var escape = LegacyMultilineTextInput.ApplyControlForTesting(
                    inputId,
                    true,
                    0,
                    new TextInputControlState { EscapePressed = true });
                if (!escape.CancelRequested)
                {
                    throw new InvalidOperationException("Expected Escape to request editor cancellation.");
                }

                LegacyMultilineTextInput.Focus(inputId, "title", 5);
                var submit = LegacyMultilineTextInput.ApplyControlForTesting(
                    inputId,
                    false,
                    80,
                    new TextInputControlState { EnterPressed = true });
                snapshot = LegacyMultilineTextInput.GetSnapshot(inputId);
                if (!submit.SubmitRequested || !string.Equals(snapshot.Draft, "title", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected title Enter to submit without inserting a newline.");
                }
            }
            finally
            {
                LegacyMultilineTextInput.ClearFocus();
            }
        }

        private static UserNoteSnapshot FindUserNote(UserNotesSnapshot snapshot, string noteId)
        {
            if (snapshot == null || snapshot.Notes == null)
            {
                return null;
            }

            for (var index = 0; index < snapshot.Notes.Count; index++)
            {
                var note = snapshot.Notes[index];
                if (note != null && string.Equals(note.NoteId, noteId, StringComparison.OrdinalIgnoreCase))
                {
                    return note;
                }
            }

            return null;
        }

        private static string BuildRepeatedText(string text, int count)
        {
            var result = string.Empty;
            for (var index = 0; index < count; index++)
            {
                result += text;
            }

            return result;
        }
    }
}

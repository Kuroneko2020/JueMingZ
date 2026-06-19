using System;
using System.Collections.Generic;
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

        private static void UserNotesCardBodyViewportMatchesLayoutAndScroll()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-body-viewport");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var cache = new UserNotesCache(new UserNotesStore());
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create note");
                var longBody = BuildRepeatedText("长正文", 100);
                RequireSuccess(cache.SaveNote(note.NoteId, "标题", longBody, out note), "seed long body");
                UserNotesUiState.SetCacheForTesting(cache, true);

                var layout = LegacyMainWindow.BuildNotesLayoutForTesting(560, 500);
                var card = layout.Cards[0];
                var bodyPanel = new LegacyUiRect(24, 48 + card.BodyY, Math.Max(1, card.Width - 16), card.BodyHeight);
                var textViewport = UserNotesUiState.ResolveBodyTextViewport(bodyPanel);
                var inset = UserNotesUiState.BodyTextInsetForTesting;
                if (textViewport.X != bodyPanel.X + inset ||
                    textViewport.Y != bodyPanel.Y + inset ||
                    textViewport.Width != bodyPanel.Width - inset * 2 ||
                    textViewport.Height != bodyPanel.Height - inset * 2)
                {
                    throw new InvalidOperationException("Expected the note body text viewport to apply the shared inset on every side.");
                }

                if (UserNotesUiState.BodyTextScaleForTesting <= 0.58f ||
                    UserNotesUiState.BodyLineHeightForTesting <= 18)
                {
                    throw new InvalidOperationException("Expected F5 note body text scale and line height to be enlarged together.");
                }

                var lines = UserNotesUiState.BuildWrappedBodyLinesForTesting(longBody, textViewport.Width);
                var expectedContentHeight = UserNotesUiState.CalculateBodyTextContentHeightForTesting(lines.Length);
                if (card.BodyContentHeight != expectedContentHeight)
                {
                    throw new InvalidOperationException("Expected layout content height to use the same text viewport width and line height as drawing.");
                }

                if (card.BodyContentHeight <= textViewport.Height)
                {
                    throw new InvalidOperationException("Expected the long note body to remain internally scrollable after layout.");
                }

                UserNotesUiState.BeginBodyViewportFrame();
                UserNotesUiState.SetBodyViewportForTesting(note.NoteId, textViewport, card.BodyContentHeight);
                var paddingMouse = new LegacyMouseSnapshot
                {
                    X = bodyPanel.X + 1,
                    Y = textViewport.Y + 2,
                    ScrollDelta = -120
                };
                if (UserNotesUiState.TryConsumeNestedScroll(paddingMouse, -120))
                {
                    throw new InvalidOperationException("Expected note body padding outside the text viewport to bubble wheel input.");
                }

                var textMouse = new LegacyMouseSnapshot
                {
                    X = textViewport.X + 2,
                    Y = textViewport.Y + 2,
                    ScrollDelta = -120
                };
                if (!UserNotesUiState.TryConsumeNestedScroll(textMouse, -120))
                {
                    throw new InvalidOperationException("Expected the text viewport to own internal body scrolling.");
                }

                LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
                {
                    ElementId = UserNotesUiState.BodyElementPrefix + note.NoteId,
                    Label = "笔记:正文",
                    Kind = "button",
                    Rect = textViewport,
                    MouseX = textViewport.X + 4,
                    MouseY = textViewport.Y + 4,
                    IsDoubleClick = true,
                    MouseCaptured = true
                });
                var anchorY = UserNotesUiState.ResolveBodyEditorImeLineY(note.NoteId, textViewport);
                if (anchorY < textViewport.Y || anchorY + UserNotesUiState.BodyLineHeightForTesting > textViewport.Bottom)
                {
                    throw new InvalidOperationException("Expected body IME anchor to stay inside the shared text viewport.");
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

        private static void UserNotesMultilineFocusAllowsButtonClickResolution()
        {
            var elements = new List<LegacyUiElement>
            {
                new LegacyUiElement
                {
                    Id = UserNotesUiState.PinButtonPrefix + "note-a",
                    Label = "笔记:悬挂",
                    Kind = "button",
                    Rect = new LegacyUiRect(10, 10, 80, 24),
                    Enabled = true
                }
            };
            var mouse = new LegacyMouseSnapshot
            {
                X = 20,
                Y = 20,
                LeftDown = true,
                LeftPressed = true,
                ReadAvailable = true,
                WindowHit = true
            };

            try
            {
                LegacyUiInput.ResetInteractionState();
                LegacyMultilineTextInput.Focus("notes:editor:body:note-a", "正文", 2);
                bool blocked;
                var resolved = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
                if (blocked || !string.Equals(resolved, UserNotesUiState.PinButtonPrefix + "note-a", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected multiline editor focus to allow note card buttons to resolve for enqueue.");
                }

                LegacyMultilineTextInput.ClearFocus();
                LegacyUiInput.HandleWindowFrame(
                    new LegacyMouseSnapshot
                    {
                        X = 4,
                        Y = 4,
                        LeftDown = true,
                        LeftPressed = true,
                        ReadAvailable = true,
                        WindowHit = true
                    },
                    new LegacyUiRect(0, 0, 120, 24),
                    new LegacyUiRect());
                resolved = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
                if (!string.IsNullOrEmpty(resolved) || blocked)
                {
                    throw new InvalidOperationException("Expected non-text active interactions to keep blocking card button click resolution.");
                }
            }
            finally
            {
                LegacyMultilineTextInput.ClearFocus();
                LegacyUiInput.ResetInteractionState();
                UserNotesUiState.ResetForTesting();
            }
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

        private static void UserNotesEditingCommandsSaveThenContinueOrStopOnFailure()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-editing-commands");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var store = new UserNotesStore();
                var cache = new UserNotesCache(store);
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create note");
                RequireSuccess(cache.SaveNote(note.NoteId, "标题", "正文", out note), "seed note");
                UserNotesUiState.SetCacheForTesting(cache, true);

                var bodyRect = new LegacyUiRect(10, 40, 260, 120);
                var inputId = UserNotesUiState.BuildEditorInputIdForTesting(note.NoteId, "body");

                OpenUserNoteBodyEditorForTesting(note.NoteId, bodyRect);
                LegacyMultilineTextInput.SetCursorIndex(inputId, int.MaxValue);
                LegacyMultilineTextInput.InsertTextForTesting(inputId, " pinned", true, 0);
                LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
                {
                    ElementId = UserNotesUiState.PinButtonPrefix + note.NoteId,
                    Label = "笔记:悬挂",
                    Kind = "button",
                    Rect = new LegacyUiRect(180, 8, 52, 24),
                    MouseCaptured = true
                });

                var saved = FindUserNote(cache.Snapshot, note.NoteId);
                if (saved == null ||
                    !string.Equals(saved.Body, "正文 pinned", StringComparison.Ordinal) ||
                    saved.PinnedState == null ||
                    !saved.PinnedState.Pinned ||
                    UserNotesUiState.HasActiveEditor)
                {
                    throw new InvalidOperationException("Expected Pin to save the active body editor and continue in the same command.");
                }

                OpenUserNoteBodyEditorForTesting(note.NoteId, bodyRect);
                LegacyMultilineTextInput.SetCursorIndex(inputId, int.MaxValue);
                LegacyMultilineTextInput.InsertTextForTesting(inputId, " delete", true, 0);
                LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
                {
                    ElementId = UserNotesUiState.DeleteButtonPrefix + note.NoteId,
                    Label = "笔记:删除",
                    Kind = "button",
                    Rect = new LegacyUiRect(236, 8, 44, 24),
                    MouseCaptured = true
                });

                saved = FindUserNote(cache.Snapshot, note.NoteId);
                if (saved == null ||
                    !string.Equals(saved.Body, "正文 pinned delete", StringComparison.Ordinal) ||
                    !UserNotesUiState.IsDeleteConfirming(note.NoteId) ||
                    UserNotesUiState.HasActiveEditor)
                {
                    throw new InvalidOperationException("Expected Delete to save the active body editor before arming confirmation.");
                }

                OpenUserNoteBodyEditorForTesting(note.NoteId, bodyRect);
                LegacyMultilineTextInput.SetCursorIndex(inputId, int.MaxValue);
                LegacyMultilineTextInput.InsertTextForTesting(inputId, " add", true, 0);
                var beforeAddCount = cache.Snapshot.Notes.Count;
                LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
                {
                    ElementId = UserNotesUiState.AddButtonId,
                    Label = "笔记:新增",
                    Kind = "button",
                    Rect = new LegacyUiRect(0, 0, 280, 34),
                    MouseCaptured = true
                });

                saved = FindUserNote(cache.Snapshot, note.NoteId);
                if (saved == null ||
                    !string.Equals(saved.Body, "正文 pinned delete add", StringComparison.Ordinal) ||
                    cache.Snapshot.Notes.Count != beforeAddCount + 1 ||
                    UserNotesUiState.HasActiveEditor)
                {
                    throw new InvalidOperationException("Expected Add to save the active body editor and then create the new note.");
                }

                OpenUserNoteBodyEditorForTesting(note.NoteId, bodyRect);
                LegacyMultilineTextInput.SetCursorIndex(inputId, int.MaxValue);
                LegacyMultilineTextInput.InsertTextForTesting(inputId, " blocked", true, 0);
                var beforeFailedAddCount = cache.Snapshot.Notes.Count;
                var bodyPath = store.GetBodyPath(note.NoteId);
                UserNotesStore.SetCommitFailurePredicateForTesting(path => string.Equals(path, bodyPath, StringComparison.OrdinalIgnoreCase));
                LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
                {
                    ElementId = UserNotesUiState.AddButtonId,
                    Label = "笔记:新增",
                    Kind = "button",
                    Rect = new LegacyUiRect(0, 0, 280, 34),
                    MouseCaptured = true
                });

                saved = FindUserNote(cache.Snapshot, note.NoteId);
                var draft = LegacyMultilineTextInput.GetSnapshot(inputId).Draft;
                if (saved == null ||
                    !string.Equals(saved.Body, "正文 pinned delete add", StringComparison.Ordinal) ||
                    cache.Snapshot.Notes.Count != beforeFailedAddCount ||
                    !UserNotesUiState.HasActiveEditor ||
                    draft == null ||
                    draft.IndexOf("blocked", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Expected failed save to stop Add and keep the body editor active.");
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
                RequireSuccess(
                    UserNotesUiState.ApplyActiveEditorControlForTesting(new TextInputControlState { EnterPressed = true }),
                    "submit edited title");

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
                LegacyMultilineTextInput.InsertTextForTesting(inputId, " outside", false, 80);
                LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
                {
                    ElementId = UserNotesUiState.EditOutsideElementId,
                    Label = "笔记:保存编辑",
                    Kind = "button",
                    Rect = new LegacyUiRect(0, 0, 320, 240),
                    MouseX = titleRect.Right + 12,
                    MouseY = titleRect.Y + 8,
                    MouseCaptured = true
                });

                saved = FindUserNote(cache.Snapshot, note.NoteId);
                if (saved == null ||
                    !string.Equals(saved.Title, "原标题 edited outside", StringComparison.Ordinal) ||
                    UserNotesUiState.HasActiveEditor)
                {
                    throw new InvalidOperationException("Expected clicking outside the title editor to save and close the editor.");
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
                RequireSuccess(
                    UserNotesUiState.ApplyActiveEditorControlForTesting(new TextInputControlState { EscapePressed = true }),
                    "cancel title edit");

                saved = FindUserNote(cache.Snapshot, note.NoteId);
                if (saved == null || !string.Equals(saved.Title, "原标题 edited outside", StringComparison.Ordinal))
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

        private static void UserNotesBodyEditorAutoScrollsCaretIntoViewport()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-body-editor-auto-scroll");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var cache = new UserNotesCache(new UserNotesStore());
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create note");
                RequireSuccess(cache.SaveNote(note.NoteId, "标题", "开头", out note), "seed body");
                UserNotesUiState.SetCacheForTesting(cache, true);

                var bodyRect = new LegacyUiRect(10, 40, 180, 90);
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
                LegacyMultilineTextInput.InsertTextForTesting(inputId, BuildLineSeparatedText("输入预览", 10), true, 0);
                var snapshot = LegacyMultilineTextInput.GetSnapshot(inputId);
                var viewport = new LegacyUiRect(20, 60, 150, UserNotesUiState.BodyLineHeightForTesting * 2);
                var lines = UserNotesUiState.BuildWrappedBodyLinesForTesting(snapshot.Draft, viewport.Width);
                var contentHeight = UserNotesUiState.CalculateBodyTextContentHeightForTesting(lines.Length);
                UserNotesUiState.BeginBodyViewportFrame();
                UserNotesUiState.SetBodyViewportForTesting(note.NoteId, viewport, contentHeight);

                if (!UserNotesUiState.EnsureActiveBodyEditorCaretVisibleForTesting(note.NoteId))
                {
                    throw new InvalidOperationException("Expected active body editor to scroll down when the caret leaves the viewport.");
                }

                var offset = UserNotesUiState.GetBodyScrollOffset(note.NoteId);
                if (offset <= 0)
                {
                    throw new InvalidOperationException("Expected active body editor scroll offset to increase.");
                }

                var anchorY = UserNotesUiState.ResolveBodyEditorImeLineY(note.NoteId, viewport);
                if (anchorY < viewport.Y || anchorY + UserNotesUiState.BodyLineHeightForTesting > viewport.Bottom)
                {
                    throw new InvalidOperationException("Expected active body editor caret preview to stay inside the visible viewport.");
                }

                LegacyMultilineTextInput.SetCursorIndex(inputId, 0);
                if (!UserNotesUiState.EnsureActiveBodyEditorCaretVisibleForTesting(note.NoteId) ||
                    UserNotesUiState.GetBodyScrollOffset(note.NoteId) != 0)
                {
                    throw new InvalidOperationException("Expected moving the body editor caret to the first line to scroll back to the top.");
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

        private static void UserNotesMultilineTextInputArmsAndReleasesNativeCapture()
        {
            var inputId = "notes:editor:capture";
            var foreignTextTaker = new object();
            try
            {
                ResetTextInputCaptureForTesting();
                TerrariaTextInputCompat.SetMainTypeForTesting(typeof(Terraria.Main));
                TerrariaInputCompat.SetUiInputMainTypeForTesting(typeof(Terraria.Main));

                LegacyMultilineTextInput.Focus(inputId, "body", 4);
                if (!Terraria.Main.blockInput ||
                    !Terraria.GameInput.PlayerInput.WritingText ||
                    Terraria.Main.CurrentInputTextTakerOverride == null)
                {
                    throw new InvalidOperationException("Expected focused multiline editor to arm native text input capture.");
                }

                var noteTextTaker = Terraria.Main.CurrentInputTextTakerOverride;
                Terraria.GameInput.PlayerInput.WritingText = false;
                bool textFocused;
                string textFocusReason;
                if (!TerrariaInputCompat.TryReadTextInputFocus(out textFocused, out textFocusReason) ||
                    !textFocused ||
                    !string.Equals(textFocusReason, "currentInputTextTakerOverride", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected note text taker to keep shared text input focus gates active.");
                }

                LegacyMultilineTextInput.UpdateInputCaptureGuard();
                if (!Terraria.GameInput.PlayerInput.WritingText ||
                    !ReferenceEquals(Terraria.Main.CurrentInputTextTakerOverride, noteTextTaker))
                {
                    throw new InvalidOperationException("Expected multiline editor guard to re-arm capture after PlayerInput reset.");
                }

                LegacyMultilineTextInput.ClearFocus();
                if (Terraria.Main.blockInput ||
                    Terraria.GameInput.PlayerInput.WritingText ||
                    Terraria.Main.CurrentInputTextTakerOverride != null)
                {
                    throw new InvalidOperationException("Expected multiline editor to release native capture when focus clears.");
                }

                Terraria.Main.CurrentInputTextTakerOverride = foreignTextTaker;
                TerrariaTextInputCompat.BeginTextInput();
                if (!ReferenceEquals(Terraria.Main.CurrentInputTextTakerOverride, foreignTextTaker))
                {
                    throw new InvalidOperationException("Expected native capture to preserve an existing Terraria text taker.");
                }

                TerrariaTextInputCompat.EndTextInput();
                if (!ReferenceEquals(Terraria.Main.CurrentInputTextTakerOverride, foreignTextTaker))
                {
                    throw new InvalidOperationException("Expected native capture release to avoid clearing a foreign text taker.");
                }
            }
            finally
            {
                ResetTextInputCaptureForTesting();
                TerrariaTextInputCompat.SetMainTypeForTesting(null);
                TerrariaInputCompat.SetUiInputMainTypeForTesting(null);
                LegacyMultilineTextInput.ClearFocus();
            }
        }

        private static void ResetTextInputCaptureForTesting()
        {
            LegacyMultilineTextInput.ClearFocus();
            TerrariaTextInputCompat.EndTextInput();
            Terraria.Main.blockInput = false;
            Terraria.Main.inputTextEnter = false;
            Terraria.Main.inputTextEscape = false;
            Terraria.Main.CurrentInputTextTakerOverride = null;
            Terraria.GameInput.PlayerInput.WritingText = false;
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

        private static void OpenUserNoteBodyEditorForTesting(string noteId, LegacyUiRect bodyRect)
        {
            LegacyUiActionService.HandleUserNotesCommandForTesting(new LegacyUiCommand
            {
                ElementId = UserNotesUiState.BodyElementPrefix + noteId,
                Label = "笔记:正文",
                Kind = "button",
                Rect = bodyRect,
                MouseX = bodyRect.X + 4,
                MouseY = bodyRect.Y + 4,
                IsDoubleClick = true,
                MouseCaptured = true
            });

            if (!UserNotesUiState.HasActiveEditor || !UserNotesUiState.IsEditingBody(noteId))
            {
                throw new InvalidOperationException("Expected double-clicking the note body to open the body editor.");
            }
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

        private static string BuildLineSeparatedText(string text, int count)
        {
            var result = string.Empty;
            for (var index = 0; index < count; index++)
            {
                result += "\n" + text + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return result;
        }
    }
}

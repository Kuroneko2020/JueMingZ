using System;
using System.IO;
using System.Text;
using JueMingZ.Automation.Information.Notes;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.Features;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void FeatureCatalogExposesUserNotesAsPlannedHiddenInformation()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.InformationUserNotes, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected user notes feature to be registered.");
            }

            if (feature.Id != FeatureIds.InformationUserNotes ||
                feature.CodeDomain != FeatureCodeDomain.Information ||
                feature.UserCategory != FeatureUserCategory.MoreInformation)
            {
                throw new InvalidOperationException("User notes feature must stay in Information / MoreInformation.");
            }

            if (feature.VisibleInMainUi || feature.IsImplemented ||
                feature.LifecycleStatus != FeatureLifecycleStatus.Planned)
            {
                throw new InvalidOperationException("User notes must remain planned and hidden until the F5 UI stage.");
            }
        }

        private static void UserNotesStoreMissingIndexUsesConfigNotesDirectory()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-missing-index");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var store = new UserNotesStore();
                AssertPathUnderConfigDirectory(store.NotesDirectory, "user notes directory");
                AssertPathUnderConfigDirectory(store.IndexPath, "user notes index");
                if (!store.NotesDirectory.EndsWith(Path.Combine("config", "notes"), StringComparison.OrdinalIgnoreCase) &&
                    !store.NotesDirectory.EndsWith("notes", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Expected notes directory to end with notes.");
                }

                UserNotesSnapshot snapshot;
                var result = store.TryLoadSnapshot(out snapshot);
                if (!result.Succeeded || snapshot == null || snapshot.Notes.Count != 0)
                {
                    throw new InvalidOperationException("Missing notes index should load as an empty snapshot.");
                }
            }
            finally
            {
                ResetUserNotesTestingHooks();
                restore();
            }
        }

        private static void UserNotesStoreCreatesUniqueDefaultNotesAndBodyFiles()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-create");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var cache = new UserNotesCache(new UserNotesStore());
                UserNoteSnapshot first;
                UserNoteSnapshot second;
                var firstResult = cache.CreateDefaultNote(out first);
                var secondResult = cache.CreateDefaultNote(out second);
                if (!firstResult.Succeeded || !secondResult.Succeeded)
                {
                    throw new InvalidOperationException("Expected default note creation to succeed.");
                }

                if (first == null || second == null ||
                    string.Equals(first.NoteId, second.NoteId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Two default notes must have distinct note ids.");
                }

                var snapshot = cache.Snapshot;
                if (snapshot.Notes.Count != 2)
                {
                    throw new InvalidOperationException("Expected cache to contain two notes after creation.");
                }

                AssertPathUnderConfigDirectory(UserNotesStore.GetDefaultNotesDirectory(), "default notes directory");
                AssertPathUnderConfigDirectory(Path.Combine(UserNotesStore.GetDefaultNotesDirectory(), first.NoteId + ".txt"), "first note body");
                AssertFileExists(Path.Combine(UserNotesStore.GetDefaultNotesDirectory(), "index.json"), "notes index");
                AssertFileExists(Path.Combine(UserNotesStore.GetDefaultNotesDirectory(), first.NoteId + ".txt"), "first note body");
                AssertFileExists(Path.Combine(UserNotesStore.GetDefaultNotesDirectory(), second.NoteId + ".txt"), "second note body");
            }
            finally
            {
                ResetUserNotesTestingHooks();
                restore();
            }
        }

        private static void UserNotesStoreCorruptIndexFailsSoftWithoutOverwrite()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-corrupt-index");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var store = new UserNotesStore();
                Directory.CreateDirectory(store.NotesDirectory);
                File.WriteAllText(store.IndexPath, "{ broken notes index", Encoding.UTF8);

                UserNotesSnapshot snapshot;
                var result = store.TryLoadSnapshot(out snapshot);
                if (result.Succeeded || !string.Equals(result.ResultCode, "indexReadFailed", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected corrupt notes index to fail softly.");
                }

                AssertFileTextEquals(store.IndexPath, "{ broken notes index", "corrupt notes index");
            }
            finally
            {
                ResetUserNotesTestingHooks();
                restore();
            }
        }

        private static void UserNotesSaveBodyFailureKeepsExistingCacheAndFiles()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-save-body-failure");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var store = new UserNotesStore();
                var cache = new UserNotesCache(store);
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create note");
                RequireSuccess(cache.SaveNote(note.NoteId, "old title", "old body", out note), "seed note");
                var bodyPath = store.GetBodyPath(note.NoteId);
                var oldIndex = File.ReadAllText(store.IndexPath, Encoding.UTF8);

                UserNotesStore.SetCommitFailurePredicateForTesting(path => string.Equals(path, bodyPath, StringComparison.OrdinalIgnoreCase));
                UserNoteSnapshot saved;
                var result = cache.SaveNote(note.NoteId, "new title", "new body", out saved);
                if (result.Succeeded)
                {
                    throw new InvalidOperationException("Expected body commit failure to fail note save.");
                }

                AssertFileTextEquals(bodyPath, "old body", "body failure note body");
                AssertFileTextEquals(store.IndexPath, oldIndex, "body failure index");
                AssertStringEquals(cache.Snapshot.Notes[0].Title, "old title", "body failure cache title");
            }
            finally
            {
                ResetUserNotesTestingHooks();
                restore();
            }
        }

        private static void UserNotesSaveIndexFailureRollsBackBody()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-save-index-failure");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var store = new UserNotesStore();
                var cache = new UserNotesCache(store);
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create note");
                RequireSuccess(cache.SaveNote(note.NoteId, "old title", "old body", out note), "seed note");
                var bodyPath = store.GetBodyPath(note.NoteId);
                var oldIndex = File.ReadAllText(store.IndexPath, Encoding.UTF8);

                UserNotesStore.SetCommitFailurePredicateForTesting(path => string.Equals(path, store.IndexPath, StringComparison.OrdinalIgnoreCase));
                UserNoteSnapshot saved;
                var result = cache.SaveNote(note.NoteId, "new title", "new body", out saved);
                if (result.Succeeded)
                {
                    throw new InvalidOperationException("Expected index commit failure to fail note save.");
                }

                AssertFileTextEquals(bodyPath, "old body", "index failure rolled back body");
                AssertFileTextEquals(store.IndexPath, oldIndex, "index failure original index");
                AssertStringEquals(cache.Snapshot.Notes[0].Body, "old body", "index failure cache body");
            }
            finally
            {
                ResetUserNotesTestingHooks();
                restore();
            }
        }

        private static void UserNotesDeleteBodyFailureKeepsVisibleNote()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-delete-body-failure");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var store = new UserNotesStore();
                var cache = new UserNotesCache(store);
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create note");
                RequireSuccess(cache.SaveNote(note.NoteId, "old title", "old body", out note), "seed note");
                var bodyPath = store.GetBodyPath(note.NoteId);

                UserNotesStore.SetDeleteFailurePredicateForTesting(path => string.Equals(path, bodyPath, StringComparison.OrdinalIgnoreCase));
                var result = cache.DeleteNote(note.NoteId);
                if (result.Succeeded)
                {
                    throw new InvalidOperationException("Expected body delete failure to fail note deletion.");
                }

                AssertFileExists(bodyPath, "body after delete failure");
                if (cache.Snapshot.Notes.Count != 1)
                {
                    throw new InvalidOperationException("Cache must keep note visible after delete body failure.");
                }
            }
            finally
            {
                ResetUserNotesTestingHooks();
                restore();
            }
        }

        private static void UserNotesDeleteIndexFailureRestoresBody()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-delete-index-failure");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var store = new UserNotesStore();
                var cache = new UserNotesCache(store);
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create note");
                RequireSuccess(cache.SaveNote(note.NoteId, "old title", "old body", out note), "seed note");
                var bodyPath = store.GetBodyPath(note.NoteId);

                UserNotesStore.SetCommitFailurePredicateForTesting(path => string.Equals(path, store.IndexPath, StringComparison.OrdinalIgnoreCase));
                var result = cache.DeleteNote(note.NoteId);
                if (result.Succeeded)
                {
                    throw new InvalidOperationException("Expected index failure to fail note deletion.");
                }

                AssertFileExists(bodyPath, "body restored after delete index failure");
                AssertFileTextEquals(bodyPath, "old body", "delete index failure body");
                if (cache.Snapshot.Notes.Count != 1)
                {
                    throw new InvalidOperationException("Cache must keep note visible after delete index failure.");
                }
            }
            finally
            {
                ResetUserNotesTestingHooks();
                restore();
            }
        }

        private static void UserNotesCacheSnapshotDoesNotTouchDisk()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-cache-no-io");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var cache = new UserNotesCache(new UserNotesStore());
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create note");
                var ioCount = 0;
                UserNotesStore.SetIoObserverForTesting((operation, path) => ioCount++);

                var snapshot = cache.Snapshot;
                if (snapshot.Notes.Count != 1 || ioCount != 0)
                {
                    throw new InvalidOperationException("Reading cached notes snapshot must not touch disk.");
                }
            }
            finally
            {
                ResetUserNotesTestingHooks();
                restore();
            }
        }

        private static void DisableUserNotesDiagnosticsForTesting()
        {
            UserNotesDiagnostics.SetRecordActionEventsForTesting(false);
            JueMingZ.UI.UserNotesPinnedOverlayInputDiagnostics.SetRecordActionEventsForTesting(false);
        }

        private static void ResetUserNotesTestingHooks()
        {
            UserNotesStore.ResetTestingHooks();
            UserNotesDiagnostics.SetObserverForTesting(null);
            UserNotesDiagnostics.SetRecordActionEventsForTesting(true);
            JueMingZ.UI.UserNotesPinnedOverlayInputDiagnostics.ResetForTesting();
            JueMingZ.UI.UserNotesPinnedOverlayInputDiagnostics.SetRecordActionEventsForTesting(true);
        }

        private static void RequireSuccess(UserNotesOperationResult result, string label)
        {
            if (result == null || !result.Succeeded)
            {
                throw new InvalidOperationException("Expected " + label + " to succeed, got " + (result == null ? "<null>" : result.ResultCode + ":" + result.Message));
            }
        }

        private static void AssertFileExists(string path, string label)
        {
            if (!File.Exists(path))
            {
                throw new InvalidOperationException("Expected " + label + " to exist: " + path);
            }
        }
    }
}

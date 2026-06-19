using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.Automation.Information.Notes
{
    public sealed class UserNotesStore
    {
        public const int CurrentSchemaVersion = 1;
        public const string DefaultTitle = "新笔记";

        private static readonly object TestingSyncRoot = new object();
        private static Func<string, string> _tempPathFactoryForTesting;
        private static Func<string, bool> _commitFailurePredicateForTesting;
        private static Func<string, bool> _deleteFailurePredicateForTesting;
        private static Action<string, string> _ioObserverForTesting;

        private readonly string _notesDirectory;

        public UserNotesStore()
            : this(GetDefaultNotesDirectory())
        {
        }

        public UserNotesStore(string notesDirectory)
        {
            _notesDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(notesDirectory) ? GetDefaultNotesDirectory() : notesDirectory);
        }

        public string NotesDirectory
        {
            get { return _notesDirectory; }
        }

        public string IndexPath
        {
            get { return Path.Combine(_notesDirectory, "index.json"); }
        }

        public static string GetDefaultNotesDirectory()
        {
            return Path.Combine(ConfigService.ConfigDirectory, "notes");
        }

        public UserNotesOperationResult TryLoadSnapshot(out UserNotesSnapshot snapshot)
        {
            snapshot = UserNotesSnapshot.Empty;
            List<UserNotesIndexEntry> entries;
            UserNotesOperationResult result;
            if (!TryLoadIndex(out entries, out result))
            {
                return result;
            }

            var notes = new List<UserNoteSnapshot>();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.NoteId))
                {
                    continue;
                }

                var bodyPath = GetBodyPath(entry.NoteId);
                var body = string.Empty;
                try
                {
                    if (File.Exists(bodyPath))
                    {
                        NotifyIo("read", bodyPath);
                        body = File.ReadAllText(bodyPath, Encoding.UTF8);
                    }
                }
                catch (Exception error)
                {
                    LogThrottle.WarnThrottled(
                        "user-notes-body-read-failed:" + bodyPath,
                        TimeSpan.FromSeconds(30),
                        "UserNotesStore",
                        "User note body read failed: " + error.Message);
                    body = string.Empty;
                }

                notes.Add(BuildSnapshot(entry, body));
            }

            snapshot = new UserNotesSnapshot(notes, Environment.TickCount);
            return UserNotesOperationResult.Success("loaded", "loaded", _notesDirectory);
        }

        public UserNotesOperationResult CreateDefaultNote(out UserNoteSnapshot note)
        {
            note = null;
            List<UserNotesIndexEntry> entries;
            UserNotesOperationResult load;
            if (!TryLoadIndex(out entries, out load))
            {
                return load;
            }

            var now = DateTime.UtcNow;
            var noteId = CreateNoteId(now);
            while (ContainsNote(entries, noteId))
            {
                noteId = CreateNoteId(DateTime.UtcNow);
            }

            var entry = CreateEntry(noteId, DefaultTitle, now, string.Empty, new UserNotePinnedState());
            var newEntries = CloneEntries(entries);
            newEntries.Add(entry);
            var result = CommitBodyAndIndex(noteId, string.Empty, newEntries);
            if (!result.Succeeded)
            {
                return result;
            }

            note = BuildSnapshot(entry, string.Empty);
            UserNotesDiagnostics.RecordOperation("Ui.Notes.Add", result, noteId);
            return result;
        }

        public UserNotesOperationResult SaveNote(string noteId, string title, string body, out UserNoteSnapshot note)
        {
            note = null;
            var normalizedId = NormalizeNoteId(noteId);
            if (string.IsNullOrEmpty(normalizedId))
            {
                return UserNotesOperationResult.Failure("invalidNoteId", "invalid note id", IndexPath);
            }

            List<UserNotesIndexEntry> entries;
            UserNotesOperationResult load;
            if (!TryLoadIndex(out entries, out load))
            {
                return load;
            }

            var index = FindNoteIndex(entries, normalizedId);
            if (index < 0)
            {
                return UserNotesOperationResult.Failure("missingNote", "note not found", IndexPath);
            }

            var now = DateTime.UtcNow;
            var newEntries = CloneEntries(entries);
            var entry = newEntries[index];
            entry.Title = NormalizeTitle(title);
            entry.UpdatedUtc = FormatUtc(now);
            entry.BodyUpdatedUtc = FormatUtc(now);
            entry.BodyLength = (body ?? string.Empty).Length;

            var result = CommitBodyAndIndex(normalizedId, body ?? string.Empty, newEntries);
            if (!result.Succeeded)
            {
                return result;
            }

            note = BuildSnapshot(entry, body ?? string.Empty);
            UserNotesDiagnostics.RecordOperation("Ui.Notes.Save", result, normalizedId);
            return result;
        }

        public UserNotesOperationResult UpdatePinnedState(string noteId, UserNotePinnedState pinnedState, out UserNoteSnapshot note)
        {
            note = null;
            var normalizedId = NormalizeNoteId(noteId);
            if (string.IsNullOrEmpty(normalizedId))
            {
                return UserNotesOperationResult.Failure("invalidNoteId", "invalid note id", IndexPath);
            }

            List<UserNotesIndexEntry> entries;
            UserNotesOperationResult load;
            if (!TryLoadIndex(out entries, out load))
            {
                return load;
            }

            var index = FindNoteIndex(entries, normalizedId);
            if (index < 0)
            {
                return UserNotesOperationResult.Failure("missingNote", "note not found", IndexPath);
            }

            var state = NormalizePinnedState(pinnedState);
            var newEntries = CloneEntries(entries);
            var entry = newEntries[index];
            entry.Pinned = state.Pinned;
            entry.PinnedX = state.X;
            entry.PinnedY = state.Y;
            entry.PinnedWidth = state.Width;
            entry.PinnedHeight = state.Height;
            entry.OpacityPercent = state.OpacityPercent;
            entry.UpdatedUtc = FormatUtc(DateTime.UtcNow);

            var result = CommitIndexOnly(newEntries);
            if (!result.Succeeded)
            {
                return result;
            }

            string body;
            TryReadBody(normalizedId, out body);
            note = BuildSnapshot(entry, body);
            UserNotesDiagnostics.RecordOperation(state.Pinned ? "Ui.Notes.Pin" : "Ui.Notes.Unpin", result, normalizedId);
            return result;
        }

        public UserNotesOperationResult DeleteNote(string noteId)
        {
            var normalizedId = NormalizeNoteId(noteId);
            if (string.IsNullOrEmpty(normalizedId))
            {
                return UserNotesOperationResult.Failure("invalidNoteId", "invalid note id", IndexPath);
            }

            List<UserNotesIndexEntry> entries;
            UserNotesOperationResult load;
            if (!TryLoadIndex(out entries, out load))
            {
                return load;
            }

            var index = FindNoteIndex(entries, normalizedId);
            if (index < 0)
            {
                return UserNotesOperationResult.Failure("missingNote", "note not found", IndexPath);
            }

            var newEntries = CloneEntries(entries);
            newEntries.RemoveAt(index);
            var bodyPath = GetBodyPath(normalizedId);
            PreparedDelete bodyDelete = null;
            try
            {
                bodyDelete = PrepareDelete(bodyPath);
                bodyDelete.Commit();
                var result = CommitIndexOnly(newEntries);
                if (!result.Succeeded)
                {
                    bodyDelete.Rollback();
                    return result;
                }

                UserNotesDiagnostics.RecordOperation("Ui.Notes.Delete", result, normalizedId);
                return result;
            }
            catch (Exception error)
            {
                if (bodyDelete != null)
                {
                    bodyDelete.Rollback();
                }

                return UserNotesOperationResult.Failure("deleteFailed", error.GetType().Name + ": " + error.Message, bodyPath);
            }
            finally
            {
                if (bodyDelete != null)
                {
                    bodyDelete.Cleanup();
                }
            }
        }

        public string GetBodyPath(string noteId)
        {
            return Path.Combine(_notesDirectory, NormalizeNoteId(noteId) + ".txt");
        }

        internal static void SetTempPathFactoryForTesting(Func<string, string> factory)
        {
            lock (TestingSyncRoot)
            {
                _tempPathFactoryForTesting = factory;
            }
        }

        internal static void SetCommitFailurePredicateForTesting(Func<string, bool> predicate)
        {
            lock (TestingSyncRoot)
            {
                _commitFailurePredicateForTesting = predicate;
            }
        }

        internal static void SetDeleteFailurePredicateForTesting(Func<string, bool> predicate)
        {
            lock (TestingSyncRoot)
            {
                _deleteFailurePredicateForTesting = predicate;
            }
        }

        internal static void SetIoObserverForTesting(Action<string, string> observer)
        {
            lock (TestingSyncRoot)
            {
                _ioObserverForTesting = observer;
            }
        }

        internal static void ResetTestingHooks()
        {
            lock (TestingSyncRoot)
            {
                _tempPathFactoryForTesting = null;
                _commitFailurePredicateForTesting = null;
                _deleteFailurePredicateForTesting = null;
                _ioObserverForTesting = null;
            }
        }

        private bool TryLoadIndex(out List<UserNotesIndexEntry> entries, out UserNotesOperationResult result)
        {
            entries = new List<UserNotesIndexEntry>();
            var path = IndexPath;
            try
            {
                if (!File.Exists(path))
                {
                    result = UserNotesOperationResult.Success("missingIndex", "missing index", path);
                    return true;
                }

                NotifyIo("read", path);
                UserNotesIndexFile file;
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    file = CreateSerializer(typeof(UserNotesIndexFile)).ReadObject(stream) as UserNotesIndexFile;
                }

                if (file == null || file.Notes == null)
                {
                    result = UserNotesOperationResult.Success("emptyIndex", "empty index", path);
                    return true;
                }

                entries = NormalizeEntries(file.Notes);
                result = UserNotesOperationResult.Success("loaded", "loaded", path);
                return true;
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "user-notes-index-read-failed:" + path,
                    TimeSpan.FromSeconds(30),
                    "UserNotesStore",
                    "User notes index read failed: " + error.Message);
                result = UserNotesOperationResult.Failure("indexReadFailed", error.GetType().Name + ": " + error.Message, path);
                return false;
            }
        }

        private UserNotesOperationResult CommitBodyAndIndex(string noteId, string body, List<UserNotesIndexEntry> entries)
        {
            PreparedFileWrite bodyWrite = null;
            PreparedFileWrite indexWrite = null;
            try
            {
                bodyWrite = PrepareTextWrite(GetBodyPath(noteId), body ?? string.Empty);
                indexWrite = PrepareIndexWrite(entries);
                bodyWrite.Commit();
                try
                {
                    indexWrite.Commit();
                }
                catch
                {
                    bodyWrite.RollbackCommitted();
                    throw;
                }

                return UserNotesOperationResult.Success("saved", "saved", IndexPath);
            }
            catch (Exception error)
            {
                return UserNotesOperationResult.Failure("saveFailed", error.GetType().Name + ": " + error.Message, IndexPath);
            }
            finally
            {
                if (bodyWrite != null)
                {
                    bodyWrite.Cleanup();
                }

                if (indexWrite != null)
                {
                    indexWrite.Cleanup();
                }
            }
        }

        private UserNotesOperationResult CommitIndexOnly(List<UserNotesIndexEntry> entries)
        {
            PreparedFileWrite indexWrite = null;
            try
            {
                indexWrite = PrepareIndexWrite(entries);
                indexWrite.Commit();
                return UserNotesOperationResult.Success("saved", "saved", IndexPath);
            }
            catch (Exception error)
            {
                return UserNotesOperationResult.Failure("saveFailed", error.GetType().Name + ": " + error.Message, IndexPath);
            }
            finally
            {
                if (indexWrite != null)
                {
                    indexWrite.Cleanup();
                }
            }
        }

        private PreparedFileWrite PrepareIndexWrite(List<UserNotesIndexEntry> entries)
        {
            var file = new UserNotesIndexFile
            {
                SchemaVersion = CurrentSchemaVersion,
                Notes = CloneEntries(entries)
            };

            var tempPath = CreateTempPath(IndexPath);
            var directory = Path.GetDirectoryName(IndexPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            NotifyIo("write", tempPath);
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                CreateSerializer(typeof(UserNotesIndexFile)).WriteObject(stream, file);
                stream.Flush(true);
            }

            return new PreparedFileWrite(IndexPath, tempPath);
        }

        private PreparedFileWrite PrepareTextWrite(string path, string text)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = CreateTempPath(path);
            NotifyIo("write", tempPath);
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(text ?? string.Empty);
                writer.Flush();
                stream.Flush(true);
            }

            return new PreparedFileWrite(path, tempPath);
        }

        private PreparedDelete PrepareDelete(string path)
        {
            return new PreparedDelete(path);
        }

        private bool TryReadBody(string noteId, out string body)
        {
            body = string.Empty;
            var path = GetBodyPath(noteId);
            try
            {
                if (!File.Exists(path))
                {
                    return true;
                }

                NotifyIo("read", path);
                body = File.ReadAllText(path, Encoding.UTF8);
                return true;
            }
            catch
            {
                body = string.Empty;
                return false;
            }
        }

        private static UserNotesIndexEntry CreateEntry(string noteId, string title, DateTime now, string body, UserNotePinnedState pinnedState)
        {
            var state = NormalizePinnedState(pinnedState);
            return new UserNotesIndexEntry
            {
                NoteId = noteId,
                Title = NormalizeTitle(title),
                CreatedUtc = FormatUtc(now),
                UpdatedUtc = FormatUtc(now),
                Pinned = state.Pinned,
                PinnedX = state.X,
                PinnedY = state.Y,
                PinnedWidth = state.Width,
                PinnedHeight = state.Height,
                OpacityPercent = state.OpacityPercent,
                BodyUpdatedUtc = FormatUtc(now),
                BodyLength = (body ?? string.Empty).Length
            };
        }

        private static UserNoteSnapshot BuildSnapshot(UserNotesIndexEntry entry, string body)
        {
            var created = ParseUtc(entry.CreatedUtc);
            var updated = ParseUtc(entry.UpdatedUtc);
            var bodyUpdated = ParseUtc(entry.BodyUpdatedUtc);
            return new UserNoteSnapshot
            {
                NoteId = NormalizeNoteId(entry.NoteId),
                Title = NormalizeTitle(entry.Title),
                CreatedUtc = created,
                UpdatedUtc = updated,
                PinnedState = NormalizePinnedState(new UserNotePinnedState
                {
                    Pinned = entry.Pinned,
                    X = entry.PinnedX,
                    Y = entry.PinnedY,
                    Width = entry.PinnedWidth,
                    Height = entry.PinnedHeight,
                    OpacityPercent = entry.OpacityPercent
                }),
                Body = body ?? string.Empty,
                BodyUpdatedUtc = bodyUpdated,
                BodyLength = (body ?? string.Empty).Length
            };
        }

        private static List<UserNotesIndexEntry> NormalizeEntries(List<UserNotesIndexEntry> entries)
        {
            var result = new List<UserNotesIndexEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (entries == null)
            {
                return result;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    continue;
                }

                var noteId = NormalizeNoteId(entry.NoteId);
                if (string.IsNullOrEmpty(noteId) || !seen.Add(noteId))
                {
                    continue;
                }

                var created = ParseUtc(entry.CreatedUtc);
                var updated = ParseUtc(entry.UpdatedUtc);
                var bodyUpdated = ParseUtc(entry.BodyUpdatedUtc);
                result.Add(new UserNotesIndexEntry
                {
                    NoteId = noteId,
                    Title = NormalizeTitle(entry.Title),
                    CreatedUtc = FormatUtc(created),
                    UpdatedUtc = FormatUtc(updated < created ? created : updated),
                    Pinned = entry.Pinned,
                    PinnedX = entry.PinnedX,
                    PinnedY = entry.PinnedY,
                    PinnedWidth = Math.Max(0f, entry.PinnedWidth),
                    PinnedHeight = Math.Max(0f, entry.PinnedHeight),
                    OpacityPercent = ClampOpacity(entry.OpacityPercent),
                    BodyUpdatedUtc = FormatUtc(bodyUpdated < created ? created : bodyUpdated),
                    BodyLength = Math.Max(0, entry.BodyLength)
                });
            }

            return result;
        }

        private static List<UserNotesIndexEntry> CloneEntries(List<UserNotesIndexEntry> entries)
        {
            var result = new List<UserNotesIndexEntry>();
            if (entries == null)
            {
                return result;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    continue;
                }

                result.Add(new UserNotesIndexEntry
                {
                    NoteId = entry.NoteId ?? string.Empty,
                    Title = entry.Title ?? string.Empty,
                    CreatedUtc = entry.CreatedUtc ?? string.Empty,
                    UpdatedUtc = entry.UpdatedUtc ?? string.Empty,
                    Pinned = entry.Pinned,
                    PinnedX = entry.PinnedX,
                    PinnedY = entry.PinnedY,
                    PinnedWidth = entry.PinnedWidth,
                    PinnedHeight = entry.PinnedHeight,
                    OpacityPercent = entry.OpacityPercent,
                    BodyUpdatedUtc = entry.BodyUpdatedUtc ?? string.Empty,
                    BodyLength = entry.BodyLength
                });
            }

            return result;
        }

        private static int FindNoteIndex(List<UserNotesIndexEntry> entries, string noteId)
        {
            if (entries == null)
            {
                return -1;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                if (entries[index] != null &&
                    string.Equals(entries[index].NoteId, noteId, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool ContainsNote(List<UserNotesIndexEntry> entries, string noteId)
        {
            return FindNoteIndex(entries, noteId) >= 0;
        }

        private static string CreateNoteId(DateTime now)
        {
            return "note-" + now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        private static string NormalizeTitle(string title)
        {
            var value = string.IsNullOrWhiteSpace(title) ? DefaultTitle : title.Trim();
            value = value.Replace("\r", " ").Replace("\n", " ");
            return value.Length > 80 ? value.Substring(0, 80) : value;
        }

        public static string NormalizeNoteId(string noteId)
        {
            var value = string.IsNullOrWhiteSpace(noteId) ? string.Empty : noteId.Trim();
            if (value.Length <= 0 || value.Length > 80)
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

        private static UserNotePinnedState NormalizePinnedState(UserNotePinnedState state)
        {
            var source = state ?? new UserNotePinnedState();
            return new UserNotePinnedState
            {
                Pinned = source.Pinned,
                X = source.X,
                Y = source.Y,
                Width = Math.Max(0f, source.Width),
                Height = Math.Max(0f, source.Height),
                OpacityPercent = ClampOpacity(source.OpacityPercent)
            };
        }

        private static int ClampOpacity(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value > 100)
            {
                return 100;
            }

            return value;
        }

        private static DateTime ParseUtc(string value)
        {
            DateTime parsed;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed))
            {
                return parsed.ToUniversalTime();
            }

            return DateTime.UtcNow;
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }

        private static DataContractJsonSerializer CreateSerializer(Type type)
        {
            return new DataContractJsonSerializer(type);
        }

        private static string CreateTempPath(string targetPath)
        {
            Func<string, string> factory;
            lock (TestingSyncRoot)
            {
                factory = _tempPathFactoryForTesting;
            }

            return factory != null
                ? factory(targetPath)
                : targetPath + ".tmp-" + Guid.NewGuid().ToString("N");
        }

        private static string CreateBackupPath(string targetPath)
        {
            return targetPath + ".bak-" + Guid.NewGuid().ToString("N");
        }

        private static bool ShouldFailCommit(string targetPath)
        {
            Func<string, bool> predicate;
            lock (TestingSyncRoot)
            {
                predicate = _commitFailurePredicateForTesting;
            }

            return predicate != null && predicate(targetPath);
        }

        private static bool ShouldFailDelete(string targetPath)
        {
            Func<string, bool> predicate;
            lock (TestingSyncRoot)
            {
                predicate = _deleteFailurePredicateForTesting;
            }

            return predicate != null && predicate(targetPath);
        }

        private static void NotifyIo(string operation, string path)
        {
            Action<string, string> observer;
            lock (TestingSyncRoot)
            {
                observer = _ioObserverForTesting;
            }

            if (observer != null)
            {
                observer(operation ?? string.Empty, path ?? string.Empty);
            }
        }

        private sealed class PreparedFileWrite
        {
            private readonly string _targetPath;
            private readonly string _tempPath;
            private readonly string _backupPath;
            private readonly bool _targetExisted;
            private bool _committed;

            public PreparedFileWrite(string targetPath, string tempPath)
            {
                _targetPath = targetPath;
                _tempPath = tempPath;
                _backupPath = CreateBackupPath(targetPath);
                _targetExisted = File.Exists(targetPath);
            }

            public void Commit()
            {
                if (ShouldFailCommit(_targetPath))
                {
                    throw new IOException("simulated commit failure: " + _targetPath);
                }

                NotifyIo("write", _targetPath);
                if (_targetExisted)
                {
                    File.Copy(_targetPath, _backupPath, true);
                    File.Replace(_tempPath, _targetPath, null, true);
                }
                else
                {
                    File.Move(_tempPath, _targetPath);
                }

                _committed = true;
            }

            public void RollbackCommitted()
            {
                if (!_committed)
                {
                    return;
                }

                try
                {
                    if (_targetExisted)
                    {
                        if (File.Exists(_backupPath))
                        {
                            File.Copy(_backupPath, _targetPath, true);
                        }
                    }
                    else if (File.Exists(_targetPath))
                    {
                        File.Delete(_targetPath);
                    }
                }
                catch
                {
                }
            }

            public void Cleanup()
            {
                TryDelete(_tempPath);
                TryDelete(_backupPath);
            }
        }

        private sealed class PreparedDelete
        {
            private readonly string _path;
            private readonly string _backupPath;
            private readonly bool _existed;
            private bool _committed;

            public PreparedDelete(string path)
            {
                _path = path;
                _backupPath = CreateBackupPath(path);
                _existed = File.Exists(path);
            }

            public void Commit()
            {
                if (!_existed)
                {
                    return;
                }

                if (ShouldFailDelete(_path))
                {
                    throw new IOException("simulated delete failure: " + _path);
                }

                NotifyIo("delete", _path);
                File.Copy(_path, _backupPath, true);
                File.Delete(_path);
                _committed = true;
            }

            public void Rollback()
            {
                if (!_committed || !_existed || !File.Exists(_backupPath))
                {
                    return;
                }

                try
                {
                    File.Copy(_backupPath, _path, true);
                }
                catch
                {
                }
            }

            public void Cleanup()
            {
                TryDelete(_backupPath);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}

using System;

namespace JueMingZ.Automation.Information.Notes
{
    public sealed class UserNotesCache
    {
        private readonly UserNotesStore _store;
        private readonly object _syncRoot = new object();
        private UserNotesSnapshot _snapshot = UserNotesSnapshot.Empty;
        private int _revision;

        public UserNotesCache()
            : this(new UserNotesStore())
        {
        }

        public UserNotesCache(UserNotesStore store)
        {
            _store = store ?? new UserNotesStore();
        }

        public UserNotesSnapshot Snapshot
        {
            get
            {
                lock (_syncRoot)
                {
                    return _snapshot.Clone();
                }
            }
        }

        public UserNotesOperationResult Refresh()
        {
            UserNotesSnapshot loaded;
            var result = _store.TryLoadSnapshot(out loaded);
            if (!result.Succeeded)
            {
                return result;
            }

            lock (_syncRoot)
            {
                _revision++;
                _snapshot = new UserNotesSnapshot(loaded.Notes, _revision);
            }

            return result;
        }

        public UserNotesOperationResult CreateDefaultNote(out UserNoteSnapshot note)
        {
            note = null;
            var result = _store.CreateDefaultNote(out note);
            if (result.Succeeded)
            {
                Refresh();
            }

            return result;
        }

        public UserNotesOperationResult SaveNote(string noteId, string title, string body, out UserNoteSnapshot note)
        {
            note = null;
            var result = _store.SaveNote(noteId, title, body, out note);
            if (result.Succeeded)
            {
                Refresh();
            }

            return result;
        }

        public UserNotesOperationResult UpdatePinnedState(string noteId, UserNotePinnedState pinnedState, out UserNoteSnapshot note)
        {
            note = null;
            var result = _store.UpdatePinnedState(noteId, pinnedState, out note);
            if (result.Succeeded)
            {
                Refresh();
            }

            return result;
        }

        public UserNotesOperationResult DeleteNote(string noteId)
        {
            var result = _store.DeleteNote(noteId);
            if (result.Succeeded)
            {
                Refresh();
            }

            return result;
        }
    }
}

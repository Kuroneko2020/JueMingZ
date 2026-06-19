using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace JueMingZ.Automation.Information.Notes
{
    [DataContract]
    internal sealed class UserNotesIndexFile
    {
        [DataMember(Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Order = 2)]
        public List<UserNotesIndexEntry> Notes { get; set; }
    }

    [DataContract]
    internal sealed class UserNotesIndexEntry
    {
        [DataMember(Order = 1)]
        public string NoteId { get; set; }

        [DataMember(Order = 2)]
        public string Title { get; set; }

        [DataMember(Order = 3)]
        public string CreatedUtc { get; set; }

        [DataMember(Order = 4)]
        public string UpdatedUtc { get; set; }

        [DataMember(Order = 5)]
        public bool Pinned { get; set; }

        [DataMember(Order = 6)]
        public float PinnedX { get; set; }

        [DataMember(Order = 7)]
        public float PinnedY { get; set; }

        [DataMember(Order = 8)]
        public float PinnedWidth { get; set; }

        [DataMember(Order = 9)]
        public float PinnedHeight { get; set; }

        [DataMember(Order = 10)]
        public int OpacityPercent { get; set; }

        [DataMember(Order = 11)]
        public string BodyUpdatedUtc { get; set; }

        [DataMember(Order = 12)]
        public int BodyLength { get; set; }
    }

    public sealed class UserNotePinnedState
    {
        public UserNotePinnedState()
        {
            OpacityPercent = 100;
        }

        public bool Pinned { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public int OpacityPercent { get; set; }

        public UserNotePinnedState Clone()
        {
            return new UserNotePinnedState
            {
                Pinned = Pinned,
                X = X,
                Y = Y,
                Width = Width,
                Height = Height,
                OpacityPercent = OpacityPercent
            };
        }
    }

    public sealed class UserNoteSnapshot
    {
        public string NoteId { get; set; }
        public string Title { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public UserNotePinnedState PinnedState { get; set; }
        public string Body { get; set; }
        public DateTime BodyUpdatedUtc { get; set; }
        public int BodyLength { get; set; }

        public UserNoteSnapshot Clone()
        {
            return new UserNoteSnapshot
            {
                NoteId = NoteId ?? string.Empty,
                Title = Title ?? string.Empty,
                CreatedUtc = CreatedUtc,
                UpdatedUtc = UpdatedUtc,
                PinnedState = PinnedState == null ? new UserNotePinnedState() : PinnedState.Clone(),
                Body = Body ?? string.Empty,
                BodyUpdatedUtc = BodyUpdatedUtc,
                BodyLength = BodyLength
            };
        }
    }

    public sealed class UserNotesSnapshot
    {
        private static readonly UserNotesSnapshot EmptyInstance = new UserNotesSnapshot(new List<UserNoteSnapshot>(), 0);
        private readonly List<UserNoteSnapshot> _notes;

        public UserNotesSnapshot(IReadOnlyList<UserNoteSnapshot> notes, int revision)
        {
            _notes = CloneList(notes);
            Revision = revision;
        }

        public static UserNotesSnapshot Empty
        {
            get { return EmptyInstance; }
        }

        public int Revision { get; private set; }

        public IReadOnlyList<UserNoteSnapshot> Notes
        {
            get { return _notes; }
        }

        public UserNotesSnapshot Clone()
        {
            return new UserNotesSnapshot(_notes, Revision);
        }

        private static List<UserNoteSnapshot> CloneList(IReadOnlyList<UserNoteSnapshot> notes)
        {
            var result = new List<UserNoteSnapshot>();
            if (notes == null)
            {
                return result;
            }

            for (var index = 0; index < notes.Count; index++)
            {
                var note = notes[index];
                if (note != null)
                {
                    result.Add(note.Clone());
                }
            }

            return result;
        }
    }

    public sealed class UserNotesOperationResult
    {
        public bool Succeeded { get; private set; }
        public string ResultCode { get; private set; }
        public string Message { get; private set; }
        public string Path { get; private set; }

        private UserNotesOperationResult(bool succeeded, string resultCode, string message, string path)
        {
            Succeeded = succeeded;
            ResultCode = resultCode ?? string.Empty;
            Message = message ?? string.Empty;
            Path = path ?? string.Empty;
        }

        public static UserNotesOperationResult Success(string resultCode, string message, string path = "")
        {
            return new UserNotesOperationResult(true, resultCode, message, path);
        }

        public static UserNotesOperationResult Failure(string resultCode, string message, string path = "")
        {
            return new UserNotesOperationResult(false, resultCode, message, path);
        }
    }
}

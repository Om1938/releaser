using Releaser.Domain.ReleaseNotes;

namespace Releaser.Server.Features.ReleaseNotes;

/// <summary>Append-only history entry written for every change to a release note.</summary>
public sealed class ReleaseNoteRevision
{
    private ReleaseNoteRevision() { } // EF Core

    public Guid Id { get; private set; }
    public Guid ReleaseNoteId { get; private set; }
    public int Revision { get; private set; }
    public ReleaseNoteState State { get; private set; }
    public ReleaseNoteContent Content { get; private set; } = null!;
    public string? Actor { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    public static ReleaseNoteRevision Of(ReleaseNote note, string? actor) => new()
    {
        Id = Guid.CreateVersion7(),
        ReleaseNoteId = note.Id,
        Revision = note.Revision,
        State = note.State,
        Content = note.Content,
        Actor = actor,
        RecordedAt = note.UpdatedAt,
    };
}

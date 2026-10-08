using Releaser.Domain.Common;

namespace Releaser.Domain.ReleaseNotes;

public enum ReleaseNoteState
{
    Draft = 1,
    Published = 2,
}

/// <summary>Keep-a-Changelog style categories.</summary>
public enum ChangeCategory
{
    Added = 1,
    Changed = 2,
    Fixed = 3,
    Removed = 4,
    Deprecated = 5,
    Security = 6,
}

public sealed record ReleaseNoteChange(ChangeCategory Category, string Text);

/// <summary>Content of one revision of a release note.</summary>
public sealed record ReleaseNoteContent(string Title, string? Summary, string BodyMarkdown, IReadOnlyList<ReleaseNoteChange> Changes);

/// <summary>Human-readable notes for one release. Publication is independent of deployment; every change creates a revision.</summary>
public sealed class ReleaseNote : IApplicationScoped
{
    private ReleaseNote() { } // EF Core

    public Guid Id { get; private set; }
    public AppId AppId { get; private set; }
    public ReleaseId ReleaseId { get; private set; }
    public ReleaseNoteContent Content { get; private set; } = null!;
    public ReleaseNoteState State { get; private set; }
    public int Revision { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public static ReleaseNote Draft(AppId appId, ReleaseId releaseId, ReleaseNoteContent content, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        AppId = appId,
        ReleaseId = releaseId,
        Content = content,
        State = ReleaseNoteState.Draft,
        Revision = 1,
        UpdatedAt = now,
    };

    public bool IsPublished => State == ReleaseNoteState.Published;

    public void Edit(ReleaseNoteContent content, DateTimeOffset now)
    {
        Content = content;
        Touch(now);
    }

    public void Publish(DateTimeOffset now)
    {
        if (IsPublished)
        {
            throw new DomainRuleException("release_note.already_published", "The release note is already published.");
        }
        State = ReleaseNoteState.Published;
        PublishedAt = now;
        Touch(now);
    }

    public void Unpublish(DateTimeOffset now)
    {
        if (!IsPublished)
        {
            throw new DomainRuleException("release_note.not_published", "The release note is not published.");
        }
        State = ReleaseNoteState.Draft;
        Touch(now);
    }

    /// <summary>Markdown rendering used for updater metadata: summary, body and categorized changes.</summary>
    public string ToMarkdown()
    {
        var sections = new List<string>();
        if (!string.IsNullOrWhiteSpace(Content.Summary))
        {
            sections.Add(Content.Summary.Trim());
        }
        if (!string.IsNullOrWhiteSpace(Content.BodyMarkdown))
        {
            sections.Add(Content.BodyMarkdown.Trim());
        }
        sections.AddRange(Content.Changes
            .GroupBy(change => change.Category)
            .OrderBy(group => group.Key)
            .Select(group => $"### {group.Key}\n" + string.Join('\n', group.Select(change => $"- {change.Text}"))));
        return string.Join("\n\n", sections);
    }

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        UpdatedAt = now;
    }
}

using FluentValidation;
using Releaser.Domain.Common;
using Releaser.Domain.Releases;
using Releaser.Domain.Targeting;
using Releaser.Electron;
using Releaser.Server.Features.Applications;

namespace Releaser.Server.Features.Releases;

public sealed record ManifestReference(PlatformTarget Platform, string Url);

public sealed record RegisterReleaseRequest(string Version, string? Title, IReadOnlyList<string> Channels, IReadOnlyList<ManifestReference> Manifests);

public sealed record UpdateReleaseRequest(string? Title);

public sealed record AssignChannelsRequest(IReadOnlyList<string> Channels);

public sealed record PreviewManifestRequest(string Url);

public sealed record ManifestFileResponse(string? Url, string? Sha512, long? Size);

public sealed record ManifestPreviewResponse(string Version, string SourceSha256, IReadOnlyList<ManifestFileResponse> Files, string RewrittenYaml);

public sealed record ReleaseManifestResponse(PlatformTarget Platform, string FeedFileName, string SourceUrl, string SourceSha256, DateTimeOffset FetchedAt, IReadOnlyList<ManifestFileResponse> Files)
{
    public static ReleaseManifestResponse From(ReleaseManifest manifest) =>
        new(manifest.Platform, Electron.FeedFileName.For(manifest.Platform), manifest.SourceUrl, manifest.SourceSha256, manifest.FetchedAt,
            [.. ElectronManifest.Parse(manifest.Content).Files.Select(f => new ManifestFileResponse(f.Url, f.Sha512, f.Size))]);
}

public sealed record ReleaseResponse(
    Guid Id, string Version, string? Title, ReleaseState State, IReadOnlyList<string> Channels, IReadOnlyList<PlatformTarget> Platforms,
    DateTimeOffset RegisteredAt, bool HasPublishedNotes, IReadOnlyList<ReleaseManifestResponse> Manifests)
{
    public static ReleaseResponse From(Release release, IEnumerable<ReleaseManifest> manifests, bool hasPublishedNotes) =>
        new(release.Id.Value, release.Version.Value, release.Title, release.State, [.. release.Channels.Select(c => c.Value)],
            release.Platforms, release.RegisteredAt, hasPublishedNotes, [.. manifests.OrderBy(m => m.Platform).Select(ReleaseManifestResponse.From)]);
}

internal sealed class RegisterReleaseValidator : AbstractValidator<RegisterReleaseRequest>
{
    public RegisterReleaseValidator()
    {
        RuleFor(r => r.Version).Must(v => SemanticVersion.TryParse(v, out _)).WithMessage("Use a semantic version such as 1.2.0.");
        RuleFor(r => r.Title).MaximumLength(200);
        RuleForEach(r => r.Channels).MustBeChannelKey();
        RuleFor(r => r.Manifests).NotEmpty().Must(m => m.Count <= Enum.GetValues<PlatformTarget>().Length)
            .Must(m => m.Select(x => x.Platform).Distinct().Count() == m.Count).WithMessage("Register at most one manifest per platform.");
        RuleForEach(r => r.Manifests).SetValidator(new ManifestReferenceValidator());
    }
}

internal sealed class ManifestReferenceValidator : AbstractValidator<ManifestReference>
{
    public ManifestReferenceValidator()
    {
        RuleFor(m => m.Platform).IsInEnum();
        RuleFor(m => m.Url).MustBeAbsoluteHttpUrl();
    }
}

internal sealed class AssignChannelsValidator : AbstractValidator<AssignChannelsRequest>
{
    public AssignChannelsValidator()
    {
        RuleFor(r => r.Channels).NotNull();
        RuleForEach(r => r.Channels).MustBeChannelKey();
    }
}

internal sealed class UpdateReleaseValidator : AbstractValidator<UpdateReleaseRequest>
{
    public UpdateReleaseValidator() => RuleFor(r => r.Title).MaximumLength(200);
}

internal sealed class PreviewManifestValidator : AbstractValidator<PreviewManifestRequest>
{
    public PreviewManifestValidator() => RuleFor(r => r.Url).MustBeAbsoluteHttpUrl();
}

internal static class UrlValidationRules
{
    public static IRuleBuilderOptions<T, string> MustBeAbsoluteHttpUrl<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(2048)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) && string.IsNullOrEmpty(uri.UserInfo))
            .WithMessage("Use an absolute https:// URL without credentials.");
}

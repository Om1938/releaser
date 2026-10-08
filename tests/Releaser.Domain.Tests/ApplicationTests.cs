using Releaser.Domain.Applications;
using Releaser.Domain.Common;
using Releaser.Domain.Targeting;

namespace Releaser.Domain.Tests;

public sealed class ApplicationTests
{
    private static Application NewApplication(params PlatformTarget[] platforms) =>
        Application.Create(ApplicationKey.From("acme"), "Acme", null, ChannelKey.From("stable"), platforms, DateTimeOffset.UnixEpoch);

    [Fact]
    public void supported_platforms_are_distinct_and_in_standard_order()
    {
        NewApplication(PlatformTarget.MacOS, PlatformTarget.Windows, PlatformTarget.MacOS).SupportedPlatforms
            .ShouldBe([PlatformTarget.Windows, PlatformTarget.MacOS]);
    }

    [Fact]
    public void an_application_must_support_at_least_one_platform()
    {
        Should.Throw<DomainRuleException>(() => NewApplication()).Code.ShouldBe("application.no_platforms");
        Should.Throw<DomainRuleException>(() => NewApplication(PlatformTarget.Windows).ChangeSupportedPlatforms([])).Code.ShouldBe("application.no_platforms");
    }

    [Fact]
    public void manifests_for_unsupported_platforms_are_rejected()
    {
        var application = NewApplication(PlatformTarget.Windows, PlatformTarget.MacOS);
        application.EnsureSupports(PlatformTarget.MacOS);
        Should.Throw<DomainRuleException>(() => application.EnsureSupports(PlatformTarget.LinuxX64)).Code.ShouldBe("release.platform_not_supported");
    }

    [Fact]
    public void supported_platforms_can_change()
    {
        var application = NewApplication(PlatformTarget.Windows);
        application.ChangeSupportedPlatforms([PlatformTarget.LinuxX64, PlatformTarget.Windows]);
        application.SupportedPlatforms.ShouldBe([PlatformTarget.Windows, PlatformTarget.LinuxX64]);
    }
}

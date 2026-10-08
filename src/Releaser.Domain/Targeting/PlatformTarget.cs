namespace Releaser.Domain.Targeting;

/// <summary>Platform/architecture combination an updater asks for. Windows and macOS metadata cover all architectures in one file.</summary>
public enum PlatformTarget
{
    Windows = 1,
    MacOS = 2,
    LinuxX64 = 3,
    LinuxArm64 = 4,
    LinuxArmv7l = 5,
}

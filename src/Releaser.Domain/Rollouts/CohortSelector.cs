using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Releaser.Domain.Targeting;

namespace Releaser.Domain.Rollouts;

/// <summary>Deterministic, monotonic cohort membership (ADR 0007).</summary>
public static class CohortSelector
{
    public static int Bucket(string salt, InstallationId installation)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{installation.Value}"), hash);
        return (int)(BinaryPrimitives.ReadUInt64BigEndian(hash) % RolloutPercentage.BasisPointsScale);
    }

    public static bool Contains(string salt, RolloutPercentage percentage, InstallationId? installation)
    {
        if (percentage.IsFull)
        {
            return true;
        }
        return installation is not null && Bucket(salt, installation) < percentage.BasisPoints;
    }
}

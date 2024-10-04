using Grpc.Health.V1;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace posts;

public class HealthStatusManager
{
    private static readonly HealthStatusManager Instance = new();

    public static HealthStatusManager GetInstance()
    {
        return Instance;
    }

    // public HealthCheckResponse.Types.ServingStatus ServingStatus { get; set; } = HealthCheckResponse.Types.ServingStatus.Unknown;
    public HealthStatus HealthStatus { get; set; } = HealthStatus.Healthy;
}
namespace KsaDevPanel.OrbitEditing;

/// <summary>Immutable body parameters copied from a game-facing adapter.</summary>
public sealed record OrbitBodySnapshot(
    string Id,
    string DisplayName,
    double MuKm3PerSec2,
    double RadiusKm,
    double? MinimumClearanceKm = null,
    double? SoiRadiusKm = null);

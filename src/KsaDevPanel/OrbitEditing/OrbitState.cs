namespace KsaDevPanel.OrbitEditing;

public sealed record OrbitState(
    double SemiMajorAxisKm,
    double Eccentricity,
    double PeriodSeconds,
    double TrueAnomalyDeg,
    OrbitVector3 PositionKm,
    OrbitVector3 VelocityKmPerSec);

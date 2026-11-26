namespace KsaDevPanel.OrbitEditing;

/// <summary>Classical elements with altitudes measured above the selected body's reference radius.</summary>
public sealed record OrbitElements(
    OrbitBodySnapshot Body,
    double PeriapsisAltitudeKm,
    double ApoapsisAltitudeKm,
    double InclinationDeg,
    double LongitudeAscendingNodeDeg,
    double ArgumentOfPeriapsisDeg,
    double TrueAnomalyDeg)
{
    public static OrbitElements FromTimeSincePeriapsis(
        OrbitBodySnapshot body,
        double periapsisAltitudeKm,
        double apoapsisAltitudeKm,
        double inclinationDeg,
        double longitudeAscendingNodeDeg,
        double argumentOfPeriapsisDeg,
        double timeSincePeriapsisSeconds)
    {
        ArgumentNullException.ThrowIfNull(body);
        OrbitModel.ValidateGeometry(body, periapsisAltitudeKm, apoapsisAltitudeKm);
        OrbitModel.RequireFinite(inclinationDeg, nameof(inclinationDeg));
        OrbitModel.RequireFinite(longitudeAscendingNodeDeg, nameof(longitudeAscendingNodeDeg));
        OrbitModel.RequireFinite(argumentOfPeriapsisDeg, nameof(argumentOfPeriapsisDeg));
        OrbitModel.RequireFinite(timeSincePeriapsisSeconds, nameof(timeSincePeriapsisSeconds));
        if (inclinationDeg < 0d || inclinationDeg > 180d)
            throw new ArgumentOutOfRangeException(nameof(inclinationDeg), "Inclination must be between 0 and 180 degrees.");
        var rp = body.RadiusKm + periapsisAltitudeKm;
        var ra = body.RadiusKm + apoapsisAltitudeKm;
        var a = (rp + ra) / 2d;
        var e = (ra - rp) / (ra + rp);
        var period = OrbitModel.TwoPi * Math.Sqrt(a * a * a / body.MuKm3PerSec2);
        if (!double.IsFinite(period) || period <= 0d)
            throw new ArgumentOutOfRangeException(nameof(body), "Orbit dimensions exceed the supported numeric range.");
        var elapsedPeriods = timeSincePeriapsisSeconds % period;
        var meanAnomaly = OrbitModel.NormalizeRadians(OrbitModel.TwoPi * (elapsedPeriods / period));
        var eccentricAnomaly = OrbitModel.SolveEccentricAnomaly(meanAnomaly, e);
        var trueAnomaly = 2d * Math.Atan2(
            Math.Sqrt(1d + e) * Math.Sin(eccentricAnomaly / 2d),
            Math.Sqrt(1d - e) * Math.Cos(eccentricAnomaly / 2d));
        return new OrbitElements(body, periapsisAltitudeKm, apoapsisAltitudeKm,
            inclinationDeg, OrbitModel.NormalizeDegrees(longitudeAscendingNodeDeg), OrbitModel.NormalizeDegrees(argumentOfPeriapsisDeg),
            OrbitModel.NormalizeDegrees(trueAnomaly * 180d / Math.PI));
    }
}

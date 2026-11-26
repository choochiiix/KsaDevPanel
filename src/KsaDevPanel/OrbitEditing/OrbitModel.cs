namespace KsaDevPanel.OrbitEditing;

/// <summary>Deterministic two-body, elliptic orbit calculations in a right-handed inertial frame.</summary>
public static class OrbitModel
{
    internal const double TwoPi = 2d * Math.PI;

    public static OrbitState Calculate(OrbitElements elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        var body = elements.Body ?? throw new ArgumentException("A body is required.", nameof(elements));
        ValidateGeometry(body, elements.PeriapsisAltitudeKm, elements.ApoapsisAltitudeKm);
        RequireFinite(elements.InclinationDeg, nameof(elements.InclinationDeg));
        RequireFinite(elements.LongitudeAscendingNodeDeg, nameof(elements.LongitudeAscendingNodeDeg));
        RequireFinite(elements.ArgumentOfPeriapsisDeg, nameof(elements.ArgumentOfPeriapsisDeg));
        RequireFinite(elements.TrueAnomalyDeg, nameof(elements.TrueAnomalyDeg));
        if (elements.InclinationDeg < 0d || elements.InclinationDeg > 180d)
            throw new ArgumentOutOfRangeException(nameof(elements.InclinationDeg), "Inclination must be between 0 and 180 degrees.");

        var rp = body.RadiusKm + elements.PeriapsisAltitudeKm;
        var ra = body.RadiusKm + elements.ApoapsisAltitudeKm;
        var a = (rp + ra) / 2d;
        var e = (ra - rp) / (ra + rp);
        if (e < 0d || e >= 1d)
            throw new ArgumentException("The orbit must be an ellipse with periapsis no greater than apoapsis.", nameof(elements));

        var mu = body.MuKm3PerSec2;
        var period = TwoPi * Math.Sqrt(a * a * a / mu);
        if (!double.IsFinite(period) || period <= 0d)
            throw new ArgumentOutOfRangeException(nameof(elements), "Orbit dimensions exceed the supported numeric range.");
        var nu = NormalizeDegrees(elements.TrueAnomalyDeg) * Math.PI / 180d;
        var p = a * (1d - e * e);
        var radius = p / (1d + e * Math.Cos(nu));
        var sqrtMuOverP = Math.Sqrt(mu / p);
        var xPerifocal = radius * Math.Cos(nu);
        var yPerifocal = radius * Math.Sin(nu);
        var vxPerifocal = -sqrtMuOverP * Math.Sin(nu);
        var vyPerifocal = sqrtMuOverP * (e + Math.Cos(nu));

        var inc = DegreesToRadians(elements.InclinationDeg);
        var node = DegreesToRadians(elements.LongitudeAscendingNodeDeg);
        var arg = DegreesToRadians(elements.ArgumentOfPeriapsisDeg);
        var cosO = Math.Cos(node); var sinO = Math.Sin(node);
        var cosI = Math.Cos(inc); var sinI = Math.Sin(inc);
        var cosW = Math.Cos(arg); var sinW = Math.Sin(arg);
        var m11 = cosO * cosW - sinO * sinW * cosI;
        var m12 = -cosO * sinW - sinO * cosW * cosI;
        var m21 = sinO * cosW + cosO * sinW * cosI;
        var m22 = -sinO * sinW + cosO * cosW * cosI;
        var m31 = sinW * sinI;
        var m32 = cosW * sinI;
        var position = new OrbitVector3(m11 * xPerifocal + m12 * yPerifocal,
            m21 * xPerifocal + m22 * yPerifocal, m31 * xPerifocal + m32 * yPerifocal);
        var velocity = new OrbitVector3(m11 * vxPerifocal + m12 * vyPerifocal,
            m21 * vxPerifocal + m22 * vyPerifocal, m31 * vxPerifocal + m32 * vyPerifocal);
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y) || !double.IsFinite(position.Z) ||
            !double.IsFinite(velocity.X) || !double.IsFinite(velocity.Y) || !double.IsFinite(velocity.Z))
            throw new ArgumentOutOfRangeException(nameof(elements), "Orbit state exceeds the supported numeric range.");

        return new OrbitState(a, e, period, NormalizeDegrees(nu * 180d / Math.PI), position, velocity);
    }

    internal static void ValidateGeometry(OrbitBodySnapshot body, double periapsisAltitudeKm, double apoapsisAltitudeKm)
    {
        ArgumentNullException.ThrowIfNull(body);
        RequireFinite(body.RadiusKm, nameof(body.RadiusKm));
        RequireFinite(body.MuKm3PerSec2, nameof(body.MuKm3PerSec2));
        if (body.RadiusKm < 0d) throw new ArgumentOutOfRangeException(nameof(body.RadiusKm), "Body radius cannot be negative.");
        if (body.MuKm3PerSec2 <= 0d) throw new ArgumentOutOfRangeException(nameof(body.MuKm3PerSec2), "Gravitational parameter must be positive.");
        RequireFinite(periapsisAltitudeKm, nameof(periapsisAltitudeKm));
        RequireFinite(apoapsisAltitudeKm, nameof(apoapsisAltitudeKm));
        if (body.MinimumClearanceKm is { } clearance)
        {
            RequireFinite(clearance, nameof(body.MinimumClearanceKm));
            if (clearance < 0d) throw new ArgumentOutOfRangeException(nameof(body.MinimumClearanceKm));
            if (periapsisAltitudeKm < clearance) throw new ArgumentOutOfRangeException(nameof(periapsisAltitudeKm), "Periapsis is below the required surface clearance.");
        }
        if (periapsisAltitudeKm < 0d) throw new ArgumentOutOfRangeException(nameof(periapsisAltitudeKm), "Orbit intersects the body.");
        if (body.RadiusKm + periapsisAltitudeKm <= 0d)
            throw new ArgumentOutOfRangeException(nameof(periapsisAltitudeKm), "Periapsis radius must be positive.");
        if (apoapsisAltitudeKm < periapsisAltitudeKm) throw new ArgumentOutOfRangeException(nameof(apoapsisAltitudeKm), "Apoapsis must be at least periapsis.");
        if (body.SoiRadiusKm is { } soi)
        {
            RequireFinite(soi, nameof(body.SoiRadiusKm));
            if (soi <= body.RadiusKm) throw new ArgumentOutOfRangeException(nameof(body.SoiRadiusKm));
            if (body.RadiusKm + apoapsisAltitudeKm >= soi) throw new ArgumentOutOfRangeException(nameof(apoapsisAltitudeKm), "Apoapsis must be strictly inside the sphere of influence.");
        }
    }

    internal static double SolveEccentricAnomaly(double meanAnomaly, double eccentricity)
    {
        var estimate = eccentricity < 0.8d ? meanAnomaly : Math.PI;
        for (var i = 0; i < 64; i++)
        {
            var delta = (estimate - eccentricity * Math.Sin(estimate) - meanAnomaly) / (1d - eccentricity * Math.Cos(estimate));
            estimate -= delta;
            if (Math.Abs(delta) < 1e-13d) return estimate;
        }
        throw new ArithmeticException("Kepler equation did not converge.");
    }

    internal static double NormalizeRadians(double value)
    {
        value %= TwoPi;
        return value < 0d ? value + TwoPi : value;
    }

    internal static double NormalizeDegrees(double value)
    {
        value %= 360d;
        return value < 0d ? value + 360d : value;
    }

    internal static double DegreesToRadians(double value) => NormalizeDegrees(value) * Math.PI / 180d;
    internal static void RequireFinite(double value, string name)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(name, "Value must be finite.");
    }
}

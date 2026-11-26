using KsaDevPanel.OrbitEditing;

var cases = new (string Name, Action Run)[]
{
    ("circular orbit derives period and circular state", CircularOrbit),
    ("elliptical state reaches periapsis and apoapsis radii", EllipticalRadii),
    ("orientation rotates the inertial position", Orientation),
    ("time since periapsis maps one period to periapsis", TimePhase),
    ("negative time wraps deterministically", NegativeTime),
    ("surface clearance is enforced", Clearance),
    ("SOI bound is enforced", Soi),
    ("SOI boundary equality is rejected", SoiEquality),
    ("zero-radius gravitating barycenter is supported", Barycenter),
    ("inclination outside classical bounds is rejected", InclinationBounds),
    ("non-finite values are rejected", NonFinite),
    ("descending periapsis is rejected", DescendingRadii),
};
var failures = new List<string>();
foreach (var (name, run) in cases)
{
    try { run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"FAIL {name}: {ex.Message}"); }
}
foreach (var failure in failures) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;

static OrbitBodySnapshot Body(double clearance = 0, double? soi = null) => new("test", "Test", 398600.4418, 6378.137, clearance, soi);
static OrbitElements Elements(double rp = 300, double ra = 300, double i = 0, double node = 0, double arg = 0, double nu = 0, OrbitBodySnapshot? body = null) =>
    new(body ?? Body(), rp, ra, i, node, arg, nu);
static void CircularOrbit()
{
    var state = OrbitModel.Calculate(Elements());
    var a = 6678.137;
    Near(state.SemiMajorAxisKm, a, 1e-9);
    Near(state.Eccentricity, 0, 1e-12);
    Near(state.PeriodSeconds, 2 * Math.PI * Math.Sqrt(a * a * a / 398600.4418), 1e-9);
    Near(state.PositionKm.X, a, 1e-9);
    Near(state.PositionKm.Y, 0, 1e-9);
    Near(state.VelocityKmPerSec.Y, Math.Sqrt(398600.4418 / a), 1e-12);
}
static void EllipticalRadii()
{
    var state = OrbitModel.Calculate(Elements(200, 1200));
    Near(Norm(state.PositionKm), 6578.137, 1e-8);
    var apo = OrbitModel.Calculate(Elements(200, 1200, nu: 180));
    Near(Norm(apo.PositionKm), 7578.137, 1e-8);
}
static void Orientation()
{
    var state = OrbitModel.Calculate(Elements(node: 90));
    Near(state.PositionKm.X, 0, 1e-9);
    Near(state.PositionKm.Y, 6678.137, 1e-9);
}
static void TimePhase()
{
    var fromTime = OrbitElements.FromTimeSincePeriapsis(Body(), 200, 1200, 0, 0, 0,
        OrbitModel.Calculate(Elements(200, 1200)).PeriodSeconds);
    Near(fromTime.TrueAnomalyDeg, 0, 1e-9);
    Near(OrbitModel.Calculate(fromTime).PositionKm.X, 6578.137, 1e-8);
}
static void NegativeTime()
{
    var period = OrbitModel.Calculate(Elements()).PeriodSeconds;
    var a = OrbitElements.FromTimeSincePeriapsis(Body(), 300, 300, 0, 0, 0, -period / 4);
    var b = OrbitElements.FromTimeSincePeriapsis(Body(), 300, 300, 0, 0, 0, 3 * period / 4);
    Near(a.TrueAnomalyDeg, b.TrueAnomalyDeg, 1e-10);
}
static void Clearance() => Throws<ArgumentOutOfRangeException>(() => OrbitModel.Calculate(Elements(100, 200, body: Body(150))));
static void Soi() => Throws<ArgumentOutOfRangeException>(() => OrbitModel.Calculate(Elements(200, 500, body: Body(0, 6800))));
static void SoiEquality() => Throws<ArgumentOutOfRangeException>(() => OrbitModel.Calculate(new OrbitElements(new OrbitBodySnapshot("x", "x", 1, 0, null, 20), 10, 20, 0, 0, 0, 0)));
static void Barycenter()
{
    var state = OrbitModel.Calculate(new OrbitElements(new OrbitBodySnapshot("bary", "Barycenter", 1000, 0), 10, 10, 0, 0, 0, 0));
    Near(state.PositionKm.X, 10, 1e-10);
}
static void InclinationBounds() => Throws<ArgumentOutOfRangeException>(() => OrbitModel.Calculate(Elements(i: 181)));
static void NonFinite() => Throws<ArgumentOutOfRangeException>(() => OrbitModel.Calculate(Elements(nu: double.NaN)));
static void DescendingRadii() => Throws<ArgumentOutOfRangeException>(() => OrbitModel.Calculate(Elements(500, 200)));
static double Norm(OrbitVector3 v) => Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
static void Near(double actual, double expected, double tolerance)
{
    if (Math.Abs(actual - expected) > tolerance) throw new InvalidOperationException($"expected {expected:R}, got {actual:R}");
}
static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"expected {typeof(T).Name}");
}

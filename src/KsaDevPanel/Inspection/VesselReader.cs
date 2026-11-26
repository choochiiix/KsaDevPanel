namespace KsaDevPanel.Inspection;

/// <summary>
/// The only input boundary for vessel inspection. Implementations must copy
/// primitive values while executing on the host-owned draw callback and must
/// not return a wrapper around a live KSA object.
/// </summary>
public interface IVesselSource
{
    VesselReadData? ReadActiveVessel();
}

/// <summary>Converts one host sample into an immutable snapshot.</summary>
public sealed class VesselReader
{
    private readonly IVesselSource _source;

    public VesselReader(IVesselSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>
    /// Call only from the verified host draw phase. No returned snapshot keeps
    /// the source or any game-owned object alive.
    /// </summary>
    public VesselSnapshot Read()
    {
        try
        {
            var data = _source.ReadActiveVessel();
            return data is null
                ? VesselSnapshot.Unavailable(VesselSnapshotAvailability.NoActiveVessel, "No active vessel")
                : VesselSnapshot.From(data);
        }
        catch (VesselWorldTransitionException ex)
        {
            return VesselSnapshot.Unavailable(VesselSnapshotAvailability.WorldTransition, ex.Message);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return VesselSnapshot.Unavailable(VesselSnapshotAvailability.Unavailable, ex.Message);
        }
    }
}

/// <summary>Adapter signal for a world replacement or scene transition.</summary>
public sealed class VesselWorldTransitionException : Exception
{
    public VesselWorldTransitionException(string message) : base(message) { }
}

using System.Diagnostics;
using System.Reflection;
using KsaDevPanel.Diagnostics;
using KsaDevPanel.Inspection;
using KsaDevPanel.UI;
using StarMap.API;

namespace KsaDevPanel;

[StarMapMod]
public sealed class DevPanelMod
{
    internal const string GameBuild = "2026.11.25.5949";
    private const string GameProduct = GameBuild + "+64c3726bd24373931e550030e4ba49c83fb46ed8";
    private const string ApiProduct = "0.5.4+477f495d1c8d59910ad7f37071b77535f59f8c5d";
    private static int started;
    private readonly SessionDiagnostics diagnostics = new();
    private readonly VesselReader vesselReader = new(new KsaVesselSource());
    private Workbench? workbench;

    [StarMapBeforeMain]
    public void Start()
    {
        if (Interlocked.Exchange(ref started, 1) != 0)
            return;

        if (ProductVersion(typeof(KSA.Mod).Assembly) != GameProduct ||
            ProductVersion(typeof(StarMapModAttribute).Assembly) != ApiProduct)
        {
            Console.WriteLine("[KsaDevPanel] disabled: host build or StarMap API mismatch");
            return;
        }

        if (!IsolatedProfile.TryGetSettingsPath(out var settingsPath))
        {
            Console.WriteLine("[KsaDevPanel] disabled: isolated profile could not be verified");
            return;
        }

        var version = typeof(DevPanelMod).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion.Split('+', 2)[0];
        diagnostics.MarkStartup(version, GameBuild);
        workbench = new Workbench(settingsPath);
    }

    [StarMapAfterGui]
    public void Draw(double dt)
    {
        if (workbench is null)
            return;

        diagnostics.MarkFirstGui();
        var scene = KSA.Program.IsEditorOpen ? SessionScene.Editor
            : KSA.Program.IsLoaded ? SessionScene.World : SessionScene.Loading;
        diagnostics.ObserveScene(scene);
        var snapshot = scene == SessionScene.World ? vesselReader.Read() : null;
        workbench.Draw(diagnostics, snapshot);
    }

    [StarMapUnload]
    public void Unload()
    {
        workbench?.Dispose();
        workbench = null;
    }

    private static string? ProductVersion(Assembly assembly)
    {
        return FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
    }
}

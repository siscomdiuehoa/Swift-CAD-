#if SWIFTCAD_SHOTS
namespace SwiftCad.App.Pages;

public partial class HomePage
{
    internal void ShotOpenPicker() => _ = PickAsync();
    internal void ShotOpenCloud(string provider) => _ = CloudAsync(provider);
}
#endif

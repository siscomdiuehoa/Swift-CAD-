namespace SwiftCad.App.Pages;

public class LicensesPage : ContentPage
{
    public LicensesPage()
    {
        Title = L.T("settings_licenses");
        BackgroundColor = Theme.Bg;
        SafeAreaEdges = SafeAreaEdges.All;
        var body = Ui.Text(L.T("loading"), Theme.FontSub);
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = Theme.Margin, Children = { body } } };
        Load(body);
    }

    private static async void Load(Label body)
    {
        try
        {
            await using var s = await FileSystem.OpenAppPackageFileAsync("THIRD-PARTY-NOTICES.txt");
            using var r = new StreamReader(s);
            body.Text = await r.ReadToEndAsync();
        }
        catch (Exception)
        {
            body.Text = L.T("error_generic");
        }
    }
}

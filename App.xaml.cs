using Microsoft.Maui.Controls;

namespace pokeapi2;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();
    }
}
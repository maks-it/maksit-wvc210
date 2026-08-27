using Avalonia.Controls;
using Avalonia.Input;
using MaksIT.Wvc210.UI.ViewModels;

namespace MaksIT.Wvc210.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                vm.Shutdown();
        };
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;
        if (e.Source is TextBox or ComboBox or NumericUpDown)
            return;

        var direction = e.Key switch
        {
            Key.Up or Key.W => "U",
            Key.Down or Key.S => "D",
            Key.Left or Key.A => "L",
            Key.Right or Key.D => "R",
            Key.Home or Key.H => "H",
            _ => null
        };

        if (direction is null)
            return;

        e.Handled = true;
        if (direction == "H")
            await vm.Live.HomeCommand.ExecuteAsync(null);
        else
            await vm.HandlePtzKeyAsync(direction);
    }
}

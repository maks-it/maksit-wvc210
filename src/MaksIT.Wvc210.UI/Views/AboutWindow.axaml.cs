using Avalonia.Controls;
using Avalonia.Interactivity;
using MaksIT.Wvc210.UI.ViewModels;


namespace MaksIT.Wvc210.UI.Views;

public partial class AboutWindow : Window {
  public AboutWindow() {
    InitializeComponent();
    DataContext ??= new AboutViewModel();
  }

  private void OnClose(object? sender, RoutedEventArgs e) =>
    Close();
}

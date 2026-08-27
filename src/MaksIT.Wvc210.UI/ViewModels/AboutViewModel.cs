using CommunityToolkit.Mvvm.Input;
using MaksIT.Wvc210.Client;
using MaksIT.Wvc210.Shared;


namespace MaksIT.Wvc210.UI.ViewModels;

public partial class AboutViewModel : ViewModelBase {
  public string ProductName => AppInfo.ProductName;
  public string Brand => AppInfo.Brand;
  public string Version { get; } = AppInfo.ReadVersion();
  public string Credits => AppInfo.Credits;
  public string Email => AppInfo.Email;
  public string Copyright => AppInfo.Copyright;
  public string License => AppInfo.License;
  public string Summary => AppInfo.Summary;

  [RelayCommand]
  private void OpenEmail() =>
    ExternalApps.OpenUrl(AppInfo.EmailUri);

  [RelayCommand]
  private void OpenSite() =>
    ExternalApps.OpenUrl(AppInfo.SiteUri);
}

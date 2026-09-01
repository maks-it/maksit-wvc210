using System.Reflection;


namespace MaksIT.Wvc210.Shared;

public static class AppInfo {
  public const string Brand = "MaksIT";
  public const string ProductName = "MaksIT.Wvc210";
  public const string Credits = "Maksym Sadovnychyy";
  public const string Email = "maksym.sadovnychyy@gmail.com";
  public const string EmailUri = "mailto:maksym.sadovnychyy@gmail.com";
  public const string SiteUri = "https://maks-it.com";
  public static string Copyright =>
    $"Copyright {DateTime.UtcNow.Year} Maksym Sadovnychyy (MAKS-IT)";
  public const string License = "Apache License 2.0";
  public const string Summary = "Desktop operator UI for a Cisco WVC210 camera.";

  public static string ReadVersion(Assembly? assembly = null) {
    var asm = assembly ?? Assembly.GetEntryAssembly() ?? typeof(AppInfo).Assembly;
    var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    if (!string.IsNullOrWhiteSpace(info)) {
      var plus = info.IndexOf('+');
      return plus > 0 ? info[..plus] : info;
    }

    var version = asm.GetName().Version;
    if (version is null)
      return "1.2.1";
    return $"{version.Major}.{version.Minor}.{version.Build}";
  }
}

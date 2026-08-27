using MaksIT.Wvc210.Shared;


namespace MaksIT.Wvc210.Tests;

public class AppInfoTests {
  [Fact]
  public void Credits_and_email_are_maksit() {
    Assert.Equal("MaksIT", AppInfo.Brand);
    Assert.Equal("Maksym Sadovnychyy", AppInfo.Credits);
    Assert.Equal("maksym.sadovnychyy@gmail.com", AppInfo.Email);
  }

  [Fact]
  public void Copyright_is_current_year_without_a_range() {
    var year = DateTime.UtcNow.Year.ToString();
    Assert.Contains(year, AppInfo.Copyright);
    Assert.DoesNotContain("–", AppInfo.Copyright);
  }

  [Fact]
  public void ReadVersion_uses_assembly_informational_version() {
    var version = AppInfo.ReadVersion(typeof(AppInfo).Assembly);
    Assert.StartsWith("1.1.0", version);
  }
}

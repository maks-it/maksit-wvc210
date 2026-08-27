using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaksIT.Wvc210.Shared;
using MaksIT.Wvc210.Client;

namespace MaksIT.Wvc210.UI.ViewModels;

public partial class SetupViewModel : ViewModelBase
{
    private readonly CameraClient _client;
    private readonly LocalTalkSettings _talkSettings;

    [ObservableProperty] private GroupDefinition? _selectedGroup;
    [ObservableProperty] private string _status = "Connect to load camera settings.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isUsersPage;
    [ObservableProperty] private bool _loginCheck;
    [ObservableProperty] private decimal _adminTimeout = 5;
    [ObservableProperty] private string _adminName = "admin";
    [ObservableProperty] private string _adminPassword = "";

    public SetupViewModel(CameraClient client, LocalTalkSettings talkSettings)
    {
        _client = client;
        _talkSettings = talkSettings;
        Groups = ConfigCatalog.Groups;
        var sections = new List<SetupSection>();
        var nav = new List<SetupNavItem>();
        foreach (var category in Groups.GroupBy(g => g.Category))
        {
            var items = category.Select(g => new SetupNavItem(g.Title, g)).ToList();
            sections.Add(new SetupSection(category.Key.ToUpperInvariant(), items));
            nav.AddRange(items);
        }

        var users = new SetupNavItem("Users & privileges", isUsers: true);
        sections.Add(new SetupSection("ACCESS", [users]));
        nav.Add(users);
        Sections = sections;
        NavItems = nav;
        SelectedGroup = Groups[0];
        nav[0].IsSelected = true;
        for (var i = 1; i <= 20; i++)
            Users.Add(new UserSlotViewModel(i));
    }

    public IReadOnlyList<GroupDefinition> Groups { get; }
    public IReadOnlyList<SetupSection> Sections { get; }
    public IReadOnlyList<SetupNavItem> NavItems { get; }
    public LocalTalkSettings TalkSettings => _talkSettings;
    public bool IsAudioPage => !IsUsersPage && SelectedGroup?.Id == "AUDIO";
    public ObservableCollection<ConfigFieldViewModel> Fields { get; } = [];
    public ObservableCollection<UserSlotViewModel> Users { get; } = [];
    public string EditorTitle => IsUsersPage ? "Users" : SelectedGroup?.Title ?? "Setup";
    public string EditorDescription => IsUsersPage
        ? "Administrator account, 20 users, and per-user microphone / speaker / PTZ / I/O / admin rights."
        : SelectedGroup?.Description ?? "";

    public async Task LoadSelectedAsync()
    {
        if (!_client.IsConfigured)
            return;
        if (!IsUsersPage && SelectedGroup is null)
            return;

        IsBusy = true;
        try
        {
            if (IsUsersPage)
            {
                await LoadUsersAsync().ConfigureAwait(true);
                Status = "Users loaded.";
                return;
            }

            var group = SelectedGroup;
            if (group is null)
                return;

            var values = await _client.GetGroupAsync(group.Id).ConfigureAwait(true);
            Fields.Clear();
            foreach (var field in group.Fields)
            {
                values.TryGetValue(field.Key, out var value);
                Fields.Add(new ConfigFieldViewModel(field, value ?? ""));
            }

            Status = $"{group.Title} loaded ({Fields.Count} settings).";
        }
        catch (Exception ex)
        {
            Status = "Load failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task ReloadAsync() => LoadSelectedAsync();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!_client.IsConfigured)
            return;
        if (!IsUsersPage && SelectedGroup is null)
            return;

        IsBusy = true;
        try
        {
            string result;
            if (IsUsersPage)
            {
                result = await SaveUsersAsync().ConfigureAwait(true);
            }
            else
            {
                var group = SelectedGroup ?? throw new InvalidOperationException("No group selected.");
                var payload = Fields
                    .Where(f => f.Kind != FieldKind.ReadOnly)
                    .ToDictionary(f => f.Key, f => f.ExportValue(), StringComparer.OrdinalIgnoreCase);
                result = await _client.SetGroupAsync(group.Id, payload).ConfigureAwait(true);
                Status = result.Contains("OK", StringComparison.OrdinalIgnoreCase)
                    ? $"{group.Title} saved."
                    : result.Trim();
                return;
            }

            Status = result.Contains("OK", StringComparison.OrdinalIgnoreCase)
                ? "Users saved."
                : result.Trim();
        }
        catch (Exception ex)
        {
            Status = "Save failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedGroupChanged(GroupDefinition? value)
    {
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(EditorDescription));
        OnPropertyChanged(nameof(IsAudioPage));
        if (value is not null && _client.IsConfigured && !IsUsersPage)
            _ = LoadSelectedAsync();
    }

    partial void OnIsUsersPageChanged(bool value)
    {
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(EditorDescription));
        OnPropertyChanged(nameof(IsAudioPage));
    }

    [RelayCommand]
    private void SelectNav(SetupNavItem? item)
    {
        if (item is null)
            return;

        foreach (var nav in NavItems)
            nav.IsSelected = ReferenceEquals(nav, item);

        if (item.IsUsers)
        {
            OpenUsers();
            return;
        }

        IsUsersPage = false;
        SelectedGroup = item.Group;
    }

    [RelayCommand]
    private void OpenUsers()
    {
        foreach (var nav in NavItems)
            nav.IsSelected = nav.IsUsers;
        SelectedGroup = null;
        IsUsersPage = true;
        if (_client.IsConfigured)
            _ = LoadSelectedAsync();
    }

    private async Task LoadUsersAsync()
    {
        var values = await _client.GetGroupAsync("USER").ConfigureAwait(true);
        LoginCheck = Get(values, "login_check") is "1";
        AdminTimeout = int.TryParse(Get(values, "admin_timeout"), out var t) ? t : 5;
        AdminName = Get(values, "admin_name");
        AdminPassword = Get(values, "admin_password");
        var audioIn = SplitFlags(Get(values, "audio_in_ctrl"));
        var audioOut = SplitFlags(Get(values, "audio_out_ctrl"));
        var pt = SplitFlags(Get(values, "pt_ctrl"));
        var io = SplitFlags(Get(values, "io_ctrl"));
        var adm = SplitFlags(Get(values, "adm_ctrl"));

        for (var i = 0; i < 20; i++)
        {
            var raw = Get(values, "user" + (i + 1));
            var name = raw;
            var password = "";
            var comma = raw.IndexOf(',');
            if (comma >= 0)
            {
                name = raw[..comma];
                password = raw[(comma + 1)..];
            }

            Users[i].Username = name;
            Users[i].Password = password;
            Users[i].AudioIn = Flag(audioIn, i);
            Users[i].AudioOut = Flag(audioOut, i);
            Users[i].PanTilt = Flag(pt, i);
            Users[i].IoControl = Flag(io, i);
            Users[i].Admin = Flag(adm, i);
        }
    }

    private Task<string> SaveUsersAsync()
    {
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["login_check"] = LoginCheck ? "1" : "0",
            ["admin_timeout"] = ((int)AdminTimeout).ToString(),
            ["admin_name"] = AdminName,
            ["admin_password"] = AdminPassword,
            ["audio_in_ctrl"] = JoinFlags(u => u.AudioIn),
            ["audio_out_ctrl"] = JoinFlags(u => u.AudioOut),
            ["pt_ctrl"] = JoinFlags(u => u.PanTilt),
            ["io_ctrl"] = JoinFlags(u => u.IoControl),
            ["adm_ctrl"] = JoinFlags(u => u.Admin)
        };

        for (var i = 0; i < 20; i++)
        {
            var slot = Users[i];
            payload["user" + (i + 1)] = string.IsNullOrWhiteSpace(slot.Username)
                ? ""
                : slot.Username + "," + slot.Password;
        }

        return _client.SetGroupAsync("USER", payload);
    }

    private string JoinFlags(Func<UserSlotViewModel, bool> selector)
        => string.Join(',', Users.Select(u => selector(u) ? "1" : "0"));

    private static string Get(Dictionary<string, string> map, string key)
        => map.TryGetValue(key, out var value) ? value : "";

    private static int[] SplitFlags(string csv)
        => csv.Split(',', StringSplitOptions.None)
            .Select(s => s.Trim() == "1" ? 1 : 0)
            .ToArray();

    private static bool Flag(int[] flags, int index)
        => index < flags.Length && flags[index] == 1;
}

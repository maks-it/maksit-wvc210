using CommunityToolkit.Mvvm.ComponentModel;
using MaksIT.Wvc210.Shared;

namespace MaksIT.Wvc210.UI.ViewModels;

public sealed class SetupSection
{
    public SetupSection(string title, IReadOnlyList<SetupNavItem> items)
    {
        Title = title;
        Items = items;
    }

    public string Title { get; }
    public IReadOnlyList<SetupNavItem> Items { get; }
}

public partial class SetupNavItem : ObservableObject
{
    public SetupNavItem(string title, GroupDefinition? group = null, bool isUsers = false)
    {
        Title = title;
        Group = group;
        IsUsers = isUsers;
    }

    public string Title { get; }
    public GroupDefinition? Group { get; }
    public bool IsUsers { get; }

    [ObservableProperty] private bool _isSelected;
}

public partial class PresetSlotViewModel : ViewModelBase
{
    public PresetSlotViewModel(int index)
    {
        Index = index;
        Key = index.ToString();
    }

    public int Index { get; }
    public string Key { get; }

    [ObservableProperty] private string _name = "";

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Preset {Index}" : Name;

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(DisplayName));
}

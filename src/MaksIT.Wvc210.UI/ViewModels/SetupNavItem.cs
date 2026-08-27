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
    [ObservableProperty] private string _position = "";

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Preset {Index}" : Name;
    public bool HasPosition => PresetOccupancy.IsOccupied(Name, Position);

    public void ApplyFromCamera(string? name, string? position)
    {
        Name = name?.Trim() ?? "";
        Position = position?.Trim() ?? "";
    }

    public void MarkSaved()
    {
        if (string.IsNullOrWhiteSpace(Name))
            Name = $"Preset {Index}";
        OnPropertyChanged(nameof(HasPosition));
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(HasPosition));
    }

    partial void OnPositionChanged(string value) =>
        OnPropertyChanged(nameof(HasPosition));
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace MaksIT.Wvc210.UI.ViewModels;

public partial class UserSlotViewModel : ViewModelBase
{
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private bool _audioIn;
    [ObservableProperty] private bool _audioOut;
    [ObservableProperty] private bool _panTilt;
    [ObservableProperty] private bool _ioControl;
    [ObservableProperty] private bool _admin;

    public UserSlotViewModel(int index)
    {
        Index = index;
    }

    public int Index { get; }
}

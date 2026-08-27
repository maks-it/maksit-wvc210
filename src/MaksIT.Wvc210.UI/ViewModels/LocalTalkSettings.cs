using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaksIT.Wvc210.Shared;
using MaksIT.Wvc210.Client;

namespace MaksIT.Wvc210.UI.ViewModels;

public partial class LocalTalkSettings : ObservableObject
{
    [ObservableProperty] private MicrophoneDevice? _selectedMicrophone;
    [ObservableProperty] private string _status = "";

    public ObservableCollection<MicrophoneDevice> Microphones { get; } = [];

    [RelayCommand]
    public void Refresh()
    {
        var selectedId = SelectedMicrophone?.Id;
        Microphones.Clear();
        try
        {
            foreach (var mic in MicrophoneEnumerator.List())
                Microphones.Add(mic);
        }
        catch (Exception ex)
        {
            Status = "Could not list microphones: " + ex.Message;
            SelectedMicrophone = null;
            return;
        }

        SelectedMicrophone = Microphones.FirstOrDefault(m => m.Id == selectedId)
            ?? Microphones.FirstOrDefault();
        Status = SelectedMicrophone is null
            ? "No local microphone found."
            : $"Using {SelectedMicrophone.Name}.";
    }

    public void Restore(string? preferredId)
    {
        Refresh();
        if (string.IsNullOrWhiteSpace(preferredId))
            return;
        SelectedMicrophone = Microphones.FirstOrDefault(m => m.Id == preferredId)
            ?? SelectedMicrophone;
    }
}

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MaksIT.Wvc210.Client;
using MaksIT.Wvc210.UI.ViewModels;

namespace MaksIT.Wvc210.UI.Views;

public partial class LiveView : UserControl
{
    public LiveView()
    {
        InitializeComponent();
        TalkHoldButton.AddHandler(InputElement.PointerPressedEvent, OnTalkPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        TalkHoldButton.AddHandler(InputElement.PointerReleasedEvent, OnTalkReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        TalkHoldButton.AddHandler(InputElement.PointerCaptureLostEvent, OnTalkCaptureLost, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private async void OnPreviewPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not LiveViewModel vm)
            return;
        if (sender is not Control surface)
            return;
        if (!vm.TryGetPreviewPixelSize(out var bw, out var bh))
            return;

        var point = e.GetPosition(surface);
        var scale = Math.Min(surface.Bounds.Width / bw, surface.Bounds.Height / bh);
        var dw = bw * scale;
        var dh = bh * scale;
        var ox = (surface.Bounds.Width - dw) / 2.0;
        var oy = (surface.Bounds.Height - dh) / 2.0;
        if (point.X < ox || point.Y < oy || point.X > ox + dw || point.Y > oy + dh)
            return;

        var nx = (point.X - ox) / dw;
        var ny = (point.Y - oy) / dh;
        var (x, y) = PtzClickMap.ToCameraOffset(nx, ny, bw, bh);
        await vm.ClickToCenterAsync(x, y);
    }

    private async void OnTalkPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not LiveViewModel vm || !vm.CanTalk)
            return;
        if (sender is Button button)
            e.Pointer.Capture(button);
        e.Handled = true;
        await vm.StartTalkAsync();
    }

    private void OnTalkReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is LiveViewModel vm)
            vm.StopTalk();
    }

    private void OnTalkCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (DataContext is LiveViewModel vm)
            vm.StopTalk();
    }
}

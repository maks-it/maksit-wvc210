using CommunityToolkit.Mvvm.ComponentModel;
using MaksIT.Wvc210.Shared;

namespace MaksIT.Wvc210.UI.ViewModels;

public partial class ConfigFieldViewModel : ViewModelBase
{
    [ObservableProperty] private string _value = "";
    [ObservableProperty] private bool _boolValue;
    [ObservableProperty] private decimal _numberValue;
    [ObservableProperty] private ChoiceOption? _selectedChoice;

    public ConfigFieldViewModel(FieldDefinition definition, string value)
    {
        Definition = definition;
        Apply(value);
    }

    public FieldDefinition Definition { get; }
    public string Key => Definition.Key;
    public string Label => Definition.Label;
    public string? Hint => Definition.Hint;
    public FieldKind Kind => Definition.Kind;
    public bool HasHint => !string.IsNullOrWhiteSpace(Hint);
    public bool IsText => Kind is FieldKind.Text or FieldKind.ReadOnly;
    public bool IsPassword => Kind == FieldKind.Password;
    public bool IsReadOnly => Kind == FieldKind.ReadOnly;
    public bool IsToggle => Kind == FieldKind.Toggle;
    public bool IsInteger => Kind == FieldKind.Integer;
    public bool IsChoice => Kind == FieldKind.Choice;
    public decimal Min => Definition.Min ?? 0;
    public decimal Max => Definition.Max ?? 100;
    public IReadOnlyList<ChoiceOption> Choices => Definition.Choices ?? [];

    public string ExportValue() => Kind switch
    {
        FieldKind.Toggle => BoolValue ? "1" : "0",
        FieldKind.Integer => ((int)NumberValue).ToString(),
        FieldKind.Choice => SelectedChoice?.Value ?? Value,
        _ => Value
    };

    public void Apply(string value)
    {
        Value = value ?? "";
        BoolValue = Value is "1" or "on" or "true";
        if (decimal.TryParse(Value, out var n))
            NumberValue = n;
        else if (Definition.Min is { } min)
            NumberValue = min;
        SelectedChoice = Choices.FirstOrDefault(c => c.Value == Value) ?? Choices.FirstOrDefault();
    }
}

namespace MaksIT.Wvc210.Shared;

public enum FieldKind
{
    Text,
    Password,
    Integer,
    Toggle,
    Choice,
    ReadOnly
}

public sealed record ChoiceOption(string Value, string Label);

public sealed record FieldDefinition(
    string Key,
    string Label,
    FieldKind Kind,
    string? Hint = null,
    int? Min = null,
    int? Max = null,
    IReadOnlyList<ChoiceOption>? Choices = null);

public sealed record GroupDefinition(
    string Id,
    string Title,
    string Category,
    string Description,
    IReadOnlyList<FieldDefinition> Fields);

namespace SecondBrain.Domain.ValueObjects;

public readonly record struct ParaContextName
{
    public ParaContextName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("PARA context name cannot be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

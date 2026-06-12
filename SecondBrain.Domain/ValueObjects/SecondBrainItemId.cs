namespace SecondBrain.Domain.ValueObjects;

public readonly record struct SecondBrainItemId
{
    public SecondBrainItemId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("SecondBrain item ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static SecondBrainItemId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

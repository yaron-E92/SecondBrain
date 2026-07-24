namespace SecondBrain.Domain.ValueObjects;

public readonly record struct TagId
{
    public TagId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Tag ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static TagId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

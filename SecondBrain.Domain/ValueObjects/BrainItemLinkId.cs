namespace SecondBrain.Domain.ValueObjects;

public readonly record struct BrainItemLinkId
{
    public BrainItemLinkId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Brain item link ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static BrainItemLinkId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

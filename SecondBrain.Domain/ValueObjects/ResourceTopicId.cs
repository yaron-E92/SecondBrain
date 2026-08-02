namespace SecondBrain.Domain.ValueObjects;

public readonly record struct ResourceTopicId
{
    public ResourceTopicId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Resource topic ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static ResourceTopicId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

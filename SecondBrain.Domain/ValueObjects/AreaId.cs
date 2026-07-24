namespace SecondBrain.Domain.ValueObjects;

public readonly record struct AreaId
{
    public AreaId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Area ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static AreaId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

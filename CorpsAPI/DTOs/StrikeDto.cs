public class StrikeAdjustByIdDto
{
    public string UserId { get; set; } = default!;
    /// <summary>Defaults to 1 if omitted.</summary>
    public int Amount { get; set; } = 1;
}

public class StrikeSetByIdDto
{
    public string UserId { get; set; } = default!;
    /// <summary>Target strike count (0+).</summary>
    public int Count { get; set; }
}

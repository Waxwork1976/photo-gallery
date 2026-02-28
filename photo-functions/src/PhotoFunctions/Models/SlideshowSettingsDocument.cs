namespace PhotoFunctions.Models;

public sealed class SlideshowSettingsDocument
{
    public int PhotoCount { get; set; } = 8;
    public int IntervalSeconds { get; set; } = 4;
    public double TransitionSeconds { get; set; } = 0.8;
}

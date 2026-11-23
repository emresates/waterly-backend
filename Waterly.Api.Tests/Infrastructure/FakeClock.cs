namespace Waterly.Api.Tests.Infrastructure;

// Lets a test decide what "now" is, e.g. a few seconds before midnight in Istanbul.
public sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

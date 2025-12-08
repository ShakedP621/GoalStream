namespace Highlights.Api.Entities;

// Tiny helper so we don't sprinkle magic strings all over the codebase.
public static class HighlightStatus
{
    public const string PendingAi = "PENDING_AI";
    public const string Ready = "READY";
    public const string FailedAi = "FAILED_AI";
}

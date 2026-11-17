namespace TaskHub.Core;

public static class QueueKeys
{
    public const string Jobs = "jobs";
    public const string Processing = "processing";
    public const string Started = "started";
    public const string Attempts = "attempts";
    public const string Delayed = "delayed";
    public const string Dead = "dead";
    public const string HandledPrefix = "handled:";
}
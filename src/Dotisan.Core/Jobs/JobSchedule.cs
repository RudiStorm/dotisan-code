namespace Dotisan.Core.Jobs;

public sealed record JobSchedule
{
    public JobSchedule(string name, string messageType, TimeSpan interval, bool enabled, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A schedule name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(messageType))
            throw new ArgumentException("A message type is required.", nameof(messageType));
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "A schedule interval must be positive.");
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("A source path is required.", nameof(sourcePath));

        Name = name;
        MessageType = messageType;
        Interval = interval;
        Enabled = enabled;
        SourcePath = sourcePath;
    }

    public string Name { get; }
    public string MessageType { get; }
    public TimeSpan Interval { get; }
    public bool Enabled { get; }
    public string SourcePath { get; }
}

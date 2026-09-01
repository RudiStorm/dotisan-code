using System.Globalization;

namespace Dotisan.Core.Jobs;

public static class JobScheduleReader
{
    private const string Marker = "// DOTISAN:SCHEDULE ";

    public static IReadOnlyList<JobSchedule> Read(string sourcePath)
    {
        if (!File.Exists(sourcePath))
            return [];

        var schedules = new List<JobSchedule>();
        foreach (var line in File.ReadLines(sourcePath))
        {
            if (!line.TrimStart().StartsWith(Marker, StringComparison.Ordinal))
                continue;

            var fields = line.Trim()[Marker.Length..].Split('|', StringSplitOptions.None);
            if (fields.Length != 4
                || !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
                || !bool.TryParse(fields[3], out var enabled))
                continue;

            try
            {
                schedules.Add(new JobSchedule(fields[0], fields[1], TimeSpan.FromSeconds(seconds), enabled, sourcePath));
            }
            catch (ArgumentException)
            {
            }
        }

        return schedules;
    }
}

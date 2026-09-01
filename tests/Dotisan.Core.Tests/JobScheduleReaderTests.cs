using Dotisan.Core.Jobs;

namespace Dotisan.Core.Tests;

public sealed class JobScheduleReaderTests
{
    [Fact]
    public void Reads_explicit_schedule_markers_from_generated_source()
    {
        var path = Path.Combine(Path.GetTempPath(), "dotisan-schedule-" + Guid.NewGuid().ToString("N") + ".cs");
        File.WriteAllText(path, "// DOTISAN:SCHEDULE sample|SampleJob|300|true\n// other comment\n");
        try
        {
            var schedules = JobScheduleReader.Read(path);

            var schedule = Assert.Single(schedules);
            Assert.Equal("sample", schedule.Name);
            Assert.Equal("SampleJob", schedule.MessageType);
            Assert.Equal(TimeSpan.FromSeconds(300), schedule.Interval);
            Assert.True(schedule.Enabled);
            Assert.Equal(path, schedule.SourcePath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Ignores_malformed_schedule_markers_and_missing_files()
    {
        var path = Path.Combine(Path.GetTempPath(), "dotisan-schedule-" + Guid.NewGuid().ToString("N") + ".cs");
        File.WriteAllText(path, "// DOTISAN:SCHEDULE malformed\n// DOTISAN:SCHEDULE also|SampleJob|not-a-number|true\n");
        try
        {
            Assert.Empty(JobScheduleReader.Read(path));
            Assert.Empty(JobScheduleReader.Read(path + ".missing"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

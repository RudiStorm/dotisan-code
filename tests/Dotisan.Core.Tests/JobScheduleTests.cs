using Dotisan.Core.Jobs;

namespace Dotisan.Core.Tests;

public sealed class JobScheduleTests
{
    [Fact]
    public void Schedule_requires_a_name_message_and_positive_interval()
    {
        var schedule = new JobSchedule("sample", "SampleJob", TimeSpan.FromMinutes(5), true, "Jobs/JobRegistration.cs");

        Assert.Equal("sample", schedule.Name);
        Assert.Equal("SampleJob", schedule.MessageType);
        Assert.Equal(TimeSpan.FromMinutes(5), schedule.Interval);
        Assert.True(schedule.Enabled);
    }

    [Fact]
    public void Invalid_schedule_values_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new JobSchedule("", "SampleJob", TimeSpan.FromMinutes(5), true, "Jobs.cs"));
        Assert.Throws<ArgumentException>(() => new JobSchedule("sample", "", TimeSpan.FromMinutes(5), true, "Jobs.cs"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JobSchedule("sample", "SampleJob", TimeSpan.Zero, true, "Jobs.cs"));
    }
}

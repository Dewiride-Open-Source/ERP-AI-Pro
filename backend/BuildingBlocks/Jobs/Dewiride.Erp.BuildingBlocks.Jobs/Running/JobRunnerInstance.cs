using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Running;

// Names the runner that holds a lease, as <machine>:<process id>, on the run and its events. It only informs: the lease token,
// not the name, decides who owns an attempt.
internal sealed class JobRunnerInstance
{
    public JobRunnerInstance(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, JobRun.LeaseHolderMaxLength, nameof(name));

        Name = name;
    }

    public string Name { get; }

    public static JobRunnerInstance ForThisProcess()
    {
        var process = ":" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        var machine = Environment.MachineName;
        var room = JobRun.LeaseHolderMaxLength - process.Length;

        return new JobRunnerInstance((machine.Length > room ? machine[..room] : machine) + process);
    }
}

using System.Runtime.InteropServices;
using Dewiride.Erp.Host.Migrator;

using var cancellation = new CancellationTokenSource();
using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, Cancel);
using var terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Cancel);

return await MigratorApplication.RunAsync(args, configure: null, cancellation.Token);

void Cancel(PosixSignalContext context)
{
    context.Cancel = true;
    cancellation.Cancel();
}

namespace Dewiride.Erp.Host.Migrator;

internal static class MigratorExitCodes
{
    public const int Succeeded = 0;

    public const int Failed = 1;

    public const int InvalidConfiguration = 2;

    public const int MigrationsPending = 3;

    public const int Usage = 64;
}

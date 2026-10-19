-- Only the Microsoft Entra admin may create users from Microsoft Entra ID, so scripts/azure/database.sh runs this
-- as that admin, with the sqlcmd variables RuntimeUser, RuntimeAppId, MigratorUser, MigratorAppId, AuditRole and DryRun.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @dryRun bit = $(DryRun);

IF DATABASE_PRINCIPAL_ID(N'$(RuntimeUser)') IS NULL
BEGIN
    PRINT N'change: create the user [$(RuntimeUser)] from Microsoft Entra ID';
    IF @dryRun = 0 CREATE USER [$(RuntimeUser)] FROM EXTERNAL PROVIDER;
END;

IF DATABASE_PRINCIPAL_ID(N'$(MigratorUser)') IS NULL
BEGIN
    PRINT N'change: create the user [$(MigratorUser)] from Microsoft Entra ID';
    IF @dryRun = 0 CREATE USER [$(MigratorUser)] FROM EXTERNAL PROVIDER;
END;

-- The SID of a service principal's user is its application (client) id, so a user left behind by a deleted
-- registration of the same name is told apart from the current one.
IF EXISTS (SELECT 1 FROM sys.database_principals
           WHERE name = N'$(RuntimeUser)'
             AND (type <> 'E' OR CAST(CAST(sid AS varbinary(16)) AS uniqueidentifier) <> '$(RuntimeAppId)'))
    THROW 50001, N'The user [$(RuntimeUser)] is not the service principal with client id $(RuntimeAppId); drop it and run database.sh again.', 1;

IF EXISTS (SELECT 1 FROM sys.database_principals
           WHERE name = N'$(MigratorUser)'
             AND (type <> 'E' OR CAST(CAST(sid AS varbinary(16)) AS uniqueidentifier) <> '$(MigratorAppId)'))
    THROW 50002, N'The user [$(MigratorUser)] is not the service principal with client id $(MigratorAppId); drop it and run database.sh again.', 1;

IF ISNULL(IS_ROLEMEMBER(N'db_datareader', N'$(RuntimeUser)'), 0) = 0
BEGIN
    PRINT N'change: add [$(RuntimeUser)] to db_datareader';
    IF @dryRun = 0 ALTER ROLE db_datareader ADD MEMBER [$(RuntimeUser)];
END;

IF ISNULL(IS_ROLEMEMBER(N'db_datawriter', N'$(RuntimeUser)'), 0) = 0
BEGIN
    PRINT N'change: add [$(RuntimeUser)] to db_datawriter';
    IF @dryRun = 0 ALTER ROLE db_datawriter ADD MEMBER [$(RuntimeUser)];
END;

IF ISNULL(IS_ROLEMEMBER(N'db_ddladmin', N'$(MigratorUser)'), 0) = 0
BEGIN
    PRINT N'change: add [$(MigratorUser)] to db_ddladmin';
    IF @dryRun = 0 ALTER ROLE db_ddladmin ADD MEMBER [$(MigratorUser)];
END;

IF ISNULL(IS_ROLEMEMBER(N'db_datareader', N'$(MigratorUser)'), 0) = 0
BEGIN
    PRINT N'change: add [$(MigratorUser)] to db_datareader';
    IF @dryRun = 0 ALTER ROLE db_datareader ADD MEMBER [$(MigratorUser)];
END;

IF ISNULL(IS_ROLEMEMBER(N'db_datawriter', N'$(MigratorUser)'), 0) = 0
BEGIN
    PRINT N'change: add [$(MigratorUser)] to db_datawriter';
    IF @dryRun = 0 ALTER ROLE db_datawriter ADD MEMBER [$(MigratorUser)];
END;

-- The migration AddChangeRecords creates the append-only role, so the migrator creates it and can always see it.
IF NOT EXISTS (SELECT 1 FROM sys.database_permissions
               WHERE class = 0 AND grantee_principal_id = DATABASE_PRINCIPAL_ID(N'$(MigratorUser)')
                 AND permission_name = N'CREATE ROLE' AND state = 'G')
BEGIN
    PRINT N'change: grant CREATE ROLE to [$(MigratorUser)]';
    IF @dryRun = 0 GRANT CREATE ROLE TO [$(MigratorUser)];
END;

IF ISNULL(IS_ROLEMEMBER(N'db_owner', N'$(RuntimeUser)'), 0) = 1
   OR ISNULL(IS_ROLEMEMBER(N'db_securityadmin', N'$(RuntimeUser)'), 0) = 1
   OR ISNULL(IS_ROLEMEMBER(N'db_ddladmin', N'$(RuntimeUser)'), 0) = 1
   OR ISNULL(IS_ROLEMEMBER(N'db_accessadmin', N'$(RuntimeUser)'), 0) = 1
    THROW 50003, N'The user [$(RuntimeUser)] is in db_owner, db_securityadmin, db_ddladmin or db_accessadmin; remove it from them by hand.', 1;

IF ISNULL(IS_ROLEMEMBER(N'db_owner', N'$(MigratorUser)'), 0) = 1
    THROW 50004, N'The user [$(MigratorUser)] is in db_owner; remove it by hand.', 1;

IF SCHEMA_ID(N'audit') IS NULL OR DATABASE_PRINCIPAL_ID(N'$(AuditRole)') IS NULL
BEGIN
    PRINT N'pending: the migrations have not created the schema audit and the role [$(AuditRole)] yet; run the migrator, then database.sh again';
END
ELSE
BEGIN
    -- The migrator owns audit when it created the schema; it needs CONTROL there to deny the role and to sweep it.
    IF DATABASE_PRINCIPAL_ID(N'$(MigratorUser)') IS NOT NULL
       AND (SELECT principal_id FROM sys.schemas WHERE name = N'audit') <> DATABASE_PRINCIPAL_ID(N'$(MigratorUser)')
       AND NOT EXISTS (SELECT 1 FROM sys.database_permissions
                       WHERE class = 3 AND major_id = SCHEMA_ID(N'audit')
                         AND grantee_principal_id = DATABASE_PRINCIPAL_ID(N'$(MigratorUser)')
                         AND permission_name = N'CONTROL' AND state = 'G')
    BEGIN
        PRINT N'change: grant CONTROL on the schema audit to [$(MigratorUser)]';
        IF @dryRun = 0 GRANT CONTROL ON SCHEMA::[audit] TO [$(MigratorUser)];
    END;

    IF ISNULL(IS_ROLEMEMBER(N'$(AuditRole)', N'$(RuntimeUser)'), 0) = 0
    BEGIN
        PRINT N'change: add [$(RuntimeUser)] to [$(AuditRole)]';
        IF @dryRun = 0 ALTER ROLE [$(AuditRole)] ADD MEMBER [$(RuntimeUser)];
    END;

    IF ISNULL(IS_ROLEMEMBER(N'$(AuditRole)', N'$(RuntimeUser)'), 0) = 1
    BEGIN
        EXECUTE AS USER = N'$(RuntimeUser)';
        DECLARE @runtimeInsert int = HAS_PERMS_BY_NAME(N'audit', N'SCHEMA', N'INSERT'),
                @runtimeUpdate int = HAS_PERMS_BY_NAME(N'audit', N'SCHEMA', N'UPDATE'),
                @runtimeDelete int = HAS_PERMS_BY_NAME(N'audit', N'SCHEMA', N'DELETE'),
                @runtimeAlter int = HAS_PERMS_BY_NAME(N'audit', N'SCHEMA', N'ALTER'),
                @runtimeAlterAnyRole int = HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER ANY ROLE');
        REVERT;
        IF @runtimeInsert <> 1 OR @runtimeUpdate <> 0 OR @runtimeDelete <> 0 OR @runtimeAlter <> 0 OR @runtimeAlterAnyRole <> 0
            THROW 50005, N'Under EXECUTE AS USER, [$(RuntimeUser)] must insert into audit and must not update, delete or alter there or alter any role.', 1;
        PRINT N'ok: [$(RuntimeUser)] may insert into audit, may not update, delete or alter there, and alters no role';
    END;

    IF DATABASE_PRINCIPAL_ID(N'$(MigratorUser)') IS NOT NULL
       AND ((SELECT principal_id FROM sys.schemas WHERE name = N'audit') = DATABASE_PRINCIPAL_ID(N'$(MigratorUser)')
            OR EXISTS (SELECT 1 FROM sys.database_permissions
                       WHERE class = 3 AND major_id = SCHEMA_ID(N'audit')
                         AND grantee_principal_id = DATABASE_PRINCIPAL_ID(N'$(MigratorUser)')
                         AND permission_name = N'CONTROL' AND state = 'G'))
    BEGIN
        EXECUTE AS USER = N'$(MigratorUser)';
        DECLARE @migratorControl int = HAS_PERMS_BY_NAME(N'audit', N'SCHEMA', N'CONTROL'),
                @migratorCreateRole int = HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CREATE ROLE');
        REVERT;
        IF @migratorControl <> 1 OR @migratorCreateRole <> 1
            THROW 50006, N'Under EXECUTE AS USER, [$(MigratorUser)] must hold CONTROL on audit and CREATE ROLE.', 1;
        PRINT N'ok: [$(MigratorUser)] holds CONTROL on audit and CREATE ROLE';
    END;
END;

PRINT N'done';

-- Only the Microsoft Entra admin may create users from Microsoft Entra ID, so scripts/azure/database.sh runs this as that admin.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @dryRun bit = $(DryRun);

-- Every statement below runs as dbo, so a database DDL trigger would run whatever it holds as dbo too.
IF EXISTS (SELECT 1 FROM sys.triggers WHERE parent_class = 0)
    THROW 50001, N'A database DDL trigger exists, and this script''s statements would fire it as dbo; read sys.triggers and drop the trigger by hand first.', 1;

DECLARE @databaseFirewallRules int = 0;
IF OBJECT_ID(N'sys.database_firewall_rules') IS NOT NULL
    EXEC sys.sp_executesql N'SELECT @count = COUNT(*) FROM sys.database_firewall_rules;', N'@count int OUTPUT', @count = @databaseFirewallRules OUTPUT;
IF @databaseFirewallRules > 0
    THROW 50002, N'A database-level firewall rule admits addresses beside the server''s one rule; remove it with sys.sp_delete_database_firewall_rule.', 1;

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
    THROW 50003, N'The user [$(RuntimeUser)] is not the service principal with client id $(RuntimeAppId); drop it (DROP USER [$(RuntimeUser)]; run as the Microsoft Entra admin through the Go sqlcmd) and run database.sh again.', 1;

IF EXISTS (SELECT 1 FROM sys.database_principals
           WHERE name = N'$(MigratorUser)'
             AND (type <> 'E' OR CAST(CAST(sid AS varbinary(16)) AS uniqueidentifier) <> '$(MigratorAppId)'))
    THROW 50004, N'The user [$(MigratorUser)] is not the service principal with client id $(MigratorAppId). It owns the schemas and [$(AuditRole)], so it cannot be dropped: restore its deleted app registration from the Microsoft Entra recycle bin, which keeps its client id (docs/operations/runbooks/database.md, "Recovery").', 1;

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

IF EXISTS (SELECT 1 FROM sys.database_role_members AS m
           JOIN sys.database_principals AS r ON r.principal_id = m.role_principal_id
           WHERE m.member_principal_id = DATABASE_PRINCIPAL_ID(N'$(RuntimeUser)')
             AND r.name NOT IN (N'db_datareader', N'db_datawriter', N'$(AuditRole)'))
    THROW 50005, N'The user [$(RuntimeUser)] is in a role beside db_datareader, db_datawriter and [$(AuditRole)]; remove it from that role by hand.', 1;

IF EXISTS (SELECT 1 FROM sys.database_role_members AS m
           JOIN sys.database_principals AS r ON r.principal_id = m.role_principal_id
           WHERE m.member_principal_id = DATABASE_PRINCIPAL_ID(N'$(MigratorUser)')
             AND r.name NOT IN (N'db_ddladmin', N'db_datareader', N'db_datawriter'))
    THROW 50006, N'The user [$(MigratorUser)] is in a role beside db_ddladmin, db_datareader and db_datawriter; remove it from that role by hand.', 1;

IF EXISTS (SELECT 1 FROM sys.database_role_members
           WHERE role_principal_id IN (DATABASE_PRINCIPAL_ID(N'db_owner'), DATABASE_PRINCIPAL_ID(N'db_securityadmin'), DATABASE_PRINCIPAL_ID(N'db_accessadmin'))
             AND member_principal_id <> DATABASE_PRINCIPAL_ID(N'dbo'))
    THROW 50007, N'db_owner, db_securityadmin or db_accessadmin has a member beside dbo; remove it by hand.', 1;

IF EXISTS (SELECT 1 FROM sys.database_role_members
           WHERE role_principal_id = DATABASE_PRINCIPAL_ID(N'db_ddladmin')
             AND member_principal_id <> ISNULL(DATABASE_PRINCIPAL_ID(N'$(MigratorUser)'), 0))
    THROW 50008, N'db_ddladmin has a member beside [$(MigratorUser)]; remove it by hand.', 1;

IF EXISTS (SELECT 1 FROM sys.database_permissions
           WHERE grantee_principal_id = DATABASE_PRINCIPAL_ID(N'$(RuntimeUser)')
             AND NOT (class = 0 AND permission_name = N'CONNECT' AND state = 'G'))
    THROW 50009, N'The user [$(RuntimeUser)] holds a permission beside CONNECT; revoke it by hand (sys.database_permissions).', 1;

IF EXISTS (SELECT 1 FROM sys.database_permissions
           WHERE grantee_principal_id = DATABASE_PRINCIPAL_ID(N'$(MigratorUser)')
             AND NOT (class = 0 AND permission_name IN (N'CONNECT', N'CREATE ROLE') AND state = 'G')
             AND NOT (class = 3 AND major_id = SCHEMA_ID(N'audit') AND permission_name = N'CONTROL' AND state = 'G'))
    THROW 50010, N'The user [$(MigratorUser)] holds a permission beside CONNECT, CREATE ROLE and CONTROL on audit; revoke it by hand (sys.database_permissions).', 1;

IF EXISTS (SELECT 1 FROM sys.database_principals WHERE owning_principal_id = DATABASE_PRINCIPAL_ID(N'$(RuntimeUser)'))
   OR EXISTS (SELECT 1 FROM sys.schemas WHERE principal_id = DATABASE_PRINCIPAL_ID(N'$(RuntimeUser)'))
    THROW 50011, N'The user [$(RuntimeUser)] owns a role or a schema; transfer it to [$(MigratorUser)] by hand.', 1;

IF SCHEMA_ID(N'audit') IS NULL OR DATABASE_PRINCIPAL_ID(N'$(AuditRole)') IS NULL
BEGIN
    PRINT N'pending: the migrations have not created the schema audit and the role [$(AuditRole)] yet, so [$(RuntimeUser)] gets no role yet; run the migrator, then database.sh again';
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

    -- The append-only role comes before the data roles, so the runtime's user never holds UPDATE or DELETE on audit.
    IF ISNULL(IS_ROLEMEMBER(N'$(AuditRole)', N'$(RuntimeUser)'), 0) = 0
    BEGIN
        PRINT N'change: add [$(RuntimeUser)] to [$(AuditRole)]';
        IF @dryRun = 0 ALTER ROLE [$(AuditRole)] ADD MEMBER [$(RuntimeUser)];
    END;

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

    IF ISNULL(IS_ROLEMEMBER(N'$(AuditRole)', N'$(RuntimeUser)'), 0) = 1
       AND ISNULL(IS_ROLEMEMBER(N'db_datawriter', N'$(RuntimeUser)'), 0) = 1
    BEGIN
        EXECUTE AS USER = N'$(RuntimeUser)';
        DECLARE @runtimeInsert int = HAS_PERMS_BY_NAME(N'audit', N'SCHEMA', N'INSERT'),
                @runtimeUpdate int = HAS_PERMS_BY_NAME(N'audit', N'SCHEMA', N'UPDATE'),
                @runtimeDelete int = HAS_PERMS_BY_NAME(N'audit', N'SCHEMA', N'DELETE'),
                @runtimeAlter int = HAS_PERMS_BY_NAME(N'audit', N'SCHEMA', N'ALTER'),
                @runtimeAlterAnyRole int = HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER ANY ROLE');
        REVERT;
        IF @runtimeInsert <> 1 OR @runtimeUpdate <> 0 OR @runtimeDelete <> 0 OR @runtimeAlter <> 0 OR @runtimeAlterAnyRole <> 0
            THROW 50012, N'Under EXECUTE AS USER, [$(RuntimeUser)] must insert into audit and must not update, delete or alter there or alter any role.', 1;
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
            THROW 50013, N'Under EXECUTE AS USER, [$(MigratorUser)] must hold CONTROL on audit and CREATE ROLE.', 1;
        PRINT N'ok: [$(MigratorUser)] holds CONTROL on audit and CREATE ROLE';
    END;
END;

PRINT N'done';

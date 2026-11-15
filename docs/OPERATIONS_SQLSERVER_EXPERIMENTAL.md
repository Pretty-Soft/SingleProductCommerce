# Experimental SQL Server backup and restore

This runbook covers local evaluation of the experimental SQL Server provider. It does not establish SQL Server as a supported production provider. Provider-specific installation, deployment, concurrency, and recovery verification are still required before making that support claim.

## What a complete store backup includes

A database backup contains catalog, settings, administrator accounts, orders, and audit records. The store also needs the API host's product-image files and ASP.NET Core Data Protection keys. Back up the configured `ProductMedia:RootPath` and `DataProtection:KeyDirectory`; when unset in Development, they default to `backend/OneCart.Api/App_Data/product-images` and `backend/OneCart.Api/App_Data/data-protection-keys`.

Keep database backups, image files, Data Protection keys, and connection secrets outside Git. Restrict access to the key directory because it can be used to decrypt protected application data and authenticate existing sessions.

## Create and verify a database backup

1. Create a backup directory on a disk accessible to the SQL Server service, and grant that service account write access. A path on your workstation is not automatically writable by the database service.
2. Use SQL Server Management Studio or `sqlcmd` to run a full backup. Choose a new filename for each backup:

```sql
BACKUP DATABASE [OneCartCommerce]
TO DISK = N'D:\SQLBackups\OneCartCommerce_20261116_1200.bak'
WITH CHECKSUM, STATS = 5;
```

3. Verify the backup set:

```sql
RESTORE VERIFYONLY
FROM DISK = N'D:\SQLBackups\OneCartCommerce_20261116_1200.bak'
WITH CHECKSUM;
```

`RESTORE VERIFYONLY` checks that the backup set is readable; it does not replace a restore rehearsal. See Microsoft's [BACKUP documentation](https://learn.microsoft.com/en-us/sql/t-sql/statements/backup-transact-sql) and [RESTORE documentation](https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-verifyonly-transact-sql).

4. Copy the image and Data Protection key directories to protected backup storage. Coordinate those file copies with the database backup: stop catalog/media writes during the copy or use a filesystem snapshot so image metadata and image bytes stay consistent.
5. Record the matching application version, database provider, configured media/key paths, and the backup timestamp. Keep the SQL connection string in the operator's secret store, not beside the backup.

## Restore rehearsal

Restore to a separate database name so the live database is not overwritten:

1. Restore the image and Data Protection key directories to a protected location accessible to the API process. Preserve file contents and access restrictions.
2. Inspect the backup's logical file names:

```sql
RESTORE FILELISTONLY
FROM DISK = N'D:\SQLBackups\OneCartCommerce_20261116_1200.bak';
```

3. Use the returned logical names and paths valid for the SQL Server instance to restore to an isolated database. Replace the example logical names and target paths with values for that instance:

```sql
RESTORE DATABASE [OneCartCommerce_RestoreCheck]
FROM DISK = N'D:\SQLBackups\OneCartCommerce_20261116_1200.bak'
WITH MOVE N'OneCartCommerce' TO N'D:\SQLData\OneCartCommerce_RestoreCheck.mdf',
     MOVE N'OneCartCommerce_log' TO N'D:\SQLData\OneCartCommerce_RestoreCheck_log.ldf',
     CHECKSUM, RECOVERY, STATS = 5;
```

4. Point a separate local API instance at the restored database through .NET user-secrets. Set `ProductMedia:RootPath` and `DataProtection:KeyDirectory` to the restored file copies. Never point a restore rehearsal at the live database or live file directories.
5. Confirm API health, administrator sign-in, a published product, at least one product image, and order history. Then remove the isolated database and temporary restore files after the rehearsal is recorded.

For a verified recovery claim, repeat the rehearsal on the actual supported deployment topology and document recovery time, recovery point, retention, and operator access. Point-in-time recovery and hosted deployment support remain open release work.

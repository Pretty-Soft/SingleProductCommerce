# PostgreSQL backup and restore

This runbook covers a local PostgreSQL database and the files that belong to the same OneCart store. A PostgreSQL custom-format dump contains one database; it does not include cluster-wide roles. Product image bytes are stored separately on the API host, and the API's ASP.NET Core Data Protection keys must also be preserved.

## Back up a local store

Use the PostgreSQL client tools that match the installed server. Keep the backup directory outside the repository because the database contains administrator credentials and guest order contact details. Restrict access to the backup files and copy them to an encrypted, access-controlled storage location.

For a consistent database-and-file set, stop the API while making this paired backup so an image upload or deletion cannot occur between the database dump and file copy. `pg_dump` itself can run while the database is in use and produces a consistent database snapshot.

```powershell
$backupDirectory = Join-Path $env:USERPROFILE 'OneCartBackups'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$databaseBackup = Join-Path $backupDirectory "onecart-$stamp.dump"
$apiData = 'D:\MyProjects\SingleProductCommerce\backend\OneCart.Api\App_Data'

New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null
pg_dump --host localhost --port 5432 --username postgres --format custom --file $databaseBackup onecart
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL backup failed.' }

$imageDirectory = Join-Path $apiData 'product-images'
$keyDirectory = Join-Path $apiData 'data-protection-keys'
if (Test-Path -LiteralPath $imageDirectory) {
    Copy-Item -LiteralPath $imageDirectory -Destination (Join-Path $backupDirectory "product-images-$stamp") -Recurse
}
if (Test-Path -LiteralPath $keyDirectory) {
    Copy-Item -LiteralPath $keyDirectory -Destination (Join-Path $backupDirectory "data-protection-keys-$stamp") -Recurse
}
```

Replace the example host, port, database, user, API data path, and backup destination with the values configured for the store. `pg_dump` prompts for a password if PostgreSQL requires one. Do not put database passwords in the command line, scripts, repository, or backup folder.

If `ProductMedia:RootPath` or `DataProtection:KeyDirectory` points somewhere else, back up those configured directories instead. On Windows, Data Protection keys are protected with DPAPI; keep their backup under the same Windows account unless a deliberate key migration procedure has been prepared. Database dumps and key files contain sensitive data and must be protected at rest.

## Verify a backup by restoring to a separate database

Restore into a new, empty database first. Do not point the verification restore at the live store database.

```powershell
$databaseBackup = 'C:\Users\YOUR_USER\OneCartBackups\onecart-YYYYMMDD-HHMMSS.dump'
$restoreDatabase = 'onecart_restore_check'

createdb --host localhost --port 5432 --username postgres $restoreDatabase
if ($LASTEXITCODE -ne 0) { throw 'Could not create the isolated restore database.' }

pg_restore --host localhost --port 5432 --username postgres --dbname $restoreDatabase --no-owner --no-privileges --exit-on-error $databaseBackup
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL restore verification failed.' }
```

Choose a unique restore database name if that name already exists. The command omits the source database's owners and grants so the current local PostgreSQL account can own the restored objects. It does not use `--clean`; no existing database objects are dropped.

To run the API against the restored database, point `ConnectionStrings:Store` in local .NET user secrets at the restore database. Point `ProductMedia:RootPath` and `DataProtection:KeyDirectory` to copies of the matching backed-up directories. Confirm the storefront, an existing image, and admin authentication work before considering the backup verified. The database dump alone is not a complete store backup.

Remove the verification database only after confirming its name and that it is the temporary restore target:

```powershell
dropdb --host localhost --port 5432 --username postgres $restoreDatabase
```

## Recovery scope

This procedure documents a logical backup and an isolated restore check. It does not provide point-in-time recovery, automated schedules, off-site retention, or a production high-availability design. Those require an approved hosting topology and an operator-selected retention policy.

# Shared File Upload And Malware-Scanning Boundary

All modules that accept user-controlled files must use the shared
`IControlledFileUploadService` contract from `ErpSystem.Core.Interfaces`.
Do not write uploaded files directly with `File.Create`, `FileStream`, or
module-specific storage code.

## Module integration

Inject `IControlledFileUploadService` and submit a
`ControlledFileUploadRequest` containing the authenticated tenant, server-
derived actor, normalized category, file metadata, and a fresh stream factory.
The shared service performs:

- tenant policy and quota evaluation;
- active-content rejection;
- extension and MIME allowlist checks;
- mandatory or policy-driven malware scanning;
- fail-closed clean-scan validation;
- SHA-256 calculation;
- shared storage-provider persistence; and
- tenant-scoped `FileUploadRecord` audit metadata.

Use the constants in `ControlledFileUploadCategories` instead of copying
category strings:

- `SupplierRegistrationEvidence` for supplier onboarding evidence;
- `DocumentManagement` for document-management uploads.

Both categories always require `FileVirusScanStatus.Clean`. A tenant policy
cannot disable that system requirement.

Additional module categories can be added centrally through
`FileUpload:RequiredCleanScanCategoriesCsv`. Category-specific
`FileUploadPolicy.RequireVirusScan` remains available for tenant-managed
categories.

## Scanner provider

`IFileVirusScanService` is the single provider contract for the application.
`AddErpSystemFileUpload` registers the centralized
`ClamAvFileVirusScanService` by default and uses `TryAddSingleton`, so an
equivalent host-level scanner registered before the ERP extension is not
overwritten. The service streams bytes through clamd's `INSTREAM` protocol;
it does not share storage paths with the scanner.

Configure the daemon under `FileVirusScan:ClamAv`:

- `Host` defaults to `127.0.0.1`;
- `Port` defaults to `3310`;
- connection and scan timeouts are bounded; and
- chunk and response sizes are validated at startup.

The checked-in Docker Compose definitions run the official
`clamav/clamav:stable` image, persist its signature database, and expose port
3310 only inside the private ERP network. Never publish clamd's TCP port to an
untrusted network because the protocol has no transport authentication.

For a native Windows/VPS deployment, install and run `clamd` as a local
service (or provide a private reachable clamd host), then override
`FileVirusScan__ClamAv__Host` and `FileVirusScan__ClamAv__Port` as needed.
`/health/ready` reports unhealthy while the configured daemon cannot answer
`PING`, and mandatory clean-scan uploads remain fail closed.

Provider failures, timeouts, `Skipped`, `Pending`, `Error`, and `Infected`
outcomes never reach storage for mandatory clean-scan categories. The legacy
`NoOpFileVirusScanService` is retained only as an explicit test/custom-host
type and is no longer the application runtime default.

## Multi-node local storage

Every API instance that uses the `Local` provider must see the same physical
storage namespace. The production Compose topology mounts the single
`erp-uploads` volume at `/app/wwwroot/uploads` in all four API containers, and
the API image prepares that mount point for its non-root runtime user.

This shared mount makes an absent file authoritative to every cleanup worker
and allows a request routed to any API node to read an uploaded object. Do not
replace the mount with separate node-local volumes. For deployments across
multiple Docker hosts, configure a genuinely shared filesystem or an
object-storage provider such as Azure Blob rather than the local Docker
volume.

## Linking uploaded files

Domain records should retain the returned `FileUploadRecord.Id`, checksum,
category, and scan status. Security-sensitive document aggregates must accept
only an active, same-tenant record with `VirusScanStatus.Clean`; add a database
guard when direct SQL writes must also be protected.

`IControlledFileUploadService.DeleteAsync` soft-deletes the tenant metadata
and schedules physical removal on that same `FileUploadRecord`. The
`FileStorageCleanupBackgroundService` can only observe this work after the
owning database transaction commits. It retries provider exceptions and
`DeleteFileAsync == false` responses with durable attempt, error, and
next-attempt metadata. Modules must not delete the physical object directly.

Storage-provider implementations must make deletion idempotent: return `true`
when the object is absent after the operation, including when it was already
absent, and return `false` only when absence could not be ensured. This lets a
retry finish safely if storage deletion succeeded but the cleanup-status save
failed.

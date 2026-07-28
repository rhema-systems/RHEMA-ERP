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
`AddErpSystemFileUpload` uses `TryAddSingleton`, so a real host-level scanner
registered before the ERP file-upload extension is not overwritten.

The built-in `NoOpFileVirusScanService` returns `Skipped`; it never reports a
file as clean. Uploads in mandatory clean-scan categories therefore fail
closed until a real provider is registered. Provider failures, timeouts,
`Skipped`, `Pending`, `Error`, and `Infected` outcomes never reach storage for
those categories.

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

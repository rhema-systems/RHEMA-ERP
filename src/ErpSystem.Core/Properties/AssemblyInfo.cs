using System.Runtime.CompilerServices;

// Disposal's ambient participant is intentionally an implementation-only bridge. The API assembly
// hosts the trusted owner adapter; no public Core service contract exposes stage/post authority.
[assembly: InternalsVisibleTo("ErpSystem.Api")]

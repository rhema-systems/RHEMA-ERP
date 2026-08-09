using System.Runtime.CompilerServices;

// The SQL compiler is intentionally internal production infrastructure. Focused tests receive
// access so injection, tenant isolation and row-governance invariants can be asserted directly.
[assembly: InternalsVisibleTo("ErpSystem.Api.Tests")]

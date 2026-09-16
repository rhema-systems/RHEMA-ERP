using System.Runtime.CompilerServices;

// The SQL compiler is intentionally internal production infrastructure. Focused tests receive
// access so injection, tenant isolation and row-governance invariants can be asserted directly.
[assembly: InternalsVisibleTo("ErpSystem.Api.Tests")]
// C11's approved-execution interfaces stay internal in production. Moq emits its test doubles
// from this conventional proxy assembly, so it needs the same test-only access.
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

# Quick Cleanup Guide - ERP System

**Last Updated:** 2025-11-27

---

## 🚀 Quick Start (5 Minutes)

### Run Automated Cleanup
```powershell
# Run the cleanup script
.\scripts\cleanup-code.ps1

# Or just format code
dotnet format

# Or format specific project
dotnet format src/ErpSystem.Core/ErpSystem.Core.csproj
```

---

## 🔧 Common Issues & Quick Fixes

### 1. **Duplicate Using Directives** (CS0105)

**Error:**
```
warning CS0105: The using directive for 'System.ComponentModel.DataAnnotations' appeared previously in this namespace
```

**Fix:**
```csharp
// Before
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations;  // ❌ Duplicate

// After
using System.ComponentModel.DataAnnotations;  // ✅ Single
```

**Automated Fix:**
```bash
dotnet format
```

---

### 2. **Circular Dependency** (CS0246)

**Error:**
```
error CS0246: The type or namespace name 'Web' could not be found
```

**Problem:**
```csharp
// ❌ Core layer trying to use API layer
using ErpSystem.Web.Services;  // Wrong!

public class CoreService
{
    private readonly Web.Services.IEmailService _emailService;  // ❌ Circular dependency
}
```

**Fix:**
```csharp
// ✅ Use Core layer interface
using ErpSystem.Core.Interfaces.Common;

public class CoreService
{
    private readonly IEmailService _emailService;  // ✅ Core interface
}
```

**Rule:** Core layer should NEVER reference API/Web layer!

---

### 3. **Member Hiding** (CS0108)

**Warning:**
```
warning CS0108: 'Activity.Priority' hides inherited member 'BusinessEntity.Priority'
```

**Fix:**
```csharp
// Before
public class Activity : BusinessEntity
{
    public string Priority { get; set; }  // ⚠️ Hides base member
}

// After - Option 1: Use 'new' keyword (if intentional)
public class Activity : BusinessEntity
{
    public new string Priority { get; set; }  // ✅ Explicit hiding
}

// After - Option 2: Rename property (if not intentional)
public class Activity : BusinessEntity
{
    public string ActivityPriority { get; set; }  // ✅ Different name
}
```

---

### 4. **Nullable Reference** (CS8601, CS8602, CS8604)

**Warning:**
```
warning CS8601: Possible null reference assignment
warning CS8602: Dereference of a possibly null reference
```

**Fix:**
```csharp
// Before
public string Name { get; set; }  // ⚠️ Can be null
var length = user.Name.Length;    // ⚠️ Might throw NullReferenceException

// After - Option 1: Make nullable
public string? Name { get; set; }  // ✅ Explicitly nullable
var length = user.Name?.Length ?? 0;  // ✅ Safe access

// After - Option 2: Ensure not null
public string Name { get; set; } = string.Empty;  // ✅ Default value
var length = user.Name.Length;  // ✅ Safe
```

---

### 5. **Async Without Await** (CS1998)

**Warning:**
```
warning CS1998: This async method lacks 'await' operators
```

**Fix:**
```csharp
// Before
public async Task<bool> ValidateAsync()
{
    return true;  // ⚠️ No await
}

// After - Option 1: Remove async
public Task<bool> ValidateAsync()
{
    return Task.FromResult(true);  // ✅ Return Task directly
}

// After - Option 2: Add await
public async Task<bool> ValidateAsync()
{
    await Task.CompletedTask;  // ✅ Add await
    return true;
}
```

---

## 📋 Pre-Commit Checklist

Before committing code, run these checks:

```powershell
# 1. Format code
dotnet format

# 2. Build without errors
dotnet build

# 3. Run tests
dotnet test

# 4. Check warnings
.\scripts\cleanup-code.ps1
```

---

## 🎯 Architecture Rules

### Layer Dependencies (MUST FOLLOW)

```
┌─────────────────────────────────────────┐
│  Frontend (Next.js/React)               │
│  ✅ Can call: API layer                 │
└─────────────────────────────────────────┘
              ↓
┌─────────────────────────────────────────┐
│  API Layer (ErpSystem.Api)              │
│  ✅ Can use: Core, Shared               │
│  ❌ Cannot use: Frontend                │
└─────────────────────────────────────────┘
              ↓
┌─────────────────────────────────────────┐
│  Core Layer (ErpSystem.Core)            │
│  ✅ Can use: Shared                     │
│  ❌ Cannot use: API, Frontend           │
└─────────────────────────────────────────┘
              ↓
┌─────────────────────────────────────────┐
│  Shared Layer (ErpSystem.Shared)        │
│  ✅ Can use: Nothing (base layer)       │
│  ❌ Cannot use: Core, API, Frontend     │
└─────────────────────────────────────────┘
```

### Email Service Pattern

```csharp
// ✅ CORRECT: Core layer service
using ErpSystem.Core.Interfaces.Common;

public class BusinessPartnerRegistrationService
{
    private readonly IEmailService _emailService;  // Core interface
    
    public async Task SendEmailAsync()
    {
        var email = new EmailDto { ... };
        await _emailService.SendEmailAsync(email);
    }
}

// ❌ WRONG: Core layer using API interface
using ErpSystem.Web.Services;  // ❌ Don't do this!

public class BusinessPartnerRegistrationService
{
    private readonly Web.Services.IEmailService _emailService;  // ❌ Circular dependency
}
```

---

## 🛠️ Tools & Commands

### Format Code
```bash
# Format entire solution
dotnet format

# Format specific project
dotnet format src/ErpSystem.Core/ErpSystem.Core.csproj

# Verify formatting (CI/CD)
dotnet format --verify-no-changes
```

### Build & Analyze
```bash
# Build with warnings
dotnet build /p:TreatWarningsAsErrors=false

# Build treating warnings as errors
dotnet build /p:TreatWarningsAsErrors=true

# Run analyzers
dotnet build /p:RunAnalyzers=true
```

### Clean Build
```bash
# Clean solution
dotnet clean

# Rebuild from scratch
dotnet clean && dotnet build --no-incremental
```

---

## 📊 Warning Reduction Goals

| Metric | Current | Target | Status |
|--------|---------|--------|--------|
| Build Errors | 0 | 0 | ✅ |
| Build Warnings | 274 | < 10 | 🔴 |
| Duplicate Usings | 2 | 0 | 🟡 |
| Nullable Warnings | ~50 | 0 | 🔴 |
| Async Warnings | ~100 | 0 | 🔴 |

---

## 🎓 Learning Resources

### Official Documentation
- [.NET Code Style Rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/)
- [EditorConfig for .NET](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files)
- [Nullable Reference Types](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references)

### Tools
- [dotnet format](https://github.com/dotnet/format)
- [StyleCop Analyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers)
- [Roslynator](https://github.com/JosefPihrt/Roslynator)

---

## 💡 Tips

1. **Run cleanup before every commit**
   ```bash
   dotnet format && dotnet build
   ```

2. **Use IDE warnings**
   - Visual Studio: View → Error List
   - VS Code: Problems panel (Ctrl+Shift+M)

3. **Fix warnings incrementally**
   - Don't try to fix all 274 warnings at once
   - Fix one type of warning at a time
   - Start with CS0105 (duplicate usings) - easiest

4. **Use automated tools**
   - Let `dotnet format` do the heavy lifting
   - Use IDE quick fixes (Ctrl+.)

5. **Follow the layer rules**
   - Core → Shared only
   - API → Core, Shared
   - Frontend → API only

---

**Need Help?** Check the full guide: `docs/CODE_CLEANUP_AND_QUALITY_PLAN.md`


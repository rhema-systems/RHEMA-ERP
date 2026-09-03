using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Spreadsheets;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

namespace ErpSystem.Api.Services.Finance.Reporting;

public sealed class FinancialStatementLayoutImportService
    : IFinancialStatementLayoutImportService
{
    private const string TemplateVersion = "1";
    private const int MaximumFileBytes = 5 * 1024 * 1024;
    private const int MaximumRows = 2_000;
    private const int MaximumMappings = 10_000;
    private static readonly Regex InvalidCodeCharacters = new(
        "[^A-Z0-9_.]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions HashJsonOptions = new(
        JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinancialStatementLayoutService _layouts;
    private readonly IFinanceAuditService _audit;

    public FinancialStatementLayoutImportService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IFinancialStatementLayoutService layouts,
        IFinanceAuditService audit)
    {
        _context = context;
        _currentUser = currentUser;
        _layouts = layouts;
        _audit = audit;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public async Task<FinancialStatementLayoutFileDto> CreateWorkbookTemplateAsync(
        CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook();
        AddInstructionsSheet(workbook);
        AddMetadataSheet(workbook);
        AddRowsSheet(workbook);
        AddMappingsSheet(workbook);
        await AddLookupsSheetAsync(workbook, cancellationToken);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new FinancialStatementLayoutFileDto
        {
            Content = stream.ToArray(),
            FileName =
                $"financial-statement-layout-import-v{TemplateVersion}.xlsx"
        };
    }

    public async Task<FinancialStatementLayoutImportPreviewDto>
        PreviewDefinitionAsync(
            FinancialStatementLayoutImportDefinitionDto definition,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var normalized = NormalizeDefinition(definition);
        var validation = new FinancialStatementLayoutValidationResultDto();

        await ValidateImportMetadataAsync(
            normalized,
            validation,
            cancellationToken);

        if (Enum.IsDefined(normalized.StatementType) &&
            normalized.AccountingBookId != Guid.Empty &&
            !validation.Issues.Any(issue =>
                issue.Code is "ACCOUNTING_BOOK_INVALID" or
                    "TARGET_LAYOUT_INVALID" or
                    "TARGET_LAYOUT_MISMATCH"))
        {
            var definitionValidation = await _layouts.ValidateDefinitionAsync(
                normalized.StatementType,
                normalized.AccountingBookId,
                normalized.Rows,
                cancellationToken);
            validation.Issues.AddRange(definitionValidation.Issues);
        }

        return new FinancialStatementLayoutImportPreviewDto
        {
            Definition = normalized,
            DefinitionHash = ComputeHash(normalized),
            WillCreateLayout = !normalized.TargetLayoutId.HasValue,
            TargetLayoutId = normalized.TargetLayoutId,
            TargetVersionId = normalized.TargetVersionId,
            RowCount = normalized.Rows.Count,
            MappingCount = normalized.Rows.Sum(row => row.Mappings.Count),
            Validation = validation
        };
    }

    public async Task<FinancialStatementLayoutImportResultDto>
        CommitDefinitionAsync(
            FinancialStatementLayoutImportCommitDto request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var preview = await PreviewDefinitionAsync(
            request.Definition,
            cancellationToken);
        EnsureExpectedHash(
            request.ExpectedDefinitionHash,
            preview.DefinitionHash);
        ThrowForErrors(preview.Validation);

        FinancialStatementLayoutDto layout;
        FinancialStatementLayoutVersionDto draft;
        var createdLayout = !preview.Definition.TargetLayoutId.HasValue;

        if (createdLayout)
        {
            layout = await _layouts.CreateLayoutAsync(
                new CreateFinancialStatementLayoutDto
                {
                    Code = preview.Definition.Code,
                    Name = preview.Definition.Name,
                    Description = preview.Definition.Description,
                    StatementType = preview.Definition.StatementType,
                    AccountingBookId = preview.Definition.AccountingBookId,
                    // Imports are deliberately draft-only and cannot replace a
                    // production default before publication review.
                    IsDefault = false,
                    EffectiveFrom = preview.Definition.EffectiveFrom,
                    EffectiveTo = preview.Definition.EffectiveTo,
                    Notes = BuildImportNotes(preview.Definition.Notes)
                },
                cancellationToken);
            draft = layout.Versions.Single(version =>
                version.Status ==
                FinancialStatementLayoutVersionStatus.Draft);
        }
        else
        {
            layout = await _layouts.GetLayoutAsync(
                    preview.Definition.TargetLayoutId!.Value,
                    cancellationToken)
                ?? throw new KeyNotFoundException(
                    "The target layout no longer exists.");

            if (preview.Definition.TargetVersionId.HasValue)
            {
                draft = layout.Versions.Single(version =>
                    version.Id == preview.Definition.TargetVersionId.Value);
            }
            else
            {
                draft = await _layouts.CreateDraftVersionAsync(
                    layout.Id,
                    new CreateFinancialStatementLayoutVersionDto
                    {
                        SourceVersionId =
                            preview.Definition.SourceVersionId,
                        EffectiveFrom =
                            preview.Definition.EffectiveFrom,
                        EffectiveTo = preview.Definition.EffectiveTo,
                        Notes = BuildImportNotes(
                            preview.Definition.Notes)
                    },
                    cancellationToken);
            }
        }

        var expectedRevision =
            preview.Definition.ExpectedTargetVersionRevision
            ?? draft.Revision;
        draft = await _layouts.ReplaceDraftRowsAsync(
            draft.Id,
            new ReplaceFinancialStatementRowsDto
            {
                ExpectedVersionRevision = expectedRevision,
                Rows = preview.Definition.Rows
            },
            cancellationToken);
        layout = (await _layouts.GetLayoutAsync(
            layout.Id,
            cancellationToken))!;

        await _audit.RecordAsync(
            new FinanceAuditEventDto
            {
                EventType =
                    FinanceAuditEvents.FinancialStatementLayoutImported,
                TenantId = TenantId,
                SourceModule = "GENERAL_LEDGER",
                SourceDocumentType =
                    "FinancialStatementLayoutImport",
                SourceDocumentId = layout.Id,
                Resource = "Finance.FinancialStatementLayout",
                ResourceId = layout.Id.ToString(),
                AfterValues = new
                {
                    preview.DefinitionHash,
                    createdLayout,
                    layout.Id,
                    draftVersionId = draft.Id,
                    draft.VersionNumber,
                    rowCount = preview.RowCount,
                    mappingCount = preview.MappingCount
                }
            },
            cancellationToken);

        return new FinancialStatementLayoutImportResultDto
        {
            DefinitionHash = preview.DefinitionHash,
            CreatedLayout = createdLayout,
            LayoutId = layout.Id,
            DraftVersionId = draft.Id,
            DraftVersionNumber = draft.VersionNumber,
            DraftVersionRevision = draft.Revision,
            Layout = layout
        };
    }

    public async Task<FinancialStatementLayoutImportPreviewDto>
        PreviewWorkbookAsync(
            Stream stream,
            string fileName,
            CancellationToken cancellationToken = default)
    {
        var parsed = await ParseWorkbookAsync(
            stream,
            fileName,
            cancellationToken);
        var preview = await PreviewDefinitionAsync(
            parsed.Definition,
            cancellationToken);
        preview.Validation.Issues.InsertRange(
            0,
            parsed.Issues);
        return preview;
    }

    public async Task<FinancialStatementLayoutImportResultDto>
        CommitWorkbookAsync(
            Stream stream,
            string fileName,
            string expectedDefinitionHash,
            CancellationToken cancellationToken = default)
    {
        var preview = await PreviewWorkbookAsync(
            stream,
            fileName,
            cancellationToken);
        EnsureExpectedHash(
            expectedDefinitionHash,
            preview.DefinitionHash);
        ThrowForErrors(preview.Validation);
        return await CommitDefinitionAsync(
            new FinancialStatementLayoutImportCommitDto
            {
                Definition = preview.Definition,
                ExpectedDefinitionHash = expectedDefinitionHash
            },
            cancellationToken);
    }

    public async Task<FinancialStatementLayoutImportPreviewDto>
        PreviewLegacyMigrationAsync(
            LegacyFinancialStatementLayoutMigrationRequestDto request,
            CancellationToken cancellationToken = default)
    {
        var definition = await BuildLegacyDefinitionAsync(
            request,
            cancellationToken);
        var preview = await PreviewDefinitionAsync(
            definition,
            cancellationToken);
        preview.Validation.Issues.Add(
            new FinancialStatementLayoutValidationIssueDto
            {
                Severity =
                    FinancialStatementLayoutValidationSeverity.Information,
                Code = "FINANCE_REVIEW_REQUIRED",
                Message =
                    "The migrated layout is a draft. Finance must reconcile, validate, and publish it before production use."
            });
        if (request.IsDefault)
        {
            preview.Validation.Issues.Add(
                new FinancialStatementLayoutValidationIssueDto
                {
                    Severity =
                        FinancialStatementLayoutValidationSeverity.Warning,
                    Code = "DEFAULT_DEFERRED",
                    Message =
                        "Default status is deferred until finance has reviewed and published the migrated layout."
                });
        }
        return preview;
    }

    public async Task<FinancialStatementLayoutImportResultDto>
        CommitLegacyMigrationAsync(
            LegacyFinancialStatementLayoutMigrationCommitDto request,
            CancellationToken cancellationToken = default)
    {
        var preview = await PreviewLegacyMigrationAsync(
            request.Request,
            cancellationToken);
        EnsureExpectedHash(
            request.ExpectedDefinitionHash,
            preview.DefinitionHash);
        ThrowForErrors(preview.Validation);
        var result = await CommitDefinitionAsync(
            new FinancialStatementLayoutImportCommitDto
            {
                Definition = preview.Definition,
                ExpectedDefinitionHash =
                    request.ExpectedDefinitionHash
            },
            cancellationToken);

        await _audit.RecordAsync(
            new FinanceAuditEventDto
            {
                EventType =
                    FinanceAuditEvents
                        .FinancialStatementLegacyLayoutMigrated,
                TenantId = TenantId,
                SourceModule = "GENERAL_LEDGER",
                SourceDocumentType =
                    "LegacyFinancialStatementLayoutMigration",
                SourceDocumentId = result.LayoutId,
                Resource = "Finance.FinancialStatementLayout",
                ResourceId = result.LayoutId.ToString(),
                AfterValues = new
                {
                    result.DefinitionHash,
                    result.DraftVersionId,
                    result.DraftVersionNumber
                }
            },
            cancellationToken);

        return result;
    }

    private async Task ValidateImportMetadataAsync(
        FinancialStatementLayoutImportDefinitionDto definition,
        FinancialStatementLayoutValidationResultDto validation,
        CancellationToken cancellationToken)
    {
        if (!definition.TemplateVersion.Equals(
                TemplateVersion,
                StringComparison.Ordinal))
        {
            AddIssue(
                validation,
                "TEMPLATE_VERSION_INVALID",
                $"Template version must be '{TemplateVersion}'.");
        }
        if (string.IsNullOrWhiteSpace(definition.Code))
        {
            AddIssue(validation, "LAYOUT_CODE_REQUIRED", "Layout code is required.");
        }
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            AddIssue(validation, "LAYOUT_NAME_REQUIRED", "Layout name is required.");
        }
        if (!Enum.IsDefined(definition.StatementType))
        {
            AddIssue(
                validation,
                "STATEMENT_TYPE_INVALID",
                "Statement type must be BalanceSheet or IncomeStatement.");
        }
        if (definition.EffectiveFrom.HasValue &&
            definition.EffectiveTo.HasValue &&
            definition.EffectiveFrom.Value.Date >
            definition.EffectiveTo.Value.Date)
        {
            AddIssue(
                validation,
                "EFFECTIVE_DATES_INVALID",
                "Effective-from date cannot be later than effective-to date.");
        }
        if (!definition.TargetLayoutId.HasValue && definition.IsDefault)
        {
            AddIssue(
                validation,
                "IMPORT_DEFAULT_NOT_ALLOWED",
                "A newly imported draft cannot become the production default. Publish and reconcile it first, then update the layout metadata.");
        }
        if (definition.Rows.Count > MaximumRows)
        {
            AddIssue(
                validation,
                "TOO_MANY_ROWS",
                $"An import may contain at most {MaximumRows:N0} rows.");
        }
        if (definition.Rows.Sum(row => row.Mappings.Count) >
            MaximumMappings)
        {
            AddIssue(
                validation,
                "TOO_MANY_MAPPINGS",
                $"An import may contain at most {MaximumMappings:N0} mappings.");
        }
        foreach (var row in definition.Rows.Where(row =>
                     string.IsNullOrWhiteSpace(row.Label)))
        {
            AddIssue(
                validation,
                "ROW_LABEL_REQUIRED",
                "Every row requires a label.",
                row.RowCode);
        }

        var bookExists = definition.AccountingBookId != Guid.Empty &&
            await _context.AccountingBooks
                .AsNoTracking()
                .AnyAsync(book =>
                    book.Id == definition.AccountingBookId &&
                    book.TenantId == TenantId &&
                    book.IsActive &&
                    !book.IsDeleted,
                    cancellationToken);
        if (!bookExists)
        {
            AddIssue(
                validation,
                "ACCOUNTING_BOOK_INVALID",
                "The accounting book is inactive or does not belong to the current tenant.");
        }

        if (!definition.TargetLayoutId.HasValue)
        {
            if (definition.TargetVersionId.HasValue ||
                definition.SourceVersionId.HasValue ||
                definition.ExpectedTargetVersionRevision.HasValue)
            {
                AddIssue(
                    validation,
                    "TARGET_LAYOUT_REQUIRED",
                    "Target and source version controls require a target layout.");
            }
            var duplicate = !string.IsNullOrWhiteSpace(definition.Code) &&
                await _context.FinancialStatementLayouts
                    .AsNoTracking()
                    .AnyAsync(layout =>
                        layout.TenantId == TenantId &&
                        layout.Code == definition.Code &&
                        !layout.IsDeleted,
                        cancellationToken);
            if (duplicate)
            {
                AddIssue(
                    validation,
                    "LAYOUT_CODE_DUPLICATE",
                    $"Layout code '{definition.Code}' already exists. Specify that layout and a draft version as the import target.");
            }
            return;
        }

        var target = await _layouts.GetLayoutAsync(
            definition.TargetLayoutId.Value,
            cancellationToken);
        if (target == null)
        {
            AddIssue(
                validation,
                "TARGET_LAYOUT_INVALID",
                "The target layout does not belong to the current tenant.");
            return;
        }
        if (target.StatementType != definition.StatementType ||
            target.AccountingBookId != definition.AccountingBookId ||
            !target.Code.Equals(
                definition.Code,
                StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(
                validation,
                "TARGET_LAYOUT_MISMATCH",
                "The target layout code, statement type, and accounting book must match the import definition.");
        }

        var targetDrafts = target.Versions
            .Where(version =>
                version.Status ==
                FinancialStatementLayoutVersionStatus.Draft)
            .ToList();
        if (definition.TargetVersionId.HasValue &&
            definition.SourceVersionId.HasValue)
        {
            AddIssue(
                validation,
                "TARGET_SOURCE_CONFLICT",
                "SourceVersionId is used only when creating a new draft and cannot be combined with TargetVersionId.");
        }
        if (definition.SourceVersionId.HasValue &&
            target.Versions.All(version =>
                version.Id != definition.SourceVersionId.Value))
        {
            AddIssue(
                validation,
                "SOURCE_VERSION_INVALID",
                "The source version does not belong to the target layout.");
        }
        if (definition.TargetVersionId.HasValue)
        {
            var targetDraft = targetDrafts.SingleOrDefault(version =>
                version.Id == definition.TargetVersionId.Value);
            if (targetDraft == null)
            {
                AddIssue(
                    validation,
                    "TARGET_VERSION_INVALID",
                    "The target version is not a draft belonging to the target layout.");
            }
            else if (!definition.ExpectedTargetVersionRevision.HasValue)
            {
                AddIssue(
                    validation,
                    "TARGET_REVISION_REQUIRED",
                    "ExpectedTargetVersionRevision is required when replacing an existing draft.");
            }
            else if (targetDraft.Revision !=
                     definition.ExpectedTargetVersionRevision.Value)
            {
                AddIssue(
                    validation,
                    "TARGET_REVISION_CONFLICT",
                    "The target draft changed after the import definition was prepared.");
            }
        }
        else if (targetDrafts.Count > 0)
        {
            AddIssue(
                validation,
                "TARGET_DRAFT_EXISTS",
                "The target layout already has a draft. Specify its version id and expected revision to replace it.");
        }
    }

    private async Task<FinancialStatementLayoutImportDefinitionDto>
        BuildLegacyDefinitionAsync(
            LegacyFinancialStatementLayoutMigrationRequestDto request,
            CancellationToken cancellationToken)
    {
        var book = await _context.AccountingBooks
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.Id == request.AccountingBookId &&
                candidate.TenantId == TenantId &&
                candidate.IsActive &&
                !candidate.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The accounting book is inactive or does not belong to the current tenant.");

        var accounts = await _context.AccountAccountingBooks
            .AsNoTracking()
            .Where(mapping =>
                mapping.TenantId == TenantId &&
                mapping.AccountingBookId == book.Id &&
                mapping.IsEnabled &&
                !mapping.IsDeleted &&
                mapping.Account.TenantId == TenantId &&
                !mapping.Account.IsDeleted)
            .OrderBy(mapping => mapping.Account.AccountNumber)
            .Select(mapping => new LegacyAccount(
                mapping.AccountId,
                mapping.Account.AccountNumber,
                mapping.Account.AccountName,
                mapping.Account.AccountType,
                mapping.FinancialStatementLineItem,
                mapping.Account.IFRSLineItem,
                mapping.Account.BaseLineItem,
                mapping.Account.LocalLineItem))
            .ToListAsync(cancellationToken);

        var compatible = accounts
            .Where(account => IsCompatible(
                request.StatementType,
                account.AccountType))
            .ToList();
        var rows = BuildLegacyRows(
            compatible,
            request.StatementType,
            book.Code);

        return new FinancialStatementLayoutImportDefinitionDto
        {
            TemplateVersion = TemplateVersion,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            StatementType = request.StatementType,
            AccountingBookId = request.AccountingBookId,
            // Never alter the live default during draft migration.
            IsDefault = false,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? "Generated from legacy GL financial-statement line-item mappings."
                : request.Notes,
            Rows = rows
        };
    }

    private static List<FinancialStatementRowInputDto> BuildLegacyRows(
        IReadOnlyCollection<LegacyAccount> accounts,
        FinancialStatementType statementType,
        string bookCode)
    {
        var rows = new List<FinancialStatementRowInputDto>();
        var usedCodes = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var sections = statementType ==
                       FinancialStatementType.BalanceSheet
            ? new[]
            {
                (AccountType.Asset, "ASSETS", "Assets"),
                (AccountType.Liability, "LIABILITIES", "Liabilities"),
                (AccountType.Equity, "EQUITY", "Equity")
            }
            : new[]
            {
                (AccountType.Revenue, "REVENUE", "Revenue"),
                (AccountType.Expense, "EXPENSES", "Expenses")
            };

        var topOrder = 10;
        foreach (var (accountType, sectionCode, sectionLabel) in sections)
        {
            var sectionAccounts = accounts
                .Where(account => account.AccountType == accountType)
                .ToList();
            if (sectionAccounts.Count == 0)
            {
                continue;
            }

            rows.Add(new FinancialStatementRowInputDto
            {
                RowCode = sectionCode,
                Label = sectionLabel,
                RowType = FinancialStatementRowType.Header,
                DisplayOrder = topOrder,
                SignMultiplier = 1,
                IsVisible = true,
                IsBold = true
            });

            var childOrder = 10;
            var contributionCodes = new List<string>();
            foreach (var group in sectionAccounts
                         .GroupBy(account =>
                             ResolveLegacyLineItem(account, bookCode))
                         .OrderBy(group => group.Key,
                             StringComparer.OrdinalIgnoreCase))
            {
                var rowCode = UniqueCode(
                    $"{sectionCode}_{group.Key}",
                    usedCodes);
                contributionCodes.Add(rowCode);
                rows.Add(new FinancialStatementRowInputDto
                {
                    RowCode = rowCode,
                    ParentRowCode = sectionCode,
                    Label = group.Key,
                    RowType = FinancialStatementRowType.Account,
                    DisplayOrder = childOrder,
                    SignMultiplier = 1,
                    IsVisible = true,
                    ShowAccountDetails = true,
                    IndentLevel = 1,
                    Mappings = group
                        .OrderBy(account => account.AccountNumber)
                        .Select(account =>
                            new FinancialStatementRowMappingInputDto
                            {
                                MappingType =
                                    FinancialStatementRowMappingType.Account,
                                AccountId = account.Id
                            })
                        .ToList()
                });
                childOrder += 10;
            }

            var totalCode = UniqueCode(
                $"TOTAL_{sectionCode}",
                usedCodes);
            rows.Add(new FinancialStatementRowInputDto
            {
                RowCode = totalCode,
                Label = $"Total {sectionLabel}",
                RowType = FinancialStatementRowType.Formula,
                DisplayOrder = topOrder + 5,
                Formula = contributionCodes.Count == 1
                    ? contributionCodes[0]
                    : $"SUM({contributionCodes[0]}:{contributionCodes[^1]})",
                SignMultiplier = 1,
                IsVisible = true,
                IsBold = true,
                IsUnderlined = true
            });
            topOrder += 20;
        }

        return rows;
    }

    private async Task<ParsedWorkbook> ParseWorkbookAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (!fileName.EndsWith(
                ".xlsx",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only .xlsx financial-statement layout workbooks are accepted.");
        }

        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        if (memory.Length == 0 || memory.Length > MaximumFileBytes)
        {
            throw new InvalidOperationException(
                $"Workbook size must be between 1 byte and {MaximumFileBytes / 1024 / 1024} MB.");
        }

        var workbookBytes = memory.ToArray();
        using var package = new XLWorkbook(
            new MemoryStream(workbookBytes, writable: false));
        var issues = new List<
            FinancialStatementLayoutValidationIssueDto>();
        RejectWorkbookCode(package, workbookBytes, issues);

        IXLWorksheet? metadata = package.TryGetWorksheet(
            "Metadata",
            out var metadataSheet)
            ? metadataSheet
            : null;
        IXLWorksheet? rowsSheet = package.TryGetWorksheet(
            "Rows",
            out var rowsWorksheet)
            ? rowsWorksheet
            : null;
        IXLWorksheet? mappingsSheet = package.TryGetWorksheet(
            "Mappings",
            out var mappingsWorksheet)
            ? mappingsWorksheet
            : null;
        if (metadata == null)
        {
            AddIssue(
                issues,
                "MISSING_METADATA_SHEET",
                "Required worksheet 'Metadata' is missing.");
        }
        if (rowsSheet == null)
        {
            AddIssue(
                issues,
                "MISSING_ROWS_SHEET",
                "Required worksheet 'Rows' is missing.");
        }
        if (mappingsSheet == null)
        {
            AddIssue(
                issues,
                "MISSING_MAPPINGS_SHEET",
                "Required worksheet 'Mappings' is missing.");
        }

        var definition = metadata == null
            ? new FinancialStatementLayoutImportDefinitionDto()
            : ReadMetadata(metadata, issues);
        if (rowsSheet != null)
        {
            definition.Rows = ReadRows(rowsSheet, issues);
        }
        if (mappingsSheet != null)
        {
            await ReadMappingsAsync(
                mappingsSheet,
                definition,
                issues,
                cancellationToken);
        }

        return new ParsedWorkbook(definition, issues);
    }

    private static FinancialStatementLayoutImportDefinitionDto ReadMetadata(
        IXLWorksheet sheet,
        List<FinancialStatementLayoutValidationIssueDto> issues)
    {
        var columns = HeaderColumns(sheet);
        var definition =
            new FinancialStatementLayoutImportDefinitionDto
            {
                TemplateVersion = Cell(
                    sheet,
                    2,
                    columns,
                    "TemplateVersion"),
                Code = Cell(sheet, 2, columns, "Code"),
                Name = Cell(sheet, 2, columns, "Name"),
                Description = NullIfBlank(
                    Cell(sheet, 2, columns, "Description")),
                Notes = NullIfBlank(
                    Cell(sheet, 2, columns, "Notes"))
            };

        definition.TargetLayoutId = ParseOptionalGuid(
            Cell(sheet, 2, columns, "TargetLayoutId"),
            issues,
            "TARGET_LAYOUT_ID_INVALID");
        definition.TargetVersionId = ParseOptionalGuid(
            Cell(sheet, 2, columns, "TargetVersionId"),
            issues,
            "TARGET_VERSION_ID_INVALID");
        definition.SourceVersionId = ParseOptionalGuid(
            Cell(sheet, 2, columns, "SourceVersionId"),
            issues,
            "SOURCE_VERSION_ID_INVALID");
        var bookText = Cell(
            sheet,
            2,
            columns,
            "AccountingBookId");
        if (!Guid.TryParse(bookText, out var bookId))
        {
            AddIssue(
                issues,
                "ACCOUNTING_BOOK_ID_INVALID",
                "AccountingBookId must be a valid GUID.");
        }
        definition.AccountingBookId = bookId;
        if (!Enum.TryParse<FinancialStatementType>(
                Cell(sheet, 2, columns, "StatementType"),
                true,
                out var statementType))
        {
            AddIssue(
                issues,
                "STATEMENT_TYPE_INVALID",
                "StatementType must be BalanceSheet or IncomeStatement.");
        }
        definition.StatementType = statementType;
        definition.IsDefault = ParseBool(
            Cell(sheet, 2, columns, "IsDefault"),
            false,
            issues,
            "IS_DEFAULT_INVALID");
        definition.EffectiveFrom = ParseOptionalDate(
            Cell(sheet, 2, columns, "EffectiveFrom"),
            issues,
            "EFFECTIVE_FROM_INVALID");
        definition.EffectiveTo = ParseOptionalDate(
            Cell(sheet, 2, columns, "EffectiveTo"),
            issues,
            "EFFECTIVE_TO_INVALID");
        var revisionText = Cell(
            sheet,
            2,
            columns,
            "ExpectedTargetVersionRevision");
        if (!string.IsNullOrWhiteSpace(revisionText))
        {
            if (int.TryParse(revisionText, out var revision) &&
                revision > 0)
            {
                definition.ExpectedTargetVersionRevision = revision;
            }
            else
            {
                AddIssue(
                    issues,
                    "TARGET_REVISION_INVALID",
                    "ExpectedTargetVersionRevision must be a positive whole number.");
            }
        }
        return definition;
    }

    private static List<FinancialStatementRowInputDto> ReadRows(
        IXLWorksheet sheet,
        List<FinancialStatementLayoutValidationIssueDto> issues)
    {
        var columns = HeaderColumns(sheet);
        var rows = new List<FinancialStatementRowInputDto>();
        if ((sheet.LastRowUsed()?.RowNumber() ?? 1) > MaximumRows + 1)
        {
            AddIssue(
                issues,
                "TOO_MANY_ROWS",
                $"A workbook may contain at most {MaximumRows:N0} statement rows.");
        }
        var lastRow = Math.Min(
            sheet.LastRowUsed()?.RowNumber() ?? 1,
            MaximumRows + 1);
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var rowCode = Cell(
                sheet,
                rowNumber,
                columns,
                "RowCode");
            if (string.IsNullOrWhiteSpace(rowCode) &&
                string.IsNullOrWhiteSpace(
                    Cell(sheet, rowNumber, columns, "Label")))
            {
                continue;
            }

            Enum.TryParse<FinancialStatementRowType>(
                Cell(sheet, rowNumber, columns, "RowType"),
                true,
                out var rowType);
            int.TryParse(
                Cell(sheet, rowNumber, columns, "DisplayOrder"),
                out var displayOrder);
            var signMultiplier = 1;
            int.TryParse(
                Cell(sheet, rowNumber, columns, "SignMultiplier"),
                out signMultiplier);
            if (signMultiplier == 0)
            {
                signMultiplier = 1;
            }
            int.TryParse(
                Cell(sheet, rowNumber, columns, "IndentLevel"),
                out var indentLevel);

            rows.Add(new FinancialStatementRowInputDto
            {
                RowCode = rowCode,
                ParentRowCode = NullIfBlank(
                    Cell(
                        sheet,
                        rowNumber,
                        columns,
                        "ParentRowCode")),
                Label = Cell(sheet, rowNumber, columns, "Label"),
                RowType = rowType,
                DisplayOrder = displayOrder,
                Formula = NullIfBlank(
                    Cell(sheet, rowNumber, columns, "Formula")),
                SignMultiplier = signMultiplier,
                IsVisible = ParseBool(
                    Cell(sheet, rowNumber, columns, "IsVisible"),
                    true,
                    issues,
                    "IS_VISIBLE_INVALID",
                    rowCode),
                SuppressIfZero = ParseBool(
                    Cell(
                        sheet,
                        rowNumber,
                        columns,
                        "SuppressIfZero"),
                    false,
                    issues,
                    "SUPPRESS_IF_ZERO_INVALID",
                    rowCode),
                ShowAccountDetails = ParseBool(
                    Cell(
                        sheet,
                        rowNumber,
                        columns,
                        "ShowAccountDetails"),
                    false,
                    issues,
                    "SHOW_ACCOUNT_DETAILS_INVALID",
                    rowCode),
                IsBold = ParseBool(
                    Cell(sheet, rowNumber, columns, "IsBold"),
                    false,
                    issues,
                    "IS_BOLD_INVALID",
                    rowCode),
                IsItalic = ParseBool(
                    Cell(sheet, rowNumber, columns, "IsItalic"),
                    false,
                    issues,
                    "IS_ITALIC_INVALID",
                    rowCode),
                IsUnderlined = ParseBool(
                    Cell(
                        sheet,
                        rowNumber,
                        columns,
                        "IsUnderlined"),
                    false,
                    issues,
                    "IS_UNDERLINED_INVALID",
                    rowCode),
                IndentLevel = indentLevel
            });
        }
        return rows;
    }

    private async Task ReadMappingsAsync(
        IXLWorksheet sheet,
        FinancialStatementLayoutImportDefinitionDto definition,
        List<FinancialStatementLayoutValidationIssueDto> issues,
        CancellationToken cancellationToken)
    {
        var columns = HeaderColumns(sheet);
        if ((sheet.LastRowUsed()?.RowNumber() ?? 1) > MaximumMappings + 1)
        {
            AddIssue(
                issues,
                "TOO_MANY_MAPPINGS",
                $"A workbook may contain at most {MaximumMappings:N0} mappings.");
        }
        var lastRow = Math.Min(
            sheet.LastRowUsed()?.RowNumber() ?? 1,
            MaximumMappings + 1);
        var accountNumbers = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var rawMappings = new List<RawMapping>();
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var rowCode = Cell(
                sheet,
                rowNumber,
                columns,
                "RowCode");
            if (string.IsNullOrWhiteSpace(rowCode))
            {
                continue;
            }
            Enum.TryParse<FinancialStatementRowMappingType>(
                Cell(sheet, rowNumber, columns, "MappingType"),
                true,
                out var mappingType);
            var accountNumber = NullIfBlank(
                Cell(sheet, rowNumber, columns, "AccountNumber"));
            if (accountNumber != null)
            {
                accountNumbers.Add(accountNumber);
            }
            rawMappings.Add(new RawMapping(
                rowCode,
                mappingType,
                accountNumber,
                NullIfBlank(
                    Cell(
                        sheet,
                        rowNumber,
                        columns,
                        "FromAccountNumber")),
                NullIfBlank(
                    Cell(
                        sheet,
                        rowNumber,
                        columns,
                        "ToAccountNumber"))));
        }

        var accounts = definition.AccountingBookId == Guid.Empty
            ? new Dictionary<string, Guid>(
                StringComparer.OrdinalIgnoreCase)
            : await _context.AccountAccountingBooks
                .AsNoTracking()
                .Where(mapping =>
                    mapping.TenantId == TenantId &&
                    mapping.AccountingBookId ==
                    definition.AccountingBookId &&
                    mapping.IsEnabled &&
                    !mapping.IsDeleted &&
                    accountNumbers.Contains(
                        mapping.Account.AccountNumber))
                .Select(mapping => new
                {
                    mapping.Account.AccountNumber,
                    mapping.AccountId
                })
                .ToDictionaryAsync(
                    account => account.AccountNumber,
                    account => account.AccountId,
                    StringComparer.OrdinalIgnoreCase,
                    cancellationToken);

        var rowsByCode = definition.Rows
            .GroupBy(row => row.RowCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
        foreach (var raw in rawMappings)
        {
            if (!rowsByCode.TryGetValue(raw.RowCode, out var row))
            {
                AddIssue(
                    issues,
                    "MAPPING_ROW_UNKNOWN",
                    $"Mapping references unknown row '{raw.RowCode}'.",
                    raw.RowCode);
                continue;
            }

            Guid? accountId = null;
            if (raw.MappingType is
                    FinancialStatementRowMappingType.Account or
                    FinancialStatementRowMappingType.AccountHierarchy)
            {
                if (raw.AccountNumber == null ||
                    !accounts.TryGetValue(
                        raw.AccountNumber,
                        out var resolvedAccountId))
                {
                    AddIssue(
                        issues,
                        "MAPPING_ACCOUNT_UNKNOWN",
                        $"Account '{raw.AccountNumber}' is not enabled for the selected tenant accounting book.",
                        raw.RowCode);
                }
                else
                {
                    accountId = resolvedAccountId;
                }
            }

            row.Mappings.Add(
                new FinancialStatementRowMappingInputDto
                {
                    MappingType = raw.MappingType,
                    AccountId = accountId,
                    FromAccountNumber = raw.FromAccountNumber,
                    ToAccountNumber = raw.ToAccountNumber
                });
        }
    }

    private async Task AddLookupsSheetAsync(
        XLWorkbook package,
        CancellationToken cancellationToken)
    {
        var sheet = package.Worksheets.Add("Lookups");
        WriteHeaders(
            sheet,
            new[]
            {
                "AccountingBookId",
                "AccountingBookCode",
                "AccountingBookName",
                "AccountNumber",
                "AccountName",
                "AccountType"
            });
        var books = await _context.AccountingBooks
            .AsNoTracking()
            .Where(book =>
                book.TenantId == TenantId &&
                book.IsActive &&
                !book.IsDeleted)
            .OrderBy(book => book.SortOrder)
            .ToListAsync(cancellationToken);
        var accounts = await _context.Accounts
            .AsNoTracking()
            .Where(account =>
                account.TenantId == TenantId &&
                !account.IsDeleted)
            .OrderBy(account => account.AccountNumber)
            .ToListAsync(cancellationToken);
        var count = Math.Max(books.Count, accounts.Count);
        for (var index = 0; index < count; index++)
        {
            var row = index + 2;
            if (index < books.Count)
            {
                sheet.Cell(row, 1).Value = books[index].Id.ToString();
                sheet.Cell(row, 2).Value = books[index].Code;
                sheet.Cell(row, 3).Value = books[index].Name;
            }
            if (index < accounts.Count)
            {
                sheet.Cell(row, 4).Value =
                    accounts[index].AccountNumber;
                sheet.Cell(row, 5).Value =
                    accounts[index].AccountName;
                sheet.Cell(row, 6).Value =
                    accounts[index].AccountType.ToString();
            }
        }
        if (sheet.LastCellUsed() != null)
        {
            sheet.ColumnsUsed().AdjustToContents(12, 45);
        }
    }

    private static void AddInstructionsSheet(XLWorkbook package)
    {
        var sheet = package.Worksheets.Add("Instructions");
        var instructions = new[]
        {
            ("Template version", TemplateVersion),
            ("Purpose", "Create or replace a draft financial-statement layout definition."),
            ("Workflow", "Preview first. Commit with the returned definition hash. Imported versions remain Draft."),
            ("Required sheets", "Metadata, Rows, Mappings"),
            ("Safety", "Do not rename sheets or headers. Formulas in workbook cells, macros, and external links are rejected."),
            ("Row formula syntax", "Formula text may contain row references, parentheses, +/-, and SUM(FIRST:LAST)."),
            ("Mappings", "Use AccountNumber for Account or AccountHierarchy mappings; use FromAccountNumber and ToAccountNumber for AccountRange."),
            ("Production", "Import never publishes a version or makes a new layout the default.")
        };
        for (var index = 0; index < instructions.Length; index++)
        {
            sheet.Cell(index + 1, 1).Value =
                instructions[index].Item1;
            sheet.Cell(index + 1, 2).Value =
                instructions[index].Item2;
        }
        sheet.Range(1, 1, instructions.Length, 1).Style.Font.Bold =
            true;
        sheet.ColumnsUsed().AdjustToContents(18, 100);
    }

    private static void AddMetadataSheet(XLWorkbook package)
    {
        var sheet = package.Worksheets.Add("Metadata");
        WriteHeaders(
            sheet,
            new[]
            {
                "TemplateVersion",
                "TargetLayoutId",
                "TargetVersionId",
                "SourceVersionId",
                "ExpectedTargetVersionRevision",
                "Code",
                "Name",
                "Description",
                "StatementType",
                "AccountingBookId",
                "IsDefault",
                "EffectiveFrom",
                "EffectiveTo",
                "Notes"
            });
        sheet.Cell(2, 1).Value = TemplateVersion;
        sheet.Cell(2, 9).Value = "BalanceSheet";
        sheet.Cell(2, 11).Value = false;
        sheet.Range(2, 12, 2, 13).Style.DateFormat.Format =
            "yyyy-mm-dd";
    }

    private static void AddRowsSheet(XLWorkbook package)
    {
        var sheet = package.Worksheets.Add("Rows");
        WriteHeaders(
            sheet,
            new[]
            {
                "RowCode",
                "ParentRowCode",
                "Label",
                "RowType",
                "DisplayOrder",
                "Formula",
                "SignMultiplier",
                "IsVisible",
                "SuppressIfZero",
                "ShowAccountDetails",
                "IsBold",
                "IsItalic",
                "IsUnderlined",
                "IndentLevel"
            });
        sheet.Cell(2, 4).Value = "Header";
        sheet.Cell(2, 7).Value = 1;
        sheet.Cell(2, 8).Value = true;
    }

    private static void AddMappingsSheet(XLWorkbook package)
    {
        var sheet = package.Worksheets.Add("Mappings");
        WriteHeaders(
            sheet,
            new[]
            {
                "RowCode",
                "MappingType",
                "AccountNumber",
                "FromAccountNumber",
                "ToAccountNumber"
            });
        sheet.Cell(2, 2).Value = "Account";
    }

    private static void RejectWorkbookCode(
        XLWorkbook package,
        byte[] workbookBytes,
        List<FinancialStatementLayoutValidationIssueDto> issues)
    {
        foreach (var sheet in package.Worksheets)
        {
            foreach (var cell in sheet.CellsUsed())
            {
                if (cell.HasFormula)
                {
                    AddIssue(
                        issues,
                        "WORKBOOK_FORMULA_NOT_ALLOWED",
                        $"Workbook formula at {sheet.Name}!{cell.Address} is not allowed.");
                }
            }
        }
        if (SpreadsheetSecurityInspector.HasExternalRelationships(
                workbookBytes))
        {
            AddIssue(
                issues,
                "EXTERNAL_LINK_NOT_ALLOWED",
                "External workbook links are not allowed.");
        }
        if (SpreadsheetSecurityInspector.HasVbaProject(workbookBytes))
        {
            AddIssue(
                issues,
                "MACRO_NOT_ALLOWED",
                "Macro-enabled workbooks are not allowed.");
        }
    }

    private static FinancialStatementLayoutImportDefinitionDto
        NormalizeDefinition(
            FinancialStatementLayoutImportDefinitionDto source)
        => new()
        {
            TemplateVersion = string.IsNullOrWhiteSpace(
                source.TemplateVersion)
                ? TemplateVersion
                : source.TemplateVersion.Trim(),
            TargetLayoutId = NonEmpty(source.TargetLayoutId),
            TargetVersionId = NonEmpty(source.TargetVersionId),
            SourceVersionId = NonEmpty(source.SourceVersionId),
            ExpectedTargetVersionRevision =
                source.ExpectedTargetVersionRevision,
            Code = NormalizeCode(source.Code),
            Name = source.Name?.Trim() ?? string.Empty,
            Description = NullIfBlank(source.Description),
            StatementType = source.StatementType,
            AccountingBookId = source.AccountingBookId,
            IsDefault = source.IsDefault,
            EffectiveFrom = source.EffectiveFrom?.Date,
            EffectiveTo = source.EffectiveTo?.Date,
            Notes = NullIfBlank(source.Notes),
            Rows = (source.Rows ?? new List<
                    FinancialStatementRowInputDto>())
                .Select(row =>
                    new FinancialStatementRowInputDto
                    {
                        RowCode = NormalizeCode(row.RowCode),
                        ParentRowCode = string.IsNullOrWhiteSpace(
                            row.ParentRowCode)
                            ? null
                            : NormalizeCode(row.ParentRowCode),
                        Label = row.Label?.Trim() ?? string.Empty,
                        RowType = row.RowType,
                        DisplayOrder = row.DisplayOrder,
                        Formula = NullIfBlank(row.Formula),
                        SignMultiplier = row.SignMultiplier,
                        IsVisible = row.IsVisible,
                        SuppressIfZero = row.SuppressIfZero,
                        ShowAccountDetails =
                            row.ShowAccountDetails,
                        IsBold = row.IsBold,
                        IsItalic = row.IsItalic,
                        IsUnderlined = row.IsUnderlined,
                        IndentLevel = row.IndentLevel,
                        Mappings = (row.Mappings ??
                                    new List<
                                        FinancialStatementRowMappingInputDto>())
                            .Select(mapping =>
                                new FinancialStatementRowMappingInputDto
                                {
                                    MappingType =
                                        mapping.MappingType,
                                    AccountId =
                                        NonEmpty(mapping.AccountId),
                                    FromAccountNumber =
                                        NullIfBlank(
                                            mapping
                                                .FromAccountNumber),
                                    ToAccountNumber =
                                        NullIfBlank(
                                            mapping
                                                .ToAccountNumber)
                                })
                            .ToList()
                    })
                .ToList()
        };

    private static string ComputeHash(
        FinancialStatementLayoutImportDefinitionDto definition)
        => Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(
                        definition,
                        HashJsonOptions))));

    private static void EnsureExpectedHash(
        string? expected,
        string actual)
    {
        if (string.IsNullOrWhiteSpace(expected) ||
            expected.Length != actual.Length ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(
                    expected.ToUpperInvariant()),
                Encoding.ASCII.GetBytes(actual)))
        {
            throw new InvalidOperationException(
                "The import definition changed after preview. Preview it again before committing.");
        }
    }

    private static void ThrowForErrors(
        FinancialStatementLayoutValidationResultDto validation)
    {
        if (validation.Issues.Any(issue =>
                issue.Severity ==
                FinancialStatementLayoutValidationSeverity.Error))
        {
            throw new FinancialStatementLayoutValidationException(
                validation);
        }
    }

    private static string ResolveLegacyLineItem(
        LegacyAccount account,
        string bookCode)
    {
        var lineItem = NullIfBlank(account.BookLineItem);
        if (lineItem != null)
        {
            return lineItem;
        }
        var normalizedBook = bookCode.Trim().ToUpperInvariant();
        return NullIfBlank(normalizedBook switch
            {
                "LOCAL_STATUTORY" or "BASE" or "LOCAL" =>
                    account.BaseLineItem,
                "MANAGEMENT" => account.LocalLineItem,
                _ => account.IfrsLineItem
            })
            ?? $"Unclassified {account.AccountType}";
    }

    private static string UniqueCode(
        string source,
        ISet<string> used)
    {
        var baseCode = NormalizeCode(source);
        if (baseCode.Length > 42)
        {
            baseCode = baseCode[..42];
        }
        if (string.IsNullOrWhiteSpace(baseCode))
        {
            baseCode = "ROW";
        }
        var candidate = baseCode;
        var suffix = 2;
        while (!used.Add(candidate))
        {
            candidate = $"{baseCode}_{suffix++}";
        }
        return candidate;
    }

    private static string NormalizeCode(string? value)
    {
        var normalized = InvalidCodeCharacters.Replace(
            value?.Trim().ToUpperInvariant() ?? string.Empty,
            "_");
        return normalized.Trim('_');
    }

    private static bool IsCompatible(
        FinancialStatementType statementType,
        AccountType accountType)
        => statementType switch
        {
            FinancialStatementType.BalanceSheet =>
                accountType is AccountType.Asset or
                    AccountType.Liability or AccountType.Equity,
            FinancialStatementType.IncomeStatement =>
                accountType is AccountType.Revenue or
                    AccountType.Expense,
            _ => false
        };

    private static string BuildImportNotes(string? notes)
        => string.IsNullOrWhiteSpace(notes)
            ? "Imported through controlled financial-statement layout import."
            : $"{notes.Trim()}\nImported through controlled financial-statement layout import.";

    private static void WriteHeaders(
        IXLWorksheet sheet,
        IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
        }
        var range = sheet.Range(1, 1, 1, headers.Count);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromArgb(31, 78, 121);
        range.Style.Font.FontColor = XLColor.White;
        sheet.SheetView.FreezeRows(1);
        range.SetAutoFilter();
        sheet.Columns(1, headers.Count).AdjustToContents(1, 2, 12, 40);
    }

    private static Dictionary<string, int> HeaderColumns(
        IXLWorksheet sheet)
    {
        var result = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (var column = 1; column <= lastColumn; column++)
        {
            var header = sheet.Cell(1, column).GetFormattedString().Trim();
            if (!string.IsNullOrWhiteSpace(header))
            {
                result[header] = column;
            }
        }
        return result;
    }

    private static string Cell(
        IXLWorksheet sheet,
        int row,
        IReadOnlyDictionary<string, int> columns,
        string name)
        => columns.TryGetValue(name, out var column)
            ? sheet.Cell(row, column).GetFormattedString().Trim()
            : string.Empty;

    private static Guid? ParseOptionalGuid(
        string value,
        List<FinancialStatementLayoutValidationIssueDto> issues,
        string code)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (Guid.TryParse(value, out var parsed) &&
            parsed != Guid.Empty)
        {
            return parsed;
        }
        AddIssue(issues, code, "The value must be a valid GUID.");
        return null;
    }

    private static DateTime? ParseOptionalDate(
        string value,
        List<FinancialStatementLayoutValidationIssueDto> issues,
        string code)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (DateTime.TryParse(value, out var parsed))
        {
            return parsed.Date;
        }
        AddIssue(issues, code, "The value must be a valid date.");
        return null;
    }

    private static bool ParseBool(
        string value,
        bool fallback,
        List<FinancialStatementLayoutValidationIssueDto> issues,
        string code,
        string? rowCode = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }
        if (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Y", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (value.Equals("0", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("NO", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("N", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        AddIssue(
            issues,
            code,
            "The value must be true or false.",
            rowCode);
        return fallback;
    }

    private static Guid? NonEmpty(Guid? value)
        => value.HasValue && value.Value != Guid.Empty
            ? value
            : null;

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static void AddIssue(
        FinancialStatementLayoutValidationResultDto validation,
        string code,
        string message,
        string? rowCode = null)
        => validation.Issues.Add(
            new FinancialStatementLayoutValidationIssueDto
            {
                Severity =
                    FinancialStatementLayoutValidationSeverity.Error,
                Code = code,
                Message = message,
                RowCode = rowCode
            });

    private static void AddIssue(
        ICollection<FinancialStatementLayoutValidationIssueDto> issues,
        string code,
        string message,
        string? rowCode = null)
        => issues.Add(
            new FinancialStatementLayoutValidationIssueDto
            {
                Severity =
                    FinancialStatementLayoutValidationSeverity.Error,
                Code = code,
                Message = message,
                RowCode = rowCode
            });

    private sealed record ParsedWorkbook(
        FinancialStatementLayoutImportDefinitionDto Definition,
        List<FinancialStatementLayoutValidationIssueDto> Issues);

    private sealed record RawMapping(
        string RowCode,
        FinancialStatementRowMappingType MappingType,
        string? AccountNumber,
        string? FromAccountNumber,
        string? ToAccountNumber);

    private sealed record LegacyAccount(
        Guid Id,
        string AccountNumber,
        string AccountName,
        AccountType AccountType,
        string? BookLineItem,
        string? IfrsLineItem,
        string? BaseLineItem,
        string? LocalLineItem);
}

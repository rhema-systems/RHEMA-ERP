using System.Globalization;
using System.Text.RegularExpressions;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Api.Services.HR.EmployeeImport;

/// <summary>What kind of cell a template column expects. Drives validation in Excel and in the reader.</summary>
public enum EmployeeImportColumnKind
{
    Text = 1,
    Date = 2,
    WholeNumber = 3,
    Money = 4,
    List = 5,
}

/// <summary>One column of the <c>Employees</c> sheet.</summary>
/// <param name="Key">Stable identifier used in JSON and findings; never shown to the user.</param>
/// <param name="Header">What the sheet prints. Required columns get a trailing <c> *</c> when rendered.</param>
/// <param name="TextFormat">Force the <c>@</c> number format so leading zeros survive.</param>
public sealed record EmployeeImportColumn(
    string Key,
    string Header,
    bool Required,
    EmployeeImportColumnKind Kind,
    string Help,
    string? ListName = null,
    bool TextFormat = false,
    double Width = 18);

/// <summary>
/// The column catalogue of the employee import template — the one place that says what the sheet
/// holds. The template builder, the reader and the wizard's guide all read from here.
/// </summary>
/// <remarks>
/// ⚠ Identification columns are NOT in the fixed list. They are generated per tenant from
/// <see cref="IdentificationType"/> (decision 4, 2026-09-03) and bound by type id through the hidden
/// <c>_meta</c> sheet, so nothing here ever says "Ghana Card".
/// </remarks>
public static class EmployeeImportColumns
{
    public const int TemplateVersion = 1;
    public const string EmployeesSheet = "Employees";
    public const string MetaSheet = "_meta";
    public const string ListsSheet = "Lists";
    public const string ReadMeSheet = "Read Me";
    public const int MaxDataRows = 10_000;
    public const long MaxFileBytes = 15_728_640; // 15 MB, as the BoQ importer
    public const string ExampleStaffNumber = "EXAMPLE-001";

    // Keys — referenced by the reader, the committer and the follow-up list.
    public const string StaffNumber = "StaffNumber";
    public const string Title = "Title";
    public const string FirstName = "FirstName";
    public const string MiddleName = "MiddleName";
    public const string Surname = "Surname";
    public const string Gender = "Gender";
    public const string DateOfBirth = "DateOfBirth";
    public const string MaritalStatus = "MaritalStatus";
    public const string EmploymentType = "EmploymentType";
    public const string DateEmployed = "DateEmployed";
    public const string ContractEndDate = "ContractEndDate";
    public const string Department = "Department";
    public const string Section = "Section";
    public const string Position = "Position";
    public const string Location = "Location";
    public const string ManagerStaffNumber = "ManagerStaffNumber";
    public const string SalaryLevel = "SalaryLevel";
    public const string Notch = "Notch";
    public const string MonthlyBasicSalary = "MonthlyBasicSalary";
    public const string OnPayroll = "OnPayroll";
    public const string OffPayrollReason = "OffPayrollReason";
    public const string Email = "Email";
    public const string MobileNumber = "MobileNumber";
    public const string Telephone = "Telephone";
    public const string SsnitNumber = "SsnitNumber";
    public const string Tin = "Tin";
    public const string DigitalAddress = "DigitalAddress";
    public const string ResidentialAddress = "ResidentialAddress";
    public const string City = "City";
    public const string Region = "Region";
    public const string Hometown = "Hometown";
    public const string Religion = "Religion";
    public const string HighestQualification = "HighestQualification";
    public const string Institution = "Institution";
    public const string YearCompleted = "YearCompleted";
    public const string ProfessionalQualification = "ProfessionalQualification";
    public const string Notes = "Notes";

    // Columns the checked file adds; the reader ignores them without a warning.
    public const string ResultHeader = "Result";
    public const string FindingsHeader = "Findings";

    public const string ListGender = "Gender";
    public const string ListMaritalStatus = "MaritalStatus";
    public const string ListEmploymentType = "EmploymentType";
    public const string ListYesNo = "YesNo";
    public const string ListOffPayrollReason = "OffPayrollReason";

    private static readonly EmployeeImportColumn[] Fixed =
    [
        new(StaffNumber, "Staff Number", true, EmployeeImportColumnKind.Text,
            "The number the employee already has. Kept exactly as typed. Must be unique in the file and in the system.",
            TextFormat: true, Width: 16),
        new(Title, "Title", false, EmployeeImportColumnKind.Text, "Mr, Mrs, Ms, Dr, Col. (Rtd) ...", Width: 10),
        new(FirstName, "First Name", true, EmployeeImportColumnKind.Text, ""),
        new(MiddleName, "Middle Name(s)", false, EmployeeImportColumnKind.Text, "Initials are acceptable."),
        new(Surname, "Surname", true, EmployeeImportColumnKind.Text, ""),
        new(Gender, "Gender", true, EmployeeImportColumnKind.List, "", ListGender, Width: 12),
        new(DateOfBirth, "Date of Birth", false, EmployeeImportColumnKind.Date,
            "A real date cell (dd/mm/yyyy). Warned if under 18 or over 70 at Date Employed.", Width: 14),
        new(MaritalStatus, "Marital Status", false, EmployeeImportColumnKind.List, "", ListMaritalStatus, Width: 14),
        new(EmploymentType, "Employment Type", true, EmployeeImportColumnKind.List,
            "Decides which staff-number register the number is checked against.", ListEmploymentType, Width: 16),
        new(DateEmployed, "Date Employed", true, EmployeeImportColumnKind.Date, "Start date with the organisation.", Width: 14),
        new(ContractEndDate, "Contract End Date", false, EmployeeImportColumnKind.Date,
            "Required when Employment Type is Contract or FixedTerm.", Width: 16),
        new(Department, "Department", true, EmployeeImportColumnKind.Text,
            "Department code or exact name from the Departments sheet.", Width: 24),
        new(Section, "Section", false, EmployeeImportColumnKind.Text,
            "Section code or name. Must belong to the Department.", Width: 20),
        new(Position, "Position", true, EmployeeImportColumnKind.Text,
            "Position code or exact title from the Positions sheet. The organisation unit is taken from the position.", Width: 28),
        // Required: the create path refuses an employee without a location ("LocationId is required
        // for employee assignment"), found by the smoke harness on the first commit, not by reading.
        new(Location, "Location", true, EmployeeImportColumnKind.Text,
            "Location code or name from the Locations sheet. Every employee is placed somewhere.", Width: 18),
        new(ManagerStaffNumber, "Manager Staff Number", false, EmployeeImportColumnKind.Text,
            "Staff number of the line manager. May be another row in this file.", TextFormat: true, Width: 18),
        new(SalaryLevel, "Salary Level", false, EmployeeImportColumnKind.Text,
            "e.g. M2, S1 — from the Salary Structure sheet.", Width: 12),
        new(Notch, "Notch", false, EmployeeImportColumnKind.WholeNumber,
            "Notch number within the level, e.g. 4 (not N4).", Width: 8),
        new(MonthlyBasicSalary, "Monthly Basic Salary", false, EmployeeImportColumnKind.Money,
            "Monthly basic, in GHS. Leave blank when On Payroll is No.", Width: 18),
        new(OnPayroll, "On Payroll", false, EmployeeImportColumnKind.List,
            "Defaults to Yes. When No, give an Off-Payroll Reason and leave salary blank.", ListYesNo, Width: 10),
        new(OffPayrollReason, "Off-Payroll Reason", false, EmployeeImportColumnKind.List, "", ListOffPayrollReason, Width: 22),
        new(Email, "Email", false, EmployeeImportColumnKind.Text,
            "Work or personal email. Optional; unique when given.", Width: 28),
        new(MobileNumber, "Mobile Number", false, EmployeeImportColumnKind.Text,
            "Typed as text so leading zeros survive.", TextFormat: true, Width: 16),
        new(Telephone, "Telephone", false, EmployeeImportColumnKind.Text, "", TextFormat: true, Width: 16),
        new(SsnitNumber, "SSNIT Number", false, EmployeeImportColumnKind.Text, "", TextFormat: true, Width: 16),
        new(Tin, "TIN", false, EmployeeImportColumnKind.Text, "", TextFormat: true, Width: 14),
        // ── identification columns are inserted here at build time ──
        new(DigitalAddress, "Digital Address", false, EmployeeImportColumnKind.Text, "GhanaPost GPS, e.g. GA-123-4567", Width: 16),
        new(ResidentialAddress, "Residential Address", false, EmployeeImportColumnKind.Text, "", Width: 30),
        new(City, "City/Town", false, EmployeeImportColumnKind.Text, "", Width: 16),
        new(Region, "Region", false, EmployeeImportColumnKind.Text, "", Width: 16),
        new(Hometown, "Hometown", false, EmployeeImportColumnKind.Text, "", Width: 16),
        new(Religion, "Religion", false, EmployeeImportColumnKind.Text, "", Width: 14),
        new(HighestQualification, "Highest Qualification", false, EmployeeImportColumnKind.Text,
            "e.g. Master of Business Administration in Finance", Width: 32),
        new(Institution, "Institution", false, EmployeeImportColumnKind.Text,
            "Awarding institution. Warned if blank while a qualification is given.", Width: 28),
        new(YearCompleted, "Year Completed", false, EmployeeImportColumnKind.WholeNumber, "", Width: 10),
        new(ProfessionalQualification, "Professional Qualification", false, EmployeeImportColumnKind.Text,
            "e.g. ICAG Certification. One per cell.", Width: 32),
        new(Notes, "Notes", false, EmployeeImportColumnKind.Text, "Anything the importer should keep on the profile.", Width: 40),
    ];

    public static IReadOnlyDictionary<string, string[]> Lists { get; } = new Dictionary<string, string[]>
    {
        [ListGender] = Enum.GetNames<Gender>(),
        [ListMaritalStatus] = Enum.GetNames<MaritalStatus>(),
        [ListEmploymentType] = Enum.GetNames<EmploymentType>(),
        [ListYesNo] = ["Yes", "No"],
        [ListOffPayrollReason] = Enum.GetNames<OffPayrollReason>(),
    };

    public static string IdNumberKey(Guid typeId) => $"Id:{typeId:N}:Number";
    public static string IdExpiryKey(Guid typeId) => $"Id:{typeId:N}:Expiry";

    public static bool TryParseIdKey(string key, out Guid typeId, out bool isExpiry)
    {
        typeId = Guid.Empty;
        isExpiry = false;
        var parts = key.Split(':');
        if (parts.Length != 3 || parts[0] != "Id" || !Guid.TryParseExact(parts[1], "N", out typeId)) return false;
        isExpiry = parts[2] == "Expiry";
        return isExpiry || parts[2] == "Number";
    }

    /// <summary>The fixed catalogue with one Number (and one Expiry, where the type expires) column per active identification type.</summary>
    public static List<EmployeeImportColumn> Build(IEnumerable<IdentificationTypeRef> identificationTypes)
    {
        var columns = new List<EmployeeImportColumn>(Fixed.Length + 8);
        foreach (var column in Fixed)
        {
            columns.Add(column);
            if (column.Key != Tin) continue;

            foreach (var type in identificationTypes.OrderBy(t => t.Name))
            {
                columns.Add(new EmployeeImportColumn(IdNumberKey(type.Id), $"{type.Name} Number", false,
                    EmployeeImportColumnKind.Text, $"{type.Name} document number. Stored as an identification record.",
                    TextFormat: true, Width: 20));
                if (type.HasExpiryDate)
                    columns.Add(new EmployeeImportColumn(IdExpiryKey(type.Id), $"{type.Name} Expiry", false,
                        EmployeeImportColumnKind.Date, $"When the {type.Name} expires, if known.", Width: 14));
            }
        }
        return columns;
    }

    public static string RenderHeader(EmployeeImportColumn column) => column.Required ? column.Header + " *" : column.Header;

    /// <summary>Header text as matched: trailing asterisk dropped, whitespace collapsed, case ignored.</summary>
    public static string NormalizeHeader(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return string.Empty;
        var text = header.Trim();
        if (text.EndsWith('*')) text = text[..^1].TrimEnd();
        return NormalizeKey(text);
    }

    /// <summary>Lookup key: trimmed, inner whitespace collapsed to one space, upper-cased.</summary>
    public static string NormalizeKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : WhitespaceRun.Replace(value.Trim(), " ").ToUpperInvariant();

    /// <summary>Code key: every whitespace removed, upper-cased ("M 2" → "M2").</summary>
    public static string NormalizeCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : WhitespaceRun.Replace(value, "").ToUpperInvariant();

    /// <summary>1 → A, 27 → AA: the Excel column letter, so findings can name a cell after the workbook is closed.</summary>
    public static string ColumnLetter(int column)
    {
        var letters = string.Empty;
        while (column > 0)
        {
            var rem = (column - 1) % 26;
            letters = (char)('A' + rem) + letters;
            column = (column - 1) / 26;
        }
        return letters;
    }

    public static string CellAddress(int column, int row) => $"{EmployeesSheet}!{ColumnLetter(column)}{row}";

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);
}

// ── Reference data the reader resolves against ───────────────────────────────────────────────

public sealed record DepartmentRef(Guid Id, string Code, string Name);
public sealed record SectionRef(Guid Id, string Code, string Name, Guid DepartmentId);
public sealed record PositionRef(Guid Id, string Code, string Title, Guid OrganizationUnitId, Guid? StaffLevelId);
public sealed record LocationRef(Guid Id, string Code, string Name);
public sealed record SalaryLevelRef(Guid Id, string Code, string Name, Guid GradeId, string GradeCode);
public sealed record SalaryNotchRef(Guid Id, int Number, decimal Amount);
public sealed record QualificationRef(Guid Id, string Name, string? ShortCode);
public sealed record IdentificationTypeRef(Guid Id, string Name, bool HasExpiryDate);

/// <summary>
/// What the register holds for one live employee whose staff number appears in the file — the
/// "from" side of an Update row's diff, and the values an update must carry forward.
/// </summary>
public sealed class EmployeeSnapshot
{
    public required Guid Id { get; init; }
    public required string EmployeeNumber { get; init; }
    public string? Title { get; init; }
    public required string FirstName { get; init; }
    public string? MiddleName { get; init; }
    public required string LastName { get; init; }
    public Gender? Gender { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public MaritalStatus? MaritalStatus { get; init; }
    public string? Religion { get; init; }
    public string? Hometown { get; init; }
    /// <summary>⚠ The update mapping writes these two unconditionally; an update must send them back.</summary>
    public bool HasDisability { get; init; }
    public bool IsFullTime { get; init; }
    public EmploymentType EmploymentType { get; init; }
    public DateOnly? DateEmployed { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? SectionId { get; init; }
    public Guid PositionId { get; init; }
    public Guid? OrganizationUnitId { get; init; }
    public Guid? LocationId { get; init; }
    public Guid? ManagerId { get; init; }
    public string? ManagerNumber { get; init; }
    public bool IsOnPayroll { get; init; }
    public OffPayrollReason? OffPayrollReason { get; init; }
    public decimal? Salary { get; init; }
    public string? Email { get; init; }
    public string? MobileNumber { get; init; }
    public string? TelephoneNumber { get; init; }
    public string? SsnitNumber { get; init; }
    public string? TinNumber { get; init; }
    public string? DigitalAddress { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Notes { get; init; }
    public Guid? CurrentLevelId { get; init; }
    public Guid? CurrentNotchId { get; init; }
    public string? CurrentLevelCode { get; init; }
    public int? CurrentNotchNumber { get; init; }
    /// <summary>Normalised names of the qualifications already on the profile.</summary>
    public HashSet<string> QualificationNames { get; init; } = new();
    /// <summary>Normalised document numbers already on the profile, per identification type.</summary>
    public Dictionary<Guid, HashSet<string>> IdNumbers { get; init; } = new();

    public string DisplayName => string.Join(" ", new[] { FirstName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>Everything the checker needs, loaded once per upload so 10,000 rows cost 10,000 dictionary hits, not 10,000 queries.</summary>
public sealed class EmployeeImportReferenceData
{
    public required Guid TenantId { get; init; }
    public required LookupTable<DepartmentRef> Departments { get; init; }
    public required LookupTable<SectionRef> Sections { get; init; }
    public required LookupTable<PositionRef> Positions { get; init; }
    public required LookupTable<LocationRef> Locations { get; init; }

    /// <summary>By <see cref="EmployeeImportColumns.NormalizeCode"/> of the level code.</summary>
    public required Dictionary<string, SalaryLevelRef> SalaryLevels { get; init; }
    public required Dictionary<Guid, List<SalaryNotchRef>> NotchesByLevel { get; init; }
    public required LookupTable<QualificationRef> Qualifications { get; init; }
    public required List<IdentificationTypeRef> IdentificationTypes { get; init; }

    /// <summary>Every staff number in the tenant, soft-deleted included (the index is unfiltered).</summary>
    public required Dictionary<string, Guid> ExistingEmployeeNumbers { get; init; }
    public required HashSet<string> ExistingEmails { get; init; }
    public required HashSet<string> ExistingSsnitNumbers { get; init; }
    public required HashSet<string> ExistingTinNumbers { get; init; }
    public required Dictionary<Guid, HashSet<string>> ExistingIdNumbersByType { get; init; }

    /// <summary>The numbering rule per register, or null where the register is manual.</summary>
    public required Dictionary<EmploymentType, StaffNumberFormat?> Rules { get; init; }
    public required Dictionary<Guid, string> OrganizationUnitNames { get; init; }
    public required Dictionary<Guid, string> StaffLevelNames { get; init; }

    /// <summary>
    /// Live employees whose staff numbers appear in the file, by normalised number. Loaded only for
    /// a session that may update, and only for the numbers actually present.
    /// </summary>
    public Dictionary<string, EmployeeSnapshot> Snapshots { get; init; } = new();
}

/// <summary>Code-or-name resolution with "did you mean" suggestions.</summary>
public sealed class LookupTable<T> where T : class
{
    private readonly Dictionary<string, T> _byKey = new();
    private readonly List<(string Display, string Key, T Item)> _items = new();

    public void Add(T item, string display, params string?[] keys)
    {
        _items.Add((display, EmployeeImportColumns.NormalizeKey(display), item));
        foreach (var key in keys)
        {
            var normalized = EmployeeImportColumns.NormalizeKey(key);
            if (normalized.Length == 0) continue;
            _byKey.TryAdd(normalized, item);
        }
    }

    public int Count => _items.Count;
    public IEnumerable<(string Display, T Item)> Items => _items.Select(i => (i.Display, i.Item));

    public bool TryResolve(string? input, out T item)
    {
        var key = EmployeeImportColumns.NormalizeKey(input);
        if (key.Length > 0 && _byKey.TryGetValue(key, out var found))
        {
            item = found;
            return true;
        }
        item = null!;
        return false;
    }

    /// <summary>The nearest display names, best first: containment beats edit distance.</summary>
    public List<string> Suggest(string? input, int max = 3)
    {
        var key = EmployeeImportColumns.NormalizeKey(input);
        if (key.Length == 0 || _items.Count == 0) return new List<string>();

        return _items
            .Select(i => (i.Display, Score: i.Key.Contains(key) || key.Contains(i.Key) ? 0 : Levenshtein(key, i.Key)))
            .Where(s => s.Score <= Math.Max(3, key.Length / 2))
            .OrderBy(s => s.Score).ThenBy(s => s.Display, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .Select(s => s.Display)
            .ToList();
    }

    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}

/// <summary>Cell-value helpers shared by the reader and the annotated-workbook writer.</summary>
public static class EmployeeImportValues
{
    public static readonly string[] DateFormats =
    [
        "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
        "d-MMM-yyyy", "dd-MMM-yyyy", "d-MMMM-yyyy", "dd-MMMM-yyyy",
        "d MMM yyyy", "d MMMM yyyy", "yyyy-MM-dd",
    ];

    public static bool TryParseDate(string? text, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var value = text.Trim();
        // "10-July-1991" and "19-nov-2025" both appear in real registers; month names are matched case-insensitively.
        if (DateTime.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            date = DateOnly.FromDateTime(parsed);
            return true;
        }
        return false;
    }

    public static string FormatDate(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static bool TryParseYesNo(string? text, out bool value)
    {
        value = false;
        switch (EmployeeImportColumns.NormalizeKey(text))
        {
            case "YES": case "Y": case "TRUE": case "1": value = true; return true;
            case "NO": case "N": case "FALSE": case "0": value = false; return true;
            default: return false;
        }
    }

    public static bool TryParseEnum<TEnum>(string? text, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var compact = EmployeeImportColumns.NormalizeCode(text).Replace("-", "").Replace("_", "");
        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (string.Equals(name, compact, StringComparison.OrdinalIgnoreCase))
                return Enum.TryParse(name, out value);
        }
        return false;
    }
}

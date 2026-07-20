import fs from "node:fs/promises";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const outputDir = path.resolve("..");
const workbookPath = path.join(outputDir, "finance_module_seeded_users_permissions.xlsx");
const generatedOn = "2026-07-20";
const seedSource = "DatabaseSeedingService.SeedTestUsersAsync";
const permissionSource = "Live RHEMAERP role-permission assignments";
const connectionString = process.env.RHEMA_FINANCE_REPORT_DB;

if (!connectionString) {
  throw new Error("Set RHEMA_FINANCE_REPORT_DB before generating the seeded-user workbook.");
}

// PowerShell single-quoted strings escape apostrophes by doubling them. Keeping the
// connection string outside this source file prevents local credentials entering Git.
const powershellConnectionString = connectionString.replaceAll("'", "''");

function runPowerShell(script) {
  return execFileSync(
    "C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe",
    ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script],
    {
      encoding: "utf8",
      maxBuffer: 30 * 1024 * 1024,
      cwd: process.cwd(),
    },
  );
}

const dataScript = String.raw`
$ErrorActionPreference = 'Stop'
$cs = '${powershellConnectionString}'
$query = @"
DECLARE @Seeded TABLE (UserName nvarchar(256), SeedPassword nvarchar(100), SeedOrder int);
INSERT INTO @Seeded VALUES
(N'finance.clerk', N'Finance123!', 1),
(N'accounts.officer', N'Finance123!', 2),
(N'ap.officer', N'Finance123!', 3),
(N'ar.officer', N'Finance123!', 4),
(N'senior.accountant', N'Finance123!', 5),
(N'finance.manager', N'Finance123!', 6),
(N'financial.controller', N'Finance123!', 7),
(N'budget.officer', N'Finance123!', 8);

SELECT s.SeedOrder,
       u.UserName,
       s.SeedPassword AS [Password],
       u.Email,
       LTRIM(RTRIM(COALESCE(u.FirstName,N'') + N' ' + COALESCE(u.LastName,N''))) AS FullName,
       t.Code AS Tenant,
       CASE WHEN u.IsActive=1 THEN N'Yes' ELSE N'No' END AS Active,
       STUFF((SELECT N'; ' + r2.Name
              FROM UserRoles ur2
              INNER JOIN AspNetRoles r2 ON r2.Id=ur2.RoleId
              WHERE ur2.UserId=u.Id
              ORDER BY r2.Name
              FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, N'') AS Roles
FROM @Seeded s
LEFT JOIN Users u ON u.UserName=s.UserName
LEFT JOIN Tenants t ON t.Id=u.TenantId
ORDER BY s.SeedOrder;

SELECT DISTINCT s.SeedOrder,
       u.UserName,
       s.SeedPassword AS [Password],
       u.Email,
       LTRIM(RTRIM(COALESCE(u.FirstName,N'') + N' ' + COALESCE(u.LastName,N''))) AS FullName,
       t.Code AS Tenant,
       CASE WHEN u.IsActive=1 THEN N'Yes' ELSE N'No' END AS Active,
       r.Name AS RoleName,
       p.Name AS PermissionName,
       p.DisplayName,
       p.Category
FROM @Seeded s
INNER JOIN Users u ON u.UserName=s.UserName
INNER JOIN Tenants t ON t.Id=u.TenantId
INNER JOIN UserRoles ur ON ur.UserId=u.Id
INNER JOIN AspNetRoles r ON r.Id=ur.RoleId
INNER JOIN RolePermissions rp ON rp.RoleId=r.Id
INNER JOIN Permissions p ON p.Id=rp.PermissionId
WHERE p.Name LIKE N'Finance.%'
  AND p.IsDeleted=0
ORDER BY s.SeedOrder, p.Name;
"@

$cn = New-Object System.Data.SqlClient.SqlConnection $cs
$cn.Open()
$cmd = $cn.CreateCommand()
$cmd.CommandTimeout = 60
$cmd.CommandText = $query
$da = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
$ds = New-Object System.Data.DataSet
[void]$da.Fill($ds)
$cn.Close()

function Convert-Table($table) {
    $rows = New-Object System.Collections.Generic.List[object]
    foreach ($dr in $table.Rows) {
        $row = [ordered]@{}
        foreach ($col in $table.Columns) {
            $value = $dr[$col.ColumnName]
            if ($value -eq [DBNull]::Value) {
                $row[$col.ColumnName] = $null
            } else {
                $row[$col.ColumnName] = $value
            }
        }
        $rows.Add([pscustomobject]$row)
    }
    return $rows
}

$result = [ordered]@{
    summary = @(Convert-Table $ds.Tables[0])
    details = @(Convert-Table $ds.Tables[1])
}
$result | ConvertTo-Json -Depth 6
`;

function excelColumnName(index) {
  let n = index + 1;
  let name = "";
  while (n > 0) {
    const mod = (n - 1) % 26;
    name = String.fromCharCode(65 + mod) + name;
    n = Math.floor((n - mod) / 26);
  }
  return name;
}

function tableRange(startRow, startCol, rowCount, colCount) {
  const start = `${excelColumnName(startCol)}${startRow}`;
  const end = `${excelColumnName(startCol + colCount - 1)}${startRow + rowCount - 1}`;
  return `${start}:${end}`;
}

function applyTitle(sheet, range, title, subtitle) {
  sheet.showGridLines = false;
  const titleRange = sheet.getRange(range);
  titleRange.merge();
  titleRange.values = [[title]];
  titleRange.format = {
    fill: "#17365D",
    font: { bold: true, color: "#FFFFFF", size: 16 },
    horizontalAlignment: "left",
    verticalAlignment: "center",
  };
  titleRange.format.rowHeight = 30;
  const subtitleRange = sheet.getRange(range.replace(/1/g, "2"));
  subtitleRange.merge();
  subtitleRange.values = [[subtitle]];
  subtitleRange.format = {
    fill: "#EAF2F8",
    font: { color: "#1F2937", size: 10 },
    horizontalAlignment: "left",
    verticalAlignment: "center",
  };
  subtitleRange.format.rowHeight = 24;
}

function styleTableHeader(range) {
  range.format = {
    fill: "#1F4E78",
    font: { bold: true, color: "#FFFFFF" },
    horizontalAlignment: "center",
    verticalAlignment: "center",
    wrapText: true,
  };
  range.format.rowHeight = 32;
}

function styleUsedRange(range) {
  range.format = {
    font: { color: "#111827", size: 10 },
    verticalAlignment: "top",
  };
  range.format.borders = { preset: "all", style: "thin", color: "#D9E2EC" };
}

const raw = runPowerShell(dataScript);
const parsed = JSON.parse(raw);
const summaryRows = parsed.summary;
const detailRows = parsed.details;

const workbook = Workbook.create();
const summarySheet = workbook.worksheets.add("Finance Seeded Users");
const detailSheet = workbook.worksheets.add("Permission Details");
const notesSheet = workbook.worksheets.add("Source Notes");

applyTitle(
  summarySheet,
  "A1:K1",
  "Finance Module Seeded Users",
  `Generated ${generatedOn}. Passwords are seed passwords from ${seedSource}; existing local passwords may differ if manually changed.`,
);

const summaryHeaders = [
  "Seed Order",
  "Username",
  "Password",
  "Full Name",
  "Email",
  "Tenant",
  "Active",
  "Role(s)",
  "Seed Source",
  "Assigned Permission Count",
  "Can Add Budget Scenarios",
];
summarySheet.getRange("A4:K4").values = [summaryHeaders];
styleTableHeader(summarySheet.getRange("A4:K4"));

const summaryValues = summaryRows.map((row) => [
  Number(row.SeedOrder),
  row.UserName ?? "",
  row.Password ?? "",
  row.FullName ?? "",
  row.Email ?? "",
  row.Tenant ?? "",
  row.Active ?? "",
  row.Roles ?? "",
  seedSource,
  null,
  null,
]);
summarySheet.getRangeByIndexes(4, 0, summaryValues.length, summaryHeaders.length).values = summaryValues;

const detailStartRow = 5;
const detailLastRow = detailStartRow + detailRows.length - 1;
const summaryFormulaRows = summaryRows.map((_, index) => {
  const rowNum = 5 + index;
  return [
    `=COUNTIF('Permission Details'!$A$${detailStartRow}:$A$${detailLastRow},B${rowNum})`,
    `=IF(COUNTIFS('Permission Details'!$A$${detailStartRow}:$A$${detailLastRow},B${rowNum},'Permission Details'!$H$${detailStartRow}:$H$${detailLastRow},"Finance.Budgeting.Write")>0,"Yes","No")`,
  ];
});
summarySheet.getRangeByIndexes(4, 9, summaryFormulaRows.length, 2).formulas = summaryFormulaRows;

const summaryRange = tableRange(4, 0, summaryRows.length + 1, summaryHeaders.length);
const summaryTable = summarySheet.tables.add(summaryRange, true, "FinanceSeededUsers");
summaryTable.style = "TableStyleMedium2";
summaryTable.showFilterButton = true;
styleUsedRange(summarySheet.getRange(summaryRange));
styleTableHeader(summarySheet.getRange("A4:K4"));
summarySheet.freezePanes.freezeRows(4);
summarySheet.getRange("A:A").format.columnWidth = 10;
summarySheet.getRange("B:B").format.columnWidth = 22;
summarySheet.getRange("C:C").format.columnWidth = 16;
summarySheet.getRange("D:D").format.columnWidth = 22;
summarySheet.getRange("E:E").format.columnWidth = 32;
summarySheet.getRange("F:F").format.columnWidth = 12;
summarySheet.getRange("G:G").format.columnWidth = 10;
summarySheet.getRange("H:H").format.columnWidth = 28;
summarySheet.getRange("I:I").format.columnWidth = 34;
summarySheet.getRange("J:K").format.columnWidth = 18;
summarySheet.getRange(`A5:K${summaryRows.length + 4}`).format.wrapText = true;
summarySheet.getRange(`A5:A${summaryRows.length + 4}`).format.horizontalAlignment = "center";
summarySheet.getRange(`F5:G${summaryRows.length + 4}`).format.horizontalAlignment = "center";
summarySheet.getRange(`J5:K${summaryRows.length + 4}`).format.horizontalAlignment = "center";

applyTitle(
  detailSheet,
  "A1:L1",
  "Assigned Finance Permissions",
  "One row per seeded finance user and assigned Finance.* permission from the live role-permission tables.",
);

const detailHeaders = [
  "Username",
  "Password",
  "Full Name",
  "Email",
  "Tenant",
  "Active",
  "Role",
  "Permission Name",
  "Permission Display Name",
  "Category",
  "Grants Budget Scenario Create",
  "Permission Source",
];
detailSheet.getRange("A4:L4").values = [detailHeaders];
styleTableHeader(detailSheet.getRange("A4:L4"));

const detailValues = detailRows.map((row) => [
  row.UserName ?? "",
  row.Password ?? "",
  row.FullName ?? "",
  row.Email ?? "",
  row.Tenant ?? "",
  row.Active ?? "",
  row.RoleName ?? "",
  row.PermissionName ?? "",
  row.DisplayName ?? "",
  row.Category ?? "",
  null,
  permissionSource,
]);
detailSheet.getRangeByIndexes(4, 0, detailValues.length, detailHeaders.length).values = detailValues;
const detailFormulaRows = detailRows.map((_, index) => {
  const rowNum = 5 + index;
  return [`=IF(H${rowNum}="Finance.Budgeting.Write","Yes","No")`];
});
detailSheet.getRangeByIndexes(4, 10, detailFormulaRows.length, 1).formulas = detailFormulaRows;

const detailRange = tableRange(4, 0, detailRows.length + 1, detailHeaders.length);
const detailTable = detailSheet.tables.add(detailRange, true, "FinancePermissionDetails");
detailTable.style = "TableStyleMedium2";
detailTable.showFilterButton = true;
styleUsedRange(detailSheet.getRange(detailRange));
styleTableHeader(detailSheet.getRange("A4:L4"));
detailSheet.freezePanes.freezeRows(4);
detailSheet.getRange("A:A").format.columnWidth = 22;
detailSheet.getRange("B:B").format.columnWidth = 16;
detailSheet.getRange("C:C").format.columnWidth = 22;
detailSheet.getRange("D:D").format.columnWidth = 32;
detailSheet.getRange("E:F").format.columnWidth = 12;
detailSheet.getRange("G:G").format.columnWidth = 28;
detailSheet.getRange("H:H").format.columnWidth = 42;
detailSheet.getRange("I:I").format.columnWidth = 36;
detailSheet.getRange("J:J").format.columnWidth = 30;
detailSheet.getRange("K:K").format.columnWidth = 20;
detailSheet.getRange("L:L").format.columnWidth = 34;
detailSheet.getRange(`A5:L${detailRows.length + 4}`).format.wrapText = true;
detailSheet.getRange(`E5:F${detailRows.length + 4}`).format.horizontalAlignment = "center";
detailSheet.getRange(`K5:K${detailRows.length + 4}`).format.horizontalAlignment = "center";

applyTitle(
  notesSheet,
  "A1:D1",
  "Source Notes",
  "Use this sheet to audit where the workbook values came from.",
);
const notes = [
  ["Field", "Value", "Notes", "Source"],
  ["Generated On", generatedOn, "Date in current Codex task context.", "Codex environment"],
  ["Tenant", "DEFAULT", "Finance seed users are created against the default tenant.", "RHEMAERP database"],
  ["Seeded User Source", seedSource, "Defines usernames, emails, names, roles, and seed passwords.", "src/ErpSystem.Api/Services/DatabaseSeedingService.cs"],
  ["Permission Source", permissionSource, "Current role-permission assignments queried from Users, UserRoles, AspNetRoles, RolePermissions, and Permissions.", "RHEMAERP database"],
  ["Password Meaning", "Seed password", "The seed routine preserves existing passwords/contact details when accounts already exist, so manually changed local passwords may differ.", "DatabaseSeedingService.cs comment above CreateTestUserAsync calls"],
  ["Budget Scenario Create Permission", "Finance.Budgeting.Write", "CreateScenario maps to MaintainBudgets/Finance.Budgeting.Write.", "FinancePermissionPolicyMap.BudgetPolicy"],
];
notesSheet.getRangeByIndexes(3, 0, notes.length, 4).values = notes;
styleTableHeader(notesSheet.getRange("A4:D4"));
const notesRange = tableRange(4, 0, notes.length, 4);
const notesTable = notesSheet.tables.add(notesRange, true, "SourceNotes");
notesTable.style = "TableStyleMedium2";
notesTable.showFilterButton = true;
styleUsedRange(notesSheet.getRange(notesRange));
styleTableHeader(notesSheet.getRange("A4:D4"));
notesSheet.getRange("A:A").format.columnWidth = 30;
notesSheet.getRange("B:B").format.columnWidth = 34;
notesSheet.getRange("C:C").format.columnWidth = 74;
notesSheet.getRange("D:D").format.columnWidth = 54;
notesSheet.getRange(`A5:D${notes.length + 3}`).format.wrapText = true;
notesSheet.freezePanes.freezeRows(4);

const summaryInspect = await workbook.inspect({
  kind: "table",
  sheetId: "Finance Seeded Users",
  range: "A4:K12",
  include: "values,formulas",
  tableMaxRows: 12,
  tableMaxCols: 12,
  maxChars: 6000,
});
console.log(summaryInspect.ndjson);

const detailInspect = await workbook.inspect({
  kind: "table",
  sheetId: "Permission Details",
  range: "A4:L20",
  include: "values,formulas",
  tableMaxRows: 18,
  tableMaxCols: 12,
  maxChars: 6000,
});
console.log(detailInspect.ndjson);

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});
console.log(errors.ndjson);

for (const sheetName of ["Finance Seeded Users", "Permission Details", "Source Notes"]) {
  const range = sheetName === "Permission Details" ? "A1:L30" : "A1:K16";
  const preview = await workbook.render({
    sheetName,
    range,
    scale: 1,
    format: "png",
  });
  const previewBytes = new Uint8Array(await preview.arrayBuffer());
  const safeName = sheetName.toLowerCase().replaceAll(" ", "_");
  await fs.writeFile(path.join(outputDir, `${safeName}_preview.png`), previewBytes);
}

await fs.mkdir(outputDir, { recursive: true });
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(workbookPath);
console.log(`saved=${workbookPath}`);

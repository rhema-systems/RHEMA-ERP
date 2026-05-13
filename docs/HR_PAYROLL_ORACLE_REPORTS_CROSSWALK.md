# HR Payroll Oracle Reports Crosswalk

Source artifacts reviewed:

- `D:\PAYROLL\PAYROLL_RHEMA_DEMO\PAYROLL_MENU.mmb`
- `D:\PAYROLL\PAYROLL_RHEMA_DEMO\MAIN_MENU.mmb`
- `D:\PAYROLL\PAYROLL_RHEMA_DEMO\APP_MENU_FRM.fmb`
- `D:\PAYROLL\PAYROLL_RHEMA_DEMO\*.RDF`
- `D:\PAYROLL\PAYROLL_RHEMA_DEMO\*.rep`

The Oracle report binaries do not expose a clean text menu tree, so implementation should use the report artifact names plus embedded SQL/function evidence in the `.rep` files as the contract, then reconcile labels against the screenshot/menu evidence when available.

## ERP Report Module Placement

The ERP already has a generic report module:

- User surface: `frontend/src/app/reports/page.tsx`
- Admin surface: `frontend/src/app/administration/reports/page.tsx`
- Backend API: `src/ErpSystem.Api/Controllers/ReportsController.cs`
- Report execution service: `src/ErpSystem.Data/Services/DatabaseReportsService.cs`
- Report metadata: `src/ErpSystem.Core/Entities/ReportEntities.cs`

Payroll reports should be seeded as published HR reports in the generic report module and also surfaced from the payroll run desk where the report is run-specific. Queries should use the existing payroll tables (`PayrollRuns`, `PayrollRunEmployees`, `PayrollTransactions`, snapshots, journal lines, profiles, HR employee master data) instead of embedding Oracle SQL directly.

## Oracle Report Families

| Family | Oracle artifacts | ERP target |
| --- | --- | --- |
| Payslips | `PAYSLIP*`, `BONUS_SLIP`, company-specific variants such as `PAYSLIP_EIC`, `PAYSLIP_GBC`, `PAYSLIP_SLTF` | Existing payroll payslip preview/snapshot plus report module export templates |
| Payroll register | `REP3_001*`, `REGISTER`, `SUMMARY` | Run register report with all/category/department/section/bank/negative-pay variants |
| PAYE/tax | `PAYE`, `REP3_004*`, `PR3_004A/B` | PAYE schedule, tax table listings, tax reconciliation |
| SSF/pension | `SSF`, `REP3_006*` | Employee/employer SSF schedules, pension contribution reconciliation |
| Bank/cash listings | `BANK_DET`, `BANK_SUM`, `CASHLIST`, `REP3_007*` | Bank detail, bank summary, cash payment list |
| Contributions/provident | `CONTRIBUTIONS`, `REP3_009*`, `REP3_010*` | Provident fund/contribution statement, balances, withdrawals, interest |
| Arrears/backpay | `REP3_005*` | Promotion arrears/backpay schedules with BAR/ALR/ESR/CSR/COR split |
| Journals | `JOUNAL_REP`, `JOUNAL_DETAIL` | Payroll journal summary/detail from payroll journal lines |
| Bonus | `BONUS_REGISTER`, `BONUS_SLIP` | Bonus register and separate bonus slip |
| Analysis | `ANALYSIS_CATEGORY`, `ANALYSIS_DEPT`, `ANALYSIS_INDV`, `ANALYSIS_LOCATION`, `ANALYSIS_REG` | Payroll analysis by category, department, individual, location, region |
| Setup/reference | `CODES_DESC`, `BRANCH_SETUP`, `LOAN_SETUP`, `STAFF_DEP`, `SHCDLEA*`, `REP3_011` onward | Payroll setup/reference reports, added after run-output reports |

## Implementation Order

1. Run-output core: payroll register, payslip export, PAYE, SSF, bank detail/summary, journal summary/detail.
2. Oracle payroll-process reports: bonus register/slip, promotion arrears, contribution/provident reports, opening balances.
3. Analysis reports: department, category, individual, location, region.
4. Setup/reference reports: code descriptions, bank branch setup, loan setup, staff dependants, schedules and remaining `REP3_011+` artifacts after label confirmation.

## Build Notes

- Seed report definitions with `Type = "hr"` or the current HR module id so `/reports?module=hr` can show them.
- Keep run-specific reports linked from `frontend/src/app/hr/payroll/page.tsx` because payroll users already expect Reports & Journals beside the selected run.
- Use parameterized SQL report definitions for tabular reports, but keep payslips and bonus slips on the payroll snapshot renderer because they require fixed-page layout behavior.
- Any Oracle-specific variants that differ only by grouping/filtering should share one ERP report with parameters rather than many duplicate report definitions.

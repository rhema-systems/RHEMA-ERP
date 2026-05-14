# HR Payroll Oracle Reports Crosswalk

Source artifacts reviewed:

- `D:\PAYROLL\PAYROLL_RHEMA_DEMO\PAYROLL_MENU.mmb`
- `D:\PAYROLL\PAYROLL_RHEMA_DEMO\MAIN_MENU.mmb`
- `D:\PAYROLL\PAYROLL_RHEMA_DEMO\APP_MENU_FRM.fmb`
- `D:\PAYROLL\PAYROLL_RHEMA_DEMO\*.RDF`
- `D:\PAYROLL\PAYROLL_RHEMA_DEMO\*.rep`

The Oracle menu binaries are compiled Oracle Forms menu modules. String extraction confirms the payroll menu module names, while the durable menu-to-form mapping is recorded in `HR_PAYROLL_ORACLE_FORMS_MENU_FIELD_CROSSWALK.md` from the payroll menu audit. Implementation must therefore treat that menu mapping plus each mapped `.fmb` launcher and `.RDF` output as the ERP contract.

## Oracle Menu Mapping

| Oracle menu path | Menu id | Launcher form | Output artifacts used for ERP report slices |
| --- | --- | --- | --- |
| Main Menu > Payroll Reports > Payroll Register Report | `A0000401` | `REP3_001.fmb` | `REP3_001_ALL.RDF`, `REP3_001_SUM.RDF`, `REP3_001_DETAIL.rdf`, `REP3_001_DEPT.rdf`, `REP3_001_SECT.RDF`, `REP3_001_RAN.RDF` |
| Main Menu > Payroll Reports > Bank Advice | `A0000402` | `BANK_ADV.fmb` / `REP3_035.fmb` | `REP3_004.RDF`, `REP3_005_GBC1.RDF`, `BANK_SUM.RDF`, `REP3_005_GBC.RDF`, `REP3_020.RDF`, `REP3_005_ARR.RDF` |
| Main Menu > Payroll Reports > Pay Slip Printing | `A0000403` | `REP3_003.fmb` | Existing ERP payslip renderer; Oracle `PAYSLIP_*.RDF` stays as the fixed-page contract |
| Main Menu > Payroll Reports > SSF Report | `A0000404` | `REP3_007.fmb` | `REP3_007.rdf`, `REP3_007_1.rdf`, `REP3_007_2.rdf`, `REP3_007_1A.rdf`, `REP3_007_3.RDF`, `REP3_001_SSF.RDF` |
| Main Menu > Payroll Reports > Monthly PAYE Report | `A0000405` | `REP3_006.fmb` | `REP3_006.RDF`, `REP3_006_GRA.RDF`, `REP3_006_PWC.rdf` |
| Main Menu > Payroll Reports > Payroll Analysis - Month | `A0000406` | `REP3_002.fmb` | `ANALYSIS_DEPT.rdf`, `ANALYSIS_REG.RDF`, `ANALYSIS_INDV.RDF` |
| Main Menu > Payroll Reports > Bank Advice By Employer Bank | `A0000407` | `REP3_009.fmb` | `REP3_009_PHC.RDF`, `REP3_009_GSLTF.rdf`, `REP3_009_GSLTF_USD.RDF`, `REP3_009_USD_CEDI.RDF`, `REP3_009_DOL_DOL.RDF` |
| Main Menu > Payroll Reports > Allowances & Deductions Sch. | `A0000408` | `REP3_010.fmb` | `REP3_010_SLTF.rdf`, `REP3_010F_SLTF.RDF` |
| Main Menu > Payroll Reports > Cash List | `A0000410` | `REP3_011.fmb` | `CASHLIST.RDF` |
| Main Menu > Payroll Reports > Journal Reports | `A0000411` | `REP3_016.fmb` | `JOUNAL_REP.RDF`, `JOUNAL_DETAIL.rdf` |
| Main Menu > Payroll Reports > Overtime Reports | `A0000412` | `REP3_019.fmb` | `REP3_019.RDF` |
| Main Menu > Payroll Reports > Annual Tax Returns | `A0000418` | `REP3_034.fmb` | `REP3_034.RDF`, `REP3_034_A.RDF`, `REP3_034_R.RDF`, `REP3_034_P.RDF`, `REP3_034_LOC.rdf` |
| Main Menu > Payroll Reports > Staff Lists | `A0000420` | `REP3_305.fmb` | `REP3_305.RDF`, `REP3_305_A.RDF`, `REP3_305_B.RDF` |
| Main Menu > Payroll Reports > Loan Statement | `A0000423` | `REP3_036.fmb` | `REP3_036.RDF`, `REP3_036_A.RDF`, `REP3_036_B.RDF`, `REP3_036_DETAILS.RDF`, `REP3_036_DETAILS_1.RDF`, `REP3_036_DETAILS_2.RDF` |
| Main Menu > Payroll Reports > Bonus Reports > Bonus Register | `A0000415` | `REP3_029.fmb` | `BONUS_REGISTER.rdf` |
| Main Menu > Payroll Reports > Bonus Reports > Bonus Slip | `A000041402` | `REP3_028.fmb` | `REP3_028.RDF`, `BONUS_SLIP.RDF` |
| Main Menu > Payroll Reports > Bonus Reports > Bonus Tax Report | `A000041403` | `REP3_030.fmb` | `REP3_030.RDF` |
| Main Menu > Payroll Reports > Bonus Reports > Bonus Bank Advice | `A000041404` | `REP3_031.fmb` | `REP3_031.RDF` |
| Main Menu > Payroll Reports > Bonus Reports > Bonus Cash List | `A000041405` | `REP3_032.fmb` | `REP3_032.RDF` |

## Oracle Launcher Parameters

| Launcher form | Oracle controls | Variant control | Parameters passed to RDF |
| --- | --- | --- | --- |
| `REP3_001.fmb` | Payroll Register criteria screenshot shows only `By` and `Period` before `Run Report` | `By` (`All`) | `P_PERIOD`, `P_COMPCODE` |
| `REP3_006.fmb` | `PAY_PERIOD`, department and employee range controls are present | `ONE.REPTYPE` (`MPR`, `GRA`) | `P_PERIOD`, `P_COMPCODE`; Oracle comments out department/employee parameters for this report |
| `REP3_007.fmb` | `PAY_PERIOD`, department and employee range controls are present | `ONE.SSF_REP` (`REP`, `REP1`, `REP2`, `REP1A`, `REP3`, `FB`) | `P_PERIOD`, `P_COMPCODE`, `P_REP_CURRENCY`; Oracle comments out department/employee parameters for this report |
| `BANK_ADV.fmb` / `REP3_035.fmb` | Bank Advice criteria screenshot shows `By`, `Pay Period`, then `Run Report` | `ONE.BY_TYPE` (`BNK` Employee Bank, `BR1` Employee Bank Branch, `BSU` Bank Summary, `BRA` Branch Summary, `REG` Regional Summary, `ARR` Arreas Advice) | `P_PERIOD`, `P_COMPCODE` |
| `REP3_016.fmb` | `PAY_PERIOD`, department and employee range controls are present | `ONE.J_REP` (`S`, `D`) | `P_PERIOD`, `P_COMPCODE`; Oracle comments out department/employee parameters for this report |
| `REP3_002.fmb` | `DEP_ID`, `REG_ID`, company code, with null defaults to `ALL` | `ONE.BY_TYPE` (`DEP`, `REG`, `IND`) | `P_REGION`, `P_DEPARTMENT`, `P_COMPCODE` |
| `REP3_009.fmb` | employer bank, branch, account/signatory criteria | `BANK_REP_TYPE` (`CC`, `CD`, `DC`, `DD`) | `P_CURR_CODE`, `P_ACCT_NO`, `P_BANK_CODE`, `P_BRANCH_CODE`, `P_SIG1`, `P_SIG2`, `P_SIG3`, `P_PSN1`, `P_PSN2`, `P_PSN3`, `P_COMPCODE` |
| `REP3_010.fmb` | `ONE.ALL_DED`, `ONE.ALW_FM`, `ONE.ALW_TO`, `PAY_PERIOD` | `ONE.ALL_DED` (`ALL`, `ALW`, `DED`, `ADV`, plus loan `REP`/`INT`) | `P_ALL_DED_FM`, `P_ALL_DED_TO`, `P_ALW_FM`, `P_ALW_TO`, `P_PERIOD`, `P_COMPCODE`, `P_REP_CURRENCY` |
| `REP3_011.fmb` | department, location, employee, pay period ranges | Cash list | `P_DEPT_FM`, `P_DEPT_TO`, `P_LOC_FM`, `P_LOC_TO`, `P_EMPNO_FM`, `P_EMPNO_TO`, `P_PERIOD`, `P_COMPCODE` |
| `REP3_019.fmb` | department, location, employee, pay period ranges | Overtime reports | `P_DEPT_FM`, `P_DEPT_TO`, `P_LOC_FM`, `P_LOC_TO`, `P_EMPNO_FM`, `P_EMPNO_TO`, `P_PERIOD`, `P_COMPCODE` |
| `REP3_034.fmb` | department, location, employee, tax year ranges | `ONE.REP_TYPE` (`O`, `A`, `R`, `P`, `L`) | `P_DEPT_FM`, `P_DEPT_TO`, `P_LOC_FM`, `P_LOC_TO`, `P_EMPNO_FM`, `P_EMPNO_TO`, `P_PERIOD`, `P_COMPCODE` |
| `REP3_305.fmb` | pay period and staff-list grouping; department and employee ranges exist but are commented out in active parameter block | `ONE.STAFF_LIST` (`DEP`, `CAT`, `GEN`) | `P_PERIOD`, `P_COMPCODE` |
| `REP3_036.fmb` | department, location, employee, loan type, facility number, pay period | `ONE.CATEGORY` (`I`, `D`) plus loan/facility selectors | `P_DEPT_FM`, `P_DEPT_TO`, `P_LOC_FM`, `P_LOC_TO`, `P_EMPNO_FM`, `P_LOANTYPE`, `P_FACILITYNO`, `P_PERIOD`, `P_COMPCODE` |
| `REP3_029.fmb` | bonus type, department, location, employee, pay period ranges | Bonus register | `P_BONUS_TYPE`, `P_DEPT_FM`, `P_DEPT_TO`, `P_LOC_FM`, `P_LOC_TO`, `P_EMPNO_FM`, `P_EMPNO_TO`, `P_PERIOD`, `P_COMPCODE` |
| `REP3_028.fmb` | bonus type, department, location, employee, pay period ranges | Bonus slip | `P_BONUS_TYPE`, `P_DEPT_FM`, `P_DEPT_TO`, `P_LOC_FM`, `P_LOC_TO`, `P_EMPNO_FM`, `P_EMPNO_TO`, `P_PERIOD`, `P_COMPCODE` |
| `REP3_030.fmb` | bonus type and pay period; older range/bank controls appear in strings but are not passed by the active launcher block | Bonus tax report | `P_BONUS_TYPE`, `P_PERIOD`, `P_COMPCODE` |
| `REP3_031.fmb` | currency, employer bank, branch, account number, signatories, positions, bonus type, pay period | Bonus bank advice | `P_CURR_CODE`, `P_ACCT_NO`, `P_BANK_CODE`, `P_BRANCH_CODE`, `P_SIG1`, `P_SIG2`, `P_SIG3`, `P_PSN1`, `P_PSN2`, `P_PSN3`, `P_PERIOD`, `P_BONUS_TYPE`, `P_COMPCODE` |
| `REP3_032.fmb` | bonus type, department, location, employee, pay period ranges | Bonus cash list | `P_BONUS_TYPE`, `P_DEPT_FM`, `P_DEPT_TO`, `P_LOC_FM`, `P_LOC_TO`, `P_EMPNO_FM`, `P_EMPNO_TO`, `P_PERIOD`, `P_COMPCODE` |

## ERP Placement

Run-specific payroll reports are exposed from `Reports > HR Reports` at `frontend/src/app/reports/hr/page.tsx`. The payroll run desk keeps operational actions such as run summary, payslip generation, email, and posting; the Oracle Payroll Reports menu belongs under the ERP Reports module.

Queries use the existing ERP payroll output tables (`PayrollRuns`, `PayrollRunEmployees`, `PayrollTransactions`, `PayrollJournalLines`, payroll profiles, payment methods, and HR employee master data) instead of embedding Oracle SQL directly.

## First-Slice Output Contracts

| ERP report | Oracle source | Required visible columns |
| --- | --- | --- |
| Payroll Register Report - All | `REP3_001_ALL.RDF` | No, Staff No, Employee Name, Basic Salary, Other Allowances, Total Overtime, Gross Salary, Employee SSF, Benefits In Kind, Tax Relief, Taxable Income, Income Tax, Other Contributions, Other Deductions, Loans, Salary Advance, Total Deductions, Net Pay |
| Payroll Register Report - Detail | `REP3_001_DETAIL.rdf` | Employee No, Employee Name, Transaction Type, Actual Transaction, Account/Component, Description, Debit/Deduction, Credit/Earning, Employer Amount |
| Monthly PAYE Deductions - Monthly Payee | `REP3_006_PWC.rdf` | Employee No, Employee Name, Employee TIN, Basic Salary, Basic, Gross Allowance, Arrears Allowance, Excess Bonus, Total Cash Emolument, Benefits In Kind, Bonus in Threshold, Total Emolument, Deductable Reliefs, Employee SSF, Employee Arrears SSF, Employee Total SSF, OffPayroll Excess Bonus, Taxable Income, Normal Tax, Arrears Tax, Bonus Tax, Total Tax Charge; Oracle `COUNT :` and `TOTAL` line |
| Monthly PAYE Deductions - GRA Monthly Payee | `REP3_006_GRA.RDF` | No, Name of Employee, TIN, Position, Non - Resident (Y / N), Basic Salary, Secondary Employment (Y / N), Social Security Fund, Third Tier, Cash Allowances, Bonus Income (up to 15% of Basic), Final Tax On Bonus, Excess Bonus, Total Cash emolument (6+10+13), Accomodation Element, Vehicle Element, Non Cash Benefit, Total Assessable Income (14+15+16+17), Deductible Reliefs, Total Reliefs (8+9+19), Chargeable Income (18 - 20), Tax Deductible, Overtime Income, Overtime Tax, GRA Tax Payable (12+22+24), Severance pay paid, Remarks; Oracle total-count line and overtime note |
| SSF Report | `REP3_007.rdf` and selector variants `REP3_007_1.rdf`, `REP3_007_2.rdf`, `REP3_007_1A.rdf`, `REP3_007_3.RDF`, `REP3_001_SSF.RDF` | Employee No, Employee Name, Social Security No, Basic Salary, Employee SSF 5.5%, Employer SSF 13%, Total 18.5%, 1st Tier 13.5%, 2nd Tier 13.5%, Total 18.5%; Oracle `COUNT :` and `TOTAL` line |
| Bank Advice - Employee Bank | `REP3_004.RDF` | Letter layout with Staff ID, Name, Acct No, Net |
| Bank Advice - Employee Bank Branch | `REP3_005_GBC1.RDF` | Branch letter layout with Staff ID, Name, Account No, Net PAY |
| Bank Advice - Bank Summary | `BANK_SUM.RDF` | Bank Name, No of Accounts, Total |
| Bank Advice - Branch Summary | `REP3_005_GBC.RDF` | Branch summary layout with Staff ID, Name, Account No, Net PAY and branch totals |
| Bank Advice - Regional Summary | `REP3_020.RDF` | Regional bank advice layout |
| Bank Advice - Arreas Advice | `REP3_005_ARR.RDF` | Arrears bank advice layout |
| Summary Journal Report | `JOUNAL_REP.RDF` | Account Number, Account Description, Debit, Credit; Oracle `Total` line |
| Detail Journal Report | `JOUNAL_DETAIL.rdf` | Account Number, Account Description, Debit, Credit; Oracle `Total` line |

## Second-Slice Output Contracts

| ERP report | Oracle source | Required visible columns |
| --- | --- | --- |
| Payroll Analysis - Month - Department | `ANALYSIS_DEPT.rdf` | Department, Count, Basic, Allowances, Total |
| Payroll Analysis - Month - Region | `ANALYSIS_REG.RDF` | Region, Count, Basic, Allowances, Total |
| Payroll Analysis - Month - Individual | `ANALYSIS_INDV.RDF` | Staff ID, Employee Name, Department, Basic, Allowances, Total |
| Bank Advice By Employer Bank | `REP3_009_PHC.RDF` and currency variants | Bank, Bank Branch, Staff Name, Account N0, Amount, Currency |
| Allowances & Deductions Sch. | `REP3_010_SLTF.rdf` | Employee No, Employee Name, Transaction Type, Actual Transaction, Description, Amount, Employer Amount |
| Cash List | `CASHLIST.RDF` | No., Employee No., Employee Name, Amount (GHC), Oracle `COUNT :` and `Total` line |
| Overtime Reports | `REP3_019.RDF` | Staff Number, Staff, Week Hrs, Saturday, Sunday, Holiday, Total Hours, Total Amount, Department |
| Bonus Register | `BONUS_REGISTER.rdf` | Employee, Name, Bonus, Tax, Net Bonus, Oracle `Count :` line |
| Bonus Slip | `REP3_028.RDF`, `BONUS_SLIP.RDF` | Employee ID, Staff Name, Amount, Bonus Tax, Net Salary, Total Gross, Period |
| Bonus Tax Report | `REP3_030.RDF` | Employee, Name, Bonus, Taxable Bonus, Non Taxable Bonus, Bonus Tax, Net Bonus |
| Bonus Bank Advice | `REP3_031.RDF` | Staff Name, Branch, Account Number, Amount, bank letter/subtotal/total |
| Bonus Cash List | `REP3_032.RDF` | Employee No., Employee Name, Amount (GHC), Oracle `COUNT :` and `Total` line |
| Annual Tax Returns | `REP3_034.RDF`, `REP3_034_A.RDF`, `REP3_034_R.RDF`, `REP3_034_P.RDF`, `REP3_034_LOC.rdf` | Staff No, Name of Employee, TIN, Position, SSF No, Basic Salary, Taxable Allowances, Taxable OT, Bonus, Taxable Benefits, Employee SSF, Tax Relief, Taxable Income, Income Tax, Tax Paid, Net Tax; location variant includes department/location grouping |
| Staff Lists | `REP3_305.RDF`, `REP3_305_A.RDF`, `REP3_305_B.RDF` | Group, Staff, Staff on Roll, Date Employed, Gender, Department, Section, Position, Location, Grade, Staff Category, Region, Basic Salary, Status |
| Loan Statement | `REP3_036.RDF`, `REP3_036_A.RDF`, `REP3_036_B.RDF`, `REP3_036_DETAILS.RDF` | Staff No, Staff Name, Loan Type, Loan Reference, Date of Loan, Loan Start Date, Loan End Date, No of Repayments, Repayment Date, Repayment, Interest, Amount Paid, Interest Paid, Cumulative Paid, Loan Balance; detail variant summarizes Loan Granted, Monthly Repayment, Paid, Balance, Status |

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
| Bonus | `BONUS_REGISTER`, `BONUS_SLIP`, `REP3_028`, `REP3_030`, `REP3_031`, `REP3_032` | Bonus register, bonus slip, bonus tax, bonus bank advice, and bonus cash list |
| Analysis | `ANALYSIS_CATEGORY`, `ANALYSIS_DEPT`, `ANALYSIS_INDV`, `ANALYSIS_LOCATION`, `ANALYSIS_REG` | Payroll analysis by category, department, individual, location, region |
| Setup/reference | `CODES_DESC`, `BRANCH_SETUP`, `LOAN_SETUP`, `STAFF_DEP`, `SHCDLEA*`, `REP3_011` onward | Payroll setup/reference reports, added after run-output reports |

## Implementation Order

1. Run-output core: payroll register, payslip export, PAYE, SSF, bank detail/summary, journal summary/detail.
2. Oracle payroll-process reports: bonus register/slip, promotion arrears, contribution/provident reports, opening balances.
3. Analysis reports: department, category, individual, location, region.
4. Setup/reference reports: code descriptions, bank branch setup, loan setup, staff dependants, schedules and remaining `REP3_011+` artifacts after label confirmation.

## Build Notes

- Seed report definitions with `Type = "hr"` or the current HR module id so `Reports > HR Reports` can show them.
- Keep run-specific operational outputs in `frontend/src/app/hr/payroll/page.tsx`, but launch formal Oracle payroll reports from `frontend/src/app/reports/hr/page.tsx`.
- Use parameterized SQL report definitions for tabular reports, but keep payslips and bonus slips on the payroll snapshot renderer because they require fixed-page layout behavior.
- Any Oracle-specific variants that differ only by grouping/filtering should share one ERP report with parameters rather than many duplicate report definitions.

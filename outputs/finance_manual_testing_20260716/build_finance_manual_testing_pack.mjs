import fs from "node:fs/promises";
import path from "node:path";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const outputDir = path.resolve("outputs/finance_manual_testing_20260716");
const outputPath = path.join(outputDir, "RHEMA_Finance_Manual_Testing_Pack.xlsx");

const colors = {
  navy: "#17365D",
  blue: "#1F4E79",
  teal: "#0F766E",
  green: "#107C41",
  amber: "#F2C94C",
  red: "#C00000",
  lightBlue: "#D9EAF7",
  lightGreen: "#E2F0D9",
  lightAmber: "#FFF2CC",
  lightRed: "#FCE4D6",
  gray: "#F3F4F6",
  border: "#D9E2F3",
  darkText: "#1F2937",
};

const accounts = [
  ["1000", "000-1000-0000", "Cash and Cash Equivalents", "Asset", "Current Assets", "No", "Yes", "No", "No", "Default bank/cash account for manual finance tests."],
  ["1010", "000-1010-0000", "Cash and Bank - Payroll Clearing", "Asset", "Current Assets", "No", "Yes", "No", "No", "Use for payroll or internal bank transfer checks."],
  ["1100", "000-1100-0000", "Accounts Receivable", "Asset", "Current Assets", "Yes", "Yes", "No", "Yes", "AR control account; customer subledger must reconcile to this."],
  ["1120", "000-1120-0000", "Staff Loans and Salary Advances", "Asset", "Current Assets", "No", "Yes", "No", "No", "Payroll recoveries and staff advance tests."],
  ["1200", "000-1200-0000", "Inventory", "Asset", "Current Assets", "No", "Yes", "Yes", "No", "Inventory/GRV tests and COGS checks."],
  ["1500", "000-1500-0000", "Property, Plant & Equipment", "Asset", "Fixed Assets", "Yes", "No", "No", "No", "PPE control account; direct posting should be blocked."],
  ["1510", "000-1510-0000", "Buildings", "Asset", "Fixed Assets", "No", "Yes", "No", "No", "Building and ROU asset posting fallback."],
  ["1520", "000-1520-0000", "Equipment", "Asset", "Fixed Assets", "No", "Yes", "No", "No", "Equipment acquisitions/capitalization."],
  ["1530", "000-1530-0000", "Vehicles", "Asset", "Fixed Assets", "No", "Yes", "No", "No", "Vehicle acquisition/disposal tests."],
  ["1590", "000-1590-0000", "Accumulated Depreciation", "Asset", "Fixed Assets", "No", "Yes", "No", "No", "Contra-asset account for depreciation and disposal clearing."],
  ["1990", "000-1990-0000", "Migration Clearing Account", "Asset", "Current Assets", "No", "Yes", "No", "No", "Opening-balance migration offsets; expected to net to zero."],
  ["2000", "000-2000-0000", "Accounts Payable", "Liability", "Current Liabilities", "Yes", "Yes", "No", "Yes", "AP control account; supplier subledger must reconcile to this."],
  ["2100", "000-2100-0000", "Accrued Expenses", "Liability", "Current Liabilities", "No", "Yes", "No", "No", "GRV/accrual clearing account."],
  ["2120", "000-2120-0000", "Accrued Payroll Payables", "Liability", "Current Liabilities", "No", "Yes", "No", "No", "Payroll liability account."],
  ["2200", "000-2200-0000", "Tax/VAT Control", "Liability", "Current Liabilities", "Yes", "No", "No", "No", "VAT, levies, WHT and tax clearing; direct posting should be blocked."],
  ["2500", "000-2500-0000", "Long-term Debt", "Liability", "Non-Current Liabilities", "No", "Yes", "No", "No", "Debt and lease liability fallback."],
  ["3000", "000-3000-0000", "Share Capital", "Equity", "Equity", "No", "Yes", "No", "No", "Opening equity."],
  ["3100", "000-3100-0000", "Retained Earnings", "Equity", "Equity", "No", "No", "No", "No", "Opening retained earnings; direct posting should be blocked after setup."],
  ["4000", "000-4000-0000", "Sales Revenue", "Revenue", "Revenue", "No", "Yes", "Yes", "Yes", "Product sales and sales budget tests."],
  ["4100", "000-4100-0000", "Service Revenue", "Revenue", "Revenue", "No", "Yes", "Yes", "Yes", "Service invoice and revenue budget tests."],
  ["4210", "000-4210-0000", "Sales Discounts Allowed", "Revenue", "Revenue Deductions", "No", "Yes", "Yes", "Yes", "Contra-revenue discount account. If missing in test DB, rerun latest finance seeder/migration."],
  ["4900", "000-4900-0000", "Other Income", "Revenue", "Other Income", "No", "Yes", "No", "No", "Bank interest and disposal gain fallback."],
  ["4910", "000-4910-0000", "Purchase Discounts Received", "Revenue", "Other Income", "No", "Yes", "No", "Yes", "Supplier settlement discounts. If missing, rerun latest finance seeder/migration."],
  ["4920", "000-4920-0000", "Payroll Recoveries and Interest Income", "Revenue", "Other Income", "No", "Yes", "No", "No", "Payroll recovery income."],
  ["7100", "000-7100-0000", "Unrealized Exchange Gain", "Revenue", "Other Income", "No", "No", "No", "No", "FX revaluation gain."],
  ["7110", "000-7110-0000", "Unrealized Exchange Loss", "Expense", "Other Expenses", "No", "No", "No", "No", "FX revaluation loss. If missing, rerun latest finance seeder/migration."],
  ["7200", "000-7200-0000", "Realized Exchange Gain", "Revenue", "Other Income", "No", "No", "No", "No", "FX settlement gain."],
  ["7210", "000-7210-0000", "Realized Exchange Loss", "Expense", "Other Expenses", "No", "No", "No", "No", "FX settlement loss. If missing, rerun latest finance seeder/migration."],
  ["5000", "000-5000-0000", "Cost of Goods Sold", "Expense", "Expense", "No", "Yes", "Yes", "No", "COGS and inventory issue tests."],
  ["6000", "000-6000-0000", "Salaries and Wages", "Expense", "Expense", "No", "Yes", "Yes", "No", "Payroll and budget tests."],
  ["6020", "000-6020-0000", "Salaries, Wages and Payroll Costs", "Expense", "Operating Expenses", "No", "Yes", "Yes", "No", "Payroll posting service tests."],
  ["6100", "000-6100-0000", "Rent Expense", "Expense", "Expense", "No", "Yes", "Yes", "No", "Rent and accrual tests."],
  ["6200", "000-6200-0000", "Utilities Expense", "Expense", "Expense", "No", "Yes", "Yes", "No", "Utilities AP invoice tests."],
  ["6300", "000-6300-0000", "Depreciation Expense", "Expense", "Expense", "No", "Yes", "No", "No", "Depreciation and impairment fallback."],
  ["6400", "000-6400-0000", "Marketing and Advertising", "Expense", "Expense", "No", "Yes", "Yes", "No", "Opex budget tests."],
  ["6500", "000-6500-0000", "Professional Fees", "Expense", "Expense", "No", "Yes", "Yes", "No", "Professional services, bank charges, allocation source."],
  ["9999", "000-9999-0000", "Suspense Account", "Asset", "Current Assets", "No", "Yes", "No", "No", "Exception/unallocated transaction tests."],
];

const accountNameByNo = Object.fromEntries(accounts.map((a) => [a[1], a[2]]));

const partners = [
  ["CONT-GH-ADOM-BUILD", "Adom Construction Ltd", "Contractor", "Approved", "GHS", "Use for local construction/AP and fixed asset work."],
  ["CUS260001", "USD Customer", "Customer", "Active/Approved", "USD", "Use for foreign-currency AR invoice and realized FX tests."],
  ["CUST-DEMO-PM", "Northwind Transformation Group", "Customer", "Approved", "GHS", "Use for management/service billing tests."],
  ["CUST-GH-AMA-MENSAH", "Ama Mensah", "Customer", "Approved", "GHS", "Use for consumer/simple AR tests."],
  ["CUST-GH-GOLDCOAST", "Golden Coast Homes Ltd", "Customer", "Approved", "GHS", "Use for local corporate AR, WHT certificate, statement tests."],
  ["CUST-GH-KWESI-OWUSU", "Kwesi Owusu", "Customer", "Approved", "GHS", "Use for credit note/collection tests."],
  ["SUP260001", "USD Supplier", "Supplier", "Active/Approved", "USD", "Use for AP foreign-currency, WHT, and asset purchase tests."],
];

const paymentTerms = [
  ["NET30", "Net 30 Days", 30, 0, 0, "Default all-purpose term."],
  ["COD", "Cash on Delivery", 0, 0, 0, "Immediate due date."],
  ["NET7", "Net 7 Days", 7, 0, 0, "Short payment cycle."],
  ["NET15", "Net 15 Days", 15, 0, 0, "Mid-month settlement."],
  ["NET45", "Net 45 Days", 45, 0, 0, "Extended customer/supplier term."],
  ["NET60", "Net 60 Days", 60, 0, 0, "Long term."],
  ["2_10_NET30", "2/10 Net 30", 30, 2, 10, "2% early-payment discount within 10 days."],
];

const taxRows = [
  ["VAT-STD-SCHEME", "Sales", "NHIL 2.5% + GETFund 2.5% + VAT 15%", 20.0, "Output VAT/levy should credit 2200; COVID component is inactive."],
  ["WHT-SERVICES", "Purchases", "Withholding Tax - Services", 7.5, "Applies to qualifying service purchases above threshold."],
  ["WHT-GOODS", "Purchases", "Withholding Tax - Goods", 3.0, "Applies to qualifying goods purchases above threshold."],
  ["VAT-STD", "Sales", "Value Added Tax standard component", 15.0, "Part of VAT-STD-SCHEME."],
  ["NHIL", "Sales", "National Health Insurance Levy", 2.5, "Part of VAT-STD-SCHEME."],
  ["GETFUND", "Sales", "GETFund Levy", 2.5, "Part of VAT-STD-SCHEME."],
];

const fixedAssetRows = [
  ["FA-BLDG", "Buildings", "1510 Buildings", "1590 Accumulated Depreciation", "6300 Depreciation Expense", "StraightLine", 240, "5%", "Seeded category."],
  ["FA-EQP", "Equipment", "1520 Equipment", "1590 Accumulated Depreciation", "6300 Depreciation Expense", "StraightLine", 60, "10%", "Seeded category."],
  ["FA-VEH", "Vehicles", "1530 Vehicles", "1590 Accumulated Depreciation", "6300 Depreciation Expense", "StraightLine", 48, "10%", "Seeded category."],
];

const seededAssets = [
  ["FA-2024-BLDG-001", "Headquarters Building", "FA-BLDG", "2022-01-01", "2022-01-31", 1200000, 50000, 0, 1250000, 50000, 240, 1125000, "Active"],
  ["FA-2024-EQP-001", "Industrial Generator", "FA-EQP", "2023-07-01", "2023-07-06", 150000, 10000, 0, 160000, 10000, 60, 144000, "Active"],
  ["FA-2024-EQP-002", "Server Rack System", "FA-EQP", "2023-12-01", "2023-12-03", 45000, 5000, 0, 50000, 0, 60, 49166.67, "Active"],
  ["FA-2024-VEH-001", "Delivery Truck - Toyota Hilux", "FA-VEH", "2023-01-01", "2023-01-15", 250000, 0, 0, 250000, 25000, 48, 187500, "Active"],
];

const transactionRows = [
  ["OB-AR-001", "2026-06-01", "Opening Balances", "AR Opening Invoice", "CUST-GH-GOLDCOAST", "Golden Coast Homes Ltd", "GHS", 1, "100", "0000", "Opening receivable - local customer", 1, 97500, 0, "", 0, 0, "NET30", "2026-07-01", "Posted", "", "Subledger opening AR; migration clearing should net to zero."],
  ["OB-AR-002", "2026-06-01", "Opening Balances", "AR Opening Invoice", "CUS260001", "USD Customer", "USD", 12.5, "100", "0000", "Opening receivable - USD customer", 1, 7000, 0, "", 0, 0, "NET30", "2026-07-01", "Posted", "", "Tests USD AR opening balance and later revaluation."],
  ["OB-AP-001", "2026-06-01", "Opening Balances", "AP Opening Invoice", "SUP260001", "USD Supplier", "USD", 12.5, "100", "0000", "Opening payable - USD supplier", 1, 6000, 0, "", 0, 0, "NET30", "2026-07-01", "Posted", "", "Tests USD AP opening balance and later revaluation."],
  ["OB-AP-002", "2026-06-01", "Opening Balances", "AP Opening Invoice", "CONT-GH-ADOM-BUILD", "Adom Construction Ltd", "GHS", 1, "300", "P101", "Opening payable - contractor retention", 1, 50000, 0, "", 0, 0, "NET30", "2026-07-01", "Posted", "", "Local AP opening item for supplier statement."],
  ["OB-GL-001", "2026-06-01", "Opening Balances", "GL Opening Batch", "", "", "GHS", 1, "000", "0000", "Opening GL balances for cash, inventory, fixed assets, liabilities and equity", 1, 0, 0, "", 0, 0, "", "", "Posted", "", "Use opening-balance batch; 1990 migration clearing should be zero after all opening entries."],
  ["AR-INV-001", "2026-06-03", "Accounts Receivable", "Customer Invoice", "CUST-GH-GOLDCOAST", "Golden Coast Homes Ltd", "GHS", 1, "100", "P101", "Property management services - June", 10, 8000, 0, "VAT-STD-SCHEME", 20, 0, "NET30", "2026-07-03", "Sent/Posted", "", "Local taxable service invoice."],
  ["AR-PMT-001", "2026-06-10", "Accounts Receivable", "Customer Receipt", "CUST-GH-GOLDCOAST", "Golden Coast Homes Ltd", "GHS", 1, "100", "P101", "Partial receipt for AR-INV-001", 1, 50000, 0, "", 0, 0, "BankTransfer", "", "Cleared/Allocated", "AR-INV-001", "Partial allocation should leave invoice balance open."],
  ["AR-CN-001", "2026-06-12", "Accounts Receivable", "Credit Note", "CUST-GH-GOLDCOAST", "Golden Coast Homes Ltd", "GHS", 1, "100", "P101", "Service quality credit against AR-INV-001", 1, 5000, 0, "VAT-STD-SCHEME", 20, 0, "", "", "Posted", "AR-INV-001", "Credit note should reduce revenue, VAT and AR balance."],
  ["AR-PMT-002", "2026-06-15", "Accounts Receivable", "Customer Receipt with WHT", "CUST-GH-GOLDCOAST", "Golden Coast Homes Ltd", "GHS", 1, "100", "P101", "Final receipt with customer WHT certificate", 1, 40000, 0, "", 0, 15, "BankTransfer", "", "Cleared/Allocated", "AR-INV-001", "Cash received is net of withholding; certificate should be captured."],
  ["AR-INV-002", "2026-06-05", "Accounts Receivable", "Customer Invoice", "CUS260001", "USD Customer", "USD", 12.5, "100", "0000", "USD consulting invoice", 1, 4000, 0, "", 0, 0, "NET15", "2026-06-20", "Sent/Posted", "", "Foreign-currency AR at invoice rate."],
  ["AR-PMT-USD-001", "2026-06-20", "Accounts Receivable", "Customer Receipt", "CUS260001", "USD Customer", "USD", 13, "100", "0000", "USD receipt at settlement rate", 1, 4000, 0, "", 0, 0, "BankTransfer", "", "Cleared/Allocated", "AR-INV-002", "Should recognize realized FX gain on settlement."],
  ["AP-INV-001", "2026-06-04", "Accounts Payable", "Vendor Invoice", "SUP260001", "USD Supplier", "USD", 12.5, "100", "0000", "USD professional services", 1, 3000, 0, "WHT-SERVICES", 0, 7.5, "NET30", "2026-07-04", "Approved/Posted", "", "Service WHT should reduce AP and credit tax control."],
  ["AP-PMT-001", "2026-06-18", "Accounts Payable", "Vendor Payment", "SUP260001", "USD Supplier", "USD", 13, "100", "0000", "Payment of AP-INV-001 at settlement rate", 1, 2775, 0, "", 0, 0, "BankTransfer", "", "Processed/Cleared", "AP-INV-001", "Should recognize realized FX loss because settlement rate increased."],
  ["PO-GRV-001", "2026-06-06", "Finance Purchase Order", "Goods Receipt", "CONT-GH-ADOM-BUILD", "Adom Construction Ltd", "GHS", 1, "300", "P101", "Inventory spares received against PO", 100, 200, 0, "", 0, 0, "NET30", "", "Received/Posted", "", "GRV posts inventory debit and GRV accrual credit."],
  ["AP-INV-PO-001", "2026-06-07", "Accounts Payable", "Vendor Invoice from GRV", "CONT-GH-ADOM-BUILD", "Adom Construction Ltd", "GHS", 1, "300", "P101", "Supplier invoice matched to PO-GRV-001", 1, 20000, 0, "VAT-STD-SCHEME", 20, 0, "NET30", "2026-07-07", "Matched/Posted", "PO-GRV-001", "Should clear GRV accrual and create AP for gross invoice."],
  ["AP-PMT-PO-001", "2026-06-25", "Accounts Payable", "Vendor Payment", "CONT-GH-ADOM-BUILD", "Adom Construction Ltd", "GHS", 1, "300", "P101", "Payment for matched PO invoice", 1, 24000, 0, "", 0, 0, "BankTransfer", "", "Processed/Cleared", "AP-INV-PO-001", "AP aging should remove matched invoice after allocation."],
  ["AP-INV-002", "2026-06-11", "Accounts Payable", "Vendor Invoice", "CONT-GH-ADOM-BUILD", "Adom Construction Ltd", "GHS", 1, "200", "0000", "Utilities expense invoice", 1, 12000, 0, "VAT-STD-SCHEME", 20, 0, "NET30", "2026-07-11", "Approved/Posted", "", "Input VAT should debit tax control."],
  ["AP-INV-003", "2026-06-13", "Accounts Payable", "Vendor Invoice", "CONT-GH-ADOM-BUILD", "Adom Construction Ltd", "GHS", 1, "100", "0000", "Professional advisory invoice with 2/10 Net30", 1, 25000, 0, "WHT-SERVICES", 0, 7.5, "2_10_NET30", "2026-07-13", "Approved/Posted", "", "Discount eligibility should be calculated separately from WHT."],
  ["AP-PMT-003", "2026-06-20", "Accounts Payable", "Vendor Payment with Discount", "CONT-GH-ADOM-BUILD", "Adom Construction Ltd", "GHS", 1, "100", "0000", "Early settlement for AP-INV-003", 1, 22625, 0, "", 0, 0, "BankTransfer", "", "Processed/Cleared", "AP-INV-003", "2% discount should credit purchase discounts received."],
  ["BANK-TRF-001", "2026-06-21", "Cash Management", "Bank Transfer", "", "", "GHS", 1, "100", "0000", "Transfer to payroll clearing bank", 1, 20000, 0, "", 0, 0, "", "", "Posted", "", "Cash/bank ledger should show equal and opposite movement."],
  ["BANK-REC-001", "2026-06-22", "Cash Management", "Bank Reconciliation Adjustment", "", "", "GHS", 1, "100", "0000", "Bank service charge from statement", 1, 750, 0, "", 0, 0, "", "", "Posted", "", "Bank reconciliation adjustment should create journal entry and audit trail."],
  ["BANK-REC-002", "2026-06-22", "Cash Management", "Bank Reconciliation Adjustment", "", "", "GHS", 1, "100", "0000", "Bank interest earned", 1, 500, 0, "", 0, 0, "", "", "Posted", "", "Interest income should appear in cash flow and income statement."],
  ["FA-ADD-001", "2026-06-08", "Fixed Assets", "AP Capital Asset Invoice", "SUP260001", "USD Supplier", "GHS", 1, "300", "P101", "Network security appliance plus installation", 1, 88000, 0, "VAT-STD-SCHEME", 20, 0, "NET30", "2026-07-08", "Capitalized/Posted", "", "Creates fixed asset, AP invoice, and GL capitalization posting."],
  ["FA-DEP-001", "2026-06-30", "Fixed Assets", "Depreciation Run", "", "", "GHS", 1, "000", "0000", "June depreciation for active assets", 1, 14340.83, 0, "", 0, 0, "", "", "Posted", "", "Depreciation schedule should tie to GL and asset book values."],
  ["FA-TRF-001", "2026-06-24", "Fixed Assets", "Asset Transfer", "", "", "GHS", 1, "300", "P101", "Transfer Server Rack System to Development project", 1, 0, 0, "", 0, 0, "", "", "Completed", "FA-2024-EQP-002", "No GL expected unless transfer cost is entered; segment/location/custodian should update."],
  ["FA-VAL-001", "2026-06-26", "Fixed Assets", "Impairment", "", "", "GHS", 1, "300", "P101", "Impair Server Rack System after damage assessment", 1, 10000, 0, "", 0, 0, "", "", "Posted", "FA-2024-EQP-002", "Check impairment mapping; if no impairment account configured, system should block or use configured fallback."],
  ["FA-DISP-001", "2026-06-28", "Fixed Assets", "Asset Disposal", "", "", "GHS", 1, "200", "0000", "Sell Delivery Truck - Toyota Hilux", 1, 190000, 0, "", 0, 0, "", "", "Disposed/Posted", "FA-2024-VEH-001", "Disposal must clear cost and accumulated depreciation and post gain/loss."],
  ["FA-LEASE-001", "2026-06-29", "Fixed Assets", "Lease Recognition", "", "", "GHS", 1, "100", "0000", "Recognize office lease ROU asset and liability", 1, 300000, 0, "", 0, 0, "", "", "Recognized/Posted", "", "If lease/ROU accounts are not configured, setup should block posting with clear message."],
  ["FA-LEASE-PMT-001", "2026-06-30", "Fixed Assets", "Lease Payment", "", "", "GHS", 1, "100", "0000", "First lease payment with interest split", 1, 6000, 0, "", 0, 0, "", "", "Posted", "FA-LEASE-001", "Liability amortization and interest split should reconcile to lease schedule."],
  ["FX-REVAL-001", "2026-06-30", "Multi-Currency", "AR Revaluation", "CUS260001", "USD Customer", "USD", 13.1, "100", "0000", "Revalue open USD AR balance", 1, 7000, 0, "", 0, 0, "", "", "Posted", "OB-AR-002", "Should reverse prior unrealized entries before current revaluation if any."],
  ["FX-REVAL-002", "2026-06-30", "Multi-Currency", "AP Revaluation", "SUP260001", "USD Supplier", "USD", 13.1, "100", "0000", "Revalue open USD AP balance", 1, 6000, 0, "", 0, 0, "", "", "Posted", "OB-AP-001", "Liability revaluation loss should increase AP in base currency."],
];

function line(txn, date, module, accountNo, dc, amount, segment, memo) {
  return [txn, date, module, "", accountNo, accountNameByNo[accountNo] || "", dc, amount, segment, memo];
}

const glRows = [
  line("OB-AR-001", "2026-06-01", "Opening Balances", "000-1100-0000", "DR", 97500, "DEPT=100;PROJ=0000", "Opening AR - Golden Coast Homes Ltd"),
  line("OB-AR-001", "2026-06-01", "Opening Balances", "000-1990-0000", "CR", 97500, "DEPT=100;PROJ=0000", "Opening AR migration clearing"),
  line("OB-AR-002", "2026-06-01", "Opening Balances", "000-1100-0000", "DR", 87500, "DEPT=100;PROJ=0000", "Opening AR - USD Customer"),
  line("OB-AR-002", "2026-06-01", "Opening Balances", "000-1990-0000", "CR", 87500, "DEPT=100;PROJ=0000", "Opening AR migration clearing"),
  line("OB-AP-001", "2026-06-01", "Opening Balances", "000-1990-0000", "DR", 75000, "DEPT=100;PROJ=0000", "Opening AP migration clearing"),
  line("OB-AP-001", "2026-06-01", "Opening Balances", "000-2000-0000", "CR", 75000, "DEPT=100;PROJ=0000", "Opening AP - USD Supplier"),
  line("OB-AP-002", "2026-06-01", "Opening Balances", "000-1990-0000", "DR", 50000, "DEPT=300;PROJ=P101", "Opening AP migration clearing"),
  line("OB-AP-002", "2026-06-01", "Opening Balances", "000-2000-0000", "CR", 50000, "DEPT=300;PROJ=P101", "Opening AP - Adom Construction Ltd"),
  line("OB-GL-001", "2026-06-01", "Opening Balances", "000-1000-0000", "DR", 350000, "DEPT=000;PROJ=0000", "Opening cash"),
  line("OB-GL-001", "2026-06-01", "Opening Balances", "000-1200-0000", "DR", 320000, "DEPT=000;PROJ=0000", "Opening inventory"),
  line("OB-GL-001", "2026-06-01", "Opening Balances", "000-1510-0000", "DR", 1250000, "DEPT=000;PROJ=0000", "Opening building cost"),
  line("OB-GL-001", "2026-06-01", "Opening Balances", "000-1520-0000", "DR", 210000, "DEPT=000;PROJ=0000", "Opening equipment cost"),
  line("OB-GL-001", "2026-06-01", "Opening Balances", "000-1530-0000", "DR", 250000, "DEPT=000;PROJ=0000", "Opening vehicle cost"),
  line("OB-GL-001", "2026-06-01", "Opening Balances", "000-1990-0000", "DR", 60000, "DEPT=000;PROJ=0000", "Clear net AR/AP migration clearing"),
  line("OB-GL-001", "2026-06-01", "Opening Balances", "000-1590-0000", "CR", 204333.33, "DEPT=000;PROJ=0000", "Opening accumulated depreciation"),
  line("OB-GL-001", "2026-06-01", "Opening Balances", "000-2100-0000", "CR", 45000, "DEPT=000;PROJ=0000", "Opening accruals"),
  line("OB-GL-001", "2026-06-01", "Opening Balances", "000-2500-0000", "CR", 500000, "DEPT=000;PROJ=0000", "Opening debt"),
  line("OB-GL-001", "2026-06-01", "Opening Balances", "000-3000-0000", "CR", 1000000, "DEPT=000;PROJ=0000", "Opening share capital"),
  line("OB-GL-001", "2026-06-01", "Opening Balances", "000-3100-0000", "CR", 690666.67, "DEPT=000;PROJ=0000", "Opening retained earnings"),
  line("AR-INV-001", "2026-06-03", "Accounts Receivable", "000-1100-0000", "DR", 96000, "DEPT=100;PROJ=P101", "Customer invoice gross"),
  line("AR-INV-001", "2026-06-03", "Accounts Receivable", "000-4100-0000", "CR", 80000, "DEPT=100;PROJ=P101", "Service revenue"),
  line("AR-INV-001", "2026-06-03", "Accounts Receivable", "000-2200-0000", "CR", 16000, "DEPT=100;PROJ=P101", "Output VAT/levies"),
  line("AR-PMT-001", "2026-06-10", "Accounts Receivable", "000-1000-0000", "DR", 50000, "DEPT=100;PROJ=P101", "Customer receipt"),
  line("AR-PMT-001", "2026-06-10", "Accounts Receivable", "000-1100-0000", "CR", 50000, "DEPT=100;PROJ=P101", "AR allocation"),
  line("AR-CN-001", "2026-06-12", "Accounts Receivable", "000-4100-0000", "DR", 5000, "DEPT=100;PROJ=P101", "Revenue credit note"),
  line("AR-CN-001", "2026-06-12", "Accounts Receivable", "000-2200-0000", "DR", 1000, "DEPT=100;PROJ=P101", "VAT reversal"),
  line("AR-CN-001", "2026-06-12", "Accounts Receivable", "000-1100-0000", "CR", 6000, "DEPT=100;PROJ=P101", "AR credit note"),
  line("AR-PMT-002", "2026-06-15", "Accounts Receivable", "000-1000-0000", "DR", 34000, "DEPT=100;PROJ=P101", "Net customer cash"),
  line("AR-PMT-002", "2026-06-15", "Accounts Receivable", "000-2200-0000", "DR", 6000, "DEPT=100;PROJ=P101", "Customer WHT certificate"),
  line("AR-PMT-002", "2026-06-15", "Accounts Receivable", "000-1100-0000", "CR", 40000, "DEPT=100;PROJ=P101", "Final AR allocation"),
  line("AR-INV-002", "2026-06-05", "Accounts Receivable", "000-1100-0000", "DR", 50000, "DEPT=100;PROJ=0000", "USD customer invoice at 12.5"),
  line("AR-INV-002", "2026-06-05", "Accounts Receivable", "000-4100-0000", "CR", 50000, "DEPT=100;PROJ=0000", "USD service revenue at invoice rate"),
  line("AR-PMT-USD-001", "2026-06-20", "Accounts Receivable", "000-1000-0000", "DR", 52000, "DEPT=100;PROJ=0000", "USD receipt at 13.0"),
  line("AR-PMT-USD-001", "2026-06-20", "Accounts Receivable", "000-1100-0000", "CR", 50000, "DEPT=100;PROJ=0000", "Clear USD AR at invoice rate"),
  line("AR-PMT-USD-001", "2026-06-20", "Accounts Receivable", "000-7200-0000", "CR", 2000, "DEPT=100;PROJ=0000", "Realized FX gain"),
  line("AP-INV-001", "2026-06-04", "Accounts Payable", "000-6500-0000", "DR", 37500, "DEPT=100;PROJ=0000", "USD professional services"),
  line("AP-INV-001", "2026-06-04", "Accounts Payable", "000-2000-0000", "CR", 34687.5, "DEPT=100;PROJ=0000", "AP net of WHT"),
  line("AP-INV-001", "2026-06-04", "Accounts Payable", "000-2200-0000", "CR", 2812.5, "DEPT=100;PROJ=0000", "WHT payable"),
  line("AP-PMT-001", "2026-06-18", "Accounts Payable", "000-2000-0000", "DR", 34687.5, "DEPT=100;PROJ=0000", "Clear AP at invoice base"),
  line("AP-PMT-001", "2026-06-18", "Accounts Payable", "000-7210-0000", "DR", 1387.5, "DEPT=100;PROJ=0000", "Realized FX loss"),
  line("AP-PMT-001", "2026-06-18", "Accounts Payable", "000-1000-0000", "CR", 36075, "DEPT=100;PROJ=0000", "USD payment at 13.0"),
  line("PO-GRV-001", "2026-06-06", "Finance Purchase Order", "000-1200-0000", "DR", 20000, "DEPT=300;PROJ=P101", "GRV inventory receipt"),
  line("PO-GRV-001", "2026-06-06", "Finance Purchase Order", "000-2100-0000", "CR", 20000, "DEPT=300;PROJ=P101", "GRV accrual"),
  line("AP-INV-PO-001", "2026-06-07", "Accounts Payable", "000-2100-0000", "DR", 20000, "DEPT=300;PROJ=P101", "Clear GRV accrual"),
  line("AP-INV-PO-001", "2026-06-07", "Accounts Payable", "000-2200-0000", "DR", 4000, "DEPT=300;PROJ=P101", "Input VAT"),
  line("AP-INV-PO-001", "2026-06-07", "Accounts Payable", "000-2000-0000", "CR", 24000, "DEPT=300;PROJ=P101", "AP gross invoice"),
  line("AP-PMT-PO-001", "2026-06-25", "Accounts Payable", "000-2000-0000", "DR", 24000, "DEPT=300;PROJ=P101", "Clear AP"),
  line("AP-PMT-PO-001", "2026-06-25", "Accounts Payable", "000-1000-0000", "CR", 24000, "DEPT=300;PROJ=P101", "Supplier payment"),
  line("AP-INV-002", "2026-06-11", "Accounts Payable", "000-6200-0000", "DR", 12000, "DEPT=200;PROJ=0000", "Utilities expense"),
  line("AP-INV-002", "2026-06-11", "Accounts Payable", "000-2200-0000", "DR", 2400, "DEPT=200;PROJ=0000", "Input VAT"),
  line("AP-INV-002", "2026-06-11", "Accounts Payable", "000-2000-0000", "CR", 14400, "DEPT=200;PROJ=0000", "AP gross invoice"),
  line("AP-INV-003", "2026-06-13", "Accounts Payable", "000-6500-0000", "DR", 25000, "DEPT=100;PROJ=0000", "Professional advisory fee"),
  line("AP-INV-003", "2026-06-13", "Accounts Payable", "000-2000-0000", "CR", 23125, "DEPT=100;PROJ=0000", "AP net of WHT"),
  line("AP-INV-003", "2026-06-13", "Accounts Payable", "000-2200-0000", "CR", 1875, "DEPT=100;PROJ=0000", "WHT payable"),
  line("AP-PMT-003", "2026-06-20", "Accounts Payable", "000-2000-0000", "DR", 23125, "DEPT=100;PROJ=0000", "Clear AP before discount"),
  line("AP-PMT-003", "2026-06-20", "Accounts Payable", "000-1000-0000", "CR", 22625, "DEPT=100;PROJ=0000", "Early settlement cash"),
  line("AP-PMT-003", "2026-06-20", "Accounts Payable", "000-4910-0000", "CR", 500, "DEPT=100;PROJ=0000", "Purchase discount received"),
  line("BANK-TRF-001", "2026-06-21", "Cash Management", "000-1010-0000", "DR", 20000, "DEPT=100;PROJ=0000", "Transfer to payroll clearing"),
  line("BANK-TRF-001", "2026-06-21", "Cash Management", "000-1000-0000", "CR", 20000, "DEPT=100;PROJ=0000", "Transfer from operating cash"),
  line("BANK-REC-001", "2026-06-22", "Cash Management", "000-6500-0000", "DR", 750, "DEPT=100;PROJ=0000", "Bank charge"),
  line("BANK-REC-001", "2026-06-22", "Cash Management", "000-1000-0000", "CR", 750, "DEPT=100;PROJ=0000", "Cash decrease"),
  line("BANK-REC-002", "2026-06-22", "Cash Management", "000-1000-0000", "DR", 500, "DEPT=100;PROJ=0000", "Bank interest cash"),
  line("BANK-REC-002", "2026-06-22", "Cash Management", "000-4900-0000", "CR", 500, "DEPT=100;PROJ=0000", "Interest income"),
  line("FA-ADD-001", "2026-06-08", "Fixed Assets", "000-1520-0000", "DR", 88000, "DEPT=300;PROJ=P101", "Capitalized equipment cost"),
  line("FA-ADD-001", "2026-06-08", "Fixed Assets", "000-2200-0000", "DR", 17600, "DEPT=300;PROJ=P101", "Input VAT on asset invoice"),
  line("FA-ADD-001", "2026-06-08", "Fixed Assets", "000-2000-0000", "CR", 105600, "DEPT=300;PROJ=P101", "AP for capital asset invoice"),
  line("FA-DEP-001", "2026-06-30", "Fixed Assets", "000-6300-0000", "DR", 14340.83, "DEPT=000;PROJ=0000", "June depreciation run"),
  line("FA-DEP-001", "2026-06-30", "Fixed Assets", "000-1590-0000", "CR", 14340.83, "DEPT=000;PROJ=0000", "Accumulated depreciation"),
  line("FA-VAL-001", "2026-06-26", "Fixed Assets", "000-6300-0000", "DR", 10000, "DEPT=300;PROJ=P101", "Impairment loss fallback"),
  line("FA-VAL-001", "2026-06-26", "Fixed Assets", "000-1520-0000", "CR", 10000, "DEPT=300;PROJ=P101", "Reduce carrying amount fallback"),
  line("FA-DISP-001", "2026-06-28", "Fixed Assets", "000-1000-0000", "DR", 190000, "DEPT=200;PROJ=0000", "Disposal proceeds"),
  line("FA-DISP-001", "2026-06-28", "Fixed Assets", "000-1590-0000", "DR", 67187.5, "DEPT=200;PROJ=0000", "Clear accumulated depreciation"),
  line("FA-DISP-001", "2026-06-28", "Fixed Assets", "000-1530-0000", "CR", 250000, "DEPT=200;PROJ=0000", "Clear vehicle cost"),
  line("FA-DISP-001", "2026-06-28", "Fixed Assets", "000-4900-0000", "CR", 7187.5, "DEPT=200;PROJ=0000", "Gain on disposal fallback"),
  line("FA-LEASE-001", "2026-06-29", "Fixed Assets", "000-1510-0000", "DR", 300000, "DEPT=100;PROJ=0000", "ROU asset fallback"),
  line("FA-LEASE-001", "2026-06-29", "Fixed Assets", "000-2500-0000", "CR", 300000, "DEPT=100;PROJ=0000", "Lease liability fallback"),
  line("FA-LEASE-PMT-001", "2026-06-30", "Fixed Assets", "000-2500-0000", "DR", 4500, "DEPT=100;PROJ=0000", "Lease principal"),
  line("FA-LEASE-PMT-001", "2026-06-30", "Fixed Assets", "000-6500-0000", "DR", 1500, "DEPT=100;PROJ=0000", "Lease interest fallback"),
  line("FA-LEASE-PMT-001", "2026-06-30", "Fixed Assets", "000-1000-0000", "CR", 6000, "DEPT=100;PROJ=0000", "Lease cash payment"),
  line("FX-REVAL-001", "2026-06-30", "Multi-Currency", "000-1100-0000", "DR", 4200, "DEPT=100;PROJ=0000", "Revalue open USD AR at 13.1"),
  line("FX-REVAL-001", "2026-06-30", "Multi-Currency", "000-7100-0000", "CR", 4200, "DEPT=100;PROJ=0000", "Unrealized FX gain"),
  line("FX-REVAL-002", "2026-06-30", "Multi-Currency", "000-7110-0000", "DR", 3600, "DEPT=100;PROJ=0000", "Unrealized FX loss"),
  line("FX-REVAL-002", "2026-06-30", "Multi-Currency", "000-2000-0000", "CR", 3600, "DEPT=100;PROJ=0000", "Revalue open USD AP at 13.1"),
];

const testScripts = [
  ["0.1", "PRE-001", "Setup", "Confirm user permissions", "Finance admin/tester user exists", "Before testing", "Confirm access to GL, AR, AP, Cash, Budget, Unit Accounting, Fixed Assets, Reports, Approvals.", "All menus/actions visible or blocked according to role.", "Missing permission claims, approval bypasses, wrong tenant context.", "Finance dashboard; audit log"],
  ["0.2", "PRE-002", "Setup", "Confirm fiscal period status", "FY2026 seeded", "Before testing", "Verify January-May 2026 are closed/locked for posting tests and June 2026 is open.", "Closed periods reject postings; June accepts postings.", "Local DB may show Status=Closed but IsClosed=false; validate actual posting guard.", "Fiscal periods list"],
  ["0.3", "PRE-003", "Setup", "Confirm chart/segments", "Finance seeder applied", "Before testing", "Validate DEPT-ACCT-PROJ structure, default account number pattern, reporting dimensions.", "All natural accounts and lookup values exist.", "Missing discount/FX control accounts can break settlement/revaluation.", "Chart of accounts; segment lookup"],
  ["1.1", "OB-001", "Opening Balances", "Enter customer opening AR invoices", "Partners active", "2026-06-01", "Use OB-AR-001 and OB-AR-002 rows.", "AR subledger total = 185,000 GHS; 1990 credits same total.", "Do not post direct AR control opening balances without subledger details.", "Trial Balance; AR aging; customer statements"],
  ["1.2", "OB-002", "Opening Balances", "Enter supplier opening AP invoices", "Partners active", "2026-06-01", "Use OB-AP-001 and OB-AP-002 rows.", "AP subledger total = 125,000 GHS; 1990 debits same total.", "AP due dates and currencies must be correct for aging and FX revaluation.", "Trial Balance; AP aging; supplier statement"],
  ["1.3", "OB-003", "Opening Balances", "Enter non-subledger GL opening batch", "Opening subledger docs posted", "2026-06-01", "Post OB-GL-001 non-subledger balances including 1990 clearing debit.", "Migration clearing net = zero; Trial Balance balances.", "Retained earnings is the balancing line; do not leave 1990 residual.", "Trial Balance; Detailed Ledger 1990"],
  ["1.4", "OB-004", "Opening Balances", "Load fixed asset opening book values", "FA categories seeded", "2026-06-01", "Validate seeded assets and opening accumulated depreciation against Fixed_Assets sheet.", "FA cost = GL asset cost; FA AD = GL 1590; NBV ties.", "Historical depreciation/import may not create GL if marked as opening.", "Fixed asset register; Balance Sheet"],
  ["2.1", "GL-001", "General Ledger", "Manual balanced journal", "June period open", "2026-06-02", "Create a small accrual/reversal journal using 6100 and 2100, then reverse it.", "Posted journal creates equal debit/credit and reversal references original.", "Maker-checker status transitions and reversal dates.", "Detailed Ledger; Trial Balance"],
  ["2.2", "GL-002", "General Ledger", "Closed-period posting block", "January-May status closed", "2026-05-31", "Attempt to post a journal dated May 2026.", "Posting is blocked with a clear closed-period error.", "If accepted, period close guard is not enforcing posting date.", "Audit log; journal validation error"],
  ["3.1", "AR-001", "Accounts Receivable", "Local taxable service invoice", "Customer active; tax group active", "2026-06-03", "Post AR-INV-001.", "Dr AR 96,000; Cr Service Revenue 80,000; Cr Tax 16,000.", "VAT scheme should exclude inactive COVID levy and use base-only calculation.", "AR aging; customer statement; tax report; Trial Balance"],
  ["3.2", "AR-002", "Accounts Receivable", "Partial receipt allocation", "AR-INV-001 posted", "2026-06-10", "Post AR-PMT-001 for 50,000 and allocate to invoice.", "Invoice remains partially paid; AR reduced by 50,000.", "Over-allocation and unapplied amount handling.", "AR aging; customer statement; cash ledger"],
  ["3.3", "AR-003", "Accounts Receivable", "Credit note", "AR-INV-001 posted", "2026-06-12", "Post AR-CN-001.", "Revenue, VAT and AR reduce by 6,000 gross.", "Credit should not exceed open invoice balance unless allowed by policy.", "Customer statement; tax report; detailed ledger"],
  ["3.4", "AR-004", "Accounts Receivable", "Receipt with customer WHT", "AR-INV-001 balance open", "2026-06-15", "Post final receipt with net cash and WHT certificate.", "AR clears; cash plus WHT debit equals invoice balance.", "WHT certificate number/date should be mandatory if amount is entered.", "Tax report; AR aging"],
  ["3.5", "AR-005", "Accounts Receivable", "USD invoice and settlement FX", "USD customer and rates exist", "2026-06-05 to 2026-06-20", "Post AR-INV-002 then AR-PMT-USD-001 at a different rate.", "AR clears and realized FX gain posts to 7200.", "Settlement must use invoice base amount plus realized FX, not retranslate revenue.", "Multi-currency detail; customer statement"],
  ["4.1", "AP-001", "Accounts Payable", "USD service invoice with WHT", "USD supplier active", "2026-06-04", "Post AP-INV-001.", "Dr Professional Fees 37,500; Cr AP 34,687.50; Cr WHT 2,812.50.", "WHT threshold and certificate fields; base-currency conversion.", "AP aging; supplier statement; tax report"],
  ["4.2", "AP-002", "Accounts Payable", "USD supplier payment FX loss", "AP-INV-001 approved", "2026-06-18", "Pay the net USD amount at 13.0.", "AP clears and realized FX loss posts to 7210.", "Payment amount should not clear gross invoice before WHT.", "Cash ledger; AP aging; multi-currency detail"],
  ["4.3", "AP-003", "Finance Purchase Order", "PO receipt/GRV accrual", "Vendor active; inventory account active", "2026-06-06", "Create PO and goods receipt PO-GRV-001.", "Dr Inventory; Cr GRV/accrual account 2100.", "Receipt quantity cannot exceed ordered/open quantity.", "Detailed Ledger 1200/2100; PO receipt status"],
  ["4.4", "AP-004", "Accounts Payable", "Three-way matched invoice", "PO-GRV-001 posted", "2026-06-07", "Create AP-INV-PO-001 from GRV receipt.", "GRV accrual clears; input VAT posts; AP gross invoice created.", "Price/quantity mismatch should produce match exception.", "AP aging; detailed ledger; tax report"],
  ["4.5", "AP-005", "Accounts Payable", "Payment with early discount", "2_10_NET30 active", "2026-06-20", "Post AP-INV-003 and AP-PMT-003 inside discount period.", "Discount credits 4910 and AP clears.", "Discount should be based on eligible base amount and timing.", "Supplier statement; detailed ledger 4910"],
  ["5.1", "CASH-001", "Cash Management", "Bank transfer", "Two cash/bank GL accounts exist", "2026-06-21", "Post BANK-TRF-001.", "Cash leaves 1000 and enters 1010; net cash group unchanged.", "No AR/AP subledger impact.", "Cash bank ledger; cash flow statement"],
  ["5.2", "CASH-002", "Cash Management", "Bank reconciliation adjustments", "Bank statement loaded", "2026-06-22", "Post BANK-REC-001 and BANK-REC-002.", "Charges reduce cash and expenses; interest increases cash and other income.", "Duplicate adjustments and statement line matching.", "Bank reconciliation; cash ledger; income statement"],
  ["6.1", "BUD-001", "Budgeting", "Create FY2026 Original budget scenario", "FY2026 exists", "Before June actuals review", "Create scenario and budget returns for departments 100/200/300.", "Budget entries save in base currency and scenario summary reconciles.", "Status workflow: draft, submitted, approved/locked.", "Budget summary; variance analysis"],
  ["6.2", "BUD-002", "Budgeting", "Budget vs actual", "June postings entered", "After postings", "Enter Budget_Unit financial budget rows and compare actuals.", "Revenue/expense variances match expected GL.", "Favorable/unfavorable rules should be clear; contra accounts handled correctly.", "Budget variance report; income statement"],
  ["7.1", "UNIT-001", "Unit Accounting", "Create unit types/accounts", "Unit accounting migrations applied", "Before quantity posting", "Create EMP, SQFT, HRS unit types and U-1000/U-1100/U-1200/U-1300 accounts.", "Posting accounts can receive entries; parent rolls up child balances.", "If tables are missing, apply unit accounting migrations before testing.", "Unit accounts list"],
  ["7.2", "UNIT-002", "Unit Accounting", "Unit budget and actual posting", "Unit accounts exist", "2026-06-30", "Enter unit budgets and post UJE for headcount/hours.", "Unit balances update after approval/posting and budget variance calculates.", "Negative quantity reversal should require reason and preserve audit.", "Unit budget variance; unit journal list"],
  ["7.3", "UNIT-003", "Allocations", "Allocate overhead using headcount driver", "Unit balances posted", "2026-06-30", "Allocate 30,000 IT/professional overhead based on headcount.", "Allocation percentages reconcile to 100% and target postings balance.", "Driver accounts with zero balance should block or be excluded explicitly.", "Allocation preview/posting; detailed ledger by segment"],
  ["8.1", "FA-001", "Fixed Assets", "Capital asset invoice from AP", "FA-EQP category active", "2026-06-08", "Post FA-ADD-001 from AP line marked for capitalization.", "Asset created; Dr 1520 88,000; Dr tax 17,600; Cr AP 105,600.", "Tax should not be capitalized if deductible; asset source document links back to AP.", "FA register; AP invoice; detailed ledger"],
  ["8.2", "FA-002", "Fixed Assets", "Monthly depreciation run", "Active assets placed in service", "2026-06-30", "Run FA-DEP-001 for June.", "Schedule lines total 14,340.83 and GL posts Dr 6300 / Cr 1590.", "Avoid duplicate depreciation for same period/asset.", "Depreciation schedule; FA register; Trial Balance"],
  ["8.3", "FA-003", "Fixed Assets", "Asset transfer", "Server Rack active", "2026-06-24", "Complete FA-TRF-001 to Development/project P101.", "Location/segment/custodian changes; no GL if no transfer cost.", "Transfer should retain historical location/custodian snapshots.", "Asset history; audit log"],
  ["8.4", "FA-004", "Fixed Assets", "Impairment/valuation", "Server Rack active", "2026-06-26", "Post FA-VAL-001 impairment.", "Book value reduces and GL posts according to configured valuation accounts.", "If impairment account missing, posting should block with a setup error.", "Asset valuation report; detailed ledger"],
  ["8.5", "FA-005", "Fixed Assets", "Asset disposal", "Truck active; depreciation up to date", "2026-06-28", "Post FA-DISP-001.", "Cost and AD clear; proceeds post; gain/loss calculated as 7,187.50.", "Depreciation must be current to disposal date; disposed asset cannot depreciate further.", "Disposal register; FA roll-forward; cash ledger"],
  ["8.6", "FA-006", "Lease Accounting", "Lease recognition and first payment", "Lease settings/accounts configured", "2026-06-29 to 2026-06-30", "Post FA-LEASE-001 and FA-LEASE-PMT-001.", "ROU asset/liability recognized; payment splits principal and interest.", "Missing ROU/interest accounts should block; schedule should reconcile.", "Lease schedule; Balance Sheet; Income Statement"],
  ["9.1", "FX-001", "Multi-Currency", "Month-end revaluation", "Open USD AR/AP balances", "2026-06-30", "Run FX-REVAL-001 and FX-REVAL-002 at 13.1.", "AR gain 4,200; AP loss 3,600; unrealized accounts used.", "Prior revaluation reversal should happen before new adjustment.", "Multi-currency detail; exchange gain/loss report"],
  ["10.1", "RPT-001", "Reporting", "Full report pack after postings", "All test postings complete", "2026-06-30", "Run reports in Reports_Checklist.", "Reports reconcile to Reconciliation_Checks sheet.", "Segment filters, book classification, export totals, and date boundaries.", "All listed reports"],
];

const workflowRows = [
  ["WF-001", "General Ledger", "GL-001", "Manual Journal Entry", "Create draft, submit, approve, post, then reverse", "Maker: Accountant; Approver: Finance Manager; Poster: GL Supervisor", "Draft", "Posted, then Reversed after reversal", "Create an accrual journal, submit for approval, approve as a different user, post, then create reversal with reason.", "No GL balance impact until posting; reversal creates linked opposite journal; audit records each transition.", "Approver must not be same user if maker-checker is enforced; rejection must leave no posted ledger.", "Journal details; approval queue; audit log", "Not Run", ""],
  ["WF-002", "Opening Balances", "OB-001 to OB-004", "Opening Balance Batch", "Prepare, review, approve/sign off, post, lock migration batch", "Maker: Migration User; Approver: Finance Lead", "Draft/Prepared", "Posted/Locked", "Submit AR, AP, GL and FA opening balances for approval before posting the batch.", "1990 clears only after approved posted batch; re-opening requires explicit authorization.", "Do not allow direct edit/delete after sign-off; residual clearing balance must block sign-off.", "Opening batch; Trial Balance; migration sign-off audit", "Not Run", ""],
  ["WF-003", "Accounts Receivable", "AR-INV-001", "Customer Invoice", "Draft invoice, submit/approve if workflow enabled, send/post", "Maker: Billing Clerk; Approver: AR Manager", "Draft", "Sent/Posted", "Create taxable invoice, submit, approve, then send/post.", "Revenue, tax and AR GL lines exist only after approved posting step.", "Rejected invoice should remain unposted and must not affect aging/revenue.", "AR invoice detail; approval queue; detailed ledger", "Not Run", ""],
  ["WF-004", "Accounts Receivable", "AR-CN-001", "Credit Note", "Submit credit note for approval before posting", "Maker: AR Clerk; Approver: Finance Manager", "Draft", "Posted", "Create credit note against AR-INV-001 and route for approval.", "Credit note reverses AR, revenue and tax only after approval/posting.", "Approval should validate credit does not exceed open invoice balance unless policy allows customer credit.", "Customer statement; audit log", "Not Run", ""],
  ["WF-005", "Accounts Receivable", "AR-PMT-001 / AR-PMT-002", "Customer Receipt", "Receive, allocate, clear/approve bank receipt", "Maker: Cashier; Approver: Treasury Lead", "Pending", "Cleared/Allocated", "Record receipt, allocate to invoice, then clear/approve through cash/bank workflow.", "Uncleared receipt should not overstate bank balance if clearing account workflow is enabled.", "Bounced/cancelled receipt must reverse AR allocation and cash posting cleanly.", "Cash ledger; AR aging; audit log", "Not Run", ""],
  ["WF-006", "Accounts Payable", "AP-INV-001", "Vendor Invoice", "Draft, submit, approve, post AP invoice", "Maker: AP Clerk; Approver: AP Manager", "Draft", "Approved/Posted", "Enter vendor service invoice with WHT, submit, reject once, correct, resubmit, approve and post.", "Expense/WHT/AP GL lines appear only after approved posting; rejection leaves invoice unposted.", "Duplicate supplier invoice and missing WHT certificate should be caught before approval.", "AP invoice detail; AP aging; audit log", "Not Run", ""],
  ["WF-007", "Accounts Payable", "AP-PMT-001 / AP-PMT-003", "Vendor Payment", "Draft payment, authorize, process, clear/reconcile", "Maker: AP Clerk; Authorizer: Treasury/Finance Manager", "Draft", "Processed/Cleared/Reconciled", "Create vendor payment, allocate invoices, authorize as different user, process, then mark cleared.", "AP clears only after processed payment; cash impact and FX/discount lines post correctly.", "Unauthorized payment must not reduce bank/AP; over-allocation must be blocked.", "Vendor payment detail; cash ledger; supplier statement", "Not Run", ""],
  ["WF-008", "Accounts Payable", "Payment batch", "Payment Batch", "Create batch, submit for approval, approve, process batch", "Maker: AP Clerk; Approver: Finance Manager", "Draft", "Completed or PartiallyCompleted", "Group eligible AP payments and route the batch through approval.", "Batch totals/count equal included payment items; failed item leaves batch partially completed with reason.", "Individual payments should not process twice after batch retry.", "Payment batch; audit log", "Not Run", ""],
  ["WF-009", "Finance Purchase Order", "PO-GRV-001", "Finance PO and GRV", "Approve PO, receive goods, approve/post receipt if required", "Maker: Procurement/Finance User; Approver: Budget/Finance Manager", "Draft PO / Draft Receipt", "Approved PO / Received Posted GRV", "Create PO, route for approval, issue PO, receive goods and post GRV.", "GRV accrual posts once and idempotency prevents duplicate receipt posting.", "Receipt over remaining quantity and receipt before PO approval must be blocked.", "PO detail; receipt detail; detailed ledger 1200/2100", "Not Run", ""],
  ["WF-010", "Budgeting", "BUD-001 / BUD-002", "Budget Scenario and Return", "Create scenario, assign return, submit, approve/lock, reject path", "Maker: Budget Owner; Approver: Finance Planning Lead", "Draft", "Approved/Locked", "Enter budget returns for departments, submit, reject one return, correct and resubmit, approve/lock scenario.", "Approved budget appears in Budget vs Actual; locked return cannot be edited without reopen/revision.", "Scenario status must control edits and reporting selection.", "Budget summary; variance report; audit log", "Not Run", ""],
  ["WF-011", "Unit Accounting", "UNIT-002", "Unit Journal Entry", "Draft, submit, approve, post, reverse unit journal", "Maker: HR/Operations Analyst; Approver: Finance Manager", "Draft", "Posted then Reversed if reversal tested", "Create headcount/hours UJE, submit, approve, post; then reverse one posted entry with reason.", "Unit account balances update only after posting; reversal creates opposite unit quantities.", "Posting to summary/non-posting account should be blocked before approval.", "Unit journal; unit account balances; audit log", "Not Run", ""],
  ["WF-012", "Unit Accounting", "UNIT-003", "Allocation Rule/Post", "Approve allocation rule/change and generated allocation journal", "Maker: Cost Accountant; Approver: Finance Manager", "Draft/Preview", "Posted", "Preview IT overhead allocation by headcount, submit approval, approve and post generated GL allocation.", "Driver percentages total 100% and generated GL journal balances.", "Zero driver balance, missing target segment, and changed driver after approval must be controlled.", "Allocation preview; detailed ledger by segment", "Not Run", ""],
  ["WF-013", "Fixed Assets", "FA-ADD-001", "Capital Asset Invoice", "Approve AP invoice and FA capitalization", "Maker: AP/FA Clerk; Approver: Fixed Asset Manager", "Draft", "Capitalized/Posted", "Mark AP line as capitalizable, submit invoice/capitalization for approval, approve and post.", "Fixed asset record links to source AP line and GL capitalization; tax treatment follows configuration.", "Asset should not depreciate before placed-in-service date or before capitalization.", "FA register; AP invoice; detailed ledger 1520/2000", "Not Run", ""],
  ["WF-014", "Fixed Assets", "FA-DEP-001", "Depreciation Run", "Preview depreciation, approve/post run", "Maker: FA Accountant; Approver: Finance Manager", "Calculated/Preview", "Posted", "Generate June depreciation preview, review exceptions, approve and post.", "GL total equals depreciation schedule total; run cannot be duplicated for same period.", "Approval should display assets excluded, disposed or not placed in service.", "Depreciation schedule; detailed ledger 6300/1590", "Not Run", ""],
  ["WF-015", "Fixed Assets", "FA-TRF-001", "Asset Transfer", "Request, approve, complete/post transfer", "Maker: Asset Custodian; Approver: Asset Manager", "Draft", "Completed/Posted if transfer cost", "Transfer Server Rack System to Development/project P101 through approval workflow.", "Custodian/location/segment snapshots update after completion; GL only if transfer cost requires posting.", "Rejected transfer must not change current asset assignment.", "Asset history; audit log", "Not Run", ""],
  ["WF-016", "Fixed Assets", "FA-VAL-001", "Impairment / Valuation", "Calculate valuation, submit, approve, post", "Maker: FA Accountant; Approver: Finance Controller", "Calculated", "Posted", "Enter impairment valuation, attach/reference valuation support, approve and post.", "Book value and GL impairment/revaluation accounts update together.", "Missing impairment/revaluation accounts should block posting before approval completion.", "Asset valuation report; detailed ledger", "Not Run", ""],
  ["WF-017", "Fixed Assets", "FA-DISP-001", "Asset Disposal", "Request disposal, approve, post sale/disposal", "Maker: Asset Manager; Approver: Finance Controller", "Draft", "Disposed/Posted", "Dispose truck after depreciation is current, approve and post proceeds/gain/loss.", "Asset status becomes disposed; cost and accumulated depreciation clear; gain/loss posts.", "Cannot dispose inactive/nonexistent asset; cannot depreciate after disposal.", "Disposal register; FA roll-forward; cash ledger", "Not Run", ""],
  ["WF-018", "Lease Accounting", "FA-LEASE-001", "Lease Recognition", "Prepare lease schedule, approve recognition, post", "Maker: Lease Accountant; Approver: Finance Controller", "Draft/Calculated", "Recognized/Posted", "Create lease, review ROU/liability schedule, approve and post recognition.", "ROU asset and lease liability balance; schedule agrees to journal.", "Missing lease control accounts should block approval/posting with clear setup error.", "Lease schedule; Balance Sheet", "Not Run", ""],
  ["WF-019", "Multi-Currency", "FX-REVAL-001 / FX-REVAL-002", "Currency Revaluation", "Preview, approve, post revaluation batch", "Maker: GL Accountant; Approver: Finance Controller", "Preview", "Posted", "Run revaluation preview, verify prior reversal, approve and post.", "Unrealized FX entries post once; reversal history is linked where applicable.", "Rate changes should be locked/audited after approval; missing rates block posting.", "Multi-currency detail; FX gain/loss report", "Not Run", ""],
  ["WF-020", "Cash Management", "BANK-REC-001 / BANK-REC-002", "Bank Reconciliation Adjustments", "Prepare adjustment, approve/post reconciliation", "Maker: Treasury Clerk; Approver: Treasury Lead", "Draft/Unmatched", "Posted/Matched", "Create bank charge and interest adjustments from reconciliation, route for approval if configured, post/match.", "Adjustment journals link to bank statement line and reconciliation batch.", "Duplicate adjustment against same statement line should be blocked.", "Bank reconciliation; cash ledger; audit log", "Not Run", ""],
  ["WF-021", "Approvals / Security", "All approval-enabled documents", "Segregation of Duties", "Attempt self-approval and unauthorized approval", "Maker user and user without approval permission", "PendingApproval", "Blocked", "Submit one document and attempt to approve with the same maker user, then with a user lacking approval permission.", "System blocks self-approval/unauthorized approval if policy is enabled.", "If self-approval is intentionally allowed, document the policy decision for UAT sign-off.", "Approval queue; authorization error; audit log", "Not Run", ""],
  ["WF-022", "Approvals / Audit", "All posted/rejected documents", "Audit Trail", "Review workflow events and comments", "Tester / Auditor", "Any", "Evidence captured", "For each module, confirm Created, Submitted, Approved/Rejected, Posted, Reversed/Voided events and comments are logged.", "Audit entries identify user, timestamp, entity type/id, action and reason/comment.", "Missing rejection reason or poster identity weakens finance controls.", "Finance audit log export", "Not Run", ""],
];

const reportRows = [
  ["Trial Balance", "2026-06-30", "Book=IFRS/Base as configured; include all segments", "Total debits equal total credits; account movements agree to Reconciliation_Checks.", "If out of balance, inspect unposted/partial posting events and journal line generation."],
  ["Detailed Ledger - 1990", "2026-06-01 to 2026-06-30", "Account=000-1990-0000", "Migration clearing net is zero after opening balances.", "Residual suggests missing AR/AP opening offset or retained earnings clearing line."],
  ["Detailed Ledger - 1100", "2026-06-01 to 2026-06-30", "Account=000-1100-0000", "AR movement ties to AR aging/customer statements and expected GL.", "Differences indicate unallocated payments, credit note issue, or FX revaluation omission."],
  ["Detailed Ledger - 2000", "2026-06-01 to 2026-06-30", "Account=000-2000-0000", "AP movement ties to AP aging/supplier statements and expected GL.", "Differences indicate payment allocation, WHT, GRV clearing or FX issue."],
  ["AR Aging", "As of 2026-06-30", "All customers; include FCY where supported", "Golden Coast June invoice fully settled; opening AR and USD open item remain.", "Aging bucket uses due dates and should not include fully paid invoice."],
  ["Customer Statement", "2026-06-01 to 2026-06-30", "Golden Coast Homes Ltd and USD Customer", "Invoice, receipt, credit note and WHT entries are in chronological order.", "Statement balance must equal AR ledger for each customer."],
  ["AP Aging", "As of 2026-06-30", "All suppliers; include FCY where supported", "Paid AP invoices removed; opening USD AP and unpaid invoices remain.", "WHT should not remain as supplier payable."],
  ["Supplier Statement", "2026-06-01 to 2026-06-30", "Adom Construction Ltd and USD Supplier", "Invoices, payments, discounts and WHT display correctly.", "Matched GRV invoice should not duplicate liability."],
  ["Cash/Bank Ledger", "2026-06-01 to 2026-06-30", "Bank/Cash accounts 1000 and 1010", "Opening plus receipts less payments equals closing cash check.", "Bank transfer should be neutral at consolidated cash level."],
  ["Bank Reconciliation", "June 2026", "Operating bank account", "Bank charge and interest adjustments are matched/posted.", "Duplicate statement adjustment should be blocked or reversed."],
  ["Income Statement", "2026-06-01 to 2026-06-30", "By DEPT and Project where supported", "Revenue, COGS/opex, depreciation, FX and disposal gain agree to expected GL.", "Contra revenue and discount accounts should present correctly."],
  ["Balance Sheet", "As of 2026-06-30", "All segments", "Cash, AR, AP, FA cost, accumulated depreciation, debt and equity agree to expected GL.", "Control accounts should reconcile to subledgers."],
  ["Cash Flow Statement", "2026-06-01 to 2026-06-30", "Indirect/direct method as implemented", "Cash changes agree to Cash/Bank Ledger.", "FA additions/disposals should classify as investing activity if supported."],
  ["Multi-Currency Detail", "As of 2026-06-30", "Currency=USD", "Open USD AR/AP balances and revaluation entries shown with source rates.", "Realized and unrealized FX must be separated."],
  ["Tax Report", "2026-06-01 to 2026-06-30", "VAT/WHT all counterparties", "Output VAT, input VAT, supplier WHT and customer WHT agree to 2200 ledger.", "Inactive COVID levy should not appear in standard VAT scheme."],
  ["Fixed Asset Register", "As of 2026-06-30", "All active/disposed assets", "Cost, AD and NBV agree to fixed asset roll-forward and GL.", "Disposed truck should no longer be active."],
  ["Depreciation Schedule", "June 2026", "All active assets", "Posted depreciation totals 14,340.83 before impairment/disposal effects.", "No duplicate period lines; new asset convention applied once."],
  ["Asset Disposal Register", "June 2026", "Disposals only", "Truck disposal proceeds, NBV and gain/loss agree to expected GL.", "Gain/loss account mapping should be explicit."],
  ["Budget vs Actual", "June 2026", "FY2026 Original; departments 100/200/300", "Actuals from GL compare to approved budget entries.", "Budget scenario and return statuses should prevent edits after approval/lock."],
  ["Unit Budget Variance", "June 2026", "EMP/SQFT/HRS accounts", "Actual unit balances compare to unit budgets.", "Parent unit accounts should roll up child postings."],
  ["Allocation Preview/Posting", "June 2026", "Driver=EMP headcount", "Allocation percentages sum to 100%; generated journals balance.", "Zero or missing driver balance should be handled explicitly."],
  ["Finance Dashboard", "After all postings", "Default tenant", "Headline KPIs reflect current AR/AP/cash/revenue/expense position.", "Dashboard should not include draft/unposted documents."],
  ["Audit/Event Log", "All June tests", "Filter by Finance modules", "Create, submit, approve, post, reverse and export actions are logged.", "Missing audit events weaken UAT sign-off evidence."],
];

const negativeRows = [
  ["NEG-001", "Posting date in closed period", "Create a GL journal dated 2026-05-31.", "Posting blocked with closed-period message.", "High"],
  ["NEG-002", "Unbalanced GL journal", "Create a journal where debits do not equal credits.", "Save/post blocked; no ledger lines created.", "High"],
  ["NEG-003", "Direct posting to blocked account", "Try direct post to 1500, 2200 or 3100.", "Blocked unless system workflow is the source.", "High"],
  ["NEG-004", "Duplicate supplier invoice", "Create same supplier invoice number/date for same supplier.", "Duplicate warning/block per AP rules.", "High"],
  ["NEG-005", "PO receipt over remaining quantity", "Receive more than ordered/open PO quantity.", "Receipt rejected.", "High"],
  ["NEG-006", "AP payment over unallocated invoice balance", "Allocate payment greater than AP invoice balance.", "Allocation rejected or excess remains unapplied explicitly.", "High"],
  ["NEG-007", "AR receipt over invoice balance", "Allocate receipt greater than AR invoice balance.", "Allocation rejected or excess customer advance recorded explicitly.", "High"],
  ["NEG-008", "Missing USD exchange rate", "Post USD invoice/payment on date with no rate.", "Posting blocked or prompts for valid rate; no silent 1.0 rate.", "High"],
  ["NEG-009", "Remove currency link with history", "Attempt to remove USD link from AR/AP account after transactions.", "Blocked or inactivated only; history preserved.", "Medium"],
  ["NEG-010", "Duplicate depreciation run", "Run June depreciation twice for same asset/date.", "Second run blocked or posts zero with audit explanation.", "High"],
  ["NEG-011", "Dispose asset before current depreciation", "Dispose active asset without current depreciation through disposal date.", "System warns/blocks or computes catch-up depreciation.", "High"],
  ["NEG-012", "Asset transfer without approval", "Move transfer from Draft directly to Completed.", "Workflow prevents status skip if approvals are required.", "Medium"],
  ["NEG-013", "Budget edit after approval/lock", "Edit approved/locked budget entry.", "Edit rejected unless scenario is reopened/revised.", "Medium"],
  ["NEG-014", "Unit posting to summary account", "Post Unit Journal Entry to U-1000 summary account.", "Posting blocked because account is not a posting account.", "Medium"],
  ["NEG-015", "Allocation with zero driver", "Run allocation when one target driver balance is zero.", "Zero target excluded or receives zero; total allocation remains reconciled.", "Medium"],
  ["NEG-016", "Inactive VAT component", "Confirm COVID19 is not included in VAT-STD-SCHEME.", "Tax calculation remains 20%, not 21%.", "High"],
  ["NEG-017", "Voiding posted invoice", "Void a posted AR/AP invoice after settlement.", "Requires reversal/credit workflow; no silent delete.", "High"],
  ["NEG-018", "Cross-tenant access", "Attempt to open documents from another tenant or forged tenant id.", "Access denied; no data leakage.", "High"],
];

const budgetRows = [
  ["Financial Budget", "4100", "Service Revenue", "June 2026", "100", 140000, null, null, null, "Budget for services in Finance/Admin and project billing."],
  ["Financial Budget", "6200", "Utilities Expense", "June 2026", "200", 15000, null, null, null, "Budget for Estates utilities."],
  ["Financial Budget", "6300", "Depreciation Expense", "June 2026", "000", 15000, null, null, null, "Budget depreciation before impairment."],
  ["Financial Budget", "6500", "Professional Fees", "June 2026", "100", 25000, null, null, null, "Budget advisory and bank charges."],
  ["Financial Budget", "1200", "Inventory Additions", "June 2026", "300", 30000, null, null, null, "Budget inventory receipt value."],
  ["Unit Budget", "U-1100", "Finance & Admin Employees", "June 2026", "100", 12, 13, null, null, "EMP actual from unit journal entry."],
  ["Unit Budget", "U-1200", "Development & Engineering Employees", "June 2026", "300", 45, 47, null, null, "EMP actual from unit journal entry."],
  ["Unit Budget", "U-1300", "Estates Management Employees", "June 2026", "200", 28, 27, null, null, "EMP actual from unit journal entry."],
  ["Unit Budget", "U-2100", "Headquarters Office Space", "June 2026", "100", 15000, 15000, null, null, "SQFT actual from unit journal entry."],
  ["Unit Budget", "U-3100", "Generator Machine Hours", "June 2026", "300", 2200, 2350, null, null, "HRS actual from unit journal entry."],
  ["Allocation Driver", "ALLOC-IT-001", "IT overhead allocation by headcount", "June 2026", "All", 30000, 30000, null, null, "Allocate across U-1100/U-1200/U-1300 actual headcount."],
];

const fixedAssetTests = [
  ["Opening", "FA-2024-BLDG-001", "Headquarters Building", "FA-BLDG", "Opening asset", "2026-06-01", 1200000, 50000, 0, 50000, 240, null, null, null, "Active", "Opening book value", "Cost and AD tie to GL."],
  ["Opening", "FA-2024-EQP-001", "Industrial Generator", "FA-EQP", "Opening asset", "2026-06-01", 150000, 10000, 0, 10000, 60, null, null, null, "Active", "Opening book value", "Serial GEN-500K-9988 if visible."],
  ["Opening", "FA-2024-EQP-002", "Server Rack System", "FA-EQP", "Opening asset", "2026-06-01", 45000, 5000, 0, 0, 60, null, null, null, "Active", "Opening book value", "Transfer and impairment target."],
  ["Opening", "FA-2024-VEH-001", "Delivery Truck - Toyota Hilux", "FA-VEH", "Opening asset", "2026-06-01", 250000, 0, 0, 25000, 48, null, null, null, "Active", "Opening book value", "Disposal target."],
  ["Addition", "FA-2026-EQP-003", "Network Security Appliance", "FA-EQP", "FA-ADD-001", "2026-06-08", 80000, 8000, 17600, 8800, 60, null, null, null, "Capitalized", "AP capital asset invoice", "Deductible tax should go to 2200, not acquisition cost."],
  ["Depreciation", "ALL-ACTIVE", "June depreciation run", "Mixed", "FA-DEP-001", "2026-06-30", 0, 0, 0, 0, 0, 14340.83, null, null, "Posted", "Dr 6300 / Cr 1590", "No duplicate run for June."],
  ["Transfer", "FA-2024-EQP-002", "Server Rack System", "FA-EQP", "FA-TRF-001", "2026-06-24", 0, 0, 0, 0, 0, 0, null, null, "Completed", "No GL expected if no transfer cost", "Location/segment/custodian history preserved."],
  ["Impairment", "FA-2024-EQP-002", "Server Rack System", "FA-EQP", "FA-VAL-001", "2026-06-26", 0, 0, 0, 0, 0, 10000, null, null, "Posted", "Dr impairment expense / Cr carrying amount", "Check configured impairment account or fallback behavior."],
  ["Disposal", "FA-2024-VEH-001", "Delivery Truck - Toyota Hilux", "FA-VEH", "FA-DISP-001", "2026-06-28", 250000, 0, 0, 25000, 48, 7187.5, null, null, "Disposed", "Clear cost/AD and post 7,187.50 gain", "Disposal should prevent further depreciation."],
  ["Lease", "LEASE-2026-001", "Office Lease ROU Asset", "ROU fallback", "FA-LEASE-001", "2026-06-29", 300000, 0, 0, 0, 60, null, null, null, "Recognized", "Dr ROU asset / Cr lease liability", "Missing lease accounts should block posting clearly."],
  ["Lease Payment", "LEASE-2026-001", "Office Lease ROU Asset", "ROU fallback", "FA-LEASE-PMT-001", "2026-06-30", 0, 0, 0, 0, 0, 6000, null, null, "Posted", "Principal 4,500; interest 1,500", "Lease schedule remaining liability should reconcile."],
];

const sources = [
  ["Finance seeded chart, segments, payment terms, taxes, fixed assets", "src/ErpSystem.Data/Seeders/FinanceDataSeeder.cs", "Code seed source", "Read 2026-07-16"],
  ["Accounts Payable entities and posting assumptions", "src/ErpSystem.Core/Entities/Finance/AccountsPayable.cs", "Code entity source", "Read 2026-07-16"],
  ["Accounts Receivable invoice/payment entities", "src/ErpSystem.Core/Entities/Finance/Invoice.cs; CustomerPayment.cs", "Code entity source", "Read 2026-07-16"],
  ["General financial report endpoints", "src/ErpSystem.Api/Controllers/Finance/FinanceController.cs", "Code API source", "Read 2026-07-16"],
  ["Finance workflow and approval controller", "src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs", "Code API source", "Read 2026-07-16"],
  ["Fixed asset entities", "src/ErpSystem.Core/Entities/Finance/FixedAssets/*.cs", "Code entity source", "Read 2026-07-16"],
  ["Unit accounting workflow", "docs/unit_accounts_user_guide.md", "Repo documentation", "Read 2026-07-16"],
  ["Business partner names/codes", "Local SQL Server RHEMAERP BusinessPartners query", "Local database lookup", "Queried 2026-07-16"],
];

function col(n) {
  let s = "";
  while (n > 0) {
    const m = (n - 1) % 26;
    s = String.fromCharCode(65 + m) + s;
    n = Math.floor((n - m) / 26);
  }
  return s;
}

function rangeA1(startRow, startCol, rows, cols) {
  return `${col(startCol)}${startRow}:${col(startCol + cols - 1)}${startRow + rows - 1}`;
}

function writeBlock(sheet, startRow, startCol, rows) {
  if (!rows.length) return;
  sheet.getRange(rangeA1(startRow, startCol, rows.length, rows[0].length)).values = rows;
}

function setFormula(sheet, row, colIndex, formula) {
  sheet.getRange(`${col(colIndex)}${row}`).formulas = [[formula]];
}

function styleTitle(sheet, title, subtitle, cols) {
  sheet.getRange("A1").values = [[title]];
  sheet.getRange(rangeA1(1, 1, 1, cols)).format = {
    fill: colors.navy,
    font: { color: "#FFFFFF", bold: true, size: 16 },
    horizontalAlignment: "left",
  };
  sheet.getRange("A1").format = {
    fill: colors.navy,
    font: { color: "#FFFFFF", bold: true, size: 16 },
    horizontalAlignment: "left",
  };
  sheet.getRange("A1").format.rowHeight = 28;
  if (subtitle) {
    sheet.getRange("A2").values = [[subtitle]];
    sheet.getRange(rangeA1(2, 1, 1, cols)).format = {
      fill: colors.lightBlue,
      font: { color: colors.darkText, italic: true },
      wrapText: true,
    };
    sheet.getRange("A2").format = {
      fill: colors.lightBlue,
      font: { color: colors.darkText, italic: true },
      wrapText: true,
    };
    sheet.getRange("A2").format.rowHeight = 46;
  }
}

function styleHeader(sheet, row, cols, fill = colors.blue) {
  sheet.getRange(rangeA1(row, 1, 1, cols)).format = {
    fill,
    font: { color: "#FFFFFF", bold: true },
    wrapText: true,
    verticalAlignment: "top",
  };
}

function styleData(sheet, startRow, startCol, rows, cols) {
  if (rows <= 0) return;
  const rg = sheet.getRange(rangeA1(startRow, startCol, rows, cols));
  rg.format = {
    borders: { preset: "all", style: "thin", color: colors.border },
    verticalAlignment: "top",
    wrapText: true,
  };
}

function setWidths(sheet, widths, maxRows = 240) {
  widths.forEach((w, idx) => {
    sheet.getRange(`${col(idx + 1)}1:${col(idx + 1)}${maxRows}`).format.columnWidth = w;
  });
}

function addValidation(sheet, range, values) {
  sheet.getRange(range).dataValidation = { rule: { type: "list", values } };
}

function toDate(value) {
  if (!value) return null;
  const [y, m, d] = value.split("-").map(Number);
  return new Date(Date.UTC(y, m - 1, d));
}

async function buildWorkbook() {
  const workbook = Workbook.create();
  workbook.comments.setSelf({ displayName: "User" });

  const readme = workbook.worksheets.add("README");
  styleTitle(readme, "RHEMA ERP Finance Manual Testing Pack", "Manual UAT transaction pack for Finance features built so far. Assumptions: DEFAULT tenant, base currency GHS, open test period June 2026, segmented account format DEPT-ACCT-PROJ.", 8);
  const readmeRows = [
    ["Section", "How to use"],
    ["1. Master_Data", "Confirm that the seeded chart of accounts, segments, payment terms, taxes, partners, and fixed asset categories exist before testing."],
    ["2. Test_Scripts", "Run the test scripts in sequence. Record status, tester initials, date, and evidence/reference in the editable columns."],
    ["3. Workflow_Approvals", "Run approval, rejection, authorization, posting, reversal, segregation-of-duties, and audit-trail tests for every approval-enabled Finance flow."],
    ["4. Transaction_Data", "Use these document-level rows as input data. Amount columns are formula-backed where possible and can be adjusted for your environment."],
    ["5. Expected_GL", "Compare generated ERP journal entries to the expected postings. Use this as a target, not as an import file."],
    ["6. Reconciliation_Checks", "Run the named ERP reports after each major stage and compare totals to the formula-backed checks."],
    ["7. Fixed_Assets", "Covers acquisition, capitalization, depreciation, transfer, impairment/valuation, disposal, and lease/ROU flows."],
    ["8. Budget_Unit", "Covers financial budgeting, unit budgets, unit journals, allocation drivers, and variance checks."],
    ["Open questions for tester", "Confirm whether UAT should use the current local RHEMAERP database or a reset seeded sandbox; confirm bank account master setup; confirm whether approval workflows should be enforced or bypassed for speed."],
    ["Important watchout", "The current local database queried on 2026-07-16 showed some latest seed accounts may be missing or named differently, especially discount and split FX gain/loss accounts. Treat that as a setup defect to resolve before full testing."],
  ];
  writeBlock(readme, 4, 1, readmeRows);
  styleHeader(readme, 4, 2, colors.teal);
  styleData(readme, 5, 1, readmeRows.length - 1, 2);
  setWidths(readme, [28, 110], 40);
  readme.freezePanes.freezeRows(4);

  const master = workbook.worksheets.add("Master_Data");
  styleTitle(master, "Master Data Assumptions", "Seeded and queried data used by this manual test pack. Replace only if your UAT database has different approved partners or account mappings.", 10);
  let r = 4;
  const segmentRows = [
    ["Segment Code", "Name", "Position", "Length", "Lookup Required", "Reporting Dimension", "Natural Account", "Values"],
    ["DEPT", "Department", 1, 3, "Yes", "Yes", "No", "000 General; 100 Finance & Administration; 200 Estates; 300 Development & Engineering; 400 Legal; 500 Planning & Communication; 600 Internal Audit"],
    ["ACCT", "Natural Account", 2, 4, "No", "Yes", "Yes", "Uses natural account code such as 1100, 2000, 4100."],
    ["PROJ", "Project", 3, 4, "Yes", "Yes", "No", "0000 No Project; P101 Kpone Affordable Housing; P102 Oxygen City; P103 Kaiser Flats; P104 Tema Event Center"],
  ];
  writeBlock(master, r, 1, segmentRows);
  styleHeader(master, r, 8);
  styleData(master, r + 1, 1, segmentRows.length - 1, 8);
  r += segmentRows.length + 2;
  const accountHeader = [["Code", "Account Number", "Name", "Type", "Category", "Control", "Direct Posting", "Budget Tracking", "Multi-Currency", "Testing Notes"]];
  writeBlock(master, r, 1, accountHeader.concat(accounts));
  styleHeader(master, r, 10);
  styleData(master, r + 1, 1, accounts.length, 10);
  const accountStart = r + 1;
  r += accounts.length + 3;
  writeBlock(master, r, 1, [["Partner Code", "Name", "Type", "Status", "Currency", "Testing Usage"]].concat(partners));
  styleHeader(master, r, 6);
  styleData(master, r + 1, 1, partners.length, 6);
  r += partners.length + 3;
  writeBlock(master, r, 1, [["Payment Term", "Name", "Due Days", "Discount %", "Discount Days", "Usage Notes"]].concat(paymentTerms));
  styleHeader(master, r, 6);
  styleData(master, r + 1, 1, paymentTerms.length, 6);
  r += paymentTerms.length + 3;
  writeBlock(master, r, 1, [["Tax Code/Group", "Applicability", "Description", "Rate %", "Testing Notes"]].concat(taxRows));
  styleHeader(master, r, 5);
  styleData(master, r + 1, 1, taxRows.length, 5);
  r += taxRows.length + 3;
  writeBlock(master, r, 1, [["Category Code", "Name", "Asset Account", "Accumulated Depreciation", "Depreciation Expense", "Default Method", "Useful Life Months", "Residual %", "Notes"]].concat(fixedAssetRows));
  styleHeader(master, r, 9);
  styleData(master, r + 1, 1, fixedAssetRows.length, 9);
  r += fixedAssetRows.length + 3;
  writeBlock(master, r, 1, [["Unit Type/Account", "Name", "Use", "Posting?", "Notes"],
    ["EMP", "Employees", "Headcount and revenue per employee", "Type", "Create if not already seeded."],
    ["SQFT", "Square Feet", "Facilities cost per square foot", "Type", "Create if not already seeded."],
    ["HRS", "Hours", "Machine/labor hours", "Type", "Create if not already seeded."],
    ["U-1000", "Total Employees", "Rollup", "No", "Parent of department employee accounts."],
    ["U-1100", "Finance & Admin Employees", "Driver", "Yes", "Used in allocation and KPI tests."],
    ["U-1200", "Development & Engineering Employees", "Driver", "Yes", "Used in allocation and KPI tests."],
    ["U-1300", "Estates Management Employees", "Driver", "Yes", "Used in allocation and KPI tests."],
  ]);
  styleHeader(master, r, 5);
  styleData(master, r + 1, 1, 7, 5);
  master.getRange(`A${accountStart}:J${accountStart + accounts.length - 1}`).conditionalFormats.add("containsText", {
    text: "If missing",
    format: { fill: colors.lightRed, font: { color: colors.red } },
  });
  setWidths(master, [15, 18, 34, 16, 24, 12, 15, 15, 16, 70], 180);
  master.freezePanes.freezeRows(3);

  const scripts = workbook.worksheets.add("Test_Scripts");
  styleTitle(scripts, "Manual Test Scripts", "Run in sequence. Columns K:N are for tester execution status and evidence.", 14);
  const scriptHeader = ["Seq", "Test ID", "Module", "Feature/Flow", "Prerequisite", "Date/Timing", "Steps / Test Data", "Expected Result", "Watch Out", "Reports / Evidence", "Run Status", "Tester", "Run Date", "Notes / Evidence Ref"];
  writeBlock(scripts, 4, 1, [scriptHeader].concat(testScripts.map((row) => row.concat(["Not Run", "", "", ""]))));
  styleHeader(scripts, 4, scriptHeader.length);
  styleData(scripts, 5, 1, testScripts.length, scriptHeader.length);
  addValidation(scripts, `K5:K${testScripts.length + 4}`, ["Not Run", "Pass", "Fail", "Blocked", "Retest"]);
  scripts.getRange(`K5:K${testScripts.length + 4}`).conditionalFormats.add("containsText", { text: "Pass", format: { fill: colors.lightGreen, font: { color: colors.green, bold: true } } });
  scripts.getRange(`K5:K${testScripts.length + 4}`).conditionalFormats.add("containsText", { text: "Fail", format: { fill: colors.lightRed, font: { color: colors.red, bold: true } } });
  scripts.getRange(`K5:K${testScripts.length + 4}`).conditionalFormats.add("containsText", { text: "Blocked", format: { fill: colors.lightAmber, font: { color: "#7F6000", bold: true } } });
  setWidths(scripts, [8, 14, 20, 28, 28, 18, 60, 54, 52, 38, 14, 14, 14, 48], 100);
  scripts.freezePanes.freezeRows(4);
  scripts.freezePanes.freezeColumns(2);

  const workflows = workbook.worksheets.add("Workflow_Approvals");
  styleTitle(workflows, "Workflow and Approval Coverage", "Approval paths should not be skipped. Run these alongside the transaction scripts to test maker-checker, authorization, rejection, posting, reversal, security and audit controls.", 14);
  const workflowHeader = ["Workflow ID", "Module", "Source Txn/Test ID", "Entity / Document", "Workflow Action", "Actors / Roles", "Pre-status", "Expected Post-status", "Approval Test Steps", "Expected Control Result", "Watch Out", "Evidence", "Run Status", "Tester Notes"];
  writeBlock(workflows, 4, 1, [workflowHeader].concat(workflowRows));
  styleHeader(workflows, 4, workflowHeader.length, colors.teal);
  styleData(workflows, 5, 1, workflowRows.length, workflowHeader.length);
  addValidation(workflows, `M5:M${workflowRows.length + 4}`, ["Not Run", "Pass", "Fail", "Blocked", "N/A"]);
  workflows.getRange(`M5:M${workflowRows.length + 4}`).conditionalFormats.add("containsText", { text: "Pass", format: { fill: colors.lightGreen, font: { color: colors.green, bold: true } } });
  workflows.getRange(`M5:M${workflowRows.length + 4}`).conditionalFormats.add("containsText", { text: "Fail", format: { fill: colors.lightRed, font: { color: colors.red, bold: true } } });
  workflows.getRange(`M5:M${workflowRows.length + 4}`).conditionalFormats.add("containsText", { text: "Blocked", format: { fill: colors.lightAmber, font: { color: "#7F6000", bold: true } } });
  setWidths(workflows, [14, 20, 18, 28, 36, 42, 18, 24, 58, 58, 54, 36, 14, 48], 80);
  workflows.freezePanes.freezeRows(4);
  workflows.freezePanes.freezeColumns(3);

  const tx = workbook.worksheets.add("Transaction_Data");
  styleTitle(tx, "Transaction Data", "Document-level transactions for manual entry. Amount formulas calculate transaction currency and base currency totals.", 24);
  const txHeader = ["Txn ID", "Date", "Module", "Document Type", "Partner Code", "Partner Name", "Currency", "Rate to GHS", "Dept", "Project", "Description", "Qty", "Unit Price", "Subtotal", "Discount %", "Discount Amt", "Tax Group", "Tax Rate %", "Tax Amt", "WHT Rate %", "WHT Amt", "Gross/Doc Total", "Base Total GHS", "Payment Term", "Due Date", "Expected Status", "Linked Txn", "Notes"];
  const txValues = transactionRows.map((row) => [
    row[0], toDate(row[1]), row[2], row[3], row[4], row[5], row[6], row[7], row[8], row[9], row[10], row[11], row[12], null, row[13], null, row[14], row[15], null, row[16], null, null, null, row[17], toDate(row[18]), row[19], row[20], row[21],
  ]);
  writeBlock(tx, 4, 1, [txHeader].concat(txValues));
  styleHeader(tx, 4, txHeader.length);
  styleData(tx, 5, 1, txValues.length, txHeader.length);
  for (let i = 0; i < txValues.length; i++) {
    const row = i + 5;
    setFormula(tx, row, 14, `=L${row}*M${row}`);
    setFormula(tx, row, 16, `=N${row}*O${row}/100`);
    setFormula(tx, row, 19, `=(N${row}-P${row})*R${row}/100`);
    setFormula(tx, row, 21, `=(N${row}-P${row})*T${row}/100`);
    setFormula(tx, row, 22, `=N${row}-P${row}+S${row}`);
    setFormula(tx, row, 23, `=V${row}*H${row}`);
  }
  tx.getRange(`B5:B${txValues.length + 4}`).setNumberFormat("yyyy-mm-dd");
  tx.getRange(`M5:W${txValues.length + 4}`).setNumberFormat("#,##0.00");
  tx.getRange(`O5:O${txValues.length + 4}`).setNumberFormat("0.0%");
  tx.getRange(`R5:R${txValues.length + 4}`).setNumberFormat("0.0");
  tx.getRange(`T5:T${txValues.length + 4}`).setNumberFormat("0.0");
  tx.getRange(`Y5:Y${txValues.length + 4}`).setNumberFormat("yyyy-mm-dd");
  setWidths(tx, [16, 13, 22, 26, 22, 28, 10, 12, 9, 10, 44, 8, 14, 14, 12, 14, 16, 12, 14, 12, 14, 16, 16, 16, 13, 18, 16, 54], 80);
  tx.freezePanes.freezeRows(4);
  tx.freezePanes.freezeColumns(2);

  const gl = workbook.worksheets.add("Expected_GL");
  styleTitle(gl, "Expected GL Postings", "Expected debit/credit lines to compare against generated journal entries. Use this as a validation target, not as an import template.", 12);
  const glHeader = ["Txn ID", "Date", "Module", "Source Doc", "Account Number", "Account Name", "DR/CR", "Amount GHS", "Segment", "Memo", "Txn Net Check", "Line Status"];
  writeBlock(gl, 4, 1, [glHeader].concat(glRows));
  styleHeader(gl, 4, glHeader.length);
  styleData(gl, 5, 1, glRows.length, glHeader.length);
  for (let i = 0; i < glRows.length; i++) {
    const row = i + 5;
    setFormula(gl, row, 11, `=SUMIFS($H$5:$H$200,$A$5:$A$200,A${row},$G$5:$G$200,"DR")-SUMIFS($H$5:$H$200,$A$5:$A$200,A${row},$G$5:$G$200,"CR")`);
    setFormula(gl, row, 12, `=IF(ABS(K${row})<0.01,"Balanced","Check Txn")`);
  }
  gl.getRange(`B5:B${glRows.length + 4}`).setNumberFormat("yyyy-mm-dd");
  gl.getRange(`H5:H${glRows.length + 4}`).setNumberFormat("#,##0.00");
  gl.getRange(`K5:K${glRows.length + 4}`).setNumberFormat("#,##0.00");
  gl.getRange(`L5:L${glRows.length + 4}`).conditionalFormats.add("containsText", { text: "Balanced", format: { fill: colors.lightGreen, font: { color: colors.green } } });
  gl.getRange(`L5:L${glRows.length + 4}`).conditionalFormats.add("containsText", { text: "Check", format: { fill: colors.lightRed, font: { color: colors.red, bold: true } } });
  setWidths(gl, [16, 13, 22, 16, 18, 32, 9, 16, 24, 46, 14, 14], 150);
  gl.freezePanes.freezeRows(4);
  gl.freezePanes.freezeColumns(2);

  const fa = workbook.worksheets.add("Fixed_Assets");
  styleTitle(fa, "Fixed Asset Transaction Flows", "Opening asset register plus acquisition, depreciation, transfer, impairment, disposal, and lease accounting test rows.", 17);
  const faHeader = ["Flow", "Asset Code", "Asset Name", "Category", "Source Txn", "Txn Date", "Purchase Price", "Install/Cap Cost", "Tax", "Residual Value", "Useful Life Months", "Manual/Test Amount", "Calculated Acquisition Cost", "Monthly Dep / Test Amount", "Expected Status", "Expected GL / Accounting", "Watch Out"];
  writeBlock(fa, 4, 1, [faHeader].concat(fixedAssetTests));
  styleHeader(fa, 4, faHeader.length);
  styleData(fa, 5, 1, fixedAssetTests.length, faHeader.length);
  for (let i = 0; i < fixedAssetTests.length; i++) {
    const row = i + 5;
    setFormula(fa, row, 13, `=G${row}+H${row}`);
    setFormula(fa, row, 14, `=IF(OR(A${row}="Opening",A${row}="Addition",A${row}="Lease"),IF(K${row}>0,(M${row}-J${row})/K${row},0),L${row})`);
  }
  fa.getRange(`F5:F${fixedAssetTests.length + 4}`).setNumberFormat("yyyy-mm-dd");
  fa.getRange(`G5:N${fixedAssetTests.length + 4}`).setNumberFormat("#,##0.00");
  setWidths(fa, [16, 18, 30, 14, 18, 13, 15, 15, 13, 14, 16, 16, 18, 20, 16, 44, 56], 60);
  fa.freezePanes.freezeRows(4);
  fa.freezePanes.freezeColumns(2);

  const budget = workbook.worksheets.add("Budget_Unit");
  styleTitle(budget, "Budgeting, Unit Accounting, Ratios and Allocations", "Financial budget vs actual and unit budget variance rows. Actual financial values pull from Expected_GL.", 11);
  const budgetHeader = ["Area", "Code", "Name", "Period", "Dept", "Budget", "Actual", "Variance", "Variance %", "Status", "Notes"];
  writeBlock(budget, 4, 1, [budgetHeader].concat(budgetRows));
  styleHeader(budget, 4, budgetHeader.length);
  styleData(budget, 5, 1, budgetRows.length, budgetHeader.length);
  const actualFormulaByCode = {
    "4100": `=SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-4100-0000",'Expected_GL'!$G$5:$G$200,"CR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-4100-0000",'Expected_GL'!$G$5:$G$200,"DR")`,
    "6200": `=SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-6200-0000",'Expected_GL'!$G$5:$G$200,"DR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-6200-0000",'Expected_GL'!$G$5:$G$200,"CR")`,
    "6300": `=SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-6300-0000",'Expected_GL'!$G$5:$G$200,"DR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-6300-0000",'Expected_GL'!$G$5:$G$200,"CR")`,
    "6500": `=SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-6500-0000",'Expected_GL'!$G$5:$G$200,"DR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-6500-0000",'Expected_GL'!$G$5:$G$200,"CR")`,
    "1200": `=SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1200-0000",'Expected_GL'!$G$5:$G$200,"DR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1200-0000",'Expected_GL'!$G$5:$G$200,"CR")`,
  };
  for (let i = 0; i < budgetRows.length; i++) {
    const row = i + 5;
    const codeValue = budgetRows[i][1];
    if (actualFormulaByCode[codeValue]) setFormula(budget, row, 7, actualFormulaByCode[codeValue]);
    setFormula(budget, row, 8, `=G${row}-F${row}`);
    setFormula(budget, row, 9, `=IF(F${row}=0,0,H${row}/F${row})`);
    setFormula(budget, row, 10, `=IF(ABS(H${row})<0.01,"On Budget",IF(H${row}>0,"Over/Above","Under/Below"))`);
  }
  budget.getRange(`F5:I${budgetRows.length + 4}`).setNumberFormat("#,##0.00");
  budget.getRange(`I5:I${budgetRows.length + 4}`).setNumberFormat("0.0%");
  budget.getRange(`J5:J${budgetRows.length + 4}`).conditionalFormats.add("containsText", { text: "On Budget", format: { fill: colors.lightGreen, font: { color: colors.green } } });
  budget.getRange(`J5:J${budgetRows.length + 4}`).conditionalFormats.add("containsText", { text: "Over", format: { fill: colors.lightAmber, font: { color: "#7F6000" } } });
  budget.getRange(`J5:J${budgetRows.length + 4}`).conditionalFormats.add("containsText", { text: "Under", format: { fill: colors.lightBlue, font: { color: colors.blue } } });
  setWidths(budget, [20, 16, 34, 14, 10, 14, 14, 14, 12, 15, 58], 60);
  budget.freezePanes.freezeRows(4);

  const checks = workbook.worksheets.add("Reconciliation_Checks");
  styleTitle(checks, "Reconciliation Checks", "Formula-backed checks to compare against ERP reports after each testing stage.", 8);
  const checkRows = [
    ["CHK-001", "Overall GL", "Expected GL total debits less credits", null, null, "Trial Balance total debits/credits", "All expected postings should balance."],
    ["CHK-002", "Migration Clearing", "Account 1990 net movement", null, null, "Detailed Ledger 1990", "Should be zero after opening balances."],
    ["CHK-003", "AR Control", "Account 1100 net debit balance", null, null, "AR Aging and Detailed Ledger 1100", "Should reconcile to customer subledger."],
    ["CHK-004", "AP Control", "Account 2000 net credit balance", null, null, "AP Aging and Detailed Ledger 2000", "Should reconcile to supplier subledger."],
    ["CHK-005", "Cash", "Accounts 1000 and 1010 net debit balance", null, null, "Cash/Bank Ledger", "Should reconcile to bank statement and cash reports."],
    ["CHK-006", "Tax/VAT Control", "Account 2200 net credit/(debit) balance", null, null, "Tax Report", "Debit balance can occur if input VAT/customer WHT exceeds output/WHT payable."],
    ["CHK-007", "Fixed Asset Cost", "FA cost accounts 1510/1520/1530 net debit", null, null, "Fixed Asset Register and Balance Sheet", "Should tie to FA cost roll-forward."],
    ["CHK-008", "Accumulated Depreciation", "Account 1590 net credit balance", null, null, "Depreciation Schedule and Balance Sheet", "Should tie to FA accumulated depreciation roll-forward."],
    ["CHK-009", "Service Revenue", "Account 4100 net credit", null, null, "Income Statement", "Should include AR invoices less credit notes."],
    ["CHK-010", "FX Results", "Realized/unrealized FX net income impact", null, null, "Multi-Currency Detail and Income Statement", "Realized and unrealized should remain separate."],
    ["CHK-011", "Budget Revenue Variance", "Budget_Unit service revenue variance", null, null, "Budget vs Actual", "Actual should be sourced from posted GL only."],
    ["CHK-012", "Unit Headcount", "Unit budget actual total headcount", null, null, "Unit Budget Variance", "Parent unit account should equal child total."],
  ];
  writeBlock(checks, 4, 1, [["Check ID", "Area", "Calculation", "Delta / Value", "Status", "Report Evidence", "Investigate If Fail"]].concat(checkRows));
  styleHeader(checks, 4, 7);
  styleData(checks, 5, 1, checkRows.length, 7);
  const formulas = [
    `=SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$G$5:$G$200,"DR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$G$5:$G$200,"CR")`,
    `=SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1990-0000",'Expected_GL'!$G$5:$G$200,"DR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1990-0000",'Expected_GL'!$G$5:$G$200,"CR")`,
    `=SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1100-0000",'Expected_GL'!$G$5:$G$200,"DR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1100-0000",'Expected_GL'!$G$5:$G$200,"CR")`,
    `=SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-2000-0000",'Expected_GL'!$G$5:$G$200,"CR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-2000-0000",'Expected_GL'!$G$5:$G$200,"DR")`,
    `=SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1000-0000",'Expected_GL'!$G$5:$G$200,"DR")+SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1010-0000",'Expected_GL'!$G$5:$G$200,"DR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1000-0000",'Expected_GL'!$G$5:$G$200,"CR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1010-0000",'Expected_GL'!$G$5:$G$200,"CR")`,
    `=SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-2200-0000",'Expected_GL'!$G$5:$G$200,"CR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-2200-0000",'Expected_GL'!$G$5:$G$200,"DR")`,
    `=SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1510-0000",'Expected_GL'!$G$5:$G$200,"DR")+SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1520-0000",'Expected_GL'!$G$5:$G$200,"DR")+SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1530-0000",'Expected_GL'!$G$5:$G$200,"DR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1510-0000",'Expected_GL'!$G$5:$G$200,"CR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1520-0000",'Expected_GL'!$G$5:$G$200,"CR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1530-0000",'Expected_GL'!$G$5:$G$200,"CR")`,
    `=SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1590-0000",'Expected_GL'!$G$5:$G$200,"CR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-1590-0000",'Expected_GL'!$G$5:$G$200,"DR")`,
    `=SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-4100-0000",'Expected_GL'!$G$5:$G$200,"CR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-4100-0000",'Expected_GL'!$G$5:$G$200,"DR")`,
    `=SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-7100-0000",'Expected_GL'!$G$5:$G$200,"CR")+SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-7200-0000",'Expected_GL'!$G$5:$G$200,"CR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-7110-0000",'Expected_GL'!$G$5:$G$200,"DR")-SUMIFS('Expected_GL'!$H$5:$H$200,'Expected_GL'!$E$5:$E$200,"000-7210-0000",'Expected_GL'!$G$5:$G$200,"DR")`,
    `='Budget_Unit'!H5`,
    `=SUM('Budget_Unit'!G10:G12)`,
  ];
  formulas.forEach((formula, idx) => {
    const row = idx + 5;
    setFormula(checks, row, 4, formula);
    setFormula(checks, row, 5, idx < 2 ? `=IF(ABS(D${row})<0.01,"PASS","CHECK")` : `=IF(ISNUMBER(D${row}),"REVIEW","CHECK")`);
  });
  checks.getRange("D5:D16").setNumberFormat("#,##0.00");
  checks.getRange("E5:E16").conditionalFormats.add("containsText", { text: "PASS", format: { fill: colors.lightGreen, font: { color: colors.green, bold: true } } });
  checks.getRange("E5:E16").conditionalFormats.add("containsText", { text: "CHECK", format: { fill: colors.lightAmber, font: { color: "#7F6000", bold: true } } });
  checks.getRange("E5:E16").conditionalFormats.add("containsText", { text: "REVIEW", format: { fill: colors.lightBlue, font: { color: colors.blue, bold: true } } });
  setWidths(checks, [14, 22, 46, 18, 12, 38, 66], 50);
  checks.freezePanes.freezeRows(4);

  const reports = workbook.worksheets.add("Reports_Checklist");
  styleTitle(reports, "Report Checklist", "Reports and report filters to run as evidence during manual Finance module testing.", 7);
  writeBlock(reports, 4, 1, [["Report", "Date/Period", "Filters", "Expected Evidence", "If It Does Not Tie"]].concat(reportRows));
  styleHeader(reports, 4, 5);
  styleData(reports, 5, 1, reportRows.length, 5);
  setWidths(reports, [30, 18, 44, 60, 60], 80);
  reports.freezePanes.freezeRows(4);

  const neg = workbook.worksheets.add("Negative_Tests");
  styleTitle(neg, "Negative and Control Tests", "Validation scenarios to confirm the module blocks unsafe or invalid finance activity.", 5);
  writeBlock(neg, 4, 1, [["Test ID", "Scenario", "Action", "Expected Result", "Risk"]].concat(negativeRows));
  styleHeader(neg, 4, 5, colors.red);
  styleData(neg, 5, 1, negativeRows.length, 5);
  neg.getRange(`E5:E${negativeRows.length + 4}`).conditionalFormats.add("containsText", { text: "High", format: { fill: colors.lightRed, font: { color: colors.red, bold: true } } });
  neg.getRange(`E5:E${negativeRows.length + 4}`).conditionalFormats.add("containsText", { text: "Medium", format: { fill: colors.lightAmber, font: { color: "#7F6000" } } });
  setWidths(neg, [14, 32, 58, 58, 12], 50);
  neg.freezePanes.freezeRows(4);

  const sourceSheet = workbook.worksheets.add("Sources");
  styleTitle(sourceSheet, "Sources and Build Notes", "Local repo and database sources used to create this workbook.", 4);
  writeBlock(sourceSheet, 4, 1, [["Item", "Source", "Source Type", "Date"]].concat(sources));
  styleHeader(sourceSheet, 4, 4);
  styleData(sourceSheet, 5, 1, sources.length, 4);
  setWidths(sourceSheet, [40, 80, 24, 18], 40);
  sourceSheet.freezePanes.freezeRows(4);

  const sheets = [readme, master, scripts, workflows, tx, gl, fa, budget, checks, reports, neg, sourceSheet];
  for (const sheet of sheets) {
    sheet.showGridLines = false;
    sheet.getRange("A1:AB220").format.font = { name: "Aptos", size: 10 };
  }

  await fs.mkdir(outputDir, { recursive: true });

  const inspectCheck = await workbook.inspect({
    kind: "table",
    range: "Reconciliation_Checks!A4:G16",
    include: "values,formulas",
    tableMaxRows: 20,
    tableMaxCols: 8,
    maxChars: 4000,
  });
  console.log(inspectCheck.ndjson);

  const errors = await workbook.inspect({
    kind: "match",
    searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
    options: { useRegex: true, maxResults: 300 },
    summary: "final formula error scan",
  });
  console.log(errors.ndjson);

  const renderRanges = {
    README: "A1:B14",
    Master_Data: "A1:J90",
    Test_Scripts: "A1:N47",
    Workflow_Approvals: "A1:N27",
    Transaction_Data: "A1:AB36",
    Expected_GL: "A1:L90",
    Fixed_Assets: "A1:Q16",
    Budget_Unit: "A1:K16",
    Reconciliation_Checks: "A1:G16",
    Reports_Checklist: "A1:E28",
    Negative_Tests: "A1:E23",
    Sources: "A1:D12",
  };

  for (const sheet of sheets) {
    const preview = await workbook.render({ sheetName: sheet.name, range: renderRanges[sheet.name], scale: 1, format: "png" });
    await fs.writeFile(path.join(outputDir, `${sheet.name}.png`), new Uint8Array(await preview.arrayBuffer()));
    console.log(`Rendered ${sheet.name}`);
  }

  const xlsx = await SpreadsheetFile.exportXlsx(workbook);
  await xlsx.save(outputPath);
  console.log(`Saved ${outputPath}`);
}

await buildWorkbook();

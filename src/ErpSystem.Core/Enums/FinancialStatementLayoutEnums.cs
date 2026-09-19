namespace ErpSystem.Core.Enums;

public enum FinancialStatementType
{
    BalanceSheet = 1,
    IncomeStatement = 2
}

public enum FinancialStatementLayoutVersionStatus
{
    Draft = 1,
    Published = 2,
    Retired = 3
}

public enum FinancialStatementRowType
{
    Header = 1,
    Account = 2,
    Formula = 3,
    Total = 4,
    Spacer = 5
}

public enum FinancialStatementRowMappingType
{
    Account = 1,
    AccountRange = 2,
    AccountHierarchy = 3,
    Classification = 4
}

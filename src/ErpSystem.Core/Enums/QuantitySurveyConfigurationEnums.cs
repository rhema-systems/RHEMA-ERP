namespace ErpSystem.Core.Enums;

public enum QuantitySurveyConfigurationProfileStatus { Draft = 0, Published = 1, Retired = 2 }
public enum QuantitySurveyConfigurationDecisionStatus { Draft = 0, Proposed = 1, Approved = 2, Rejected = 3 }
public enum QuantitySurveyConfigurationApprovalStatus { Pending = 0, Approved = 1, Rejected = 2 }
public enum QuantitySurveyConfigurationEvidenceStatus { Missing = 0, Attached = 1, Verified = 2 }

public enum QuantitySurveyBoqStandard { Smm7, Cesmm3, Cesmm4, TdcLocal }
public enum QuantitySurveyBoqVersionType { Original, Tender, Approved, Revised, Remeasurement, TerminatedRepackaged, FinalAccount }
public enum QuantitySurveyRateComponent { Material, Labour, Plant, Equipment, Subcontract, Overhead, Profit, Attendance, Contingency, Wastage, Transport, Other }
public enum QuantitySurveyRateDimension { ProjectType, Location, Contractor, Supplier, Period, Material, Plant, Equipment }
public enum QuantitySurveyIndexSource { GssPbci, RoadsInfrastructure, ControlledManualImport }
public enum QuantitySurveyEscalationFormula { FixedCoefficientIndexRatio, ContractDefinedFormula }
public enum QuantitySurveyTaxHandling { Exclusive, Inclusive, FinanceCalculated }
public enum QuantitySurveyMaterialValuationBasis { DeliveredCost, ApprovedRate, LowerOfCostOrApprovedRate }
public enum QuantitySurveyExternalSubmissionChannel { ExternalPortal, ControlledExcel, Api, SignedPdf }
public enum QuantitySurveyThirdPartyTool { Candy, PlanSwift, BaniEstimation, Cad, Bim }
public enum QuantitySurveyIntegrationModule { Projects, Procurement, Inventory, Contracts, AccountsPayable, AccountsReceivable, GeneralLedger, DocumentManagement, Workflow }
public enum QuantitySurveyPostingMode { ReadOnlyReference, ControlledEvent, SynchronousApi }
public enum QuantitySurveyMigrationSource { Excel, Csv, LegacyDatabase, PhysicalRecord, CentralDms }
public enum QuantitySurveyBoqImportStatus { Previewed = 0, Invalid = 1, Committed = 2, Expired = 3, Failed = 4 }

import { getStoredToken } from '@/services/api.service';
import type { ControlledDocumentCopyType, ControlledDocumentIssueSummary } from '@/types/controlled-documents';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

export const DOCUMENT_TYPES = {
  financeJournalVoucher: 'Finance.JournalVoucher',
  financeApPaymentVoucher: 'Finance.AP.PaymentVoucher',
  financeCashBankPaymentSlip: 'Finance.CashBank.PaymentSlip',
  financeArCustomerReceipt: 'Finance.AR.CustomerReceipt',
  financeApSupplierStatement: 'Finance.AP.SupplierStatement',
  financeApAgingReport: 'Finance.AP.AgingReport',
  financeApCashRequirements: 'Finance.AP.CashRequirements',
  financeApMatchExceptionReport: 'Finance.AP.MatchExceptionReport',
  financeApProcurementReconciliation: 'Finance.AP.ProcurementReconciliation',
  financeArAgingReport: 'Finance.AR.AgingReport',
  financeArCustomerStatement: 'Finance.AR.CustomerStatement',
  financeCashPositionReport: 'Finance.Cash.PositionReport',
  financeTaxInputRegister: 'Finance.Tax.InputRegister',
  financeTaxOutputRegister: 'Finance.Tax.OutputRegister',
  financeTaxVatReconciliation: 'Finance.Tax.VatReconciliation',
  financeTaxWhtPayable: 'Finance.Tax.WhtPayable',
  financeTaxWhtCertificateRegister: 'Finance.Tax.WhtCertificateRegister',
  financeTaxWhtCertificate: 'Finance.Tax.WhtCertificate',
  financeTaxWhtRemittanceRegister: 'Finance.Tax.WhtRemittanceRegister',
  financeBudgetConsolidated: 'Finance.Budget.Consolidated',
  financeBudgetScenarioComparison: 'Finance.Budget.ScenarioComparison',
  financeTrialBalance: 'Finance.TrialBalance',
  financeBaseDeltaReport: 'Finance.BaseDeltaReport',
  financeIncomeStatement: 'Finance.IncomeStatement',
  financeBalanceSheet: 'Finance.BalanceSheet',
  financeCashFlowStatement: 'Finance.CashFlowStatement',
  financeMultiCurrencyDetail: 'Finance.MultiCurrencyDetail',
  financeDetailedLedger: 'Finance.DetailedLedger',
  financeClosePack: 'Finance.ClosePack',
} as const;

export interface RenderedDocumentFile {
  blob: Blob;
  fileName: string;
  contentType: string;
}

type DocumentOptions = { format?: string; copyType?: string };
type ControlledDocumentIssueOptions = {
  format?: string;
  copyType: ControlledDocumentCopyType;
  replacementReason?: string;
};
type DocumentParameters = Record<string, string | number | boolean | string[] | null | undefined>;

class DocumentOutputService {
  async getControlledDocumentIssues(
    documentType: string,
    entityId: string
  ): Promise<ControlledDocumentIssueSummary> {
    const url = this.buildApiUrl(
      `/documents/${encodeURIComponent(documentType)}/${encodeURIComponent(entityId)}/issues`
    );
    const token = getStoredToken();
    const response = await fetch(url.toString(), {
      headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    });
    if (!response.ok) {
      const message = await this.readErrorMessage(response);
      throw new Error(message || `Failed to load controlled document history (${response.status})`);
    }
    return response.json();
  }

  async downloadRetainedControlledDocument(issueId: string): Promise<void> {
    const url = this.buildApiUrl(`/documents/controlled-issues/${encodeURIComponent(issueId)}`);
    const token = getStoredToken();
    const response = await fetch(url.toString(), {
      headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    });
    if (!response.ok) {
      const message = await this.readErrorMessage(response);
      throw new Error(message || `Failed to download retained controlled document (${response.status})`);
    }
    const blob = await response.blob();
    const fileName = this.getFileName(response.headers.get('content-disposition'), `controlled-document-${issueId}.pdf`);
    this.downloadFile({ blob, fileName, contentType: response.headers.get('content-type') || blob.type || 'application/pdf' });
  }

  async fetchDocument(
    documentType: string,
    entityId: string,
    options: DocumentOptions = {}
  ): Promise<RenderedDocumentFile> {
    const format = options.format || 'pdf';
    const copyType = options.copyType || 'Original';
    const url = this.buildApiUrl(`/documents/${encodeURIComponent(documentType)}/${encodeURIComponent(entityId)}`);
    url.searchParams.set('format', format);
    url.searchParams.set('copyType', copyType);

    const token = getStoredToken();
    const response = await fetch(url.toString(), {
      method: 'GET',
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
    });

    if (!response.ok) {
      const message = await this.readErrorMessage(response);
      throw new Error(message || `Failed to render document (${response.status} ${response.statusText})`);
    }

    const blob = await response.blob();
    const contentType = response.headers.get('content-type') || blob.type || 'application/octet-stream';
    const fileName = this.getFileName(response.headers.get('content-disposition'), `${documentType}-${entityId}.${format}`);

    return { blob, fileName, contentType };
  }

  async fetchReportDocument(
    documentType: string,
    parameters: DocumentParameters = {},
    options: DocumentOptions = {}
  ): Promise<RenderedDocumentFile> {
    const format = options.format || 'pdf';
    const copyType = options.copyType || 'Original';
    const url = this.buildApiUrl(`/documents/${encodeURIComponent(documentType)}`);
    url.searchParams.set('format', format);
    url.searchParams.set('copyType', copyType);
    this.appendParameters(url, parameters);

    const token = getStoredToken();
    const response = await fetch(url.toString(), {
      method: 'GET',
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
    });

    if (!response.ok) {
      const message = await this.readErrorMessage(response);
      throw new Error(message || `Failed to render document (${response.status} ${response.statusText})`);
    }

    const blob = await response.blob();
    const contentType = response.headers.get('content-type') || blob.type || 'application/octet-stream';
    const fileName = this.getFileName(response.headers.get('content-disposition'), `${documentType}.${format}`);

    return { blob, fileName, contentType };
  }

  async downloadDocument(
    documentType: string,
    entityId: string,
    options: DocumentOptions = {}
  ): Promise<void> {
    const file = await this.fetchDocument(documentType, entityId, options);
    this.downloadFile(file);
  }

  async issueControlledDocument(
    documentType: string,
    entityId: string,
    options: ControlledDocumentIssueOptions
  ): Promise<void> {
    const url = this.buildApiUrl(
      `/documents/${encodeURIComponent(documentType)}/${encodeURIComponent(entityId)}/issue`
    );
    const token = getStoredToken();
    const response = await fetch(url.toString(), {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify({
        format: options.format || 'pdf',
        copyType: options.copyType,
        replacementReason: options.replacementReason?.trim() || undefined,
      }),
    });

    if (!response.ok) {
      const message = await this.readErrorMessage(response);
      throw new Error(message || `Failed to issue controlled document (${response.status} ${response.statusText})`);
    }

    const blob = await response.blob();
    const contentType = response.headers.get('content-type') || blob.type || 'application/pdf';
    const fileName = this.getFileName(
      response.headers.get('content-disposition'),
      `${documentType}-${entityId}.pdf`
    );
    this.downloadFile({ blob, fileName, contentType });
  }

  async downloadReportDocument(
    documentType: string,
    parameters: DocumentParameters = {},
    options: DocumentOptions = {}
  ): Promise<void> {
    const file = await this.fetchReportDocument(documentType, parameters, options);
    this.downloadFile(file);
  }

  async printDocument(
    documentType: string,
    entityId: string,
    options: DocumentOptions = {}
  ): Promise<void> {
    const file = await this.fetchDocument(documentType, entityId, { ...options, format: options.format || 'pdf' });
    await this.printFile(file);
  }

  async printReportDocument(
    documentType: string,
    parameters: DocumentParameters = {},
    options: DocumentOptions = {}
  ): Promise<void> {
    const file = await this.fetchReportDocument(documentType, parameters, { ...options, format: options.format || 'pdf' });
    await this.printFile(file);
  }

  /**
   * Prints a PDF already returned by a controlled Finance report endpoint. This lets legacy
   * report endpoints join the shared isolated-PDF print pipeline without printing the app page.
   */
  async printRenderedFile(file: RenderedDocumentFile): Promise<void> {
    await this.printFile(file);
  }

  private downloadFile(file: RenderedDocumentFile): void {
    const objectUrl = URL.createObjectURL(file.blob);

    try {
      const link = document.createElement('a');
      link.href = objectUrl;
      link.download = file.fileName;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    } finally {
      URL.revokeObjectURL(objectUrl);
    }
  }

  private async printFile(file: RenderedDocumentFile): Promise<void> {
    const objectUrl = URL.createObjectURL(file.blob);
    const iframe = document.createElement('iframe');
    iframe.style.position = 'fixed';
    iframe.style.right = '0';
    iframe.style.bottom = '0';
    iframe.style.width = '0';
    iframe.style.height = '0';
    iframe.style.border = '0';
    iframe.title = file.fileName;

    const cleanup = () => {
      setTimeout(() => {
        if (iframe.parentNode) {
          iframe.parentNode.removeChild(iframe);
        }
        URL.revokeObjectURL(objectUrl);
      }, 10_000);
    };

    await new Promise<void>((resolve, reject) => {
      iframe.onload = () => {
        try {
          iframe.contentWindow?.focus();
          iframe.contentWindow?.print();
          resolve();
        } catch (error) {
          const popup = window.open(objectUrl, '_blank', 'noopener,noreferrer');
          if (!popup) {
            reject(error);
            return;
          }
          resolve();
        } finally {
          cleanup();
        }
      };

      iframe.onerror = () => {
        cleanup();
        reject(new Error('Unable to open the generated document for printing.'));
      };

      iframe.src = objectUrl;
      document.body.appendChild(iframe);
    });
  }

  private appendParameters(url: URL, parameters: DocumentParameters): void {
    Object.entries(parameters).forEach(([key, value]) => {
      if (value === null || value === undefined || value === '') return;
      if (Array.isArray(value)) {
        if (value.length > 0) {
          url.searchParams.set(key, value.join(','));
        }
        return;
      }

      url.searchParams.set(key, String(value));
    });
  }

  private buildApiUrl(path: string): URL {
    const apiBase = API_BASE_URL.replace(/\/$/, '');
    const requestPath = `${apiBase}${path}`;
    if (/^https?:\/\//i.test(requestPath)) {
      return new URL(requestPath);
    }

    if (typeof window === 'undefined') {
      throw new Error('Document rendering requires a browser origin when NEXT_PUBLIC_API_URL is relative.');
    }

    // Keep deployed browser builds on the ERP origin instead of falling back to a developer localhost URL.
    return new URL(requestPath, window.location.origin);
  }

  private async readErrorMessage(response: Response): Promise<string | null> {
    const text = await response.text().catch(() => '');
    if (!text) return null;

    try {
      const data = JSON.parse(text);
      return data.message || data.title || data.error || text;
    } catch {
      return text;
    }
  }

  private getFileName(contentDisposition: string | null, fallback: string): string {
    if (!contentDisposition) {
      return this.safeFileName(fallback);
    }

    const encoded = contentDisposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1];
    if (encoded) {
      return this.safeFileName(decodeURIComponent(encoded));
    }

    const plain = contentDisposition.match(/filename="?([^";]+)"?/i)?.[1];
    return this.safeFileName(plain || fallback);
  }

  private safeFileName(fileName: string): string {
    return fileName.replace(/[\\/:*?"<>|]/g, '-');
  }
}

export const documentOutputService = new DocumentOutputService();

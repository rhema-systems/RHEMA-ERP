'use client';

import { type ReactNode, useLayoutEffect, useRef, useState } from 'react';
import html2canvas from 'html2canvas';
import jsPDF from 'jspdf';
import { Download, Printer } from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import type { PayrollPayslip, PayrollTransaction } from '@/services/payrollService';

interface PayrollPayslipPreviewDialogProps {
  open: boolean;
  payslip: PayrollPayslip | null;
  onOpenChange: (open: boolean) => void;
}

const formatAmount = (value: number | null | undefined) =>
  new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value ?? 0);

const formatMonth = (value?: string | null) => {
  if (!value) {
    return '';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '';
  }

  return new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric' }).format(date);
};

const currencyLabel = (currency?: string | null) => {
  const code = currency || 'GHS';
  return code.toUpperCase() === 'GHS' ? 'GH\u00a2' : code;
};

const sumLines = (lines: PayrollTransaction[]) => lines.reduce((total, line) => total + (line.amount || 0), 0);

const payslipPdfFileName = (payslip: PayrollPayslip) =>
  `${payslip.isSeparateBonusRun ? 'bonus-slip' : 'payslip'}-${payslip.employeeNumber || 'employee'}-${payslip.runNumber || 'run'}.pdf`.replace(/[^a-z0-9_.-]+/gi, '-');

const nextAnimationFrame = () => new Promise<void>((resolve) => window.requestAnimationFrame(() => resolve()));

async function capturePayslipForPdf(page: HTMLElement) {
  const exportHost = document.createElement('div');
  exportHost.setAttribute('aria-hidden', 'true');
  exportHost.style.position = 'fixed';
  exportHost.style.left = '0';
  exportHost.style.top = '0';
  exportHost.style.width = '210mm';
  exportHost.style.height = '297mm';
  exportHost.style.overflow = 'hidden';
  exportHost.style.pointerEvents = 'none';
  exportHost.style.zIndex = '-1';

  const clonedPage = page.cloneNode(true) as HTMLElement;
  clonedPage.classList.remove('mx-auto', 'shadow-sm');
  clonedPage.style.margin = '0';
  clonedPage.style.boxShadow = 'none';
  clonedPage.style.fontFamily = 'Arial, Helvetica, sans-serif';
  clonedPage.style.height = '297mm';
  clonedPage.style.width = '210mm';
  clonedPage.style.maxWidth = 'none';

  exportHost.appendChild(clonedPage);
  document.body.appendChild(exportHost);

  try {
    await nextAnimationFrame();
    await nextAnimationFrame();

    const { width, height } = clonedPage.getBoundingClientRect();
    const captureWidth = Math.ceil(width);
    const captureHeight = Math.ceil(height);

    return await html2canvas(clonedPage, {
      backgroundColor: '#ffffff',
      height: captureHeight,
      logging: false,
      scale: 2,
      scrollX: 0,
      scrollY: 0,
      useCORS: true,
      width: captureWidth,
      windowWidth: captureWidth,
      windowHeight: captureHeight,
      x: 0,
      y: 0,
    });
  } finally {
    exportHost.remove();
  }
}

function usePayslipPageFit(payslip: PayrollPayslip | null) {
  const frameRef = useRef<HTMLDivElement>(null);
  const contentRef = useRef<HTMLDivElement>(null);
  const [scale, setScale] = useState(1);
  const [frameHeight, setFrameHeight] = useState(0);

  useLayoutEffect(() => {
    const frame = frameRef.current;
    const content = contentRef.current;

    if (!frame || !content) {
      return;
    }

    let animationFrame = 0;

    const updateScale = () => {
      const frameWidth = frame.clientWidth;
      const frameHeight = frame.clientHeight;
      const contentWidth = content.scrollWidth;
      const contentHeight = content.scrollHeight;

      if (!frameWidth || !frameHeight || !contentWidth || !contentHeight) {
        setScale(1);
        setFrameHeight(0);
        return;
      }

      const nextScale = Math.min(1, frameWidth / contentWidth, frameHeight / contentHeight);
      setScale(Number.isFinite(nextScale) && nextScale > 0 ? Math.floor(nextScale * 1000) / 1000 : 1);
      setFrameHeight(frameHeight);
    };

    const scheduleUpdate = () => {
      window.cancelAnimationFrame(animationFrame);
      animationFrame = window.requestAnimationFrame(updateScale);
    };

    scheduleUpdate();

    const resizeObserver = new ResizeObserver(scheduleUpdate);
    resizeObserver.observe(frame);
    resizeObserver.observe(content);
    window.addEventListener('resize', scheduleUpdate);

    return () => {
      window.cancelAnimationFrame(animationFrame);
      resizeObserver.disconnect();
      window.removeEventListener('resize', scheduleUpdate);
    };
  }, [payslip]);

  return { frameRef, contentRef, scale, frameHeight };
}

function lineLabel(line: PayrollTransaction) {
  const type = line.transactionType;

  if (type === 'BasicSalary') {
    return 'BASIC SALARY';
  }

  if (type === 'IncomeTax') {
    if ((line.componentCode || '').toUpperCase() === 'BON' || (line.description || '').toUpperCase().includes('BONUS')) {
      return 'INCOME TAX (BONUS)';
    }

    return 'INCOME TAX - TOTAL';
  }

  if (type === 'INT') {
    return 'LOAN INTEREST';
  }

  if (type === 'EmployeePension') {
    return 'SOCIAL SECURITY FUND';
  }

  if (type === 'ADV') {
    return 'SALARY ADVANCE';
  }

  if (type === 'OVE') {
    return 'OVERTIME';
  }

  if (type === 'BAR') {
    return 'PROMOTION BASIC ARREARS';
  }

  if (type === 'ALR') {
    return 'PROMOTION ALLOWANCE ARREARS';
  }

  if (type === 'ESR' || type === 'CSR' || type === 'COR') {
    return 'PROMOTION CONTRIBUTION ARREARS';
  }

  return (line.description || line.componentCode || type).toUpperCase();
}

function lineOrder(line: PayrollTransaction) {
  switch (line.transactionType) {
    case 'BasicSalary':
    case 'IncomeTax':
      return 0;
    case 'Allowance':
    case 'Benefit':
    case 'Deduction':
      return 10;
    case 'EmployeeContribution':
    case 'EmployeePension':
      return 20;
    case 'LoanRepayment':
    case 'REP':
    case 'INT':
    case 'ADV':
      return 30;
    case 'OVE':
    case 'BAR':
    case 'ALR':
    case 'ESR':
    case 'CSR':
    case 'COR':
      return 40;
    default:
      return 50;
  }
}

function DetailRow({ label, value }: { label: string; value?: string | number | null }) {
  return (
    <div className="grid grid-cols-[86px_5px_minmax(0,1fr)]">
      <span className="whitespace-nowrap text-right font-bold">{label}</span>
      <span className="text-center font-bold">:</span>
      <span className="min-w-0 whitespace-nowrap font-medium">{value || ''}</span>
    </div>
  );
}

function SpacedUnderline({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <span
      className={`inline-flex flex-col whitespace-nowrap align-bottom ${className}`}
      style={{ lineHeight: 1.08 }}
    >
      <span className="block">{children}</span>
      <span
        aria-hidden="true"
        style={{
          borderTop: '1px solid #000',
          display: 'block',
          height: 0,
          marginTop: '2px',
          width: '100%',
        }}
      />
    </span>
  );
}

function PayLinesTable({ title, lines }: { title: string; lines: PayrollTransaction[] }) {
  const visibleLines = [...lines]
    .filter((line) => line.amount !== 0)
    .sort((a, b) => lineOrder(a) - lineOrder(b) || lineLabel(a).localeCompare(lineLabel(b)));

  return (
    <table className="w-full table-fixed border-collapse text-[10.5px] leading-tight">
      <thead>
        <tr>
          <th className="w-[32px] pb-1.5 text-left text-[10.5px] font-bold">
            <SpacedUnderline className="italic">Item</SpacedUnderline>
          </th>
          <th className="pb-1.5 text-center text-[15px] font-normal">
            <SpacedUnderline>{title}</SpacedUnderline>
          </th>
          <th className="w-[64px] pb-1.5 text-right text-[10.5px] font-bold">
            <SpacedUnderline>Amount</SpacedUnderline>
          </th>
        </tr>
      </thead>
      <tbody>
        {visibleLines.map((line) => (
          <tr key={line.id}>
            <td colSpan={2} className="whitespace-nowrap py-[3px] pr-2 align-top font-medium">
              {lineLabel(line)}
            </td>
            <td className="whitespace-nowrap py-[3px] text-right align-top font-medium">{formatAmount(line.amount)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function SalarySummary({ payslip, totalEarnings, totalDeductions }: { payslip: PayrollPayslip; totalEarnings: number; totalDeductions: number }) {
  return (
    <div className="mx-auto w-[84mm] border border-black px-3 py-1.5 text-[10.5px]">
      <div className="mb-1.5 text-center text-[12.5px] font-bold">
        <SpacedUnderline>SALARY SUMMARY ({currencyLabel(payslip.currencyCode)})</SpacedUnderline>
      </div>
      <div className="grid grid-cols-[1fr_88px] gap-y-1">
        <span>Total Earnings :</span>
        <span className="text-right">{formatAmount(totalEarnings)}</span>
        <span>Total Deductions :</span>
        <span className="text-right">{formatAmount(totalDeductions)}</span>
        <span className="font-bold">Net Salary :</span>
        <span className="text-right font-bold">{formatAmount(payslip.netIncome)}</span>
      </div>
    </div>
  );
}

function TaxAnalysis({ payslip }: { payslip: PayrollPayslip }) {
  const bonusIncomeTax = payslip.bonusIncomeTax ?? 0;
  const normalIncomeTax = payslip.normalIncomeTax ?? Math.max(0, payslip.incomeTax - bonusIncomeTax);

  return (
    <section className="mt-1.5 text-[10.5px]">
      <div className="text-center text-[14px] font-normal">
        <SpacedUnderline>TAX ANALYSIS</SpacedUnderline>
      </div>
      <div className="mt-1 grid grid-cols-2 gap-x-[4mm]">
        <div className="grid grid-cols-[max-content_4px_72px]">
          <span className="whitespace-nowrap">Taxable Earning</span>
          <span>:</span>
          <span className="whitespace-nowrap text-right tabular-nums">{formatAmount(payslip.taxableIncome)}</span>
          <span className="whitespace-nowrap">Tax Relief</span>
          <span>:</span>
          <span className="whitespace-nowrap text-right tabular-nums">{formatAmount(payslip.taxRelief)}</span>
        </div>
        <div className="grid grid-cols-[max-content_4px_72px]">
          <span className="whitespace-nowrap">Income Tax (Normal)</span>
          <span>:</span>
          <span className="whitespace-nowrap text-right tabular-nums">{formatAmount(normalIncomeTax)}</span>
          <span className="whitespace-nowrap">Income Tax (Bonus)</span>
          <span>:</span>
          <span className="whitespace-nowrap text-right tabular-nums">{formatAmount(bonusIncomeTax)}</span>
          <span className="whitespace-nowrap font-bold">Income Tax (Total)</span>
          <span>:</span>
          <span className="whitespace-nowrap text-right font-bold tabular-nums">{formatAmount(payslip.incomeTax)}</span>
        </div>
      </div>
    </section>
  );
}

function ContributionStatement({ payslip }: { payslip: PayrollPayslip }) {
  const providentRows = payslip.contributions.filter((line) => line.isProvidentFund);
  const statutoryRows = payslip.contributions.filter((line) => !line.isProvidentFund);
  const statutoryDisplayRows = statutoryRows.length > 0
    ? statutoryRows
    : [{
        item: 'SOCIAL SECURITY FUND',
        employeeContribution: 0,
        employerContribution: 0,
        totalContribution: 0,
        openingBalance: 0,
        totalWithdrawal: 0,
        grandTotal: 0,
        firstTier: 0,
        secondTier: 0,
        isProvidentFund: false,
      }];

  return (
    <section className="mt-3 text-[9.5px] leading-tight">
      <div className="text-center text-[13px] font-normal">
        <SpacedUnderline>CONTRIBUTION STATEMENT</SpacedUnderline>
      </div>
      <table className="mt-1 w-full table-fixed border-collapse">
        <thead>
          <tr className="align-bottom">
            <th className="w-[30mm] text-left italic">
              <SpacedUnderline>Item</SpacedUnderline>
            </th>
            <th className="text-right">
              <SpacedUnderline>Employee&apos;s Contr.</SpacedUnderline>
            </th>
            <th className="text-right">
              <SpacedUnderline>Employer&apos;s Contr.</SpacedUnderline>
            </th>
            <th className="text-right">
              <SpacedUnderline>Total Contri.</SpacedUnderline>
            </th>
            <th className="text-right">
              <SpacedUnderline>Opening Bal.</SpacedUnderline>
            </th>
            <th className="text-right">
              <SpacedUnderline>Total Withdrawal</SpacedUnderline>
            </th>
            <th className="text-right">
              <SpacedUnderline>Grand Total</SpacedUnderline>
            </th>
          </tr>
        </thead>
        <tbody>
          {(providentRows.length > 0 ? providentRows : []).map((line) => (
            <tr key={line.item}>
              <td className="py-[3px]">{line.item}</td>
              <td className="py-[3px] text-right">{formatAmount(line.employeeContribution)}</td>
              <td className="py-[3px] text-right">{formatAmount(line.employerContribution)}</td>
              <td className="py-[3px] text-right">{formatAmount(line.totalContribution)}</td>
              <td className="py-[3px] text-right">{formatAmount(line.openingBalance)}</td>
              <td className="py-[3px] text-right">{line.totalWithdrawal ? formatAmount(line.totalWithdrawal) : ''}</td>
              <td className="py-[3px] text-right">{formatAmount(line.grandTotal)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <table className="mt-1.5 w-full table-fixed border-collapse">
        <thead>
          <tr>
            <th className="w-[42mm] text-left italic">
              <SpacedUnderline>Item</SpacedUnderline>
            </th>
            <th className="text-right">
              <SpacedUnderline>Employee&apos;s Contr.</SpacedUnderline>
            </th>
            <th className="text-right">
              <SpacedUnderline>Employer&apos;s Contr.</SpacedUnderline>
            </th>
            <th className="text-right">
              <SpacedUnderline>Total Contribution</SpacedUnderline>
            </th>
            <th className="text-right">
              <SpacedUnderline>1st Tier</SpacedUnderline>
            </th>
            <th className="text-right">
              <SpacedUnderline>2nd Tier</SpacedUnderline>
            </th>
          </tr>
        </thead>
        <tbody>
          {statutoryDisplayRows.map((line) => (
            <tr key={line.item}>
              <td className="py-[3px]">{line.item}</td>
              <td className="py-[3px] text-right">{formatAmount(line.employeeContribution)}</td>
              <td className="py-[3px] text-right">{formatAmount(line.employerContribution)}</td>
              <td className="py-[3px] text-right">{formatAmount(line.totalContribution)}</td>
              <td className="py-[3px] text-right">{formatAmount(line.firstTier)}</td>
              <td className="py-[3px] text-right">{formatAmount(line.secondTier)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}

function BankDetails({ payslip }: { payslip: PayrollPayslip }) {
  const rows = payslip.bankDetails.length > 0
    ? payslip.bankDetails
    : [{ bankName: '', accountNumber: '', currencyCode: payslip.currencyCode, exchangeRate: null, amount: payslip.netIncome }];

  return (
    <section className="mt-2 border-t border-black pt-1.5 text-[10px] leading-tight">
      <div className="text-center text-[13px] font-normal">
        <SpacedUnderline className="px-1">BANK DETAILS</SpacedUnderline>
      </div>
      <div className="mt-2 space-y-[4px]">
        <div className="grid grid-cols-[74mm_34mm_23mm_24mm_minmax(22mm,1fr)] items-end gap-x-[2mm] font-bold">
          <SpacedUnderline className="justify-self-start">Bank</SpacedUnderline>
          <SpacedUnderline className="justify-self-start">Acct No</SpacedUnderline>
          <SpacedUnderline className="justify-self-start">Currency</SpacedUnderline>
          <SpacedUnderline className="justify-self-start">Exch. Rate</SpacedUnderline>
          <SpacedUnderline className="justify-self-end">Amount</SpacedUnderline>
        </div>
        {rows.map((row, index) => (
          <div key={`${row.bankName}-${row.accountNumber}-${index}`} className="grid grid-cols-[74mm_34mm_23mm_24mm_minmax(22mm,1fr)] gap-x-[2mm]">
            <span className="whitespace-nowrap">{row.bankName}</span>
            <span className="whitespace-nowrap">{row.accountNumber}</span>
            <span className="whitespace-nowrap">{currencyLabel(row.currencyCode)}</span>
            <span className="whitespace-nowrap">{row.exchangeRate ? formatAmount(row.exchangeRate) : ''}</span>
            <span className="whitespace-nowrap text-right">{formatAmount(row.amount)}</span>
          </div>
        ))}
      </div>
    </section>
  );
}

function BonusSlip({ payslip }: { payslip: PayrollPayslip }) {
  const bonusLines = payslip.earnings.filter((line) => line.transactionType === 'BON' && line.amount !== 0);
  const bonusAmount = bonusLines.reduce((total, line) => total + line.amount, 0);
  const bank = payslip.bankDetails[0];
  const description = bonusLines.map((line) => lineLabel(line)).join(', ') || payslip.separateBonusCode || 'Bonus';

  return (
    <div className="flex min-h-full flex-col">
      <header className="text-center">
        {payslip.companyName && <div className="text-[16px] font-bold">{payslip.companyName}</div>}
        {payslip.companyAddress && <div>{payslip.companyAddress}</div>}
        {payslip.companyPhone && <div>{payslip.companyPhone}</div>}
        <div className="mt-8 text-[17px] font-bold">
          <SpacedUnderline>Bonus Slip</SpacedUnderline>
        </div>
      </header>

      <section className="mx-auto mt-10 w-[150mm] space-y-2 text-[12px]">
        <DetailRow label="Staff No" value={payslip.employeeNumber} />
        <DetailRow label="Staff Name" value={payslip.employeeName} />
        <DetailRow label="Bank" value={bank?.bankName} />
        <DetailRow label="Acct No" value={bank?.accountNumber} />
      </section>

      <section className="mx-auto mt-8 w-[150mm] text-[12px]">
        <div className="grid grid-cols-[1fr_38mm] border-b border-black pb-1 font-bold">
          <SpacedUnderline>Description</SpacedUnderline>
          <SpacedUnderline className="justify-self-end">Amount</SpacedUnderline>
        </div>
        <div className="grid grid-cols-[1fr_38mm] py-2">
          <span>{description}</span>
          <span className="text-right">{formatAmount(bonusAmount)}</span>
        </div>
        <div className="grid grid-cols-[1fr_38mm] py-2">
          <span>Income Tax</span>
          <span className="text-right">{formatAmount(payslip.incomeTax)}</span>
        </div>
        <div className="grid grid-cols-[1fr_38mm] border-t border-black py-2 font-bold">
          <span>Net Bonus</span>
          <span className="text-right">{formatAmount(payslip.netIncome)}</span>
        </div>
      </section>

      <footer className="mt-auto h-[2mm]" />
    </div>
  );
}

export function PayrollPayslipPreviewDialog({ open, payslip, onOpenChange }: PayrollPayslipPreviewDialogProps) {
  const pageRef = useRef<HTMLElement>(null);
  const [downloading, setDownloading] = useState(false);

  const handlePrint = () => {
    const cleanup = () => {
      document.body.classList.remove('printing-payroll-payslip');
      window.removeEventListener('afterprint', cleanup);
    };

    document.body.classList.add('printing-payroll-payslip');
    window.addEventListener('afterprint', cleanup);
    window.print();
    window.setTimeout(cleanup, 500);
  };

  const handleDownload = async () => {
    if (!payslip || !pageRef.current || downloading) {
      return;
    }

    setDownloading(true);
    try {
      const page = pageRef.current;
      if ('fonts' in document) {
        await document.fonts.ready;
      }
      await nextAnimationFrame();

      const canvas = await capturePayslipForPdf(page);

      const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
      doc.addImage(canvas.toDataURL('image/png'), 'PNG', 0, 0, 210, 297, undefined, 'FAST');
      doc.save(payslipPdfFileName(payslip));
    } finally {
      setDownloading(false);
    }
  };

  const totalEarnings = payslip ? sumLines(payslip.earnings) : 0;
  const totalDeductions = payslip ? sumLines(payslip.deductions) : 0;
  const { frameRef, contentRef, scale, frameHeight } = usePayslipPageFit(payslip);
  const fittedContentStyle = {
    transform: `scale(${scale})`,
    transformOrigin: 'top left',
    minHeight: scale < 1 && frameHeight > 0 ? `${frameHeight / scale}px` : '100%',
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="h-screen max-h-screen w-screen max-w-none overflow-hidden rounded-none border-0 bg-zinc-200 p-0 shadow-none sm:rounded-none">
        <DialogHeader className="payroll-payslip-no-print border-b bg-white px-6 py-3">
          <div className="flex items-center justify-between gap-3 pr-8">
            <div>
              <DialogTitle>{payslip?.isSeparateBonusRun ? 'Bonus Slip Preview' : 'Payslip Preview'}</DialogTitle>
              <DialogDescription>{payslip ? `${payslip.employeeNumber} - ${payslip.employeeName}` : 'Select a payslip to preview.'}</DialogDescription>
            </div>
            {payslip && (
              <div className="flex items-center gap-2">
                <Button type="button" variant="outline" disabled={downloading} onClick={() => void handleDownload()} className="hidden">
                  <Download className="mr-2 h-4 w-4" />
                  {downloading ? 'Preparing PDF' : 'Download'}
                </Button>
                <Button type="button" onClick={handlePrint}>
                  <Printer className="mr-2 h-4 w-4" />
                  Print
                </Button>
              </div>
            )}
          </div>
        </DialogHeader>

        {payslip && (
          <div className="h-[calc(100vh-73px)] overflow-auto px-8 py-6">
            <article ref={pageRef} className="payroll-payslip-print-root mx-auto h-[297mm] w-[210mm] overflow-hidden border border-black bg-white px-[1mm] pb-[3mm] pt-[14mm] text-[10.5px] leading-tight text-black shadow-sm">
              <div ref={frameRef} className="h-full overflow-hidden">
                <div ref={contentRef} className="flex min-h-full flex-col" style={fittedContentStyle}>
                  {payslip.htmlContent ? (
                    <div className="contents" dangerouslySetInnerHTML={{ __html: payslip.htmlContent }} />
                  ) : payslip.isSeparateBonusRun ? (
                    <BonusSlip payslip={payslip} />
                  ) : (
                  <>
                  <div className="flex-1">
                  <header className="text-center">
                    {payslip.companyName && <div className="text-[16px] font-bold">{payslip.companyName}</div>}
                    {payslip.companyAddress && <div>{payslip.companyAddress}</div>}
                    {payslip.companyPhone && <div>{payslip.companyPhone}</div>}
                    <div className="mt-2.5 text-[16px] font-bold">
                      <SpacedUnderline>PAYSLIP</SpacedUnderline>
                    </div>
                  </header>

                  <section className="mt-3 grid grid-cols-2 gap-x-[8mm]">
                    <div className="space-y-[4px]">
                      <DetailRow label="Employee ID" value={payslip.employeeNumber} />
                      <DetailRow label="Name" value={payslip.employeeName} />
                      <DetailRow label="Department" value={payslip.departmentName} />
                      <DetailRow label="Section" value={payslip.sectionName} />
                      <DetailRow label="Positions" value={payslip.positionTitle} />
                      <div className="pt-[5px]">
                        <DetailRow label="Staff Category" value={payslip.staffCategory} />
                      </div>
                    </div>
                    <div className="space-y-[4px]">
                      <DetailRow label="Month" value={formatMonth(payslip.payPeriodTo)} />
                      <DetailRow label="Currency Used" value={currencyLabel(payslip.currencyCode)} />
                      <DetailRow label="Job Location" value={payslip.jobLocation} />
                      <DetailRow label="SSF Number" value={payslip.ssfNumber} />
                      <DetailRow label="Staff TIN" value={payslip.staffTin} />
                    </div>
                  </section>

                  <section className="mt-2 border-t border-black pt-1.5">
                    <div className="grid grid-cols-2 gap-x-[6mm]">
                      <PayLinesTable title="EARNINGS" lines={payslip.earnings} />
                      <PayLinesTable title="DEDUCTIONS" lines={payslip.deductions} />
                    </div>
                  </section>

                  <section className="mt-3.5">
                    <SalarySummary payslip={payslip} totalEarnings={totalEarnings} totalDeductions={totalDeductions} />
                  </section>

                  <TaxAnalysis payslip={payslip} />
                  <ContributionStatement payslip={payslip} />
                  <BankDetails payslip={payslip} />
                  </div>
                  <footer className="mt-auto h-[2mm]" />
                  </>
                  )}
                </div>
              </div>
            </article>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

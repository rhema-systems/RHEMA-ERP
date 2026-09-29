import type { ProcedureCaseSummary } from '@/services/procedure-case.service';

export function facilitiesCaseReportRows(cases: ProcedureCaseSummary[]) {
  return cases.map((item) => ({
    Reference: item.referenceNumber || item.id,
    Title: item.title,
    Type: item.entityType,
    Applicant: item.applicantName || '',
    Status: item.status,
    Stage: item.currentStageName,
    'Assigned role': item.currentAssignedRole || '',
    Created: item.createdAt.slice(0, 10),
    Updated: item.updatedAt?.slice(0, 10) || '',
  }));
}

export async function downloadFacilitiesCaseReport(
  cases: ProcedureCaseSummary[],
  format: 'xlsx' | 'pdf' | 'csv'
) {
  const rows = facilitiesCaseReportRows(cases);
  const stamp = new Date().toISOString().slice(0, 10);
  if (format === 'xlsx' || format === 'csv') {
    const XLSX = await import('xlsx');
    const sheet = XLSX.utils.json_to_sheet(rows);
    sheet['!cols'] = [
      { wch: 22 }, { wch: 36 }, { wch: 28 }, { wch: 24 },
      { wch: 16 }, { wch: 28 }, { wch: 22 }, { wch: 14 }, { wch: 14 },
    ];
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, sheet, 'Facilities cases');
    XLSX.writeFile(workbook, `facilities-cases-${stamp}.${format}`, { bookType: format });
    return;
  }

  const { jsPDF } = await import('jspdf');
  const pdf = new jsPDF();
  pdf.setFontSize(16);
  pdf.text('Facilities case register', 14, 18);
  pdf.setFontSize(9);
  pdf.text(`Generated ${stamp}  |  ${rows.length} cases`, 14, 26);
  let y = 36;
  rows.forEach((row) => {
    const lines = pdf.splitTextToSize(
      `${row.Reference}  |  ${row.Status}  |  ${row.Stage}\n${row.Title}  |  ${row.Applicant || 'No applicant'}`,
      180
    ) as string[];
    const height = lines.length * 5 + 5;
    if (y + height > 280) {
      pdf.addPage();
      y = 18;
    }
    pdf.text(lines, 14, y);
    y += height;
  });
  pdf.save(`facilities-cases-${stamp}.pdf`);
}

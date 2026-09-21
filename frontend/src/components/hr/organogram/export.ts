import type { ExportRow } from './export-data';
import { toCsv } from './export-data';
import type { ExportContext } from './StaticOrgChart';

/**
 * File-producing side of export. Everything here takes a rendered element or a row set and hands
 * the browser a download; what to render is decided by `OrgExportDialog` and `OrgChart`.
 *
 * The image path follows the payslip and procurement precedents in this repo: html2canvas first,
 * and on the "unsupported color function oklch" failure the browser-backed foreignObject renderer.
 * The static chart uses hex throughout so the first attempt normally succeeds; the fallback covers
 * inherited page styles.
 */

export type ImageFormat = 'png' | 'pdf' | 'print';
export type TableFormat = 'xlsx' | 'csv';
export type ExportFormat = ImageFormat | TableFormat;
export type PdfPage = 'a4' | 'a3' | 'actual';

const isUnsupportedColorError = (error: unknown) =>
  error instanceof Error &&
  /unsupported color function\s+["']?(?:oklch|oklab|lab|lch|color)["']?/i.test(error.message);

export async function elementToCanvas(element: HTMLElement, scale = 2): Promise<HTMLCanvasElement> {
  const { default: html2canvas } = await import('html2canvas');
  const options = {
    backgroundColor: '#ffffff',
    scale,
    useCORS: true,
    logging: false,
    windowWidth: element.scrollWidth,
    windowHeight: element.scrollHeight,
  };
  try {
    return await html2canvas(element, options);
  } catch (error) {
    if (!isUnsupportedColorError(error)) throw error;
    return html2canvas(element, { ...options, foreignObjectRendering: true });
  }
}

export function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export async function exportPng(element: HTMLElement, stem: string) {
  // Scale down for very large charts so the canvas stays under browser limits (~16k px a side).
  const longest = Math.max(element.scrollWidth, element.scrollHeight);
  const scale = longest * 2 > 15000 ? Math.max(1, 15000 / longest) : 2;
  const canvas = await elementToCanvas(element, scale);
  const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/png'));
  if (!blob) throw new Error('The browser could not encode the image.');
  downloadBlob(blob, `${stem}.png`);
}

const PAGE_MM: Record<Exclude<PdfPage, 'actual'>, [number, number]> = {
  a4: [297, 210],
  a3: [420, 297],
};
const PX_TO_MM = 25.4 / 96;

export async function exportPdf(element: HTMLElement, stem: string, page: PdfPage, context: ExportContext) {
  const { jsPDF } = await import('jspdf');
  const longest = Math.max(element.scrollWidth, element.scrollHeight);
  const scale = longest * 2 > 15000 ? Math.max(1, 15000 / longest) : 2;
  const canvas = await elementToCanvas(element, scale);
  const imgW = canvas.width / scale;
  const imgH = canvas.height / scale;

  let doc: InstanceType<typeof jsPDF>;
  let drawW: number;
  let drawH: number;
  let x: number;
  let y: number;

  if (page === 'actual') {
    // One page the size of the chart, at 96 dpi. A plotter or a print shop takes this as-is.
    drawW = imgW * PX_TO_MM;
    drawH = imgH * PX_TO_MM;
    doc = new jsPDF({ orientation: drawW >= drawH ? 'landscape' : 'portrait', unit: 'mm', format: [drawW, drawH] });
    x = 0;
    y = 0;
  } else {
    const [pw, ph] = PAGE_MM[page];
    const margin = 10;
    doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: page });
    const fit = Math.min((pw - margin * 2) / (imgW * PX_TO_MM), (ph - margin * 2) / (imgH * PX_TO_MM));
    drawW = imgW * PX_TO_MM * fit;
    drawH = imgH * PX_TO_MM * fit;
    x = (pw - drawW) / 2;
    y = (ph - drawH) / 2;
  }

  doc.setProperties({
    title: `${context.dimensionLabel} organogram`,
    subject: context.scopeLabel,
    author: context.tenantName ?? 'RHEMA ERP',
    creator: 'RHEMA ERP · HR',
  });
  doc.addImage(canvas.toDataURL('image/jpeg', 0.92), 'JPEG', x, y, drawW, drawH, undefined, 'FAST');
  doc.save(`${stem}.pdf`);
}

/**
 * Opens the static chart in a print window with the document's stylesheets, so the browser's own
 * print dialog handles paper size and scaling. Same mechanism as procurement's document output.
 */
export function printElement(element: HTMLElement, title: string) {
  const printWindow = window.open('', '_blank', 'width=1200,height=850');
  if (!printWindow) {
    throw new Error('The browser blocked the print window. Allow pop-ups for this site and try again.');
  }
  printWindow.opener = null;
  const styles = Array.from(document.querySelectorAll<HTMLLinkElement | HTMLStyleElement>('link[rel="stylesheet"], style'))
    .map((node) => node.outerHTML)
    .join('\n');
  const w = element.scrollWidth;
  const h = element.scrollHeight;
  const landscape = w >= h;
  printWindow.document.open();
  printWindow.document.write(`<!doctype html><html><head><meta charset="utf-8"><title>${escapeHtml(title)}</title>${styles}
    <style>
      html,body{margin:0;background:#fff}
      body{padding:0}
      @page{size:${landscape ? 'A3 landscape' : 'A3 portrait'};margin:10mm}
      .org-print-wrap{transform-origin:top left;width:${w}px}
      @media print{
        .org-print-wrap{zoom:var(--print-zoom,1)}
      }
    </style></head>
    <body><div class="org-print-wrap">${element.outerHTML}</div>
    <script>
      (function(){
        // Scale the chart to the printable width so one very wide chart lands on one sheet.
        var pageW = ${landscape ? 400 : 277} * 96 / 25.4; // A3 minus margins, in px
        var z = Math.min(1, pageW / ${w});
        document.documentElement.style.setProperty('--print-zoom', z.toFixed(3));
      })();
    </script>
    </body></html>`);
  printWindow.document.close();
  printWindow.addEventListener(
    'load',
    () => {
      printWindow.focus();
      printWindow.print();
    },
    { once: true },
  );
}

export async function exportTable(
  rows: ExportRow[],
  columns: string[],
  format: TableFormat,
  stem: string,
  context: ExportContext,
) {
  if (format === 'csv') {
    downloadBlob(new Blob([toCsv(rows, columns)], { type: 'text/csv;charset=utf-8' }), `${stem}.csv`);
    return;
  }
  const XLSX = await import('xlsx');
  const wb = XLSX.utils.book_new();
  const data = rows.map((r) => {
    const o: Record<string, string | number> = {};
    for (const c of columns) o[c] = r[c] ?? '';
    return o;
  });
  const ws = XLSX.utils.json_to_sheet(data, { header: columns });
  ws['!cols'] = columns.map((c) => ({
    wch: Math.min(60, Math.max(c.length, ...data.map((r) => String(r[c] ?? '').length)) + 2),
  }));
  ws['!autofilter'] = { ref: XLSX.utils.encode_range({ s: { r: 0, c: 0 }, e: { r: rows.length, c: columns.length - 1 } }) };
  XLSX.utils.book_append_sheet(wb, ws, 'Organogram');

  const about = XLSX.utils.aoa_to_sheet([
    ['Organisation', context.tenantName ?? ''],
    ['Dimension', context.dimensionLabel],
    ['Structure', context.structureName ?? ''],
    ['Scope', context.scopeLabel],
    ['Filters', context.filters.join('; ')],
    ['Rows', rows.length],
    ['Generated', context.generatedAt.toISOString()],
    ['Source', 'RHEMA ERP · HR · Organogram'],
    ...(context.dimensionLabel.toLowerCase() === 'people'
      ? [['Note', 'Email addresses are not included in organogram exports.']]
      : []),
  ]);
  about['!cols'] = [{ wch: 14 }, { wch: 60 }];
  XLSX.utils.book_append_sheet(wb, about, 'About');
  XLSX.writeFile(wb, `${stem}.xlsx`);
}

function escapeHtml(s: string) {
  return s.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c] ?? c);
}

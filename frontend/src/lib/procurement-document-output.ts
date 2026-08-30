import html2canvas from 'html2canvas';
import { jsPDF } from 'jspdf';

const safeFileName = (value: string) =>
  value.replace(/[^a-z0-9._-]+/gi, '-').replace(/-+/g, '-');

export const printProcurementDocument = (element: HTMLElement, title: string) => {
  const printWindow = window.open('', '_blank', 'width=1100,height=800');
  if (!printWindow) {
    throw new Error('The browser blocked the print window. Allow pop-ups and try again.');
  }

  printWindow.opener = null;
  const styles = Array.from(
    document.querySelectorAll<HTMLLinkElement | HTMLStyleElement>('link[rel="stylesheet"], style')
  ).map((node) => node.outerHTML).join('\n');
  const clone = element.cloneNode(true) as HTMLElement;
  clone.querySelectorAll('[data-document-exclude="true"]').forEach((node) => node.remove());

  printWindow.document.open();
  printWindow.document.write(`<!doctype html><html><head><meta charset="utf-8"><title>${title}</title>${styles}
    <style>body{padding:24px;background:#fff}@page{size:auto;margin:12mm}</style></head>
    <body>${clone.outerHTML}</body></html>`);
  printWindow.document.close();
  printWindow.addEventListener('load', () => {
    printWindow.focus();
    printWindow.print();
  }, { once: true });
};

export const exportProcurementDocumentPdf = async (element: HTMLElement, fileName: string) => {
  const canvas = await html2canvas(element, {
    backgroundColor: '#ffffff',
    scale: 1.5,
    useCORS: true,
    ignoreElements: (node) => node instanceof HTMLElement && node.dataset.documentExclude === 'true',
  });
  const pdf = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
  const pageWidth = pdf.internal.pageSize.getWidth();
  const pageHeight = pdf.internal.pageSize.getHeight();
  const imageWidth = pageWidth;
  const imageHeight = (canvas.height * imageWidth) / canvas.width;
  const image = canvas.toDataURL('image/png');
  let remainingHeight = imageHeight;
  let y = 0;

  pdf.addImage(image, 'PNG', 0, y, imageWidth, imageHeight);
  remainingHeight -= pageHeight;
  while (remainingHeight > 0) {
    y = remainingHeight - imageHeight;
    pdf.addPage();
    pdf.addImage(image, 'PNG', 0, y, imageWidth, imageHeight);
    remainingHeight -= pageHeight;
  }
  pdf.save(safeFileName(fileName.endsWith('.pdf') ? fileName : `${fileName}.pdf`));
};

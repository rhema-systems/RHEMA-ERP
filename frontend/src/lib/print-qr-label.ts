export function printQrLabel(element: HTMLElement | null, documentTitle: string, copies = 1): boolean {
  if (!element) return false;

  const printWindow = window.open('', '_blank', 'width=520,height=720');
  if (!printWindow) return false;

  printWindow.document.open();
  printWindow.document.write(`<!doctype html>
<html>
  <head>
    <meta charset="utf-8" />
    <title>${escapeHtml(documentTitle)}</title>
    <style>
      @page { size: auto; margin: 12mm; }
      * { box-sizing: border-box; }
      body {
        margin: 0;
        color: #111827;
        background: #fff;
        font-family: Arial, Helvetica, sans-serif;
      }
      .label {
        width: 100%;
        max-width: 380px;
        margin: 0 auto;
        padding: 18px;
        text-align: center;
        border: 1px solid #d1d5db;
      }
      .label + .label { break-before: page; page-break-before: always; }
      svg { display: block; width: 100%; max-width: 260px; height: auto; margin: 0 auto; }
      .label-title { margin-top: 12px; font-size: 17px; font-weight: 700; }
      .label-description { margin-top: 5px; font-size: 12px; line-height: 1.4; color: #4b5563; }
      .label-kicker { margin-bottom: 10px; font-size: 10px; font-weight: 700; text-transform: uppercase; }
    </style>
  </head>
  <body>
    ${Array.from({ length: Math.max(1, Math.min(1000, Math.trunc(copies))) }, () => `<div class="label">${element.innerHTML}</div>`).join('')}
    <script>
      window.addEventListener('load', function () {
        window.focus();
        window.print();
        window.close();
      });
    </script>
  </body>
</html>`);
  printWindow.document.close();
  return true;
}

function escapeHtml(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

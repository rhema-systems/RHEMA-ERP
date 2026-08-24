'use client';

import { AttachmentsPanel, type AttachmentLike } from '@/components/hr/common/AttachmentsPanel';
import { assetRegisterService } from '@/services/hr/asset-register.service';

/**
 * A row as the shared panel wants it, plus the one fact it has no opinion about.
 *
 * ⚠ `isStored` is false on anything written by the JSON endpoints slice 12b replaced — those
 * recorded a file name and a path and **stored nothing**. The panel would happily offer a download
 * button for one; the callback below refuses in words instead of letting it 404.
 */
interface AssetFileRow extends AttachmentLike {
  isStored: boolean;
}

const NOT_STORED =
  'This row predates the upload gate: a file name was recorded but no file was ever stored, '
  + 'so there is nothing to open. Re-attach the document to fix it.';

/**
 * The documents and photographs filed against an asset — area 16 slice 12b.
 *
 * Both halves are the **shared** `AttachmentsPanel`, not a bespoke one: every HR area's attachment
 * surface has the same shape — list, attach, download, remove — and the panel already renders the
 * gate's refusal message verbatim, which is the part worth getting right. The four callbacks are
 * what differ per area, which is exactly what it asks for.
 *
 * ⚠ **Both uploads go through the controlled gate**: scanned and registered in the central DMS
 * before any row is written, and the row rolled back if that write then fails. Until this slice the
 * create endpoints took a `fileName` and a `filePath` as JSON — the caller named a path, the server
 * wrote the string down, and no file existed anywhere. The list rendered beautifully over nothing.
 *
 * ⚠ **`filePath` is not a URL and is never rendered as a link.** The files live outside the web
 * root and the download endpoints need the bearer token, which an `<a href>` cannot attach. That is
 * why `download` is a callback here rather than an href — `hrDocumentService` fetches the bytes as
 * a blob and revokes the object URL afterwards.
 *
 * ⚠ **A photograph is not a document.** They are two panels rather than one list with a type
 * column, because the acts differ: a document is downloaded and a photograph is looked at, and a
 * photograph of damage taken at a return is evidence in a money claim against a named employee.
 */
export function AssetFilesPanel({ assetId }: { assetId: string }) {
  return (
    <div className="space-y-4">
      <AttachmentsPanel<AssetFileRow>
        title="Documents"
        note="The invoice, the warranty certificate, the manual. Scanned before it is stored."
        queryKey={['hr', 'assets', 'files', assetId, 'attachments']}
        emptyDescription="Nothing has been filed against this asset yet."
        list={async () => (await assetRegisterService.getAttachments(assetId))
          .map((a) => ({ ...a, isStored: a.isStored }))}
        upload={(file, description) =>
          assetRegisterService.uploadAttachment(assetId, file, description ?? undefined)
            .then((a) => ({ ...a, isStored: a.isStored }))}
        download={async (a) => {
          if (!a.isStored) throw new Error(NOT_STORED);
          await assetRegisterService.downloadAttachment(a.id, a.fileName);
        }}
        remove={(id) => assetRegisterService.deleteAttachment(id)}
      />

      <AttachmentsPanel<AssetFileRow>
        title="Photographs"
        note="Its condition when issued or taken back. A photograph of damage is evidence in a charge, so caption it."
        queryKey={['hr', 'assets', 'files', assetId, 'images']}
        emptyDescription="Nothing recorded about this asset's condition."
        // ⚠ The image DTO calls it `caption`, the panel renders `description`. Mapped here rather
        // than renamed on the DTO: "description" is the wrong word for what a photograph carries.
        list={async () => (await assetRegisterService.getImages(assetId))
          .map((i) => ({ ...i, description: i.caption, isStored: i.isStored }))}
        upload={(file, caption) =>
          assetRegisterService.uploadImage(assetId, file, caption ?? undefined)
            .then((i) => ({ ...i, description: i.caption, isStored: i.isStored }))}
        // Opened inline in a new tab rather than saved: looking at a photograph is the whole act.
        download={async (i) => {
          if (!i.isStored) throw new Error(NOT_STORED);
          await assetRegisterService.openImage(i.id);
        }}
        remove={(id) => assetRegisterService.deleteImage(id)}
      />
    </div>
  );
}

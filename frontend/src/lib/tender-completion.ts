import { tenderService } from '@/services/tenderService';
import {
  buildUpdateTenderDto,
  type PersistedTenderFormData,
} from '@/lib/tender-form-payload';

export async function completeExistingTenderDraft(
  tenderId: string,
  formData: PersistedTenderFormData,
  onCompleted: (tenderId: string) => void
): Promise<void> {
  await tenderService.updateTender(tenderId, buildUpdateTenderDto(formData));
  onCompleted(tenderId);
}

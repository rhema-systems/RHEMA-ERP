'use client';

import { registerLicense } from '@syncfusion/ej2-base';

const SYNCFUSION_LICENSE_KEY =
  process.env.NEXT_PUBLIC_SYNCFUSION_LICENSE_KEY?.trim();

let registered = false;

export function registerSyncfusionLicense() {
  if (registered || !SYNCFUSION_LICENSE_KEY) return;
  registerLicense(SYNCFUSION_LICENSE_KEY);
  registered = true;
}

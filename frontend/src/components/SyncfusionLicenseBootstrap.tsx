'use client';

import React from 'react';

import { registerSyncfusionLicense } from '@/lib/syncfusion-license';

registerSyncfusionLicense();

export function SyncfusionLicenseBootstrap() {
  React.useEffect(() => {
    registerSyncfusionLicense();
  }, []);

  return null;
}

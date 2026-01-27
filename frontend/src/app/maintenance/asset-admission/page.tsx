import { Suspense } from 'react';
import AssetAdmissionManagement from '../../../components/maintenance/AssetAdmissionManagement';

export default function AssetAdmissionPage() {
  return (
    <Suspense fallback={<div className="flex items-center justify-center h-full">Loading...</div>}>
      <AssetAdmissionManagement />
    </Suspense>
  );
}
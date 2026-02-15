import { Suspense } from 'react';
import JobCardsPage from '../../../components/maintenance/JobCardManagement';

export default function Page() {
  return (
    <Suspense fallback={<div className="flex items-center justify-center h-screen">Loading...</div>}>
      <JobCardsPage />
    </Suspense>
  );
}

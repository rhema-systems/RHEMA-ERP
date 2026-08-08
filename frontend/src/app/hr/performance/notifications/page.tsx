'use client';

import { PageHeader } from '@/components/hr/common/PageHeader';
import { AppraisalNotificationsPanel } from '@/components/hr/performance/AppraisalNotificationsPanel';

/**
 * Your appraisal notification queue.
 *
 * Only two things write here so far — opening a cycle, and HR sending deadline reminders from
 * a cycle's progress tab. The rest of the appraisal pipeline (self-evaluation submitted, peer
 * evaluation assigned, calibration complete, appeal resolved) has notification types defined
 * but nothing raising them yet; they will start appearing here as those stages are built.
 */
export default function AppraisalNotificationsPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Appraisal Notifications"
        description="Cycle openings, deadline reminders and anything else the appraisal process needs you to act on."
        backHref="/hr/performance"
      />
      <AppraisalNotificationsPanel limit={50} />
    </div>
  );
}

"use client";

import React from "react";
import { MaintenanceDashboardContent } from "@/components/maintenance/MaintenanceDashboardContent";

/**
 * Embedded maintenance dashboard view to be reused inside the main
 * Maintenance Management page (e.g., as a tab on /maintenance).
 */
export function MaintenanceDashboardView() {
  return <MaintenanceDashboardContent />;
}

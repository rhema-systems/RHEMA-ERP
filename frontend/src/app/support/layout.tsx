import type { ReactNode } from 'react';
import Link from 'next/link';
import { Building2, LifeBuoy } from 'lucide-react';

export default function SupportLayout({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 via-blue-50 to-indigo-100">
      <header className="border-b bg-white/80 backdrop-blur supports-[backdrop-filter]:bg-white/60">
        <div className="mx-auto max-w-6xl px-4 py-4 flex items-center justify-between">
          <Link href="/support" className="flex items-center gap-2">
            <div className="h-10 w-10 rounded-xl bg-gradient-to-r from-blue-600 to-indigo-600 flex items-center justify-center">
              <LifeBuoy className="h-5 w-5 text-white" />
            </div>
            <div className="leading-tight">
              <div className="font-semibold text-slate-900">Support Portal</div>
              <div className="text-xs text-slate-500">Enquiry • Helpdesk • Complaints</div>
            </div>
          </Link>

          <div className="text-sm text-slate-600 flex items-center gap-2">
            <Building2 className="h-4 w-4" />
            <span>Powered by ERP System</span>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-4 py-6">{children}</main>
    </div>
  );
}


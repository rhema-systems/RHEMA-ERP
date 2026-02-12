import { NextRequest, NextResponse } from 'next/server';

const isSupportHostname = (host: string | null) => {
  if (!host) return false;
  const h = host.toLowerCase();
  const configured = process.env.SUPPORT_PORTAL_HOSTNAME?.toLowerCase();
  if (configured && h === configured) return true;
  return h.startsWith('support.');
};

const shouldSkipRewrite = (pathname: string) => {
  if (pathname.startsWith('/_next')) return true;
  if (pathname.startsWith('/api')) return true;
  if (pathname === '/favicon.ico') return true;
  if (pathname.startsWith('/uploads')) return true;
  if (pathname.startsWith('/external-portal')) return true;

  // Shared auth pages (keep at root so existing flows continue to work)
  if (pathname.startsWith('/login')) return true;
  if (pathname.startsWith('/forgot-password')) return true;
  if (pathname.startsWith('/reset-password')) return true;
  if (pathname.startsWith('/verify-otp')) return true;
  if (pathname.startsWith('/register')) return true;
  if (pathname.startsWith('/tenant-select')) return true;

  return false;
};

export function middleware(req: NextRequest) {
  const host = req.headers.get('host');
  const pathname = req.nextUrl.pathname;

  if (!isSupportHostname(host)) {
    return NextResponse.next();
  }

  // Clean URLs on support subdomain: if user lands on /external-portal/*, redirect to the pretty path.
  // This keeps the public portal visually separate from the internal ERP URL structure.
  if (pathname === '/external-portal' || pathname.startsWith('/external-portal/')) {
    const url = req.nextUrl.clone();
    url.pathname = pathname.replace(/^\/external-portal/, '') || '/';
    return NextResponse.redirect(url);
  }

  if (shouldSkipRewrite(pathname)) {
    return NextResponse.next();
  }

  const url = req.nextUrl.clone();
  // Support Portal is served within the existing External Portal UI.
  // - /support/* (legacy) => /external-portal/support/*
  // - /tickets/* (pretty support host URLs) => /external-portal/support/tickets/*
  if (pathname.startsWith('/support')) {
    url.pathname = `/external-portal${pathname}`;
  } else {
    url.pathname = `/external-portal/support${pathname}`;
  }
  return NextResponse.rewrite(url);
}

export const config = {
  matcher: ['/((?!_next/static|_next/image).*)'],
};

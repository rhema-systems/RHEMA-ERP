import type { Metadata } from "next";
import { Inter } from "next/font/google";
import "./globals.css";
import { ReactQueryProvider } from "../lib/react-query";
import { TenantProvider } from "../contexts/TenantContext";
import { Toaster } from "../components/ui/toaster";

const inter = Inter({ subsets: ["latin"] });

export const metadata: Metadata = {
  title: "ERP System - Enterprise Resource Planning",
  description: "Modern enterprise resource planning system",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className={inter.className} suppressHydrationWarning>
        <ReactQueryProvider>
          <TenantProvider>
            {children}
            <Toaster />
          </TenantProvider>
        </ReactQueryProvider>
      </body>
    </html>
  );
}

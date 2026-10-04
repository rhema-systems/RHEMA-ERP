import React, { type ReactNode, type SVGProps } from 'react';
import { BarChart3, Cloud, Settings, ShieldCheck, type LucideIcon } from 'lucide-react';

import type { LoginPageStyle } from '../../services/login-appearance';

interface LoginShellProps {
  children: ReactNode;
}

interface LoginBenefit {
  title: string;
  detail?: string;
  Icon: LucideIcon | ((props: SVGProps<SVGSVGElement>) => React.JSX.Element);
}

function AnalyticsBarsIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" {...props}>
      <rect x="3.25" y="13.25" width="4.25" height="7.5" rx="1.15" fill="currentColor" />
      <rect x="9.875" y="8.75" width="4.25" height="12" rx="1.15" fill="currentColor" />
      <rect x="16.5" y="3.25" width="4.25" height="17.5" rx="1.15" fill="currentColor" />
    </svg>
  );
}

function CollaborationIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" {...props}>
      <circle cx="12" cy="7" r="3.25" fill="currentColor" />
      <circle cx="5.75" cy="9.25" r="2.45" fill="currentColor" />
      <circle cx="18.25" cy="9.25" r="2.45" fill="currentColor" />
      <path d="M5.9 12.35c-3.05 0-4.9 1.55-4.9 3.75 0 1.18.7 1.9 1.88 1.9h3.77a6.35 6.35 0 0 1 2.22-4.78 6.52 6.52 0 0 0-2.97-.87Z" fill="currentColor" />
      <path d="M18.1 12.35c3.05 0 4.9 1.55 4.9 3.75 0 1.18-.7 1.9-1.88 1.9h-3.77a6.35 6.35 0 0 0-2.22-4.78 6.52 6.52 0 0 1 2.97-.87Z" fill="currentColor" />
      <path d="M12 11.35c-4.05 0-6.35 2.18-6.35 5.15v.65c0 1.32.8 2.1 2.12 2.1h8.46c1.32 0 2.12-.78 2.12-2.1v-.65c0-2.97-2.3-5.15-6.35-5.15Z" fill="currentColor" />
    </svg>
  );
}

function SustainableLeafIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" {...props}>
      <path d="M20.8 3.2C13.7 3.35 7.18 5.7 4.28 10.02c-2.3 3.43-.98 7.08 2.15 8.16 2.8.97 5.64-.38 7.18-2.62 2.47-3.58 2.58-7.18 7.19-12.36Z" fill="currentColor" />
      <path d="M4.1 20.8c2.05-5.48 5.36-8.68 11.48-11.72" stroke="white" strokeWidth="1.7" strokeLinecap="round" />
    </svg>
  );
}

const lightBenefits: LoginBenefit[] = [
  { title: 'Streamline Operations', detail: 'Automate and simplify your processes', Icon: Settings },
  { title: 'Make Better Decisions', detail: 'Real-time insights across your enterprise', Icon: AnalyticsBarsIcon },
  { title: 'Enable Collaboration', detail: 'People, data and teams connected', Icon: CollaborationIcon },
  { title: 'Drive Sustainable Growth', detail: 'Build a stronger tomorrow, together', Icon: SustainableLeafIcon },
];

const darkBenefits: LoginBenefit[] = [
  { title: 'Integrated Modules', Icon: Settings },
  { title: 'Secure & Reliable', Icon: ShieldCheck },
  { title: 'Anywhere Access', Icon: Cloud },
  { title: 'Real-time Insights', Icon: BarChart3 },
];

function ProductMark({ dark = false }: { dark?: boolean }) {
  return (
    <div data-login-product className="inline-flex w-fit items-center gap-3 text-left drop-shadow-xl">
      <span data-login-logo-mark aria-hidden="true" data-dark={dark ? 'true' : 'false'}>
        <span /><span /><span /><span />
      </span>
      <span className="grid gap-0.5">
        <strong data-login-product-name>RHEMA-ERP</strong>
        <small data-login-product-tagline>
          People&nbsp; • &nbsp;Process&nbsp; • &nbsp;Progress
        </small>
      </span>
    </div>
  );
}

function EnterpriseMessage({ premium = false }: { premium?: boolean }) {
  const benefits = premium ? darkBenefits : lightBenefits;

  return (
    <section data-login-message aria-label="RHEMA-ERP overview">
      <p data-login-desktop-copy data-login-eyebrow>
        {premium ? 'Enterprise Resource Planning' : ''}
      </p>
      <h1 data-login-desktop-copy data-login-headline>
        {premium ? (
          <>
            Connecting <span className="block">People, Process</span><span className="block text-sky-400">and Progress</span>
          </>
        ) : (
          <>
            One Platform. <span className="block">A Smarter</span><span className="block">Organization.</span>
          </>
        )}
      </h1>
      <p data-login-desktop-copy data-login-supporting-copy>
        {premium
          ? 'A unified platform to manage your people, assets, finances and operations — built for a stronger tomorrow.'
          : 'Integrated solutions for a more efficient, transparent and sustainable future.'}
      </p>
      <ul data-login-benefits data-premium={premium ? 'true' : 'false'}>
        {benefits.map(({ title, detail, Icon }) => (
          <li key={title}>
            <span data-login-benefit-icon><Icon aria-hidden="true" /></span>
            <span data-login-benefit-copy>
              <strong>{title}</strong>
              {detail ? <small>{detail}</small> : null}
            </span>
          </li>
        ))}
      </ul>
      {premium ? (
        <p data-login-quote>
          <span aria-hidden="true">“</span>
          Building a smarter organization<br />for a better, more sustainable future.
        </p>
      ) : null}
      <div data-login-promise>
        <span data-login-promise-copy>
          <span>People</span>
          <span data-login-promise-separator aria-hidden="true">•</span>
          <span>Process</span>
          <span data-login-promise-separator aria-hidden="true">•</span>
          <span>Progress</span>
        </span>
        {!premium ? (
          <span data-login-promise-strokes aria-hidden="true">
            <span /><span /><span /><span />
          </span>
        ) : null}
      </div>
    </section>
  );
}

function ShellFrame({
  children,
  style,
}: LoginShellProps & { style: LoginPageStyle }) {
  const dark = style === 'DarkPremium';
  const backgroundImage = dark
    ? "url('/images/auth/login-dark-premium.webp')"
    : "url('/images/auth/login-light-corporate.webp')";

  return (
    <main
      className="relative isolate min-h-svh overflow-x-hidden overflow-y-auto text-slate-50"
      data-testid="login-shell"
      data-login-style={style}
    >
      <div
        data-login-background
        className="absolute inset-0 -z-20 bg-cover bg-no-repeat"
        style={{ backgroundImage }}
        aria-hidden="true"
      />
      <div data-login-overlay className="absolute inset-0 -z-10" aria-hidden="true" />

      <style>{`
        /* Light and Dark deliberately own separate visual tokens. Desktop
           values use the 1440 x 900 comparison viewport as the baseline. */
        [data-login-style="LightCorporate"] {
          --login-shell-max-width: 1344px;
          --login-left-column-width: 780px;
          --login-column-gap: 144px;
          --login-card-width: 384px;
          --login-card-radius: 24px;
          --login-card-padding: 28px;
          --login-card-field-height: 44px;
          --login-card-action-height: 44px;
          --login-card-top: 134px;
          --login-brand-top: 58px;
          --login-brand-left: 28px;
          --login-message-top: 180px;
          --login-message-left: 28px;
          --login-headline-size: 46px;
          --login-headline-line-height: 1.03;
          --login-headline-width: 395px;
          --login-copy-width: 440px;
          --login-feature-columns: 1fr;
          --login-feature-column-gap: 0px;
          --login-feature-row-gap: 7px;
          --login-background-position: 58% center;
        }

        [data-login-style="DarkPremium"] {
          --login-shell-max-width: 1344px;
          --login-left-column-width: 580px;
          --login-column-gap: 108px;
          --login-card-width: 440px;
          --login-card-radius: 26px;
          --login-card-padding: 36px;
          --login-card-field-height: 46px;
          --login-card-action-height: 46px;
          --login-card-top: 132px;
          --login-brand-top: 38px;
          --login-brand-left: 28px;
          --login-message-top: 196px;
          --login-message-left: 28px;
          --login-headline-size: 54px;
          --login-headline-line-height: 0.98;
          --login-headline-width: 510px;
          --login-copy-width: 490px;
          --login-feature-columns: repeat(4, minmax(0, 1fr));
          --login-feature-column-gap: 18px;
          --login-feature-row-gap: 0px;
          --login-background-position: center center;
        }

        [data-login-background] {
          background-position: var(--login-background-position);
          transform: scale(1.01);
        }

        [data-login-style="LightCorporate"] [data-login-background] {
          filter: saturate(1.08) contrast(1.04);
        }

        [data-login-style="LightCorporate"] [data-login-overlay] {
          background:
            radial-gradient(ellipse 54% 74% at 12% 37%, rgba(248, 252, 255, 0.66) 0%, rgba(248, 252, 255, 0.56) 42%, rgba(248, 252, 255, 0.2) 72%, rgba(248, 252, 255, 0) 100%),
            linear-gradient(90deg, rgba(248, 252, 255, 0.74) 0%, rgba(248, 252, 255, 0.62) 22%, rgba(248, 252, 255, 0.32) 35%, rgba(248, 252, 255, 0.1) 47%, rgba(248, 252, 255, 0.02) 58%, rgba(248, 252, 255, 0) 68%);
        }

        [data-login-style="DarkPremium"] [data-login-overlay] {
          background:
            radial-gradient(circle at 72% 48%, rgba(56, 189, 248, 0.1), transparent 35%),
            linear-gradient(90deg, rgba(1, 8, 22, 0.84) 0%, rgba(2, 12, 31, 0.58) 46%, rgba(2, 8, 23, 0.32) 75%, rgba(2, 8, 23, 0.2) 100%);
        }

        [data-login-layout] {
          width: 100%;
          max-width: var(--login-shell-max-width);
        }

        [data-login-message] {
          color: white;
          text-shadow: 0 12px 28px rgb(2 8 23 / 0.42);
        }

        [data-login-style="LightCorporate"] [data-login-message] {
          color: rgb(8 34 78);
          text-shadow: none;
        }

        [data-login-logo-mark] {
          display: flex;
          width: 44px;
          height: 44px;
          align-items: flex-end;
          justify-content: center;
          gap: 3px;
        }

        [data-login-logo-mark] > span {
          width: 8px;
          border-radius: 3px 3px 1px 1px;
          background: linear-gradient(180deg, rgb(59 130 246), rgb(37 99 235));
          box-shadow: 0 4px 12px rgb(37 99 235 / 0.24);
        }

        [data-login-logo-mark] > span:nth-child(1) { height: 18px; }
        [data-login-logo-mark] > span:nth-child(2) { height: 29px; }
        [data-login-logo-mark] > span:nth-child(3) { height: 38px; }
        [data-login-logo-mark] > span:nth-child(4) { height: 25px; }

        [data-login-logo-mark][data-dark="true"] > span {
          background: linear-gradient(180deg, rgb(56 189 248), rgb(37 99 235));
          box-shadow: 0 4px 16px rgb(14 165 233 / 0.28);
        }

        [data-login-product-name] {
          color: white;
          font-size: 1.28rem;
          font-weight: 800;
          letter-spacing: 0.03em;
          line-height: 1;
        }

        [data-login-style="LightCorporate"] [data-login-product-name] {
          color: rgb(8 28 73);
          font-size: 21px;
          font-weight: 800;
          letter-spacing: 0.01em;
          line-height: 1;
        }

        [data-login-product-tagline] {
          color: rgb(226 232 240 / 0.82);
          font-size: 0.58rem;
          font-weight: 700;
          letter-spacing: 0.16em;
          text-transform: uppercase;
        }

        [data-login-style="LightCorporate"] [data-login-product-tagline] {
          color: rgb(47 68 106 / 0.94);
          font-size: 8px;
          font-weight: 600;
          letter-spacing: 0.19em;
          line-height: 1.15;
        }

        [data-login-style="LightCorporate"] [data-login-product] {
          filter: none;
        }

        [data-login-style="LightCorporate"] [data-login-logo-mark] > span {
          box-shadow: 0 3px 8px rgb(46 104 244 / 0.2);
        }

        [data-login-style="LightCorporate"] [data-login-logo-mark] > span:nth-child(1) {
          background: linear-gradient(180deg, rgb(34 211 238), rgb(88 62 225));
        }

        [data-login-style="LightCorporate"] [data-login-logo-mark] > span:nth-child(2) {
          background: linear-gradient(180deg, rgb(59 130 246), rgb(55 84 232));
        }

        [data-login-style="LightCorporate"] [data-login-logo-mark] > span:nth-child(3) {
          background: linear-gradient(180deg, rgb(71 162 248), rgb(70 77 226));
        }

        [data-login-style="LightCorporate"] [data-login-logo-mark] > span:nth-child(4) {
          background: linear-gradient(180deg, rgb(75 137 246), rgb(88 61 221));
        }

        [data-login-eyebrow] {
          margin-bottom: 14px;
          color: rgb(186 230 253);
          font-size: 0.72rem;
          font-weight: 700;
          letter-spacing: 0.2em;
          text-transform: uppercase;
        }

        [data-login-style="LightCorporate"] [data-login-eyebrow] {
          display: none;
        }

        [data-login-headline] {
          max-width: var(--login-headline-width);
          font-size: var(--login-headline-size);
          font-weight: 700;
          letter-spacing: -0.04em;
          line-height: var(--login-headline-line-height);
        }

        [data-login-supporting-copy] {
          max-width: var(--login-copy-width);
          margin-top: 32px;
          color: rgb(241 245 249 / 0.88);
          font-size: 1rem;
          line-height: 1.65;
        }

        [data-login-style="LightCorporate"] [data-login-supporting-copy] {
          margin-top: 18px;
        }

        [data-login-style="LightCorporate"] [data-login-supporting-copy] {
          color: rgb(38 58 86);
        }

        [data-login-benefits] {
          display: none;
          max-width: var(--login-headline-width);
          grid-template-columns: var(--login-feature-columns);
          column-gap: var(--login-feature-column-gap);
          row-gap: var(--login-feature-row-gap);
          margin-top: 32px;
          color: rgb(248 250 252 / 0.9);
          font-size: 0.78rem;
          font-weight: 600;
        }

        [data-login-style="LightCorporate"] [data-login-benefits] {
          color: rgb(8 34 78);
          margin-top: 37px;
        }

        [data-login-benefits] li {
          display: flex;
          min-width: 0;
          align-items: center;
          gap: 8px;
        }

        [data-login-benefits] svg {
          width: 15px;
          height: 15px;
          flex: 0 0 auto;
          color: rgb(125 211 252);
        }

        [data-login-style="DarkPremium"] [data-login-benefits] li {
          flex-direction: column;
          align-items: center;
          gap: 9px;
          text-align: center;
          line-height: 1.3;
        }

        [data-login-benefit-icon] {
          display: grid;
          width: 42px;
          height: 42px;
          flex: 0 0 auto;
          place-items: center;
          border-radius: 50%;
          background: rgb(219 234 254 / 0.8);
          color: rgb(37 99 235);
        }

        [data-login-benefit-icon] svg {
          width: 19px;
          height: 19px;
        }

        [data-login-benefit-copy] {
          display: grid;
          min-width: 0;
          gap: 2px;
        }

        [data-login-benefit-copy] strong {
          font-size: 0.76rem;
          font-weight: 700;
        }

        [data-login-benefit-copy] small {
          color: rgb(71 85 105);
          font-size: 0.66rem;
          font-weight: 500;
          line-height: 1.25;
        }

        [data-login-benefits][data-premium="true"] [data-login-benefit-icon] {
          width: 48px;
          height: 48px;
          border: 1px solid rgb(147 197 253 / 0.2);
          border-radius: 10px;
          background: rgb(30 64 175 / 0.22);
          color: rgb(226 232 240);
        }

        [data-login-benefits][data-premium="true"] [data-login-benefit-copy] strong {
          max-width: 86px;
          font-size: 0.68rem;
          line-height: 1.35;
        }

        [data-login-benefits][data-premium="false"] li:nth-child(2) [data-login-benefit-icon] {
          background: rgb(229 249 237 / 0.96);
          color: rgb(6 181 94);
        }

        [data-login-benefits][data-premium="false"] li:nth-child(3) [data-login-benefit-icon] {
          background: rgb(239 234 255 / 0.96);
          color: rgb(113 35 235);
        }

        [data-login-benefits][data-premium="false"] li:nth-child(4) [data-login-benefit-icon] {
          background: rgb(255 235 223 / 0.97);
          color: rgb(255 105 45);
        }

        [data-login-benefits][data-premium="false"] li {
          gap: 11px;
        }

        [data-login-benefits][data-premium="false"] [data-login-benefit-icon] {
          width: 38px;
          height: 38px;
          background: rgb(229 240 255 / 0.96);
          color: rgb(38 108 242);
        }

        [data-login-benefits][data-premium="false"] [data-login-benefit-icon] svg {
          width: 20px;
          height: 20px;
          color: inherit;
        }

        [data-login-benefits][data-premium="false"] li:first-child [data-login-benefit-icon] svg {
          stroke-width: 2.8;
        }

        [data-login-benefits][data-premium="false"] [data-login-benefit-copy] {
          gap: 1px;
        }

        [data-login-benefits][data-premium="false"] [data-login-benefit-copy] strong {
          color: rgb(8 34 78);
          font-size: 13px;
          font-weight: 700;
          letter-spacing: -0.012em;
          line-height: 1.15;
        }

        [data-login-benefits][data-premium="false"] [data-login-benefit-copy] small {
          color: rgb(39 64 101);
          font-size: 11px;
          font-weight: 400;
          letter-spacing: -0.005em;
          line-height: 1.2;
        }

        [data-login-promise] {
          display: flex;
          align-items: center;
          justify-content: center;
          margin-top: 14px;
          color: rgb(241 245 249 / 0.76);
          font-size: 0.65rem;
          font-weight: 700;
          letter-spacing: 0.16em;
          text-transform: uppercase;
        }

        [data-login-promise-copy] {
          display: flex;
          align-items: center;
          gap: 8px;
          white-space: nowrap;
        }

        [data-login-style="LightCorporate"] [data-login-promise] {
          display: grid;
          justify-items: start;
          gap: 8px;
          color: rgb(42 62 98 / 0.92);
          font-size: 9px;
          font-weight: 600;
          letter-spacing: 0.22em;
          line-height: 1;
          text-shadow: none;
        }

        [data-login-style="LightCorporate"] [data-login-promise-copy] {
          gap: 9px;
        }

        [data-login-promise-separator] {
          display: block;
          width: 4px;
          height: 4px;
          flex: 0 0 auto;
          overflow: hidden;
          border-radius: 999px;
          background: rgb(125 211 252);
          color: transparent;
          font-size: 0;
          letter-spacing: 0;
        }

        [data-login-style="LightCorporate"] [data-login-promise-separator] {
          width: auto;
          height: auto;
          overflow: visible;
          border-radius: 0;
          background: none;
          color: rgb(42 62 98 / 0.82);
          font-size: 6px;
          line-height: 1;
        }

        [data-login-promise-strokes] {
          display: flex;
          align-items: center;
          gap: 5px;
          letter-spacing: 0;
        }

        [data-login-promise-strokes] > span {
          display: block;
          width: 23px;
          height: 3px;
          border-radius: 999px;
          background: rgb(37 125 255);
        }

        [data-login-promise-strokes] > span:nth-child(2) {
          width: 26px;
          background: rgb(25 178 113);
        }

        [data-login-promise-strokes] > span:nth-child(3) {
          width: 25px;
          background: rgb(109 40 217);
        }

        [data-login-promise-strokes] > span:nth-child(4) {
          width: 19px;
          background: rgb(246 91 50);
        }

        [data-login-quote] {
          position: relative;
          margin-top: 46px;
          padding-left: 28px;
          color: rgb(203 213 225 / 0.72);
          font-size: 0.76rem;
          font-style: italic;
          line-height: 1.5;
        }

        [data-login-quote] > span {
          position: absolute;
          top: -10px;
          left: 0;
          color: rgb(96 165 250 / 0.7);
          font-size: 2rem;
          font-style: normal;
          font-weight: 800;
        }

        [data-login-form-column] {
          width: min(100%, var(--login-card-width));
        }

        [data-login-card] {
          width: 100%;
          border-radius: var(--login-card-radius);
        }

        [data-login-style="LightCorporate"] [data-login-card] {
          border: 1px solid rgb(148 163 184 / 0.3);
          background: rgb(255 255 255 / 0.97);
          color: rgb(15 23 42);
          box-shadow: 0 24px 60px rgb(15 23 42 / 0.2), 0 2px 8px rgb(15 23 42 / 0.08);
          backdrop-filter: blur(12px) saturate(108%);
        }

        [data-login-style="DarkPremium"] [data-login-card] {
          position: relative;
          isolation: isolate;
          overflow: hidden;
          display: flex;
          min-height: 530px;
          flex-direction: column;
          border: 1px solid rgb(191 219 254 / 0.54);
          background:
            linear-gradient(145deg, rgb(96 145 225 / 0.23) 0%, rgb(34 66 126 / 0.18) 34%, rgb(8 20 48 / 0.7) 76%),
            rgb(8 20 48 / 0.54);
          color: white;
          box-shadow:
            0 30px 78px rgb(1 8 22 / 0.56),
            0 0 48px rgb(56 189 248 / 0.13),
            inset 0 1px 0 rgb(255 255 255 / 0.2),
            inset 1px 0 0 rgb(147 197 253 / 0.11),
            inset -1px 0 0 rgb(191 219 254 / 0.08);
          -webkit-backdrop-filter: blur(30px) saturate(142%);
          backdrop-filter: blur(30px) saturate(142%);
        }

        [data-login-style="DarkPremium"] [data-login-card]::before {
          position: absolute;
          z-index: -1;
          inset: 0;
          border-radius: inherit;
          background:
            radial-gradient(circle at 12% 0%, rgb(191 219 254 / 0.2), transparent 36%),
            linear-gradient(180deg, rgb(255 255 255 / 0.08), transparent 28%);
          box-shadow:
            inset 0 0 0 1px rgb(125 211 252 / 0.08),
            inset 0 -22px 50px rgb(2 8 23 / 0.22);
          content: '';
          pointer-events: none;
        }

        [data-login-card-header] {
          padding: var(--login-card-padding);
          padding-bottom: 16px;
        }

        [data-login-style="DarkPremium"] [data-login-card-header] {
          padding-bottom: 24px;
        }

        [data-login-card-title] {
          display: grid;
          gap: 1px;
          text-align: left;
          line-height: 1.05;
        }

        [data-login-card-title] > span {
          font-size: 1.35em;
          letter-spacing: -0.025em;
        }

        [data-login-card-description] {
          text-align: left;
        }

        [data-login-card-content] {
          padding-top: 8px;
          padding-right: var(--login-card-padding);
          padding-bottom: var(--login-card-padding);
          padding-left: var(--login-card-padding);
        }

        [data-login-style="DarkPremium"] [data-login-card-content] {
          display: flex;
          flex: 1;
          flex-direction: column;
        }

        [data-login-style="DarkPremium"] [data-login-card-content] > form {
          display: grid;
          gap: 22px;
        }

        [data-login-style="DarkPremium"] [data-login-card-content] > form > :not([hidden]) ~ :not([hidden]) {
          margin-top: 0;
        }

        [data-login-style="DarkPremium"] [data-login-card-content] > form + div {
          margin-top: 28px;
          padding-top: 22px;
        }

        [data-login-field] {
          height: var(--login-card-field-height);
        }

        [data-login-primary-action],
        [data-login-secondary-action] {
          height: var(--login-card-action-height);
        }

        @media (min-width: 768px) {
          [data-login-layout] {
            grid-template-columns: minmax(0, var(--login-left-column-width)) minmax(0, 1fr);
            align-items: stretch;
            align-content: start;
            gap: var(--login-column-gap);
            min-height: 100svh;
          }

          [data-login-brand] {
            position: relative;
            min-height: 660px;
          }

          [data-login-product] {
            position: absolute;
            top: var(--login-brand-top);
            left: var(--login-brand-left);
          }

          [data-login-message] {
            position: absolute;
            top: var(--login-message-top);
            left: var(--login-message-left);
            text-align: left;
          }

          [data-login-desktop-copy] {
            display: block;
          }

          [data-login-promise] {
            position: fixed;
            bottom: 40px;
            left: max(28px, calc((100vw - var(--login-shell-max-width)) / 2 + var(--login-message-left)));
            justify-content: flex-start;
            margin-top: 0;
          }

          [data-login-style="LightCorporate"] [data-login-promise] {
            position: static;
            margin-top: 48px;
          }

          [data-login-style="DarkPremium"] [data-login-promise] {
            right: max(28px, calc((100vw - var(--login-shell-max-width)) / 2 + 28px));
            left: auto;
          }

          [data-login-form-column] {
            align-self: start;
            justify-self: start;
            margin-top: var(--login-card-top);
          }

        }

        @media (min-width: 1024px) {
          [data-login-benefits] {
            display: grid;
          }

          [data-login-style="DarkPremium"] [data-login-form-column] {
            transform: translateX(72px);
          }
        }

        @media (max-width: 767px) {
          [data-login-layout] {
            align-content: start;
            gap: 20px;
            padding: 6rem 1rem 1.5rem;
          }

          [data-login-brand] {
            display: contents;
          }

          [data-login-product] {
            margin-inline: auto;
          }

          [data-login-message] {
            text-align: center;
          }

          [data-login-promise] {
            position: static;
          }

          [data-login-style="LightCorporate"] [data-login-promise] {
            justify-items: center;
            margin-inline: auto;
          }

          [data-login-desktop-copy],
          [data-login-benefits],
          [data-login-quote] {
            display: none;
          }

          [data-login-card-header] {
            padding: 22px 20px 16px;
          }

          [data-login-card-content] {
            padding-right: 20px;
            padding-bottom: 20px;
            padding-left: 20px;
          }

          [data-login-style="DarkPremium"] [data-login-card] {
            min-height: 0;
          }

          [data-login-style="DarkPremium"] [data-login-card-content] > form {
            gap: 16px;
          }

          [data-login-style="DarkPremium"] [data-login-card-content] > form + div {
            margin-top: 16px;
            padding-top: 16px;
          }

          [data-login-form-column] {
            justify-self: center;
          }
        }
      `}</style>

      <div data-login-layout className="mx-auto grid min-h-svh grid-cols-1 content-center gap-4">
        <div data-login-brand>
          <ProductMark dark={dark} />
          <EnterpriseMessage premium={dark} />
        </div>
        <div data-login-form-column>{children}</div>
      </div>

    </main>
  );
}

export function LightCorporateLogin({ children }: LoginShellProps) {
  return <ShellFrame style="LightCorporate">{children}</ShellFrame>;
}

export function DarkPremiumLogin({ children }: LoginShellProps) {
  return <ShellFrame style="DarkPremium">{children}</ShellFrame>;
}

export function LoginPresentation({
  style,
  children,
}: LoginShellProps & { style: LoginPageStyle }) {
  return style === 'DarkPremium' ? (
    <DarkPremiumLogin>{children}</DarkPremiumLogin>
  ) : (
    <LightCorporateLogin>{children}</LightCorporateLogin>
  );
}

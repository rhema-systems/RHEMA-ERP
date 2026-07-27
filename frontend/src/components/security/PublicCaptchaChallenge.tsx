'use client';

import {
  forwardRef,
  useEffect,
  useImperativeHandle,
  useRef,
} from 'react';
import ReCAPTCHA from 'react-google-recaptcha';

type CaptchaProvider = 'recaptcha' | 'hcaptcha';

type HCaptchaWidgetId = string | number;

interface HCaptchaApi {
  render(
    container: HTMLElement,
    options: {
      sitekey: string;
      theme: 'light' | 'dark';
      callback: (token: string) => void;
      'expired-callback': () => void;
      'error-callback': () => void;
    }
  ): HCaptchaWidgetId;
  reset(widgetId?: HCaptchaWidgetId): void;
  remove?(widgetId: HCaptchaWidgetId): void;
}

declare global {
  interface Window {
    hcaptcha?: HCaptchaApi;
  }
}

export interface PublicCaptchaChallengeHandle {
  reset(): void;
}

interface PublicCaptchaChallengeProps {
  id: string;
  enabled: boolean;
  provider: CaptchaProvider;
  recaptchaSiteKey?: string | null;
  hCaptchaSiteKey?: string | null;
  onChange(token: string | null): void;
}

const HCAPTCHA_SCRIPT_ID = 'tdc-hcaptcha-script';

export const PublicCaptchaChallenge = forwardRef<
  PublicCaptchaChallengeHandle,
  PublicCaptchaChallengeProps
>(function PublicCaptchaChallenge(
  {
    id,
    enabled,
    provider,
    recaptchaSiteKey,
    hCaptchaSiteKey,
    onChange,
  },
  ref
) {
  const recaptchaRef = useRef<ReCAPTCHA>(null);
  const hcaptchaContainerRef = useRef<HTMLDivElement>(null);
  const hcaptchaWidgetIdRef = useRef<HCaptchaWidgetId | null>(null);
  const onChangeRef = useRef(onChange);

  useEffect(() => {
    onChangeRef.current = onChange;
  }, [onChange]);

  useImperativeHandle(ref, () => ({
    reset() {
      onChangeRef.current(null);
      if (provider === 'recaptcha') {
        recaptchaRef.current?.reset();
      } else if (
        hcaptchaWidgetIdRef.current !== null &&
        window.hcaptcha
      ) {
        window.hcaptcha.reset(hcaptchaWidgetIdRef.current);
      }
    },
  }), [provider]);

  useEffect(() => {
    if (
      !enabled ||
      provider !== 'hcaptcha' ||
      !hCaptchaSiteKey ||
      !hcaptchaContainerRef.current
    ) {
      return;
    }

    let disposed = false;
    const container = hcaptchaContainerRef.current;

    const render = () => {
      if (
        disposed ||
        !window.hcaptcha ||
        hcaptchaWidgetIdRef.current !== null
      ) {
        return;
      }
      hcaptchaWidgetIdRef.current = window.hcaptcha.render(container, {
        sitekey: hCaptchaSiteKey,
        theme: 'dark',
        callback: (token) => onChangeRef.current(token),
        'expired-callback': () => onChangeRef.current(null),
        'error-callback': () => onChangeRef.current(null),
      });
    };

    let script = document.getElementById(
      HCAPTCHA_SCRIPT_ID
    ) as HTMLScriptElement | null;
    if (!script) {
      script = document.createElement('script');
      script.id = HCAPTCHA_SCRIPT_ID;
      script.src = 'https://js.hcaptcha.com/1/api.js?render=explicit';
      script.async = true;
      script.defer = true;
      document.head.appendChild(script);
    }
    script.addEventListener('load', render);
    render();
    const pollId = window.setInterval(() => {
      if (window.hcaptcha) {
        window.clearInterval(pollId);
        render();
      }
    }, 100);

    return () => {
      disposed = true;
      script?.removeEventListener('load', render);
      window.clearInterval(pollId);
      if (
        hcaptchaWidgetIdRef.current !== null &&
        window.hcaptcha?.remove
      ) {
        window.hcaptcha.remove(hcaptchaWidgetIdRef.current);
      }
      hcaptchaWidgetIdRef.current = null;
    };
  }, [enabled, hCaptchaSiteKey, provider]);

  if (!enabled) return null;

  const siteKey =
    provider === 'hcaptcha' ? hCaptchaSiteKey : recaptchaSiteKey;
  if (!siteKey) {
    return (
      <p
        id={`${id}-configuration-error`}
        className="text-sm text-amber-300"
        role="alert"
      >
        CAPTCHA is enabled but its public site key is not configured. Contact
        support.
      </p>
    );
  }

  if (provider === 'hcaptcha') {
    return (
      <div
        id={id}
        ref={hcaptchaContainerRef}
        aria-describedby={`${id}-description`}
      />
    );
  }

  return (
    <div id={id}>
      <ReCAPTCHA
        ref={recaptchaRef}
        sitekey={siteKey}
        theme="dark"
        onChange={onChange}
        onExpired={() => onChange(null)}
        onErrored={() => onChange(null)}
      />
    </div>
  );
});

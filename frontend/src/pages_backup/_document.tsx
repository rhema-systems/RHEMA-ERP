/* eslint-disable */
import { Head, Html, Main, NextScript } from 'next/document';

// Some build paths in this repo still expect a pages-router Document module to exist.
// Providing a minimal custom Document keeps `next build` stable while the app uses the App Router.
export default function Document() {
  return (
    <Html lang="en">
      <Head />
      <body>
        <Main />
        <NextScript />
      </body>
    </Html>
  );
}


import { ScrollViewStyleReset } from 'expo-router/html';

export default function Root({ children }: { children: React.ReactNode }) {
  return (
    <html lang="zh-CN">
      <head>
        <meta charSet="utf-8" />
        <meta httpEquiv="X-UA-Compatible" content="IE=edge" />
        <meta
          name="viewport"
          content="width=device-width, initial-scale=1, maximum-scale=1, viewport-fit=cover"
        />
        <meta name="theme-color" content="#4A90A4" />
        <meta name="mobile-web-app-capable" content="yes" />
        <meta name="apple-mobile-web-app-capable" content="yes" />
        <meta name="apple-mobile-web-app-status-bar-style" content="default" />
        <ScrollViewStyleReset />
        <style
          dangerouslySetInnerHTML={{
            __html: `
              html, body, #root {
                height: 100%;
                margin: 0;
                background: #F5F0E8;
                overflow: hidden;
              }
              body {
                font-family: "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;
                -webkit-font-smoothing: antialiased;
                -webkit-tap-highlight-color: transparent;
                touch-action: manipulation;
                overscroll-behavior: none;
                padding: env(safe-area-inset-top) env(safe-area-inset-right)
                  env(safe-area-inset-bottom) env(safe-area-inset-left);
              }
              * { box-sizing: border-box; }
              input, textarea, select {
                font-size: 16px;
              }
              input:focus, textarea:focus {
                outline: 2px solid #4A90A4;
                outline-offset: 1px;
              }
              ::-webkit-scrollbar { width: 6px; height: 6px; }
              ::-webkit-scrollbar-thumb {
                background: rgba(74, 144, 164, 0.35);
                border-radius: 8px;
              }
              @media (max-width: 767px) {
                ::-webkit-scrollbar { width: 0; height: 0; }
              }
            `,
          }}
        />
      </head>
      <body>{children}</body>
    </html>
  );
}

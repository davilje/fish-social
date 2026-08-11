import pino from 'pino';

const LOKI_ENABLED = process.env.LOKI_ENABLED === 'true';
const LOKI_HOST = process.env.LOKI_HOST ?? 'http://localhost:3100';
const LOKI_BASIC_AUTH = process.env.LOKI_BASIC_AUTH ?? '';

let lokiStream: pino.DestinationStream | null = null;
let lokiInitialized = false;

function createLokiStream(): pino.DestinationStream | null {
  try {
    const PinoLoki = require('pino-loki');
    const stream = PinoLoki({
      host: LOKI_HOST,
      labels: {
        service: process.env.LOKI_LABEL_SERVICE ?? 'fish-social-server',
        env: process.env.NODE_ENV ?? 'development',
      },
      json: true,
      batching: true,
      interval: 5,
      maxBatchSize: 100,
      ...(LOKI_BASIC_AUTH ? { basicAuth: { username: '', password: LOKI_BASIC_AUTH } } : {}),
    });
    return stream;
  } catch (e) {
    console.warn('[loki] Failed to init pino-loki transport:', e instanceof Error ? e.message : String(e));
    return null;
  }
}

export function getLokiStream(): pino.DestinationStream | null {
  if (lokiInitialized) return lokiStream;
  lokiInitialized = true;
  if (LOKI_ENABLED) {
    lokiStream = createLokiStream();
    if (lokiStream) {
      console.log('[loki] Loki transport initialized, host=' + LOKI_HOST);
    }
  } else {
    console.log('[loki] Loki disabled (LOKI_ENABLED != true)');
  }
  return lokiStream;
}

export function getLokiEnabled(): boolean {
  return LOKI_ENABLED;
}

/**
 * 日批告警 Webhook（D-L3-08）
 * 用法: node scripts/analytics/send-daily-alert.mjs <summary.json>
 */
import fs from 'fs';

const summaryPath = process.argv[2];
if (!summaryPath || !fs.existsSync(summaryPath)) {
  console.warn('[daily-alert] summary.json not found, skip');
  process.exit(0);
}

const summary = JSON.parse(fs.readFileSync(summaryPath, 'utf8'));
const webhookUrl = process.env.DAILY_ALERT_WEBHOOK_URL || process.env.ALERT_WEBHOOK_URL || '';
const minLevel = process.env.DAILY_ALERT_MIN_LEVEL || 'bad';
const dryRun = process.env.DAILY_ALERT_DRY_RUN === '1';
const webhookType = process.env.ALERT_WEBHOOK_TYPE || 'generic';

const levelRank = { info: 0, warn: 1, bad: 2, warning: 1, critical: 2 };
const minRank = levelRank[minLevel] ?? 2;

const alerts = (summary.alerts || []).filter((a) => (levelRank[a.level] ?? 0) >= minRank);

if (!alerts.length) {
  console.log('[daily-alert] no alerts at min level, skip');
  process.exit(0);
}

const payload = {
  dateKey: summary.meta?.dateKey,
  rulesVersion: summary.meta?.rulesVersion,
  reportPath: `docs/analytics/daily/${summary.meta?.dateKey}/report.html`,
  alerts,
  generatedAt: summary.meta?.generatedAt,
};

if (!webhookUrl) {
  console.log('[daily-alert] no webhook configured, skip');
  process.exit(0);
}

function formatBody(p) {
  const lines = p.alerts.map((a) => `- [${a.level}] ${a.id}: ${a.message}`).join('\n');
  const text = `运营日报告警 ${p.dateKey} (${p.rulesVersion})\n${lines}\n报告: ${p.reportPath}`;
  switch (webhookType) {
    case 'dingtalk':
      return { msgtype: 'text', text: { content: text } };
    case 'wework':
      return { msgtype: 'markdown', markdown: { content: `### 运营日报告警 ${p.dateKey}\n${lines}` } };
    default:
      return p;
  }
}

const body = formatBody(payload);

if (dryRun) {
  console.log('[daily-alert] DRY_RUN payload:', JSON.stringify(body, null, 2));
  process.exit(0);
}

try {
  const res = await fetch(webhookUrl, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
    signal: AbortSignal.timeout(5000),
  });
  if (!res.ok) {
    console.warn(`[daily-alert] webhook HTTP ${res.status}: ${await res.text()}`);
  } else {
    console.log(`[daily-alert] sent ${alerts.length} alert(s) for ${summary.meta?.dateKey}`);
  }
} catch (e) {
  console.warn('[daily-alert] webhook failed:', e instanceof Error ? e.message : String(e));
}

const ALERT_WEBHOOK_URL = process.env.ALERT_WEBHOOK_URL ?? '';
const ALERT_WEBHOOK_TYPE = process.env.ALERT_WEBHOOK_TYPE ?? 'generic';

interface AlertPayload {
  title: string;
  message: string;
  severity: 'critical' | 'warning' | 'info';
  ruleName: string;
  labels?: Record<string, string>;
  timestamp: number;
}

export async function sendAlert(payload: AlertPayload): Promise<boolean> {
  if (!ALERT_WEBHOOK_URL) {
    console.warn("[alert] No ALERT_WEBHOOK_URL configured, alert \"" + payload.title + "\" not sent");
    return false;
  }
  try {
    const body = formatPayload(payload);
    const response = await fetch(ALERT_WEBHOOK_URL, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
      signal: AbortSignal.timeout(5000),
    });
    if (!response.ok) {
      console.warn("[alert] Webhook returned " + response.status + ": " + await response.text());
      return false;
    }
    console.log("[alert] Sent: " + payload.title + " (" + payload.severity + ")");
    return true;
  } catch (e) {
    console.warn("[alert] Failed to send webhook:", e instanceof Error ? e.message : String(e));
    return false;
  }
}

function formatPayload(payload: AlertPayload): Record<string, unknown> {
  switch (ALERT_WEBHOOK_TYPE) {
    case "dingtalk":
      return {
        msgtype: "text",
        text: {
          content: "[" + payload.severity.toUpperCase() + "] " + payload.title + "\n" + payload.message + "\nRule: " + payload.ruleName + "\nTime: " + new Date(payload.timestamp).toISOString(),
        },
      };
    case "wework":
      return {
        msgtype: "markdown",
        markdown: {
          content: "### " + payload.title + "\n> " + payload.message + "\n> Rule: " + payload.ruleName + "\n> Time: " + new Date(payload.timestamp).toISOString(),
        },
      };
    default:
      return {
        title: payload.title,
        message: payload.message,
        severity: payload.severity,
        rule: payload.ruleName,
        timestamp: payload.timestamp,
        labels: payload.labels ?? {},
      };
  }
}

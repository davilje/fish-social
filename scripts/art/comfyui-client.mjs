/**
 * Thin ComfyUI HTTP client for Fish Social art generation.
 */
import { randomUUID } from 'crypto';

export function createComfyClient({
  baseUrl,
  pollIntervalMs = 1500,
  timeoutMs = 300000,
  clientIdPrefix = 'fish-social-art',
} = {}) {
  const root = String(baseUrl || 'http://127.0.0.1:8188').replace(/\/$/, '');

  async function request(pathname, { method = 'GET', body, expectJson = true } = {}) {
    const url = `${root}${pathname}`;
    let res;
    try {
      res = await fetch(url, {
        method,
        headers: body ? { 'Content-Type': 'application/json' } : undefined,
        body: body ? JSON.stringify(body) : undefined,
      });
    } catch (err) {
      const msg = err?.cause?.code || err?.message || String(err);
      throw new Error(`无法连接 ComfyUI ${root}（${msg}）。请确认服务已启动。`);
    }
    if (!res.ok) {
      const text = await res.text().catch(() => '');
      throw new Error(`ComfyUI ${method} ${pathname} → ${res.status}: ${text.slice(0, 400)}`);
    }
    if (!expectJson) return res;
    return res.json();
  }

  async function healthCheck() {
    const stats = await request('/system_stats');
    let queue = null;
    try {
      queue = await request('/queue');
    } catch {
      queue = null;
    }
    return { baseUrl: root, stats, queue };
  }

  async function listNodeClasses() {
    const info = await request('/object_info');
    return Object.keys(info || {}).sort();
  }

  async function hasNodeClass(className) {
    const classes = await listNodeClasses();
    return classes.includes(className);
  }

  function makeClientId() {
    return `${clientIdPrefix}-${randomUUID()}`;
  }

  async function queuePrompt(workflow, clientId = makeClientId()) {
    const data = await request('/prompt', {
      method: 'POST',
      body: { prompt: workflow, client_id: clientId },
    });
    if (data?.node_errors && Object.keys(data.node_errors).length > 0) {
      throw new Error(`ComfyUI workflow 校验失败: ${JSON.stringify(data.node_errors)}`);
    }
    if (!data?.prompt_id) {
      throw new Error(`ComfyUI /prompt 未返回 prompt_id: ${JSON.stringify(data)}`);
    }
    return { promptId: data.prompt_id, number: data.number, clientId };
  }

  async function getHistory(promptId) {
    const hist = await request(`/history/${promptId}`);
    return hist?.[promptId] || null;
  }

  function sleep(ms) {
    return new Promise((r) => setTimeout(r, ms));
  }

  async function waitForCompletion(promptId, {
    pollIntervalMs: interval = pollIntervalMs,
    timeoutMs: timeout = timeoutMs,
  } = {}) {
    const started = Date.now();
    while (Date.now() - started < timeout) {
      const entry = await getHistory(promptId);
      if (entry?.status?.completed || entry?.outputs) {
        const statusStr = entry?.status?.status_str;
        if (statusStr === 'error') {
          throw new Error(
            `ComfyUI 执行失败 prompt_id=${promptId}: ${JSON.stringify(entry.status)}`,
          );
        }
        if (entry?.outputs && Object.keys(entry.outputs).length > 0) {
          return entry;
        }
        if (entry?.status?.completed) {
          return entry;
        }
      }
      await sleep(interval);
    }
    throw new Error(`ComfyUI 等待超时（${timeout}ms）prompt_id=${promptId}`);
  }

  function resolveOutputImages(historyEntry) {
    const images = [];
    const outputs = historyEntry?.outputs || {};
    for (const [nodeId, out] of Object.entries(outputs)) {
      const list = out?.images || [];
      for (const img of list) {
        images.push({
          nodeId,
          filename: img.filename,
          subfolder: img.subfolder || '',
          type: img.type || 'output',
        });
      }
    }
    return images;
  }

  async function fetchImageBuffer({ filename, subfolder = '', type = 'output' }) {
    const qs = new URLSearchParams({ filename, subfolder, type });
    const res = await request(`/view?${qs}`, { expectJson: false });
    const buf = Buffer.from(await res.arrayBuffer());
    return buf;
  }

  return {
    baseUrl: root,
    healthCheck,
    listNodeClasses,
    hasNodeClass,
    makeClientId,
    queuePrompt,
    getHistory,
    waitForCompletion,
    resolveOutputImages,
    fetchImageBuffer,
  };
}

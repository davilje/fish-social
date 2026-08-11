import type { Request, Response, NextFunction } from 'express';

export type RbacRole = 'viewer' | 'operator' | 'admin';

const ADMIN_SECRET = process.env.ADMIN_SECRET ?? '';
const RBAC_RULES_RAW = process.env.ADMIN_RBAC_RULES ?? '';

interface RbacRule {
  key: string;
  role: RbacRole;
}

let rules: RbacRule[] = [];
let parsed = false;

function parseRbacRules(): void {
  if (parsed) return;
  parsed = true;
  if (!RBAC_RULES_RAW) {
    // No RBAC config: backward compatible, ADMIN_SECRET grants admin
    return;
  }
  try {
    const parsedRules = JSON.parse(RBAC_RULES_RAW) as RbacRule[];
    rules = parsedRules.filter((r) => ['viewer', 'operator', 'admin'].includes(r.role));
  } catch {
    console.warn('[rbac] Failed to parse ADMIN_RBAC_RULES, falling back to backward-compatible mode');
    rules = [];
  }
}

/** Live Inspector SSE �� EventSource cannot set X-Admin-Key (STAB-05 ��2.2). */
export function isAdminLiveSessionPath(req: Request): boolean {
  const p = req.path || '';
  return p === '/api/admin/live-session' || p.endsWith('/live-session');
}

/**
 * STAB-05: production REST rejects query.key unless ADMIN_ALLOW_QUERY_KEY=true.
 * SSE /live-session always allows query key (no plaintext in logs).
 */
export function allowAdminQueryKey(req: Request): boolean {
  if (isAdminLiveSessionPath(req)) return true;
  const explicit = process.env.ADMIN_ALLOW_QUERY_KEY;
  if (explicit === '1' || explicit === 'true') return true;
  if (explicit === '0' || explicit === 'false') return false;
  return process.env.NODE_ENV !== 'production';
}

export function extractAdminKey(req: Request): { key: string; fromQuery: boolean } {
  const header = String(req.header('X-Admin-Key') ?? '').trim();
  if (header) return { key: header, fromQuery: false };
  if (!allowAdminQueryKey(req)) return { key: '', fromQuery: false };
  const q = String(req.query.key ?? '').trim();
  return { key: q, fromQuery: Boolean(q) };
}

function auditSseQueryKey(req: Request): void {
  console.log(
    `[admin_sse_query_key_used] ${JSON.stringify({
      ts: Date.now(),
      eventType: 'admin_sse_query_key_used',
      method: req.method,
      path: req.path,
      ip: req.ip,
      // never log the key
    })}`,
  );
}

export function resolveRole(req: Request): RbacRole | null {
  parseRbacRules();
  const { key, fromQuery } = extractAdminKey(req);
  if (!key) return null;

  if (fromQuery && isAdminLiveSessionPath(req)) {
    auditSseQueryKey(req);
  }

  // Backward compatible: ADMIN_SECRET grants admin
  if (!RBAC_RULES_RAW) {
    if (key === ADMIN_SECRET) return 'admin';
    return null;
  }

  // RBAC mode
  for (const rule of rules) {
    if (key === rule.key) return rule.role;
  }
  if (key === ADMIN_SECRET) return 'admin'; // ADMIN_SECRET always admin fallback
  return null;
}

const ROLE_HIERARCHY: Record<RbacRole, number> = {
  viewer: 1,
  operator: 2,
  admin: 3,
};

export function requireRole(minRole: RbacRole) {
  return (req: Request, res: Response, next: NextFunction): void => {
    const role = resolveRole(req);
    if (!role) {
      res.status(401).json({ error: '��Ч�Ĺ���Ա��Կ' });
      return;
    }
    const minLevel = ROLE_HIERARCHY[minRole];
    const userLevel = ROLE_HIERARCHY[role];
    if (userLevel < minLevel) {
      res.status(403).json({ error: 'Ȩ�޲���', required: minRole, current: role });
      return;
    }
    next();
  };
}

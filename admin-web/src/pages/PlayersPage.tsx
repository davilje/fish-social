import { useEffect, useState } from 'react';
import { api, type PlayerOverviewRow } from '../api';
import { formatDurationMs, phaseLabel } from '../eventFormat';
import { ADMIN_PONDS, formatPondName } from '../pondNames';

const PHASES = [
  'idle',
  'seated',
  'baiting',
  'casting',
  'waiting',
  'hooked',
  'resolving',
  'stopping',
  'disconnected',
];

export function PlayersPage(props?: {
  onOpenPlayer?: (playerId: string, focus?: 'live' | 'history') => void;
  onOpenPond?: (pondId?: string) => void;
}) {
  const [hours, setHours] = useState(24);
  const [humansOnly, setHumansOnly] = useState(true);
  const [pondId, setPondId] = useState('');
  const [phase, setPhase] = useState('');
  const [q, setQ] = useState('');
  const [rows, setRows] = useState<PlayerOverviewRow[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function load() {
    setLoading(true);
    setError('');
    try {
      const res = await api.playersOverview({
        hours,
        humansOnly,
        pondId: pondId || undefined,
        phase: phase || undefined,
        q: q.trim() || undefined,
      });
      setRows(res.rows);
    } catch (e) {
      setRows([]);
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hours, humansOnly, pondId, phase]);

  const highlightId = rows.length === 1 ? rows[0]!.playerId : null;

  return (
    <section>
      <h2>玩家一览</h2>
      <p className="meta">默认仅真人 · 近 N 小时埋点聚合 + 当前在塘状态。塘/钓位显示中文名。</p>
      <div className="form-row">
        <label className="chk">
          <input
            type="checkbox"
            checked={humansOnly}
            onChange={(e) => setHumansOnly(e.target.checked)}
          />
          仅真人
        </label>
        <label>
          小时
          <input
            type="number"
            min={1}
            max={720}
            value={hours}
            onChange={(e) => setHours(Number(e.target.value) || 24)}
          />
        </label>
        <select value={pondId} onChange={(e) => setPondId(e.target.value)}>
          <option value="">全部鱼塘</option>
          {ADMIN_PONDS.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
        <select value={phase} onChange={(e) => setPhase(e.target.value)}>
          <option value="">全部相位</option>
          {PHASES.map((p) => (
            <option key={p} value={p}>
              {phaseLabel(p)}
            </option>
          ))}
        </select>
        <input
          placeholder="playerId / 昵称"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') void load();
          }}
        />
        <button type="button" onClick={() => void load()}>
          查找
        </button>
        <button type="button" onClick={() => void load()} disabled={loading}>
          刷新
        </button>
      </div>
      {error && <p className="err">{error}</p>}
      {!error && !loading && rows.length === 0 && (
        <p className="meta">近 {hours} 小时无埋点 / 无在塘用户（可关掉「仅真人」或放宽筛选）。</p>
      )}
      {rows.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>昵称</th>
              <th>playerId</th>
              <th>鱼塘</th>
              <th>钓位</th>
              <th>相位</th>
              <th>本局时长</th>
              <th>近{hours}h 钓获</th>
              <th>近{hours}h 断线</th>
              <th>近{hours}h 上钩</th>
              <th>类型</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr
                key={r.playerId}
                className={highlightId === r.playerId ? 'row-highlight' : undefined}
              >
                <td>
                  {r.nickname}
                  {r.online ? <span className="pill-online">在线</span> : null}
                </td>
                <td>
                  <code
                    title="点击复制"
                    style={{ cursor: 'pointer' }}
                    onClick={() => void navigator.clipboard?.writeText(r.playerId)}
                  >
                    {r.playerId}
                  </code>
                </td>
                <td title={r.pondId ?? undefined}>
                  {r.pondName ?? formatPondName(r.pondId) ?? '—'}
                </td>
                <td title={r.spotId ?? undefined}>{r.spotName ?? '—'}</td>
                <td>{phaseLabel(r.fishingPhase)}</td>
                <td>{formatDurationMs(r.sessionFishingMs)}</td>
                <td>{r.catchCount}</td>
                <td>{r.disconnectCount}</td>
                <td>{r.biteHookCount}</td>
                <td>{r.isBot ? '机器人' : '真人'}</td>
                <td className="ops-cell">
                  <button type="button" onClick={() => props?.onOpenPlayer?.(r.playerId, 'live')}>
                    详情
                  </button>
                  {r.pondId ? (
                    <button type="button" onClick={() => props?.onOpenPond?.(r.pondId!)}>
                      鱼塘
                    </button>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}

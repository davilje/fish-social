import { useEffect, useMemo, useRef, useState } from 'react';
import { api, getAdminKey, type PlayerLiveState, type PlayerTimeline } from '../api';
import { sopEventRowClass } from '../sopEventColors';
import {
  eventLabel,
  formatDurationMs,
  payloadTitle,
  phaseLabel,
  summarizeEventPayload,
} from '../eventFormat';
import { formatPondName, formatSpotName } from '../pondNames';

interface LiveTick {
  type: 'tick';
  live: PlayerLiveState;
  recentEvents: Array<{
    eventType: string;
    createdAt: number;
    pondId: string | null;
    payloadSummary: Record<string, unknown>;
  }>;
  pondHumans: number;
  pondBots: number;
  timestamp: number;
}

type EventRow = {
  key: string;
  createdAt: number;
  eventType: string;
  pondId: string | null;
  summary: string;
  title?: string;
  source: 'live' | 'history';
  raw?: { eventType: string; pondId: string | null; payload?: Record<string, unknown> };
};

export function PlayerDetailPage(props?: {
  initialPlayerId?: string;
  initialHours?: number;
  /** from old tab=live → prefer SSE; tab=timeline → history */
  initialFocus?: 'live' | 'history';
}) {
  const [playerId, setPlayerId] = useState(props?.initialPlayerId ?? '');
  const [hours, setHours] = useState(props?.initialHours ?? 24);
  const [timeline, setTimeline] = useState<PlayerTimeline | null>(null);
  const [live, setLive] = useState<PlayerLiveState | null>(null);
  const [recentLive, setRecentLive] = useState<LiveTick['recentEvents']>([]);
  const [pondCounts, setPondCounts] = useState({ humans: 0, bots: 0 });
  const [sseStatus, setSseStatus] = useState<'idle' | 'connected' | 'error'>('idle');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const sourceRef = useRef<EventSource | null>(null);

  function disconnectSse() {
    sourceRef.current?.close();
    sourceRef.current = null;
    setSseStatus('idle');
  }

  function connectSse(id = playerId) {
    disconnectSse();
    const key = getAdminKey();
    const pid = id.trim();
    if (!key || !pid) return;
    const url = `/api/admin/live-session?playerId=${encodeURIComponent(pid)}&key=${encodeURIComponent(key)}`;
    const es = new EventSource(url);
    sourceRef.current = es;
    es.onopen = () => setSseStatus('connected');
    es.onmessage = (ev) => {
      try {
        const data = JSON.parse(ev.data) as LiveTick & { type: string };
        if (data.type !== 'tick' || !data.live) return;
        setLive(data.live);
        setRecentLive(data.recentEvents ?? []);
        setPondCounts({ humans: data.pondHumans ?? 0, bots: data.pondBots ?? 0 });
      } catch {
        /* ignore */
      }
    };
    es.onerror = () => {
      setSseStatus('error');
      disconnectSse();
    };
  }

  async function loadAll(id = playerId, h = hours) {
    const pid = id.trim();
    if (!pid) return;
    setLoading(true);
    setError('');
    try {
      const [tl, state] = await Promise.all([
        api.playerTimeline(pid, h),
        api.liveState(pid).catch(() => null),
      ]);
      setTimeline(tl);
      if (state) setLive(state);
    } catch (e) {
      setTimeline(null);
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const id = props?.initialPlayerId?.trim();
    if (!id) return;
    void loadAll(id, props?.initialHours ?? 24);
    if (props?.initialFocus !== 'history' && getAdminKey()) {
      connectSse(id);
    }
    return () => disconnectSse();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const s = timeline?.summary;
  const fishingActive = live?.user?.status === 'fishing';
  const startedNull = fishingActive && live?.user?.fishingStartedAt == null;
  const phaseMissing =
    fishingActive && (live?.user?.fishingPhase == null || live.user.fishingPhase === 'idle');
  const sessionMissing = fishingActive && live?.user?.sessionFishingMs == null;
  const missingAnchorDiag = live?.diagnostics?.find((d) => d.id === 'missing_fishing_started_at');

  const events: EventRow[] = useMemo(() => {
    const hist: EventRow[] = (timeline?.events ?? []).map((ev) => ({
      key: `h-${ev.id}`,
      createdAt: ev.createdAt,
      eventType: ev.eventType,
      pondId: ev.pondId,
      summary: summarizeEventPayload(ev.eventType, ev.payload),
      title: payloadTitle(ev.payload),
      source: 'history' as const,
      raw: ev,
    }));
    const liveRows: EventRow[] = recentLive.map((ev, i) => ({
      key: `l-${ev.createdAt}-${i}`,
      createdAt: ev.createdAt,
      eventType: ev.eventType,
      pondId: ev.pondId,
      summary: summarizeEventPayload(ev.eventType, ev.payloadSummary),
      title: JSON.stringify(ev.payloadSummary),
      source: 'live' as const,
      raw: { eventType: ev.eventType, pondId: ev.pondId, payload: ev.payloadSummary },
    }));
    const byKey = new Map<string, EventRow>();
    for (const row of [...hist, ...liveRows]) {
      const dedupe = `${row.createdAt}|${row.eventType}|${row.summary}`;
      if (!byKey.has(dedupe) || row.source === 'live') byKey.set(dedupe, row);
    }
    return [...byKey.values()].sort((a, b) => b.createdAt - a.createdAt);
  }, [timeline, recentLive]);

  return (
    <section>
      <h2>玩家详情</h2>
      <p className="meta">实时状态 + 事件流（历史/SSE 同表）。旧深链 tab=timeline|live 会落到本页。</p>
      <div className="form-row">
        <input
          placeholder="playerId"
          value={playerId}
          onChange={(e) => setPlayerId(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') void loadAll();
          }}
        />
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
        <button type="button" onClick={() => void loadAll()} disabled={loading}>
          查询
        </button>
        <button type="button" onClick={() => connectSse()}>
          开始盯梢
        </button>
        <button type="button" onClick={disconnectSse}>
          停止
        </button>
        <span className="meta">SSE: {sseStatus}</span>
      </div>
      {error && <p className="err">{error}</p>}

      {missingAnchorDiag && (
        <div className="banner-error" role="alert">
          计时锚点缺失：{missingAnchorDiag.message}
        </div>
      )}

      <h3>§ 实时状态</h3>
      {live ? (
        <div className="live-cards">
          <div className={`live-card ${phaseMissing ? 'live-card-error' : ''}`}>
            <div className="lbl">相位</div>
            <div className={`val ${phaseMissing ? 'err' : ''}`}>
              {phaseLabel(live.user?.fishingPhase)}
            </div>
            <div className="sub">status={live.user?.status ?? '—'} · found={String(live.found)}</div>
          </div>
          <div className={`live-card ${sessionMissing ? 'live-card-error' : ''}`}>
            <div className="lbl">本局时长</div>
            <div className={`val ${sessionMissing ? 'err' : ''}`}>
              {formatDurationMs(live.user?.sessionFishingMs)}
            </div>
            <div className="sub">今日 {formatDurationMs(live.user?.todayFishingMs)}</div>
          </div>
          <div className={`live-card ${startedNull ? 'live-card-error' : ''}`}>
            <div className="lbl">StartedAt</div>
            <div className={`val ${startedNull ? 'err' : ''}`}>
              {live.user?.fishingStartedAt == null
                ? '—'
                : new Date(live.user.fishingStartedAt).toLocaleTimeString()}
            </div>
            <div className="sub">socket={live.socketBound ? 'bound' : 'unbound'}</div>
          </div>
          <div className="live-card">
            <div className="lbl">塘 · 钓位</div>
            <div className="val" title={live.pondId ?? undefined}>
              {formatPondName(live.pondId)}
            </div>
            <div className="sub" title={live.user?.spotId ?? undefined}>
              {formatSpotName(live.user?.spotId, live.pondId)} · 真人 {pondCounts.humans} · 机器人{' '}
              {pondCounts.bots}
            </div>
          </div>
        </div>
      ) : (
        <p className="meta">输入 playerId 查询后显示实时态；或点「开始盯梢」连 SSE。</p>
      )}

      {live?.diagnostics && live.diagnostics.length > 0 && (
        <ul className="diag-list">
          {live.diagnostics.map((d) => (
            <li key={d.id} className={`diag-${d.level}`}>
              <strong>{d.id}</strong> [{d.level}] {d.message}
            </li>
          ))}
        </ul>
      )}

      {s && (
        <>
          <h3>§ 近窗摘要</h3>
          <div className="live-cards">
            <div className="live-card">
              <div className="lbl">钓获</div>
              <div className="val">{Number(s.catchAcceptCount ?? 0)}</div>
            </div>
            <div className="live-card">
              <div className="lbl">上钩</div>
              <div className="val">{s.biteHookCount ?? 0}</div>
            </div>
            <div className="live-card">
              <div className="lbl">断线</div>
              <div className="val">{s.disconnectCount ?? 0}</div>
            </div>
            <div className="live-card">
              <div className="lbl">进塘失败</div>
              <div className="val">{s.joinPondFailCount ?? 0}</div>
            </div>
            <div className="live-card">
              <div className="lbl">开始钓鱼</div>
              <div className="val">{s.fishingStartCount ?? 0}</div>
            </div>
            <div className="live-card">
              <div className="lbl">相位切换</div>
              <div className="val">{s.phaseTransitionCount ?? 0}</div>
            </div>
          </div>
        </>
      )}

      <h3>§ 事件流</h3>
      {events.length === 0 ? (
        <p className="meta">暂无事件</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>时间</th>
              <th>来源</th>
              <th>事件</th>
              <th>塘</th>
              <th>摘要</th>
            </tr>
          </thead>
          <tbody>
            {events.map((ev) => (
              <tr key={ev.key} className={sopEventRowClass(ev.raw ?? { eventType: ev.eventType })}>
                <td>{new Date(ev.createdAt).toLocaleString()}</td>
                <td>{ev.source === 'live' ? '实时' : '历史'}</td>
                <td title={ev.eventType}>{eventLabel(ev.eventType)}</td>
                <td title={ev.pondId ?? undefined}>{formatPondName(ev.pondId)}</td>
                <td title={ev.title}>{ev.summary}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}

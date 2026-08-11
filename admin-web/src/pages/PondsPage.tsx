import { useEffect, useMemo, useRef, useState } from 'react';
import { api, type FishingDebugReport, type PondOverview } from '../api';
import { formatDurationMs, phaseLabel } from '../eventFormat';
import { ADMIN_PONDS, formatPondName, formatSpotName } from '../pondNames';

interface PondFishRow {
  id: string;
  spotId: string | null;
  quality: string;
  sizeM: number;
  speciesId?: string;
}

export function PondsPage(props?: { initialPondId?: string; focusDebug?: boolean }) {
  const [pondId, setPondId] = useState(() => {
    const init = props?.initialPondId;
    return init && ADMIN_PONDS.some((p) => p.id === init) ? init : ADMIN_PONDS[0].id;
  });
  const [overview, setOverview] = useState<PondOverview[]>([]);
  const [debug, setDebug] = useState<FishingDebugReport | null>(null);
  const [fish, setFish] = useState<PondFishRow[]>([]);
  const [error, setError] = useState('');
  const [humansOnly, setHumansOnly] = useState(false);
  const [loading, setLoading] = useState(false);
  const debugAnchor = useRef<HTMLDivElement | null>(null);

  async function loadOverview() {
    try {
      const res = await api.ponds();
      setOverview(res.ponds);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  }

  async function loadPond(pId = pondId, refresh = false) {
    setLoading(true);
    setError('');
    try {
      const [dbg, fishRes] = await Promise.all([
        api.fishingDebug(pId, undefined, { refresh }),
        api.pondFish(pId),
      ]);
      setDebug(dbg);
      setFish(fishRes.fish as PondFishRow[]);
    } catch (e) {
      setDebug(null);
      setFish([]);
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadOverview();
  }, []);

  useEffect(() => {
    void loadPond(pondId, false);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pondId]);

  useEffect(() => {
    if (props?.focusDebug) {
      requestAnimationFrame(() => {
        debugAnchor.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      });
    }
  }, [props?.focusDebug, debug]);

  const current = overview.find((p) => p.pondId === pondId);
  const fishers = useMemo(() => {
    if (!debug) return [];
    return humansOnly ? debug.activeFishers.filter((f) => !f.isBot) : debug.activeFishers;
  }, [debug, humansOnly]);

  const popRate =
    current?.summary?.maxPopulation && current.summary.maxPopulation > 0
      ? ((current.summary.fishCount ?? 0) / current.summary.maxPopulation) * 100
      : null;

  return (
    <section>
      <h2>鱼塘</h2>
      <p className="meta">概览 + 钓位概率（缓存快照）+ 鱼列表。旧 tab=debug 会打开本页并滚到钓位区。</p>
      <div className="form-row">
        <select value={pondId} onChange={(e) => setPondId(e.target.value)}>
          {ADMIN_PONDS.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
        <button type="button" onClick={() => void loadPond(pondId, false)} disabled={loading}>
          刷新概览
        </button>
        <label className="chk">
          <input
            type="checkbox"
            checked={humansOnly}
            onChange={(e) => setHumansOnly(e.target.checked)}
          />
          仅真人（在钓列表）
        </label>
      </div>
      {error && <p className="err">{error}</p>}

      <div className="live-cards">
        <div className="live-card">
          <div className="lbl">鱼塘</div>
          <div className="val">{formatPondName(pondId)}</div>
        </div>
        <div className="live-card">
          <div className="lbl">鱼数 / 上限</div>
          <div className="val">
            {current?.summary?.fishCount ?? debug?.summary.totalFish ?? '—'} /{' '}
            {current?.summary?.maxPopulation ?? '—'}
          </div>
        </div>
        <div className="live-card">
          <div className="lbl">真人 · 机器人</div>
          <div className="val">
            {current?.humanCount ?? '—'} · {humansOnly ? '—' : (current?.botCount ?? '—')}
          </div>
        </div>
        <div className="live-card">
          <div className="lbl">人口率</div>
          <div className="val">{popRate != null ? `${popRate.toFixed(0)}%` : '—'}</div>
        </div>
      </div>

      <div ref={debugAnchor} id="pond-debug">
        <h3>§ 钓位概率</h3>
        <p className="meta">
          主列：钓位权重（稳定）与鱼数。「抽样咬钩率」来自代表鱼抽签，默认读缓存；点「再抽样」才强制 refresh。
        </p>
        <div className="form-row">
          <button type="button" onClick={() => void loadPond(pondId, true)} disabled={loading}>
            再抽样
          </button>
        </div>
        {debug && (
          <table>
            <thead>
              <tr>
                <th>钓位</th>
                <th>权重 spotMultiplier</th>
                <th>鱼数</th>
                <th>抽样咬钩率</th>
              </tr>
            </thead>
            <tbody>
              {debug.spots.map((s) => (
                <tr key={s.spotId}>
                  <td title={s.spotId}>{formatSpotName(s.spotId, pondId)}</td>
                  <td>{s.spotMultiplier != null ? s.spotMultiplier.toFixed(2) : '—'}</td>
                  <td>{s.fishAtSpotCount}</td>
                  <td title="单次抽样，仅供参考">
                    {(s.tickBiteChance * 100).toFixed(2)}%
                    <span className="pill-sample">抽样</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <h3>§ 鱼列表</h3>
      {fish.length === 0 ? (
        <p className="meta">无鱼或尚未加载</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>品质</th>
              <th>体长 m</th>
              <th>钓点</th>
              <th>物种</th>
              <th>id</th>
            </tr>
          </thead>
          <tbody>
            {fish.slice(0, 80).map((f) => (
              <tr key={f.id}>
                <td>{f.quality}</td>
                <td>{f.sizeM?.toFixed?.(2) ?? f.sizeM}</td>
                <td title={f.spotId ?? undefined}>{formatSpotName(f.spotId, pondId)}</td>
                <td>{f.speciesId ?? '—'}</td>
                <td>
                  <code>{f.id.slice(0, 10)}</code>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {fish.length > 80 && <p className="meta">仅显示前 80 条（共 {fish.length}）</p>}

      <details className="raw-fold" open={fishers.length > 0}>
        <summary>§ 在钓玩家（{fishers.length}）</summary>
        <table>
          <thead>
            <tr>
              <th>昵称</th>
              <th>相位</th>
              <th>本局</th>
              <th>钓位</th>
              <th>类型</th>
            </tr>
          </thead>
          <tbody>
            {fishers.length === 0 && (
              <tr>
                <td colSpan={5}>当前无人在钓</td>
              </tr>
            )}
            {fishers.map((f) => (
              <tr key={f.userId} className={f.fishingStartedAt == null ? 'row-error' : undefined}>
                <td>{f.nickname ?? f.playerId ?? f.userId.slice(0, 8)}</td>
                <td>{phaseLabel(f.fishingPhase)}</td>
                <td>{formatDurationMs(f.sessionFishingMs)}</td>
                <td title={f.spotId}>{formatSpotName(f.spotId, pondId)}</td>
                <td>{f.isBot ? '机器人' : '真人'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </details>
    </section>
  );
}

import { useEffect, useMemo, useState } from 'react';
import { api, getAdminKey, setAdminKey } from './api';
import { BusinessHealthPage } from './pages/BusinessHealthPage';
import { PlayerDetailPage } from './pages/PlayerDetailPage';
import { PlayersPage } from './pages/PlayersPage';
import { PondsPage } from './pages/PondsPage';

/** Canonical tabs after ADMIN-OBS-1.4 */
type Tab = 'players' | 'player' | 'ponds' | 'health';

type DeepLink = {
  tab: Tab;
  playerId: string;
  pondId?: string;
  hours?: number;
  focus?: 'live' | 'history';
  focusDebug?: boolean;
};

function parseDeepLink(): DeepLink {
  const sp = new URLSearchParams(window.location.search);
  const raw = sp.get('tab') ?? 'players';
  const playerId = sp.get('playerId') ?? '';
  const pondId = sp.get('pondId') ?? undefined;
  const hoursRaw = sp.get('hours');
  const hours = hoursRaw != null && hoursRaw !== '' ? Number(hoursRaw) : undefined;

  // Legacy redirects
  if (raw === 'timeline') {
    return {
      tab: 'player',
      playerId,
      hours: Number.isFinite(hours) ? hours : undefined,
      focus: 'history',
    };
  }
  if (raw === 'live') {
    return {
      tab: 'player',
      playerId,
      hours: Number.isFinite(hours) ? hours : undefined,
      focus: 'live',
    };
  }
  if (raw === 'debug') {
    return {
      tab: 'ponds',
      playerId,
      pondId,
      focusDebug: true,
    };
  }

  const tab = (['players', 'player', 'ponds', 'health'].includes(raw) ? raw : 'players') as Tab;
  return {
    tab,
    playerId,
    pondId,
    hours: Number.isFinite(hours) ? hours : undefined,
    focusDebug: sp.get('focus') === 'debug',
  };
}

export function App() {
  const deep = useMemo(() => parseDeepLink(), []);
  const [tab, setTab] = useState<Tab>(deep.tab);
  const [jumpPlayerId, setJumpPlayerId] = useState(deep.playerId);
  const [jumpPondId, setJumpPondId] = useState(deep.pondId);
  const [playerFocus, setPlayerFocus] = useState<'live' | 'history' | undefined>(deep.focus);
  const [focusDebug, setFocusDebug] = useState(!!deep.focusDebug);
  const [adminKey, setAdminKeyState] = useState(getAdminKey());
  const [connected, setConnected] = useState<boolean | null>(null);
  const [authError, setAuthError] = useState('');

  // Rewrite legacy tab in address bar
  useEffect(() => {
    const sp = new URLSearchParams(window.location.search);
    const raw = sp.get('tab');
    if (raw === 'timeline' || raw === 'live') {
      sp.set('tab', 'player');
      window.history.replaceState(null, '', `?${sp.toString()}`);
    } else if (raw === 'debug') {
      sp.set('tab', 'ponds');
      window.history.replaceState(null, '', `?${sp.toString()}`);
    }
  }, []);

  async function probe(key: string) {
    const trimmed = key.trim();
    if (!trimmed) {
      setConnected(null);
      setAuthError('');
      return;
    }
    try {
      await api.status(trimmed);
      setConnected(true);
      setAuthError('');
    } catch (e) {
      setConnected(false);
      setAuthError(e instanceof Error ? e.message : '连接失败');
    }
  }

  useEffect(() => {
    void probe(adminKey);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const tabs = useMemo(
    () =>
      [
        ['players', '玩家', '全员一览 · 筛选 · 中文塘位'],
        ['player', '玩家详情', '实时状态 + 事件流'],
        ['ponds', '鱼塘', '概览 · 钓位概率 · 鱼列表'],
        ['health', '业务健康', '汇总卡 + 折线趋势'],
      ] as const,
    [],
  );

  const activeHint = tabs.find(([id]) => id === tab)?.[2] ?? '';

  function openPlayer(playerId: string, focus?: 'live' | 'history') {
    setJumpPlayerId(playerId);
    setPlayerFocus(focus ?? 'live');
    setTab('player');
    const sp = new URLSearchParams(window.location.search);
    sp.set('tab', 'player');
    sp.set('playerId', playerId);
    window.history.replaceState(null, '', `?${sp.toString()}`);
  }

  function openPond(pondId?: string) {
    if (pondId) setJumpPondId(pondId);
    setFocusDebug(true);
    setTab('ponds');
    const sp = new URLSearchParams(window.location.search);
    sp.set('tab', 'ponds');
    if (pondId) sp.set('pondId', pondId);
    window.history.replaceState(null, '', `?${sp.toString()}`);
  }

  return (
    <div className="app">
      <header className="header">
        <h1>Fish Social Admin</h1>
        <p className="tab-hint">
          实时排障台（非日报）。默认「玩家」。深链兼容{' '}
          <code>?tab=timeline|live|debug</code> → 玩家详情 / 鱼塘。
        </p>
        <div className="auth-row">
          <input
            type="password"
            placeholder="X-Admin-Key（与游戏 Debug 相同；默认见 .env ADMIN_SECRET）"
            value={adminKey}
            onChange={(e) => setAdminKeyState(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                setAdminKey(adminKey);
                void probe(adminKey);
              }
            }}
          />
          <button
            type="button"
            onClick={() => {
              setAdminKey(adminKey);
              void probe(adminKey);
            }}
          >
            保存并连接
          </button>
          {connected === true && <span className="ok">已连接</span>}
          {connected === false && (
            <span className="err">
              连接失败{authError ? `：${authError}` : ''}
              {' · '}
              请确认已启动游戏服（打开运营平台.bat / npm run server）
            </span>
          )}
        </div>
        <nav>
          {tabs.map(([id, label]) => (
            <button
              key={id}
              type="button"
              className={tab === id ? 'active' : ''}
              onClick={() => {
                setTab(id);
                const sp = new URLSearchParams(window.location.search);
                sp.set('tab', id);
                window.history.replaceState(null, '', `?${sp.toString()}`);
              }}
              title={tabs.find(([tid]) => tid === id)?.[2]}
            >
              {label}
            </button>
          ))}
        </nav>
        {activeHint ? <p className="tab-hint active-tab">{activeHint}</p> : null}
      </header>
      <main>
        {tab === 'players' && (
          <PlayersPage onOpenPlayer={openPlayer} onOpenPond={openPond} />
        )}
        {tab === 'player' && (
          <PlayerDetailPage
            key={`player-${jumpPlayerId}-${playerFocus ?? 'default'}`}
            initialPlayerId={jumpPlayerId}
            initialHours={deep.hours}
            initialFocus={playerFocus}
          />
        )}
        {tab === 'ponds' && (
          <PondsPage
            key={`ponds-${jumpPondId ?? ''}-${focusDebug ? 'dbg' : ''}`}
            initialPondId={jumpPondId}
            focusDebug={focusDebug}
          />
        )}
        {tab === 'health' && <BusinessHealthPage />}
      </main>
    </div>
  );
}

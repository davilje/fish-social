'use strict';

/* ================================================================
 * 猫咪钓鱼手账 · 可交互原型
 * 布局坐标与游戏一致：画布 1672×941，页面 160,150 1370×650，
 * 三栏 24,112,210,496 / 254,164,710,432 / 984,112,362,496。
 * 动效令牌与 docs/design/ui/journal/2026-10-21-cozy-journal-motion.md 第 4 节一致。
 * ================================================================ */

const U = '../../../../../fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/';
const icon = n => `${U}icons/${n}.png`;
const iconL = n => `${U}icons-light/${n}.png`;
const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));

const EASE = {
  outQuad: 'cubic-bezier(0.5, 1, 0.89, 1)',
  outCubic: 'cubic-bezier(0.33, 1, 0.68, 1)',
  outBack: 'cubic-bezier(0.34, 1.56, 0.64, 1)',
  inOutSine: 'cubic-bezier(0.37, 0, 0.63, 1)',
  inQuad: 'cubic-bezier(0.11, 0, 0.5, 0)',
};
const T = { tap: 60, snap: 220, slide: 200, hop: 420, float: 2400, stagger: 40 };

let rate = 1;
let reduced = false;
let scale = 1;

/* ---------------- motion core ---------------- */

const DONE = { finished: Promise.resolve(), cancel() {}, finish() {}, pause() {}, play() {} };
const MOTION_KEYS = ['transform', 'translate', 'scale', 'rotate'];

function play(el, frames, opts = {}) {
  if (!el) return DONE;
  const { essential, ...timing } = opts;
  let list = frames;
  if (reduced && !essential) {
    if (timing.iterations === Infinity) return DONE;
    list = frames.map(f => {
      const c = { ...f };
      MOTION_KEYS.forEach(k => delete c[k]);
      return c;
    });
    const empty = list.every(f => Object.keys(f).every(k => k === 'offset' || k === 'easing'));
    if (empty) return DONE;
    timing.duration = Math.min(timing.duration || 0, 120);
    timing.delay = 0;
  }
  const a = el.animate(list, { fill: 'backwards', ...timing });
  a.playbackRate = rate;
  return a;
}

const wait = ms => new Promise(r => setTimeout(r, ms / rate));

function setRate(r) {
  rate = r;
  document.documentElement.style.setProperty('--rate', r);
  document.getAnimations().forEach(a => { a.playbackRate = r; });
}

let captionTimer = 0;
function caption(text) {
  $('#caption-text').textContent = text;
  const tag = $('#caption .tag');
  tag.animate([{ background: '#fc923c', color: '#fff' }, { background: '#ffd7a8', color: '#1d1916' }], 600);
  clearTimeout(captionTimer);
}

function pt(el) {
  const s = $('#stage').getBoundingClientRect();
  const r = el.getBoundingClientRect();
  return {
    x: (r.left + r.width / 2 - s.left) / scale,
    y: (r.top + r.height / 2 - s.top) / scale,
    left: (r.left - s.left) / scale,
    top: (r.top - s.top) / scale,
    w: r.width / scale,
    h: r.height / scale,
  };
}

function layer(html, cls) {
  const el = document.createElement('div');
  if (cls) el.className = cls;
  el.innerHTML = html || '';
  $('#motion').appendChild(el);
  return el;
}

const fmt = n => Math.round(n).toLocaleString('en-US');

function tilt(id) {
  let h = 7;
  for (const c of id) h = (h * 31 + c.charCodeAt(0)) | 0;
  return ((Math.abs(h) % 300) / 100 - 1.5).toFixed(2);
}

/* ---------------- svg art (placeholders for 原画 sprites) ---------------- */

function bobberSvg(top = '#e2533f') {
  return `<svg viewBox="0 0 40 58"><line x1="20" y1="1" x2="20" y2="15" stroke="#75472e" stroke-width="3" stroke-linecap="round"/>
<path d="M6 30a14 14 0 0 1 28 0z" fill="${top}"/><path d="M6 30a14 14 0 0 0 28 0z" fill="#fffdf7"/>
<circle cx="20" cy="30" r="14" fill="none" stroke="#75472e" stroke-width="2.6"/>
<line x1="20" y1="44" x2="20" y2="55" stroke="#75472e" stroke-width="3" stroke-linecap="round"/>
<ellipse cx="14" cy="23" rx="4" ry="2.6" fill="#fff" opacity=".65"/></svg>`;
}

function pawSvg(color = '#75472e') {
  return `<svg viewBox="0 0 40 40"><ellipse cx="20" cy="27" rx="10" ry="8.5" fill="${color}"/>
<ellipse cx="8.5" cy="16" rx="4.2" ry="5.2" fill="${color}"/><ellipse cx="16" cy="9.5" rx="4.2" ry="5.4" fill="${color}"/>
<ellipse cx="24" cy="9.5" rx="4.2" ry="5.4" fill="${color}"/><ellipse cx="31.5" cy="16" rx="4.2" ry="5.2" fill="${color}"/></svg>`;
}

/* ---------------- data (from fish-social-unity/Assets/Resources/GameData) ---------------- */

const NAV = [
  { id: 'bag', label: '背包', icon: 'bag' },
  { id: 'equip', label: '装备', icon: 'rod' },
  { id: 'shop', label: '商店', icon: 'shop' },
  { id: 'ach', label: '成就', icon: 'medal' },
  { id: 'rank', label: '排行榜', icon: 'trophy' },
  { id: 'profile', label: '个人信息', icon: 'user' },
  { id: 'feed', label: '钓友动态', icon: 'social' },
];

const BAITS = [
  { id: 'bait-basic', name: '基础杂饵', img: 'bait-basic', cost: 0, lv: 0, desc: '什么鱼都会看一眼的家常杂饵。默认携带，用不完。', chips: ['不限量', '全鱼种'] },
  { id: 'bait-veg', name: '谷物饵', img: 'bait-wheat', cost: 15, lv: 2, desc: '麦香浓郁，草食鱼更容易咬钩。', chips: ['草食 +6%', 'Lv.2'] },
  { id: 'bait-mix', name: '腥香饵', img: 'bait-mixed', cost: 20, lv: 3, desc: '腥香混合，杂食鱼的心头好。', chips: ['杂食 +6%', 'Lv.3'] },
  { id: 'bait-meat', name: '荤腥饵', img: 'bait-shrimp', cost: 25, lv: 4, desc: '鲜虾碎肉，肉食鱼闻着就来。', chips: ['肉食 +7%', 'Lv.4'] },
];

const RODS = [
  { id: 'rod-bamboo', name: '竹制手竿', img: 'rod-bamboo', price: 0, sub: '手竿入门', bite: 0, esc: 0, brk: 0.45 },
  { id: 'rod-tai', name: '台钓碳素竿', img: 'rod-light', price: 2500, sub: '台钓', bite: 3, esc: 4, brk: 0.8 },
  { id: 'rod-stream', name: '溪流竿', img: 'rod-light', price: 3200, sub: '溪流', bite: 4, esc: 3, brk: 0.55 },
  { id: 'rod-iso', name: '矶钓竿', img: 'rod-long', price: 5500, sub: '矶钓', bite: 3, esc: 6, brk: 1.2 },
  { id: 'rod-surf', name: '海竿', img: 'rod-long', price: 4800, sub: '抛竿', bite: 2, esc: 7, brk: 1.5 },
  { id: 'rod-lure', name: '路亚竿', img: 'rod-wood', price: 9000, sub: '路亚', bite: 5, esc: 5, brk: 1.0 },
  { id: 'rod-heavy', name: '重型路亚竿', img: 'rod-wood', price: 22000, sub: '重路亚', bite: 4, esc: 10, brk: 2.5 },
  { id: 'rod-giant', name: '巨物竿', img: 'rod-long', price: 45000, sub: '巨物', bite: 3, esc: 12, brk: 4.0 },
];

const VESSELS = [
  { id: 'vessel-raft', name: '筏钓日票', img: 'boat-wood', price: 15000, lv: 12 },
  { id: 'vessel-boat', name: '路亚小艇', img: 'boat-blue', price: 35000, lv: 14 },
  { id: 'vessel-trawler', name: '捕捞船', img: 'boat-blue', price: 80000, lv: 18 },
];

const FISH = [
  { id: 'koi', name: '锦鲤', size: 0.62, stars: 5, rare: true, isNew: true },
  { id: 'golden_line', name: '金线鲃', size: 0.31, stars: 4, rare: true },
  { id: 'crucian', name: '鲫鱼', size: 0.21, stars: 2, isNew: true },
  { id: 'carp', name: '鲤鱼', size: 0.58, stars: 3 },
  { id: 'grass_carp', name: '草鱼', size: 0.86, stars: 3 },
  { id: 'black_bass', name: '大口黑鲈', size: 0.44, stars: 3 },
  { id: 'mandarin', name: '鳜鱼', size: 0.38, stars: 4 },
  { id: 'silver_carp', name: '鲢鱼', size: 0.72, stars: 2 },
  { id: 'bream', name: '鳊鱼', size: 0.29, stars: 2 },
  { id: 'catfish', name: '鲶鱼', size: 0.66, stars: 3 },
  { id: 'snakehead', name: '乌鳢', size: 0.53, stars: 3 },
  { id: 'yellow_croaker', name: '大黄鱼', size: 0.35, stars: 3 },
].map(f => ({ ...f, price: Math.round(f.size * f.stars * 60) }));

const ACH = [
  { id: 'a1', name: '初次上钩', desc: '钓到第一条鱼', icon: 'hook', state: 'done' },
  { id: 'a2', name: '锦鲤附体', desc: '钓到一条锦鲤', icon: 'star', state: 'claim', reward: 200 },
  { id: 'a3', name: '百鱼斩', desc: '累计钓到 100 条鱼', icon: 'fish', state: 'todo', progress: 0.64 },
  { id: 'a4', name: '湖泊旅人', desc: '去过 5 个湖泊', icon: 'map', state: 'claim', reward: 150 },
  { id: 'a5', name: '早起的猫', desc: '清晨钓到 10 条鱼', icon: 'sun', state: 'todo', progress: 0.3 },
  { id: 'a6', name: '满载而归', desc: '一次卖出 1000 金币', icon: 'coin', state: 'todo', progress: 0.12 },
];

const CATEGORY = [
  { id: 'wilderness', label: '野外', color: '#61B86B' },
  { id: 'reservoir', label: '水库', color: '#479EC7' },
  { id: 'advanced', label: '进阶', color: '#F2BF47' },
  { id: 'veteran', label: '老手', color: '#9E6BDB' },
  { id: 'giant', label: '巨物', color: '#B8C7DB' },
  { id: 'forbidden', label: '禁区', color: '#DB6152' },
];
const WATER = { reservoir: '水库', lake: '湖泊', river: '河流', coastal: '近海', estuary: '河口' };
const PONDS = [
  ['pond-calm', '千岛湖', '湖畔', 0.1, 0.15, 'reservoir'], ['pond-mist', '太湖', '云雾', 0.3, 0.15, 'lake'],
  ['pond-sunset', '洪泽湖', '落日', 0.5, 0.15, 'lake'], ['pond-bamboo', '鄱阳湖', '竹影', 0.7, 0.15, 'lake'],
  ['pond-reed', '洞庭湖', '芦苇', 0.9, 0.15, 'lake'], ['pond-crystal', '滇池', '晶莹', 0.1, 0.38, 'lake'],
  ['pond-lotus', '洱海', '荷塘', 0.3, 0.38, 'lake'], ['pond-mirror', '镜泊湖', '镜面', 0.5, 0.38, 'lake'],
  ['pond-willow', '查干湖', '垂柳', 0.7, 0.38, 'lake'], ['pond-stone', '万绿湖', '石滩', 0.9, 0.38, 'reservoir'],
  ['pond-spring', '北江', '春水', 0.1, 0.62, 'river'], ['pond-dusk', '丹江口水库', '黄昏', 0.3, 0.62, 'reservoir'],
  ['pond-pine', '清江', '松林', 0.5, 0.62, 'river'], ['pond-coral', '舟山近海', '珊瑚', 0.7, 0.62, 'coastal'],
  ['pond-moon', '厦门湾', '月色', 0.9, 0.62, 'estuary'], ['pond-fern', '南澳近海', '蕨影', 0.1, 0.85, 'coastal'],
  ['pond-ridge', '长江故道野塘', '山脊', 0.3, 0.85, 'river'], ['pond-harbor', '青岛近海', '港湾', 0.5, 0.85, 'coastal'],
  ['pond-orchid', '涠洲近海', '兰花', 0.7, 0.85, 'coastal'], ['pond-frost', '兴凯湖', '霜降', 0.9, 0.85, 'lake'],
].map(([id, name, theme, x, y, water], i) => ({
  id, name, theme, x, y, water,
  cat: CATEGORY[i % CATEGORY.length],
  locked: i >= 12,
  lv: i >= 12 ? 8 + (i - 12) : 1 + (i % 6),
  fee: i < 3 ? 0 : 20 + (i % 5) * 10,
}));

const S = {
  page: 'shop',
  open: true,
  tab: { shop: 1, bag: 0, equip: 0, ach: 0, settings: 0 },
  sel: { shop: 'rod-tai', bag: 'koi', equip: 'rod-bamboo', ach: 'a2', map: null },
  coins: 8600,
  owned: new Set(['rod-bamboo']),
  equipped: 'rod-bamboo',
  bait: 'bait-basic',
  fav: new Set(),
  res: '1920×1080',
  mode: 0,
  notify: [true, false, true, true, true],
  tray: true,
  feeMode: 0,
  map: { ox: -180, oy: -140, s: 1 },
};

/* ---------------- pages ---------------- */

const PAGES = {
  bag: { title: '背包', sub: '今天的收获都在这里', icon: 'bag', accent: '#FC923C', compare: 'equipment.jpg',
    tabs: [{ label: '鱼获', icon: 'fish' }, { label: '道具', icon: 'gift' }] },
  equip: { title: '装备', sub: '挑一根顺手的竿', icon: 'rod', accent: '#5DADE2', compare: 'equipment.jpg',
    tabs: [{ label: '钓竿', icon: 'rod' }, { label: '鱼饵', icon: 'leaf' }] },
  shop: { title: '商店', sub: '为下一次垂钓，备好小小惊喜', icon: 'shop', accent: '#F2BF47', compare: 'shop.jpg',
    tabs: [{ label: '鱼饵', icon: 'leaf' }, { label: '钓竿', icon: 'rod' }, { label: '船具', icon: 'pond' }] },
  ach: { title: '成就', sub: '每一枚奖章都是一个故事', icon: 'medal', accent: '#F2BF47', compare: 'achievements.jpg',
    tabs: [{ label: '全部', icon: 'medal' }, { label: '已完成', icon: 'check' }] },
  rank: { title: '排行榜', sub: '看看谁是本周的钓鱼王', icon: 'trophy', accent: '#F7B6C8', compare: 'leaderboard.jpg', empty: '排行榜正在统计，小猫先打个盹。' },
  profile: { title: '个人中心', sub: '记录属于你的钓鱼时光', icon: 'user', accent: '#FC923C', compare: 'profile.jpg', empty: '这一页在原型里留空，用来展示空状态。' },
  feed: { title: '钓友动态', sub: '钓友们最近在忙什么', icon: 'social', accent: '#6BB86B', compare: 'feed.jpg', empty: '还没有新动态，去鱼塘认识新朋友吧。' },
  map: { title: '探索世界', sub: '在地图上发现新的鱼塘', icon: 'map', accent: '#5DADE2', compare: 'worldmap.jpg' },
  settings: { title: '设置', sub: '按你的习惯调整，改动会自动保存', icon: 'settings', accent: '#C7B299', compare: 'settings.jpg',
    tabs: [{ label: '窗口显示', icon: 'settings' }, { label: '通知偏好', icon: 'bell' }, { label: '托盘与其他', icon: 'home' }] },
};

function railHtml(id) {
  const p = PAGES[id];
  const tabs = p.tabs.map((t, i) => `<div class="tab ${i === S.tab[id] ? 'sel' : ''}" data-tab="${i}" style="top:${12 + i * 88}px">
    <img class="dark" src="${icon(t.icon)}"><img class="light" src="${iconL(t.icon)}"><span>${t.label}</span></div>`).join('');
  return `<div class="rail">${tabs}${p.note ? `<div class="note">${p.note}</div>` : ''}</div>`;
}

function stickerHtml(o) {
  return `<div class="sticker ${o.sel ? 'sel' : ''} ${o.locked ? 'locked' : ''} ${o.cls || ''}" data-id="${o.id}" style="rotate:${tilt(o.id)}deg">
    <i class="tape ${o.tape || ''}"></i>${o.ribbon ? `<span class="ribbon">${o.ribbon}</span>` : ''}
    <div class="art">${o.art}</div><div class="name">${o.name}</div><div class="meta">${o.meta || ''}</div>
    ${o.glint ? '<div class="glint"><i></i></div>' : ''}${o.stamp ? `<div class="stamp small" style="right:-14px;top:-18px">${o.stamp}</div>` : ''}</div>`;
}

function emptyHtml(text, area) {
  const style = area === 'mid' ? 'left:254px;width:710px' : '';
  return `<div class="empty" style="${style}"><div class="basket"><div class="sleeper"><img src="${U}characters/avatars/orange.png"></div>
    <div class="weave"></div><span class="z">Z</span></div><div>${text}</div></div>`;
}

const coinIcon = `<img src="${icon('coin')}">`;

/* ----- shop ----- */

function shopItems() {
  const tab = S.tab.shop;
  if (tab === 0) return BAITS.map(b => ({ ...b, kind: 'bait' }));
  if (tab === 1) return RODS.map(r => ({ ...r, kind: 'rod' }));
  return VESSELS.map(v => ({ ...v, kind: 'vessel', locked: true }));
}

function shopBody() {
  const items = shopItems();
  if (!items.find(i => i.id === S.sel.shop)) S.sel.shop = items[0].id;
  const head = ['垂钓补给', '顺手的竿', '出海装备'][S.tab.shop];
  const cards = items.map(it => stickerHtml({
    id: it.id, name: it.name, sel: it.id === S.sel.shop, locked: it.locked,
    art: `<img src="assets/items/${it.img}.png">`,
    meta: it.kind === 'bait' ? (it.cost ? `${coinIcon}${it.cost} / 次` : '免费 · 不限量')
      : it.kind === 'vessel' ? `Lv.${it.lv} 解锁`
        : S.owned.has(it.id) ? '已拥有' : `${coinIcon}${fmt(it.price)}`,
    stamp: it.kind === 'bait' && it.id === S.bait ? '使用中' : '',
  })).join('');
  return `<div class="mid-h">${head}<span class="count">${items.length} 件</span></div>
    <div class="mid"><div class="grid c3">${cards}</div></div>
    <div class="aside">${shopAside()}</div>
    <div class="status">金币不够时购买按钮会摇一摇；买到的钓竿会飞进「装备」。</div>`;
}

function shopAside() {
  const it = shopItems().find(i => i.id === S.sel.shop);
  let detail;
  let button;
  if (it.kind === 'bait') {
    detail = `${it.desc}<div class="chips">${it.chips.map(c => `<span class="chip">${c}</span>`).join('')}
      <span class="chip">${coinIcon}${it.cost ? it.cost + ' / 次' : '免费'}</span></div>`;
    button = it.id === S.bait
      ? `<div class="btn" style="left:18px;top:420px;width:326px">${pawSvgImg()}正在使用</div>`
      : `<div class="btn primary" data-act="use-bait" style="left:18px;top:420px;width:326px"><span class="fish-icon">${miniFish()}</span>设为当前鱼饵</div>`;
  } else if (it.kind === 'rod') {
    detail = `${it.sub} · 咬钩 +${it.bite}% · 脱钩 -${it.esc}%<div class="chips"><span class="chip">断线 ${it.brk} m</span>
      <span class="chip">${coinIcon}${fmt(it.price)}</span></div>`;
    button = S.owned.has(it.id)
      ? `<div class="btn" style="left:18px;top:420px;width:326px">已拥有</div>`
      : `<div class="btn primary" data-act="buy" style="left:18px;top:420px;width:326px"><span class="fish-icon">${miniFish()}</span>${coinIcon}${fmt(it.price)} 购买</div>`;
  } else {
    detail = `需要等级 Lv.${it.lv}，之后可以开船去更远的海。<div class="chips"><span class="chip">${coinIcon}${fmt(it.price)}</span></div>`;
    button = `<div class="btn primary off" style="left:18px;top:420px;width:326px"><img src="${iconL('lock')}">Lv.${it.lv} 解锁</div>`;
  }
  return `<div class="heading">商品详情</div>
    <div class="polaroid"><i class="tape dots"></i><div class="photo"><img src="assets/items/${it.img}.png"></div><div class="caption">${it.name}</div></div>
    <div class="detail">${detail}</div>${button}`;
}

function miniFish() {
  return `<svg width="30" height="22" viewBox="0 0 30 22"><path d="M2 11c5-8 15-9 20-2l6-6v16l-6-6c-5 7-15 6-20-2z" fill="#fff"/><circle cx="9" cy="10" r="1.8" fill="#fc923c"/></svg>`;
}

function pawSvgImg() {
  return `<span style="width:26px;height:26px;display:inline-block">${pawSvg('#c9a27a')}</span>`;
}

/* ----- bag ----- */

function bagBody() {
  if (S.tab.bag === 1) return emptyHtml('道具袋空空的，小猫在里面睡着了。');
  if (!FISH.find(f => f.id === S.sel.bag) && FISH.length) S.sel.bag = FISH[0].id;
  const cards = FISH.map(f => stickerHtml({
    id: f.id, name: f.name, sel: f.id === S.sel.bag, ribbon: f.isNew ? '新' : '', glint: f.rare,
    art: `<img src="assets/fish/${f.id}.png">`, meta: `${f.size.toFixed(2)} m · <span class="stars">${'★'.repeat(f.stars)}</span>`,
    tape: f.rare ? 'dots' : '',
  })).join('');
  return `<div class="mid-h">鱼获<span class="count">${FISH.length} 条</span></div>
    <div class="mid"><div class="grid c4">${cards}</div></div>
    <div class="aside">${bagAside()}</div>
    <div class="status">新鱼有樱粉角标，第一次看过后角标会被撕下；稀有鱼每隔几秒闪一次光。</div>`;
}

function bagAside() {
  const f = FISH.find(x => x.id === S.sel.bag);
  if (!f) return '<div class="heading">鱼获详情</div>';
  const fav = S.fav.has(f.id);
  return `<div class="heading">鱼获详情</div>
    <div class="polaroid"><i class="tape ${f.rare ? 'dots' : ''}"></i><div class="photo"><img src="assets/fish/${f.id}.png"></div><div class="caption">${f.name}</div></div>
    <div class="detail"><span class="stars">${'★'.repeat(f.stars)}${'☆'.repeat(5 - f.stars)}</span>
      <div class="chips"><span class="chip">${f.size.toFixed(2)} m</span><span class="chip">${coinIcon}售价 ${f.price}</span>${f.rare ? '<span class="chip">稀有</span>' : ''}</div></div>
    <div class="btn primary" data-act="sell" style="left:18px;top:420px;width:204px"><span class="fish-icon">${miniFish()}</span>出售</div>
    <div class="btn" data-act="fav" style="left:234px;top:420px;width:110px"><span class="heart" style="color:${fav ? '#e2533f' : '#c9a27a'};font-size:26px">${fav ? '♥' : '♡'}</span></div>`;
}

/* ----- equip ----- */

function equipBody() {
  if (S.tab.equip === 1) {
    const cards = BAITS.map(b => stickerHtml({
      id: b.id, name: b.name, sel: b.id === S.bait, art: `<img src="assets/items/${b.img}.png">`,
      meta: b.cost ? `${coinIcon}${b.cost} / 次` : '免费 · 不限量', stamp: b.id === S.bait ? '使用中' : '',
    })).join('');
    return `<div class="mid-h">鱼饵<span class="count">点一下就换上</span></div>
      <div class="mid"><div class="grid c3">${cards}</div></div>
      <div class="aside"><div class="heading">当前鱼饵</div>${baitSummary()}</div>
      <div class="status">鱼饵按次扣金币，不需要囤货。</div>`;
  }
  const cards = RODS.map(r => stickerHtml({
    id: r.id, name: r.name, sel: r.id === S.sel.equip, locked: !S.owned.has(r.id),
    art: `<img src="assets/items/${r.img}.png">`,
    meta: S.owned.has(r.id) ? r.sub : '商店购买', stamp: r.id === S.equipped ? '使用中' : '',
  })).join('');
  return `<div class="mid-h">钓竿<span class="count">已拥有 ${S.owned.size} / ${RODS.length}</span></div>
    <div class="mid"><div class="grid c3">${cards}</div></div>
    <div class="aside">${equipAside()}</div>
    <div class="status">装备时会在卡片上盖一个「使用中」的章。</div>`;
}

function baitSummary() {
  const b = BAITS.find(x => x.id === S.bait);
  return `<div class="polaroid"><i class="tape"></i><div class="photo"><img src="assets/items/${b.img}.png"></div><div class="caption">${b.name}</div></div>
    <div class="detail">${b.desc}<div class="chips">${b.chips.map(c => `<span class="chip">${c}</span>`).join('')}</div></div>`;
}

function equipAside() {
  const r = RODS.find(x => x.id === S.sel.equip);
  const owned = S.owned.has(r.id);
  const button = !owned
    ? `<div class="btn primary" data-act="to-shop" style="left:18px;top:420px;width:326px"><img src="${iconL('shop')}">去商店看看</div>`
    : r.id === S.equipped
      ? `<div class="btn" style="left:18px;top:420px;width:326px">${pawSvgImg()}使用中</div>`
      : `<div class="btn primary" data-act="equip" style="left:18px;top:420px;width:326px"><span class="fish-icon">${miniFish()}</span>装备</div>`;
  return `<div class="heading">钓竿详情</div>
    <div class="polaroid"><i class="tape lake"></i><div class="photo"><img src="assets/items/${r.img}.png"></div><div class="caption">${r.name}</div></div>
    <div class="detail">${r.sub} · 咬钩 +${r.bite}% · 脱钩 -${r.esc}%<div class="chips"><span class="chip">断线 ${r.brk} m</span></div></div>${button}`;
}

/* ----- achievements ----- */

function achBody() {
  const list = S.tab.ach === 1 ? ACH.filter(a => a.state === 'done') : ACH;
  const cards = list.map(a => {
    const foot = a.state === 'claim'
      ? `<div class="btn primary claim" data-act="claim" data-id="${a.id}">领取 +${a.reward}</div>`
      : a.state === 'todo' ? `<div class="bar"><i style="width:${Math.round(a.progress * 100)}%"></i></div>` : '';
    return `<div class="sticker medal-card ${a.state === 'todo' ? 'todo' : ''}" data-id="${a.id}" style="rotate:${tilt(a.id)}deg">
      <i class="tape dots"></i><div class="art"><div class="rays"></div><div class="medal"><img src="${icon(a.icon)}"></div></div>
      <div class="name">${a.name}</div><div class="meta">${a.desc}</div>${foot}
      ${a.state === 'done' ? '<div class="stamp small" style="right:-12px;top:-16px">已领取</div>' : ''}</div>`;
  }).join('');
  const done = ACH.filter(a => a.state === 'done').length;
  return `<div class="mid-h">${S.tab.ach ? '已完成' : '全部成就'}<span class="count">${done} / ${ACH.length}</span></div>
    <div class="mid"><div class="grid c3">${cards}</div></div>
    <div class="aside"><div class="heading">成就进度</div>
      <div class="polaroid"><i class="tape dots"></i><div class="photo"><img src="${icon('trophy')}" style="max-height:120px"></div><div class="caption">${done} / ${ACH.length}</div></div>
      <div class="detail">领取奖励时，奖章会翻一圈亮起来，金币飞进右上角。</div></div>
    <div class="status">进度条在页面打开时从 0 长到当前进度。</div>`;
}

/* ----- map ----- */

function mapBody() {
  const legend = CATEGORY.map(c => `<div><i style="background:${c.color}"></i>${c.label}</div>`).join('');
  const buoys = PONDS.map(p => `<div class="buoy ${p.locked ? 'locked' : ''}" data-id="${p.id}" style="left:${p.x * 1050}px;top:${p.y * 700}px">
      <div class="body">${bobberSvg(p.locked ? '#9aa0a6' : p.cat.color)}</div><span class="label">${p.locked ? '🔒 ' : ''}${p.name}</span></div>`).join('');
  return `<div class="rail"><div class="legend">${legend}<div><i style="background:#61666B"></i>未解锁</div></div>
      <div class="note">拖动地图浏览<br>点浮标查看鱼塘</div></div>
    <div class="mid-h">世界地图<span class="count">${PONDS.length} 个鱼塘</span></div>
    <div class="map-frame"><i class="tape lake" style="opacity:.95;left:-30px;top:-6px"></i>
      <i class="tape lake" style="opacity:.95;left:auto;right:-30px;top:auto;bottom:-6px;rotate:-28deg"></i>
      <div class="map-view"><div class="map-content"><img src="assets/world-map.jpg">${buoys}</div></div></div>
    <div class="aside">${mapAside()}</div>
    <div class="status" id="map-status">点选地图上的鱼塘。</div>`;
}

function mapAside() {
  const p = PONDS.find(x => x.id === S.sel.map);
  if (!p) return `<div class="heading">鱼塘详情</div><div class="polaroid" style="opacity:.55"><div class="photo"><img class="cover" src="assets/pond-afternoon.jpg"></div><div class="caption">还没选鱼塘</div></div><div class="detail">点地图上的浮标，小猫会帮你把照片贴到这里。</div>`;
    return `<div class="heading">鱼塘详情</div>
    <div class="polaroid pond-photo"><i class="tape lake"></i><div class="photo"><img class="cover" src="assets/pond-afternoon.jpg"></div><div class="caption">${p.name}</div></div>
    <div class="detail pond-info"><div class="pond-description">${p.theme} · ${WATER[p.water]} · <span style="color:${p.cat.color}">●</span> ${p.cat.label}</div>
      <div class="pond-species">可遇鱼种：鲫鱼 · 鲤鱼 · 草鱼</div>
      <div class="chips"><span class="chip">Lv.${p.lv}</span><span class="chip">${coinIcon}${p.fee ? p.fee + ' 金币入场' : '免费入场'}</span></div></div>
    <div class="pond-entry-controls"><div class="seg pond-fee"><span class="knob" style="left:${S.feeMode ? 165 : 4}px;width:157px"></span>
      <button class="${S.feeMode ? '' : 'on'}" data-fee="0">回鱼出售</button><button class="${S.feeMode ? 'on' : ''}" data-fee="1">自动回鱼</button></div>
    ${p.locked
      ? `<button class="btn primary off pond-enter" disabled><img src="${iconL('lock')}">Lv.${p.lv} 解锁</button>`
      : `<button class="btn primary pond-enter" data-act="enter"><span class="fish-icon">${miniFish()}</span>进入鱼塘</button>`}</div>`;
}

/* ----- settings ----- */

const PRESETS = ['1280×720', '1600×900', '1920×1080', '2560×1440'];
const MODES = ['窗口', '无边框', '全屏'];
const NOTIFY = [['开启通知', '所有提醒的总开关'], ['免打扰', '夜里不弹提示'], ['咬钩提醒', '挂机时有鱼咬钩'], ['好友邀请', '钓友叫你一起'], ['连接异常', '断线时提醒']];

function settingsBody() {
  let mid = '';
  let head = '';
  if (S.tab.settings === 0) {
    head = '窗口尺寸';
    mid = PRESETS.map((r, i) => `<div class="btn preset" data-res="${r}" style="left:${18 + (i % 2) * 349}px;top:${18 + Math.floor(i / 2) * 78}px;width:325px">
        ${r}${r === S.res ? '<span class="check-stamp">✓</span>' : ''}</div>`).join('')
      + `<div style="position:absolute;left:22px;top:186px;font-size:20px">显示模式</div>
      <div class="seg" style="left:18px;top:222px;width:674px"><span class="knob" style="left:${4 + S.mode * 222}px;width:222px"></span>
        ${MODES.map((m, i) => `<button class="${i === S.mode ? 'on' : ''}" data-mode="${i}">${m}</button>`).join('')}</div>
      <div style="position:absolute;left:22px;top:300px;font-size:16px;color:var(--ink-soft)">切换后立即生效并保存，不需要再点保存。</div>`;
  } else if (S.tab.settings === 1) {
    head = '提醒开关';
    mid = NOTIFY.map(([t, d], i) => `<div class="setting-row" style="top:${14 + i * 78}px">${t}<span class="desc">${d}</span>
      <div class="switch ${S.notify[i] ? 'on' : ''}" data-sw="${i}"><span class="paw-push">${pawSvg('#fff3df')}</span><span class="knob" style="translate:${S.notify[i] ? 36 : 0}px 0"></span></div></div>`).join('');
  } else {
    head = '系统托盘';
    mid = `<div class="setting-row" style="top:14px">关闭窗口时隐藏到托盘<div class="switch ${S.tray ? 'on' : ''}" data-sw="tray"><span class="paw-push">${pawSvg('#fff3df')}</span><span class="knob" style="translate:${S.tray ? 36 : 0}px 0"></span></div></div>
      <div class="btn" style="left:18px;top:104px;width:325px">立即隐藏到托盘</div>
      <div class="btn" style="left:367px;top:104px;width:325px">重置新手引导</div>
      <div class="btn" style="left:18px;top:184px;width:325px">退出游戏</div>`;
  }
  const on = S.notify.filter(Boolean).length;
  return `<div class="mid-h">${head}</div><div class="mid">${mid}</div>
    <div class="aside"><div class="heading">当前设置</div>
      <div style="position:absolute;left:24px;top:70px;font-size:40px">${S.res}</div>
      <div style="position:absolute;left:26px;top:126px;font-size:20px;color:var(--ink-soft)">${MODES[S.mode]} · 提醒 ${on} 项</div>
      <div style="position:absolute;left:26px;top:180px;right:24px;font-size:16px;line-height:1.7;color:var(--ink-soft)">所有改动都会自动保存。<br>开关由猫爪拨动，选中的尺寸会盖一个小勾章。</div>
      <span style="position:absolute;right:30px;bottom:30px;width:90px;height:90px;opacity:.12;rotate:-16deg">${pawSvg()}</span></div>`;
}

function emptyBody(id) {
  return emptyHtml(PAGES[id].empty);
}

const BODY = { shop: shopBody, bag: bagBody, equip: equipBody, ach: achBody, map: mapBody, settings: settingsBody, rank: emptyBody, profile: emptyBody, feed: emptyBody };

function pageHtml(id) {
  const p = PAGES[id];
  const rail = p.tabs ? railHtml(id) : id === 'map' ? '' : `<div class="rail"><div class="note">原型只做了几个代表页。这里用来展示空状态：小猫在鱼篓里睡觉，呼吸起伏。</div></div>`;
  return rail + BODY[id](id);
}

/* ---------------- shell: header, render, flip ---------------- */

function applyHeader(id) {
  const p = PAGES[id];
  const page = $('#page');
  page.style.setProperty('--accent', p.accent);
  $('.page-header .title-icon').src = icon(p.icon);
  const h1 = $('.page-header h1');
  h1.textContent = p.title;
  $('.page-header .sub').textContent = p.sub;
  const paw = $('.page-header .paw-stamp');
  paw.innerHTML = pawSvg(p.accent);
  paw.style.left = `${h1.offsetLeft + h1.offsetWidth + 6}px`;
  $('#compare').src = `assets/current/${p.compare}`;
}

let pageLoops = [];

function render(id) {
  const content = $('.content');
  content.innerHTML = pageHtml(id);
  bindPage(id, content);
  startPageLoops(id, content);
}

function entrance(root, { rail = true } = {}) {
  if (rail) {
    $$('.rail .tab, .rail .legend > div', root).forEach((t, i) => {
      play(t, [{ opacity: 0, translate: '-18px 0' }, { opacity: 1, translate: t.classList.contains('sel') ? '-16px 0' : '0 0' }],
        { duration: T.slide, delay: i * 30, easing: EASE.outCubic });
    });
  }
  play($('.mid-h', root), [{ opacity: 0 }, { opacity: 1 }], { duration: 160 });
  stagger(root);
  const aside = $('.aside', root);
  play(aside, [{ opacity: 0 }, { opacity: 1 }], { duration: 160, delay: 80 + Math.min(8, $$('.sticker', root).length) * 20 });
  const frame = $('.map-frame', root);
  if (frame) play(frame, [{ opacity: 0, scale: '1.06', rotate: '3deg' }, { opacity: 1, scale: '1', rotate: '-0.6deg' }], { duration: 260, easing: EASE.outBack });
  $$('.bar i', root).forEach(b => play(b, [{ width: '0%' }, { width: b.style.width }], { duration: 500, delay: 200, easing: EASE.outCubic }));
  const sleeper = $('.basket', root);
  if (sleeper) play(sleeper, [{ opacity: 0, translate: '0 12px' }, { opacity: 1, translate: '0 0' }], { duration: 260, easing: EASE.outCubic });
}

function stagger(root) {
  $$('.sticker', root).forEach((c, i) => {
    if (i >= 8) return;
    const t = parseFloat(c.style.rotate) || 0;
    play(c, [{ opacity: 0, scale: '1.12', rotate: `${t + 6}deg` }, { opacity: 1, scale: '1', rotate: `${t}deg` }],
      { duration: T.snap + 40, delay: i * T.stagger, easing: EASE.outBack });
  });
}

let switching = false;

async function go(id, { tab } = {}) {
  if (switching) return;
  if (tab !== undefined) S.tab[id] = tab;
  if (S.open && id === S.page && tab === undefined) return;
  switching = true;
  wake();
  const navIdx = NAV.findIndex(n => n.id === id);
  moveBobber(navIdx < 0 ? null : navIdx);
  $$('.navtab').forEach((n, i) => n.classList.toggle('on', i === navIdx));
  const content = $('.content');
  if (!S.open) {
    // Keep the contents covered until their entrance animations are installed.
    // Otherwise the paper reveals the finished layout, then entrance hides it again.
    content.style.visibility = 'hidden';
    S.page = id;
    S.open = true;
    applyHeader(id);
    render(id);
    caption('打开页面：纸页从下方 16px 升起 200ms OutCubic → 活页环扣上 80ms → 卡片依次贴入');
    $('#page').style.visibility = 'visible';
    stopCatBreath();
    await play($('#page'), [{ opacity: 0, translate: '0 16px' }, { opacity: 1, translate: '0 0' }], { duration: T.slide, easing: EASE.outCubic }).finished;
    $$('.ring').forEach((r, i) => play(r, [{ scale: '1 0.6' }, { scale: '1 1' }], { duration: 80, delay: i * 30, easing: EASE.outBack }));
    entrance(content);
    content.style.visibility = '';
    switching = false;
    return;
  }
  caption('翻 Flip：旧页 120ms 左移 24px 淡出 → 新页 200ms OutCubic 右侧滑入 → 卡片 Stagger 40ms 贴入（OutBack）→ 右栏最后淡入');
  await play(content, [{ opacity: 1, translate: '0 0' }, { opacity: 0, translate: '-24px 0' }], { duration: 120, easing: EASE.inQuad, fill: 'forwards' }).finished;
  content.getAnimations().forEach(a => a.cancel());
  S.page = id;
  applyHeader(id);
  play($('.page-header h1'), [{ opacity: 0, translate: '10px 0' }, { opacity: 1, translate: '0 0' }], { duration: T.slide, easing: EASE.outCubic });
  render(id);
  play(content, [{ opacity: 0, translate: '24px 0' }, { opacity: 1, translate: '0 0' }], { duration: T.slide, easing: EASE.outCubic });
  entrance(content, { rail: false });
  switching = false;
}

async function closePage() {
  if (!S.open || switching) return;
  switching = true;
  caption('关闭页面：下沉 12px 淡出 140ms；浮漂沉入水里；没有打开的页面时角落小猫开始呼吸');
  await play($('#page'), [{ opacity: 1, translate: '0 0' }, { opacity: 0, translate: '0 12px' }], { duration: 140, easing: EASE.inQuad, fill: 'forwards' }).finished;
  $('#page').getAnimations().forEach(a => a.cancel());
  $('#page').style.visibility = 'hidden';
  stopPageLoops();
  S.open = false;
  moveBobber(null);
  $$('.navtab').forEach(n => n.classList.remove('on'));
  $('#compare').src = 'assets/current/shop.jpg';
  startCatBreath();
  switching = false;
}

let tabGeneration = 0;
async function switchTab(i) {
  if (switching) return;
  const id = S.page;
  if (S.tab[id] === i) return;
  const generation = ++tabGeneration;
  S.tab[id] = i;
  $$('.rail .tab').forEach((t, k) => t.classList.toggle('sel', k === i));
  caption('索引贴：选中的页签从纸边多拉出 16px（200ms OutCubic）；中栏旧卡 100ms 淡出，新卡依次贴入');
  const mid = $$('.mid, .mid-h, .aside, .status, .empty', $('.content'));
  await Promise.all(mid.map(m => play(m, [{ opacity: 1 }, { opacity: 0 }], { duration: 100, fill: 'forwards' }).finished));
  const content = $('.content');
  if (id !== S.page || generation !== tabGeneration) return;
  const rail = $('.rail', content);
  const tmp = document.createElement('div');
  tmp.innerHTML = BODY[id](id);
  $$('.mid, .mid-h, .aside, .status, .empty', content).forEach(n => n.remove());
  Array.from(tmp.children).forEach(n => content.appendChild(n));
  bindPage(id, content);
  stopPageLoops();
  startPageLoops(id, content);
  entrance(content, { rail: false });
  void rail;
}

/* ---------------- bobber nav ---------------- */

let bobIdx = 2;
let bobFloat = DONE;
let bobAnims = [];

function tabCenter(i) {
  const tab = $$('.navtab')[i];
  return tab.offsetLeft + tab.offsetWidth / 2 - 20;
}

function startBobFloat() {
  bobFloat.cancel();
  bobFloat = play($('#bobber .float'), [{ translate: '0 -3px', rotate: '-4deg' }, { translate: '0 3px', rotate: '4deg' }],
    { duration: T.float / 2, iterations: Infinity, direction: 'alternate', easing: EASE.inOutSine });
  if (pageLoops.length || reduced || document.hidden) bobFloat.pause();
}

function navRipple(i) {
  const nav = pt($('#nav'));
  const x = nav.left + tabCenter(i) + 20;
  ripple(x, nav.top + 2, { count: 1, w: 70, h: 22 });
}

async function moveBobber(idx) {
  try {
  const bob = $('#bobber');
  bobAnims.forEach(a => a.cancel());
  bobAnims = [];
  const cur = getComputedStyle(bob).translate.split(' ').map(v => parseFloat(v) || 0);
  const x0 = cur[0] || 0;
  if (idx === bobIdx && bob.style.opacity !== '0') return;
  if (idx === null) {
    bobIdx = null;
    bobFloat.pause();
    const a = play(bob, [{ translate: `${x0}px 0`, opacity: 1 }, { translate: `${x0}px 18px`, opacity: 0 }], { duration: 260, easing: EASE.inQuad, fill: 'forwards' });
    bobAnims.push(a);
    ripple(pt($('#nav')).left + x0 + 20, pt($('#nav')).top + 2, { count: 2, w: 70, h: 22 });
    await a.finished;
    bob.style.translate = `${x0}px 18px`;
    bob.style.opacity = '0';
    a.cancel();
    return;
  }
  const x1 = tabCenter(idx);
  if (bobIdx === null) {
    bobIdx = idx;
    bob.style.translate = `${x1}px 0`;
    bob.style.opacity = '1';
    const a = play(bob, [{ translate: `${x1}px 18px`, opacity: 0 }, { translate: `${x1}px -8px`, opacity: 1, offset: 0.7 }, { translate: `${x1}px 0` }],
      { duration: 320, easing: EASE.outCubic });
    bobAnims.push(a);
    navRipple(idx);
    await a.finished;
    startBobFloat();
    return;
  }
  bobIdx = idx;
  bobFloat.pause();
  caption('漂 + Hop：浮漂下蹲 60ms → 抛物线跳到新页签 360ms（最高 46px，带拖尾点）→ 落水拉伸回弹 → 水波 360ms → 图标上弹');
  const crouch = play(bob, [{ scale: '1 1' }, { scale: '1.14 0.84' }], { duration: T.tap, easing: EASE.outQuad, fill: 'forwards' });
  bobAnims.push(crouch);
  await crouch.finished;
  const dx = x1 - x0;
  const dir = Math.sign(dx) || 1;
  const N = 16;
  const frames = [];
  for (let k = 0; k <= N; k++) {
    const t = k / N;
    const ex = 0.5 - Math.cos(Math.PI * t) / 2;
    const y = -46 * 4 * t * (1 - t);
    const sc = t < 0.2 ? '0.9 1.14' : t > 0.85 ? '0.94 1.08' : '1 1';
    frames.push({ translate: `${x0 + dx * ex}px ${y}px`, rotate: `${dir * 16 * (1 - 2 * t)}deg`, scale: sc });
  }
  crouch.cancel();
  const hop = play(bob, frames, { duration: T.hop - 60, easing: 'linear', fill: 'forwards' });
  bobAnims.push(hop);
  const nav = pt($('#nav'));
  [0.25, 0.5, 0.75].forEach(t => setTimeout(() => {
    const ex = 0.5 - Math.cos(Math.PI * t) / 2;
    const d = layer('', 'dot-trail');
    d.style.left = `${nav.left + x0 + dx * ex + 20}px`;
    d.style.top = `${nav.top - 60 + -46 * 4 * t * (1 - t) + 28}px`;
    play(d, [{ opacity: 0.9, scale: '1' }, { opacity: 0, scale: '0.4' }], { duration: 320, fill: 'forwards' }).finished.then(() => d.remove());
  }, (T.hop - 60) * t / rate));
  await hop.finished;
  if (bobIdx !== idx) return;
  bob.style.translate = `${x1}px 0`;
  hop.cancel();
  const land = play(bob, [{ scale: '1.16 0.84' }, { scale: '0.95 1.07', offset: 0.5 }, { scale: '1 1' }], { duration: 200, easing: EASE.outCubic });
  bobAnims.push(land);
  navRipple(idx);
  const img = $$('.navtab img')[idx];
  play(img, [{ translate: '0 0' }, { translate: '0 -8px', offset: 0.4 }, { translate: '0 0' }], { duration: 300, easing: EASE.outCubic });
  await land.finished;
  if (bobIdx === idx && !pageLoops.length && !reduced) bobFloat.play();
  } catch (error) { if (error.name !== 'AbortError') throw error; }
}

/* ---------------- effects ---------------- */

function ripple(x, y, { count = 2, w = 90, h = 34, color } = {}) {
  for (let i = 0; i < count; i++) {
    const r = layer('', 'ripple');
    r.style.left = `${x}px`;
    r.style.top = `${y}px`;
    r.style.width = `${w}px`;
    r.style.height = `${h}px`;
    r.style.margin = `${-h / 2}px 0 0 ${-w / 2}px`;
    if (color) r.style.borderColor = color;
    play(r, [{ scale: '0.4', opacity: 0.9 }, { scale: '1.5', opacity: 0 }], { duration: 360, delay: i * 120, easing: EASE.outCubic, fill: 'both', essential: true })
      .finished.then(() => r.remove());
  }
}

function press(el) {
  if (el.classList.contains('off')) {
    caption('不可用：水平抖动 ±4px 两次（180ms），不缩放');
    play(el, [{ translate: '0 0' }, { translate: '-4px 0' }, { translate: '4px 0' }, { translate: '-3px 0' }, { translate: '3px 0' }, { translate: '0 0' }],
      { duration: 180, easing: 'linear' });
    return;
  }
  el._press?.cancel();
  const light = el.classList.contains('sticker') || el.classList.contains('buoy');
  el._press = play(el, [{ scale: '1 1' }, { scale: light ? '1.02 0.96' : '1.06 0.92' }], { duration: T.tap, easing: EASE.outQuad, fill: 'forwards' });
}

function release(el) {
  if (!el._press) return;
  el._press.cancel();
  el._press = null;
  const light = el.classList.contains('sticker') || el.classList.contains('buoy');
  play(el, [{ scale: light ? '1.02 0.96' : '1.06 0.92' }, { scale: light ? '0.99 1.02' : '0.98 1.04', offset: 0.5 }, { scale: '1 1' }],
    { duration: T.snap, easing: EASE.outCubic });
  if (!light && !el.classList.contains('navtab') && !el.classList.contains('tab')) {
    caption('弹 Press：按下 60ms 压扁到 1.06×0.92（OutQuad）→ 松开 220ms 回弹 0.98×1.04 → 1');
  }
}

let coinShown = S.coins;
let coinRaf = 0;

function rollCoins(delta, from) {
  const start = coinShown;
  const end = S.coins;
  const t0 = performance.now();
  cancelAnimationFrame(coinRaf);
  const num = $('#coins .num');
  const step = now => {
    const t = Math.min(1, ((now - t0) * rate) / 400);
    const e = 1 - Math.pow(1 - t, 3);
    coinShown = start + (end - start) * e;
    num.textContent = fmt(coinShown);
    if (t < 1) coinRaf = requestAnimationFrame(step);
  };
  coinRaf = requestAnimationFrame(step);
  play($('#coins img'), [{ scale: '1.35' }, { scale: '1' }], { duration: T.snap + 80, easing: EASE.outBack, essential: true });
  const p = pt($('#coins'));
  const f = layer(`${delta > 0 ? '+' : ''}${fmt(delta)}`, `float-text ${delta < 0 ? 'minus' : ''}`);
  f.style.left = `${p.left + 70}px`;
  f.style.top = `${p.top + 62}px`;
  play(f, [{ opacity: 0, translate: '0 6px' }, { opacity: 1, translate: '0 0', offset: 0.2 }, { opacity: 0, translate: '0 -20px' }],
    { duration: 900, easing: EASE.outCubic, fill: 'forwards', essential: true }).finished.then(() => f.remove());
  void from;
}

async function flyTo(srcEl, dstEl, src, { size = 110, dur = 620 } = {}) {
  const a = pt(srcEl);
  const b = pt(dstEl);
  const img = document.createElement('img');
  img.className = 'flyer';
  img.src = src;
  img.style.width = img.style.height = `${size}px`;
  img.style.margin = `${-size / 2}px`;
  $('#motion').appendChild(img);
  const frames = [];
  const N = 14;
  const cx = (a.x + b.x) / 2;
  const cy = Math.min(a.y, b.y) - 180;
  for (let k = 0; k <= N; k++) {
    const t = k / N;
    const x = (1 - t) * (1 - t) * a.x + 2 * (1 - t) * t * cx + t * t * b.x;
    const y = (1 - t) * (1 - t) * a.y + 2 * (1 - t) * t * cy + t * t * b.y;
    frames.push({ left: `${x}px`, top: `${y}px`, scale: `${1 - 0.65 * t}`, rotate: `${-25 * t}deg`, opacity: t > 0.92 ? 0.6 : 1 });
  }
  await play(img, frames, { duration: dur, easing: 'cubic-bezier(0.45, 0, 0.25, 1)', fill: 'forwards', essential: true }).finished;
  img.remove();
}

async function coinsFly(srcEl, n = 6) {
  const a = pt(srcEl);
  const b = pt($('#coins img'));
  const jobs = [];
  for (let i = 0; i < n; i++) {
    const c = document.createElement('img');
    c.className = 'coin-fly';
    c.src = icon('coin');
    $('#motion').appendChild(c);
    const sx = a.x + (Math.random() - 0.5) * 80;
    const sy = a.y + (Math.random() - 0.5) * 40;
    const cx = (sx + b.x) / 2 + (Math.random() - 0.5) * 120;
    const cy = Math.min(sy, b.y) - 120;
    const frames = [];
    for (let k = 0; k <= 10; k++) {
      const t = k / 10;
      frames.push({
        left: `${(1 - t) * (1 - t) * sx + 2 * (1 - t) * t * cx + t * t * b.x}px`,
        top: `${(1 - t) * (1 - t) * sy + 2 * (1 - t) * t * cy + t * t * b.y}px`,
        scale: `${t < 0.15 ? 0.4 + t * 4 : 1 - t * 0.3}`,
      });
    }
    jobs.push(play(c, frames, { duration: 520, delay: i * 45, easing: 'cubic-bezier(0.45, 0, 0.3, 1)', fill: 'both', essential: true })
      .finished.then(() => c.remove()));
  }
  await Promise.all(jobs);
}

const toastQueue = [];
let toastBusy = false;

function toast(text, kind = 'ok') {
  toastQueue.push({ text, kind });
  if (!toastBusy) nextToast();
}

async function nextToast() {
  const job = toastQueue.shift();
  if (!job) { toastBusy = false; return; }
  toastBusy = true;
  const el = layer(job.kind === 'error'
    ? `<div class="note">${job.text}</div><div class="cat"><img src="${U}characters/avatars/orange.png"></div>`
    : `<div class="note">${job.text}</div><img class="fish" src="assets/fish/crucian.png">`, `toast ${job.kind}`);
  caption(job.kind === 'error'
    ? '错误提示：小猫歪头举着便签（OutBack），便签描边改为浮漂红'
    : '提示：小鱼叼着便签从右侧游进来 420ms OutCubic，摆尾，停 2.5 秒后继续游走');
  const swimIn = play(el, [{ translate: '460px 0', opacity: 0 }, { translate: '0 0', opacity: 1 }], { duration: 420, easing: EASE.outCubic });
  const fish = $('.fish', el);
  const wag = fish ? play(fish, [{ rotate: '-5deg' }, { rotate: '5deg' }], { duration: 240, iterations: 12, direction: 'alternate', easing: EASE.inOutSine }) : DONE;
  const cat = $('.cat', el);
  if (cat) play(cat, [{ rotate: '0deg' }, { rotate: '14deg' }], { duration: 300, delay: 300, easing: EASE.outBack, fill: 'forwards' });
  await swimIn.finished;
  await wait(2500);
  await play(el, [{ translate: '0 0', opacity: 1 }, { translate: '-140px -8px', opacity: 0 }], { duration: 320, easing: EASE.inQuad, fill: 'forwards' }).finished;
  wag.cancel();
  el.remove();
  nextToast();
}

/* ---------------- corner cat ---------------- */

const cat = () => $('#corner-cat');
let catBreath = DONE;
let lastActive = performance.now();
let sleeping = false;
let zzzLoop = DONE;

function startCatBreath() {
  catBreath.cancel();
  const target = window.roomCompanion?.active ? $('.room-cat-art', cat()) : cat();
  catBreath = play(target, [{ scale: '1 1' }, { scale: '1 1.025' }], { duration: 1500, iterations: Infinity, direction: 'alternate', easing: EASE.inOutSine });
}

function stopCatBreath() { catBreath.cancel(); catBreath = DONE; }

function catNod() {
  play(cat(), [{ rotate: '0deg' }, { rotate: '-6deg' }, { rotate: '0deg' }, { rotate: '-6deg' }, { rotate: '0deg' }], { duration: 600, easing: EASE.inOutSine });
}

function catJump() {
  play(cat(), [
    { translate: '0 0', scale: '1 1' },
    { translate: '0 0', scale: '1.08 0.9', offset: 0.12 },
    { translate: '0 -64px', scale: '0.94 1.08', offset: 0.5, easing: EASE.inQuad },
    { translate: '0 0', scale: '1.1 0.88', offset: 0.85 },
    { translate: '0 0', scale: '1 1' },
  ], { duration: 640, easing: EASE.outQuad });
}

function catLean(on) {
  cat()._lean?.cancel();
  cat()._lean = play(cat(), [{ rotate: on ? '0deg' : '-5deg' }, { rotate: on ? '-5deg' : '0deg' }], { duration: T.snap, easing: EASE.outBack, fill: 'forwards' });
}

function sleep() {
  if (window.roomCompanion?.active && S.open) return;
  if (sleeping) return;
  sleeping = true;
  if (window.roomCompanion?.active) {
    window.roomCompanion.rest();
    return;
  }
  caption('30 秒没有操作：角落小猫趴下睡觉，Zzz 依次飘起；任何操作都会叫醒它');
  cat()._sleep = play(cat(), [{ rotate: '0deg', translate: '0 0' }, { rotate: '7deg', translate: '6px 10px' }], { duration: 600, easing: EASE.inOutSine, fill: 'forwards' });
  zzzLoop = play($('#zzz'), [{ opacity: 0, translate: '0 10px' }, { opacity: 1, translate: '6px -6px', offset: 0.4 }, { opacity: 0, translate: '14px -30px' }],
    { duration: 2000, iterations: Infinity, easing: 'linear' });
}

function wake() {
  lastActive = performance.now();
  if (!sleeping) return;
  sleeping = false;
  zzzLoop.cancel();
  cat()._sleep?.cancel();
  if (window.roomCompanion?.active) {
    window.roomCompanion.wake();
    return;
  }
  play(cat(), [{ translate: '0 0' }, { translate: '0 -18px', offset: 0.4 }, { translate: '0 0' }], { duration: 320, easing: EASE.outCubic });
}

/* ---------------- page loops (one loop per screen) ---------------- */

function startPageLoops(id, root) {
  stopPageLoops();
  const buoys = $$('.buoy:not(.locked) .body', root);
  buoys.forEach((b, i) => {
    pageLoops.push(play(b, [{ translate: '0 -3px', rotate: '-4deg' }, { translate: '0 3px', rotate: '4deg' }],
      { duration: T.float / 2, iterations: Infinity, direction: 'alternate', easing: EASE.inOutSine, delay: -((i * 397) % 1200) }));
  });
  const sleeper = $('.sleeper', root);
  if (sleeper) {
    pageLoops.push(play(sleeper, [{ scale: '1 1' }, { scale: '1.02 0.97' }], { duration: 1500, iterations: Infinity, direction: 'alternate', easing: EASE.inOutSine }));
    pageLoops.push(play($('.basket .z', root), [{ opacity: 0, translate: '0 10px', scale: '0.7' }, { opacity: 1, translate: '8px -10px', scale: '1', offset: 0.45 }, { opacity: 0, translate: '18px -40px', scale: '1.2' }],
      { duration: 2200, iterations: Infinity, easing: 'linear' }));
  }
  if (pageLoops.length) bobFloat.pause();
  else if (bobIdx !== null && !reduced) bobFloat.play();
}

function stopPageLoops() {
  pageLoops.forEach(a => a.cancel());
  pageLoops = [];
  if (bobIdx !== null && !reduced) bobFloat.play();
}

setInterval(() => {
  if (!S.open || reduced) return;
  const shiny = $$('.content .glint i').slice(0, 2);
  shiny.forEach((g, i) => play(g, [{ translate: '0 0' }, { translate: '520px 0' }], { duration: 500, delay: i * 180, easing: EASE.inOutSine }));
}, 6000);

/* ---------------- page bindings ---------------- */

function bindPage(id, root) {
  $$('.rail .tab', root).forEach(t => { t.onclick = () => switchTab(+t.dataset.tab); });
  $$('.sticker', root).forEach(c => c.addEventListener('click', e => {
    if (e.target.closest('[data-act]')) return;
    selectCard(id, c);
  }));
  $$('[data-act]', root).forEach(b => b.addEventListener('click', () => act(b.dataset.act, b)));
  if (id === 'map') bindMap(root);
  if (id === 'settings') bindSettings(root);
}

function selectCard(id, card) {
  if (id === 'ach') return;
  const key = card.dataset.id;
  if (id === 'equip' && S.tab.equip === 1) {
    if (S.bait === key) return;
    S.bait = key;
    stampOn(card, '使用中');
    $$('.sticker .stamp', card.parentElement).forEach(s => { if (!card.contains(s)) s.remove(); });
    const aside = $('.aside');
    aside.innerHTML = `<div class="heading">当前鱼饵</div>${baitSummary()}`;
    play($('.polaroid', aside), [{ scale: '0.9', opacity: 0.4 }, { scale: '1', opacity: 1 }], { duration: T.snap, easing: EASE.outBack });
    toast(`已换上 ${BAITS.find(b => b.id === key).name}`);
    return;
  }
  if (S.sel[id] === key) return;
  S.sel[id] = key;
  $$('.sticker', card.parentElement).forEach(c => c.classList.toggle('sel', c === card));
  caption('贴 Sticker 选中：胶带从左往右拉出 160ms OutCubic；卡片抬起 6px；右栏拍立得 OutBack 弹入，文字直接替换');
  play($('.tape', card), [{ scale: '0 1' }, { scale: '1 1' }], { duration: 160, easing: EASE.outCubic });
  const ribbon = $('.ribbon', card);
  if (ribbon && id === 'bag') {
    const f = FISH.find(x => x.id === key);
    if (f) f.isNew = false;
    play(ribbon, [{ rotate: '0deg', translate: '0 0', opacity: 1 }, { rotate: '28deg', translate: '14px -12px', opacity: 0 }],
      { duration: 300, delay: 200, easing: EASE.inQuad, fill: 'forwards' }).finished.then(() => ribbon.remove());
  }
  refreshAside(id);
}

function refreshAside(id) {
  const aside = $('.aside');
  const html = id === 'shop' ? shopAside() : id === 'bag' ? bagAside() : id === 'equip' ? equipAside() : id === 'map' ? mapAside() : null;
  if (html === null) return;
  aside.innerHTML = html;
  $$('[data-act]', aside).forEach(b => b.addEventListener('click', () => act(b.dataset.act, b)));
  if (id === 'map') bindFee(aside);
  play($('.polaroid', aside), [{ scale: '0.9', opacity: 0.3, translate: '24px 0' }, { scale: '1', opacity: 1, translate: '0 0' }], { duration: T.snap + 40, easing: EASE.outBack });
}

function stampOn(card, text) {
  const s = document.createElement('div');
  s.className = 'stamp small';
  s.style.right = '-14px';
  s.style.top = '-18px';
  s.textContent = text;
  card.appendChild(s);
  caption('盖章 Stamp：从 1.6 倍砸下 200ms OutBack，页面容器轻震 2px');
  play(s, [{ opacity: 0, scale: '1.7', rotate: '-40deg' }, { opacity: 1, scale: '1', rotate: '-14deg' }], { duration: 200, easing: EASE.outBack });
  setTimeout(() => play($('.content'), [{ translate: '0 0' }, { translate: '2px 1px' }, { translate: '-2px 0' }, { translate: '1px -1px' }, { translate: '0 0' }], { duration: 160 }), 170 / rate);
}

async function act(name, el) {
  wake();
  if (name === 'buy') return buy(el);
  if (name === 'use-bait') {
    S.bait = S.sel.shop;
    const card = $(`.sticker[data-id="${S.bait}"]`);
    $$('.content .sticker .stamp').forEach(s => s.remove());
    if (card) stampOn(card, '使用中');
    refreshAside('shop');
    catNod();
    toast(`已换上 ${BAITS.find(b => b.id === S.bait).name}`);
    return;
  }
  if (name === 'sell') return sell(el);
  if (name === 'fav') {
    const f = S.sel.bag;
    if (S.fav.has(f)) S.fav.delete(f); else S.fav.add(f);
    const heart = $('.heart', el);
    heart.textContent = S.fav.has(f) ? '♥' : '♡';
    heart.style.color = S.fav.has(f) ? '#e2533f' : '#c9a27a';
    caption('收藏：爱心放大到 1.4 倍再 OutBack 回弹');
    play(heart, [{ scale: '1.5' }, { scale: '1' }], { duration: 280, easing: EASE.outBack });
    return;
  }
  if (name === 'equip') {
    S.equipped = S.sel.equip;
    $$('.content .sticker .stamp').forEach(s => s.remove());
    stampOn($(`.sticker[data-id="${S.equipped}"]`), '使用中');
    refreshAside('equip');
    catNod();
    toast(`${RODS.find(r => r.id === S.equipped).name} 已装备`);
    return;
  }
  if (name === 'to-shop') return go('shop', { tab: 1 });
  if (name === 'claim') return claim(el);
  if (name === 'enter') return enterPond(el);
}

async function buy(btn) {
  const it = RODS.find(r => r.id === S.sel.shop);
  if (!it) return;
  if (it.price > S.coins) {
    caption('金币不足：按钮抖动 + 金币胶囊红色描边闪两下 + 小猫举便签提示');
    play(btn, [{ translate: '0 0' }, { translate: '-6px 0' }, { translate: '6px 0' }, { translate: '-4px 0' }, { translate: '4px 0' }, { translate: '0 0' }], { duration: 220 });
    const pill = $('#coins');
    play(pill, [{ borderColor: '#f0e2c8' }, { borderColor: '#e2533f' }, { borderColor: '#f0e2c8' }, { borderColor: '#e2533f' }, { borderColor: '#f0e2c8' }], { duration: 700, essential: true });
    toast(`还差 ${fmt(it.price - S.coins)} 金币，再钓几条鱼吧`, 'error');
    return;
  }
  caption('购买成功：商品图沿弧线飞进「装备」页签 620ms，页签接住后弹一下；金币数字滚动 400ms');
  S.coins -= it.price;
  rollCoins(-it.price);
  S.owned.add(it.id);
  const photo = $('.aside .polaroid .photo img');
  const target = $$('.navtab')[1];
  catNod();
  await flyTo(photo, $('img', target), `assets/items/${it.img}.png`);
  play($('img', target), [{ scale: '1.35', translate: '0 -6px' }, { scale: '1', translate: '0 0' }], { duration: 300, easing: EASE.outBack, essential: true });
  ripple(pt(target).x, pt($('#nav')).top + 2, { count: 1, w: 70, h: 22 });
  const card = $(`.sticker[data-id="${it.id}"] .meta`);
  if (card) card.textContent = '已拥有';
  refreshAside('shop');
  toast(`${it.name} 已放进装备`);
}

async function sell(btn) {
  const f = FISH.find(x => x.id === S.sel.bag);
  const card = $(`.sticker[data-id="${f.id}"]`);
  if (!card) return;
  caption('出售：卡片撕成 3 片纸屑落下淡出 520ms；其余卡片 FLIP 补位 260ms；金币飞入顶栏');
  const origin = card.getBoundingClientRect();
  const grid = card.parentElement;
  const before = new Map($$('.sticker', grid).map(c => [c, c.getBoundingClientRect()]));
  const cuts = [
    'polygon(0 0, 38% 0, 30% 40%, 42% 70%, 34% 100%, 0 100%)',
    'polygon(38% 0, 70% 0, 62% 35%, 72% 68%, 64% 100%, 34% 100%, 42% 70%, 30% 40%)',
    'polygon(70% 0, 100% 0, 100% 100%, 64% 100%, 72% 68%, 62% 35%)',
  ];
  cuts.forEach((clip, i) => {
    const piece = card.cloneNode(true);
    piece.classList.remove('sel');
    Object.assign(piece.style, { position: 'absolute', left: `${(origin.left - $('#stage').getBoundingClientRect().left) / scale}px`, top: `${(origin.top - $('#stage').getBoundingClientRect().top) / scale}px`, width: `${origin.width / scale}px`, height: `${origin.height / scale}px`, clipPath: clip, margin: 0 });
    $('#motion').appendChild(piece);
    const dx = (i - 1) * 40 + (Math.random() - 0.5) * 20;
    play(piece, [{ translate: '0 0', rotate: `${tilt(f.id)}deg`, opacity: 1 },
      { translate: `${dx}px 90px`, rotate: `${(i - 1) * 24 + 6}deg`, opacity: 0 }], { duration: 520, easing: EASE.inQuad, fill: 'forwards', essential: true })
      .finished.then(() => piece.remove());
  });
  card.remove();
  FISH.splice(FISH.indexOf(f), 1);
  $$('.sticker', grid).forEach(c => {
    const a = before.get(c);
    const b = c.getBoundingClientRect();
    const dx = (a.left - b.left) / scale;
    const dy = (a.top - b.top) / scale;
    if (dx || dy) play(c, [{ translate: `${dx}px ${dy}px` }, { translate: '0 0' }], { duration: 260, easing: EASE.outCubic });
  });
  S.sel.bag = FISH[0] ? FISH[0].id : null;
  const next = $(`.sticker[data-id="${S.sel.bag}"]`);
  if (next) next.classList.add('sel');
  $('.mid-h .count').textContent = `${FISH.length} 条`;
  refreshAside('bag');
  await coinsFly({ getBoundingClientRect: () => origin }, 5);
  S.coins += f.price;
  rollCoins(f.price);
}

async function claim(btn) {
  const a = ACH.find(x => x.id === btn.dataset.id);
  const card = btn.closest('.sticker');
  caption('领取：奖章绕 Y 轴翻转 360° 600ms，背后放射线淡入淡出并转动，金币飞入顶栏，盖「已领取」章');
  play($('.medal', card), [{ transform: 'rotateY(0deg) scale(1)' }, { transform: 'rotateY(180deg) scale(1.15)', offset: 0.5 }, { transform: 'rotateY(360deg) scale(1)' }],
    { duration: 600, easing: EASE.outCubic });
  play($('.rays', card), [{ opacity: 0, rotate: '0deg', scale: '0.6' }, { opacity: 1, rotate: '40deg', scale: '1', offset: 0.4 }, { opacity: 0, rotate: '90deg', scale: '1.1' }],
    { duration: 900, easing: EASE.outCubic });
  btn.remove();
  a.state = 'done';
  await wait(420);
  stampOn(card, '已领取');
  await coinsFly(card, 6);
  S.coins += a.reward;
  rollCoins(a.reward);
  const done = ACH.filter(x => x.state === 'done').length;
  $('.mid-h .count').textContent = `${done} / ${ACH.length}`;
  $('.aside .caption').textContent = `${done} / ${ACH.length}`;
}

/* ----- map ----- */

function mapApply(animate) {
  const c = $('.map-content');
  if (!c) return;
  const m = S.map;
  const to = `translate(${m.ox}px, ${m.oy}px) scale(${m.s})`;
  if (animate) {
    const from = c.style.transform || 'translate(-180px, -140px) scale(1)';
    play(c, [{ transform: from }, { transform: to }], { duration: 350, easing: EASE.outCubic });
  }
  c.style.transform = to;
}

function clampMap() {
  const m = S.map;
  m.ox = Math.min(0, Math.max(690 - 1050 * m.s, m.ox));
  m.oy = Math.min(0, Math.max(420 - 700 * m.s, m.oy));
}

function bindMap(root) {
  mapApply(false);
  const view = $('.map-view', root);
  let drag = null;
  view.addEventListener('pointerdown', e => {
    if (e.target.closest('.buoy')) return;
    drag = { x: e.clientX, y: e.clientY, ox: S.map.ox, oy: S.map.oy };
    view.setPointerCapture(e.pointerId);
    view.style.cursor = 'grabbing';
  });
  view.addEventListener('pointermove', e => {
    if (!drag) return;
    S.map.ox = drag.ox + (e.clientX - drag.x) / scale;
    S.map.oy = drag.oy + (e.clientY - drag.y) / scale;
    clampMap();
    mapApply(false);
  });
  const end = () => { drag = null; view.style.cursor = ''; };
  view.addEventListener('pointerup', end);
  view.addEventListener('pointercancel', end);
  $$('.buoy', root).forEach(b => {
    b.addEventListener('pointerenter', () => {
      play($('.body', b), [{ scale: '1' }, { scale: '1.15' }], { duration: 160, easing: EASE.outBack, fill: 'forwards', composite: 'add' });
      play($('.label', b), [{ translate: '-50% 6px', opacity: 0.6 }, { translate: '-50% 0', opacity: 1 }], { duration: 160, easing: EASE.outBack });
    });
    b.addEventListener('pointerleave', () => {
      $('.body', b).getAnimations().filter(a => a.effect.getKeyframes().some(k => k.scale)).forEach(a => a.cancel());
    });
    b.addEventListener('click', () => selectPond(b));
  });
  bindFee($('.aside', root));
}

function bindFee(aside) {
  $$('[data-fee]', aside).forEach(btn => btn.addEventListener('click', () => {
    S.feeMode = +btn.dataset.fee;
    $$('[data-fee]', aside).forEach(x => x.classList.toggle('on', x === btn));
    $('.seg .knob', aside).style.left = `${S.feeMode ? 165 : 4}px`;
    caption('分段选择：色块滑到新选项 220ms OutBack');
  }));
}

function selectPond(b) {
  wake();
  const p = PONDS.find(x => x.id === b.dataset.id);
  if (p.locked) {
    press(b);
    play($('.body', b), [{ translate: '0 0' }, { translate: '-4px 0' }, { translate: '4px 0' }, { translate: '-3px 0' }, { translate: '0 0' }], { duration: 200, composite: 'add' });
    toast(`${p.name} 需要 Lv.${p.lv} 才能去`, 'error');
    return;
  }
  S.sel.map = p.id;
  caption('选中浮标：两圈水波（间隔 200ms）+ 猫爪印从上方落下盖章 + 镜头 350ms OutCubic 平移放大到 1.15 倍 + 拍立得从右侧贴入');
  $$('.paw', $('.map-content')).forEach(x => x.remove());
  const paw = document.createElement('div');
  paw.className = 'paw';
  paw.innerHTML = pawSvg('#75472e');
  b.appendChild(paw);
  play(paw, [{ opacity: 0, translate: '0 -30px', scale: '1.6' }, { opacity: 0.85, translate: '0 0', scale: '1' }], { duration: 260, delay: 120, easing: EASE.outBack });
  S.map.s = 1.15;
  S.map.ox = 345 - p.x * 1050 * S.map.s;
  S.map.oy = 210 - p.y * 700 * S.map.s;
  clampMap();
  mapApply(true);
  setTimeout(() => {
    const q = pt(b);
    ripple(q.x, q.y - 4, { count: 2, w: 110, h: 40 });
  }, 360 / rate);
  $('#map-status').textContent = `已选中 ${p.name}，可以进入鱼塘。`;
  refreshAside('map');
}

async function enterPond() {
  const p = PONDS.find(x => x.id === S.sel.map);
  const b = $(`.buoy[data-id="${p.id}"]`);
  caption('进入鱼塘：浮标「咚」地沉入水里 + 三圈水波 → 前往页（浮漂下沉再浮起，代替转圈）→ 打开 Overlay');
  const q = pt(b);
  ripple(q.x, q.y - 4, { count: 3, w: 120, h: 44 });
  await play($('.body', b), [{ translate: '0 0', opacity: 1 }, { translate: '0 16px', opacity: 0 }], { duration: 360, easing: EASE.inQuad, fill: 'forwards', composite: 'add' }).finished;
  const go2 = layer(`<div style="width:60px;height:86px" class="go-bob">${bobberSvg()}</div><div>正在前往 ${p.name}…</div>`, 'going');
  go2.style.pointerEvents = 'auto';
  play(go2, [{ opacity: 0 }, { opacity: 1 }], { duration: 200, fill: 'forwards', essential: true });
  const dip = play($('.go-bob', go2), [{ translate: '0 0' }, { translate: '0 14px', offset: 0.3 }, { translate: '0 -4px', offset: 0.7 }, { translate: '0 0' }],
    { duration: 900, iterations: Infinity, easing: EASE.inOutSine });
  await wait(1700);
  dip.cancel();
  await play(go2, [{ opacity: 1 }, { opacity: 0 }], { duration: 200, fill: 'forwards', essential: true }).finished;
  go2.remove();
  $('.body', b).getAnimations().forEach(a => a.cancel());
  startPageLoops('map', $('.content'));
  toast('原型到此为止：这里会打开 Overlay 鱼塘');
}

/* ----- settings ----- */

function bindSettings(root) {
  $$('.preset', root).forEach(btn => btn.addEventListener('click', () => {
    if (S.res === btn.dataset.res) return;
    S.res = btn.dataset.res;
    $$('.check-stamp', root).forEach(c => c.remove());
    const c = document.createElement('span');
    c.className = 'check-stamp';
    c.textContent = '✓';
    btn.appendChild(c);
    caption('选中尺寸：右上角小勾章从 1.6 倍砸下 200ms OutBack；切换后自动保存，用小鱼提示告知');
    play(c, [{ scale: '1.7', rotate: '-30deg', opacity: 0 }, { scale: '1', rotate: '0deg', opacity: 1 }], { duration: 200, easing: EASE.outBack });
    refreshSettingsAside();
    toast(`已切换到 ${S.res}，已自动保存`);
  }));
  $$('[data-mode]', root).forEach(btn => btn.addEventListener('click', () => {
    S.mode = +btn.dataset.mode;
    $$('[data-mode]', root).forEach(x => x.classList.toggle('on', x === btn));
    $('.seg .knob', root).style.left = `${4 + S.mode * 222}px`;
    caption('分段选择：色块滑到新选项 220ms OutBack（切换窗口模式时页面本身不做动画）');
    refreshSettingsAside();
  }));
  $$('.switch', root).forEach(sw => sw.addEventListener('click', () => toggleSwitch(sw)));
}

function refreshSettingsAside() {
  const tmp = document.createElement('div');
  tmp.innerHTML = settingsBody();
  $('.aside').innerHTML = $('.aside', tmp).innerHTML;
}

async function toggleSwitch(sw) {
  const key = sw.dataset.sw;
  const on = key === 'tray' ? (S.tray = !S.tray) : (S.notify[+key] = !S.notify[+key]);
  const knob = $('.knob', sw);
  const paw = $('.paw-push', sw);
  sw.classList.toggle('on', on);
  if (on) {
    caption('猫爪开关：猫爪从左边伸出 80ms → 把圆钮推到右边 220ms OutBack → 收回；底色过渡到叶绿');
    await play(paw, [{ opacity: 0, translate: '-12px 0' }, { opacity: 1, translate: '0 0' }], { duration: 80, fill: 'forwards' }).finished;
    play(paw, [{ opacity: 1, translate: '0 0' }, { opacity: 1, translate: '34px 0', offset: 0.7 }, { opacity: 0, translate: '20px 0' }], { duration: 320, easing: EASE.outCubic, fill: 'forwards' });
    await play(knob, [{ translate: '0 0' }, { translate: '36px 0' }], { duration: T.snap, delay: 20, easing: EASE.outBack }).finished;
    paw.getAnimations().forEach(a => a.cancel());
  } else {
    caption('关闭：圆钮滑回左边 220ms OutCubic，不出猫爪');
    play(knob, [{ translate: '36px 0' }, { translate: '0 0' }], { duration: T.snap, easing: EASE.outCubic });
  }
  knob.style.translate = on ? '36px 0' : '0 0';
  refreshSettingsAside();
}

/* ---------------- celebration ---------------- */

let celebrating = null;

async function celebrate(first) {
  if (celebrating) return;
  wake();
  const species = first ? { id: 'golden_line', name: '金线鲃', size: 0.31, reward: 120 } : { id: 'carp', name: '鲤鱼', size: 0.58, reward: 60 };
  const box = $('#celebrate');
  box.innerHTML = `<div class="dim"></div><div class="water"></div><div class="c-bobber">${bobberSvg()}</div>
    <div class="card"><div class="title">${species.name}</div><div class="size">0.00 m</div>${first ? '<div class="stamp">新</div>' : ''}</div>
    <img class="c-fish" src="assets/fish/${species.id}.png"><div class="skip">点击任意位置跳过</div>`;
  box.classList.add('show');
  const token = { skipped: false };
  celebrating = token;
  const step = ms => new Promise(r => {
    const t = setTimeout(r, ms / rate);
    token.wake = () => { clearTimeout(t); r(); };
  });
  box.onclick = () => {
    token.skipped = true;
    box.getAnimations({ subtree: true }).forEach(a => a.finish());
    token.wake?.();
  };
  caption(first
    ? '溅 Splash 收获演出（首次）：浮漂下沉 → 水花 + 鱼跃出翻转 → 金币弹出 → 落成贴纸 → 盖「新」章 + 小猫跳 → 尺寸与金币滚动，共 1.6 秒'
    : '普通收获：只播放到落成贴纸（1.0 秒），不盖章，金币照常飞入');
  const els = { dim: $('.dim', box), water: $('.water', box), bob: $('.c-bobber', box), card: $('.card', box), fish: $('.c-fish', box), stamp: $('.stamp', box) };
  play(els.dim, [{ opacity: 0 }, { opacity: 1 }], { duration: 160, fill: 'both', essential: true });
  play(els.water, [{ opacity: 0, scale: '0.8' }, { opacity: 1, scale: '1' }], { duration: 200, fill: 'both', essential: true });
  play(els.fish, [{ opacity: 0 }], { duration: 1, fill: 'forwards', essential: true });

  // 0-180ms 浮漂下沉
  play(els.bob, [{ translate: '0 0' }, { translate: '0 12px' }, { translate: '0 6px' }], { duration: 180, easing: EASE.inOutSine, fill: 'forwards' });
  ripple(836, 640, { count: 2, w: 160, h: 46, color: 'rgba(255,255,255,0.85)' });
  await step(180);

  // 180-600ms 水花 + 鱼跃出 + 翻转
  if (!token.skipped) {
    for (let i = 0; i < 14; i++) {
      const d = document.createElement('i');
      d.className = 'drop';
      d.style.left = '829px';
      d.style.top = '612px';
      box.appendChild(d);
      const ang = (-150 + Math.random() * 120) * Math.PI / 180;
      const dist = 90 + Math.random() * 130;
      const tx = Math.cos(ang) * dist;
      const ty = Math.sin(ang) * dist;
      play(d, [{ translate: '0 0', opacity: 1, scale: '0.6' }, { translate: `${tx * 0.7}px ${ty}px`, opacity: 1, scale: '1', offset: 0.5, easing: EASE.inQuad },
        { translate: `${tx}px ${ty * 0.2 + 60}px`, opacity: 0, scale: '0.8' }], { duration: 640, easing: EASE.outCubic, fill: 'forwards' });
    }
    play(els.bob, [{ opacity: 1 }, { opacity: 0 }], { duration: 120, fill: 'forwards', essential: true });
  }
  play(els.fish, [
    { opacity: 0, translate: '0 100px', scale: '0.5', rotate: '25deg' },
    { opacity: 1, translate: '0 -40px', scale: '1', rotate: '-30deg', offset: 0.43 },
    { opacity: 1, translate: '0 -240px', scale: '1', rotate: '-390deg' },
  ], { duration: 420, easing: EASE.outCubic, fill: 'forwards' });
  await step(180);
  if (!token.skipped) {
    for (let i = 0; i < 8; i++) {
      const c = document.createElement('img');
      c.src = icon('coin');
      c.className = 'coin-fly c-coin';
      c.style.left = '836px';
      c.style.top = '380px';
      box.appendChild(c);
      const ang = (i / 8) * Math.PI * 2;
      play(c, [{ translate: '0 0', scale: '0.3', opacity: 0 }, { translate: `${Math.cos(ang) * 150}px ${Math.sin(ang) * 110}px`, scale: '1', opacity: 1 }],
        { duration: 360, easing: EASE.outBack, fill: 'forwards' });
    }
  }
  await step(240);

  // 600-1000ms 落成贴纸
  play(els.card, [{ opacity: 0, scale: '1.12', rotate: '4.5deg' }, { opacity: 1, scale: '1', rotate: '-1.5deg' }], { duration: 260, easing: EASE.outBack, fill: 'forwards', essential: true });
  play(els.fish, [{ opacity: 1, translate: '0 -240px', scale: '1', rotate: '-390deg' }, { opacity: 1, translate: '0 -200px', scale: '1.05', rotate: '-364deg' }],
    { duration: 400, easing: EASE.outBack, fill: 'forwards', essential: true });
  await step(300);
  const sizeEl = $('.size', els.card);
  const t0 = performance.now();
  const rollSize = now => {
    const t = Math.min(1, ((now - t0) * rate) / 360);
    sizeEl.textContent = `${(species.size * (1 - Math.pow(1 - t, 3))).toFixed(2)} m`;
    if (t < 1 && !token.skipped) requestAnimationFrame(rollSize); else sizeEl.textContent = `${species.size.toFixed(2)} m`;
  };
  requestAnimationFrame(rollSize);
  await step(100);

  // 1000-1240ms 首次：盖「新」章 + 小猫跳
  if (first && els.stamp) {
    play(els.stamp, [{ opacity: 0, scale: '1.7', rotate: '-40deg' }, { opacity: 1, scale: '1', rotate: '-14deg' }], { duration: 200, easing: EASE.outBack, fill: 'forwards', essential: true });
    setTimeout(() => play(els.card, [{ translate: '0 0' }, { translate: '3px 2px' }, { translate: '-3px -1px' }, { translate: '0 0' }], { duration: 140 }), 180 / rate);
    catJump();
    await step(240);
  }

  // 1240-1600ms 金币飞入顶栏
  const coins = $$('.c-coin', box);
  const target = pt($('#coins img'));
  coins.forEach((c, i) => {
    const p = pt(c);
    c.getAnimations().forEach(a => a.cancel());
    c.style.left = `${p.x}px`;
    c.style.top = `${p.y}px`;
    play(c, [{ left: `${p.x}px`, top: `${p.y}px`, scale: '1' }, { left: `${target.x}px`, top: `${target.y}px`, scale: '0.7' }],
      { duration: 420, delay: i * 40, easing: 'cubic-bezier(0.45, 0, 0.3, 1)', fill: 'forwards', essential: true });
  });
  await step(420);
  S.coins += species.reward;
  rollCoins(species.reward);
  await step(token.skipped ? 0 : 1200);
  await play(box, [{ opacity: 1 }, { opacity: 0 }], { duration: 200, fill: 'forwards', essential: true }).finished;
  box.getAnimations().forEach(a => a.cancel());
  box.classList.remove('show');
  box.innerHTML = '';
  celebrating = null;
}

/* ---------------- bell ---------------- */

let bellTimer = 0;

function ring() {
  const img = $('#bell img');
  play(img, [{ rotate: '0deg' }, { rotate: '14deg' }, { rotate: '-12deg' }, { rotate: '9deg' }, { rotate: '-6deg' }, { rotate: '3deg' }, { rotate: '0deg' }], { duration: 900, easing: EASE.outCubic });
}

function bellDemo() {
  caption('新通知：红点 OutBack 弹出，铃铛左右摆 3 次（±12°，900ms），之后每 20 秒再摆一次，点开后停止');
  play($('#bell .dot'), [{ scale: '0' }, { scale: '1' }], { duration: T.snap, easing: EASE.outBack, fill: 'forwards', essential: true });
  ring();
  clearInterval(bellTimer);
  bellTimer = setInterval(ring, 20000 / rate);
}

/* ---------------- boot ---------------- */

function fit() {
  const vp = $('#viewport');
  const w = vp.clientWidth - 24;
  const h = vp.clientHeight - 24;
  scale = Math.min(w / 1672, h / 941);
  const st = $('#stage');
  st.style.transform = `scale(${scale})`;
  st.style.left = `${(vp.clientWidth - 1672 * scale) / 2}px`;
  st.style.top = `${(vp.clientHeight - 941 * scale) / 2}px`;
}

function boot() {
  installJournalPages();
  $$('[data-icon]').forEach(i => { i.src = icon(i.dataset.icon); });
  $('#avatar-img').src = jAvatar(J.avatar);
  $('#coins .num').textContent = fmt(S.coins);
  $('.page-header #close').addEventListener('click', closePage);
  const nav = $('#nav');
  NAV.forEach(n => {
    const t = document.createElement('div');
    t.className = `navtab ${n.id === S.page ? 'on' : ''}`;
    t.innerHTML = `<img src="${icon(n.icon)}"><span>${n.label}</span>`;
    t.addEventListener('click', () => go(n.id));
    nav.appendChild(t);
  });
  $('#bobber .float').innerHTML = bobberSvg();
  fit();
  applyHeader(S.page);
  render(S.page);
  requestAnimationFrame(() => {
    if (NAV.some(n => n.id === S.page)) {
      $('#bobber').style.translate = `${tabCenter(bobIdx)}px 0`;
      startBobFloat();
    } else {
      $('#bobber').style.opacity = '0';
    }
    applyHeader(S.page);
  });
  entrance($('.content'));

  document.addEventListener('pointerdown', e => {
    wake();
    const el = e.target.closest('.btn, .tab, .navtab, .round, #close, .sticker, .seg button, .switch, .plus, .buoy, .claim');
    if (!el) return;
    document._pressed = el;
    press(el);
  });
  const up = () => {
    if (document._pressed) release(document._pressed);
    document._pressed = null;
  };
  document.addEventListener('pointerup', up);
  document.addEventListener('pointercancel', up);

  document.addEventListener('pointerover', e => {
    const b = e.target.closest('.btn.primary:not(.off)');
    if (b && !b._hover) {
      b._hover = true;
      catLean(true);
      const fish = $('.fish-icon', b);
      if (fish) play(fish, [{ rotate: '0deg' }, { rotate: '-14deg' }, { rotate: '10deg' }, { rotate: '0deg' }], { duration: 420, easing: EASE.inOutSine });
    }
  });
  document.addEventListener('pointerout', e => {
    const b = e.target.closest('.btn.primary');
    if (b && b._hover && !b.contains(e.relatedTarget)) {
      b._hover = false;
      catLean(false);
    }
  });

  $('#bell').addEventListener('click', () => {
    clearInterval(bellTimer);
    play($('#bell .dot'), [{ scale: '1' }, { scale: '0' }], { duration: 160, fill: 'forwards', essential: true });
    toast('阿花邀请你去千岛湖钓鱼');
  });
  $('#gear').addEventListener('click', () => go('settings'));
  $('#world-entry').addEventListener('click', () => go('map'));
  $('#coins .plus').addEventListener('click', () => toast('原型：这里打开充值页'));

  $('#cmp-new').addEventListener('click', () => setCompare(false));
  $('#cmp-old').addEventListener('click', () => setCompare(true));
  $$('[data-rate]').forEach(b => b.addEventListener('click', () => {
    setRate(+b.dataset.rate);
    $$('[data-rate]').forEach(x => x.classList.toggle('on', x === b));
  }));
  $('#reduce').addEventListener('click', e => {
    reduced = !reduced;
    e.target.textContent = `减少动效：${reduced ? '开' : '关'}`;
    e.target.classList.toggle('on', reduced);
    caption(reduced ? '减少动效已开启：只保留 120ms 内的淡入淡出，循环动画全部停止' : '减少动效已关闭');
    if (reduced) {
      bobFloat.cancel();
      stopPageLoops();
      catBreath.cancel();
    } else {
      startBobFloat();
      if (S.open) startPageLoops(S.page, $('.content')); else startCatBreath();
    }
  });
  $$('[data-goto]').forEach(b => b.addEventListener('click', () => go(b.dataset.goto)));
  $$('[data-demo]').forEach(b => b.addEventListener('click', () => {
    const d = b.dataset.demo;
    if (d === 'catch-new') celebrate(true);
    if (d === 'catch') celebrate(false);
    if (d === 'toast') toast('窗口设置已自动保存');
    if (d === 'error') toast('网络有点慢，正在重新连接…', 'error');
    if (d === 'bell') bellDemo();
    if (d === 'sleep') sleep();
  }));

  window.addEventListener('keydown', e => {
    if (e.code === 'Space' && !e.repeat && !e.target.closest('input, textarea, select, [contenteditable=true], [role=button], button')) { e.preventDefault(); setCompare(true); }
  });
  window.addEventListener('keyup', e => {
    if (e.code === 'Space') setCompare(false);
  });
  window.addEventListener('resize', fit);

  setInterval(() => {
    if (!sleeping && performance.now() - lastActive > 30000 / rate) sleep();
  }, 1000);
}

function setCompare(on) {
  $('#stage').classList.toggle('comparing', on);
  $('#cmp-new').classList.toggle('on', !on);
  $('#cmp-old').classList.toggle('on', on);
}

// The workspace installs its page overrides in the next script. A cached font
// can resolve before that script loads, so wait for both scripts and the font.
Promise.all([
  document.fonts.load('20px FishCozy').catch(() => {}),
  new Promise(resolve => {
    if (document.readyState !== 'loading') resolve();
    else document.addEventListener('DOMContentLoaded', resolve, { once: true });
  }),
]).then(boot);

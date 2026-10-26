'use strict';

// Extra journal pages share the original shell, artwork, motion and game data.
// All edits, likes, comments and visits below are local prototype interactions.
const JOURNAL_KEY = 'fish-cozy-journal-pages-v1';
const J = {
  nickname: '小橘', bio: '喜欢晴天和湖水，也喜欢等一条鱼上钩。', avatar: 'orange',
  showcase: ['koi', 'golden_line', 'mandarin'], album: ['koi', 'grass_carp', 'golden_line', 'carp', 'mandarin', 'black_bass'],
  rankSelected: 'cat-flower', feedSelected: 'post-1', codexSelected: 'koi', showcaseSelected: 0,
  albumPage: 0, albumSelected: 'koi', pondQuery: '', pondSelected: 'pond-calm',
  pondSeen: new Set(), followed: [], liked: [], comments: {}, posts: [], editing: false,
};
const AVATARS = [['orange', '橘猫'], ['white', '白猫'], ['black', '黑猫'], ['calico', '三花'], ['silver', '银渐层'], ['blue', '蓝猫']];
const JOURNAL_PLAYERS = [
  { id: 'cat-flower', name: '阿花', avatar: 'calico', lv: 12, fish: 'koi', daily: 1.28, weekly: 36.8, pond: 86, rare: 18, bio: '今天也想遇到一条漂亮的鱼。' },
  { id: 'cat-snow', name: '小白', avatar: 'white', lv: 10, fish: 'grass_carp', daily: 1.16, weekly: 42.6, pond: 74, rare: 14, bio: '有风的时候，就在湖边坐一坐。' },
  { id: 'cat-black', name: '煤球', avatar: 'black', lv: 11, fish: 'black_bass', daily: 1.05, weekly: 31.2, pond: 92, rare: 21, bio: '夜钓的快乐，你也懂。' },
  { id: 'cat-blue', name: '蓝莓', avatar: 'blue', lv: 9, fish: 'mandarin', daily: .94, weekly: 28.5, pond: 66, rare: 12, bio: '把每一次收获都记进小本子。' },
  { id: 'self', name: '小橘', avatar: 'orange', lv: 7, fish: 'koi', daily: .86, weekly: 24.3, pond: 64, rare: 9, bio: '' },
  { id: 'cat-silver', name: '银杏', avatar: 'silver', lv: 8, fish: 'carp', daily: .78, weekly: 21.8, pond: 58, rare: 7, bio: '不着急，鱼总会来的。' },
  { id: 'cat-cream', name: '奶糖', avatar: 'cream', lv: 6, fish: 'crucian', daily: .62, weekly: 18.6, pond: 43, rare: 5, bio: '新手上路，请多关照喵。' },
  { id: 'cat-cow', name: '小墨', avatar: 'cow', lv: 6, fish: 'bream', daily: .51, weekly: 15.4, pond: 39, rare: 4, bio: '钓鱼和发呆都很擅长。' },
];
const JOURNAL_POSTS = [
  { id: 'post-1', author: 'cat-flower', fish: 'koi', text: '锦鲤真的来了！这份好运，分给今天在湖边的每一位钓友。', likes: 28, time: '10 分钟前', pond: '千岛湖', friends: true, replies: [{ name: '小白', text: '好漂亮的花纹，接住好运！' }] },
  { id: 'post-2', author: 'cat-snow', fish: 'grass_carp', text: '草鱼又刷新纪录了。今天的谷物饵，果然很受欢迎。', likes: 16, time: '35 分钟前', pond: '太湖', friends: true, replies: [{ name: '煤球', text: '下次我也试试谷物饵。' }] },
  { id: 'post-3', author: 'cat-black', fish: 'black_bass', text: '夕阳快落下的时候，等到了这一口。慢慢等，总会有惊喜。', likes: 11, time: '1 小时前', pond: '洪泽湖', friends: false, replies: [] },
  { id: 'post-4', author: 'cat-blue', fish: 'mandarin', text: '今天第一次钓到鳜鱼，已经贴进我的图鉴啦。', likes: 9, time: '2 小时前', pond: '鄱阳湖', friends: false, replies: [] },
];
let journalFish = [];
let pondObserver = null;
let pondEntering = false;
let journalModalFocus = null;
function jEscape(value) { return String(value).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c])); }
function jAvatar(id) { return `${U}characters/avatars/${id}.png`; }
function jFish(id) { return journalFish.find(f => f.id === id) || journalFish[0]; }
function jPlayer(id) { return id === 'self' ? { ...JOURNAL_PLAYERS.find(p => p.id === id), name: J.nickname, avatar: J.avatar, bio: J.bio } : JOURNAL_PLAYERS.find(p => p.id === id) || JOURNAL_PLAYERS[0]; }
function jButton(label, action, extra = '', cls = '') { return `<button type="button" class="btn j-button ${cls}" data-act="j-${action}" ${extra}>${label}</button>`; }
function jSave() {
  const { nickname, bio, avatar, showcase, album, followed, liked, comments, posts } = J;
  try { localStorage.setItem(JOURNAL_KEY, JSON.stringify({ nickname, bio, avatar, showcase, album, followed, liked, comments, posts })); } catch { /* Browsers can disallow storage for file URLs. */ }
}
function jRestore() {
  try {
    const data = JSON.parse(localStorage.getItem(JOURNAL_KEY) || '{}');
    if (typeof data.nickname === 'string' && data.nickname.trim()) J.nickname = data.nickname.slice(0, 16);
    if (typeof data.bio === 'string') J.bio = data.bio.slice(0, 80);
    if (AVATARS.some(a => a[0] === data.avatar)) J.avatar = data.avatar;
    for (const key of ['showcase', 'album']) if (Array.isArray(data[key])) J[key] = data[key].filter(id => id === null || journalFish.some(f => f.id === id)).slice(0, key === 'showcase' ? 3 : 24);
    J.showcase = Array.from({ length: 3 }, (_, i) => J.showcase[i] || null);
    for (const key of ['followed', 'liked']) if (Array.isArray(data[key])) J[key] = data[key].filter(x => typeof x === 'string');
    if (Array.isArray(data.posts)) J.posts = data.posts.filter(p => typeof p.id === 'string' && typeof p.text === 'string' && journalFish.some(f => f.id === p.fish)).slice(0, 30).map(p => ({ ...p, author: 'self', text: p.text.slice(0, 160), replies: [], likes: 0, time: '刚刚', friends: true, pond: '千岛湖' }));
    if (data.comments && typeof data.comments === 'object' && !Array.isArray(data.comments)) J.comments = Object.fromEntries(Object.entries(data.comments).filter(([, x]) => Array.isArray(x)).map(([id, list]) => [id, list.filter(c => typeof c.text === 'string').slice(0, 50).map(c => ({ name: String(c.name).slice(0, 16), text: c.text.slice(0, 80) }))]));
  } catch { /* Malformed saved previews fall back to the seeded example. */ }
}
function jPolaroid(src, title, tape = 'dots', cls = '') { return `<div class="polaroid ${cls}"><i class="tape ${tape}"></i><div class="photo"><img src="${src}" alt="${jEscape(title)}"></div><div class="caption">${jEscape(title)}</div></div>`; }
function jRefresh() {
  stopPageLoops();
  render(S.page);
  entrance($('.content'), { rail: false });
}
function jSyncPlayer() {
  $('#player .name').textContent = J.nickname;
  $('#avatar-img').src = jAvatar(J.avatar);
}

/* ---------------- leaderboard ---------------- */
const JOURNAL_BOARDS = [
  { key: 'daily', title: '今日大鱼', unit: 'm', sub: '今日最大体长', digits: 2 },
  { key: 'weekly', title: '每周钓王', unit: 'kg', sub: '本周累计重量', digits: 1 },
  { key: 'pond', title: '当前鱼塘', unit: '条', sub: '千岛湖累计鱼获', digits: 0 },
  { key: 'rare', title: '稀有鱼获', unit: '种', sub: '珍稀鱼种收藏', digits: 0 },
];
function jBoard() { return JOURNAL_BOARDS[S.tab.rank]; }
function jRankValue(p) { const b = jBoard(); return `${p[b.key].toFixed(b.digits)} <small>${b.unit}</small>`; }
function jRanks() { return JOURNAL_PLAYERS.map(p => jPlayer(p.id)).sort((a, b) => b[jBoard().key] - a[jBoard().key]); }
function rankBody() {
  const list = jRanks(), top = list.slice(0, 3), mine = list.findIndex(p => p.id === 'self') + 1;
  return `<div class="mid-h">${jBoard().title}<span class="count">${jBoard().sub}</span></div>
  <div class="mid j-rank-mid"><div class="j-podium">${[1, 0, 2].map(index => {
    const p = top[index];
    return `<button class="sticker j-podium-card place-${index + 1} ${J.rankSelected === p.id ? 'sel' : ''}" data-id="${p.id}" style="rotate:${tilt(p.id)}deg"><i class="tape dots"></i><div class="j-place">${index === 0 ? `<img src="${icon('trophy')}" alt="冠军">` : index + 1}</div><img class="j-rank-avatar" src="${jAvatar(p.avatar)}" alt="${jEscape(p.name)}"><div class="name">${jEscape(p.name)}</div><div class="meta">${jRankValue(p)}</div><span class="j-podium-label">${['第一名', '第二名', '第三名'][index]}</span></button>`;
  }).join('')}</div><div class="j-rank-list">${list.map((p, i) => `<button class="sticker j-rank-row ${J.rankSelected === p.id ? 'sel' : ''} ${p.id === 'self' ? 'j-self' : ''}" data-id="${p.id}" style="rotate:0deg"><i class="tape"></i><b class="j-rank-position">${String(i + 1).padStart(2, '0')}</b><img src="${jAvatar(p.avatar)}" alt=""><span class="j-rank-person">${jEscape(p.name)}${p.id === 'self' ? '<small>我</small>' : ''}<em>Lv.${p.lv}</em></span><strong>${jRankValue(p)}</strong></button>`).join('')}</div></div>
  <div class="aside">${rankAside()}</div><div class="status">我的排名 #${mine} · ${jBoard().sub} ${jRankValue(jPlayer('self'))} · 每一次抛竿，都在刷新自己的纪录。</div>`;
}
function rankAside() {
  const p = jPlayer(J.rankSelected), index = jRanks().findIndex(x => x.id === p.id) + 1, f = jFish(p.fish);
  return `<div class="heading">钓友名片 <span class="j-small-label">#${index}</span></div>${jPolaroid(jAvatar(p.avatar), p.name, '', 'j-portrait')}
  <div class="detail">Lv.${p.lv} · ${jEscape(p.bio)}<div class="chips"><span class="chip">${jBoard().title} ${jRankValue(p)}</span><span class="chip">代表鱼获 · ${f.name}</span></div></div>
  ${jButton(p.id === 'self' ? '打开我的资料' : '看看钓友的收藏', 'player-card', `data-player="${p.id}" style="left:18px;top:420px;width:326px"`, 'primary')}`;
}

/* ---------------- profile / showcase / codex / album ---------------- */
function profileBody() {
  const tab = S.tab.profile;
  if (tab === 1) return showcaseBody();
  if (tab === 2) return codexBody();
  if (tab === 3) return albumBody();
  if (tab === 4) {
    const old = S.tab.ach; S.tab.ach = 0; const body = achBody(); S.tab.ach = old;
    return body;
  }
  if (J.editing) return profileEditBody();
  const best = jFish(J.showcase.find(Boolean) || 'koi');
  return `<div class="mid-h">我的钓鱼时光<span class="count">在湖边的第 28 天</span></div><div class="mid j-profile-mid">
    <div class="j-profile-banner"><img class="j-profile-landscape" src="assets/pond-afternoon.jpg" alt="午后的湖畔"><div class="j-profile-identity"><img src="${jAvatar(J.avatar)}" alt="${jEscape(J.nickname)}"><div><h2>${jEscape(J.nickname)} <span>Lv.7</span></h2><p>${jEscape(J.bio)}</p></div></div></div>
    <div class="j-profile-stats"><div><img src="${icon('fish')}" alt=""><b>64</b><span>累计鱼获</span></div><div><img src="${icon('map')}" alt=""><b>5</b><span>探索水域</span></div><div><img src="${icon('star')}" alt=""><b>${journalFish.length} / 15</b><span>图鉴收集</span></div></div>
    <div class="j-xp-note"><div><strong>距离下一个钓鱼小目标</strong><span>Lv.7 → Lv.8</span></div><div class="bar"><i style="width:64%"></i></div><p>320 / 500 经验 · 去收获下一段故事吧。</p></div>
    <div class="j-profile-links">${jButton('编辑资料', 'edit-profile', '', 'primary')}${jButton('我的展示柜', 'profile-tab', 'data-tab-target="1"')}${jButton('翻翻相册', 'profile-tab', 'data-tab-target="3"')}</div>
  </div><div class="aside"><div class="heading">我的心头好</div>${jPolaroid(`assets/fish/${best.id}.png`, best.name)}<div class="detail">${best.size.toFixed(2)} m · <span class="stars">${'★'.repeat(best.stars)}</span><div class="chips"><span class="chip">千岛湖</span><span class="chip">已珍藏在展示柜</span></div></div>${jButton('整理我的展示柜', 'profile-tab', 'data-tab-target="1" style="left:18px;top:420px;width:326px"', 'primary')}</div><div class="status">头像、昵称、简介和展示鱼获，都可以在这里整理。</div>`;
}
function profileEditBody() {
  return `<div class="mid-h">编辑资料<span class="count">保存后更新手账和顶栏</span></div><div class="mid j-edit-mid"><form id="j-profile-form">
    <label for="j-nickname">游戏昵称 <small>1–16 个字</small></label><input id="j-nickname" maxlength="16" value="${jEscape(J.nickname)}" autocomplete="off" required>
    <label for="j-bio">个人简介 <small>最多 80 个字</small></label><textarea id="j-bio" maxlength="80">${jEscape(J.bio)}</textarea>
    <label>选择头像</label><div class="j-avatar-options">${AVATARS.map(([id, label]) => `<button type="button" class="${J.avatar === id ? 'sel' : ''}" data-avatar="${id}" aria-label="${label}" aria-pressed="${J.avatar === id}"><img src="${jAvatar(id)}" alt="${label}"><span>${label}</span></button>`).join('')}</div><input id="j-avatar-value" type="hidden" value="${J.avatar}">
    <p id="j-form-error" role="alert"></p><div class="j-form-actions"><button class="btn primary j-button" type="submit">保存资料</button>${jButton('取消修改', 'cancel-edit')}</div></form></div>
    <div class="aside"><div class="heading">资料预览</div>${jPolaroid(jAvatar(J.avatar), J.nickname, '', 'j-edit-preview')}<div class="detail" id="j-bio-preview">${jEscape(J.bio)}</div></div><div class="status">把喜欢的名字、头像和一句话介绍，留在自己的手账里。</div>`;
}
function showcaseBody() {
  const slots = J.showcase;
  if (J.showcaseSelected > 2) J.showcaseSelected = 0;
  return `<div class="mid-h">我的展示柜<span class="count">${slots.filter(Boolean).length} / 3 个展示位</span></div><div class="mid"><div class="grid c3 j-showcase-grid">${slots.map((id, i) => {
    const f = id && jFish(id);
    return stickerHtml({ id: `slot-${i}`, sel: J.showcaseSelected === i, cls: 'j-showcase-slot', art: f ? `<img class="j-showcase-fish" src="assets/fish/${f.id}.png">` : `<span class="j-slot-plus">＋</span>`, name: f ? f.name : '留给下一次惊喜', meta: f ? `${f.size.toFixed(2)} m · ${'★'.repeat(f.stars)}` : '点击选择一条鱼', tape: 'dots', ribbon: `展示 ${i + 1}` });
  }).join('')}</div><div class="j-shelf-note">${pawSvg('#d6b98c')}<span>把最喜欢的鱼获摆在这里，<br>让来做客的钓友也看看你的好运气。</span></div></div><div class="aside">${showcaseAside()}</div><div class="status">选中展示格，再从背包挑一条鱼。清空展示格不会出售鱼获。</div>`;
}
function showcaseAside() {
  const id = J.showcase[J.showcaseSelected], f = id && jFish(id);
  return `<div class="heading">展示位 ${J.showcaseSelected + 1}</div>${jPolaroid(f ? `assets/fish/${f.id}.png` : icon('fish'), f ? f.name : '还没有摆上鱼获', 'dots')}
    <div class="detail">${f ? `${f.size.toFixed(2)} m · <span class="stars">${'★'.repeat(f.stars)}</span><div class="chips"><span class="chip">来自千岛湖</span><span class="chip">最喜欢的收藏</span></div>` : '从背包里选一条最喜欢的鱼，贴进这一个展示格。'}</div>
    ${jButton(f ? '更换鱼获' : '选择鱼获', 'pick-showcase', 'style="left:18px;top:420px;width:210px"', 'primary')}${jButton('清空', 'clear-showcase', `style="left:240px;top:420px;width:104px" ${f ? '' : 'disabled'}`)}`;
}
function codexBody() {
  const all = [...journalFish, { id: 'taimen', name: '哲罗鲑', locked: true }, { id: 'chinese_sturgeon', name: '中华鲟', locked: true }, { id: 'black_carp', name: '青鱼', locked: true }];
  return `<div class="mid-h">鱼类图鉴<span class="count">已收录 ${journalFish.length} / ${all.length}</span></div><div class="mid"><div class="grid c4 j-codex-grid">${all.map(f => stickerHtml({ id: f.id, sel: J.codexSelected === f.id, locked: f.locked, art: `<img src="assets/fish/${f.id}.png">`, name: f.locked ? '尚未发现' : f.name, meta: f.locked ? '捕获后解锁' : `<span class="stars">${'★'.repeat(f.stars)}</span>`, stamp: f.locked ? '' : '已收录' })).join('')}</div></div><div class="aside">${codexAside()}</div><div class="status">灰色剪影是尚未遇见的朋友；每一种新鱼，都会在图鉴里留下印记。</div>`;
}
function codexAside() {
  const f = journalFish.find(x => x.id === J.codexSelected);
  if (!f) return `<div class="heading">未发现的鱼种</div>${jPolaroid(`assets/fish/${J.codexSelected}.png`, '等待一次相遇', 'lake', 'j-locked-photo')}<div class="detail">还没有遇见这位朋友。<br>捕获后解锁食性、推荐鱼饵和自己的捕获纪录。</div>${jButton('去探索新的水域', 'open-ponds', 'style="left:18px;top:420px;width:326px"', 'primary')}`;
  const vegetarian = ['grass_carp', 'silver_carp', 'bream'].includes(f.id);
  return `<div class="heading">${f.name} · 图鉴</div>${jPolaroid(`assets/fish/${f.id}.png`, f.name, 'lake')}
    <div class="detail j-codex-detail"><div class="chips"><span class="chip">${vegetarian ? '草食鱼' : '杂食 / 肉食'}</span><span class="chip">${vegetarian ? '谷物饵' : '腥香饵'}推荐</span></div><dl><dt>最大体长</dt><dd>${f.size.toFixed(2)} m</dd><dt>累计捕获</dt><dd>${f.stars + 2} 次</dd><dt>首次相遇</dt><dd>10 月 14 日 · 千岛湖</dd><dt>最近相遇</dt><dd>今天 · 湖畔</dd></dl></div>`;
}
function albumBody() {
  const ids = J.album.filter(id => journalFish.some(f => f.id === id));
  const total = Math.max(1, Math.ceil(ids.length / 2));
  J.albumPage = Math.min(J.albumPage, total - 1);
  const visible = ids.slice(J.albumPage * 2, J.albumPage * 2 + 2);
  if (!visible.includes(J.albumSelected)) J.albumSelected = visible[0] || null;
  return `<div class="mid-h">湖畔相册<span class="count">${ids.length} 张珍藏</span></div><div class="mid j-album-mid"><div class="j-album-spread">${visible.length ? visible.map((id, i) => {
    const f = jFish(id);
    return `<button class="sticker j-album-card ${id === J.albumSelected ? 'sel' : ''}" data-id="${id}" style="rotate:${i ? 1.3 : -1.3}deg"><i class="tape ${i ? 'lake' : 'dots'}"></i><div class="j-album-photo" style="background-image:url('assets/${i ? 'pond-afternoon.jpg' : 'home-sakura.jpg'}')"><img src="assets/fish/${id}.png" alt="${f.name}"></div><div class="name">${f.name} · ${f.size.toFixed(2)} m</div><div class="meta">${f.rare ? '难忘的稀有鱼获' : '值得记下的一次收获'}</div><div class="j-album-date">10 月 ${21 - J.albumPage * 2 - i} 日 · 千岛湖</div></button>`;
  }).join('') : '<div class="j-compact-empty">相册空了，去图鉴里挑一段回忆吧。</div>'}</div><div class="j-album-pager">${jButton('‹ 上一页', 'album-page', `data-step="-1" ${J.albumPage === 0 ? 'disabled' : ''}`)}<span>${J.albumPage + 1} / ${total}</span>${jButton('下一页 ›', 'album-page', `data-step="1" ${J.albumPage === total - 1 ? 'disabled' : ''}`)}</div></div><div class="aside">${albumAside()}</div><div class="status">一页两张拍立得，记下稀有鱼、大鱼，以及每一个值得留住的时刻。</div>`;
}
function albumAside() {
  const f = J.albumSelected && jFish(J.albumSelected);
  return `<div class="heading">回忆便签</div>${jPolaroid(f ? `assets/fish/${f.id}.png` : icon('fish'), f ? f.name : '下一页，等你来写', '')}<div class="detail">${f ? `在千岛湖，遇见 ${f.size.toFixed(2)} m 的${f.name}。<br>这一天的风和水光，也一起留在了照片里。` : '每次相遇都值得留念。可以从已有的鱼获中挑选一张，钉进相册。'}</div>${jButton('钉选回忆', 'album-add', 'style="left:18px;top:420px;width:198px"', 'primary')}${jButton('取消钉选', 'album-remove', `style="left:228px;top:420px;width:116px;font-size:18px" ${f ? '' : 'disabled'}`)}`;
}

/* ---------------- feed and comments ---------------- */
function jPosts() { return [...J.posts, ...JOURNAL_POSTS].filter(p => S.tab.feed === 0 || (S.tab.feed === 1 ? p.friends || J.followed.includes(p.author) : p.author === 'self')); }
function jPost(id) { return [...J.posts, ...JOURNAL_POSTS].find(p => p.id === id); }
function jReplies(p) { return [...(p.replies || []), ...(J.comments[p.id] || [])]; }
function feedBody() {
  const posts = jPosts();
  if (!posts.some(p => p.id === J.feedSelected)) J.feedSelected = posts[0]?.id || null;
  return `<div class="mid-h">${['公开动态', '好友动态', '我的动态'][S.tab.feed]}<span class="count">${posts.length} 条湖畔来信</span>${jButton('分享鱼获', 'compose', '', 'primary j-heading-action')}</div>
  <div class="mid j-feed-mid">${posts.length ? posts.map(p => {
    const author = jPlayer(p.author), f = jFish(p.fish), liked = J.liked.includes(p.id);
    return `<article class="sticker j-post ${J.feedSelected === p.id ? 'sel' : ''}" data-id="${p.id}" style="rotate:0deg"><i class="tape ${p.author === 'self' ? 'dots' : ''}"></i><header><img src="${jAvatar(author.avatar)}" alt=""><div><strong>${jEscape(author.name)}</strong><span>Lv.${author.lv} · ${jEscape(p.time)}</span></div><small>${p.visibility === 'friends' ? '仅好友' : '公开'}</small></header><p>${jEscape(p.text)}</p><div class="j-post-catch"><img src="assets/fish/${f.id}.png" alt="${f.name}"><div><b>${f.name}</b><span>${f.size.toFixed(2)} m · ${'★'.repeat(f.stars)}</span></div><small>${jEscape(p.pond)}</small></div><footer>${jButton(`${liked ? '♥ 已赞' : '♡ 点赞'} ${p.likes + (liked ? 1 : 0)}`, 'like', `data-post="${p.id}" aria-pressed="${liked}"`, liked ? 'j-liked' : '')}${jButton(`评论 ${jReplies(p).length}`, 'comments', `data-post="${p.id}"`)}<span>${p.author === 'self' ? '我的鱼获故事' : '点击卡片，看看钓友来信'}</span></footer></article>`;
  }).join('') : `<div class="j-feed-empty">${emptyHtml('还没有写下自己的鱼获故事。')} ${jButton('分享第一条鱼获', 'compose', '', 'primary')}</div>`}</div><div class="aside">${feedAside()}</div><div class="status">把收获分享给钓友，也把一句鼓励留在他们的手账里。</div>`;
}
function feedAside() {
  const p = jPost(J.feedSelected);
  if (!p) return `<div class="heading">写一封湖畔来信</div>${jPolaroid(`assets/fish/koi.png`, '让快乐多一份', 'dots')}<div class="detail">选一条背包里的鱼，写下这一刻的心情。好友和其他钓友都能在这里遇见你的故事。</div>${jButton('分享鱼获', 'compose', 'style="left:18px;top:420px;width:326px"', 'primary')}`;
  const author = jPlayer(p.author), replies = jReplies(p);
  return `<div class="heading">给 ${jEscape(author.name)} 的留言</div><div class="j-comment-summary"><img src="assets/fish/${p.fish}.png" alt="鱼获"><div><strong>${jFish(p.fish).name}</strong><span>${jEscape(p.pond)} · ${jReplies(p).length} 条评论</span></div></div>
  <div class="j-comments" aria-live="polite">${replies.length ? replies.map(c => `<div class="j-comment"><strong>${jEscape(c.name)}</strong><p>${jEscape(c.text)}</p></div>`).join('') : '<div class="j-no-comments">还没有留言，<br>送上第一句鼓励吧。</div>'}</div>
  <form id="j-comment-form" data-post="${p.id}"><label class="j-visually-hidden" for="j-comment-input">评论内容</label><input id="j-comment-input" maxlength="80" placeholder="写一句温暖的话…" required autocomplete="off"><button class="btn primary j-button" type="submit">留下鼓励</button><span class="j-comment-error" role="alert"></span></form>`;
}

/* ---------------- pond list ---------------- */
function jPonds() { return PONDS.filter(p => (S.tab.ponds !== 1 || !p.locked) && (S.tab.ponds !== 2 || p.fee === 0) && (!J.pondQuery || p.name.includes(J.pondQuery) || p.cat.label.includes(J.pondQuery) || WATER[p.water].includes(J.pondQuery))); }
function pondsBody() {
  const list = jPonds();
  if (!list.some(p => p.id === J.pondSelected)) J.pondSelected = list[0]?.id || null;
  return `<div class="mid-h">挑一片喜欢的水<span class="count" id="j-pond-count">${list.length} 个鱼塘</span><label class="j-search"><img src="${icon('map')}" alt=""><input id="j-pond-search" type="search" placeholder="搜索鱼塘 / 水域" value="${jEscape(J.pondQuery)}" aria-label="搜索鱼塘"></label></div><div class="mid j-pond-mid"><div class="grid c2 j-pond-grid">${jPondCards(list)}</div></div><div class="aside">${pondsAside()}</div><div class="status">鱼塘照片是旅行的起点。选好水域，再决定今天的回鱼方式。</div>`;
}
function jPondCards(list) {
  return list.length ? list.map((p, i) => `<button class="sticker j-pond-card ${J.pondSelected === p.id ? 'sel' : ''} ${p.locked ? 'j-pond-locked' : ''}" data-id="${p.id}" style="rotate:${tilt(p.id)}deg"><i class="tape lake"></i><div class="j-pond-photo"><img src="assets/${PONDS.indexOf(p) % 3 ? 'pond-afternoon.jpg' : 'home-sakura.jpg'}" style="object-position:${20 + i % 5 * 15}% 50%" alt="${p.name}"><span>${p.locked ? `Lv.${p.lv} 解锁` : '可进入'}</span></div><div class="name">${p.name}</div><div class="meta"><span style="color:${p.cat.color}">●</span>${p.cat.label} · ${WATER[p.water]}<b>${p.fee ? p.fee + ' 金币' : '免费'}</b></div></button>`).join('') + `<div class="j-list-end"><img src="${icon('fish')}" alt=""> 到底了，愿你今天满载而归。</div>` : '<div class="j-search-empty">没有找到这片水域。<br><small>试试“湖”“千岛湖”或“野外”。</small></div>';
}
function pondsAside() {
  const p = PONDS.find(p => p.id === J.pondSelected);
  if (!p) return `<div class="heading">鱼塘详情</div>${jPolaroid(icon('map'), '下一站，还没选好', 'lake')}<div class="detail">换一个搜索词试试，或者切到「全部鱼塘」看看。</div>`;
  const old = S.sel.map; S.sel.map = p.id; const html = mapAside(); S.sel.map = old;
  return html.replace('data-act="enter"', 'data-act="j-enter-list"').replace('assets/pond-afternoon.jpg', `assets/${PONDS.indexOf(p) % 3 ? 'pond-afternoon.jpg' : 'home-sakura.jpg'}`);
}

/* ---------------- local modal / actions ---------------- */
function jCloseDialog() {
  $('#j-dialog')?.remove();
  $$('#stage > [data-j-inert]').forEach(el => { el.inert = false; delete el.dataset.jInert; });
  if (journalModalFocus?.isConnected) journalModalFocus.focus();
  if (S.open && !pondEntering) startPageLoops(S.page, $('.content'));
}
function jDialog(title, html) {
  if (!$('#j-dialog')) journalModalFocus = document.activeElement;
  $('#j-dialog')?.remove();
  const modal = document.createElement('div'); modal.id = 'j-dialog'; modal.setAttribute('role', 'dialog'); modal.setAttribute('aria-modal', 'true'); modal.setAttribute('aria-labelledby', 'j-dialog-title');
  modal.innerHTML = `<section class="j-dialog-paper"><header><h2 id="j-dialog-title">${jEscape(title)}</h2><button type="button" class="j-dialog-close" aria-label="关闭">×</button></header>${html}</section>`;
  $('#stage').append(modal);
  $$('#stage > :not(#j-dialog)').forEach(el => { if (!el.inert) { el.inert = true; el.dataset.jInert = '1'; } });
  $('.j-dialog-close', modal).onclick = jCloseDialog;
  modal.addEventListener('click', e => { if (e.target === modal) jCloseDialog(); });
  modal.addEventListener('keydown', e => {
    if (e.key === 'Escape') { e.stopPropagation(); jCloseDialog(); }
    if (e.key === 'Tab') {
      const focus = $$('button:not([disabled]),input,textarea,select', modal), first = focus[0], last = focus.at(-1);
      if (e.shiftKey && document.activeElement === first) { last.focus(); e.preventDefault(); }
      else if (!e.shiftKey && document.activeElement === last) { first.focus(); e.preventDefault(); }
    }
  });
  play($('.j-dialog-paper', modal), [{ opacity: 0, translate: '0 16px' }, { opacity: 1, translate: '0 0' }], { duration: T.slide, easing: EASE.outCubic });
  $('.j-dialog-close', modal).focus();
  return modal;
}
function jFishPicker(mode) {
  const list = mode === 'showcase' ? FISH : journalFish.filter(f => !J.album.includes(f.id));
  const modal = jDialog(mode === 'showcase' ? `选择展示位 ${J.showcaseSelected + 1} 的鱼获` : '把一张回忆钉进相册', `<p class="j-dialog-sub">${mode === 'showcase' ? '来自你的背包，选择后立即展示。' : '已经钉选的回忆不会重复加入。'}</p><div class="j-picker-grid">${list.length ? list.map(f => `<button class="j-picker-fish" data-pick="${f.id}"><img src="assets/fish/${f.id}.png" alt="${f.name}"><strong>${f.name}</strong><span>${f.size.toFixed(2)} m</span></button>`).join('') : '<p>暂时没有可选鱼获，下一次相遇会带来新故事。</p>'}</div>`);
  $$('[data-pick]', modal).forEach(b => b.onclick = () => {
    if (mode === 'showcase') { J.showcase[J.showcaseSelected] = b.dataset.pick; }
    else { J.album.push(b.dataset.pick); J.albumPage = Math.floor((J.album.length - 1) / 2); J.albumSelected = b.dataset.pick; }
    jSave(); jCloseDialog(); jRefresh(); toast(mode === 'showcase' ? '已经摆进展示柜啦' : '新回忆已经钉进相册');
  });
}
function jCompose() {
  if (!FISH.length) { toast('背包还没有鱼获，先去湖边钓一条吧', 'error'); return; }
  const modal = jDialog('分享今天的鱼获', `<form id="j-compose-form"><label for="j-post-fish">挑一条鱼</label><select id="j-post-fish">${FISH.map(f => `<option value="${f.id}">${f.name} · ${f.size.toFixed(2)} m</option>`).join('')}</select><label for="j-post-text">这一刻的心情</label><textarea id="j-post-text" maxlength="160" placeholder="今天在湖边，发生了什么开心事？" required></textarea><label for="j-post-visibility">谁可以看到</label><select id="j-post-visibility"><option value="public">所有钓友</option><option value="friends">仅好友</option></select><p class="j-compose-error" role="alert"></p><button type="submit" class="btn primary j-button">贴进动态手账</button><small class="j-local-note">预览内容仅保存在本浏览器，不会发送到真实钓友。</small></form>`);
  $('#j-compose-form', modal).onsubmit = e => {
    e.preventDefault(); const text = $('#j-post-text').value.trim();
    if (!text) { $('.j-compose-error').textContent = '写一句话，再和大家分享吧。'; return; }
    const post = { id: `local-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`, author: 'self', fish: $('#j-post-fish').value, text, likes: 0, time: '刚刚', pond: '千岛湖', visibility: $('#j-post-visibility').value, friends: true, replies: [] };
    J.posts.unshift(post); J.feedSelected = post.id; S.tab.feed = 2; jSave(); jCloseDialog(); jRefresh(); toast('鱼获故事已贴进手账');
  };
}
async function jEnterList() {
  if (pondEntering) return;
  const p = PONDS.find(p => p.id === J.pondSelected);
  if (!p || p.locked) return;
  if (p.fee > S.coins) { toast(`还差 ${p.fee - S.coins} 金币，先去免费鱼塘吧`, 'error'); return; }
  pondEntering = true; stopPageLoops(); bobFloat.pause();
  const modal = jDialog(`出发去${p.name}`, `<div class="j-departure"><div class="j-depart-bobber">${bobberSvg()}</div><p>浮漂轻轻落水，出发啦…</p><div class="j-depart-ready" hidden><img src="assets/pond-afternoon.jpg" alt="${p.name}"><p>${p.theme} · ${WATER[p.water]} · ${S.feeMode ? '自动回鱼' : '回鱼出售'}</p><small>已完成鱼塘进入预览，实际游戏会在这里打开桌面鱼塘。</small>${jButton('回到手账', 'return-list', '', 'primary')}</div></div>`);
  $('.j-dialog-close', modal).hidden = true;
  const animation = play($('.j-depart-bobber', modal), [{ translate: '0 0' }, { translate: '0 16px', offset: .35 }, { translate: '0 -4px', offset: .7 }, { translate: '0 0' }], { duration: T.celebrate || 900, easing: EASE.inOutSine });
  await animation.finished;
  pondEntering = false;
  if (!modal.isConnected) return;
  $('.j-dialog-close', modal).hidden = false;
  $('.j-depart-bobber', modal).hidden = true;
  $('.j-departure > p', modal).textContent = `已经抵达 ${p.name}`;
  $('.j-depart-ready', modal).hidden = false;
  $('[data-act="j-return-list"]', modal).onclick = () => { jCloseDialog(); startPageLoops(S.page, $('.content')); };
  $('[data-act="j-return-list"]', modal).focus();
}
function jPlayerCard(id) {
  if (id === 'self') { go('profile', { tab: 0 }); return; }
  const p = jPlayer(id), f = jFish(p.fish), followed = J.followed.includes(id);
  const modal = jDialog(`${p.name}的钓鱼手账`, `<div class="j-player-card"><img class="j-player-face" src="${jAvatar(p.avatar)}" alt="${p.name}"><h3>${p.name} <small>Lv.${p.lv}</small></h3><p>${p.bio}</p><div class="j-player-fish"><img src="assets/fish/${f.id}.png" alt="${f.name}"><div><strong>心头好 · ${f.name}</strong><p>${f.size.toFixed(2)} m · ${'★'.repeat(f.stars)}</p></div></div><div class="j-player-records"><span><b>${p.pond}</b>累计鱼获</span><span><b>${p.rare}</b>稀有收藏</span><span><b>${p.daily.toFixed(2)} m</b>今日大鱼</span></div><button class="btn primary j-button" id="j-follow">${followed ? '已关注 ✓' : '关注这位钓友'}</button><small class="j-local-note">关注关系仅用于本地预览。</small></div>`);
  $('#j-follow', modal).onclick = e => { const has = J.followed.includes(id); J.followed = has ? J.followed.filter(x => x !== id) : [...J.followed, id]; jSave(); e.currentTarget.textContent = has ? '关注这位钓友' : '已关注 ✓'; };
}
function jAction(name, el) {
  if (name === 'player-card') return jPlayerCard(el.dataset.player);
  if (name === 'edit-profile') { J.editing = true; jRefresh(); $('#j-nickname').focus(); }
  if (name === 'cancel-edit') { J.editing = false; jRefresh(); }
  if (name === 'profile-tab') { J.editing = false; switchTab(+el.dataset.tabTarget); }
  if (name === 'pick-showcase') return jFishPicker('showcase');
  if (name === 'clear-showcase') { J.showcase[J.showcaseSelected] = null; jSave(); jRefresh(); toast('展示格已清空，鱼获仍然在背包里'); }
  if (name === 'album-page') { J.albumPage += +el.dataset.step; jRefresh(); }
  if (name === 'album-add') return jFishPicker('album');
  if (name === 'album-remove') { J.album = J.album.filter(id => id !== J.albumSelected); jSave(); jRefresh(); toast('已取消钉选，鱼获和图鉴记录仍然保留'); }
  if (name === 'open-ponds') return go('ponds');
  if (name === 'open-map') { S.sel.map = J.pondSelected; go('map'); }
  if (name === 'compose') return jCompose();
  if (name === 'like') {
    const id = el.dataset.post, was = J.liked.includes(id); J.liked = was ? J.liked.filter(x => x !== id) : [...J.liked, id];
    jSave(); const p = jPost(id); el.textContent = `${was ? '♡ 点赞' : '♥ 已赞'} ${p.likes + (was ? 0 : 1)}`; el.classList.toggle('j-liked', !was); el.setAttribute('aria-pressed', String(!was));
    play(el, [{ scale: '1.1' }, { scale: '1' }], { duration: T.snap, easing: EASE.outBack });
  }
  if (name === 'comments') { J.feedSelected = el.dataset.post; jSelect('feed', J.feedSelected); $('#j-comment-input')?.focus(); }
  if (name === 'enter-list') return jEnterList();
}
function jSelect(id, key) {
  if (id === 'rank') J.rankSelected = key;
  if (id === 'feed') J.feedSelected = key;
  if (id === 'ponds') J.pondSelected = key;
  if (id === 'profile') {
    if (S.tab.profile === 1) J.showcaseSelected = Number(key.replace('slot-', ''));
    if (S.tab.profile === 2) J.codexSelected = key;
    if (S.tab.profile === 3) J.albumSelected = key;
    if (![1, 2, 3].includes(S.tab.profile)) return;
  }
  $$('.mid .sticker', $('.content')).forEach(c => c.classList.toggle('sel', c.dataset.id === key));
  const tape = $(`.mid .sticker[data-id="${key}"] .tape`);
  if (tape) play(tape, [{ scale: '0 1' }, { scale: '1 1' }], { duration: 160, easing: EASE.outCubic });
  refreshAside(id);
  if (id === 'profile' && S.tab.profile === 1) startPageLoops(id, $('.content'));
}
function jBindForms(root) {
  const form = $('#j-profile-form', root);
  if (form) {
    const preview = () => { $('.j-edit-preview .photo img').src = jAvatar($('#j-avatar-value').value); $('.j-edit-preview .caption').textContent = $('#j-nickname').value || '你的名字'; $('#j-bio-preview').textContent = $('#j-bio').value; };
    $$('[data-avatar]', form).forEach(b => b.onclick = () => { $('#j-avatar-value').value = b.dataset.avatar; $$('[data-avatar]', form).forEach(x => { x.classList.toggle('sel', x === b); x.setAttribute('aria-pressed', String(x === b)); }); preview(); });
    $('#j-nickname').oninput = preview; $('#j-bio').oninput = preview;
    form.onsubmit = e => { e.preventDefault(); const name = $('#j-nickname').value.trim(); if (!name) { $('#j-form-error').textContent = '昵称不能为空。'; return; } J.nickname = name; J.bio = $('#j-bio').value.trim(); J.avatar = $('#j-avatar-value').value; J.editing = false; jSave(); jSyncPlayer(); jRefresh(); toast('资料已经记进手账啦'); };
  }
  const comments = $('#j-comment-form', root);
  if (comments) comments.onsubmit = e => {
    e.preventDefault(); const input = $('#j-comment-input'), value = input.value.trim();
    if (!value) { $('.j-comment-error').textContent = '先写一句话吧。'; return; }
    const id = comments.dataset.post; J.comments[id] = [...(J.comments[id] || []), { name: J.nickname, text: value }]; jSave();
    refreshAside('feed'); const p = jPost(id), btn = $(`[data-act="j-comments"][data-post="${id}"]`); if (btn) btn.textContent = `评论 ${jReplies(p).length}`;
    const list = $('.j-comments'); list.scrollTop = list.scrollHeight; $('#j-comment-input').focus(); toast('鼓励已经留在钓友的手账里');
  };
  const search = $('#j-pond-search', root);
  if (search) search.oninput = () => {
    J.pondQuery = search.value.trim(); const list = jPonds(); if (!list.some(p => p.id === J.pondSelected)) J.pondSelected = list[0]?.id || null;
    $('.j-pond-grid').innerHTML = jPondCards(list); $('#j-pond-count').textContent = `${list.length} 个鱼塘`; $('.j-pond-mid').scrollTop = 0;
    $$('.j-pond-card').forEach(c => c.onclick = () => jSelect('ponds', c.dataset.id)); jObservePonds(); refreshAside('ponds');
  };
}
function jObservePonds() {
  pondObserver?.disconnect();
  const mid = $('.j-pond-mid'); if (!mid) return;
  pondObserver = new IntersectionObserver(entries => {
    let staggerIndex = 0;
    entries.forEach(entry => {
      if (!entry.isIntersecting) return;
      const card = entry.target;
      if (!J.pondSeen.has(card.dataset.id)) {
        J.pondSeen.add(card.dataset.id);
        play(card, [{ opacity: 0, scale: '1.12', rotate: `${tilt(card.dataset.id) + 6}deg` }, { opacity: 1, scale: '1', rotate: `${tilt(card.dataset.id)}deg` }], { duration: T.snap, delay: Math.min(staggerIndex++, 7) * T.stagger, easing: EASE.outBack });
      }
      pondObserver.unobserve(card);
    });
  }, { root: mid, threshold: .12 });
  $$('.j-pond-card', mid).forEach(c => pondObserver.observe(c));
}

function installJournalPages() {
  journalFish = FISH.map(f => ({ ...f })); jRestore();
  Object.assign(S.tab, { rank: 0, profile: 0, feed: 0, ponds: 0 });
  Object.assign(PAGES.rank, { tabs: [{ label: '今日大鱼', icon: 'fish' }, { label: '每周钓王', icon: 'trophy' }, { label: '当前鱼塘', icon: 'pond' }, { label: '稀有鱼获', icon: 'star' }] });
  Object.assign(PAGES.profile, { tabs: [{ label: '资料', icon: 'user' }, { label: '展示柜', icon: 'fish' }, { label: '图鉴', icon: 'map' }, { label: '相册', icon: 'heart' }, { label: '成就', icon: 'medal' }] });
  Object.assign(PAGES.feed, { tabs: [{ label: '公开动态', icon: 'social' }, { label: '好友动态', icon: 'heart' }, { label: '我的动态', icon: 'user' }] });
  PAGES.ponds = { title: '选择鱼塘', sub: '挑一片水，坐下来慢慢钓', icon: 'pond', accent: '#5DADE2', compare: 'worldmap.jpg', tabs: [{ label: '全部鱼塘', icon: 'map' }, { label: '可进入', icon: 'check' }, { label: '免费鱼塘', icon: 'leaf' }] };
  Object.assign(BODY, { rank: rankBody, profile: profileBody, feed: feedBody, ponds: pondsBody });
  for (const id of ['rank', 'profile', 'feed']) delete PAGES[id].empty;
  const oldBind = bindPage, oldSelect = selectCard, oldAside = refreshAside, oldAct = act, oldStagger = stagger, oldRender = render;
  render = function(id) { pondObserver?.disconnect(); $('.content').dataset.journalPage = id; oldRender(id); };
  bindPage = function(id, root) {
    oldBind(id, root); jBindForms(root); if (id === 'ponds') { bindFee($('.aside', root)); jObservePonds(); }
    if (id === 'map') {
      $('.rail .note', root).innerHTML = `拖动地图浏览<br>点浮标查看鱼塘${jButton('列表选塘', 'open-ponds', '', 'primary')}`;
      $('[data-act="j-open-ponds"]', root).onclick = () => go('ponds');
    }
    $$('.tab, .sticker, .navtab, .btn[data-act]', root).forEach(el => { if (el.tagName !== 'BUTTON') { el.setAttribute('role', 'button'); el.tabIndex = 0; } });
  };
  stagger = function(root) { if (S.page !== 'ponds') oldStagger(root); };
  selectCard = function(id, card) { if (['rank', 'profile', 'feed', 'ponds'].includes(id)) return jSelect(id, card.dataset.id); return oldSelect(id, card); };
  refreshAside = function(id) {
    const fn = id === 'rank' ? rankAside : id === 'feed' ? feedAside : id === 'ponds' ? pondsAside : id === 'profile' ? ({ 1: showcaseAside, 2: codexAside, 3: albumAside })[S.tab.profile] : null;
    if (!fn) return oldAside(id);
    const aside = $('.aside'); if (!aside) return; aside.innerHTML = fn();
    $$('[data-act]', aside).forEach(b => b.onclick = () => act(b.dataset.act, b));
    if (id === 'ponds') bindFee(aside); jBindForms(aside);
    play($('.polaroid', aside), [{ opacity: .3, scale: '.9' }, { opacity: 1, scale: '1' }], { duration: T.snap, easing: EASE.outBack });
  };
  act = function(name, el) { if (name.startsWith('j-')) { wake(); return jAction(name.slice(2), el); } return oldAct(name, el); };
  startPageLoops = function(id, root) {
    stopPageLoops();
    if (reduced || document.hidden) { bobFloat.pause(); return; }
    const fish = id === 'profile' && S.tab.profile === 1 ? $('.j-showcase-slot.sel .j-showcase-fish', root) : null;
    const buoy = id === 'map' ? $(`.buoy[data-id="${S.sel.map}"]:not(.locked) .body`, root) || $('.buoy:not(.locked) .body', root) : null;
    const sleeper = $('.sleeper', root);
    if (fish) pageLoops.push(play(fish, [{ rotate: '-3deg' }, { rotate: '3deg' }], { duration: T.float / 2, iterations: Infinity, direction: 'alternate', easing: EASE.inOutSine }));
    else if (buoy) pageLoops.push(play(buoy, [{ translate: '0 -3px', rotate: '-4deg' }, { translate: '0 3px', rotate: '4deg' }], { duration: T.float / 2, iterations: Infinity, direction: 'alternate', easing: EASE.inOutSine }));
    else if (sleeper) pageLoops.push(play(sleeper, [{ scale: '1 1' }, { scale: '1.02 .97' }], { duration: 1500, iterations: Infinity, direction: 'alternate', easing: EASE.inOutSine }));
    if (pageLoops.length) bobFloat.pause(); else if (bobIdx !== null) bobFloat.play();
  };
  $('#player').addEventListener('click', () => go('profile', { tab: 0 }));
  document.addEventListener('keydown', e => {
    const editable = e.target.closest('input, textarea, select, [contenteditable=true]');
    if (editable) return;
    const button = e.target.closest('[role=button]');
    if (button && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); button.click(); }
  });
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) { bobFloat.pause(); pageLoops.forEach(a => a.pause()); catBreath.pause(); }
    else if (S.open) startPageLoops(S.page, $('.content'));
  });
  $('#reduce').addEventListener('click', () => document.body.classList.toggle('j-reduced', !reduced));
  reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
  document.body.classList.toggle('j-reduced', reduced);
  $('#reduce').textContent = `减少动效：${reduced ? '开' : '关'}`; $('#reduce').classList.toggle('on', reduced);
  const requested = new URLSearchParams(location.search).get('page');
  if (requested && PAGES[requested]) {
    S.page = requested;
    const navIdx = NAV.findIndex(p => p.id === requested);
    if (navIdx >= 0) bobIdx = navIdx;
  }
  jSyncPlayer();
  [...AVATARS.map(([id]) => jAvatar(id)), ...journalFish.map(f => `assets/fish/${f.id}.png`)].forEach(src => { const img = new Image(); img.src = src; img.decode?.().catch(() => {}); });
}

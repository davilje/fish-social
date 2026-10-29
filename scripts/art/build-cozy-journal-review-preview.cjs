const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

const root = path.resolve(__dirname, '../..');
const defaultOutput = path.join(root, 'docs/美术/手账/production');
const groups = [
  ['actions', '按钮与间距'], ['details', '详情内容与弹窗'],
  ['profiles', '排行榜与资料'], ['pages', '全部正式页面']
];
const languages = { 'zh-CN': '中文', 'en-US': 'English', shared: '通用' };
const states = { default: '默认', hover: '悬停', pressed: '按下', disabled: '禁用',
  summary: '摘要', dialog: '弹窗', bottom: '长文底部', browse: '浏览', page: '页面' };
const pageTitles = { PanelShop: '商店', PanelEquipment: '装备', PanelCatch: '背包', feed: '评论删除' };

function captureTitle(group, stem) {
  const rest = stem.replace(/-(zh-CN|en-US)/, '');
  if (group === 'actions') {
    const [page, state] = rest.split('-');
    return [pageTitles[page] + ' · ' + states[state], state];
  }
  if (group === 'details') {
    const [page, ...parts] = rest.split('-');
    const last = parts.at(-1);
    const state = last === 'bottom' ? 'bottom' : last === 'browse' ? 'browse' : last;
    const subtype = page === 'PanelCatch' ? ({ one: '单条鱼获', two: '另一条鱼获' }[parts[0]] || '')
      : ({ '0': '钓竿', '1': '鱼饵', '2': '船具', rod: '钓竿' }[parts[0]] || '');
    return [[pageTitles[page], subtype, states[state]].filter(Boolean).join(' · '), state];
  }
  if (rest.startsWith('rank-')) {
    const title = { Daily: '日榜', Weekly: '周榜', Pond: '鱼塘榜', Rare: '稀有榜' }[rest.slice(5)];
    return ['排行榜 · ' + title, 'page'];
  }
  return [rest.startsWith('public-') ? '他人资料 · ' + rest.slice(7) : '本人资料', 'page'];
}

function buildReview({ output = defaultOutput, captureRoot } = {}) {
  const manifestPath = path.join(output, 'review-manifest.json');
  let captures = fs.existsSync(manifestPath)
    ? JSON.parse(fs.readFileSync(manifestPath, 'utf8')).captures : [];
  if (captureRoot) {
    captures = [];
    for (const [folder, group] of [
      ['compact-actions-2026-10-30', 'actions'],
      ['item-details-2026-10-30', 'details'],
      ['rank-profile-repair-2026-10-29', 'profiles']
    ]) {
      const source = path.join(captureRoot, folder);
      const target = path.join(output, 'reviews', folder);
      fs.mkdirSync(target, { recursive: true });
      for (const name of fs.readdirSync(source).filter(name => name.endsWith('.png')).sort()) {
        const stem = name.slice(0, -4);
        const language = stem.includes('zh-CN') ? 'zh-CN' : 'en-US';
        const [title, state] = captureTitle(group, stem);
        const bytes = fs.readFileSync(path.join(source, name));
        fs.writeFileSync(path.join(target, name), bytes);
        captures.push({ id: group + '-' + stem, title, group, language, state,
          file: 'reviews/' + folder + '/' + name,
          sha256: crypto.createHash('sha256').update(bytes).digest('hex') });
      }
    }
  }
  const base = JSON.parse(fs.readFileSync(path.join(output, 'evidence.json'), 'utf8')).pages;
  const current = {
    PanelShop: 'actions-PanelShop-zh-CN-default', PanelCatch: 'actions-PanelCatch-zh-CN-default',
    PanelEquipment: 'actions-PanelEquipment-zh-CN-default', PanelSocialFeed: 'actions-feed-zh-CN-default',
    PanelLeaderboard: 'profiles-rank-zh-CN-Daily', PanelProfileHub: 'profiles-self-zh-CN'
  };
  const pages = base.map(page => {
    const updated = captures.find(capture => capture.id === current[page.id]);
    return { id: page.id, title: page.title, group: 'pages', language: updated ? 'zh-CN' : 'shared',
      state: 'page', file: updated ? updated.file : page.file };
  });
  const records = [...captures, ...pages];
  for (const record of records) {
    if (!fs.existsSync(path.join(output, record.file))) throw new Error('Missing preview: ' + record.file);
  }
  fs.writeFileSync(manifestPath, JSON.stringify({ generatedAt: new Date().toISOString(), captures, pages }, null, 2) + '\n');
  const payload = JSON.stringify({ records, groups, languages, states }).replace(/</g, '\\u003c');
  const html = fs.readFileSync(path.join(__dirname, 'journal-review-preview.html'), 'utf8')
    .replace('/* PREVIEW_DATA */', 'const previewData = ' + payload + ';');
  fs.writeFileSync(path.join(output, 'index.html'), html);
  console.log(JSON.stringify({ preview: path.join(output, 'index.html'), captures: captures.length, pages: pages.length }));
}

module.exports = { buildReview };
if (require.main === module) {
  const index = process.argv.indexOf('--captures');
  buildReview({ captureRoot: index >= 0 ? path.resolve(process.argv[index + 1]) : undefined });
}

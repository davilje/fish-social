import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {marked} from '/Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/marked/lib/marked.esm.js';

const out=path.dirname(fileURLToPath(import.meta.url));
const spec=path.resolve(out,'../../策划/规格/玩法/鱼塘成长补鱼与个体履历.md');
const parts=[
  ['current-rules','现行规则',path.resolve(out,'../../策划/规格/玩法/钓鱼系统现行规则.md')],
  ['implementation','实现与验证',path.join(out,'实现与验证报告.md')],
  ['pending','未完成事项',path.resolve(out,'../../策划/报告/FISH-ECOLOGY-01-未完成事项与后续交接.md')],
  ['simulation','24小时／30天',path.join(out,'implementation-simulation.md')],
  ['report','曲线与数据',path.join(out,'入塘口径修订.md')],
  ['spec','最终规格',spec],
  ['checklist','修改清单',path.join(out,'完整修改清单.md')],
  ['options','B / E0 / L1',path.join(out,'额外成长与旧鱼兼容选项.md')],
];
const esc=s=>s.replaceAll('&','&amp;').replaceAll('"','&quot;').replaceAll('<','&lt;');
function render(file){
  return marked.parse(fs.readFileSync(file,'utf8')).replace(/(href|src)="([^"#]+)"/g,(all,attr,url)=>{
    if (/^(https?:|data:|mailto:|\/)/.test(url)) return all;
    return `${attr}="${esc(path.relative(out,path.resolve(path.dirname(file),url)))}"`;
  });
}
const html=`<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>鱼群成长 · 实现与生态验证</title><style>
:root{--bg:#f4f5ef;--paper:#fffef9;--ink:#263e36;--muted:#66776d;--line:#d9e2d7;--green:#297763}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font:16px/1.85 -apple-system,BlinkMacSystemFont,'PingFang SC',sans-serif}header,main,footer{max-width:1220px;margin:auto;padding:30px clamp(18px,4vw,56px)}header{padding-top:48px;padding-bottom:20px}.eyebrow{color:var(--green);font-size:12px;letter-spacing:2px}h1{font-size:clamp(27px,3.5vw,42px);line-height:1.4;margin:12px 0}header p{color:var(--muted);max-width:870px}.stats{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-top:25px}.stat{padding:15px 20px;background:var(--paper);border:1px solid var(--line);border-radius:12px}.stat b{display:block;color:var(--green);font-size:24px}.stat span{font-size:12px;color:var(--muted)}nav{position:sticky;top:0;background:var(--bg);z-index:5;border-bottom:1px solid var(--line);padding:12px 0;display:flex;gap:8px;overflow:auto}button{flex:none;cursor:pointer;color:var(--green);border:1px solid var(--line);border-radius:9px;background:var(--paper);padding:10px 16px;font:inherit;font-size:14px}button[aria-selected=true]{background:var(--green);color:white;border-color:var(--green)}article{padding:8px 0 20px}article>h1{font-size:28px}h2{font-size:24px;border-top:1px solid var(--line);margin-top:40px;padding-top:23px;line-height:1.5}h3{font-size:19px;margin-top:27px}p{margin:16px 0}a{color:var(--green);text-underline-offset:4px;overflow-wrap:anywhere}img{display:block;max-width:100%;height:auto;margin:25px auto;border:1px solid var(--line);border-radius:12px}.table-wrap{overflow-x:auto;border:1px solid var(--line);border-radius:10px;margin:22px 0}table{width:100%;border-collapse:collapse;font-size:13px;background:var(--paper)}th,td{padding:12px 14px;text-align:left;border-bottom:1px solid var(--line);min-width:95px;vertical-align:top}th{background:#e8eee3}tr:last-child td{border-bottom:0}li{margin:8px 0}pre{overflow:auto;padding:18px;background:#e8eee4;border-radius:10px;font-size:13px}code{font-size:.88em;overflow-wrap:anywhere}blockquote{margin-left:0;border-left:3px solid #94b49e;padding-left:20px;color:var(--muted)}.history{border-top:1px solid var(--line);padding-top:20px;color:var(--muted);font-size:14px}.history summary{cursor:pointer;color:var(--green)}.notice{border-left:3px solid #b49358;background:#f4edde;padding:12px 16px;font-size:13px}.foot{font-size:12px;color:var(--muted);padding-top:0}[hidden]{display:none!important}@media(max-width:650px){header{padding-top:25px}.stats{gap:7px}.stat{padding:12px 10px}.stat b{font-size:20px}.stat span{font-size:11px}main{padding-top:5px}body{font-size:15px}button{padding:9px 12px}h2{font-size:22px}article>h1{font-size:24px}th,td{padding:10px}}@media print{nav,.history{display:none}article[hidden]{display:block!important}header,main{padding:0}.stats{break-inside:avoid}h2{break-after:avoid}img{break-inside:avoid}body{background:white}}
</style></head><body><header><div class="eyebrow">FISH-ECOLOGY-01 / 2026.11.04 / 本地已实现 · 未部署</div><h1>记录一条鱼，如何在塘里长大</h1><p>以投放体长和入塘日期建立独立履历。开服鱼拥有前14天的入塘批次；自然成长、吃饵加速与真实在塘时间分别记录。</p><div class="stats"><div class="stat"><b>3.4～4.6 cm</b><span>幼鱼基准规格 · 固定个体差异</span></div><div class="stat"><b>−14～0 天</b><span>开服初始鱼的历史入塘范围</span></div><div class="stat"><b>±15%</b><span>独立于投放规格的品质上限波动</span></div></div></header><main><nav role="tablist" aria-label="方案内容">${parts.map(([id,title],i)=>`<button role="tab" id="tab-${id}" aria-controls="${id}" aria-selected="${i===0}" data-panel="${id}">${title}</button>`).join('')}</nav>${parts.map(([id,title,file],i)=>`<article role="tabpanel" aria-labelledby="tab-${id}" id="${id}" ${i?'hidden':''}>${render(file)}</article>`).join('')}<details class="history"><summary>历史推演与资料（参数有差异）</summary><p class="notice">以下捕捞与占用率推演保留旧参数，含稀有T80=4天及固定4厘米出生口径，仅供追溯；本版重跑结果见“24小时／30天”。历史推演不能作为新方案已经上线的证明。</p><p><a href="历史推演.html">此前完整图表与捕捞推演</a> · <a href="历史报告-出生口径.md">历史Markdown</a> · <a href="../../策划/提示词/玩法/fish-ecology-entry-growth-dev.prompt.md">开发交接</a></p></details></main><footer class="foot">本地业务实现与隔离库验证完成；未部署，未对实际测试库执行备份、归档或清塘。</footer><script>
const ids=${JSON.stringify(parts.map(([id])=>id))};
function select(id){if(!ids.includes(id))id='implementation';document.querySelectorAll('[role=tabpanel]').forEach(el=>el.hidden=el.id!==id);document.querySelectorAll('[data-panel]').forEach(el=>el.setAttribute('aria-selected',String(el.dataset.panel===id)));}
document.querySelectorAll('[data-panel]').forEach(button=>button.addEventListener('click',()=>{history.replaceState(null,'','#'+button.dataset.panel);select(button.dataset.panel)}));
window.addEventListener('hashchange',()=>select(location.hash.slice(1)));select(location.hash.slice(1));
document.querySelectorAll('table').forEach(table=>{const wrap=document.createElement('div');wrap.className='table-wrap';table.before(wrap);wrap.append(table)});
</script></body></html>`;
fs.writeFileSync(path.join(out,'index.html'),html);
fs.writeFileSync(path.join(out,'报告.md'),fs.readFileSync(path.join(out,'实现与验证报告.md'),'utf8')+'\n\n'+fs.readFileSync(path.join(out,'入塘口径修订.md'),'utf8')+'\n\n'+fs.readFileSync(spec,'utf8'));
console.log(JSON.stringify({output:'index.html',panels:parts.length,bytes:Buffer.byteLength(html)}));

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const sharp = require('sharp');
const root = path.resolve(__dirname, '../..');
const source = path.join(root, 'fish-social-unity/Library/CozyRedesignPreviews');
const output = path.join(root, 'docs/art/cozy-journal/production');
const pages = [
 ['PanelShop','商店'],['PanelWorldMap','世界地图'],['PanelPondSelection','选择鱼塘'],
 ['PanelCatch','背包'],['PanelEquipment','装备'],['PanelAchievements','成就'],
 ['PanelLeaderboard','排行榜'],['PanelProfileHub','个人资料'],['PanelProfileEdit','编辑资料'],
 ['PanelProfileEdit-展示鱼获','编辑展示鱼获'],['PanelProfileEdit-选择鱼获','选择展示鱼获'],['PanelPondSettlement','离塘结算'],
 ['PanelProfileHub-展示柜','展示柜'],['PanelProfileHub-图鉴','图鉴'],['PanelProfileHub-相册','相册'],
 ['PanelSocialFeed','钓友动态'],['PanelSocialFeed-发布','发布鱼获'],['PanelSocial','我的钓友'],
 ['PanelSocial-好友申请','好友申请'],['PanelSocial-私信','私信'],['PanelSocial-鱼塘聊天','鱼塘聊天'],['PanelSocial-消息通知','消息通知'],
 ['PanelSettings','设置'],['PanelGallery','独立图鉴']
];
(async () => {
 fs.mkdirSync(output, {recursive:true});
 const records=[];
 for (const [id,title] of pages) {
  const bytes=fs.readFileSync(path.join(source,id+'.png'));
  await sharp(bytes).webp({quality:95}).toFile(path.join(output,id+'.webp'));
  records.push({id,title,file:id+'.webp',source:'fish-social-unity/Library/CozyRedesignPreviews/'+id+'.png',sourceSha256:crypto.createHash('sha256').update(bytes).digest('hex')});
 }
 const tests=fs.readFileSync(path.join(root,'fish-social-unity/Temp/CozyRedesign.tests.txt'),'utf8').trim();
 const guard=fs.readFileSync(path.join(root,'fish-social-unity/Library/CozyRedesign.validation.txt'),'utf8').trim();
 fs.writeFileSync(path.join(output,'evidence.json'),JSON.stringify({generatedAt:new Date().toISOString(),renderer:'Unity 2021.3.29f1c1, saved production prefabs, native uGUI',fixture:'Editor example DTOs rendered through production presenters, actual pond catalog and art; not live account acceptance',resolution:[1672,941],unityEditMode:tests,prefabGuard:guard,platformValidation:'Windows build, Steam live sessions and DPI pending; host has MacStandaloneSupport only',pages:records},null,2)+'\n');
 fs.writeFileSync(path.join(output,'index.html'),`<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>猫咪钓鱼手账 · Unity 正式页面</title><style>*{box-sizing:border-box}body{margin:0;background:#29241f;color:#f5e8d4;font:16px system-ui,sans-serif}header{padding:22px 28px 10px}h1{font-size:26px;font-weight:500;margin:0 0 10px}p{color:#c8b79e;line-height:1.7;margin:8px 0}a{color:#ffc987}nav{display:flex;gap:8px;flex-wrap:wrap;padding:14px 28px}button{font:inherit;padding:9px 14px;border:1px solid #73614d;border-radius:10px;background:#40372e;color:#e9dac3;cursor:pointer}button[aria-pressed=true]{background:#ffa051;border-color:#ffa051;color:#35271b}button:focus-visible,a:focus-visible{outline:3px solid #7bbcdf;outline-offset:3px}.viewer{margin:8px 16px 20px}img{display:block;width:100%;max-width:1672px;height:auto;margin:auto;border-radius:10px;background:#fff7e9}.full img{width:1672px;max-width:none}.full .viewer{overflow:auto}footer{padding:0 28px 28px;line-height:1.8;color:#c8b79e}.tools{display:flex;gap:16px;align-items:center;flex-wrap:wrap}small{font-size:13px}</style><header><h1>猫咪钓鱼手账 · Unity 正式页面</h1><p>正式预制体实际渲染 · 原生文字、按钮与滚动控件 · 1672 × 941</p><div class="tools"><a href="../../../design/ui/prototype/cozy-journal/index.html">查看原版交互预览 ↗</a><a href="workspace.html">新版资料与收获对照</a><a href="parity.html">原方案对照</a><a href="motion.html">动效逐帧对照</a><a href="../../../design/ui/prototype/social-refinement/index.html">新社交交互预览</a><a href="../../../design/ui/prototype/journal-workspace/index.html">新版资料与收获交互预览</a><a href="evidence.json">验证记录</a><button id="fit">原尺寸查看</button></div></header><nav aria-label="选择页面">${records.map((p,i)=>`<button data-index="${i}" aria-pressed="${i===0}">${p.title}</button>`).join('')}</nav><main class="viewer"><img id="preview" src="${records[0].file}" alt="${records[0].title} · Unity正式页面"></main><footer><strong id="caption">${records[0].title}</strong><p>这些是正式 Unity 页面截图，业务内容使用编辑器样例数据；游戏内操作连接真实接口。此处支持切换截图，不模拟游戏按钮。← → 可切页。</p><small>自动检查：${tests.split('\n')[0]}；预制体构建守卫：${guard}。Windows / Steam 联机与 DPI 待平台验收。</small></footer><script>const pages=${JSON.stringify(records)},buttons=[...document.querySelectorAll('[data-index]')];let selected=0;function show(i){selected=(i+pages.length)%pages.length;const p=pages[selected];document.getElementById('preview').src=p.file;document.getElementById('preview').alt=p.title+' · Unity正式页面';document.getElementById('caption').textContent=p.title;buttons.forEach((b,j)=>b.setAttribute('aria-pressed',j===selected));history.replaceState(null,'','#'+encodeURIComponent(p.id))}buttons.forEach((b,i)=>b.onclick=()=>show(i));document.getElementById('fit').onclick=()=>{document.body.classList.toggle('full');document.getElementById('fit').textContent=document.body.classList.contains('full')?'适应窗口':'原尺寸查看'};document.addEventListener('keydown',e=>{if(e.key==='ArrowRight')show(selected+1);if(e.key==='ArrowLeft')show(selected-1)});const initial=pages.findIndex(p=>p.id===decodeURIComponent(location.hash.slice(1)));if(initial>=0)show(initial);</script></html>`);
 console.log('Published local production preview: '+records.length+' states');
})().catch(error=>{console.error(error);process.exitCode=1;});

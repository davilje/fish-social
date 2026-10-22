'use strict';
// Local design sandbox. Existing game shell and motion remain the source of truth.
// All prototype changes live only in this tab; leave the original preview storage untouched.
jSave=function(){};
jRestore=function(){};
const ws = { draft:null, editTab:'avatars', composer:null, filter:false, sort:'time', slots:['koi','golden_line','mandarin',null,null,null,null,null] };
const wsAvatars=[...AVATARS,['cream','奶油猫'],['cow','燕尾猫'],['brown','棕猫']];
const wsRecords=[
 {id:'koi',name:'锦鲤',size:.62,weight:2.38,time:'15:42:18',new:true,value:186},
 {id:'mandarin',name:'鳜鱼',size:.38,weight:.55,time:'15:31:06',new:true,value:128},
 {id:'grass_carp',name:'草鱼',size:.84,weight:5.93,time:'15:18:44',value:96},
 {id:'golden_line',name:'金线鲃',size:.31,weight:.30,time:'15:07:12',value:74},
 {id:'carp',name:'鲤鱼',size:.45,weight:.91,time:'14:52:31',value:62},
 {id:'crucian',name:'鲫鱼',size:.23,weight:.12,time:'14:35:04',value:34},
 {id:'bream',name:'鳊鱼',size:.34,weight:.39,time:'14:18:55',value:28},
 {id:'silver_carp',name:'鲢鱼',size:.51,weight:1.33,time:'14:06:29',value:40}
];
const wsFish=id=>wsRecords.find(f=>f.id===id)||wsRecords[0];
const wsArt=f=>`assets/fish/${f.id}.png`;
const wsBtn=(label,attrs='',primary=false)=>`<button type="button" class="ws-button${primary?' primary':''}" ${attrs}>${label}</button>`;
const wsMetric=(label,value)=>`<div class="ws-metric"><span>${label}</span><strong>${value}</strong></div>`;
function wsCaptureDraft(){if(!$('#ws-name'))return;ws.draft.nickname=$('#ws-name').value;ws.draft.bio=$('#ws-bio').value;}
function wsStartEdit(){ws.draft={nickname:J.nickname,bio:J.bio,avatar:J.avatar,slots:[...ws.slots]};ws.editTab='avatars';J.editing=true;S.tab.profile=0;jRefresh();}
const oldProfileBody=profileBody;
profileBody=function(){
 if(S.tab.profile!==0)return oldProfileBody();
 if(J.editing)return profileEditBody();
 const f=wsFish(ws.slots.find(Boolean));
 return `<div class="mid-h ws-profile-head">我的钓鱼时光${wsBtn('编辑资料','data-ws="edit"')}</div><div class="mid ws-profile">
 <section class="ws-card ws-identity"><img src="${jAvatar(J.avatar)}" alt="头像"><div><span class="ws-kicker">AN ANGLER’S JOURNAL · 湖畔手账</span><h2>${jEscape(J.nickname)}<small>Lv.7</small></h2><p title="${jEscape(J.bio)}">${jEscape(J.bio)}</p><p class="ws-muted" style="margin-top:8px">2026.09.25 开始垂钓 · 在湖边的第 28 天</p></div></section>
 <div class="ws-metrics">${wsMetric('累计钓获','64 <small>条</small>')}${wsMetric('探索水域','5 <small>处</small>')}${wsMetric('累计钓鱼时长','12 <small>时</small> 36 <small>分</small>')}</div>
 <section class="ws-card ws-progress"><header><span>下一段钓鱼旅程</span><span>Lv.7 → Lv.8</span></header><div class="bar"><i style="width:49.23%"></i></div><div class="ws-muted">320 / 650 经验 · 距离升级还需 330 经验</div><div class="ws-recent"><img src="assets/fish/koi.png" alt="锦鲤"><div><strong>最近收获 · 在千岛湖遇见锦鲤</strong><span class="ws-muted">今天 15:42 · 0.62 m · 2.38 kg</span></div></div></section></div>
 <aside class="aside ws-profile-aside"><div class="heading">我的珍藏</div>${jPolaroid(wsArt(f),f.name,'dots')}<div class="detail"><span class="ws-kicker">珍藏在展示位 1</span><dl><dt>体长</dt><dd>${f.size.toFixed(2)} m</dd><dt>体重</dt><dd>${f.weight.toFixed(2)} kg</dd><dt>记录时间</dt><dd>10.22 ${f.time.slice(0,5)}</dd><dt>相遇水域</dt><dd>千岛湖</dd></dl><p class="ws-muted">那些耐心等待的时刻，<br>都在手账里留下了印记。</p></div></aside><div class="status">收藏、图鉴和相册，在左侧页签里慢慢翻看。</div>`;
};
profileEditBody=function(){
 if(!ws.draft)ws.draft={nickname:J.nickname,bio:J.bio,avatar:J.avatar,slots:[...ws.slots]};
 const d=ws.draft;
 return `<div class="mid-h ws-heading">编辑我的手账<span class="count">名字、模样，还有最喜欢的鱼获</span></div><div class="ws-edit">
 <section class="ws-card ws-edit-left"><div class="ws-edit-person"><img id="ws-face" src="${jAvatar(d.avatar)}" alt="当前头像"><div><span class="ws-kicker">你的钓友名片</span><b id="ws-live-name">${jEscape(d.nickname)}</b><span class="ws-muted">Lv.7 · 湖畔常客</span></div></div>
 <div><label for="ws-name">游戏昵称 <small>最多 12 字</small></label><input id="ws-name" maxlength="12" value="${jEscape(d.nickname)}" autocomplete="off"><label for="ws-bio">个人简介 <small id="ws-bio-count">${d.bio.length} / 120</small></label><textarea id="ws-bio" maxlength="120" placeholder="向钓友介绍一下自己…">${jEscape(d.bio)}</textarea></div><div class="ws-edit-footer"><span id="ws-edit-error" role="alert"></span>${wsBtn('取消修改','data-ws="cancel-edit"')}${wsBtn('保存资料','data-ws="save-profile"',true)}</div></section>
 <section class="ws-card ws-edit-right"><div class="ws-edit-tabs">${wsBtn('选择头像',`data-edit-tab="avatars" aria-pressed="${ws.editTab==='avatars'}" ${ws.editTab==='avatars'?'style="background:#fbe8cf;border-color:#e9a25d"':''}`)}${wsBtn('展示鱼获',`data-edit-tab="showcase" aria-pressed="${ws.editTab==='showcase'}" ${ws.editTab==='showcase'?'style="background:#fbe8cf;border-color:#e9a25d"':''}`)}<small>${ws.editTab==='avatars'?'9 个模样，选你喜欢的':'8 个展示位 · 点击整张卡片更换'}</small></div>
 ${ws.editTab==='avatars'?`<div class="ws-avatar-grid">${wsAvatars.map(([id,name])=>`<button class="ws-avatar ${d.avatar===id?'selected':''}" data-ws-avatar="${id}" aria-pressed="${d.avatar===id}"><img src="${jAvatar(id)}" alt=""><span>${name}</span></button>`).join('')}</div>`:`<div class="ws-showcase-grid">${d.slots.map((id,i)=>{const f=id&&wsFish(id);return `<button class="ws-showcase" data-ws-slot="${i}" aria-label="更换展示位 ${i+1}"><span class="slot">${String(i+1).padStart(2,'0')}</span>${f?`<img src="${wsArt(f)}" alt="${f.name}"><strong>${f.name}</strong><small>${f.size.toFixed(2)} m · 点击更换</small>`:'<span class="plus">＋</span><strong>留给下一次惊喜</strong><small>点击选择鱼获</small>'}</button>`}).join('')}</div>`}</section></div>`;
};
const oldBindForms=jBindForms;
jBindForms=function(root){oldBindForms(root);
 $$('[data-ws="edit"]',root).forEach(b=>b.onclick=wsStartEdit);
 if(!$('#ws-name',root))return;
 $('#ws-name').oninput=()=>{wsCaptureDraft();$('#ws-live-name').textContent=ws.draft.nickname||'你的名字';};
 $('#ws-bio').oninput=()=>{wsCaptureDraft();$('#ws-bio-count').textContent=ws.draft.bio.length+' / 120';};
 $$('[data-edit-tab]',root).forEach(b=>b.onclick=()=>{wsCaptureDraft();ws.editTab=b.dataset.editTab;jRefresh();});
 $$('[data-ws-avatar]',root).forEach(b=>b.onclick=()=>{wsCaptureDraft();ws.draft.avatar=b.dataset.wsAvatar;$('#ws-face').src=jAvatar(ws.draft.avatar);$$('[data-ws-avatar]',root).forEach(a=>{a.classList.toggle('selected',a===b);a.setAttribute('aria-pressed',a===b);});});
 $$('[data-ws-slot]',root).forEach(b=>b.onclick=()=>{wsCaptureDraft();wsPicker(+b.dataset.wsSlot);});
 $('[data-ws="cancel-edit"]',root).onclick=()=>{ws.draft=null;J.editing=false;jRefresh();};
 $('[data-ws="save-profile"]',root).onclick=()=>{wsCaptureDraft();if(!ws.draft.nickname.trim()){$('#ws-edit-error').textContent='先给自己起一个名字吧。';$('#ws-name').focus();return;}J.nickname=ws.draft.nickname.trim();J.bio=ws.draft.bio.trim();J.avatar=ws.draft.avatar;ws.slots=[...ws.draft.slots];J.showcase=ws.slots.slice(0,3);J.editing=false;ws.draft=null;jSyncPlayer();jRefresh();toast('名片和展示鱼获已保存到本次预览');};
};
function wsDialog(title,html){const modal=jDialog(title,html);$('.j-dialog-paper',modal).classList.add('ws-dialog');return modal;}
function wsPicker(slot){
 let selected=ws.draft.slots[slot];
 const m=wsDialog(`整理展示位 ${slot+1}`,`<p class="ws-subtitle">挑一条喜欢的鱼获。清空展示位不会移除背包里的鱼。</p><input class="ws-picker-search" type="search" placeholder="搜索鱼种…" aria-label="搜索鱼种"><div class="ws-picker-grid"></div><footer class="ws-modal-footer">${wsBtn('清空此展示位','id="ws-clear-slot"')}<span class="ws-muted" id="ws-pick-caption"></span>${wsBtn('取消','id="ws-pick-cancel"')}${wsBtn('放进展示位','id="ws-pick-confirm"',true)}</footer>`);
 const draw=()=>{const q=$('.ws-picker-search',m).value.trim();const list=wsRecords.filter(f=>f.name.includes(q));$('.ws-picker-grid',m).innerHTML=list.length?list.map(f=>`<button class="ws-picker-item ${selected===f.id?'selected':''}" data-choice="${f.id}"><img src="${wsArt(f)}" alt=""><strong>${f.name}</strong><small>${f.size.toFixed(2)} m · ${f.weight.toFixed(2)} kg</small><small>10.22 ${f.time} · 千岛湖</small></button>`).join(''):'<div class="ws-empty">没有找到这种鱼，换个名字试试。</div>';$('#ws-pick-caption').textContent=selected?'已选：'+wsFish(selected).name:'选择一条鱼获';$('#ws-pick-confirm').disabled=!selected;$$('[data-choice]',m).forEach(b=>b.onclick=()=>{selected=b.dataset.choice;draw();});};
 $('.ws-picker-search',m).oninput=draw;$('#ws-pick-cancel').onclick=jCloseDialog;$('#ws-clear-slot').onclick=()=>{ws.draft.slots[slot]=null;jCloseDialog();jRefresh();};$('#ws-pick-confirm').onclick=()=>{ws.draft.slots[slot]=selected;jCloseDialog();jRefresh();};draw();
}
function wsCatchPreview(f){return `<i class="tape dots"></i><div class="ws-catch-image"><img src="${wsArt(f)}" alt="${f.name}"></div><h3>${f.name}${f.new?' <span class="ws-record-tag">新纪录</span>':''}</h3><div class="ws-fish-facts"><span>${f.size.toFixed(2)} m<small>体长</small></span><span>${f.weight.toFixed(2)} kg<small>体重</small></span></div><div class="ws-fish-time">2026.10.22 ${f.time} · 千岛湖</div>`;}
jCompose=function(id,returnToSettlement=false){
 ws.composer={fish:typeof id==='string'?id:'koi',text:'',visibility:'public'};
 const m=wsDialog('把这一刻，贴进动态手账',`<p class="ws-subtitle">选一条鱼获，写下今天的湖光与好心情。</p><div class="ws-compose-layout"><div><div class="ws-catch-preview"></div><div class="ws-compose-choices">${wsRecords.map(f=>`<button data-compose-fish="${f.id}" aria-label="选择${f.name}"><img src="${wsArt(f)}" alt=""><span>${f.name}</span></button>`).join('')}</div></div><div class="ws-compose-right"><div class="ws-author"><img src="${jAvatar(J.avatar)}" alt=""><div><strong>${jEscape(J.nickname)}</strong><div class="ws-muted">今天的湖畔来信</div></div></div><label for="ws-post-text">这一刻的心情</label><textarea class="ws-note" id="ws-post-text" maxlength="160" placeholder="比如：等了好久的一口，终于等到了这条漂亮的锦鲤。"></textarea><div class="ws-count"><span id="ws-post-count">0</span> / 160</div><div class="ws-visibility"><span>谁可以看</span>${wsBtn('所有钓友','data-visibility="public"')}${wsBtn('仅好友','data-visibility="friends"')}</div><p class="ws-muted" id="ws-visibility-hint">出现在公开动态里，和更多钓友分享快乐。</p></div></div><footer class="ws-modal-footer"><span id="ws-compose-error" role="alert"></span>${wsBtn('先不分享','id="ws-compose-cancel"')}${wsBtn('发布动态','id="ws-compose-submit"',true)}</footer>`);
 const draw=()=>{$('.ws-catch-preview',m).innerHTML=wsCatchPreview(wsFish(ws.composer.fish));$$('[data-compose-fish]',m).forEach(b=>{b.classList.toggle('selected',b.dataset.composeFish===ws.composer.fish);b.setAttribute('aria-pressed',b.dataset.composeFish===ws.composer.fish);});$$('[data-visibility]',m).forEach(b=>{b.classList.toggle('selected',b.dataset.visibility===ws.composer.visibility);b.setAttribute('aria-pressed',b.dataset.visibility===ws.composer.visibility);});$('#ws-visibility-hint').textContent=ws.composer.visibility==='public'?'出现在公开动态里，和更多钓友分享快乐。':'只出现在好友动态里，与熟悉的钓友分享。';};
 $$('[data-compose-fish]',m).forEach(b=>b.onclick=()=>{ws.composer.fish=b.dataset.composeFish;draw();});$$('[data-visibility]',m).forEach(b=>b.onclick=()=>{ws.composer.visibility=b.dataset.visibility;draw();});$('#ws-post-text').oninput=e=>{ws.composer.text=e.target.value;$('#ws-post-count').textContent=e.target.value.length;$('#ws-compose-error').textContent='';};$('#ws-compose-cancel').onclick=()=>{jCloseDialog();if(returnToSettlement)wsSettlement();};
 $('#ws-compose-submit').onclick=()=>{if(!ws.composer.text.trim()){$('#ws-compose-error').textContent='写一句今天的心情，再分享给钓友吧。';$('#ws-post-text').focus();return;}const p={id:'preview-'+Date.now(),author:'self',fish:ws.composer.fish,text:ws.composer.text.trim(),visibility:ws.composer.visibility,likes:0,time:'刚刚',pond:'千岛湖',friends:true,replies:[]};J.posts.unshift(p);J.feedSelected=p.id;jCloseDialog();J.editing=false;go('feed',{tab:2});toast('已发布到本次预览的「我的动态」');};draw();
};
function wsSettlement(){
 ws.filter=false;ws.sort='time';
 const total=wsRecords.reduce((s,f)=>s+f.value,0);
 const m=wsDialog('收竿啦，带上今天的湖畔记忆',`<p class="ws-subtitle">千岛湖 · 2026 年 10 月 22 日 · 14:00 — 15:48 · 回鱼出售</p><div class="ws-settlement-stats">${wsMetric('本次咬钩','12 <small>次</small>')}${wsMetric('脱钩','4 <small>次</small>')}${wsMetric('钓鱼时长','1 <small>时</small> 48 <small>分</small>')}${wsMetric('成功钓获','8 <small>条</small>')}</div><div class="ws-settlement-body"><section><div class="ws-record-toolbar"><span>本次鱼获 <small id="ws-record-count">8 条</small></span><label><input type="checkbox" id="ws-record-only"> 只看新纪录</label><select id="ws-record-sort" aria-label="鱼获排序"><option value="time">最近钓获优先</option><option value="size">体长从大到小</option><option value="weight">体重从大到小</option></select></div><div class="ws-catches"></div></section><aside class="ws-account"><h3>这次出行的小账本</h3><dl><dt>回鱼收入</dt><dd>+${total} 金币</dd><dt>鱼塘费用</dt><dd>−200 金币</dd></dl><div class="net">本次净收入<strong>+${total-200} <small>金币</small></strong></div><p class="ws-muted">按本次回鱼出售模式统计。<br>鱼获记录保留在钓鱼手账里。</p></aside></div><footer class="ws-modal-footer"><span class="ws-muted">这一页，记下了你的每一次耐心等待。</span>${wsBtn('回到个人手账','id="ws-settlement-home"',true)}</footer>`);
 const draw=()=>{let list=wsRecords.filter(f=>!ws.filter||f.new);list=[...list].sort((a,b)=>ws.sort==='time'?b.time.localeCompare(a.time):b[ws.sort]-a[ws.sort]);$('#ws-record-count').textContent=list.length+' 条';$('.ws-catches',m).innerHTML=list.map(f=>`<article class="ws-catch-row"><img src="${wsArt(f)}" alt="${f.name}"><div><b>${f.name}</b>${f.new?'<span class="ws-record-tag">新纪录</span>':''}<small>10.22 · ${f.time}</small></div><div>${f.size.toFixed(2)} m<small>体长</small></div><div>${f.weight.toFixed(2)} kg<small>体重</small></div>${wsBtn('分享',`data-share-record="${f.id}"`)}</article>`).join('');$$('[data-share-record]',m).forEach(b=>b.onclick=()=>{const id=b.dataset.shareRecord;jCloseDialog();jCompose(id,true);});};$('#ws-record-only').onchange=e=>{ws.filter=e.target.checked;draw();};$('#ws-record-sort').onchange=e=>{ws.sort=e.target.value;draw();};$('#ws-settlement-home').onclick=()=>{jCloseDialog();J.editing=false;go('profile',{tab:0});};draw();
}
const originalAction=jAction;
jAction=function(name,el){if(name==='edit-profile')return wsStartEdit();if(name==='cancel-edit'){ws.draft=null;J.editing=false;jRefresh();return;}return originalAction(name,el);};
const oldInstall=installJournalPages;
installJournalPages=function(){oldInstall();journalFish=journalFish.map(f=>{const r=wsRecords.find(x=>x.id===f.id);return r?{...f,size:r.size}:f;});J.posts=[];$('#player .xp b').textContent='320 / 650';$('#player .xp i').style.width='49.23%';S.page='profile';S.tab.profile=0;bobIdx=NAV.findIndex(n=>n.id==='profile');J.editing=false;
 const toolbar=$('#toolbar');for(const g of $$('.group',toolbar))g.style.display='none';$('.brand',toolbar).textContent='手账新布局 · 可交互设计';const links=document.createElement('div');links.className='ws-links';links.innerHTML='<button data-workspace="profile">个人资料</button><button data-workspace="edit">编辑资料</button><button data-workspace="compose">分享鱼获</button><button data-workspace="settlement">离塘结算</button>';toolbar.append(links);
 const open=async mode=>{if($('#j-dialog'))jCloseDialog();$$('[data-workspace]').forEach(b=>b.classList.toggle('active',b.dataset.workspace===mode));if(mode==='profile'){J.editing=false;await go('profile',{tab:0});}if(mode==='edit'){J.editing=false;await go('profile',{tab:0});wsStartEdit();}if(mode==='compose'){await go('feed',{tab:0});jCompose();}if(mode==='settlement')wsSettlement();history.replaceState(null,'',location.pathname+'#'+mode);};$$('[data-workspace]').forEach(b=>b.onclick=()=>open(b.dataset.workspace));$('#caption .hint').textContent='设计预览 · 示例统计与体重 · 操作不会发送到真实账号';$('#caption-text').textContent='切换上方四个入口，体验新的资料、分享与结算流程。';
 setTimeout(()=>open(['edit','compose','settlement'].includes(location.hash.slice(1))?location.hash.slice(1):'profile'),100);
};

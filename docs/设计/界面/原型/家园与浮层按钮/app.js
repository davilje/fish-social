const paths={
  rod:'<path d="M5 21 17 4q1-2 3-2M7 18l-2-2M17 4v10q0 3-3 3-2 0-2-2v-1"/><circle cx="8" cy="16" r="2.2"/>',
  tank:'<path d="M3 6h18v13H3zM2 4h20M4 10q3-2 6 0t6 0 4 0M7 18v-4m0 2-2-1m2 0 2-2"/><path d="M12 14q3-3 5 0-2 3-5 0l-2-2v4z"/><circle cx="17" cy="8" r=".6"/>',
  sleep:'<path d="M16 3a7 7 0 0 1-9 9 7 7 0 1 0 9-9Z"/><path d="M15 17h4l-4 4h4M3 4h3L3 7h3"/>',
  food:'<path d="M3 12h18l-2 8H5zM2 12h20M7 9h.1M12 8h.1M17 9h.1"/><path d="M10 16q2-2 4 0m-3 2h2"/>',
  door:'<path d="M4 21V4l10-2v19M2 21h14M8 12h.1M16 12h6m-3-3 3 3-3 3"/>',
  leave:'<path d="M10 4H4v16h6M9 12h12m-4-4 4 4-4 4"/>',
  left:'<path d="m14 6-6 6 6 6"/>',right:'<path d="m10 6 6 6-6 6"/>',
  bait:'<path d="M8 3h8l-1 4q5 3 4 8-1 3-7 3s-7-1-7-4q0-4 4-7zM8 7h8M9 11h.1m3 2h.1m3-2h.1M4 21q8 3 16 0"/><path d="M5 18h.1m14 0h.1"/>',
  bag:'<path d="M7 7V5a5 5 0 0 1 10 0v2M5 7h14l2 14H3zM9 12v2m6-2v2"/>',
  fish:'<path d="M5 12q8-9 15 0-7 9-15 0L2 8v8zM13 7l-3-3m3 13-3 3"/><circle cx="16" cy="11" r=".7"/>',
  chat:'<path d="M4 4h16v12h-9l-5 4v-4H4zM8 10h.1m4 0h.1m4 0h.1"/>'
};
const icon=name=>`<svg class="icon" viewBox="0 0 24 24" aria-hidden="true">${paths[name]||paths.fish}</svg>`;
document.querySelectorAll('[data-icon]').forEach(node=>node.innerHTML=icon(node.dataset.icon));
const $=id=>document.getElementById(id);
let activeView='home',sleeping=false,foodTimer=null,catTimer=null,toastTimer=null,feeding=false,pan=1,baitCount=12,baitState='active',baitTimer=null,fishing=false,lastFocused=null;
const resource='../../../../../fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/';
let homeWindow=null;
async function bindExistingHome(){
  const frame=$('home-base'),win=frame.contentWindow,doc=frame.contentDocument;
  if(!doc?.getElementById('stage'))return;
  homeWindow=win;
  // Reuse the complete existing interface. Only the two retired room labels are hidden.
  const style=doc.createElement('style');style.textContent='#toolbar,#caption{display:none!important}#viewport{position:absolute!important;inset:0!important}#stage{left:0!important;top:0!important;transform:none!important}#room-tackle,#room-aquarium{display:none!important}';doc.head.append(style);
  const stage=doc.getElementById('stage');
  const sync=()=>{$('home-scene').classList.toggle('base-page-open',stage.classList.contains('room-page-open'));};
  new MutationObserver(sync).observe(stage,{attributes:true,attributeFilter:['class']});sync();
  await win.roomAssetsReady;
  if(win.closePage)win.closePage();
  $('home-base').dataset.ready='true';
}
$('home-base').addEventListener('load',()=>bindExistingHome().catch(console.error));
if($('home-base').contentDocument?.readyState==='complete')bindExistingHome().catch(console.error);
function fit(){
  for(const [wrap,scene,width]of[['.home-wrap','home-scene',1672],['.overlay-wrap','overlay-scene',960]]){
    const box=document.querySelector(wrap);if(box.clientWidth)$(scene).style.transform=`scale(${box.clientWidth/width})`;
  }
}
new ResizeObserver(fit).observe(document.querySelector('main'));
function view(name){
  activeView=name;
  for(const id of['home','overlay','kit']){$(id+'-panel').hidden=id!==name;document.querySelector(`[data-view="${id}"]`).setAttribute('aria-pressed',id===name);}
  $('leave-confirm').hidden=true;$('bait-popover').hidden=true;$('bait-marker').setAttribute('aria-expanded','false');
  history.replaceState(null,'','#'+name);fit();
}
document.querySelectorAll('[data-view]').forEach(button=>button.onclick=()=>view(button.dataset.view));
$('reduce').onchange=()=>document.body.classList.toggle('reduce',$('reduce').checked);
const glows={rod:[515,145,230,385],tank:[1040,325,375,205],sleep:[40,533,320,130],food:[1420,591,166,70],go:[1550,175,116,480]};
document.querySelectorAll('.home-action').forEach(button=>{
  function glow(){const[x,y,w,h]=glows[button.dataset.action];Object.assign($('prop-glow').style,{left:x+'px',top:y+'px',width:w+'px',height:h+'px'});$('prop-glow').hidden=false;}
  button.onpointerenter=glow;button.onfocus=glow;button.onpointerleave=()=>{$('prop-glow').hidden=true;};button.onblur=button.onpointerleave;
  button.onclick=()=>homeAction(button.dataset.action);
});
function catNote(text,x=745,y=474){
  clearTimeout(catTimer);$('cat-note').textContent=text;$('cat-note').style.left=x+'px';$('cat-note').style.top=y+'px';$('cat-note').hidden=false;
  catTimer=setTimeout(()=>{$('cat-note').hidden=true;},2500);
}
function homeStatus(action,label,state=''){
  const button=document.querySelector(`[data-action="${action}"]`);
  button.querySelector('.home-tip').textContent=label;button.setAttribute('aria-label',label);
  button.classList.toggle('is-active',state==='active');button.classList.toggle('is-busy',state==='busy');
}
function wake(){sleeping=false;homeWindow?.roomCompanion?.wake();homeStatus('sleep','去睡觉');}
function inspect(kind){
  lastFocused=document.activeElement;
  const rod=kind==='rod';$('inspect-icon').innerHTML=icon(rod?'rod':'tank');$('inspect-title').textContent=rod?'我的钓竿':'鱼缸与展示柜';
  $('inspect-description').textContent=rod?'查看当前钓竿与属性，整理好今天的钓具。正式接入时进入装备页。':'看看收藏的鱼获和展示位。正式接入时进入自己的展示柜。';
  $('inspect-dialog').hidden=false;document.querySelector('.inspect-close').focus();
  document.querySelector('main').inert=true;document.querySelector('.tabs').inert=true;
}
function closeInspect(){
  $('inspect-dialog').hidden=true;document.querySelector('main').inert=false;document.querySelector('.tabs').inert=false;lastFocused?.focus();
}
document.querySelectorAll('.inspect-close').forEach(button=>button.onclick=closeInspect);
$('inspect-dialog').onclick=event=>{if(event.target===$('inspect-dialog'))closeInspect();};
function homeAction(action){
  if(action==='rod'||action==='tank'){
    homeWindow?.document.getElementById(action==='rod'?'room-tackle':'room-aquarium').click();return;
  }
  if(action==='sleep'){
    if(sleeping){wake();catNote('伸个懒腰，继续玩吧。');return;}
    clearTimeout(foodTimer);feeding=false;homeStatus('food','开饭啦');document.querySelector('[data-action="food"]').disabled=false;
    sleeping=true;homeWindow?.document.querySelector('[data-instance="cat-bed"]')?.click();homeStatus('sleep','轻轻唤醒','active');return;
  }
  if(action==='food'){
    wake();feeding=true;homeStatus('food','吃饭中','busy');document.querySelector('[data-action="food"]').disabled=true;catNote('开饭啦，喵！',1370,469);
    foodTimer=setTimeout(()=>{feeding=false;homeStatus('food','再吃一点');document.querySelector('[data-action="food"]').disabled=false;catNote('吃饱啦，谢谢你。');},3200);return;
  }
  view('overlay');toast('带好钓具，出发啦。');
}
function toast(text){clearTimeout(toastTimer);$('overlay-toast').textContent=text;$('overlay-toast').hidden=false;toastTimer=setTimeout(()=>{$('overlay-toast').hidden=true;},2400);}
function move(direction){pan=Math.max(0,Math.min(2,pan+direction));$('pond-art').style.backgroundPosition=(35+pan*15)+'% center';$('pan-left').disabled=pan===0;$('pan-right').disabled=pan===2;document.querySelectorAll('#viewport-dots i').forEach((dot,index)=>dot.classList.toggle('current',index===pan));toast(['左侧视野','中央视野','右侧视野'][pan]);}
$('pan-left').onclick=()=>move(-1);$('pan-right').onclick=()=>move(1);
const baitLabels={active:'窝料生效中',working:'正在打窝',empty:'还没有打窝',max:'窝料已达上限',expired:'窝料效果已到期'};
function setBait(state,{keepCount=false}={}){
  clearTimeout(baitTimer);baitState=state;$('bait-state').value=state;
  if(!keepCount)baitCount=state==='max'?50:state==='empty'||state==='expired'?0:12;
  $('bait-marker').className='bait-marker '+state;
  $('marker-count').textContent=state==='working'?'···':state==='expired'?'—':baitCount;
  $('marker-note').textContent=baitLabels[state];$('bait-title').textContent=baitLabels[state];
  $('bait-marker').setAttribute('aria-label','查看打窝效果：'+baitLabels[state]);
  $('bait-count').textContent=baitCount+' / 50';$('bait-time').hidden=!['active','max'].includes(state);
  $('bait-help').textContent=state==='working'?'正在投放窝料，投放完成后更新数量。':state==='empty'?'坐席后可投放窝料。':state==='expired'?'效果已结束，可重新投放窝料。':state==='max'?'已达 50 层，暂时无法继续叠加。':'叠加数量与剩余时间分别展示。';
  $('bait-button-label').innerHTML=state==='working'?'投料中':state==='max'?'已达上限 <small>50/50</small>':`打窝 <small>${baitCount}/50</small>`;
  $('add-bait').disabled=state==='working'||state==='max';
}
$('bait-state').onchange=()=>setBait($('bait-state').value);
function toggleBait(open=!$('bait-popover').hidden){$('bait-popover').hidden=open;$('bait-marker').setAttribute('aria-expanded',!open);}
$('bait-marker').onclick=()=>toggleBait();
$('bait-marker').onkeydown=event=>{if(event.key==='Enter'||event.key===' '){event.preventDefault();toggleBait();}};
$('bait-marker').onpointerenter=()=>toggleBait(false);
$('close-bait').onclick=()=>{toggleBait(true);$('bait-marker').focus();};
$('add-bait').onclick=()=>{
  const count=baitCount;setBait('working',{keepCount:true});toast('正在投放窝料……');
  baitTimer=setTimeout(()=>{baitCount=Math.min(count+1,50);setBait(baitCount===50?'max':'active',{keepCount:true});toast('窝料已投放 · '+baitCount+'/50');},1600);
};
$('fish-toggle').onclick=()=>{fishing=!fishing;$('fish-toggle').lastElementChild.textContent=fishing?'收竿休息':'开始钓鱼';toast(fishing?'安静等一条鱼上钩。':'收好鱼竿，休息一下。');};
$('leave').onclick=()=>{$('leave-confirm').hidden=!$('leave-confirm').hidden;if(!$('leave-confirm').hidden)$('stay').focus();};
$('stay').onclick=()=>{$('leave-confirm').hidden=true;$('leave').focus();};
$('confirm-leave').onclick=()=>{fishing=false;$('fish-toggle').lastElementChild.textContent='开始钓鱼';view('home');catNote('回家啦，今天也很开心。');};
document.addEventListener('keydown',event=>{
  if(event.key==='Escape'){if(!$('inspect-dialog').hidden)closeInspect();$('leave-confirm').hidden=true;toggleBait(true);}
  if(event.key==='Tab'&&!$('inspect-dialog').hidden){const buttons=[...$('inspect-dialog').querySelectorAll('button')];const first=buttons[0],last=buttons.at(-1);if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}}
});
const samples=[['查看钓竿','rod'],['查看鱼缸','tank'],['去睡觉','sleep'],['开饭啦','food'],['出门钓鱼','door','primary'],['向左看','left'],['向右看','right'],['离开鱼塘','leave']];
$('state-board').innerHTML='<div class="state-row"><span>交互按钮</span><span>默认</span><span>悬停</span><span>按下</span><span>不可用</span></div>'+samples.map(([label,name,kind=''])=>{
  const arrow=name==='left'||name==='right',home=['rod','tank','sleep','food','door'].includes(name);
  return `<div class="state-row"><span>${label}</span>${['','demo-hover','demo-pressed','demo-disabled'].map(state=>`<button class="action ${kind} ${arrow?'pan':''} ${home?'home-icon-sample':''} ${state}" ${state==='demo-disabled'?'disabled':''} aria-label="${label}" tabindex="-1">${arrow||home?icon(name):`<span class="${name==='leave'?'':'icon-disc'}">${icon(name)}</span><span>${label}</span>`}</button>`).join('')}</div>`;
}).join('');
$('marker-samples').innerHTML=[['empty','0','未打窝','静态轮廓'],['working','···','正在打窝','环形缺口缓慢转动'],['active','12','生效中','显示叠加数量'],['max','50','已达上限','停止继续投料'],['expired','—','已到期','灰色并显示到期文字']].map(([state,count,title,note])=>`<div class="marker-sample"><div class="bait-marker ${state}"><span class="marker-art">${icon('bait')}</span><span class="marker-count">${count}</span></div><p>${title}<small>${note}</small></p></div>`).join('');
setBait('active');
view(['home','overlay','kit'].includes(location.hash.slice(1))?location.hash.slice(1):'home');
window.addEventListener('hashchange',()=>{
  const next=location.hash.slice(1);if(['home','overlay','kit'].includes(next)&&next!==activeView)view(next);
});

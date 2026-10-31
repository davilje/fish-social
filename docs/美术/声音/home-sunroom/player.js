const $=id=>document.getElementById(id);
const music=$('homeMusic');
const paths={full:'../home-sunroom/assets/home-sunroom.wav?v=20261031-sunroom',excerpt:'../home-sunroom/assets/excerpt.wav?v=20261031-sunroom',current:'../../../../fish-social-unity/Assets/StreamingAssets/Audio/music/pond_daylight.wav?v=adopted-v5'};
const labels={full:'《晴窗小憩》完整新曲',excerpt:'《晴窗小憩》开篇片段',current:'现行首页音乐（对照）'};
const format=seconds=>Math.floor(seconds/60)+':'+String(Math.floor(seconds%60)).padStart(2,'0');
let pendingChapter=null;
const loadedSources=new Map(),objectUrls=[];let loadToken=0;
async function loadSource(id){
 const token=++loadToken;stopMusic();pendingChapter=null;window.HomeMusicLab.selection=null;window.HomeMusicLab.ready=false;
 music.removeAttribute('src');music.load();
 $('musicStatus').textContent='正在准备 '+labels[id]+'…';
 try{
  // Blob URLs support real random seeking even when the local HTTP server has no Range support.
  if(!loadedSources.has(id))loadedSources.set(id,fetch(paths[id],{cache:'no-store'}).then(async response=>{if(!response.ok)throw Error('HTTP '+response.status);const url=URL.createObjectURL(await response.blob());objectUrls.push(url);return url;}).catch(error=>{loadedSources.delete(id);throw error;}));
  const url=await loadedSources.get(id);if(token!==loadToken)return;
  music.src=url;music.load();window.HomeMusicLab.selection=id;window.HomeMusicLab.ready=true;
  $('musicStatus').textContent='已选择 '+labels[id]+'，点击播放器试听。';
 }catch{if(token===loadToken)$('musicStatus').textContent='音乐暂时无法读取，请刷新后重试。';}
}
function stopMusic(reset=false){music.pause();if(reset){pendingChapter=null;music.currentTime=0;}}
function applyChapter(){
 if(!pendingChapter)return;
 const chapter=pendingChapter;
 // A plain local static server may not support byte ranges: wait for the target to be buffered.
 if(chapter.startSeconds&&(!music.seekable.length||music.seekable.end(music.seekable.length-1)<chapter.startSeconds))return;
 pendingChapter=null;music.currentTime=chapter.startSeconds;
 $('musicStatus').textContent=music.paused?'已定位至「'+chapter.title+'」，点击播放器试听。':'正在试听「'+chapter.title+'」。';
}
for(const event of ['progress','canplay','canplaythrough'])music.addEventListener(event,applyChapter);
function stopOthers(){
  window.AudioClusterLab?.engine.stopImmediate('正在试听首页音乐。');
  $('recording').pause();
}
music.volume=.7;
music.addEventListener('play',()=>{stopOthers();$('musicStatus').textContent='正在试听 '+labels[$('musicSelection').value]+'。';});
music.addEventListener('pause',()=>{$('musicStatus').textContent=music.ended?'这一段听完了。':'音乐已暂停，可从当前位置继续。';});
music.addEventListener('ended',()=>{$('musicStatus').textContent='这一段听完了。';});
music.addEventListener('error',()=>{$('musicStatus').textContent='音乐暂时无法读取，请刷新后重试。';});
$('musicSelection').addEventListener('change',()=>{loadSource($('musicSelection').value);$('musicChapters').hidden=$('musicSelection').value!=='full';$('musicFacts').hidden=$('musicSelection').value==='current';});
$('musicLoop').addEventListener('change',()=>{music.loop=$('musicLoop').checked;});
$('musicVolume').addEventListener('input',()=>{music.volume=Number($('musicVolume').value)/100;$('musicVolumeValue').value=$('musicVolume').value+'%';});
$('musicStop').addEventListener('click',()=>{stopMusic(true);$('musicStatus').textContent='音乐已停止。';});
// Native media and the real-time ambience engine share one audition space.
document.addEventListener('play',event=>{if(event.target!==music)stopMusic();},true);
for(const id of ['play','new','loadEffects'])$(id).addEventListener('click',()=>stopMusic(),true);
const bindEffects=frame=>{
  const bind=()=>{try{frame.contentDocument.addEventListener('pointerdown',()=>stopMusic(),true);frame.contentDocument.addEventListener('click',()=>stopMusic(),true);}catch{}};
  frame.addEventListener('load',bind);bind();
};
new MutationObserver(records=>{for(const record of records)for(const node of record.addedNodes){if(node.nodeName==='IFRAME')bindEffects(node);}}).observe($('effectsHost'),{childList:true});
document.addEventListener('visibilitychange',()=>{if(document.hidden)stopMusic();});
window.addEventListener('pagehide',()=>{stopMusic();for(const url of objectUrls)URL.revokeObjectURL(url);});
window.HomeMusicLab={music,paths,stop:stopMusic,ready:false,selection:null};
loadSource('full');
// Composition metadata is optional for playback; the native player remains usable if it cannot load.
try{
 const response=await fetch('../home-sunroom/assets/score.json?v=20261031-sunroom',{cache:'no-store'});
 if(response.ok){
  const score=await response.json();window.HomeMusicLab.score=score;
  $('musicFacts').textContent=`完整曲 ${format(score.durationSeconds)} · G 大调 · 约 ${score.nominalBpm} BPM，随乐句轻微舒展`;
  for(const chapter of score.chapters||[]){
   const item=document.createElement('li'),button=document.createElement('button');button.type='button';
   button.textContent=`${format(chapter.startSeconds)} ${chapter.title}`;
   button.addEventListener('click',()=>{pendingChapter=chapter;$('musicStatus').textContent='正在准备「'+chapter.title+'」…';applyChapter();});
   item.append(button);$('musicChapters').append(item);
  }
 }
}catch{}

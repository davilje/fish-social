const data = window.breedReview;
const labels = { sit: '站立到准备', ready: '准备', bait: '挂饵', cast: '抛钩', wait: '等待', bite: '咬钩后仰', effort: '持续发力', retrieve: '收杆出水', retrieve_empty: '空军收杆', catch: '展示收获', reset_catch: '收获后复位', reset_empty: '空军后复位', groundbait: '打窝', leave: '离席' };
const control = id => document.getElementById(id);
const images = new Map();
let visible = [];
let position = 0;
let timer = 0;
let playing = false;
let revision = 0;
const selected = () => data.breeds.find(breed => breed.id === control('breed').value);

function imagePath(frame, breed = selected()) {
  if (breed.id === 'orange') return control('mode').value === 'full' ? `orange-full/${frame.id}.png` : frame.sourceCharacter;
  if (breed.installed) return `${breed.id}-${control('mode').value === 'full' ? 'full' : 'character'}/${frame.id}.png`;
  return `breeds/${breed.id}/painted/${frame.drawing}.png`;
}

function loadImage(url) {
  if (!images.has(url)) images.set(url, new Promise((resolve, reject) => {
    const image = new Image();
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error('无法加载：' + url));
    image.src = url;
  }));
  return images.get(url);
}

function stop() {
  clearTimeout(timer);
  playing = false;
  control('play').textContent = '播放';
  control('play').setAttribute('aria-pressed', 'false');
}

async function show() {
  const token = ++revision;
  const frame = visible[position];
  const breed = selected();
  control('error').textContent = '';
  control('caption').textContent = `${breed.name} · ${labels[frame.clip]} · ${position + 1}/${visible.length} · ${frame.durationMs} ms`;
  control('status').textContent = breed.status + (!breed.installed ? '；未完成的姿势不以旧换色图代替，当前只显示画稿。' : '。');
  for (const button of control('grid').children) button.setAttribute('aria-current', String(button.dataset.id === frame.id));
  const current = control('current').getContext('2d');
  current.clearRect(0, 0, 640, 512);
  const original = control('original').getContext('2d');
  original.clearRect(0, 0, 640, 512);
  try {
    const originalImages = await Promise.all((control('mode').value === 'full'
      ? frame.renderLayerOrder.map(layer => frame.layers[layer]) : [frame.sourceCharacter]).map(loadImage));
    if (token !== revision) return;
    for (const originalImage of originalImages) original.drawImage(originalImage, 0, 0, 640, 512);
    if (!breed.available.includes(frame.drawing)) { stop(); control('error').textContent = '此姿势尚未完成。'; return; }
    if (breed.id === 'orange') {
      for (const originalImage of originalImages) current.drawImage(originalImage, 0, 0, 640, 512);
      return;
    }
    const image = await loadImage(imagePath(frame));
    if (token !== revision) return;
    current.drawImage(image, 0, 0, 640, 512);
  } catch (error) { if (token === revision) { stop(); control('error').textContent = error.message; } }
}

async function tick() {
  if (!playing) return;
  position = (position + 1) % visible.length;
  await show();
  if (playing) timer = setTimeout(tick, visible[position].durationMs / Number(control('speed').value));
}

function render() {
  stop();
  visible = data.frames.filter(frame => frame.clip === control('clip').value);
  position = Math.min(position, visible.length - 1);
  control('grid').replaceChildren();
  for (const [index, frame] of visible.entries()) {
    const button = document.createElement('button');
    button.dataset.id = frame.id;
    if (selected().available.includes(frame.drawing)) {
      const image = document.createElement('img'); image.src = imagePath(frame); image.alt = frame.id; button.append(image);
    }
    button.append(document.createTextNode(frame.id));
    button.onclick = () => { stop(); position = index; show(); };
    control('grid').append(button);
  }
  show();
}

for (const breed of data.breeds) control('breed').add(new Option(breed.name, breed.id));
for (const id of new Set(data.frames.map(frame => frame.clip))) control('clip').add(new Option(labels[id], id));
control('breed').value = 'cow';
control('clip').value = 'ready';
for (const id of ['breed', 'clip', 'mode']) control(id).onchange = render;
control('speed').onchange = () => { if (playing) { clearTimeout(timer); timer = setTimeout(tick, visible[position].durationMs / Number(control('speed').value)); } };
control('play').onclick = () => {
  if (playing) { stop(); return; }
  playing = true; control('play').textContent = '暂停'; control('play').setAttribute('aria-pressed', 'true');
  timer = setTimeout(tick, visible[position].durationMs / Number(control('speed').value));
};
for (const [id, delta] of [['previous', -1], ['next', 1]]) control(id).onclick = () => { stop(); position = (position + delta + visible.length) % visible.length; show(); };
document.addEventListener('visibilitychange', () => { if (document.hidden) stop(); });
render();

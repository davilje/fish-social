const data = window.dafeiyu;
const labels = { sit: '站立到准备', ready: '准备', bait: '挂饵', cast: '抛钩', wait: '等待', bite: '咬钩后仰', effort: '持续发力', retrieve: '收杆出水', retrieve_empty: '空军收杆', catch: '展示收获', reset_catch: '收获后复位', reset_empty: '空军后复位', groundbait: '打窝', leave: '离席' };
const layerNames = { body: '身体 / 后发 / 鲸尾', head: '头部 / 头饰', far_hand: '后手', rod: '完整鱼竿', line: '鱼线', fish: '鱼', hands: '前手', bobber: '浮漂', bait: '鱼钩 / 饵' };
const control = id => document.getElementById(id);
const visible = new Set(Object.keys(layerNames));
let current = data.frames.find(frame => frame.clip === 'ready') ?? data.frames[0];
let queue = [];
let queueIndex = 0;
let timer = 0;
let running = false;
let full = false;
let revision = 0;
let playbackToken = 0;
const imagesReady = async () => Promise.all([...document.querySelectorAll('.stage img')].map(image => image.decode()));

function stop() {
  playbackToken++;
  clearTimeout(timer); running = false;
  control('play').textContent = '播放动作'; control('play').setAttribute('aria-pressed', 'false');
}

function appendImage(stage, source, className) {
  const image = new Image(); image.src = source; image.className = className; image.alt = ''; stage.append(image);
}

async function render() {
  const token = ++revision;
  control('caption').textContent = `大肥鱼 · ${current.label} / ${current.id}`;
  control('status').textContent = `${labels[current.clip]} · ${current.id} · ${current.durationMs} ms · ${data.uniqueDrawings} 张姿势稿 / ${data.frames.length} 个源帧`;
  control('error').textContent = '';
  for (const id of ['stage', 'original']) {
    const stage = control(id); stage.replaceChildren(); stage.classList.toggle('checker', control('checker').checked);
    if (control('seat').checked && current.showSeat !== false) appendImage(stage, data.seat.file, 'bench');
    for (const layer of current.renderLayerOrder) {
      if (!visible.has(layer)) continue;
      appendImage(stage, id === 'stage' ? `${current.layerFolder}/${layer}.png` : `../../cat-skins/reference/${current.id}/${layer}.png`, 'layer');
    }
  }
  control('character-link').href = current.character; control('composite-link').href = current.composite;
  for (const button of control('grid').children) button.setAttribute('aria-current', String(button.dataset.frame === current.id));
  try { await imagesReady(); } catch (error) { if (token === revision) { stop(); control('error').textContent = `图片加载失败：${error.message}`; } }
}

function rebuildGrid() {
  control('clip').value = current.clip; control('grid').replaceChildren();
  for (const frame of data.frames.filter(frame => frame.clip === current.clip)) {
    const button = document.createElement('button'); button.dataset.frame = frame.id;
    const image = new Image(); image.src = frame.review; image.alt = frame.label;
    button.append(image, document.createTextNode(frame.id));
    button.addEventListener('click', () => select(frame.id)); control('grid').append(button);
  }
}

async function select(id) {
  stop(); full = false;
  const frame = data.frames.find(frame => frame.id === id);
  if (!frame) throw new Error('Unknown frame: ' + id);
  current = frame; rebuildGrid(); await render();
}

function duration(frame) {
  return (full && frame.clip === 'wait' ? 650 : frame.durationMs) / Number(control('speed').value);
}

async function advance() {
  if (!running) return;
  const token = playbackToken;
  queueIndex++;
  if (queueIndex >= queue.length) {
    if (!full && control('loop').checked) queueIndex = 0;
    else { stop(); if (full) await select(data.frames.find(frame => frame.clip === 'ready').id); return; }
  }
  const next = queue[queueIndex];
  if (next.clip !== current.clip) { current = next; rebuildGrid(); } else current = next;
  await render();
  if (running && token === playbackToken) timer = setTimeout(advance, duration(current));
}

async function play(outcome) {
  stop(); full = Boolean(outcome);
  const token = playbackToken;
  const framesFor = clip => data.frames.filter(frame => frame.clip === clip);
  if (full) {
    const effort = framesFor('effort');
    const effortMs = effort.reduce((total, frame) => total + frame.durationMs, 0);
    const seconds = Math.max(1, Math.min(30, Number(control('effort-duration').value) || 3));
    const loops = Math.ceil(seconds * 1000 / effortMs);
    queue = ['sit', 'bait', 'cast', 'wait'].flatMap(framesFor)
      .concat(outcome === 'empty' ? [] : framesFor('bite').concat(Array.from({ length: loops }, () => effort).flat()), framesFor(outcome === 'empty' ? 'retrieve_empty' : 'retrieve'),
        outcome === 'empty' ? [] : framesFor('catch'), framesFor(outcome === 'empty' ? 'reset_empty' : 'reset_catch'));
  } else queue = framesFor(current.clip);
  queueIndex = full ? 0 : Math.max(0, queue.findIndex(frame => frame.id === current.id));
  current = queue[queueIndex]; rebuildGrid(); running = true;
  control('play').textContent = '暂停'; control('play').setAttribute('aria-pressed', 'true');
  await render(); if (running && token === playbackToken) timer = setTimeout(advance, duration(current));
}

for (const clip of new Set(data.frames.map(frame => frame.clip))) control('clip').add(new Option(labels[clip], clip));
for (const [layer, name] of Object.entries(layerNames)) {
  const label = document.createElement('label'), input = document.createElement('input');
  input.type = 'checkbox'; input.checked = true; input.dataset.layer = layer;
  input.addEventListener('change', () => { input.checked ? visible.add(layer) : visible.delete(layer); render(); });
  label.append(input, document.createTextNode(name)); control('layers').append(label);
}
control('clip').addEventListener('change', () => select(data.frames.find(frame => frame.clip === control('clip').value).id));
for (const id of ['previous', 'next']) control(id).addEventListener('click', () => {
  const frames = data.frames.filter(frame => frame.clip === current.clip);
  const index = frames.findIndex(frame => frame.id === current.id);
  select(frames[(index + (id === 'next' ? 1 : frames.length - 1)) % frames.length].id);
});
control('play').addEventListener('click', () => running ? stop() : play());
control('success').addEventListener('click', () => play('catch'));
control('empty').addEventListener('click', () => play('empty'));
for (const id of ['seat', 'checker']) control(id).addEventListener('change', render);
control('speed').addEventListener('change', () => { if (running) { clearTimeout(timer); timer = setTimeout(advance, duration(current)); } });
rebuildGrid();
window.dafeiyuReview = { select, play, stop, ready: render(), state: () => ({ id: current.id, clip: current.clip, running, full, queueLength: queue.length }) };

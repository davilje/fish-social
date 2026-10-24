const data = window.sangbiaoPreview;
const clipSelect = document.querySelector('#clip');
const frameStatus = document.querySelector('#status');
const imageCache = new Map();
const canvases = [...document.querySelectorAll('canvas')];
let frameIndex = 0;
let playing = false;
let timer;
let renderVersion = 0;

function loadImage(source) {
  if (!imageCache.has(source)) imageCache.set(source, new Promise((resolve, reject) => {
    const image = new Image();
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error('无法加载 ' + source));
    image.src = source;
  }));
  return imageCache.get(source);
}

function drawAnchor(context) {
  if (!document.querySelector('#anchor').checked) return;
  context.strokeStyle = '#e23a72';
  context.lineWidth = 1;
  context.beginPath();
  context.moveTo(0, 450.5); context.lineTo(640, 450.5);
  context.moveTo(264.5, 438); context.lineTo(264.5, 462);
  context.stroke();
}

async function render() {
  const version = ++renderVersion;
  const frames = data.clips[clipSelect.value];
  const frame = frames[frameIndex];
  const visible = new Set([...document.querySelectorAll('#layers input:checked')].map(input => input.value));
  try {
    const layers = await Promise.all(frame.layers.filter(layer => visible.has(layer.name)).map(layer => loadImage(layer.path)));
    const original = await loadImage(frame.original);
    if (version !== renderVersion) return;
    for (const canvas of canvases) canvas.getContext('2d').clearRect(0, 0, 640, 512);
    const context = canvases[0].getContext('2d');
    for (const image of layers) context.drawImage(image, 0, 0);
    canvases[1].getContext('2d').drawImage(original, 0, 0);
    for (const canvas of canvases) drawAnchor(canvas.getContext('2d'));
    frameStatus.textContent = `${frameIndex + 1}/${frames.length} · ${frame.source} · ${frame.duration}ms`;
    document.querySelector('#order').textContent = '本帧层序：' + frame.layers.map(layer => layer.name).join(' → ');
    document.querySelector('#error').textContent = '';
    if (playing) timer = setTimeout(advance, frame.duration / Number(document.querySelector('#speed').value));
  } catch (error) {
    stop();
    document.querySelector('#error').textContent = error.message;
  }
}

function stop() {
  playing = false;
  clearTimeout(timer);
  document.querySelector('#play').textContent = '播放';
}

function advance() {
  const definition = data.manifest.clips[clipSelect.value];
  frameIndex++;
  if (frameIndex >= data.clips[clipSelect.value].length) {
    if (['loop', 'intro-loop'].includes(definition.playback)) frameIndex = definition.loopStart ?? 0;
    else if (definition.nextClip && data.clips[definition.nextClip]) { clipSelect.value = definition.nextClip; frameIndex = 0; }
    else { frameIndex = data.clips[clipSelect.value].length - 1; stop(); }
  }
  render();
}

if (!data) {
  document.querySelector('#error').textContent = '完整 44 稿导出后生成动作预览数据；请先运行 build_sangbiao.mjs。';
} else {
  for (const clip of Object.keys(data.clips)) clipSelect.add(new Option(clip, clip));
  clipSelect.value = 'ready';
  for (const layer of data.manifest.layers) {
    const label = document.createElement('label');
    const input = document.createElement('input');
    input.type = 'checkbox'; input.checked = true; input.value = layer;
    label.append(input, document.createTextNode(layer));
    document.querySelector('#layers').append(label);
    input.onchange = () => { stop(); render(); };
  }
  clipSelect.onchange = () => { stop(); frameIndex = 0; render(); };
  document.querySelector('#play').onclick = () => {
    if (playing) stop();
    else { playing = true; document.querySelector('#play').textContent = '暂停'; render(); }
  };
  for (const [id, direction] of [['prev', -1], ['next', 1]]) document.querySelector('#' + id).onclick = () => {
    stop();
    const count = data.clips[clipSelect.value].length;
    frameIndex = (frameIndex + direction + count) % count;
    render();
  };
  document.querySelector('#anchor').onchange = () => { stop(); render(); };
  document.querySelector('#background').onchange = event => canvases.forEach(canvas => { canvas.style.backgroundColor = event.target.value; });
  const seen = new Set();
  for (const frame of data.frames) {
    if (seen.has(frame.artwork)) continue;
    seen.add(frame.artwork);
    const figure = document.createElement('figure');
    const image = document.createElement('img');
    image.src = 'frames/' + frame.id + '/composite.png'; image.loading = 'lazy'; image.alt = frame.artwork + ' 带道具合成';
    const caption = document.createElement('figcaption');
    caption.textContent = `${frame.artwork} · IoU ${(frame.overlap * 100).toFixed(1)}% · 脚底 Δ${frame.foot.deltaY}px`;
    figure.append(image, caption); document.querySelector('#gallery').append(figure);
  }
  render();
}

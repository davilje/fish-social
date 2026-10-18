const data = window.poseStudy;
const originalCanvas = document.querySelector('#original');
const candidateCanvas = document.querySelector('#candidate');
const mode = document.querySelector('#mode');
const cache = new Map();
let selection = 0;
let revision = 0;
let blinking = false;
let blinkOriginal = false;
let currentAssets = null;

function loadImage(url) {
  if (!cache.has(url)) cache.set(url, new Promise((resolve, reject) => {
    const asset = new Image();
    asset.onload = () => resolve(asset);
    asset.onerror = () => reject(new Error(`无法加载 ${url}`));
    asset.src = window.poseImages[url] || url;
  }));
  return cache.get(url);
}

function canvasFor(asset) {
  const canvas = document.createElement('canvas');
  [canvas.width, canvas.height] = data.canvas;
  if (asset) canvas.getContext('2d').drawImage(asset, 0, 0, canvas.width, canvas.height);
  return canvas;
}

function readAlpha(canvas) {
  return canvas.getContext('2d').getImageData(0, 0, ...data.canvas).data;
}

function compare(original, candidate) {
  const source = readAlpha(original);
  const variant = readAlpha(candidate);
  const delta = canvasFor();
  const context = delta.getContext('2d');
  const pixels = context.createImageData(...data.canvas);
  let intersection = 0;
  let union = 0;
  for (let offset = 0; offset < source.length; offset += 4) {
    const hasOriginal = source[offset + 3] >= 128;
    const hasCandidate = variant[offset + 3] >= 128;
    if (hasOriginal || hasCandidate) union++;
    if (hasOriginal && hasCandidate) intersection++;
    if (hasOriginal !== hasCandidate) {
      pixels.data.set(hasCandidate ? [220, 55, 70, 235] : [20, 161, 188, 235], offset);
    }
  }
  context.putImageData(pixels, 0, 0);
  return { delta, overlap: union ? intersection / union : 0 };
}

function drawGear(context, assets, useCandidate) {
  for (const layer of assets.frame.order) {
    if (!data.bodyLayers.includes(layer) || !useCandidate) {
      context.drawImage(assets.layers[layer], 0, 0, ...data.canvas);
      continue;
    }
    const masked = canvasFor(assets.candidate);
    const maskContext = masked.getContext('2d');
    maskContext.globalCompositeOperation = 'destination-in';
    maskContext.drawImage(assets.layers[layer], 0, 0, ...data.canvas);
    context.drawImage(masked, 0, 0);
  }
}

function render() {
  if (!currentAssets) return;
  const assets = currentAssets;
  const originalContext = originalCanvas.getContext('2d');
  const candidateContext = candidateCanvas.getContext('2d');
  originalContext.clearRect(0, 0, ...data.canvas);
  candidateContext.clearRect(0, 0, ...data.canvas);
  const useOriginal = blinking && blinkOriginal;
  if (mode.value === 'gear') {
    drawGear(originalContext, assets, false);
    drawGear(candidateContext, assets, !useOriginal);
  } else {
    originalContext.drawImage(assets.original, 0, 0);
    candidateContext.drawImage(useOriginal ? assets.original : assets.candidate, 0, 0);
    if (mode.value === 'ghost') {
      candidateContext.globalAlpha = 0.45;
      candidateContext.drawImage(assets.original, 0, 0);
      candidateContext.globalAlpha = 1;
    }
    if (mode.value === 'difference') candidateContext.drawImage(assets.comparison.delta, 0, 0);
  }
  document.querySelector('#right-label').textContent = useOriginal
    ? '交替对照 · 原版橘猫' : '三花猫 · 完整姿势重绘';
}

async function selectFrame(index) {
  selection = index;
  const request = ++revision;
  currentAssets = null;
  document.querySelector('#status').textContent = '载入中…';
  document.querySelector('#error').textContent = '';
  document.querySelectorAll('#poses button').forEach((button, buttonIndex) => {
    button.setAttribute('aria-pressed', String(index === buttonIndex));
  });
  try {
    const frame = data.frames[index];
    const [variant, layers] = await Promise.all([
      loadImage(frame.candidate),
      Promise.all(Object.entries(frame.layers).map(async ([name, url]) => [name, await loadImage(url)])),
    ]);
    if (request !== revision) return;
    const original = canvasFor();
    const layerImages = Object.fromEntries(layers);
    const originalContext = original.getContext('2d');
    for (const layer of frame.order) {
      if (data.bodyLayers.includes(layer)) originalContext.drawImage(layerImages[layer], 0, 0, ...data.canvas);
    }
    const candidate = canvasFor(variant);
    const comparison = compare(original, candidate);
    currentAssets = { frame, original, candidate, layers: layerImages, comparison };
    document.querySelector('#metrics').textContent =
      `原帧 ${frame.clip} / ${frame.index + 1}　·　新稿 ${variant.naturalWidth} × ${variant.naturalHeight}　·　轮廓重合率 ${(comparison.overlap * 100).toFixed(1)}%　·　状态：待校准 / 未接入`;
    document.querySelector('#status').textContent = mode.value === 'gear'
      ? '诊断试配：重点检查手掌与竿柄接触点、鱼与手掌的遮挡。不是最终分层素材。'
      : '完整姿势素材对照。轮廓重合率仅定位偏移，不能证明毛色连续或握杆正确。';
    render();
    window.poseStudyResult = { id: frame.id, overlap: comparison.overlap, loaded: true };
  } catch (error) {
    if (request !== revision) return;
    document.querySelector('#error').textContent = error.message;
    document.querySelector('#status').textContent = '载入失败';
    window.poseStudyResult = { loaded: false, error: error.message };
  }
}

for (const [index, frame] of data.frames.entries()) {
  const button = document.createElement('button');
  button.textContent = frame.label;
  button.setAttribute('aria-pressed', 'false');
  button.onclick = () => selectFrame(index);
  document.querySelector('#poses').append(button);
}
for (const frame of data.coverage) {
  const row = document.createElement('tr');
  for (const value of [`${frame.sourceClip} / ${frame.sourceIndex + 1}`,
    frame.runtime.map(entry => `${entry.clip}:${entry.index + 1}`).join('、'),
    frame.state === 'draft-unapproved' ? '关键姿势草稿 · 待校准' : '待绘制']) {
    const cell = document.createElement('td');
    cell.textContent = value;
    row.append(cell);
  }
  document.querySelector('#coverage').append(row);
}
mode.onchange = () => selectFrame(selection);
document.querySelector('#blink').onclick = event => {
  blinking = !blinking;
  blinkOriginal = false;
  event.currentTarget.setAttribute('aria-pressed', String(blinking));
  event.currentTarget.textContent = blinking ? '停止对照' : '交替对照';
  render();
};
setInterval(() => { if (blinking) { blinkOriginal = !blinkOriginal; render(); } }, 600);
selectFrame(0);

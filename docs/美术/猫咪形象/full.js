const frames = window.poseStudy.frames;
const labels = { sit: '站立到准备', ready: '准备', bait: '挂饵', cast: '抛钩', wait: '等待', bite: '咬钩后仰', effort: '持续发力', retrieve: '收杆出水', retrieve_empty: '空军收杆', catch: '展示收获', reset_catch: '收获后复位', reset_empty: '空军后复位', groundbait: '打窝', leave: '离席' };
const clip = document.querySelector('#clip');
const grid = document.querySelector('#grid');
const mode = document.querySelector('#mode');
const compare = document.querySelector('#compare');
const speed = document.querySelector('#speed');
const play = document.querySelector('#play');
let visible = [];
let position = 0;
let timer = 0;
let playing = false;

function imagePath(frame) {
  return `${mode.value}/${frame.id}.png?continuity-v3`;
}

function stop() {
  clearTimeout(timer);
  playing = false;
  play.textContent = '播放';
  play.setAttribute('aria-pressed', 'false');
}

function show() {
  const frame = visible[position];
  document.querySelector('#current').src = imagePath(frame);
  const record = window.paintedReview.frames.find(record => record.id === frame.id);
  const before = record.artwork.replace('calico-continuity/', 'calico-painted/');
  document.querySelector('#original').src = compare.value === 'before' ? before : frame.sourceCharacter;
  document.querySelector('#compareCaption').textContent = compare.value === 'before' ? '修正前 · 对照耳斑与花斑边界' : '原版姿势 · 对照轮廓和握点';
  document.querySelector('#caption').textContent = `${labels[frame.clip]} · ${frame.id} · ${frame.durationMs} ms`;
  document.querySelector('#status').textContent = `${position + 1} / ${visible.length}`;
  for (const button of grid.children) button.setAttribute('aria-current', String(button.dataset.id === frame.id));
}

function schedule() {
  timer = setTimeout(() => {
    position = (position + 1) % visible.length;
    show();
    schedule();
  }, visible[position].durationMs / Number(speed.value));
}

function render() {
  grid.replaceChildren();
  for (const [index, frame] of visible.entries()) {
    const button = document.createElement('button');
    button.dataset.id = frame.id;
    const image = document.createElement('img');
    image.src = imagePath(frame);
    image.alt = `${labels[frame.clip]} ${frame.index + 1}`;
    const caption = document.createElement('span');
    caption.textContent = frame.id;
    button.append(image, caption);
    button.onclick = () => { stop(); position = index; show(); };
    grid.append(button);
  }
  show();
}

for (const id of ['all', ...new Set(frames.map(frame => frame.clip))]) {
  const option = document.createElement('option');
  option.value = id;
  option.textContent = labels[id] || '全部动作';
  clip.append(option);
}
clip.value = 'effort';
clip.onchange = () => {
  stop();
  visible = clip.value === 'all' ? frames : frames.filter(frame => frame.clip === clip.value);
  position = 0;
  render();
};
mode.onchange = () => { stop(); render(); };
compare.onchange = show;
speed.onchange = () => { if (playing) { clearTimeout(timer); schedule(); } };
play.onclick = () => {
  if (playing) { stop(); return; }
  playing = true;
  play.textContent = '暂停';
  play.setAttribute('aria-pressed', 'true');
  schedule();
};
for (const [id, direction] of [['previous', -1], ['next', 1]]) {
  document.querySelector('#' + id).onclick = () => {
    stop(); position = (position + direction + visible.length) % visible.length; show();
  };
}
document.addEventListener('visibilitychange', () => { if (document.hidden) stop(); });
document.addEventListener('error', event => {
  if (event.target instanceof HTMLImageElement) {
    stop(); document.querySelector('#error').textContent = '图片加载失败：' + event.target.getAttribute('src');
  }
}, true);
clip.onchange();

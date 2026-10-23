(() => {
  'use strict';
  const root = new URL('assets/home-animation-v1/', location.href);
  const byId = id => document.getElementById(id);
  const controls = Object.fromEntries(['skin', 'action', 'direction', 'play', 'previous', 'next', 'scrubber',
    'speed', 'loop', 'zoom', 'anchors', 'background', 'art', 'canvas', 'empty', 'status', 'download'].map(id => [id, byId(id)]));
  const skins = [['orange', '橘猫'], ['black', '黑猫'], ['blue', '蓝猫'], ['brown', '棕猫'],
    ['calico', '三花猫'], ['cow', '奶牛猫'], ['cream', '奶油猫'], ['silver', '银渐层'], ['white', '白猫'], ['shared', '公共素材']];
  const actions = { 'pet-roll': '抚摸打滚', walk: '四足行走', 'walk-start': '行走起步', 'walk-stop': '行走停步',
    eat: '低头进食', 'food-pour': '倒粮', 'food-fill': '粮食累积', 'bowl-food': '粮食状态' };
  const directions = { neutral: '固定朝向', left: '向左', right: '向右' };
  const statuses = { review: '待验收', approved: '已验收', pending: '待制作', draft: '制作中', calibration: '待标定' };
  let clips = [], active = null, frames = [], index = 0, playing = false, elapsed = 0, lastTime = 0;
  let generation = 0, dataGeneration = 0, loopRange = null, open = true;
  const pad = value => String(value).padStart(2, '0');
  const point = value => Array.isArray(value) && value.length === 2 && value.every(Number.isFinite);
  const validPoint = value => point(value) && active && value.every((coordinate, axis) => coordinate >= 0 && coordinate <= active.canvas[axis]);
  const actionOf = clip => clip.action || clip.clipId || Object.keys(actions).sort((first, second) => second.length - first.length)
    .find(action => clip.id === action || clip.id?.includes(action)) || clip.id;
  const durationOf = frame => frame.durationMs ?? 1000 / (active?.fps || 12);
  const selectedClips = () => clips.filter(clip => clip.skinId === controls.skin.value);
  function option(value, label, disabled = false) {
    const result = document.createElement('option');
    result.value = value; result.textContent = label; result.disabled = disabled;
    return result;
  }
  skins.forEach(([id, name]) => controls.skin.append(option(id, name + (!['orange', 'shared'].includes(id) ? ' · 待制作' : ''), !['orange', 'shared'].includes(id))));
  function status(text, level = '') {
    controls.status.textContent = text; controls.status.dataset.level = level;
  }
  function empty(title, detail) {
    controls.empty.hidden = false;
    controls.empty.querySelector('strong').textContent = title;
    controls.empty.querySelector('p').textContent = detail;
  }
  function pause() {
    playing = false; elapsed = 0;
    controls.play.textContent = '播放'; controls.play.setAttribute('aria-pressed', 'false');
  }
  function disablePlayback(disabled) {
    for (const id of ['play', 'previous', 'next', 'scrubber']) controls[id].disabled = disabled;
  }
  function reset() {
    generation++; pause(); active = null; frames = []; index = 0; loopRange = null;
    controls.art.hidden = true; controls.art.removeAttribute('src'); controls.canvas.hidden = true;
    controls.download.removeAttribute('href'); controls.download.setAttribute('aria-disabled', 'true');
    controls.scrubber.value = '0'; controls.scrubber.max = '0';
    byId('frame-list').replaceChildren(); byId('metadata').replaceChildren();
    byId('frame-label').textContent = '帧 — / —'; byId('duration').textContent = '— ms';
    byId('filename').textContent = '尚未选择帧'; byId('clip-status').textContent = '等待素材';
    disablePlayback(true);
  }
  function assetUrl(file) {
    if (typeof file !== 'string' || !file || file.includes('\\')) return null;
    let url;
    try { url = new URL(file, root); } catch { return null; }
    if (url.origin !== root.origin || !url.pathname.startsWith(root.pathname) || !/\.png$/i.test(url.pathname)) return null;
    url.searchParams.set('review', String(window.homeAnimation?.revision || 'v1') + '-' + dataGeneration);
    return url.href;
  }
  function metadata(rows) {
    byId('metadata').replaceChildren();
    rows.forEach(([name, value]) => {
      const term = document.createElement('dt'), detail = document.createElement('dd');
      term.textContent = name; detail.textContent = value; byId('metadata').append(term, detail);
    });
  }
  function updateAnchors() {
    const mouth = active?.frames[index]?.mouthAnchor ?? active?.frames[index]?.mouthAnchorPx;
    for (const [id, coordinates] of [['registration', active?.registration], ['mouth', mouth]]) {
      const marker = byId(id);
      marker.querySelector('span').textContent = (id === 'mouth' ? '嘴部' : '注册点') + (active?.calibrationStatus === 'pending' ? ' · 待标定' : '');
      marker.hidden = !controls.anchors.checked || !validPoint(coordinates);
      if (!marker.hidden) { marker.style.left = coordinates[0] / active.canvas[0] * 100 + '%'; marker.style.top = coordinates[1] / active.canvas[1] * 100 + '%'; }
    }
  }
  function resizeCanvas() {
    if (!active) return;
    const zoom = Number(controls.zoom.value);
    const displayScale = Number.isFinite(active.displayScale) && active.displayScale > 0 ? active.displayScale : .5;
    controls.canvas.style.width = active.canvas[0] * displayScale * zoom + 'px';
    controls.canvas.style.height = active.canvas[1] * displayScale * zoom + 'px';
  }
  function render() {
    if (!active) return;
    const item = frames[index], frame = active.frames[index];
    controls.scrubber.value = String(index);
    byId('frame-label').textContent = `帧 ${pad(index)} / ${pad(frames.length - 1)}`;
    byId('duration').textContent = `${durationOf(frame).toFixed(2)} ms`;
    byId('filename').textContent = frame.file || '未填写文件路径';
    controls.download.removeAttribute('href'); controls.download.setAttribute('aria-disabled', 'true');
    for (const [position, button] of [...byId('frame-list').children].entries()) {
      button.setAttribute('aria-current', String(position === index)); button.dataset.state = frames[position].state;
    }
    controls.art.hidden = item.state !== 'ready';
    controls.empty.hidden = item.state === 'ready';
    controls.canvas.hidden = false;
    if (item.state === 'ready') {
      if (controls.art.src !== item.url) controls.art.src = item.url;
      controls.art.alt = `${active.label || actions[actionOf(active)] || active.id} · ${directions[active.direction] || '固定朝向'} · 帧 ${pad(index)}`;
      controls.download.href = item.url;
      controls.download.download = `${active.skinId}-${active.id}-${active.direction || 'neutral'}-${pad(index)}.png`;
      controls.download.setAttribute('aria-disabled', 'false');
    } else {
      controls.art.removeAttribute('src');
      empty(item.state === 'error' ? '这一帧暂不可查看' : '正在读取帧图', item.error || frame.file);
    }
    const failed = frames.filter(entry => entry.state === 'error').length;
    const loaded = frames.filter(entry => entry.state === 'ready').length;
    const mouth = frame.mouthAnchor ?? frame.mouthAnchorPx;
    const anchorNote = active.calibrationStatus === 'pending' ? ' · 接触锚点待标定' : !validPoint(active.registration) ? ' · 注册点待标定' : actionOf(active) === 'eat' && !validPoint(mouth) ? ' · 嘴部锚点待标定' : '';
    status(item.error || `已读取 ${loaded} / ${frames.length} 帧${failed ? ` · ${failed} 帧缺失或尺寸不符` : ''}${anchorNote}`, failed ? 'error' : '');
    controls.play.disabled = loaded !== frames.length;
    if (failed) pause();
    updateAnchors();
  }
  function selectClip() {
    reset();
    const clip = selectedClips().find(entry => actionOf(entry) === controls.action.value && (entry.direction || 'neutral') === controls.direction.value);
    if (!clip) { empty('这个动作还在制作中', '素材到位后，点击「刷新素材」即可查看。'); status('当前选择暂无帧图'); return; }
    if (!point(clip.canvas) || clip.canvas.some(size => !Number.isInteger(size) || size < 1 || size > 8192) || !Array.isArray(clip.frames) || !clip.frames.length) {
      empty('素材清单不完整', '需要有效画布尺寸与至少一帧图像。'); status('无法读取此动作的画布或帧列表', 'error'); return;
    }
    active = clip;
    if (clip.frames.some(frame => !frame || !Number.isFinite(durationOf(frame)) || durationOf(frame) <= 0)) {
      active = null; empty('帧时长无效', '每帧需填写正数 durationMs，或提供有效 fps。'); status('无法播放：帧时长无效', 'error'); return;
    }
    if (clip.loop != null) {
      if (!point(clip.loop) || !clip.loop.every(Number.isInteger) || clip.loop[0] < 0 || clip.loop[0] >= clip.loop[1] || clip.loop[1] > clip.frames.length) {
        active = null; empty('循环范围无效', '循环段使用左闭右开区间 [start, end)，终点不可超过总帧数。'); status('无法播放：循环范围无效', 'error'); return;
      }
      loopRange = clip.loop;
    }
    byId('clip-status').textContent = statuses[clip.status] || clip.status || '待验收';
    metadata([['画布', `${clip.canvas[0]} × ${clip.canvas[1]} px`], ['帧数 / 单次时长', `${clip.frames.length} 帧 / ${(clip.frames.reduce((sum, frame) => sum + durationOf(frame), 0) / 1000).toFixed(2)} s`],
      ['注册点', point(clip.registration) ? clip.registration.join(', ') + (clip.calibrationStatus === 'pending' ? ' · 待标定' : '') : '待标定'], ['循环段', loopRange ? `[${loopRange[0]}, ${loopRange[1]}) · 终点不含` : '无 · 勾选循环时整段重播'], ['显示基准', `${clip.displayScale || .5} × 源画布；显示 2× 可放大检查`]]);
    byId('loop-note').textContent = (loopRange ? `循环段 [${loopRange[0]}, ${loopRange[1]})；关闭循环可继续播放收尾。` : '循环开启时整段重播。') + ' 空格播放 / 暂停 · ← → 逐帧';
    controls.scrubber.max = String(clip.frames.length - 1); disablePlayback(false); resizeCanvas();
    const token = generation;
    frames = clip.frames.map(frame => ({ url: assetUrl(frame.file), state: 'loading', error: '' }));
    frames.forEach((item, position) => {
      const button = document.createElement('button'); button.type = 'button'; button.textContent = pad(position);
      button.setAttribute('aria-label', `查看帧 ${pad(position)}`);
      button.addEventListener('click', () => { pause(); index = position; render(); });
      byId('frame-list').append(button);
      if (!item.url) { item.state = 'error'; item.error = '文件路径无效：需使用素材目录内的相对 PNG 路径'; return; }
      const image = new Image();
      image.onload = () => {
        if (token !== generation) return;
        const match = image.naturalWidth === clip.canvas[0] && image.naturalHeight === clip.canvas[1];
        item.state = match ? 'ready' : 'error';
        if (!match) item.error = `尺寸不符：${clip.frames[position].file} 为 ${image.naturalWidth} × ${image.naturalHeight}，应为 ${clip.canvas.join(' × ')}`;
        render();
      };
      image.onerror = () => { if (token !== generation) return; item.state = 'error'; item.error = `文件缺失或无法读取：${clip.frames[position].file}`; render(); };
      image.src = item.url;
    });
    render();
  }
  function fillDirections() {
    const previous = controls.direction.value;
    const available = [...new Set(selectedClips().filter(clip => actionOf(clip) === controls.action.value).map(clip => clip.direction || 'neutral'))];
    controls.direction.replaceChildren(...available.map(value => option(value, directions[value] || value)));
    controls.direction.disabled = !available.length;
    if (available.includes(previous)) controls.direction.value = previous;
    selectClip();
  }
  function fillActions() {
    const previous = controls.action.value;
    const available = [...new Set(selectedClips().map(actionOf))];
    controls.action.replaceChildren(...available.map(value => option(value, actions[value] || selectedClips().find(clip => actionOf(clip) === value)?.label || value)));
    if (!available.length) controls.action.append(option('', '暂无动作 · 待制作'));
    controls.action.disabled = !available.length;
    if (available.includes(previous)) controls.action.value = previous;
    fillDirections();
  }
  function loadData() {
    reset(); clips = []; fillActions();
    empty('正在读取素材清单', '请稍候'); status('正在加载 data.js…');
    const token = dataGeneration = Math.max(Date.now(), dataGeneration + 1);
    byId('review-data')?.remove(); delete window.homeAnimation;
    const script = document.createElement('script'); script.id = 'review-data';
    script.onload = () => {
      if (token !== dataGeneration) return;
      const data = window.homeAnimation;
      if (data?.version !== 1 || !Array.isArray(data.clips) || !Array.isArray(data.skins)) {
        empty('素材清单格式不正确', '需要 version: 1、skins 与 clips。'); status('未找到有效的 homeAnimation 清单', 'error'); return;
      }
      clips = data.clips.filter(clip => clip && typeof clip.id === 'string' && skins.some(([id]) => id === clip.skinId));
      fillActions();
    };
    script.onerror = () => {
      if (token !== dataGeneration) return;
      empty('素材还未发布到预览', '橘猫与公共素材制作中。清单到位后，点击右上角「刷新素材」。');
      status('缺少 assets/home-animation-v1/data.js；当前没有可播放素材', 'error');
    };
    script.src = new URL(`data.js?review=${Date.now()}`, root).href; document.head.append(script);
  }
  function togglePlay() {
    if (playing) { pause(); return; }
    if (!active || frames.some(frame => frame.state !== 'ready')) return;
    if (index === frames.length - 1) index = 0;
    playing = true; elapsed = 0; lastTime = performance.now(); controls.play.textContent = '暂停'; controls.play.setAttribute('aria-pressed', 'true'); render();
  }
  function tick(now) {
    if (playing) {
      elapsed += Math.min(now - lastTime, 250) * Number(controls.speed.value);
      let changed = false, steps = 0;
      while (playing && elapsed >= durationOf(active.frames[index]) && steps++ < 200) {
        elapsed -= durationOf(active.frames[index]);
        if (controls.loop.checked && loopRange && index === loopRange[1] - 1) index = loopRange[0];
        else if (index < frames.length - 1) index++;
        else if (controls.loop.checked) index = 0;
        else pause();
        changed = true;
      }
      if (changed) render();
    }
    lastTime = now; requestAnimationFrame(tick);
  }
  controls.skin.addEventListener('change', fillActions); controls.action.addEventListener('change', fillDirections);
  controls.direction.addEventListener('change', selectClip); controls.play.addEventListener('click', togglePlay);
  function step(delta) { if (!active) return; pause(); index = Math.max(0, Math.min(frames.length - 1, index + delta)); render(); }
  controls.previous.addEventListener('click', () => step(-1)); controls.next.addEventListener('click', () => step(1));
  controls.scrubber.addEventListener('input', () => { pause(); index = Number(controls.scrubber.value); render(); });
  controls.zoom.addEventListener('change', resizeCanvas); controls.anchors.addEventListener('change', updateAnchors);
  controls.background.addEventListener('change', () => { byId('stage').dataset.background = controls.background.value; });
  controls.download.addEventListener('click', event => { if (!controls.download.hasAttribute('href')) event.preventDefault(); });
  byId('reload').addEventListener('click', loadData);
  function close() { pause(); if (window.parent !== window) window.parent.postMessage({ type: 'home-animation-review-close' }, location.protocol === 'file:' ? '*' : location.origin); }
  byId('close').hidden = window.parent === window; byId('close').addEventListener('click', close);
  window.addEventListener('message', event => {
    if (event.source !== window.parent || (location.protocol !== 'file:' && event.origin !== location.origin)) return;
    if (event.data?.type === 'home-animation-review-pause') { open = false; pause(); }
    if (event.data?.type === 'home-animation-review-open') open = true;
  });
  document.addEventListener('visibilitychange', () => { if (document.hidden) pause(); });
  window.addEventListener('blur', pause);
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape') { event.preventDefault(); close(); return; }
    if (!open || /^(INPUT|SELECT|TEXTAREA)$/.test(event.target.tagName)) return;
    if (event.code === 'Space' && !/^(BUTTON|A)$/.test(event.target.tagName)) { event.preventDefault(); togglePlay(); }
    if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') { event.preventDefault(); step(event.key === 'ArrowLeft' ? -1 : 1); }
  });
  loadData(); requestAnimationFrame(tick);
})();

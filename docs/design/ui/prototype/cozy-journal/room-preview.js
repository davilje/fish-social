'use strict';

// The existing cat becomes the single room companion; page rendering stays in app.js.
(() => {
  const stage = document.getElementById('stage');
  const background = document.getElementById('bg');
  const indoor = document.getElementById('room-bg-indoor');
  const outdoor = document.getElementById('room-bg-outdoor');
  const actor = document.getElementById('corner-cat');
  const art = actor.querySelector('.room-cat-art');
  const bubble = document.getElementById('room-cat-bubble');
  const floor = document.getElementById('room-floor');
  const tackle = document.getElementById('room-tackle');
  const aquarium = document.getElementById('room-aquarium');
  const scene = document.getElementById('room-scene');
  const layerToggle = document.getElementById('room-layers-toggle');

  // Individual alpha PNGs and placements are shared with the exported asset manifest.
  window.roomAssetsReady = fetch('assets/room-v4/manifest.json').then(response => {
    if (!response.ok) throw new Error(`Room asset manifest: ${response.status}`);
    return response.json();
  }).then(async manifest => {
    const images = manifest.placements.map(item => {
      const img = document.createElement('img');
      img.className = 'room-prop';
      img.dataset.component = item.asset;
      img.dataset.instance = item.id;
      img.alt = '';
      img.src = `assets/room-v4/${manifest.assets[item.asset].file}`;
      Object.assign(img.style, {
        left: `${item.x}px`, top: `${item.y}px`, width: `${item.width}px`,
        height: `${item.height}px`, zIndex: String(item.layer),
        objectFit: item.fit || 'contain',
        transform: item.rotation ? `rotate(${item.rotation}deg)` : '',
        transformOrigin: '50% 100%',
      });
      scene.append(img);
      return img;
    });
    await Promise.all(images.map(img => img.decode()));
    scene.dataset.ready = 'true';
    return manifest;
  }).catch(error => {
    scene.dataset.ready = 'error';
    console.error(error);
    caption('房间物件加载失败，请刷新预览。');
    throw error;
  });
  layerToggle.addEventListener('click', () => {
    scene.hidden = !scene.hidden;
    stage.classList.toggle('room-layers-hidden', scene.hidden);
    layerToggle.textContent = scene.hidden ? '物件：隐藏' : '物件：显示';
    layerToggle.setAttribute('aria-pressed', String(!scene.hidden));
  });
  let isIndoor = true, seated = false, motion = null, steps = null, drag = null;
  let point = { x: 835, y: 742 }, bubbleTimer, idleSince = performance.now();
  let nextStroll = performance.now() + 12000;
  const media = matchMedia('(prefers-reduced-motion: reduce)');
  const calm = () => reduced || media.matches;
  const active = () => isIndoor && !S.open && !document.hidden;
  const clamp = p => ({ x: Math.max(580, Math.min(1070, p.x)), y: Math.max(690, Math.min(775, p.y)) });
  const dimensions = () => seated ? { w: 176, h: 204 } : { w: 172, h: 246 };

  function place(p) {
    point = clamp(p);
    const { w, h } = dimensions();
    actor.style.left = `${point.x - w / 2}px`;
    actor.style.top = `${point.y - h}px`;
  }
  function stop() {
    if (motion) {
      const cs = getComputedStyle(actor), { w, h } = dimensions();
      const current = { x: parseFloat(cs.left) + w / 2, y: parseFloat(cs.top) + h };
      motion.cancel(); motion = null;
      place(current);
    }
    steps?.cancel(); steps = null;
    actor.classList.remove('is-walking');
  }
  function pose(sit) {
    seated = sit;
    actor.classList.toggle('is-sitting', sit);
    actor.dataset.pose = sit ? 'sitting' : 'standing';
    place(point);
  }
  function say(message) {
    bubble.textContent = message;
    bubble.classList.add('visible');
    clearTimeout(bubbleTimer);
    bubbleTimer = setTimeout(() => bubble.classList.remove('visible'), 2400);
  }
  function interact() {
    wake(); idleSince = performance.now(); nextStroll = idleSince + 12000;
  }
  function rest() {
    stop(); pose(true);
    caption('小橘坐下休息了；点它、点地板或拖动即可继续活动。');
    say('在家歇一会儿～');
  }
  function stand() { stop(); pose(false); }
  function walkTo(target, manual = true) {
    if (!active()) return;
    if (manual) interact();
    stop(); pose(false);
    const start = { ...point }, dest = clamp(target);
    const distance = Math.hypot(dest.x - start.x, dest.y - start.y);
    place(dest);
    if (distance < 2 || calm()) return;
    const { w, h } = dimensions();
    const travel = actor.animate([
      { left: `${start.x - w / 2}px`, top: `${start.y - h}px` },
      { left: `${dest.x - w / 2}px`, top: `${dest.y - h}px` }
    ], { duration: Math.max(350, distance / 170 * 1000), easing: 'ease-in-out' });
    motion = travel;
    actor.classList.add('is-walking');
    steps = art.animate([{ transform: 'translateY(0) rotate(-1deg)' }, { transform: 'translateY(-3px) rotate(1deg)' }, { transform: 'translateY(0) rotate(-1deg)' }], { duration: 330, iterations: Infinity });
    travel.finished.then(() => {
      if (motion !== travel) return;
      motion = null; steps?.cancel(); steps = null; actor.classList.remove('is-walking');
    }).catch(() => {});
  }
  function stagePoint(event) {
    const rect = stage.getBoundingClientRect();
    return { x: (event.clientX - rect.left) * 1672 / rect.width, y: (event.clientY - rect.top) * 941 / rect.height };
  }
  floor.addEventListener('click', e => {
    walkTo(e.detail === 0 ? { x: 835, y: 742 } : stagePoint(e));
    caption('点击地板：小橘移动到指定位置。也可以拖动小橘，或聚焦后用方向键移动、R 坐下。');
  });
  actor.addEventListener('pointerdown', e => {
    if (!active() || e.button !== 0) return;
    interact(); stop(); pose(false);
    const p = stagePoint(e);
    drag = { id: e.pointerId, start: p, offset: { x: point.x - p.x, y: point.y - p.y }, moved: false };
    actor.setPointerCapture(e.pointerId);
  });
  actor.addEventListener('pointermove', e => {
    if (!drag || drag.id !== e.pointerId) return;
    const p = stagePoint(e);
    if (Math.hypot(p.x - drag.start.x, p.y - drag.start.y) > 5) drag.moved = true;
    if (drag.moved) place({ x: p.x + drag.offset.x, y: p.y + drag.offset.y });
  });
  function finishDrag(e) {
    if (!drag || drag.id !== e.pointerId) return;
    const moved = drag.moved;
    if (actor.hasPointerCapture(e.pointerId)) actor.releasePointerCapture(e.pointerId);
    drag = null;
    actor.dataset.dragged = moved ? 'true' : 'false';
    if (moved) { interact(); caption('小橘的位置已调整；活动范围限定在中央地板，避开家具和底部导航。'); }
  }
  actor.addEventListener('pointerup', finishDrag);
  actor.addEventListener('pointercancel', e => { finishDrag(e); });
  actor.addEventListener('click', e => {
    if (!active()) return;
    if (actor.dataset.dragged === 'true' && e.detail !== 0) { actor.dataset.dragged = 'false'; return; }
    interact(); stop(); pose(false); catNod(); say('今天也一起钓鱼吧！');
  });
  actor.addEventListener('keydown', e => {
    if (!active()) return;
    const directions = { ArrowLeft: [-60, 0], ArrowRight: [60, 0], ArrowUp: [0, -30], ArrowDown: [0, 30] };
    if (directions[e.key]) {
      e.preventDefault(); stop();
      const [x, y] = directions[e.key]; walkTo({ x: point.x + x, y: point.y + y });
    } else if (e.key.toLowerCase() === 'r') { e.preventDefault(); interact(); rest(); }
    else if (e.key === 'Home') { e.preventDefault(); walkTo({ x: 835, y: 742 }); }
  });
  tackle.addEventListener('click', () => go('equip'));
  aquarium.addEventListener('click', () => go('profile', { tab: 1 }));

  function syncPage() {
    stage.classList.toggle('room-page-open', S.open);
    for (const el of [actor, floor, tackle, aquarium]) el.inert = !active();
    if (!active()) { stop(); drag = null; bubble.classList.remove('visible'); }
    else if (!seated) caption('小橘在房间中央活动：点地板移动，拖动调整位置，点小橘互动；方向键移动，R 坐下。');
    nextStroll = performance.now() + 12000;
  }
  function setRoomBackground(value) {
    stop(); isIndoor = value;
    // Clear outdoor-only persistent poses before switching the same actor indoors.
    zzzLoop.cancel(); actor._sleep?.cancel(); actor._lean?.cancel(); sleeping = false;
    background.src = value ? 'assets/room-v4/room-base.jpg' : 'assets/home-sakura.jpg';
    background.alt = value ? '均匀光照的空房间底图，用品由独立透明切图叠放' : '原有樱花湖畔背景';
    layerToggle.disabled = !value;
    stage.classList.toggle('room-mode', value);
    indoor.classList.toggle('on', value); outdoor.classList.toggle('on', !value);
    indoor.setAttribute('aria-pressed', String(value)); outdoor.setAttribute('aria-pressed', String(!value));
    document.querySelector('[data-demo="sleep"]').textContent = value ? '小猫休息' : '小猫睡着';
    if (value) { pose(false); place(point); }
    else { actor.style.left = ''; actor.style.top = ''; }
    if (!S.open) startCatBreath();
    syncPage();
  }
  window.roomCompanion = { get active() { return isIndoor; }, rest, wake: stand };
  indoor.addEventListener('click', () => setRoomBackground(true));
  outdoor.addEventListener('click', () => setRoomBackground(false));
  document.getElementById('room-home').addEventListener('click', () => closePage());
  new MutationObserver(syncPage).observe(document.getElementById('page'), { attributes: true, attributeFilter: ['style'] });
  document.addEventListener('visibilitychange', syncPage);
  function reduceMotion() { if (calm()) stop(); }
  document.getElementById('reduce').addEventListener('click', () => queueMicrotask(reduceMotion));
  media.addEventListener('change', reduceMotion);
  setInterval(() => {
    if (!active() || calm() || seated || drag || motion || performance.now() < nextStroll) return;
    nextStroll = performance.now() + 12000;
    walkTo({ x: 650 + Math.random() * 350, y: 710 + Math.random() * 50 }, false);
  }, 1000);
  setRoomBackground(true);
  const ready = new Promise(resolve => {
    if (document.readyState !== 'loading') resolve();
    else document.addEventListener('DOMContentLoaded', resolve, { once: true });
  });
  Promise.all([document.fonts.load('20px FishCozy').catch(() => {}), ready]).then(() => {
    if (new URLSearchParams(location.search).get('roomView') === 'home') requestAnimationFrame(() => closePage());
  });
})();

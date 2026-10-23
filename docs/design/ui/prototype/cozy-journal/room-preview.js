'use strict';

// Background review controls live in the existing prototype toolbar.
// The game UI, its layout, page renderers, and data remain owned by app.js.
(() => {
  const background = document.getElementById('bg');
  const indoor = document.getElementById('room-bg-indoor');
  const outdoor = document.getElementById('room-bg-outdoor');
  const home = document.getElementById('room-home');

  function setRoomBackground(isIndoor) {
    background.src = isIndoor ? 'assets/home-room-balanced.jpg' : 'assets/home-sakura.jpg';
    background.alt = isIndoor ? '湖畔小屋室内背景候选' : '原有樱花湖畔背景';
    indoor.classList.toggle('on', isIndoor);
    outdoor.classList.toggle('on', !isIndoor);
    indoor.setAttribute('aria-pressed', String(isIndoor));
    outdoor.setAttribute('aria-pressed', String(!isIndoor));
  }

  indoor.addEventListener('click', () => setRoomBackground(true));
  outdoor.addEventListener('click', () => setRoomBackground(false));
  home.addEventListener('click', () => closePage());

  const ready = new Promise(resolve => {
    if (document.readyState !== 'loading') resolve();
    else document.addEventListener('DOMContentLoaded', resolve, { once: true });
  });

  Promise.all([document.fonts.load('20px FishCozy').catch(() => {}), ready]).then(() => {
    if (new URLSearchParams(location.search).get('roomView') === 'home') {
      requestAnimationFrame(() => closePage());
    }
  });
})();

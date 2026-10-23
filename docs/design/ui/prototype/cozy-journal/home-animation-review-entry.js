(() => {
  'use strict';
  const trigger = document.getElementById('home-animation-review-open');
  const dialog = document.createElement('dialog');
  dialog.id = 'home-animation-review-dialog';
  dialog.setAttribute('aria-label', '小屋动作预览');
  const frame = document.createElement('iframe');
  frame.title = '小屋动作素材审阅';
  dialog.append(frame);
  document.body.append(dialog);
  const signal = type => frame.contentWindow?.postMessage({ type }, location.protocol === 'file:' ? '*' : location.origin);
  trigger.addEventListener('click', () => {
    if (!frame.getAttribute('src')) frame.src = 'home-animation-review.html';
    dialog.showModal();
    signal('home-animation-review-open');
    frame.focus();
  });
  dialog.addEventListener('close', () => {
    signal('home-animation-review-pause');
    trigger.focus();
  });
  dialog.addEventListener('click', event => {
    if (event.target === dialog) dialog.close();
  });
  window.addEventListener('message', event => {
    if (event.source !== frame.contentWindow || (location.protocol !== 'file:' && event.origin !== location.origin)) return;
    if (event.data?.type === 'home-animation-review-close') dialog.close();
  });
})();

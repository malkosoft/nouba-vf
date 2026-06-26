document.addEventListener('click', async function (event) {
    const btn = event.target.closest('.fullscreen-btn');
    if (!btn) return;

    const target = document.querySelector(btn.dataset.fullscreenTarget || 'body') || document.documentElement;
    try {
        if (!document.fullscreenElement) {
            await target.requestFullscreen();
        } else {
            await document.exitFullscreen();
        }
    } catch (e) {
        console.warn('Fullscreen non disponible', e);
    }
});

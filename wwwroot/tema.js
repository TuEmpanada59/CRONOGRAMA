// Aplica el tema guardado antes de pintar la página (oscuro por defecto)
// y el ancho guardado de la columna de nombres del cronograma.
(function () {
  var t = 'dark', w = 0;
  try { t = localStorage.getItem('cg-tema') || 'dark'; w = parseInt(localStorage.getItem('cg-lw'), 10) || 0; } catch (e) {}
  document.documentElement.setAttribute('data-theme', t === 'light' ? 'light' : 'dark');
  if (w) document.documentElement.style.setProperty('--g-lw', Math.max(140, Math.min(700, w)) + 'px');
})();

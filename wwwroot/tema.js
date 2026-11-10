// Aplica el tema guardado antes de pintar la página (oscuro por defecto).
(function () {
  var t = 'dark';
  try { t = localStorage.getItem('cg-tema') || 'dark'; } catch (e) {}
  document.documentElement.setAttribute('data-theme', t === 'light' ? 'light' : 'dark');
})();

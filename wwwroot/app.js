// Ayudas mínimas del navegador para la interfaz Blazor (la lógica vive en .NET).
(function () {
  'use strict';
  // Cambiar tema claro/oscuro (se recuerda en este navegador)
  document.addEventListener('click', function (e) {
    var b = e.target.closest && e.target.closest('[data-tema]');
    if (!b) return;
    var t = document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-theme', t);
    try { localStorage.setItem('cg-tema', t); } catch (_) {}
    var m = document.querySelector('meta[name="theme-color"]');
    if (m) m.content = t === 'dark' ? '#1e222b' : '#ffffff';
  });
  // Firefox necesita datos en el arrastre para permitir mover tarjetas del tablero
  document.addEventListener('dragstart', function (e) {
    var c = e.target.closest && e.target.closest('[data-card]');
    if (c) { try { e.dataTransfer.setData('text/plain', c.getAttribute('data-card')); e.dataTransfer.effectAllowed = 'move'; } catch (_) {} }
  });
  // Escape cierra el panel lateral abierto
  document.addEventListener('keydown', function (e) {
    if (e.key !== 'Escape') return;
    var x = document.querySelector('.scrim .panel-h .x');
    if (x) x.click();
  });
  // Estirar la columna de nombres del cronograma (se recuerda en este navegador)
  var raiz = document.documentElement, MIN = 140, MAX = 700;
  function anchoLabels(w) {
    if (w == null) { raiz.style.removeProperty('--g-lw'); try { localStorage.removeItem('cg-lw'); } catch (_) {} return; }
    w = Math.round(Math.max(MIN, Math.min(MAX, w)));
    raiz.style.setProperty('--g-lw', w + 'px');
    return w;
  }
  function guardarAncho(w) { try { localStorage.setItem('cg-lw', String(w)); } catch (_) {} }
  try { var g = parseInt(localStorage.getItem('cg-lw'), 10); if (g) anchoLabels(g); } catch (_) {}
  function anchoActual(h) { var l = h.parentNode.querySelector('.g-label'); return l ? l.getBoundingClientRect().width : 250; }
  document.addEventListener('pointerdown', function (e) {
    var h = e.target.closest && e.target.closest('.g-resize');
    if (!h || e.button !== 0) return;
    e.preventDefault();
    var x0 = e.clientX, w0 = anchoActual(h), w = w0;
    h.classList.add('activo'); document.body.classList.add('redimensionando');
    try { h.setPointerCapture(e.pointerId); } catch (_) {}
    function mover(ev) { w = anchoLabels(w0 + ev.clientX - x0); }
    function soltar() {
      h.removeEventListener('pointermove', mover); h.removeEventListener('pointerup', soltar); h.removeEventListener('pointercancel', soltar);
      h.classList.remove('activo'); document.body.classList.remove('redimensionando');
      guardarAncho(w);
    }
    h.addEventListener('pointermove', mover); h.addEventListener('pointerup', soltar); h.addEventListener('pointercancel', soltar);
  });
  document.addEventListener('dblclick', function (e) {
    if (e.target.closest && e.target.closest('.g-resize')) anchoLabels(null);
  });
  document.addEventListener('keydown', function (e) {
    var h = e.target.closest && e.target.closest('.g-resize');
    if (!h || (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight')) return;
    e.preventDefault();
    guardarAncho(anchoLabels(anchoActual(h) + (e.key === 'ArrowRight' ? 20 : -20)));
  });

  window.cg = {
    leer: function (k) { try { return localStorage.getItem(k); } catch (_) { return null; } },
    guardar: function (k, v) { try { localStorage.setItem(k, v); } catch (_) {} },
    // Centra la línea de "hoy" en el cronograma la primera vez que se abre
    centrarHoy: function () {
      var w = document.querySelector('.gantt-wrap'), h = w && w.querySelector('.g-today');
      if (w && h && !w.dataset.centrado) { w.dataset.centrado = '1'; w.scrollLeft = Math.max(0, h.offsetLeft - w.clientWidth / 2); }
    },
    enfocar: function (sel) { setTimeout(function () { var el = document.querySelector(sel); if (el) el.focus(); }, 30); }
  };
})();

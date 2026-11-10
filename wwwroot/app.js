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

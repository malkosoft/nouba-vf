/* ═══════════════════════════════════════════════════════════════════════
   NOUBA — Sélecteur de thème (Admin & Agent), préférences INDÉPENDANTES.
   Le périmètre ("admin" ou "agent") est lu sur l'attribut data-theme-scope
   de la balise <script>. Chaque périmètre a sa propre clé localStorage :
   les deux interfaces sont donc totalement indépendantes.
   Le défaut est "dark" (aucun attribut posé → thème sombre natif, inchangé).
   ═══════════════════════════════════════════════════════════════════════ */
(function () {
  var script = document.currentScript;
  var scope = (script && script.getAttribute('data-theme-scope')) || 'admin';
  var KEY = 'nouba-theme-' + scope;
  var VALID = ['dark', 'light', 'auto', 'contrast'];

  function current() {
    try { var v = localStorage.getItem(KEY); return VALID.indexOf(v) >= 0 ? v : 'dark'; }
    catch (_) { return 'dark'; }
  }

  function apply(theme) {
    if (VALID.indexOf(theme) < 0) theme = 'dark';
    // "dark" = défaut : on RETIRE l'attribut → aucune surcharge → thème sombre natif.
    if (theme === 'dark') document.documentElement.removeAttribute('data-theme');
    else document.documentElement.setAttribute('data-theme', theme);
    try { localStorage.setItem(KEY, theme); } catch (_) {}
    var sel = document.getElementById('noubaThemeSelect');
    if (sel && sel.value !== theme) sel.value = theme;
  }

  // Appliqué immédiatement (script en <head>, avant le rendu du <body>) → pas de flash.
  apply(current());
  window.noubaSetTheme = apply;

  document.addEventListener('DOMContentLoaded', function () {
    var sel = document.getElementById('noubaThemeSelect');
    if (sel) {
      sel.value = current();
      sel.addEventListener('change', function () { apply(sel.value); });
    }
  });
})();

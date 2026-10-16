// HuntXtract overlay: renders one overlay design ("profile"). Used by the OBS browser source
// (overlay.html, polls the local API) and by the previews inside the app.
(function () {
  'use strict';

  const SCOPES = { today: 'Heute', last10: 'Letzte 10', all: 'Gesamt', last: 'Letztes Match' };

  // What the overlay itself writes, in English; the German texts are the ones in the code. The
  // app sets the language for its previews, the OBS page takes it from the program.
  const EN = {
    'Heute': 'Today',
    'Letzte 10': 'Last 10',
    'Gesamt': 'All time',
    'Letztes Match': 'Last match',
    'Ausgang': 'Outcome',
    'Team-MMR': 'Team MMR',
    'Stand': 'current',
    'Ø aller Zeilen': 'Ø of all rows',
    'Ø pro Match': 'Ø per match',
    '{a} von {b}': '{a} of {b}',
    'Keine Werte gewählt': 'No values selected',
  };
  let lang = 'de';
  const formats = {};

  function setLanguage(value) {
    lang = value === 'en' ? 'en' : 'de';
  }

  function L(text) {
    return lang === 'en' && EN[text] !== undefined ? EN[text] : text;
  }

  // 1.234 and 2,5 in German, 1,234 and 2.5 in English.
  function formatter(decimals) {
    const key = lang + (decimals ? '.1' : '');
    if (!formats[key]) {
      formats[key] = new Intl.NumberFormat(lang === 'en' ? 'en-GB' : 'de-DE',
        decimals ? { minimumFractionDigits: 1, maximumFractionDigits: 1 } : undefined);
    }
    return formats[key];
  }

  // Pictures from the official Hunt fan kit (web/img); the app's design settings use them too.
  const IMAGES = [
    { id: 'jaeger', name: 'Jäger im Nebel' },
    { id: 'aufbruch', name: 'Aufbruch' },
    { id: 'duell', name: 'Duell' },
    { id: 'showdown', name: 'Showdown' },
    { id: 'bayou', name: 'Bayou' },
    { id: 'nacht', name: 'Nacht am Sumpf' },
    { id: 'wald', name: 'Dunkler Wald' },
    { id: 'zikade', name: 'Zikade' },
    { id: 'pergament', name: 'Pergament' },
    { id: 'hexe', name: 'Garten der Hexe' },
    { id: 'spielszene', name: 'Spielszene' },
  ];

  const SEGOE = "'Segoe UI Variable Text', 'Segoe UI', system-ui, sans-serif";
  const GARAMOND = "'BL Garamond', Georgia, serif";
  const CRIMSON = "'BL Crimson', Georgia, serif";
  // caps: labels in real small caps instead of spaced capitals. Rye has one weight only.
  const FONTS = {
    hunt: { name: 'Hunt', title: GARAMOND, label: GARAMOND, value: GARAMOND, wTitle: 600, wValue: 600, caps: true },
    crimson: { name: 'Crimson', title: CRIMSON, label: CRIMSON, value: CRIMSON, wTitle: 600, wValue: 700, caps: true },
    western: { name: 'Western', title: "'BL Rye', Georgia, serif", label: CRIMSON, value: "'BL Rye', Georgia, serif", wTitle: 400, wValue: 400, caps: false },
    modern: { name: 'Modern', title: "Georgia, 'Cambria', serif", label: SEGOE, value: SEGOE, wTitle: 600, wValue: 650, caps: false },
    mono: { name: 'Technisch', title: SEGOE, label: SEGOE, value: "'Cascadia Mono', Consolas, monospace", wTitle: 600, wValue: 600, caps: false },
  };
  const LAYOUTS = ['bar', 'card', 'grid', 'line'];
  const BORDERS = ['none', 'top', 'left', 'frame', 'double'];

  // Design templates: everything about the look, nothing about which values are shown.
  const PRESETS = {
    hunt: { name: 'Hunt 1896', font: 'hunt', bgColor: '#0c0a08', bgOpacity: 0.85, bgImage: '', bgImageOpacity: 0.35, text: '#efe7d8', label: '#b3a790', accent: '#d4b06a', ok: '#8fcf9c', bad: '#e0735d', radius: 2, border: 'double', shadow: true, textShadow: false },
    modern: { name: 'Modern', font: 'modern', bgColor: '#100d0a', bgOpacity: 0.86, bgImage: '', bgImageOpacity: 0.35, text: '#f3ede2', label: '#a39a8d', accent: '#e2b25c', ok: '#86d6a2', bad: '#f08477', radius: 9, border: 'top', shadow: true, textShadow: false },
    glas: { name: 'Glas', font: 'modern', bgColor: '#0e0c0a', bgOpacity: 0.42, bgImage: '', bgImageOpacity: 0.35, text: '#ffffff', label: '#d9d3ca', accent: '#f0c674', ok: '#9be3b1', bad: '#ff9a8c', radius: 14, border: 'frame', shadow: false, textShadow: true },
    schrift: { name: 'Nur Schrift', font: 'hunt', bgColor: '#000000', bgOpacity: 0, bgImage: '', bgImageOpacity: 0.35, text: '#ffffff', label: '#e8dcc6', accent: '#e6c07a', ok: '#a3e6b4', bad: '#ff9f8f', radius: 0, border: 'none', shadow: false, textShadow: true },
    steckbrief: { name: 'Steckbrief', font: 'western', bgColor: '#e8dbbd', bgOpacity: 0.97, bgImage: 'pergament', bgImageOpacity: 0.25, text: '#2a1c10', label: '#6d5237', accent: '#8a2f1c', ok: '#2f6b3a', bad: '#9a2a1c', radius: 2, border: 'frame', shadow: true, textShadow: false },
    blutmond: { name: 'Blutmond', font: 'hunt', bgColor: '#140808', bgOpacity: 0.86, bgImage: 'duell', bgImageOpacity: 0.25, text: '#f3e5df', label: '#c29f96', accent: '#e0553f', ok: '#8ccf9f', bad: '#ff6b5e', radius: 3, border: 'left', shadow: true, textShadow: false },
    bayou: { name: 'Bayou', font: 'crimson', bgColor: '#0b120d', bgOpacity: 0.84, bgImage: 'bayou', bgImageOpacity: 0.3, text: '#eaf0e1', label: '#a8b79e', accent: '#c9b458', ok: '#98d8a5', bad: '#e58a6e', radius: 6, border: 'frame', shadow: true, textShadow: false },
    nacht: { name: 'Mitternacht', font: 'modern', bgColor: '#0a0e15', bgOpacity: 0.82, bgImage: 'nacht', bgImageOpacity: 0.35, text: '#e6edf6', label: '#97a7bb', accent: '#9cc2ea', ok: '#86dab0', bad: '#f39393', radius: 10, border: 'top', shadow: true, textShadow: false },
  };
  const DESIGN_KEYS = ['font', 'bgColor', 'bgOpacity', 'bgImage', 'bgImageOpacity', 'text', 'label', 'accent', 'ok', 'bad', 'radius', 'border', 'shadow', 'textShadow'];

  const DEFAULTS = Object.assign({
    name: 'Overlay',
    scope: 'today',
    values: 'sum',
    items: null,
    titleMode: 'auto',
    title: '',
    preset: 'hunt',
    layout: 'bar',
    columns: 3,
    align: 'left',
    labelPos: 'top',
    gap: 1,
    scale: 1,
    showSub: true,
    tone: true,
    animate: true,
  }, stripName(PRESETS.hunt));

  function stripName(preset) {
    const copy = Object.assign({}, preset);
    delete copy.name;
    return copy;
  }

  function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
  }

  function num(value, min, max, fallback) {
    const n = typeof value === 'number' ? value : parseFloat(value);
    return isFinite(n) ? clamp(n, min, max) : fallback;
  }

  function color(value, fallback) {
    return typeof value === 'string' && /^#[0-9a-f]{6}$/i.test(value) ? value.toLowerCase() : fallback;
  }

  function text(value, max) {
    return typeof value === 'string' ? value.slice(0, max) : '';
  }

  function oneOf(value, list, fallback) {
    return list.indexOf(value) >= 0 ? value : fallback;
  }

  function newId() {
    return Math.random().toString(36).slice(2, 8) + Date.now().toString(36).slice(-3);
  }

  function rgba(hex, alpha) {
    const n = parseInt(hex.slice(1), 16);
    return 'rgba(' + ((n >> 16) & 255) + ', ' + ((n >> 8) & 255) + ', ' + (n & 255) + ', ' + alpha + ')';
  }

  // Which values exist for a period, in a sensible order. Player-row MMR is one "Team-MMR" value.
  function available(stats, scope) {
    const fields = (stats && stats.fields) || [];
    if (scope === 'last') {
      return [{ key: 'outcome', label: 'Ausgang', hint: 'Extracted/Died' }]
        .concat(fields.filter((f) => f.type !== 'status').map((f) => ({ key: f.id, label: f.label, hint: f.type === 'text' ? 'Text' : '' })));
    }
    const list = [
      { key: 'matches', label: 'Matches', hint: 'Anzahl' },
      { key: 'rate', label: 'Extraction Rate', hint: '' },
      { key: 'extracted', label: 'Extracted', hint: 'Anzahl' },
      { key: 'died', label: 'Died', hint: 'Anzahl' },
    ];
    for (const f of fields) if (f.type === 'number') list.push({ key: f.id, label: f.label, hint: '' });
    for (const f of fields) if ((f.type === 'stand' || f.type === 'sterne') && !f.rowMmr) list.push({ key: f.id, label: f.label, hint: 'Stand' });
    if (fields.some((f) => f.rowMmr)) list.push({ key: 'teamMmr', label: 'Team-MMR', hint: 'Ø' });
    return list;
  }

  // A new overlay shows the count, the rate and the first two sums.
  function defaultItems(stats) {
    const numbers = ((stats && stats.fields) || []).filter((f) => f.type === 'number').map((f) => f.id);
    const on = ['matches', 'rate'].concat(numbers.slice(0, 2));
    const keys = on.concat(available(stats, 'today').map((i) => i.key).filter((k) => on.indexOf(k) < 0));
    return keys.map((key) => ({ key, on: on.indexOf(key) >= 0, label: '' }));
  }

  // Fills in defaults and checks every value – the result may end up in CSS.
  function normalize(raw, stats) {
    const c = Object.assign({}, DEFAULTS, raw && typeof raw === 'object' ? raw : {});
    const o = {
      id: typeof c.id === 'string' && /^[a-z0-9-]{1,32}$/.test(c.id) ? c.id : newId(),
      name: text(c.name, 40).trim() || 'Overlay',
      scope: SCOPES[c.scope] ? c.scope : 'today',
      values: c.values === 'avg' ? 'avg' : 'sum',
      titleMode: oneOf(c.titleMode, ['auto', 'custom', 'none'], 'auto'),
      title: text(c.title, 60),
      preset: PRESETS[c.preset] ? c.preset : '',
      layout: oneOf(c.layout, LAYOUTS, 'bar'),
      columns: Math.round(num(c.columns, 1, 8, 3)),
      align: oneOf(c.align, ['left', 'center', 'right'], 'left'),
      labelPos: c.labelPos === 'bottom' ? 'bottom' : 'top',
      gap: num(c.gap, 0.3, 3, 1),
      scale: num(c.scale, 0.4, 4, 1),
      showSub: c.showSub !== false,
      tone: c.tone !== false,
      animate: c.animate !== false,
      font: FONTS[c.font] ? c.font : 'hunt',
      bgColor: color(c.bgColor, DEFAULTS.bgColor),
      bgOpacity: num(c.bgOpacity, 0, 1, DEFAULTS.bgOpacity),
      bgImage: IMAGES.some((i) => i.id === c.bgImage) ? c.bgImage : '',
      bgImageOpacity: num(c.bgImageOpacity, 0, 1, DEFAULTS.bgImageOpacity),
      text: color(c.text, DEFAULTS.text),
      label: color(c.label, DEFAULTS.label),
      accent: color(c.accent, DEFAULTS.accent),
      ok: color(c.ok, DEFAULTS.ok),
      bad: color(c.bad, DEFAULTS.bad),
      radius: num(c.radius, 0, 40, DEFAULTS.radius),
      border: oneOf(c.border, BORDERS, DEFAULTS.border),
      shadow: !!c.shadow,
      textShadow: !!c.textShadow,
    };
    const seen = {};
    o.items = Array.isArray(c.items)
      ? c.items.filter((i) => i && typeof i.key === 'string' && i.key && !seen[i.key] && (seen[i.key] = true))
        .slice(0, 80).map((i) => ({ key: i.key.slice(0, 40), on: !!i.on, label: text(i.label, 40) }))
      : defaultItems(stats);
    return o;
  }

  // Old addresses carried their look in the URL (…/overlay?scope=today&show=…&bg=dark). They keep working.
  function fromLegacy(search) {
    const p = new URLSearchParams(search || '');
    const bg = p.get('bg');
    const title = p.has('title') ? p.get('title') : null;
    return normalize(Object.assign(stripName(PRESETS.modern), {
      scope: p.get('scope'),
      values: p.get('values'),
      items: (p.get('show') || 'matches,rate,hunterKills,bounty').split(',').map((k) => ({ key: k.trim(), on: true, label: '' })).filter((i) => i.key),
      layout: p.get('layout') === 'card' ? 'card' : 'bar',
      accent: '#' + (p.get('accent') || 'e2b25c').replace(/[^0-9a-f]/gi, '').slice(0, 6),
      scale: parseFloat(p.get('scale')) || 1,
      bgOpacity: bg === 'glass' ? 0.5 : bg === 'none' ? 0 : 0.86,
      shadow: bg !== 'none' && bg !== 'glass',
      textShadow: bg === 'none',
      border: bg === 'none' ? 'none' : 'top',
      titleMode: title === null ? 'auto' : title === '' ? 'none' : 'custom',
      title: title || '',
    }));
  }

  function formatNumber(value, decimals) {
    if (value === null || value === undefined || value === '') return '–';
    if (decimals && Math.abs(value) >= 100) return formatter(false).format(Math.round(value));
    return formatter(decimals).format(value);
  }

  function statusText(value) {
    if (value === 'extrahiert') return 'Extracted';
    if (value === 'gestorben') return 'Died';
    return '–';
  }

  function toneOf(value) {
    return value === 'extrahiert' ? 'ok' : value === 'gestorben' ? 'bad' : '';
  }

  function buildItems(stats, o) {
    const fields = (stats && stats.fields) || [];
    const byId = {};
    for (const f of fields) byId[f.id] = f;
    const out = [];
    const shown = o.items.filter((i) => i.on);

    if (o.scope === 'last') {
      const match = stats && stats.last;
      const statusField = fields.find((f) => f.type === 'status');
      const outcome = (match && statusField && match.values[statusField.id]) || (match && match.outcome) || '';
      for (const it of shown) {
        let entry = null;
        if (it.key === 'outcome') {
          entry = { label: L('Ausgang'), value: statusText(outcome), tone: toneOf(outcome) };
        } else {
          const f = byId[it.key];
          if (!f) continue;
          const v = match ? match.values[f.id] : null;
          const empty = v === null || v === undefined || v === '';
          if (f.type === 'status') entry = { label: f.label, value: statusText(v), tone: toneOf(v) };
          else if (empty && f.survivorOnly && outcome === 'gestorben') entry = { label: f.label, value: 'Dead', tone: 'bad' };
          else if (empty) entry = { label: f.label, value: '–' };
          else if (f.type === 'sterne') entry = { label: f.label, value: formatNumber(v) + ' ★' };
          else if (f.type === 'number' || f.type === 'stand') entry = { label: f.label, value: formatNumber(v) };
          else entry = { label: f.label, value: String(v) };
        }
        entry.key = it.key;
        if (it.label) entry.label = it.label;
        out.push(entry);
      }
      return out;
    }

    const agg = (stats && stats[o.scope]) || { matches: 0, extracted: 0, died: 0, sums: {}, avgs: {}, lasts: {} };
    const useAvg = o.scope === 'last10' || o.values === 'avg';
    for (const it of shown) {
      let entry = null;
      if (it.key === 'matches') {
        entry = { label: 'Matches', value: formatNumber(agg.matches) };
      } else if (it.key === 'rate') {
        const total = agg.extracted + agg.died;
        entry = {
          label: 'Extraction Rate',
          value: agg.extractionRate === null || agg.extractionRate === undefined ? '–' : Math.round(agg.extractionRate * 100) + ' %',
          sub: total ? L('{a} von {b}').replace('{a}', agg.extracted).replace('{b}', total) : '',
        };
      } else if (it.key === 'extracted') {
        entry = { label: 'Extracted', value: formatNumber(agg.extracted), tone: 'ok' };
      } else if (it.key === 'died') {
        entry = { label: 'Died', value: formatNumber(agg.died), tone: 'bad' };
      } else if (it.key === 'teamMmr') {
        const v = agg.rowMmrAvg;
        entry = { label: L('Team-MMR'), value: v === null || v === undefined ? '–' : formatNumber(v, true) + ' ★', sub: L('Ø aller Zeilen') };
      } else {
        const f = byId[it.key];
        if (!f) continue;
        if (f.type === 'stand' || f.type === 'sterne') {
          // A state (hunter level, MMR): the newest reading, not a sum.
          const v = agg.lasts ? agg.lasts[f.id] : null;
          const known = v !== null && v !== undefined;
          entry = { label: f.label, value: known ? formatNumber(v) + (f.type === 'sterne' ? ' ★' : '') : '–', sub: L('Stand') };
        } else if (f.type === 'number') {
          const avg = agg.avgs ? agg.avgs[f.id] : null;
          const sum = agg.sums ? agg.sums[f.id] : null;
          entry = useAvg
            ? { label: f.label, value: formatNumber(avg, true), sub: L('Ø pro Match') }
            : { label: f.label, value: formatNumber(sum), sub: avg === null || avg === undefined ? '' : 'Ø ' + formatNumber(avg, true) };
        } else {
          continue;
        }
      }
      entry.key = it.key;
      if (it.label) entry.label = it.label;
      out.push(entry);
    }
    return out;
  }

  function el(tag, className, content) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (content !== undefined) node.textContent = content;
    return node;
  }

  // Draws the overlay into container. Values that changed since the last call flash once.
  function render(container, stats, config, options) {
    const o = normalize(config, stats);
    const font = FONTS[o.font];
    const imageBase = (options && options.imageBase) || '/img/';
    const previous = container.kgPrevious || {};
    const current = {};

    const classes = ['kg-ov', 'kg-' + o.layout, 'kg-b-' + o.border, 'kg-a-' + o.align, 'kg-l-' + o.labelPos];
    if (o.shadow) classes.push('kg-shadow');
    if (o.textShadow) classes.push('kg-tshadow');
    if (o.tone) classes.push('kg-tone');
    if (o.animate) classes.push('kg-anim');
    if (!o.showSub) classes.push('kg-nosub');
    if (font.caps) classes.push('kg-caps');
    const root = el('div', classes.join(' '));
    const vars = {
      '--kg-scale': String(o.scale),
      '--kg-bg': rgba(o.bgColor, o.bgOpacity),
      '--kg-text': o.text,
      '--kg-label': o.label,
      '--kg-accent': o.accent,
      '--kg-accent-line': rgba(o.accent, 0.5),
      '--kg-line': rgba(o.label, 0.3),
      '--kg-ok': o.ok,
      '--kg-bad': o.bad,
      '--kg-radius': String(o.radius),
      '--kg-gap': String(o.gap),
      '--kg-cols': String(o.columns),
      '--kg-font-title': font.title,
      '--kg-font-label': font.label,
      '--kg-font-value': font.value,
      '--kg-w-title': String(font.wTitle),
      '--kg-w-value': String(font.wValue),
    };
    for (const key in vars) root.style.setProperty(key, vars[key]);

    if (o.bgImage && o.bgImageOpacity > 0) {
      const img = el('div', 'kg-img');
      img.style.backgroundImage = 'url("' + imageBase + o.bgImage + '.jpg")';
      img.style.opacity = String(o.bgImageOpacity);
      root.appendChild(img);
    }

    const title = o.titleMode === 'none' ? '' : o.titleMode === 'custom' ? o.title : L(SCOPES[o.scope]);
    if (title) root.appendChild(el('div', 'kg-title', title));

    const list = el('div', 'kg-items');
    const items = buildItems(stats, o);
    if (!items.length) list.appendChild(el('div', 'kg-empty', L('Keine Werte gewählt')));
    for (const item of items) {
      current[item.key] = item.value;
      const changed = previous[item.key] !== undefined && previous[item.key] !== item.value;
      const node = el('div', 'kg-item' + (item.tone ? ' kg-' + item.tone : '') + (changed ? ' kg-changed' : ''));
      node.appendChild(el('div', 'kg-label', item.label));
      node.appendChild(el('div', 'kg-value', item.value));
      if (item.sub) node.appendChild(el('div', 'kg-sub', item.sub));
      list.appendChild(node);
    }
    root.appendChild(list);
    container.replaceChildren(root);
    container.kgPrevious = current;
    return root;
  }

  window.KopfgeldOverlay = {
    render, normalize, fromLegacy, available, defaultItems, newId, setLanguage,
    SCOPES, IMAGES, FONTS, LAYOUTS, BORDERS, PRESETS, DESIGN_KEYS,
    // Kept for older callers.
    scopeTitles: SCOPES,
  };

  // Standalone page for the OBS browser source: poll the app's local API.
  //   /overlay?o=<id>  – a design from the program; changes there show up here by themselves
  //   /overlay         – the first design
  //   /overlay?scope=… – an old address with the look in the URL
  if (document.documentElement.hasAttribute('data-overlay-page')) {
    const params = new URLSearchParams(location.search);
    const legacy = ['show', 'layout', 'scope', 'bg', 'accent'].some((k) => params.has(k));
    const legacyConfig = legacy ? fromLegacy(location.search) : null;
    const id = params.get('o');
    // Old addresses too: their look comes from the URL, the values and the language from here.
    const url = '/api/frame' + (id && !legacy ? '?o=' + encodeURIComponent(id) : '');
    const target = document.getElementById('overlay');
    let last = '';
    const tick = async () => {
      try {
        const res = await fetch(url, { cache: 'no-store' });
        const body = await res.text();
        if (body !== last) {
          last = body;
          const data = JSON.parse(body);
          setLanguage(data.lang);
          document.documentElement.lang = lang;
          render(target, data.stats, legacy ? legacyConfig : data.config);
        }
      } catch (e) {
        // HuntXtract is not running right now: keep showing the last state.
      }
      setTimeout(tick, 1000);
    };
    tick();
  }
})();

'use strict';

/* ================================================================= helpers */

// Builds an element. Plain texts and the titles, placeholders and alt texts are shown in the
// chosen language (tr in i18n.js); the code itself is written in German.
function h(tag, props, ...children) {
  const node = document.createElement(tag);
  if (props) {
    for (const [key, value] of Object.entries(props)) {
      if (value === null || value === undefined || value === false) continue;
      if (key === 'class') node.className = value;
      else if (key === 'text') node.textContent = tr(value);
      else if (key === 'style') Object.assign(node.style, value);
      else if (key === 'vars') for (const [k, v] of Object.entries(value)) node.style.setProperty(k, v);
      else if (key === 'dataset') Object.assign(node.dataset, value);
      else if (key.startsWith('on') && typeof value === 'function') node.addEventListener(key.slice(2).toLowerCase(), value);
      else if (key === 'value') node.value = value;
      else if (key === 'checked') node.checked = !!value;
      else if (value === true) node.setAttribute(key, '');
      else node.setAttribute(key, TRANSLATED_PROPS.has(key) ? tr(String(value)) : value);
    }
  }
  for (const child of children.flat(Infinity)) {
    if (child === null || child === undefined || child === false) continue;
    node.append(child instanceof Node ? child : document.createTextNode(tr(String(child))));
  }
  return node;
}

const ICONS = {
  target: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="7"/><path d="M12 2v4M12 18v4M2 12h4M18 12h4"/></svg>',
  camera: '<svg viewBox="0 0 24 24"><path d="M4 8h3l2-3h6l2 3h3v11H4z"/><circle cx="12" cy="13" r="3.5"/></svg>',
  image: '<svg viewBox="0 0 24 24"><rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3 16 5-5 4 4 3-3 6 6"/></svg>',
  plus: '<svg viewBox="0 0 24 24"><path d="M12 5v14M5 12h14"/></svg>',
  x: '<svg viewBox="0 0 24 24"><path d="M6 6l12 12M18 6 6 18"/></svg>',
  trash: '<svg viewBox="0 0 24 24"><path d="M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13"/></svg>',
  pencil: '<svg viewBox="0 0 24 24"><path d="M4 20h4L19 9l-4-4L4 16z"/></svg>',
  hand: '<svg viewBox="0 0 24 24"><path d="M8 13V5.5a1.5 1.5 0 0 1 3 0V11m0-1V4.5a1.5 1.5 0 0 1 3 0V11m0-4.5a1.5 1.5 0 0 1 3 0V14a6 6 0 0 1-6 6h-.5a6 6 0 0 1-4.9-2.5L4 14.3a1.6 1.6 0 0 1 2.5-2L8 13.6"/></svg>',
  info: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M12 11v6M12 7.5v.5"/></svg>',
  shield: '<svg viewBox="0 0 24 24"><path d="M12 3l7 3v5c0 4.6-2.9 8.2-7 10-4.1-1.8-7-5.4-7-10V6z"/><path d="m9 12 2 2 4-4"/></svg>',
  alert: '<svg viewBox="0 0 24 24"><path d="M12 3 2 20h20z"/><path d="M12 10v4M12 17v.5"/></svg>',
  copy: '<svg viewBox="0 0 24 24"><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V4H4v12h4"/></svg>',
  external: '<svg viewBox="0 0 24 24"><path d="M14 4h6v6M20 4l-9 9M18 14v6H4V6h6"/></svg>',
  download: '<svg viewBox="0 0 24 24"><path d="M12 4v11M7 10l5 5 5-5M4 20h16"/></svg>',
  folder: '<svg viewBox="0 0 24 24"><path d="M3 6h6l2 2h10v11H3z"/></svg>',
  play: '<svg viewBox="0 0 24 24"><path d="M7 5v14l11-7z"/></svg>',
  check: '<svg viewBox="0 0 24 24"><path d="m5 12 5 5 9-10"/></svg>',
  trophy: '<svg viewBox="0 0 24 24"><path d="M8 4h8v5a4 4 0 0 1-8 0zM8 6H4v2a3 3 0 0 0 4 3M16 6h4v2a3 3 0 0 1-4 3M12 13v4M8 20h8"/></svg>',
  grip: '<svg viewBox="0 0 24 24" style="fill:currentColor;stroke:none"><circle cx="9" cy="6" r="1.6"/><circle cx="15" cy="6" r="1.6"/><circle cx="9" cy="12" r="1.6"/><circle cx="15" cy="12" r="1.6"/><circle cx="9" cy="18" r="1.6"/><circle cx="15" cy="18" r="1.6"/></svg>',
  up: '<svg viewBox="0 0 24 24"><path d="m6 15 6-6 6 6"/></svg>',
  down: '<svg viewBox="0 0 24 24"><path d="m6 9 6 6 6-6"/></svg>',
  left: '<svg viewBox="0 0 24 24"><path d="m15 6-6 6 6 6"/></svg>',
  right: '<svg viewBox="0 0 24 24"><path d="m9 6 6 6-6 6"/></svg>',
};

function icon(name) {
  const template = document.createElement('template');
  template.innerHTML = ICONS[name] || '';
  return template.content.firstChild;
}

const clamp = (v, min, max) => Math.min(max, Math.max(min, v));
// Numbers and dates in the format of the language: 1.234 and 2,5 in German, 1,234 and 2.5 in English.
const numberFormats = {};
const numberFormat = { format: (v) => (numberFormats[localeTag()] = numberFormats[localeTag()] || new Intl.NumberFormat(localeTag())).format(v) };
const decimalFormat = {
  format: (v) => (numberFormats['1' + localeTag()] = numberFormats['1' + localeTag()] ||
    new Intl.NumberFormat(localeTag(), { minimumFractionDigits: 1, maximumFractionDigits: 1 })).format(v),
};

const fmt = {
  int: (v) => (v === null || v === undefined || v === '' ? '–' : numberFormat.format(v)),
  dec: (v) => (v === null || v === undefined ? '–' : Math.abs(v) >= 100 ? numberFormat.format(Math.round(v)) : decimalFormat.format(v)),
  pct: (v) => (v === null || v === undefined ? '–' : Math.round(v * 100) + ' %'),
  date: (iso) => new Date(iso).toLocaleDateString(localeTag(), { day: '2-digit', month: '2-digit', year: 'numeric' }),
  time: (iso) => new Date(iso).toLocaleTimeString(localeTag(), { hour: '2-digit', minute: '2-digit' }),
  // Seconds as 0:42 or 1:05.
  dur: (s) => (s === null || s === undefined ? '–' : Math.floor(s / 60) + ':' + String(Math.round(s) % 60).padStart(2, '0')),
  rel(iso) {
    const s = (Date.now() - new Date(iso).getTime()) / 1000;
    if (s < 60) return t('gerade eben');
    if (s < 3600) return t('vor {n} Min.', { n: Math.floor(s / 60) });
    if (s < 86400) return t('vor {n} Std.', { n: Math.floor(s / 3600) });
    const d = Math.floor(s / 86400);
    return d === 1 ? t('gestern') : t('vor {n} Tagen', { n: d });
  },
};

function statusBadge(value) {
  if (value === 'extrahiert') return h('span', { class: 'badge ok' }, 'Extracted');
  if (value === 'gestorben') return h('span', { class: 'badge bad' }, 'Died');
  return h('span', { class: 'badge neutral' }, 'Unbekannt');
}

const store = {
  get(key, fallback) {
    try {
      const v = localStorage.getItem('kopfgeld.' + key);
      return v === null ? fallback : JSON.parse(v);
    } catch (e) {
      return fallback;
    }
  },
  set(key, value) {
    try {
      localStorage.setItem('kopfgeld.' + key, JSON.stringify(value));
    } catch (e) { /* not important */ }
  },
};

/* ================================================================= host bridge */

const api = (() => {
  const webview = window.chrome && window.chrome.webview;
  let nextId = 1;
  const pending = new Map();
  const listeners = {};
  if (webview) {
    webview.addEventListener('message', (e) => {
      const msg = e.data;
      if (!msg || typeof msg !== 'object') return;
      if (msg.reply !== undefined && pending.has(msg.reply)) {
        const p = pending.get(msg.reply);
        pending.delete(msg.reply);
        if (msg.ok) p.resolve(msg.result);
        else p.reject(new Error(msg.error || 'Unbekannter Fehler'));
        return;
      }
      if (msg.event) (listeners[msg.event] || []).forEach((fn) => fn(msg.data));
    });
  }
  return {
    call(cmd, args) {
      if (!webview) return Promise.reject(new Error('Nur im HuntXtract-Programm verfügbar'));
      const id = nextId++;
      return new Promise((resolve, reject) => {
        pending.set(id, { resolve, reject });
        webview.postMessage({ id, cmd, args: args || {} });
      });
    },
    on(event, fn) {
      (listeners[event] = listeners[event] || []).push(fn);
    },
  };
})();

/* ================================================================= state */

const state = {
  app: null,
  settings: null,
  calibration: null,
  matches: [],
  stats: null,
  status: null,
  view: null,
  matchFilter: store.get('matchFilter', 'today'),
  mapPeriod: store.get('mapPeriod', 'all'),
  chartField: store.get('chartField', null),
  freshUntil: 0,
};

const numericFields = () => (state.stats ? state.stats.fields.filter((f) => f.type === 'number') : []);
// "Stand" and star fields hold a state (hunter level, MMR): the newest reading counts, not a sum.
const standFields = () => (state.stats ? state.stats.fields.filter((f) => f.type === 'stand' || f.type === 'sterne') : []);
const statusField = () => (state.stats ? state.stats.fields.find((f) => f.type === 'status') : null);
const valueFields = () => (state.stats ? state.stats.fields.filter((f) => f.type !== 'status') : []);
const isNumberType = (type) => type === 'number' || type === 'stand' || type === 'sterne';

// Extracted/died: the read status field if the end screen shows one, otherwise set by hand.
function outcomeOf(match) {
  const sf = statusField();
  const read = sf && match ? match.values[sf.id] : null;
  return read || (match && match.outcome) || '';
}

function outcomeBadge(value, clickable) {
  const node = value === 'extrahiert' ? h('span', { class: 'badge ok' }, 'Extracted')
    : value === 'gestorben' ? h('span', { class: 'badge bad' }, 'Died')
    : h('span', { class: 'badge neutral' }, clickable ? 'Ausgang?' : 'Unbekannt');
  if (clickable) {
    node.classList.add('clickable');
    node.title = t('Klicken: Extracted → Died → Unbekannt');
  }
  return node;
}

async function setOutcome(match, value) {
  const sf = statusField();
  const args = { id: match.id };
  if (sf) args.values = { [sf.id]: value };
  else args.outcome = value;
  try {
    await api.call('updateMatch', args);
  } catch (e) {
    toast('error', e.message);
  }
}

function nextOutcome(value) {
  return value === '' ? 'extrahiert' : value === 'extrahiert' ? 'gestorben' : '';
}

// Badge that cycles the outcome with each click.
function outcomeToggle(match) {
  const current = outcomeOf(match);
  const badge = outcomeBadge(current, true);
  badge.addEventListener('click', (e) => {
    e.stopPropagation();
    setOutcome(match, nextOutcome(current));
  });
  return badge;
}

async function saveSettings(partial, message) {
  try {
    state.settings = await api.call('saveSettings', partial);
    if (message) toast('ok', message);
    return true;
  } catch (e) {
    toast('error', e.message);
    return false;
  }
}

/* ================================================================= toasts & modal */

function toast(kind, text, ms) {
  const node = h('div', { class: 'toast ' + kind }, text);
  document.getElementById('toasts').append(node);
  setTimeout(() => {
    node.classList.add('leaving');
    setTimeout(() => node.remove(), 250);
  }, ms || (kind === 'error' ? 8000 : 4500));
}

const modal = {
  onClose: null,
  open(content, onClose) {
    const backdrop = document.getElementById('modal');
    backdrop.replaceChildren(content);
    backdrop.hidden = false;
    this.onClose = onClose || null;
  },
  close() {
    const backdrop = document.getElementById('modal');
    if (backdrop.hidden) return;
    backdrop.hidden = true;
    backdrop.replaceChildren();
    const fn = this.onClose;
    this.onClose = null;
    if (fn) fn();
    // Data that arrived while the dialog was open has not been drawn yet.
    if (state.staleView && !this.isOpen) refreshAfterData();
  },
  get isOpen() {
    return !document.getElementById('modal').hidden;
  },
};

// keepLabel: the button that changes nothing, when "Abbrechen" would be mistaken for the action.
function confirmDialog(title, text, confirmLabel, danger, keepLabel) {
  return new Promise((resolve) => {
    let result = false;
    const box = h('div', { class: 'modal narrow' },
      h('div', { class: 'modal-head' }, h('h2', {}, title)),
      h('div', { class: 'modal-body' }, h('p', { class: 'muted' }, text)),
      h('div', { class: 'modal-foot' },
        h('button', { class: 'btn ghost', onclick: () => modal.close() }, keepLabel || 'Abbrechen'),
        h('button', { class: 'btn ' + (danger ? 'danger' : 'primary'), onclick: () => { result = true; modal.close(); } }, confirmLabel)));
    modal.open(box, () => resolve(result));
  });
}

// Pictures in full width with tabs to switch between them; with two or more also arrows on the
// picture and ← → on the keyboard (onKey, to be hooked up while the dialog is open).
// A shot with a trail (the map) gets the trail drawn over it.
function shotViewer(shots, start) {
  let index = 0;
  const img = h('img', { alt: '' });
  const layer = h('div', { class: 'trail-layer' });
  const caption = h('div', { class: 'shot-caption' });
  caption.hidden = true;
  const picture = h('div', { class: 'shot-picture' }, img, layer, caption);
  const tabs = shots.length > 1
    ? h('div', { class: 'segmented' }, shots.map((s, i) => h('button', { onclick: () => go(i) }, s.label)))
    : h('span', { class: 'section-label' }, shots[0].label);
  const frame = h('div', { class: 'shot-frame' }, picture,
    shots.length > 1 && h('button', { class: 'shot-nav prev', title: 'Vorheriges Bild (←)', onclick: () => go(index - 1) }, icon('left')),
    shots.length > 1 && h('button', { class: 'shot-nav next', title: 'Nächstes Bild (→)', onclick: () => go(index + 1) }, icon('right')));
  function go(i) {
    index = (i + shots.length) % shots.length;
    const shot = shots[index];
    img.src = shot.url;
    img.alt = tr(shot.label);
    picture.classList.toggle('map', !!shot.trail);
    caption.hidden = true;
    layer.replaceChildren(...(shot.trail ? [trailSvg(shot.trail, shot.outcome, pointHover(img, caption, shot.trail, shot.url))] : []));
    if (shots.length > 1) tabs.querySelectorAll('button').forEach((b, n) => b.classList.toggle('active', n === index));
  }
  go(start || 0);
  return {
    el: h('div', { class: 'shot-viewer' }, h('div', { class: 'shot-tabs' }, tabs), frame),
    onKey(e) {
      if (shots.length < 2 || (e.target && /^(INPUT|SELECT|TEXTAREA)$/.test(e.target.tagName))) return;
      if (e.key === 'ArrowLeft') go(index - 1);
      else if (e.key === 'ArrowRight') go(index + 1);
    },
  };
}

// The trail of a match on its map: the position from each look at the map, joined up as a
// marching dashed line, from the start (ring) to the end (dot – green extracted, red died).
// outcome 'live': the match is still running, the last position pulses.
// onPoint(index | null): points with a picture of their moment get a target to hover; called
// when the mouse enters one (and with null when it leaves).
function trailSvg(trail, outcome, onPoint) {
  const ns = 'http://www.w3.org/2000/svg';
  const el = (tag, attrs, title) => {
    const node = document.createElementNS(ns, tag);
    for (const [key, value] of Object.entries(attrs)) node.setAttribute(key, value);
    if (title) {
      const tip = document.createElementNS(ns, 'title');
      tip.textContent = tr(title);
      node.append(tip);
    }
    return node;
  };
  const svg = el('svg', { viewBox: '0 0 1000 1000', preserveAspectRatio: 'none' });
  const xy = trail.map(([x, y]) => [Math.round(x * 10000) / 10, Math.round(y * 10000) / 10]);
  if (xy.length > 1) {
    const points = xy.map(([x, y]) => x + ',' + y).join(' ');
    svg.append(el('polyline', { points, class: 'trail-shadow', 'vector-effect': 'non-scaling-stroke' }));
    svg.append(el('polyline', { points, class: 'trail-ants', 'vector-effect': 'non-scaling-stroke' }));
  }
  for (const [x, y] of xy.slice(1, -1)) svg.append(el('circle', { cx: x, cy: y, r: 4.5, class: 'trail-dot' }));
  const [sx, sy] = xy[0], [ex, ey] = xy[xy.length - 1];
  if (xy.length > 1) svg.append(el('circle', { cx: sx, cy: sy, r: 11, class: 'trail-start' }, 'Start'));
  const end = outcome === 'gestorben' ? ' died' : outcome === 'extrahiert' ? ' extracted' : '';
  if (outcome === 'live') svg.append(el('circle', { cx: ex, cy: ey, r: 13, class: 'trail-pulse' }));
  svg.append(el('circle', { cx: ex, cy: ey, r: 13, class: 'trail-end' + end },
    outcome === 'live' ? 'Zuletzt hier' : xy.length > 1 ? 'Ende' : 'Position'));
  if (onPoint && trail.some((p) => p[3])) {
    xy.forEach(([x, y], i) => {
      if (trail[i][3]) svg.append(el('circle', { cx: x, cy: y, r: 18, class: 'trail-hit', 'data-i': i }));
    });
    svg.addEventListener('mouseover', (e) => {
      const i = e.target.getAttribute && e.target.getAttribute('data-i');
      if (i === null || i === undefined) return;
      e.target.classList.add('active');
      onPoint(Number(i));
    });
    svg.addEventListener('mouseout', (e) => {
      if (!e.target.classList || !e.target.classList.contains('trail-hit')) return;
      e.target.classList.remove('active');
      onPoint(null);
    });
  }
  return svg;
}

// "Punkt 5 von 12 · 3:25 min" – when a point of the trail was taken.
function pointLabel(i, trail) {
  const s = trail[i][2] || 0;
  return t('Punkt {n} von {total} · {time} min', { n: i + 1, total: trail.length, time: Math.floor(s / 60) + ':' + String(s % 60).padStart(2, '0') });
}

// Hovering a point shows the map of its moment in img, with a caption; leaving it, the map again
// (and a hint that the points can be hovered, if they have pictures).
function pointHover(img, caption, trail, url) {
  const hint = trail.some((p) => p[3]) ? tr('Fahr über einen Punkt – dann siehst du die Karte aus diesem Moment.') : '';
  const show = (i) => {
    const point = i === null ? null : trail[i];
    const src = point && point[3] ? point[3] : url;
    if (src && img.getAttribute('src') !== src) img.src = src;
    caption.textContent = point ? pointLabel(i, trail) : hint;
    caption.hidden = !caption.textContent;
  };
  show(null);
  return show;
}

/* ================================================================= live trail */

// The trail of the match that is running: fetched whenever the host reports a change and shown
// on the overview, where it grows with every look at the map.
const liveTrail = {
  data: null,
  version: -1,
  card: null,
  parts: null,
  filled: null,

  // Called with every status: fetches the trail when its version changed.
  async sync(st) {
    if (st && st.trailVersion !== undefined && st.trailVersion !== this.version) {
      this.version = st.trailVersion;
      try {
        this.data = await api.call('liveTrail');
        this.version = Math.max(this.version, this.data.version);
      } catch (e) { /* the next status tries again */ }
    }
    this.update();
  },

  // Hunt's own log says a match is on (hidden while Hunt isn't running or the log isn't found) –
  // unless it was cancelled here.
  get inMatch() {
    const hunt = state.status && state.status.hunt;
    return !!(hunt && hunt.phase === 'Match' && !state.status.matchCancelled);
  },

  // Running: Hunt says so – or the trail has points and the match can't be long over.
  get running() {
    const d = this.data;
    return this.inMatch || !!(d && d.points.length && Date.now() - new Date(d.lastSeen).getTime() < 30 * 60000);
  },

  // The card for the overview, or null while no match is running.
  render() {
    this.card = null;
    this.filled = null;
    if (!this.running) return null;
    const img = h('img', { alt: '' });
    const layer = h('div', { class: 'trail-layer' });
    const empty = h('div', { class: 'live-empty' }, 'Drück im Match Tab – bei jedem Blick auf die Karte wächst hier deine Spur.');
    const caption = h('div', { class: 'shot-caption' });
    caption.hidden = true;
    const name = h('div', { class: 'live-map-name' });
    const facts = h('div', { class: 'kv' });
    this.parts = { img, layer, empty, caption, name, facts };
    this.card = h('div', { class: 'card live-trail' },
      h('div', { class: 'card-head' }, h('h3', {}, 'Laufendes Match'),
        h('span', { class: 'live-badge' }, h('span', { class: 'live-dot' }), 'Live'),
        h('span', { class: 'spacer' }),
        h('button', {
          class: 'btn small ghost',
          title: 'Nach einem Absturz oder Verbindungsabbruch: nicht länger auf die Mission Summary dieses Matches warten',
          onclick: () => this.cancel(),
        }, icon('x'), 'Match abbrechen')),
      h('div', { class: 'live-body' },
        h('div', { class: 'live-map' }, h('div', { class: 'shot-picture map' }, img, layer, empty, caption)),
        h('div', { class: 'live-info' }, name, facts,
          h('p', { class: 'small muted' }, 'Die Spur wächst bei jedem Blick auf die Karte (Tab). Mit der Mission Summary wird sie beim Match gespeichert.'))));
    this.fill();
    return this.card;
  },

  // After a crash or a lost connection no Mission Summary comes: the trail so far is dropped and
  // the card goes until the next match.
  async cancel() {
    if (!(await confirmDialog('Laufendes Match abbrechen?',
      'Nach einem Absturz oder Verbindungsabbruch kommt keine Mission Summary. HuntXtract wartet dann nicht länger auf dieses Match und verwirft seine Laufspur. Das nächste Match wird wie gewohnt erfasst, gespeicherte Matches bleiben unverändert.',
      'Match abbrechen', true, 'Weiter verfolgen'))) return;
    try {
      await api.call('cancelMatch');
      toast('ok', 'Match abgebrochen – HuntXtract wartet auf das nächste.');
    } catch (e) {
      toast('error', e.message);
    }
  },

  // New data: the card is brought up to date – or the overview drawn anew when it comes or goes.
  update() {
    if (state.view !== 'dashboard') return;
    if (!!this.card !== this.running) {
      if (modal.isOpen) state.staleView = true;
      else rerender();
      return;
    }
    this.fill();
  },

  fill() {
    const d = this.data || { version: -1, points: [] }, p = this.parts;
    const hunt = (state.status && state.status.hunt) || {};
    const key = d.version + '|' + hunt.phase + '|' + hunt.map;
    if (!this.card || this.filled === key) return;
    this.filled = key;
    const clock = (time) => time ? t('{time} Uhr', { time: fmt.time(time) }) : '–';
    // Before the first look the map and the start come from Hunt's log.
    p.name.textContent = d.mapName || hunt.map || tr('Karte noch nicht erkannt');
    p.facts.replaceChildren(
      h('div', { class: 'k' }, 'Start'), h('div', { class: 'v' }, clock(d.started || (this.inMatch ? hunt.since : null))),
      h('div', { class: 'k' }, 'Punkte'), h('div', { class: 'v num' }, fmt.int(d.points.length)),
      h('div', { class: 'k' }, 'Letzter Blick'), h('div', { class: 'v' }, clock(d.lastSeen)));
    // The same img keeps showing the old map until the new one has loaded – no flicker.
    if (d.pictureUrl && p.img.getAttribute('src') !== d.pictureUrl) p.img.src = d.pictureUrl;
    p.img.hidden = !d.pictureUrl;
    p.empty.hidden = d.points.length > 0;
    p.caption.hidden = true;
    p.layer.replaceChildren(...(d.points.length ? [trailSvg(d.points, 'live', pointHover(p.img, p.caption, d.points, d.pictureUrl))] : []));
  },
};

// A finished setup to look at: where the frames go on the Mission Summary and the Team Details.
function showExamples(start) {
  const viewer = shotViewer([
    { label: 'Mission Summary', url: '/img/vorlage-summary.jpg' },
    { label: 'Team Details', url: '/img/vorlage-team.jpg' },
  ], start);
  const box = h('div', { class: 'modal' },
    h('div', { class: 'modal-head' }, h('h2', {}, 'Beispiel: so liegen die Rahmen'),
      h('button', { class: 'icon-btn', title: 'Schließen', onclick: () => modal.close() }, icon('x'))),
    h('div', { class: 'modal-body' }, viewer.el,
      h('p', { class: 'small muted', style: { marginTop: '12px' } },
        'Eine fertige Einrichtung bei 1920 × 1080. Bei dir liegen die Rahmen an ungefähr denselben Stellen – je nach Auflösung leicht versetzt. ' +
        'Gestrichelt ist die Wiedererkennung, die Überschrift der Seite. Die Namen anderer Spieler sind unkenntlich gemacht.')));
  window.addEventListener('keydown', viewer.onKey);
  modal.open(box, () => window.removeEventListener('keydown', viewer.onKey));
}

function showImage(url, title) {
  const box = h('div', { class: 'modal' },
    h('div', { class: 'modal-head' }, h('h2', {}, title || 'Screenshot'),
      h('button', { class: 'icon-btn', title: 'Schließen', onclick: () => modal.close() }, icon('x'))),
    h('div', { class: 'modal-body' }, h('img', { src: url, class: 'shot', style: { width: '100%', display: 'block', borderRadius: '7px' } })));
  modal.open(box);
}

/* ================================================================= status pills */

const OBS_PILL = {
  Ready: ['ok', 'OBS verbunden'],
  Connecting: ['warn', 'Verbinde mit OBS …'],
  Unreachable: ['bad', 'OBS nicht erreichbar'],
  AuthFailed: ['bad', 'OBS-Passwort falsch'],
  NoSource: ['warn', 'Keine Quelle gewählt'],
  SourceMissing: ['bad', 'Quelle fehlt in OBS'],
  NoImage: ['warn', 'Quelle liefert kein Bild'],
};

// Straight from the monitor only Hunt is read – why there is no picture right now.
const HUNT_HIDDEN = {
  NotInFront: ['', 'Hunt nicht im Vordergrund'],
  NotRunning: ['', 'Hunt läuft nicht'],
  OtherScreen: ['warn', 'Hunt auf anderem Bildschirm'],
};

const DETECT_PILL = {
  Watching: ['ok', 'Wartet auf Mission Summary'],
  Paused: ['warn', 'Pausiert – Einrichtung offen'],
  Settling: ['busy', 'Mission Summary erkannt'],
  Reading: ['busy', 'Liest aus …'],
  Cooldown: ['ok', 'Match gespeichert'],
  NotCalibrated: ['warn', 'Nicht eingerichtet'],
  Off: ['', 'Erkennung aus'],
};

function renderStatus() {
  const st = state.status;
  if (!st) return;
  let [obsTone, obsText] = OBS_PILL[st.obs] || ['', st.obs];
  // Reading straight from a monitor: OBS is not part of the picture then.
  const fromScreen = state.settings && state.settings.captureSource === 'screen';
  if (fromScreen) obsText = st.obs === 'Ready' ? 'Bildschirm wird gelesen' : st.obs === 'NoImage' ? 'Bildschirm nicht lesbar' : obsText;
  // Only Hunt is ever read from the screen: while it isn't in front, say so instead.
  const hidden = fromScreen && st.obs === 'Ready' ? HUNT_HIDDEN[st.huntHidden] : null;
  if (hidden) [obsTone, obsText] = hidden;
  const obsPill = document.getElementById('pillObs');
  obsPill.querySelector('.dot').className = 'dot ' + obsTone;
  obsPill.querySelector('.pill-text').textContent = t(obsText);
  obsPill.title = hidden ? tr('HuntXtract nimmt nur Hunt auf. Solange Hunt nicht im Vordergrund ist, bleibt das Bild schwarz – dein Desktop und andere Programme werden nicht gelesen.') : (st.obsText || '');

  let [detectTone, detectText] = DETECT_PILL[st.detect] || ['', st.detect];
  if (st.obs !== 'Ready' && (st.detect === 'Watching' || st.detect === 'Off')) {
    detectTone = '';
    detectText = fromScreen ? 'Erkennung wartet auf ein Bild' : 'Erkennung wartet auf OBS';
  } else if (hidden && (st.detect === 'Watching' || st.detect === 'Off')) {
    detectTone = '';
    detectText = 'Erkennung pausiert';
  }
  const detectPill = document.getElementById('pillDetect');
  detectPill.querySelector('.dot').className = 'dot ' + detectTone;
  detectPill.querySelector('.pill-text').textContent = t(detectText);
  detectPill.title = (st.detectText || '') +
    (st.similarity !== null && st.similarity !== undefined ? t(' · Ähnlichkeit {n} %', { n: Math.round(st.similarity) }) : '');

  // Where Hunt is, from its own log – hidden while Hunt isn't running.
  const hunt = st.hunt || {};
  const huntPill = document.getElementById('pillHunt');
  huntPill.hidden = !hunt.phase || hunt.phase === 'None';
  if (!huntPill.hidden) {
    const minutes = hunt.since ? Math.max(0, Math.floor((Date.now() - new Date(hunt.since).getTime()) / 60000)) : 0;
    // A match cancelled by hand (Hunt's log may still report it after a crash).
    const cancelled = hunt.phase === 'Match' && st.matchCancelled;
    huntPill.querySelector('.dot').className = 'dot ' + ((hunt.phase === 'Match' && !cancelled) || hunt.phase === 'Found' ? 'ok'
      : hunt.phase === 'Loading' || hunt.phase === 'Searching' ? 'busy' : '');
    huntPill.querySelector('.pill-text').textContent =
      cancelled ? tr('Match abgebrochen')
      : hunt.phase === 'Searching' ? t('Sucht Match · {time}', { time: fmt.dur(searchSeconds(hunt)) })
      : hunt.phase === 'Found' ? t('Lobby gefunden · wartet {time}', { time: fmt.dur(hunt.since ? (Date.now() - new Date(hunt.since).getTime()) / 1000 : 0) })
      : hunt.phase === 'Match' ? (hunt.map ? t('Im Match · {map} · {n} min', { map: hunt.map, n: minutes }) : t('Im Match · {n} min', { n: minutes }))
        : hunt.phase === 'Loading' ? (hunt.map ? t('Lädt {map} …', { map: hunt.map }) : tr('Lädt …'))
          : hunt.phase === 'Summary' ? 'Mission Summary'
            : hunt.phase === 'Range' ? (hunt.level === 'shooting' ? tr('Schießstand') : t('Im Level {name}', { name: hunt.level || '?' }))
              : tr('Im Menü');
    huntPill.title = tr('Aus Hunts eigenem Protokoll');
  }

  const setupButton = document.querySelector('#nav button[data-view="setup"]');
  const needsSetup = !st.calibrated || st.obs === 'NoSource';
  const dot = setupButton.querySelector('.badge-dot');
  if (needsSetup && !dot) setupButton.append(h('span', { class: 'badge-dot', title: 'Einrichtung nicht abgeschlossen' }));
  if (!needsSetup && dot) dot.remove();
  live.update(st);
}

// Bar under the top bar: counts down while the Mission Summary is still handing out points.
const live = {
  mode: null,
  timer: null,
  deadline: 0,
  started: 0,
  counting: false,
  update(st) {
    const countdown = st.detect === 'Settling' && st.settleRemainingMs >= 0;
    if (countdown) {
      const now = Date.now();
      const deadline = now + st.settleRemainingMs;
      const started = now - st.settleElapsedMs;
      // One report per captured frame – ignore small differences so the seconds don't jump.
      if (this.mode !== 'countdown' || Math.abs(deadline - this.deadline) > 400) this.deadline = deadline;
      if (this.mode !== 'countdown' || Math.abs(started - this.started) > 1500) this.started = started;
      this.counting = !!st.counting;
      this.mode = 'countdown';
      if (!this.timer) this.timer = setInterval(() => this.draw(), 200);
    } else {
      this.mode = st.detect === 'Settling' || st.detect === 'Reading' ? 'reading' : null;
      clearInterval(this.timer);
      this.timer = null;
    }
    this.draw();
  },
  draw() {
    const bar = document.getElementById('liveBar');
    bar.hidden = !this.mode;
    if (!this.mode) return;
    const count = document.getElementById('liveCount');
    const progress = document.getElementById('liveProgress');
    if (this.mode === 'countdown') {
      const now = Date.now();
      const seconds = Math.ceil(Math.max(0, this.deadline - now) / 1000);
      document.getElementById('liveTitle').textContent = t('Mission Summary erkannt');
      document.getElementById('liveDetail').textContent = this.counting
        ? t('Die Punkte werden noch vergeben – HuntXtract liest aus, sobald sich nichts mehr ändert.')
        : t('Bitte die Mission Summary offen lassen, bis das Match gespeichert ist.');
      count.hidden = false;
      count.querySelector('b').textContent = seconds > 0 ? seconds + ' s' : '…';
      progress.style.width = clamp((now - this.started) / Math.max(1, this.deadline - this.started), 0, 1) * 100 + '%';
      document.querySelector('#pillDetect .pill-text').textContent = seconds > 0
        ? t('Mission Summary erkannt · {n} s', { n: seconds }) : t('Mission Summary erkannt');
    } else {
      document.getElementById('liveTitle').textContent = t('Lese Werte aus …');
      document.getElementById('liveDetail').textContent = t('Gleich fertig.');
      count.hidden = true;
      progress.style.width = '100%';
    }
  },
};

/* ================================================================= views */

const views = {
  dashboard: { title: 'Übersicht', render: renderDashboard },
  matches: { title: 'Matches', render: renderMatches },
  setup: { title: 'Einrichtung', render: () => setup.render(), enter: () => setup.enter(), leave: () => setup.leave() },
  overlay: { title: 'Overlay', render: () => overlayView.render() },
  settings: { title: 'Einstellungen', render: renderSettings },
};

function show(name) {
  if (state.view && views[state.view].leave) views[state.view].leave();
  state.view = name;
  document.querySelectorAll('#nav button').forEach((b) => b.classList.toggle('active', b.dataset.view === name));
  document.getElementById('viewTitle').textContent = t(views[name].title);
  const container = document.getElementById('view');
  container.replaceChildren(views[name].render());
  container.scrollTop = 0;
  if (views[name].enter) views[name].enter();
}

function rerender() {
  const container = document.getElementById('view');
  const scroll = container.scrollTop;
  container.replaceChildren(views[state.view].render());
  container.scrollTop = scroll;
}

// New stats/matches from the host: redraw views that show them. The setup page is left
// alone so an unsaved field layout is never thrown away.
function refreshAfterData() {
  state.staleView = false;
  if (state.view === 'dashboard' || state.view === 'matches') rerender();
  else if (state.view === 'overlay') {
    overlayView.renderPreview();
    overlayView.renderPresets();
  }
}

// The resolution the ready-made setup is for here, as the program sees the game ("2560 × 1440").
const presetResolution = () => (state.app && state.app.preset && state.app.preset.resolution) || '16:9';

/* ================================================================= dashboard */

function renderDashboard() {
  const s = state.stats;
  const root = h('div', { class: 'stack' });
  const fresh = Date.now() < state.freshUntil;

  if (!state.calibration.ready) {
    root.append(h('div', { class: 'banner' }, icon('target'),
      h('div', { class: 'grow' }, h('h3', {}, 'Noch nicht eingerichtet'),
        h('p', {}, 'Zeig HuntXtract einmal deine Mission Summary – danach zählt es jedes Match automatisch.')),
      h('button', { class: 'btn primary', onclick: () => show('setup') }, 'Einrichtung starten')));
  } else if (state.calibration.preset && !state.matches.length) {
    // Set up with the preset: nothing to do but play, until the first match is in.
    root.append(h('div', { class: 'banner' }, icon('check'),
      h('div', { class: 'grow' }, h('h3', {}, t('Schon eingerichtet für {res}', { res: presetResolution() })),
        h('p', {}, 'HuntXtract hat die mitgelieferten Rahmen für Mission Summary, Team Details und Karte übernommen. Spiel einfach ein Match – die Mission Summary wird von selbst erkannt.')),
      h('button', { class: 'btn', onclick: () => show('setup') }, 'Einrichtung ansehen')));
  }

  // While a match runs: its trail so far, live.
  const liveCard = liveTrail.render();
  if (liveCard) root.append(liveCard);

  root.append(todaySection(s, fresh));

  const numeric = numericFields();
  const hasStatus = !!s.hasOutcome;
  root.append(h('div', { class: 'grid-2' }, lastMatchCard(s.last), last10Card(s.last10, numeric, hasStatus)));
  root.append(chartCard(numeric));
  root.append(mapsCard());
  const searches = searchCard();
  if (searches) root.append(searches);
  root.append(allTimeCard(s.all, numeric, hasStatus));
  return root;
}

// Today in three tiers: how it went, what came together, and where you stand now.
function todaySection(s, fresh) {
  const today = s.today;
  const numeric = numericFields();
  const hasStatus = !!s.hasOutcome;
  const box = h('div', {});
  box.append(h('div', { class: 'section-head' }, h('h2', {}, 'Heute'),
    h('span', { class: 'muted small' }, t('seit {time} Uhr', { time: fmt.time(s.dayStart) }))));

  // Tier 1: the day at a glance.
  const hero = h('div', { class: 'card hero' + (fresh ? ' fresh' : '') },
    h('div', { class: 'hero-count' },
      h('div', { class: 'big num' }, fmt.int(today.matches)),
      h('div', { class: 'label' }, today.matches === 1 ? 'Match heute' : 'Matches heute')));
  if (hasStatus && today.matches) {
    const total = today.extracted + today.died;
    const share = total ? Math.round((today.extracted / total) * 100) : 0;
    hero.append(h('div', { class: 'hero-rate' },
      h('div', { class: 'row' },
        h('b', { class: 'num' }, today.extractionRate === null ? '–' : fmt.pct(today.extractionRate)),
        h('span', { class: 'muted small' }, 'Extraction Rate'),
        h('span', { class: 'grow' }),
        h('span', { class: 'small', style: { color: 'var(--ok)' } }, fmt.int(today.extracted) + ' Extracted'),
        h('span', { class: 'small', style: { color: 'var(--bad)' } }, fmt.int(today.died) + ' Died')),
      h('div', { class: 'ratebar' }, h('span', { style: { width: share + '%' } }))));
  } else {
    hero.append(h('div', { class: 'hero-rate' }, h('span', { class: 'muted small' },
      today.matches ? 'Setz den Ausgang bei deinen Matches, dann steht hier deine Extraction Rate.' : 'Sobald ein Match gespeichert ist, steht hier deine Bilanz des Tages.')));
  }
  box.append(hero);

  // Tier 2: what added up today.
  if (numeric.length) {
    box.append(h('div', { class: 'tier-label' }, 'Summen heute'));
    if (today.matches) {
      const cards = h('div', { class: 'cards' }, numeric.map((f) => statCard(f.label, fmt.int(today.sums[f.id]),
        today.avgs[f.id] !== null ? t('Ø {n} pro Match', { n: fmt.dec(today.avgs[f.id]) }) : '', '', fresh)));
      // Teammates whose name is written in Cyrillic.
      if (nameFields().length) {
        const start = new Date(s.dayStart).getTime();
        const list = state.matches.filter((m) => new Date(m.time).getTime() >= start);
        const hits = list.filter(hasCyrillicMate).length;
        cards.append(statCard('Kyrillische Mitspieler', fmt.int(hits),
          list.length ? t('in {hits} von {total} Matches · {pct} %', { hits, total: list.length, pct: Math.round((hits / list.length) * 100) }) : '',
          '', fresh));
      }
      box.append(cards);
    } else {
      box.append(h('div', { class: 'card empty slim' }, h('p', { class: 'small muted' }, 'Heute noch kein Match – hier erscheinen Kills, Bounty, Hunt Dollars und der Rest, sobald eins gespeichert ist.')));
    }
  }

  // Tier 3: values that are a state, not a sum.
  const stands = standFields().filter((f) => !f.rowMmr);
  const rowMmr = standFields().filter((f) => f.rowMmr);
  if (stands.length || rowMmr.length) {
    box.append(h('div', { class: 'tier-label' }, 'Aktueller Stand'));
    const cards = h('div', { class: 'cards' });
    for (const f of stands) {
      const today0 = today.lasts[f.id];
      const value = today0 !== null && today0 !== undefined ? today0 : s.all.lasts[f.id];
      const known = value !== null && value !== undefined;
      const avg = today.levelAvgs ? today.levelAvgs[f.id] : null;
      // An average only makes sense for a rating, not for a level.
      const sub = !known ? 'noch nicht gelesen'
        : f.type === 'sterne' && avg !== null && avg !== undefined && today.matches > 1 ? t('Ø heute {n} ★', { n: fmt.dec(avg) })
        : 'zuletzt gelesen';
      cards.append(statCard(f.label, known ? fmt.int(value) + (f.type === 'sterne' ? ' ★' : '') : '–', sub, f.type === 'sterne' ? 'gold' : '', fresh));
    }
    // All player rows in one card instead of one per row.
    if (rowMmr.length) {
      const avg = today.rowMmrAvg !== null && today.rowMmrAvg !== undefined ? today.rowMmrAvg : s.all.rowMmrAvg;
      const fromToday = today.rowMmrAvg !== null && today.rowMmrAvg !== undefined;
      cards.append(statCard('Team-MMR', avg === null || avg === undefined ? '–' : fmt.dec(avg) + ' ★',
        avg === null || avg === undefined ? 'noch nicht gelesen' : fromToday ? 'Ø aller Zeilen heute' : 'Ø aller Zeilen bisher', 'gold', fresh));
    }
    box.append(cards);
  }
  return box;
}

function statCard(label, value, sub, tone, fresh) {
  return h('div', { class: 'card stat' + (fresh ? ' fresh' : '') },
    h('div', { class: 'stat-label', title: label }, label),
    h('div', { class: 'stat-value ' + (tone || '') }, value),
    h('div', { class: 'stat-sub' }, sub || ''));
}

function lastMatchCard(last) {
  const card = h('div', { class: 'card' }, h('div', { class: 'card-head' }, h('h3', {}, 'Letztes Match'),
    last && h('span', { class: 'spacer muted small' }, fmt.rel(last.time) + ' · ' + t('{time} Uhr', { time: fmt.time(last.time) }))));
  if (!last) {
    card.append(h('div', { class: 'empty' }, icon('trophy'), h('h3', {}, 'Noch kein Match gespeichert'),
      h('p', { class: 'small' }, 'Sobald in Hunt die Mission Summary erscheint, landet das Match hier. Lass sie offen, bis alle Punkte vergeben sind – nach etwa 15–20 Sekunden kommt der Ton.')));
    return card;
  }
  const values = h('div', { class: 'kv' });
  // The end screen does not always say whether you got out: one click right after the match.
  values.append(h('div', { class: 'k' }, 'Ausgang'), h('div', { class: 'v' }, outcomeOf(last)
    ? outcomeToggle(last)
    : h('div', { class: 'row', style: { justifyContent: 'flex-end', gap: '6px' } },
      h('button', { class: 'btn small outcome-ok', onclick: () => setOutcome(last, 'extrahiert') }, 'Extracted'),
      h('button', { class: 'btn small outcome-bad', onclick: () => setOutcome(last, 'gestorben') }, 'Died'))));
  for (const f of valueFields()) {
    const info = valueInfo(last, f);
    values.append(h('div', { class: 'k' }, f.label),
      h('div', { class: 'v ' + info.state, title: info.title }, info.text));
  }
  card.append(h('div', { class: 'last-match' },
    last.screenshot
      ? h('img', { class: 'shot-thumb', src: last.screenshot, alt: 'Screenshot', onclick: () => showImage(last.screenshot, t('Match #{id}', { id: last.id })) })
      : h('div', { class: 'shot-missing' }, 'Kein Screenshot'),
    h('div', {}, values,
      h('div', { class: 'row', style: { marginTop: '14px' } },
        h('button', { class: 'btn small', onclick: () => openMatch(last.id) }, icon('pencil'), 'Werte prüfen'),
        last.manual && h('span', { class: 'chip' }, 'manuell ausgelesen')))));
  return card;
}

function last10Card(agg, numeric, hasStatus) {
  const card = h('div', { class: 'card' }, h('div', { class: 'card-head' }, h('h3', {}, 'Letzte 10 Matches')));
  if (!agg.matches) {
    card.append(h('p', { class: 'muted small' }, 'Noch keine Daten.'));
    return card;
  }
  const table = h('div', { class: 'kv3' }, h('div', {}), h('div', { class: 'h' }, 'Ø'), h('div', { class: 'h' }, 'Bestwert'));
  if (hasStatus) table.append(h('div', { class: 'k' }, 'Extraction Rate'), h('div', { class: 'v' }, fmt.pct(agg.extractionRate)), h('div', { class: 'b' }, fmt.int(agg.extracted) + ' / ' + fmt.int(agg.extracted + agg.died)));
  for (const f of numeric) {
    table.append(h('div', { class: 'k' }, f.label), h('div', { class: 'v' }, fmt.dec(agg.avgs[f.id])), h('div', { class: 'b' }, fmt.int(agg.best[f.id])));
  }
  card.append(table);
  return card;
}

// How long the running search has been going (Hunt's log), in seconds.
function searchSeconds(hunt) {
  return hunt && hunt.searchSince ? Math.max(0, (Date.now() - new Date(hunt.searchSince).getTime()) / 1000) : 0;
}

// What the overlay previews draw: the values plus where Hunt is (for the live search time).
function overlayData() {
  return Object.assign({}, state.stats, { hunt: (state.status && state.status.hunt) || null });
}

const DAY_PARTS = { 0: 'Nachts', 6: 'Morgens', 12: 'Nachmittags', 18: 'Abends' };

// How long matchmaking takes: today, overall and by time of day – from Hunt's own log.
function searchCard() {
  const s = state.stats && state.stats.search;
  if (!s || !s.all || !s.all.count) return null;
  const card = h('div', { class: 'card' }, h('div', { class: 'card-head' }, h('h3', {}, 'Matchsuche'),
    h('span', { class: 'muted small' }, t(s.all.count === 1 ? '1 Suche erfasst' : '{n} Suchen erfasst', { n: fmt.int(s.all.count) }))));
  const stat = (value, label, title) => h('div', { class: 'map-stat', title }, h('b', {}, value), h('span', {}, label));
  // The whole wait first: from the click on "Ready" until the map loads.
  card.append(h('div', { class: 'search-stats' },
    stat(fmt.dur(s.all.avgTotal), 'Ø Klick bis Match', tr('Vom Klick auf „Bereit“ bis die Karte lädt')),
    stat(fmt.dur(s.all.avgSeconds), 'Ø Suche', tr('Bis Hunt eine Lobby gefunden hat')),
    stat(fmt.dur(s.all.avgLobby), 'Ø Lobby', tr('Von der gefundenen Lobby bis die Karte lädt – warten, bis sie voll ist')),
    stat(fmt.dur(s.all.longestTotal !== null && s.all.longestTotal !== undefined ? s.all.longestTotal : s.all.longest), 'Längste'),
    stat(fmt.int(s.all.dissolved), 'Lobbys aufgelöst', tr('Lobbys, die sich vor dem Match wieder aufgelöst haben'))));
  // Today and the last 10 next to each other.
  const table = h('div', { class: 'search-table' }, h('div'), h('div', { class: 'h' }, 'Suche'), h('div', { class: 'h' }, 'Lobby'),
    h('div', { class: 'h' }, 'Gesamt'), h('div', { class: 'h' }, 'Suchen'));
  for (const [key, label] of [['today', 'Heute'], ['last10', 'Letzte 10'], ['all', 'Insgesamt']]) {
    const a = s[key];
    table.append(h('div', { class: 'k' }, label), h('div', {}, fmt.dur(a.avgSeconds)), h('div', {}, fmt.dur(a.avgLobby)),
      h('div', { class: 'v' }, fmt.dur(a.avgTotal)), h('div', { class: 'n' }, fmt.int(a.count)));
  }
  card.append(table);
  // By time of day (the whole wait): the bar compares with the slowest part of the day.
  const value = (p) => (p.avgTotal !== null && p.avgTotal !== undefined ? p.avgTotal : p.avgSeconds);
  const parts = (s.byTime || []).filter((p) => p.count);
  if (parts.length > 1) {
    const slowest = Math.max(...parts.map(value));
    const rows = h('div', { class: 'search-parts' });
    for (const p of s.byTime) {
      rows.append(h('div', { class: 'search-part' + (p.count ? '' : ' none') },
        h('span', { class: 'search-part-name', title: t('{from}–{to} Uhr', { from: p.from, to: p.from + 6 }) }, DAY_PARTS[p.from]),
        h('div', { class: 'map-bar' }, h('span', { style: { width: p.count ? Math.max(3, (value(p) / slowest) * 100) + '%' : '0' } })),
        h('span', { class: 'search-part-value' }, p.count ? fmt.dur(value(p)) : '–'),
        h('span', { class: 'search-part-count' }, p.count ? t(p.count === 1 ? '1 Suche' : '{n} Suchen', { n: p.count }) : '')));
    }
    card.append(h('div', { class: 'tier-label', style: { marginTop: '16px' } }, 'Ø Klick bis Match nach Tageszeit'), rows);
  }
  return card;
}

const MAP_PERIODS = [['today', 'Heute'], ['30', '30 Tage'], ['all', 'Gesamt']];

// Every map played in the chosen period: how often, how it went, and the picture of it from the
// last match there. The bar compares with the map played most.
function mapsCard() {
  const period = MAP_PERIODS.some(([key]) => key === state.mapPeriod) ? state.mapPeriod : 'all';
  const since = period === 'today' ? new Date(state.stats.dayStart).getTime() : period === '30' ? Date.now() - 30 * 86400000 : 0;
  const list = state.matches.filter((m) => new Date(m.time).getTime() >= since);
  const segmented = h('div', { class: 'segmented' }, MAP_PERIODS.map(([key, label]) =>
    h('button', { class: period === key ? 'active' : '', onclick: () => { state.mapPeriod = key; store.set('mapPeriod', key); rerender(); } }, label)));
  const sub = h('span', { class: 'muted small' });
  const card = h('div', { class: 'card' }, h('div', { class: 'card-head' }, h('h3', {}, 'Karten'), sub, h('span', { class: 'spacer' }), segmented));
  const named = list.filter((m) => m.mapName);
  if (!named.length) {
    card.append(h('p', { class: 'muted small' }, list.length ? 'Zu diesen Matches ist keine Karte bekannt.' : 'Keine Matches in diesem Zeitraum'));
    return card;
  }
  const byMap = new Map();
  for (const m of named) {
    if (!byMap.has(m.mapName)) byMap.set(m.mapName, []);
    byMap.get(m.mapName).push(m);
  }
  sub.textContent = t(named.length === 1 ? '{n} Match' : '{n} Matches', { n: named.length }) + ' · ' + t(byMap.size === 1 ? '{n} Karte' : '{n} Karten', { n: byMap.size });
  // Averages per map of the first number among the kills and among the rewards.
  const { groups } = matchGroups(valueFields());
  const averaged = [groups[0], groups[1]].map((g) => g.fields.find((f) => f.type === 'number')).filter(Boolean);
  const maps = [...byMap.entries()].sort((a, b) => b[1].length - a[1].length || a[0].localeCompare(b[0]));
  const most = maps[0][1].length;
  const grid = h('div', { class: 'map-grid' });
  for (const [name, matches] of maps) {
    const extracted = matches.filter((m) => outcomeOf(m) === 'extrahiert').length;
    const died = matches.filter((m) => outcomeOf(m) === 'gestorben').length;
    const latest = matches[matches.length - 1];
    const shot = matches.slice().reverse().find((m) => m.mapShot);
    // Small figures under the bar: value on top, what it is below.
    const stat = (value, label, cls, title) => h('div', { class: 'map-stat' + (cls ? ' ' + cls : ''), title }, h('b', {}, value), h('span', {}, label));
    const stats = [];
    if (extracted + died) stats.push(stat(fmt.pct(extracted / (extracted + died)), 'Extraction', extracted >= died ? 'ok' : 'bad', t('{n}× Extracted · {m}× Died', { n: extracted, m: died })));
    for (const f of averaged) {
      const values = matches.map((m) => m.values[f.id]).filter((v) => typeof v === 'number');
      if (values.length) stats.push(stat(fmt.dec(values.reduce((a, b) => a + b, 0) / values.length), ['Ø ', f.label], '', t('Durchschnitt {label}', { label: tr(f.label) })));
    }
    stats.push(stat(fmt.rel(latest.time), 'Zuletzt', '', t('{date}, {time} Uhr', { date: fmt.date(latest.time), time: fmt.time(latest.time) })));
    // Map names stay as the game writes them.
    grid.append(h('div', { class: 'map-tile' },
      shot
        ? h('div', { class: 'map-thumb', style: { backgroundImage: 'url("' + shot.mapShot + '")' } })
        : h('div', { class: 'map-thumb empty' }, document.createTextNode(name.split(/\s+/).map((w) => w.charAt(0)).join('').slice(0, 2).toUpperCase())),
      h('div', { class: 'map-info' },
        h('div', { class: 'map-name' }, document.createTextNode(name)),
        h('div', { class: 'map-share' },
          h('div', { class: 'map-bar' }, h('span', { style: { width: Math.max(3, (matches.length / most) * 100) + '%' } })),
          h('span', { class: 'map-count' }, t(matches.length === 1 ? '{n} Match' : '{n} Matches', { n: matches.length }), ' · ', fmt.pct(matches.length / named.length))),
        h('div', { class: 'map-stats' }, stats))));
  }
  card.append(grid);
  const unnamed = list.length - named.length;
  if (unnamed) card.append(h('p', { class: 'small faint', style: { marginTop: '10px' } }, t(unnamed === 1 ? '1 Match ohne Karte' : '{n} Matches ohne Karte', { n: unnamed })));
  return card;
}

function chartCard(numericOnly) {
  const card = h('div', { class: 'card' });
  // Levels and MMR are worth a look over time as well.
  const numeric = numericOnly.concat(standFields());
  if (!numeric.length) return card;
  let fieldId = state.chartField && numeric.some((f) => f.id === state.chartField) ? state.chartField : numeric[0].id;
  const select = h('select', {
    class: 'input',
    onchange: (e) => { state.chartField = e.target.value; store.set('chartField', state.chartField); rerender(); },
  }, numeric.map((f) => h('option', { value: f.id, selected: f.id === fieldId }, f.label)));
  card.append(h('div', { class: 'card-head' }, h('h3', {}, 'Verlauf'), h('span', { class: 'muted small' }, 'letzte 20 Matches'),
    h('span', { class: 'spacer' }), select));

  const recent = state.matches.slice(-20);
  if (!recent.length) {
    card.append(h('p', { class: 'muted small' }, 'Sobald Matches gespeichert sind, siehst du hier den Verlauf.'));
    return card;
  }
  const values = recent.map((m) => (typeof m.values[fieldId] === 'number' ? m.values[fieldId] : null));
  const max = Math.max(1, ...values.filter((v) => v !== null));
  const bars = h('div', { style: { display: 'flex', alignItems: 'flex-end', gap: '6px', height: '170px', padding: '6px 2px 0' } });
  recent.forEach((m, i) => {
    const v = values[i];
    const st = outcomeOf(m);
    const color = st === 'extrahiert' ? 'var(--ok)' : st === 'gestorben' ? 'var(--bad)' : 'var(--faint)';
    const height = v === null ? 3 : Math.max(3, (v / max) * 150);
    bars.append(h('div', {
      style: { flex: '1', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '4px', cursor: 'pointer', minWidth: '0' },
      title: fmt.date(m.time) + ' ' + fmt.time(m.time) + ' · ' + fmt.int(v),
      onclick: () => openMatch(m.id),
    },
    h('div', { class: 'small num', style: { color: 'var(--muted)', fontSize: '11px' } }, v === null ? '' : fmt.int(v)),
    h('div', { style: { width: '100%', maxWidth: '38px', height: height + 'px', background: color, borderRadius: '4px 4px 1px 1px', opacity: v === null ? 0.35 : 0.9 } })));
  });
  card.append(bars);
  card.append(h('div', { class: 'legend', style: { marginTop: '12px' } },
    h('span', { vars: { '--c': 'var(--ok)' } }, 'Extracted'),
    h('span', { vars: { '--c': 'var(--bad)' } }, 'Died'),
    h('span', { vars: { '--c': 'var(--faint)' } }, 'Unbekannt')));
  return card;
}

function allTimeCard(agg, numeric, hasStatus) {
  const parts = [t(agg.matches === 1 ? '{n} Match' : '{n} Matches', { n: fmt.int(agg.matches) })];
  if (hasStatus && agg.extractionRate !== null) parts.push('Extraction Rate ' + fmt.pct(agg.extractionRate));
  for (const f of numeric) parts.push(tr(f.label) + ' ' + fmt.int(agg.sums[f.id]));
  for (const f of standFields().filter((x) => !x.rowMmr)) parts.push(tr(f.label) + ' ' + fmt.int(agg.lasts[f.id]) + (f.type === 'sterne' ? ' ★' : ''));
  if (agg.rowMmrAvg !== null && agg.rowMmrAvg !== undefined) parts.push(t('Team-MMR Ø {n} ★', { n: fmt.dec(agg.rowMmrAvg) }));
  if (nameFields().length && state.matches.length) {
    const hits = state.matches.filter(hasCyrillicMate).length;
    parts.push(t('Kyrillische Mitspieler {hits} von {total}', { hits, total: state.matches.length }));
  }
  return h('div', { class: 'card', style: { padding: '13px 20px' } },
    h('div', { class: 'row' }, h('span', { class: 'label', style: { textTransform: 'uppercase', letterSpacing: '.1em' } }, 'Gesamt'),
      h('span', { class: 'muted' }, parts.join('  ·  '))));
}

/* ================================================================= matches */

const MATCH_FILTERS = [['today', 'Heute'], ['7', '7 Tage'], ['30', '30 Tage'], ['all', 'Alle']];

function filteredMatches() {
  const list = state.matches.slice().reverse();
  if (state.matchFilter === 'all') return list;
  if (state.matchFilter === 'today') {
    const start = new Date(state.stats.dayStart).getTime();
    return list.filter((m) => new Date(m.time).getTime() >= start);
  }
  const since = Date.now() - Number(state.matchFilter) * 86400000;
  return list.filter((m) => new Date(m.time).getTime() >= since);
}

// The game day a match belongs to: after midnight it still counts to the evening before.
function gameDay(iso) {
  const d = new Date(iso);
  d.setHours(d.getHours() - ((state.settings && state.settings.dayStartHour) || 0));
  return d;
}

function dayLabel(iso) {
  const d = gameDay(iso);
  const today = gameDay(new Date().toISOString());
  const days = Math.round((new Date(today.getFullYear(), today.getMonth(), today.getDate()) - new Date(d.getFullYear(), d.getMonth(), d.getDate())) / 86400000);
  if (days === 0) return 'Heute';
  if (days === 1) return 'Gestern';
  return d.toLocaleDateString(localeTag(), { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' });
}

function matchSummary(list) {
  const extracted = list.filter((m) => outcomeOf(m) === 'extrahiert').length;
  const died = list.filter((m) => outcomeOf(m) === 'gestorben').length;
  const parts = [t(list.length === 1 ? '{n} Match' : '{n} Matches', { n: list.length })];
  if (extracted) parts.push(extracted + '× Extracted');
  if (died) parts.push(died + '× Died');
  if (extracted + died > 1) parts.push('Rate ' + Math.round((extracted / (extracted + died)) * 100) + ' %');
  return parts.join(' · ');
}

/* ---------- teammates with names in Cyrillic letters ---------- */

const CYRILLIC = /[Ѐ-ӿ]/;
const nameFields = () => (state.stats ? state.stats.fields.filter((f) => f.type === 'text') : []);

// Everyone in the team details except yourself – your own row carries your own name.
function teammateNames(match) {
  const own = (state.settings && state.settings.playerName ? state.settings.playerName : '').trim().toLowerCase();
  return nameFields().map((f) => String(match.values[f.id] || '').trim())
    .filter((name) => name.length > 0 && name.toLowerCase() !== own);
}

const hasCyrillicMate = (match) => teammateNames(match).some((name) => CYRILLIC.test(name));

// What to show for a value: a dead hunter has no Hunter Level or Progress – no reading error.
function valueInfo(match, field, outcome) {
  const v = match.values[field.id];
  if (v !== null && v !== undefined && v !== '') {
    const text = field.type === 'sterne' ? fmt.int(v) + ' ★' : isNumberType(field.type) ? fmt.int(v) : String(v);
    return { text, state: '', title: '' };
  }
  if (field.survivorOnly && (outcome === undefined ? outcomeOf(match) : outcome) === 'gestorben')
    return { text: 'Dead', state: 'dead', title: 'Steht nicht in der Mission Summary – der Hunter ist gestorben' };
  return { text: '?', state: 'missing', title: 'Nicht erkannt – zum Korrigieren anklicken' };
}

function matchRow(m, fields) {
  const outcome = outcomeOf(m);
  const row = h('div', {
    class: 'match-row' + (outcome === 'extrahiert' ? ' extracted' : outcome === 'gestorben' ? ' died' : ''),
    onclick: () => openMatch(m.id),
  });
  row.append(h('div', { class: 'match-head' },
    h('span', { class: 'num' }, '#' + m.id),
    h('span', { class: 'time' }, t('{time} Uhr', { time: fmt.time(m.time) })),
    outcomeToggle(m),
    h('span', { class: 'grow' }),
    m.trail && m.trail.length > 0 && h('span', { class: 'chip', title: t(m.trail.length === 1 ? 'Laufspur mit 1 Punkt' : 'Laufspur mit {n} Punkten', { n: m.trail.length }) },
      document.createTextNode(m.mapName || tr('Laufspur'))),
    hasCyrillicMate(m) && h('span', { class: 'chip', title: 'Mindestens ein Mitspieler hat einen kyrillisch geschriebenen Namen' }, 'kyrillisch'),
    m.manual && h('span', { class: 'chip' }, 'manuell'),
    m.edited && h('span', { class: 'chip' }, 'bearbeitet'),
    m.note && h('span', { class: 'chip', title: m.note }, 'Notiz'),
    m.screenshot && h('button', {
      class: 'icon-btn',
      title: 'Screenshot ansehen',
      onclick: (e) => { e.stopPropagation(); showImage(m.screenshot, t('Match #{id}', { id: m.id })); },
    }, icon('image')),
    h('button', { class: 'icon-btn', title: 'Werte prüfen' }, icon('pencil'))));
  // The Mission Summary in its groups on the left, the Team Details on the right.
  const { groups, team } = matchGroups(fields);
  const stats = h('div', { class: 'match-stats' });
  for (const g of groups) {
    if (!g.fields.length) continue;
    stats.append(h('div', { class: 'ms-group' },
      h('div', { class: 'ms-title' }, g.title),
      g.fields.map((f) => {
        const info = valueInfo(m, f, outcome);
        return h('div', { class: 'ms-row' + (info.state ? ' ' + info.state : ''), title: info.title || '' },
          h('span', { class: 'k', title: f.label }, f.label), h('span', { class: 'v' }, info.text));
      })));
  }
  const body = h('div', { class: 'match-body' }, stats);
  const teamBox = teamPanel(m, team);
  if (teamBox) body.append(teamBox);
  row.append(body);
  return row;
}

// MMR as Hunt shows it: six stars, as many filled as the value.
function starMeter(value) {
  const n = Math.max(0, Math.min(6, Math.round(Number(value) || 0)));
  return h('span', { class: 'tm-stars', title: n === 1 ? t('1 Stern') : t('{n} Sterne', { n }) },
    [0, 1, 2, 3, 4, 5].map((i) => h('span', { class: i < n ? 'on' : '' }, '★')));
}

// The Team Details of a match: Match MMR and your own on top, then every player in one row
// with his stars – your own row marked. Nothing when the Team Details were never read.
function teamPanel(m, team) {
  const has = (f) => !!f && m.values[f.id] !== null && m.values[f.id] !== undefined && m.values[f.id] !== '';
  if (!team.general.concat(team.names, team.mmrs).some(has)) return null;
  const own = (state.settings.playerName || '').trim().toLowerCase();
  const box = h('div', { class: 'tm-box' },
    h('div', { class: 'tm-head' }, h('span', { class: 'ms-title' }, 'Team'),
      team.general.filter(has).map((f) => h('span', { class: 'tm-chip', title: f.label },
        h('span', {}, f.label), h('b', {}, f.type === 'sterne' ? fmt.int(m.values[f.id]) + ' ★' : isNumberType(f.type) ? fmt.int(m.values[f.id]) : String(m.values[f.id]))))));
  team.names.forEach((f, i) => {
    const mmr = team.mmrs[i];
    if (!has(f) && !has(mmr)) return;
    const name = has(f) ? String(m.values[f.id]).trim() : '';
    const isOwn = !!own && name.toLowerCase() === own;
    box.append(h('div', { class: 'tm-player' + (isOwn ? ' own' : '') },
      h('span', { class: 'tm-name' + (name ? '' : ' missing'), title: name || tr('Nicht erkannt – zum Korrigieren anklicken') }, name || '?'),
      isOwn && h('span', { class: 'chip own' }, 'du'),
      has(mmr) ? starMeter(m.values[mmr.id]) : h('span', { class: 'tm-stars-missing', title: tr('Nicht erkannt – zum Korrigieren anklicken') }, '?')));
  });
  // MMR rows without a name field of their own.
  for (const f of team.mmrs.slice(team.names.length).filter(has)) {
    box.append(h('div', { class: 'tm-player' }, h('span', { class: 'tm-name', title: f.label }, f.label), starMeter(m.values[f.id])));
  }
  return box;
}

function renderMatches() {
  const fields = valueFields();
  const list = filteredMatches();
  const root = h('div', { class: 'stack' });
  const segmented = h('div', { class: 'segmented' }, MATCH_FILTERS.map(([key, label]) =>
    h('button', {
      class: state.matchFilter === key ? 'active' : '',
      onclick: () => { state.matchFilter = key; store.set('matchFilter', key); rerender(); },
    }, label)));
  root.append(h('div', { class: 'toolbar' }, segmented,
    h('span', { class: 'muted small' }, matchSummary(list)),
    h('span', { class: 'grow' }),
    h('button', { class: 'btn', onclick: exportCsv }, icon('download'), 'CSV exportieren')));

  if (!list.length) {
    root.append(h('div', { class: 'card empty' }, icon('trophy'), h('h3', {}, 'Keine Matches in diesem Zeitraum'),
      h('p', { class: 'small' }, state.matches.length ? 'Wähle oben einen längeren Zeitraum.' : 'Sobald die Mission Summary erkannt wird, erscheinen die Matches hier.')));
    return root;
  }

  // One block per game day, newest first.
  const days = [];
  for (const m of list) {
    const key = gameDay(m.time).toDateString();
    const day = days.length && days[days.length - 1].key === key ? days[days.length - 1] : null;
    if (day) day.matches.push(m);
    else days.push({ key, time: m.time, matches: [m] });
  }
  for (const day of days) {
    root.append(h('div', {},
      h('div', { class: 'day-head' }, h('b', {}, dayLabel(day.time)), h('span', {}, matchSummary(day.matches))),
      h('div', { class: 'match-list' }, day.matches.map((m) => matchRow(m, fields)))));
  }
  root.append(h('p', { class: 'small faint' }, 'Ein Klick auf eine Zeile öffnet die Werte zum Prüfen, ein Klick auf den Ausgang schaltet zwischen Extracted, Died und Unbekannt um.'));
  return root;
}

async function exportCsv() {
  try {
    const path = await api.call('exportCsv');
    if (path) toast('ok', t('Exportiert: {path}', { path }));
  } catch (e) {
    toast('error', e.message);
  }
}

// The values of a match in groups: what the labels say about a field decides where it goes.
// Fields of the Team Details page form their own group, each player with his MMR in one row.
function matchGroups(fields) {
  const teamIds = new Set(((state.calibration.team && state.calibration.team.fields) || []).map((f) => f.id));
  const groups = [
    { title: 'Kills', test: /kill/i, fields: [] },
    { title: 'Fortschritt', test: /level|progress|fortschritt|xp|erfahrung|bloodline|blutlinie/i, fields: [] },
    { title: 'Belohnung', test: /bounty|dollar|token|event|punkte|point|geld|kopfgeld|reward|belohnung/i, fields: [] },
    { title: 'Weitere Werte', test: null, fields: [] },
  ];
  const team = { general: [], names: [], mmrs: [] };
  for (const f of fields) {
    if (teamIds.has(f.id)) {
      if (f.type === 'text') team.names.push(f);
      else if (f.rowMmr) team.mmrs.push(f);
      else team.general.push(f);
      continue;
    }
    (groups.find((g) => g.test && g.test.test(f.label)) || groups[groups.length - 1]).fields.push(f);
  }
  // The level before the progress bars; otherwise the order of the setup.
  groups[1].fields.sort((a, b) => (b.type === 'stand') - (a.type === 'stand'));
  // Belohnung before Fortschritt, the way the Mission Summary reads.
  return { groups: [groups[0], groups[2], groups[1], groups[3]], team };
}

// What the program itself wrote as a reading ("5 Sterne", "Symbol – Level 50") in the current
// language – also for matches read while the other language was set.
function readingText(raw) {
  const stars = /^(\d+) (?:Sterne?|stars?)$/.exec(raw);
  if (stars) return stars[1] === '1' ? t('1 Stern') : t('{n} Sterne', { n: stars[1] });
  if (raw === 'keine Sterne' || raw === 'no stars') return t('keine Sterne');
  if (raw === 'Symbol – Level 50' || raw === 'symbol – level 50') return t('Symbol – Level 50');
  return raw;
}

function openMatch(id) {
  const match = state.matches.find((m) => m.id === id);
  if (!match) return;
  const fields = valueFields();
  const inputs = {};
  let save = null;
  let outcome = outcomeOf(match);
  const own = (state.settings.playerName || '').trim().toLowerCase();

  // One value: label, input (Enter saves), what was read, marked when changed or not recognised.
  const valueField = (f) => {
    const v = match.values[f.id];
    const original = v === null || v === undefined ? '' : String(v);
    const info = valueInfo(match, f, outcome);
    const input = h('input', {
      class: 'input',
      type: isNumberType(f.type) ? 'number' : 'text',
      min: '0',
      value: original,
      placeholder: info.state === 'dead' ? 'Dead' : '',
    });
    inputs[f.id] = input;
    const raw = match.raw && match.raw[f.id];
    const isOwn = f.type === 'text' && own && original.trim().toLowerCase() === own;
    const field = h('div', { class: 'field value-field' + (info.state === 'missing' ? ' missing' : '') },
      h('label', {}, f.label, isOwn && h('span', { class: 'chip own' }, 'du')),
      f.type === 'sterne' ? h('div', { class: 'input-suffix' }, input, h('span', {}, '★')) : input,
      raw !== undefined && h('span', { class: 'raw', title: readingText(raw || '') }, t('gelesen: „{raw}“', { raw: readingText(raw || '') })));
    const markChanged = () => field.classList.toggle('changed', input.value !== original);
    input.addEventListener('input', markChanged);
    input.addEventListener('change', markChanged);
    input.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        save();
      }
    });
    return field;
  };

  // Pictures on top: the Mission Summary and, when they were read, the Team Details.
  const shots = [];
  if (match.screenshot) shots.push({ label: 'Mission Summary', url: match.screenshot });
  if (match.teamScreenshot) shots.push({ label: 'Team Details', url: match.teamScreenshot });
  if (match.mapShot && match.trail && match.trail.length) shots.push({ label: 'Laufspur', url: match.mapShot, trail: match.trail, outcome });
  const viewer = shots.length ? shotViewer(shots) : null;

  const outcomeControl = h('div', { class: 'segmented outcome-seg' },
    [['extrahiert', 'Extracted'], ['gestorben', 'Died'], ['', 'Unbekannt']].map(([value, label]) => h('button', {
      class: (value === outcome ? 'active ' : '') + (value === 'extrahiert' ? 'ok' : value === 'gestorben' ? 'bad' : ''),
      onclick: (e) => {
        outcome = value;
        e.currentTarget.parentNode.querySelectorAll('button').forEach((b) => b.classList.toggle('active', b === e.currentTarget));
      },
    }, label)));
  const note = h('input', { class: 'input', placeholder: 'z. B. Trio mit …, Clutch am Boss', maxlength: '500', value: match.note || '' });
  note.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      save();
    }
  });

  const { groups, team } = matchGroups(fields);
  const view = h('div', { class: 'match-view' },
    viewer ? viewer.el : h('div', { class: 'shot-missing slim' }, 'Kein Screenshot gespeichert'),
    h('div', { class: 'match-meta' },
      h('div', { class: 'field' }, h('label', {}, 'Ausgang'), outcomeControl),
      h('div', { class: 'field grow' }, h('label', {}, 'Notiz'), note)),
    groups.filter((g) => g.fields.length).map((g) => h('section', { class: 'value-group' },
      h('div', { class: 'section-label' }, g.title),
      h('div', { class: 'value-grid' }, g.fields.map(valueField)))),
    (team.general.length || team.names.length || team.mmrs.length) && h('section', { class: 'value-group' },
      h('div', { class: 'section-label' }, 'Team'),
      team.general.length && h('div', { class: 'value-grid' }, team.general.map(valueField)),
      team.names.length && h('div', { class: 'team-rows' }, team.names.map((f, i) => [valueField(f), team.mmrs[i] ? valueField(team.mmrs[i]) : h('div')])),
      team.mmrs.length > team.names.length && h('div', { class: 'value-grid' }, team.mmrs.slice(team.names.length).map(valueField))));

  save = async () => {
    const values = {};
    for (const f of fields) values[f.id] = inputs[f.id].value;
    const args = { id: match.id, values, note: note.value };
    const sf = statusField();
    if (sf) values[sf.id] = outcome;
    else args.outcome = outcome;
    try {
      await api.call('updateMatch', args);
      toast('ok', t('Match #{id} gespeichert', { id: match.id }));
      modal.close();
    } catch (e) {
      toast('error', e.message);
    }
  };
  const remove = async () => {
    modal.close();
    if (!(await confirmDialog('Match löschen?',
      t('Match #{id} vom {date} wird endgültig gelöscht, samt Screenshots.', { id: match.id, date: fmt.date(match.time) }), 'Löschen', true))) return;
    try {
      await api.call('deleteMatch', { id: match.id });
      toast('ok', t('Match #{id} gelöscht', { id: match.id }));
    } catch (e) {
      toast('error', e.message);
    }
  };

  const box = h('div', { class: 'modal' },
    h('div', { class: 'modal-head' },
      h('h2', {}, t('Match #{id}', { id: match.id })),
      h('div', { class: 'chips' },
        match.mapName && h('span', { class: 'chip' }, document.createTextNode(match.mapName)),
        match.manual && h('span', { class: 'chip' }, 'manuell ausgelesen'),
        match.edited && h('span', { class: 'chip' }, 'bearbeitet')),
      h('span', { class: 'muted small' }, t('{date}, {time} Uhr', { date: fmt.date(match.time), time: fmt.time(match.time) })),
      h('button', { class: 'icon-btn', title: 'Schließen', onclick: () => modal.close() }, icon('x'))),
    h('div', { class: 'modal-body' }, view),
    h('div', { class: 'modal-foot' },
      h('button', { class: 'btn danger', style: { marginRight: 'auto' }, onclick: remove }, icon('trash'), 'Löschen'),
      h('span', { class: 'small faint' }, shots.length > 1 ? '← → wechselt das Bild · Enter speichert' : 'Enter speichert'),
      h('button', { class: 'btn ghost', onclick: () => modal.close() }, 'Abbrechen'),
      h('button', { class: 'btn primary', onclick: save }, 'Speichern')));
  const onKey = viewer ? viewer.onKey : null;
  if (onKey) window.addEventListener('keydown', onKey);
  modal.open(box, () => { if (onKey) window.removeEventListener('keydown', onKey); });
}

/* ================================================================= setup */

const PALETTE = ['#e2b25c', '#6fc48d', '#6fa8e0', '#e2705f', '#b48be0', '#e0cf5f', '#5fd0c4', '#e07fb0', '#9fd35f', '#d9975f', '#8fa0e8'];
const FIELD_TYPES = [['number', 'Zahl'], ['stand', 'Stand'], ['sterne', 'Sterne'], ['status', 'Status'], ['text', 'Text']];

const copyField = (f) => ({ id: f.id, label: f.label, type: f.type, survivorOnly: !!f.survivorOnly, derived: !!f.derived, area: f.area ? { ...f.area } : null });
const fieldPayload = (f) => ({ id: f.id, label: f.label.trim() || tr('Feld'), type: f.type, survivorOnly: !!f.survivorOnly, derived: !!f.derived, area: f.area });

const setup = {
  draft: null,
  dirty: false,
  selected: 'anchor',
  teamSelected: 'anchor',
  results: {},
  teamResults: {},
  sources: null,
  editor: null,
  teamEditor: null,
  mapSelected: 'mapArea',
  mapEditor: null,
  mapResult: null,
  refs: {},
  countdown: null,

  resetDraft() {
    const c = state.calibration;
    this.draft = {
      anchor: c.anchor ? { ...c.anchor } : null,
      threshold: c.threshold,
      fields: c.fields.map(copyField),
      team: {
        anchor: c.team && c.team.anchor ? { ...c.team.anchor } : null,
        fields: (c.team && c.team.fields ? c.team.fields : []).map(copyField),
      },
      map: {
        anchor: c.map && c.map.anchor ? { ...c.map.anchor } : null,
        mapArea: c.map && c.map.mapArea ? { ...c.map.mapArea } : null,
        nameArea: c.map && c.map.nameArea ? { ...c.map.nameArea } : null,
      },
      extractedWords: c.extractedWords.slice(),
      diedWords: c.diedWords.slice(),
    };
    this.dirty = false;
    this.results = {};
    this.teamResults = {};
    this.mapResult = null;
  },

  // Which box is being drawn, the editor and the list – per page ("summary", "team", "map").
  selectedOf(page) {
    return page === 'team' ? this.teamSelected : page === 'map' ? this.mapSelected : this.selected;
  },

  select(page, id) {
    if (page === 'team') this.teamSelected = id;
    else if (page === 'map') this.mapSelected = id;
    else this.selected = id;
  },

  editorOf(page) {
    return page === 'team' ? this.teamEditor : page === 'map' ? this.mapEditor : this.editor;
  },

  enter() {
    api.call('setPreview', { enabled: true, calibrating: true }).catch(() => {});
  },

  leave() {
    api.call('setPreview', { enabled: false, calibrating: false }).catch(() => {});
    if (this.countdown) clearInterval(this.countdown.timer);
    this.countdown = null;
  },

  // Everything that can be drawn on a page: its anchor and the fields that are read from it.
  // The map has three fixed boxes instead of fields.
  items(page) {
    if (page === 'map') {
      const m = this.draft.map;
      return [
        { id: 'anchor', label: 'Wiedererkennung', color: PALETTE[0], area: m.anchor },
        { id: 'mapArea', label: 'Kartenausschnitt', color: PALETTE[2], area: m.mapArea },
        { id: 'nameArea', label: 'Kartenname', color: PALETTE[4], area: m.nameArea },
      ];
    }
    const d = page === 'team' ? this.draft.team : this.draft;
    return [{ id: 'anchor', label: 'Wiedererkennung', color: PALETTE[0], area: d.anchor }]
      .concat(d.fields.filter((f) => !f.derived).map((f, i) => ({ id: f.id, label: f.label, color: PALETTE[1 + (i % (PALETTE.length - 1))], area: f.area })));
  },

  setArea(id, area, page) {
    if (page === 'map') {
      this.draft.map[id] = area;
      return;
    }
    const d = page === 'team' ? this.draft.team : this.draft;
    if (id === 'anchor') d.anchor = area;
    else {
      const f = d.fields.find((x) => x.id === id);
      if (f) f.area = area;
    }
  },

  markDirty() {
    this.dirty = true;
    if (this.refs.dirty) this.refs.dirty.classList.remove('hidden');
  },

  render() {
    if (!this.draft) this.resetDraft();
    this.refs = {};
    const root = h('div', { class: 'stack' });
    if (state.calibration.ready) {
      root.append(h('div', { class: 'banner pause-banner' }, icon('info'),
        h('div', { class: 'grow' }, h('h3', {}, 'Pausiert, solange die Einrichtung offen ist'),
          h('p', {}, 'Auf dieser Seite speichert HuntXtract keine Matches – auch wenn die Mission Summary erkannt wird. Zum Zählen einfach zur Übersicht wechseln.')),
        h('button', { class: 'btn', onclick: () => this.finish() }, 'Fertig – zur Übersicht')));
    }
    const preset = state.app.preset || {};
    if (state.calibration.preset) {
      root.append(h('div', { class: 'banner preset-banner' }, icon('check'),
        h('div', { class: 'grow' }, h('h3', {}, t('Fertig eingerichtet für {res}', { res: presetResolution() })),
          h('p', {}, 'Die Rahmen für Mission Summary, Team Details und Karte kommen aus der mitgelieferten Einrichtung für Hunt in 16:9 (Full HD, 2K, 4K) – zeichnen musst du nichts. Sitzt bei dir nach dem ersten Match ein Rahmen nicht, verschieb ihn und speichere. Dann ist es deine eigene Einrichtung.'))));
    } else if (preset.available && preset.fits && !state.calibration.ready) {
      root.append(h('div', { class: 'banner preset-banner' }, icon('target'),
        h('div', { class: 'grow' }, h('h3', {}, t('Fertige Einrichtung für {res}', { res: presetResolution() })),
          h('p', {}, t('Hunt läuft bei dir in {res}. Statt selbst Rahmen zu zeichnen, kannst du die mitgelieferte Einrichtung laden – mit Mission Summary, Team Details und Karte.', { res: presetResolution() }))),
        h('button', { class: 'btn primary', onclick: () => this.loadPreset() }, 'Vorgabe laden')));
    }
    root.append(this.stepObs(), this.stepCapture(), this.stepFields(), this.stepTeam(), this.stepMap(), this.stepTest());
    this.showPreviewState();
    return root;
  },

  /* ---------- step 1: OBS */
  stepObs() {
    const s = state.settings;
    const st = state.status;
    const done = st && st.obs === 'Ready';
    const host = h('input', { class: 'input', value: s.obsHost });
    const port = h('input', { class: 'input', type: 'number', min: '1', max: '65535', value: String(s.obsPort) });
    const password = h('input', { class: 'input', type: 'password', placeholder: s.hasPassword ? '•••••••• (gespeichert)' : 'Passwort aus OBS', autocomplete: 'off' });
    const result = h('div', { class: 'small muted' });
    const sourceSelect = h('select', { class: 'input', style: { minWidth: '260px' } });
    const fillSources = () => {
      sourceSelect.replaceChildren();
      const list = this.sources || [];
      const current = state.settings.sourceName;
      if (current && !list.some((x) => x.name === current)) sourceSelect.append(h('option', { value: current }, t('{name} (aktuell)', { name: current })));
      if (!list.length && !current) sourceSelect.append(h('option', { value: '' }, 'Erst „Verbinden“ klicken'));
      for (const src of list) sourceSelect.append(h('option', { value: src.name, selected: src.name === current }, src.name + ' · ' + src.kindLabel));
      if (!current) {
        const games = list.filter((x) => x.kind === 'game_capture');
        if (games.length === 1) sourceSelect.value = games[0].name;
      }
    };
    fillSources();

    const connect = async () => {
      result.className = 'small muted';
      result.textContent = t('Verbinde …');
      try {
        const r = await api.call('obsTest', { host: host.value.trim(), port: Number(port.value), password: password.value || null });
        this.sources = r.sources;
        fillSources();
        result.className = 'small';
        result.style.color = 'var(--ok)';
        result.textContent = t(r.sources.length === 1
          ? 'Verbunden mit OBS {version} – {n} Videoquelle gefunden. Wähle unten deine Spielaufnahme.'
          : 'Verbunden mit OBS {version} – {n} Videoquellen gefunden. Wähle unten deine Spielaufnahme.', { version: r.obsVersion, n: r.sources.length });
      } catch (e) {
        result.className = 'small error-text';
        result.style.color = '';
        result.textContent = e.message;
      }
    };
    const apply = async () => {
      const partial = { obsHost: host.value.trim(), obsPort: Number(port.value), sourceName: sourceSelect.value };
      if (password.value) partial.password = password.value;
      if (!partial.sourceName) {
        toast('warn', 'Bitte zuerst verbinden und eine Quelle wählen.');
        return;
      }
      if (await saveSettings(partial, 'OBS-Einstellungen gespeichert')) {
        password.value = '';
        password.placeholder = tr('•••••••• (gespeichert)');
      }
    };

    this.refs.obsLine = h('div', { class: 'row small' }, h('span', { class: 'dot ' + ((OBS_PILL[st && st.obs] || [''])[0]) }), h('span', { class: 'muted' }, st ? st.obsText : ''));
    const card = h('div', { class: 'card step' + (done ? ' done' : '') },
      h('div', { class: 'step-head' }, h('div', { class: 'step-num' }, '1'), h('h2', {}, 'Bildquelle'), h('span', { class: 'spacer' }), this.refs.obsLine),
      this.sourceSwitch());
    if (s.captureSource === 'screen') {
      card.append(this.screenPicker());
      return card;
    }
    card.append(
      h('div', { class: 'hint', style: { marginBottom: '16px' } }, icon('info'),
        h('div', {}, 'In OBS: ', h('b', {}, 'Werkzeuge → WebSocket-Servereinstellungen'), ' → „WebSocket-Server aktivieren“ anhaken. Das Passwort kopierst du dort über ', h('b', {}, '„Verbindungsinformationen anzeigen“'), '.')),
      h('div', { class: 'form-grid' },
        h('div', { class: 'field' }, h('label', {}, 'Adresse'), host),
        h('div', { class: 'field' }, h('label', {}, 'Port'), port),
        h('div', { class: 'field' }, h('label', {}, 'Passwort'), password)),
      h('div', { class: 'row', style: { marginTop: '12px' } }, h('button', { class: 'btn', onclick: connect }, 'Verbinden & Quellen laden'), result),
      h('div', { class: 'row', style: { marginTop: '16px' } },
        h('div', { class: 'field grow', style: { maxWidth: '520px' } }, h('label', {}, 'Bildquelle (deine Hunt-Spielaufnahme)'), sourceSelect),
        h('button', { class: 'btn primary', style: { alignSelf: 'flex-end' }, onclick: apply }, 'Übernehmen')),
      h('p', { class: 'small faint', style: { marginTop: '10px' } }, 'Die Quelle muss in der gerade aktiven Szene sichtbar sein, sonst nimmt OBS das Spiel nicht auf.'));
    return card;
  },

  // OBS or straight from a monitor.
  sourceSwitch() {
    const current = state.settings.captureSource === 'screen' ? 'screen' : 'obs';
    const buttons = h('div', { class: 'segmented' }, [['obs', 'Über OBS'], ['screen', 'Bildschirm direkt']].map(([value, label]) =>
      h('button', {
        class: value === current ? 'active' : '',
        onclick: async () => {
          if (value === current) return;
          if (await saveSettings({ captureSource: value })) rerender();
        },
      }, label)));
    return h('div', { class: 'row', style: { marginBottom: '16px' } }, buttons,
      h('span', { class: 'small faint' }, current === 'screen'
        ? 'HuntXtract liest direkt von einem Monitor – OBS muss dafür nicht laufen.'
        : 'HuntXtract holt das Bild aus deiner OBS-Aufnahme.'));
  },

  // Nothing to pick: the picture is taken from the monitor Hunt runs on.
  screenPicker() {
    return h('div', {},
      h('div', { class: 'hint', style: { marginBottom: '10px' } }, icon('shield'),
        h('div', {}, h('b', {}, 'Nur Hunt wird aufgenommen: '),
          'HuntXtract liest nur Hunts Fenster und nur, solange Hunt im Vordergrund ist. Ist ein anderes Programm vorne, bleibt das Bild schwarz – dein Desktop und andere Programme werden nie gelesen.')),
      h('div', { class: 'hint', style: { marginBottom: '16px' } }, icon('info'),
        h('div', {}, 'Hunt sollte dafür im ', h('b', {}, 'randlosen Fenster'), ' laufen. Im exklusiven Vollbild liefert Windows oft ein schwarzes Bild – dann ist OBS die bessere Quelle. ' +
          'Das Countdown-Fenster von HuntXtract wird nicht mitgelesen.')),
      h('p', { class: 'small faint' }, 'Den Bildschirm, auf dem Hunt läuft, findet HuntXtract selbst – auch mit mehreren Monitoren. Im nächsten Schritt siehst du ein Livebild – daran erkennst du sofort, ob die Aufnahme klappt.'));
  },

  /* ---------- step 2: capture reference */
  stepCapture() {
    const hasRef = !!state.calibration.referenceUrl;
    const fromScreen = state.settings.captureSource === 'screen';
    const img = h('img', { alt: 'Livebild', style: { display: 'none' } });
    const placeholder = h('div', { class: 'placeholder' }, 'Noch kein Bild – ist Schritt 1 erledigt und läuft das Spiel?');
    this.refs.previewImg = img;
    this.refs.previewPlaceholder = placeholder;
    const box = h('div', { class: 'preview-box' }, img, placeholder,
      h('div', { class: 'preview-tag' }, h('span', { class: 'dot ok' }), fromScreen ? 'LIVE VOM BILDSCHIRM' : 'LIVE AUS OBS'));

    const captureButton = h('button', { class: 'btn primary', onclick: () => this.capture('summary') }, icon('camera'), 'Jetzt aufnehmen');
    const delayedButton = h('button', { class: 'btn', onclick: () => this.startCountdown(delayedButton, 'summary') }, 'In 5 Sekunden aufnehmen');
    this.refs.captureButtons = [captureButton, delayedButton];
    this.showPreviewState();
    return h('div', { class: 'card step' + (hasRef ? ' done' : '') },
      h('div', { class: 'step-head' }, h('div', { class: 'step-num' }, '2'), h('h2', {}, 'Mission Summary aufnehmen')),
      h('p', { class: 'lead' }, 'Öffne in Hunt nach einem Match die Mission Summary (mit Bounty Collected, Monsters Killed, Hunters Killed …) und warte, bis alle Zahlen fertig hochgezählt sind. Dann aufnehmen. Mit „In 5 Sekunden“ hast du Zeit, zurück ins Spiel zu wechseln.'),
      box,
      h('div', { class: 'row', style: { marginTop: '14px' } }, captureButton, delayedButton,
        hasRef && h('span', { class: 'muted small' }, 'Eine Mission Summary ist schon aufgenommen – neu aufnehmen ersetzt sie, markierte Felder bleiben.')));
  },

  startCountdown(button, page) {
    if (this.countdown) return;
    let left = 5;
    button.disabled = true;
    button.textContent = t('Aufnahme in {n} …', { n: left });
    const timer = setInterval(() => {
      left--;
      if (left > 0) {
        button.textContent = t('Aufnahme in {n} …', { n: left });
        return;
      }
      clearInterval(timer);
      this.countdown = null;
      button.disabled = false;
      button.textContent = t('In 5 Sekunden aufnehmen');
      this.capture(page);
    }, 1000);
    this.countdown = { timer };
  },

  async capture(page) {
    if (this.capturing) return;
    // Straight from the monitor only Hunt is taken: it waits until the game is in front.
    const st = state.status || {};
    if (state.settings.captureSource === 'screen' && st.huntHidden === 'NotInFront') {
      toast('info', 'Wechsle zu Hunt – aufgenommen wird, sobald Hunt im Vordergrund ist.');
    }
    this.capturing = true;
    try {
      const r = await api.call('captureReference', { page });
      const target = page === 'team' ? state.calibration.team : page === 'map' ? state.calibration.map : state.calibration;
      target.referenceUrl = r.url;
      target.hasReference = true;
      if (page === 'map') {
        this.mapResult = null;
        toast('ok', t('Karte aufgenommen ({w}×{h}). Prüf jetzt die Rahmen und teste die Karte.', { w: r.width, h: r.height }));
      } else {
        toast('ok', t('{page} aufgenommen ({w}×{h}). Jetzt die Felder markieren.', { page: page === 'team' ? 'Team Details' : 'Mission Summary', w: r.width, h: r.height }));
      }
      if (state.view === 'setup') rerender();
    } catch (e) {
      toast('error', e.message);
    } finally {
      this.capturing = false;
    }
  },

  onPreview(data) {
    const img = this.refs.previewImg;
    if (img && data.image) {
      img.src = data.image;
      img.style.display = 'block';
      this.refs.previewPlaceholder.style.display = 'none';
    }
    this.updateMeter(data.similarity);
  },

  // Straight from the monitor only Hunt is read: without Hunt in front, the live picture says why
  // there is none instead of showing an old one.
  showPreviewState() {
    const img = this.refs.previewImg;
    const placeholder = this.refs.previewPlaceholder;
    if (!img || !placeholder) return;
    const st = state.status || {};
    const hidden = state.settings.captureSource === 'screen' && st.obs === 'Ready' ? HUNT_HIDDEN[st.huntHidden] : null;
    const tagDot = img.parentElement && img.parentElement.querySelector('.preview-tag .dot');
    if (tagDot) tagDot.className = 'dot' + (hidden ? '' : ' ok');
    if (hidden) {
      img.style.display = 'none';
      placeholder.style.display = '';
      placeholder.textContent = tr(st.huntHidden === 'NotRunning'
        ? 'Hunt läuft nicht. HuntXtract nimmt nur Hunt auf – starte das Spiel, dann erscheint hier das Livebild.'
        : st.huntHidden === 'OtherScreen'
          ? 'Hunt läuft auf einem anderen Bildschirm – wähle in Schritt 1 den Bildschirm, auf dem Hunt läuft.'
          : 'Hunt ist gerade nicht im Vordergrund. HuntXtract nimmt nur Hunt auf – wechsle ins Spiel, dann erscheint hier das Livebild.');
      if (this.refs.meterFill) this.refs.meterFill.style.width = '0%';
      if (this.refs.meterText) {
        this.refs.meterText.textContent = t('Kein Bild – {why}', { why: tr(hidden[1]) });
        this.refs.meterText.style.color = '';
      }
    } else if (img.getAttribute('src')) {
      img.style.display = 'block';
      placeholder.style.display = 'none';
    } else {
      placeholder.textContent = tr('Noch kein Bild – ist Schritt 1 erledigt und läuft das Spiel?');
    }
  },

  /* ---------- step 3: mark fields */
  stepFields() {
    const url = state.calibration.referenceUrl;
    const card = h('div', { class: 'card step' + (state.calibration.ready ? ' done' : '') },
      h('div', { class: 'step-head' }, h('div', { class: 'step-num' }, '3'), h('h2', {}, 'Felder markieren'), h('span', { class: 'spacer' }),
        h('button', { class: 'btn small', title: 'Zeigt eine fertige Einrichtung – so ungefähr gehören die Rahmen hin', onclick: () => showExamples(0) }, icon('image'), 'Beispiel ansehen')));
    if (!url) {
      card.append(h('div', { class: 'empty' }, icon('image'), h('h3', {}, 'Noch keine Mission Summary aufgenommen'), h('p', { class: 'small' }, 'Erledige zuerst Schritt 2.')));
      return card;
    }
    card.append(h('p', { class: 'lead' }, 'Wähle rechts einen Eintrag und zieh auf dem Bild einen Rahmen darum. Rahmen lassen sich verschieben und an den Ecken ändern. Tipp: die Rahmen eng um die Zahl legen, ohne Beschriftung daneben.'));
    card.append(h('div', { class: 'hint', style: { margin: '0 0 14px' } }, icon('info'),
      h('span', {}, h('b', {}, 'nur bei Extracted: '),
        'Felder, die nur in der Mission Summary stehen, wenn dein Hunter rausgekommen ist – zum Beispiel Hunter Level oder Hunter Progress. ' +
        'HuntXtract trägt dann bei jedem Match von selbst „Extracted“ oder „Died“ ein.')));

    card.append(this.fieldEditor('summary', url));
    return card;
  },

  // Image plus list of boxes for one page – the Mission Summary, the Team Details or the map.
  fieldEditor(page, url) {
    const editor = createEditor({
      imageUrl: url,
      items: () => this.items(page),
      selected: () => this.selectedOf(page),
      onSelect: (id) => {
        this.select(page, id);
        this.refreshList(page);
      },
      onChange: (id, area, final) => {
        if (id) this.setArea(id, area, page);
        if (final) {
          this.markDirty();
          if (page === 'map') this.mapResult = null;
          this.refreshList(page);
        }
      },
    });
    if (page === 'team') this.teamEditor = editor;
    else if (page === 'map') this.mapEditor = editor;
    else this.editor = editor;

    const list = h('div', { class: 'field-list' });
    if (page === 'team') this.refs.teamList = list;
    else if (page === 'map') this.refs.mapList = list;
    else this.refs.list = list;
    this.refreshList(page);
    const side = h('div', {}, list,
      page !== 'map' && h('button', { class: 'btn small', style: { marginTop: '10px' }, onclick: () => this.addField(page) }, icon('plus'), 'Feld hinzufügen'));

    const zoom = store.get('editorZoom', 1);
    editor.setZoom(zoom);
    const zoomButtons = h('div', { class: 'segmented' }, [1, 1.5, 2, 3].map((z) => h('button', {
      class: z === zoom ? 'active' : '',
      onclick: (e) => {
        store.set('editorZoom', z);
        editor.setZoom(z);
        e.currentTarget.parentNode.querySelectorAll('button').forEach((b) => b.classList.toggle('active', b === e.currentTarget));
      },
    }, Math.round(z * 100) + ' %')));
    const imageColumn = h('div', {},
      h('div', { class: 'editor-tools' }, h('span', { class: 'label' }, 'ZOOM'), zoomButtons, h('span', { class: 'small faint' }, 'Größer zoomen hilft, die Rahmen genau um kleine Zahlen zu legen.')),
      h('div', { class: 'editor-scroll' }, editor.el));
    return h('div', { class: 'editor' }, imageColumn, side);
  },

  refreshList(page) {
    if (page === 'map') {
      this.refreshMapList();
      return;
    }
    const team = page === 'team';
    const list = team ? this.refs.teamList : this.refs.list;
    if (!list) return;
    list.replaceChildren();
    const d = team ? this.draft.team : this.draft;
    const results = team ? this.teamResults : this.results;
    const editor = team ? this.teamEditor : this.editor;
    // Derived fields have no box on the image, so they come after the drawable ones.
    const entries = this.items(page).concat(d.fields.filter((f) => f.derived).map((f) => ({ id: f.id, label: f.label, color: 'var(--faint)', area: null })));
    for (const item of entries) {
      const isAnchor = item.id === 'anchor';
      const field = isAnchor ? null : d.fields.find((f) => f.id === item.id);
      const selected = (team ? this.teamSelected : this.selected) === item.id;
      const node = h('div', {
        class: 'field-item' + (selected && !(field && field.derived) ? ' selected' : ''),
        vars: { '--c': item.color },
        onclick: () => {
          if (field && field.derived) return;
          if (team) this.teamSelected = item.id;
          else this.selected = item.id;
          editor.redraw();
          this.refreshList(page);
        },
      });
      node.append(h('span', { class: 'swatch' }));
      if (isAnchor) {
        node.append(h('div', { class: 'name' }, h('b', {}, 'Wiedererkennung'),
          h('small', {}, item.area
            ? (team ? 'markiert – daran erkennt HuntXtract die Team Details' : 'markiert – daran erkennt HuntXtract die Mission Summary')
            : t('z. B. die Überschrift „{title}“', { title: team ? 'Team Details' : 'Mission Summary' }))));
        node.append(h('div', { class: 'controls' }, item.area ? h('span', { style: { color: 'var(--ok)' } }, icon('check')) : null));
      } else if (field.derived) {
        node.append(h('div', { class: 'name' }, h('b', {}, field.label),
          h('small', {}, 'ermittelt HuntXtract selbst – die Sterne aus der Zeile mit deinem Namen')));
        node.append(h('div', { class: 'controls' }, h('button', {
          class: 'icon-btn',
          title: 'Feld entfernen',
          onclick: (e) => {
            e.stopPropagation();
            d.fields = d.fields.filter((f) => f.id !== field.id);
            this.markDirty();
            this.refreshList(page);
          },
        }, icon('x'))));
        const found = results[field.id];
        if (found) {
          const ok = found.value !== null && found.value !== undefined && found.value !== '';
          node.append(h('div', { class: 'result' }, h('span', { class: 'faint' }, 'Test:'),
            h('span', { class: 'val' + (ok ? '' : ' fail') }, ok ? found.value + ' ★' : 'nicht gefunden'),
            h('span', { class: 'raw', title: found.raw }, found.raw || '')));
        }
      } else {
        const label = h('input', { value: field.label, maxlength: '40', onclick: (e) => e.stopPropagation() });
        label.addEventListener('input', () => {
          field.label = label.value;
          this.markDirty();
          editor.redraw();
        });
        const type = h('select', {
          class: 'input',
          title: 'Zahl: wird zusammengezählt (Kills, Bounty …)\nStand: nur der neueste Wert zählt, wird nicht summiert (Hunter Level …)\nSterne: zählt die hellen Sterne im Rahmen (MMR)\nStatus: Extracted oder Died\nText: beliebiger Text',
          onclick: (e) => e.stopPropagation(),
        }, FIELD_TYPES.map(([value, text]) => h('option', { value, selected: field.type === value }, text)));
        type.addEventListener('change', () => {
          field.type = type.value;
          this.markDirty();
        });
        node.append(h('div', { class: 'name' }, label, h('small', {}, item.area ? 'markiert' : selected ? 'jetzt auf dem Bild einen Rahmen ziehen' : 'noch nicht markiert')));
        node.append(h('div', { class: 'controls' }, type,
          h('button', {
            class: 'icon-btn',
            title: 'Feld entfernen',
            onclick: (e) => {
              e.stopPropagation();
              d.fields = d.fields.filter((f) => f.id !== field.id);
              if (team && this.teamSelected === field.id) this.teamSelected = 'anchor';
              if (!team && this.selected === field.id) this.selected = 'anchor';
              this.markDirty();
              editor.redraw();
              this.refreshList(page);
            },
          }, icon('x'))));
        // Fields the Mission Summary only shows for a surviving hunter give away the outcome.
        if (!team && field.type !== 'status') {
          const survivor = h('input', { type: 'checkbox', checked: field.survivorOnly, onclick: (e) => e.stopPropagation() });
          survivor.addEventListener('change', () => {
            field.survivorOnly = survivor.checked;
            this.markDirty();
            this.refreshList(page);
          });
          node.append(h('label', {
            class: 'survivor-flag' + (field.survivorOnly ? ' on' : ''),
            title: 'Steht in der Mission Summary nur, wenn der Hunter rausgekommen ist – z. B. Hunter Level oder Hunter Progress.\nIst keins dieser Felder lesbar, trägt HuntXtract „Died“ ein, sonst „Extracted“.',
            onclick: (e) => e.stopPropagation(),
          }, survivor, 'nur bei Extracted'));
        }
        const r = results[field.id];
        if (r) {
          const ok = r.value !== null && r.value !== undefined && r.value !== '';
          const shown = field.type === 'status' ? (r.value === 'extrahiert' ? 'Extracted' : r.value === 'gestorben' ? 'Died' : 'nicht erkannt')
            : ok ? String(r.value) + (field.type === 'sterne' ? ' ★' : '') : field.survivorOnly ? 'leer – zählt als Died' : 'nicht erkannt';
          node.append(h('div', { class: 'result' }, h('span', { class: 'faint' }, 'Test:'),
            h('span', { class: 'val' + (ok || (field.survivorOnly && field.type !== 'status') ? '' : ' fail') }, shown),
            h('span', { class: 'raw', title: r.raw }, r.raw ? t('„{raw}“', { raw: r.raw }) : '')));
        }
      }
      list.append(node);
    }
  },

  addField(page) {
    const team = page === 'team';
    const id = 'feld' + Date.now().toString(36);
    const d = team ? this.draft.team : this.draft;
    d.fields.push({ id, label: tr('Neues Feld'), type: team ? 'sterne' : 'number', survivorOnly: false, derived: false, area: null });
    if (team) this.teamSelected = id;
    else this.selected = id;
    this.markDirty();
    (team ? this.teamEditor : this.editor).redraw();
    this.refreshList(page);
  },

  /* ---------- step 4: the Team Details page (optional) */
  stepTeam() {
    const team = state.calibration.team || {};
    const url = team.referenceUrl;
    const card = h('div', { class: 'card step' + (team.ready ? ' done' : '') },
      h('div', { class: 'step-head' }, h('div', { class: 'step-num' }, '4'), h('h2', {}, 'Team Details'),
        h('span', { class: 'muted small' }, 'optional'), h('span', { class: 'spacer' }),
        h('button', { class: 'btn small', title: 'Zeigt eine fertige Einrichtung der Team Details', onclick: () => showExamples(1) }, icon('image'), 'Beispiel ansehen')));
    card.append(h('p', { class: 'lead' }, 'Wechselst du nach der Mission Summary auf „Team Details“, kann HuntXtract auch diese Seite lesen und die Werte beim selben Match ergänzen: Namen deiner Mitspieler, deren MMR-Sterne und die Match-MMR.'));

    const name = h('input', { class: 'input', value: state.settings.playerName || '', placeholder: 'Dein Steam-Name', maxlength: '40' });
    const saveName = async () => {
      if ((state.settings.playerName || '') === name.value.trim()) return;
      await saveSettings({ playerName: name.value.trim() });
    };
    name.addEventListener('change', saveName);
    name.addEventListener('blur', saveName);
    card.append(h('div', { class: 'row', style: { marginBottom: '14px', alignItems: 'flex-end' } },
      h('div', { class: 'field', style: { maxWidth: '340px' } }, h('label', {}, 'Dein Name in Hunt'), name),
      h('span', { class: 'small faint', style: { maxWidth: '520px' } }, 'Deine Zeile steht mal oben, mal unten. HuntXtract sucht die Zeile mit diesem Namen und übernimmt deren Sterne als „Eigene MMR“.')));

    if (!url) {
      const captureButton = h('button', { class: 'btn', onclick: () => this.capture('team') }, icon('camera'), 'Jetzt aufnehmen');
      const delayedButton = h('button', { class: 'btn', onclick: () => this.startCountdown(delayedButton, 'team') }, 'In 5 Sekunden aufnehmen');
      card.append(h('div', { class: 'empty' }, icon('image'), h('h3', {}, 'Noch keine Team Details aufgenommen'),
        h('p', { class: 'small' }, 'Öffne in Hunt nach einem Match den Reiter „Team Details“ und nimm ihn auf. Ohne diesen Schritt bleibt alles wie bisher.')));
      card.append(h('div', { class: 'row' }, captureButton, delayedButton));
      return card;
    }

    const testButton = h('button', { class: 'btn', onclick: () => this.test(testButton, 'team') }, icon('play'), 'Auslesen testen');
    const captureButton = h('button', { class: 'btn ghost small', onclick: () => this.capture('team') }, icon('camera'), 'Neu aufnehmen');
    card.append(h('div', { class: 'hint', style: { margin: '0 0 14px' } }, icon('info'),
      h('span', {}, h('b', {}, 'Sterne: '), 'Für MMR ziehst du den Rahmen nur um die Sternenreihe, ohne die Beschriftung „MMR“ daneben. ' +
        'HuntXtract zählt die hellen Sterne – aus fünf hellen und einem dunklen wird die 5.')));
    if (!state.app.ocrCyrillic) {
      card.append(h('div', { class: 'hint', style: { margin: '0 0 14px' } }, icon('alert'),
        h('span', {}, h('b', {}, 'Kyrillische Namen: '), t('Deine Texterkennung kann nur {lang}. Namen wie „Хантер“ kommen damit als Buchstabensalat an. ' +
          'Abhilfe: Windows-Einstellungen → Zeit und Sprache → Sprache und Region → Sprache hinzufügen → Russisch, dabei das optionale Feature „Texterkennung“ mitinstallieren (nicht als Anzeigesprache setzen). ' +
          'Danach HuntXtract neu starten, dann werden kyrillische Namen richtig gelesen und gezählt.', { lang: state.app.ocrLanguage }))));
    }
    card.append(this.fieldEditor('team', url));
    card.append(h('div', { class: 'row', style: { marginTop: '14px' } }, testButton, captureButton,
      h('span', { class: 'small faint' }, 'Gespeichert wird alles zusammen mit Schritt 6.')));
    return card;
  },

  /* ---------- step 5: the map on Tab (optional) – for the trail of each match */
  stepMap() {
    const map = state.calibration.map || {};
    const url = map.referenceUrl;
    const card = h('div', { class: 'card step' + (map.ready ? ' done' : '') },
      h('div', { class: 'step-head' }, h('div', { class: 'step-num' }, '5'), h('h2', {}, 'Karte · Laufspur'),
        h('span', { class: 'muted small' }, 'optional')));
    card.append(h('p', { class: 'lead' }, 'Drückst du im Match Tab, zeigt Hunt die Karte mit deinem gelben Punkt. HuntXtract merkt sich bei jedem Blick darauf, wo du gerade bist, und zeichnet daraus deine Laufspur – zu sehen in der Match-Ansicht.'));
    if (!url) {
      const captureButton = h('button', { class: 'btn', onclick: () => this.capture('map') }, icon('camera'), 'Jetzt aufnehmen');
      const delayedButton = h('button', { class: 'btn', onclick: () => this.startCountdown(delayedButton, 'map') }, 'In 5 Sekunden aufnehmen');
      card.append(h('div', { class: 'empty' }, icon('image'), h('h3', {}, 'Noch keine Karte aufgenommen'),
        h('p', { class: 'small' }, 'Öffne zu Rundenbeginn, vor dem ersten Clue, mit Tab die Karte, ohne zu zoomen, und nimm sie auf – am einfachsten mit „In 5 Sekunden aufnehmen“.')));
      card.append(h('div', { class: 'row' }, captureButton, delayedButton));
      return card;
    }
    card.append(h('div', { class: 'hint', style: { margin: '0 0 14px' } }, icon('info'),
      h('span', {}, 'Die Rahmen sind für 16:9-Bildschirme schon gesetzt. Prüf nur, ob der Kartenausschnitt genau auf dem roten Rand liegt und die Wiedererkennung die Legende umfasst. ' +
        'Gezählt wird nur die Karte, wie Hunt sie öffnet – gezoomt passen die Positionen nicht. ' +
        'Jede Karte merkt sich HuntXtract einmal zu Rundenbeginn, solange der rote Rand noch außen herum läuft – danach erkennt es sie die ganze Runde.')));
    card.append(this.fieldEditor('map', url));
    const testButton = h('button', { class: 'btn', onclick: () => this.testMap(testButton) }, icon('play'), 'Karte testen');
    const captureButton = h('button', { class: 'btn ghost small', onclick: () => this.capture('map') }, icon('camera'), 'Neu aufnehmen');
    card.append(h('div', { class: 'row', style: { marginTop: '14px' } }, testButton, captureButton,
      h('span', { class: 'small faint' }, 'Gespeichert wird alles zusammen mit Schritt 6.')));
    return card;
  },

  // The three boxes of the map, each with a word on what it is for, and the result of the test.
  refreshMapList() {
    const list = this.refs.mapList;
    if (!list) return;
    list.replaceChildren();
    const r = this.mapResult;
    const hints = {
      anchor: 'die Legende rechts – daran erkennt HuntXtract die Karte',
      mapArea: 'genau auf dem roten Rand der Karte',
      nameArea: 'der Name über der Karte',
    };
    for (const item of this.items('map')) {
      const selected = this.mapSelected === item.id;
      const node = h('div', {
        class: 'field-item' + (selected ? ' selected' : ''),
        vars: { '--c': item.color },
        onclick: () => {
          this.mapSelected = item.id;
          this.mapEditor.redraw();
          this.refreshMapList();
        },
      }, h('span', { class: 'swatch' }),
      h('div', { class: 'name' }, h('b', {}, item.label),
        h('small', {}, item.area ? hints[item.id] : selected ? 'jetzt auf dem Bild einen Rahmen ziehen' : 'noch nicht markiert')),
      h('div', { class: 'controls' }, item.area ? h('span', { style: { color: 'var(--ok)' } }, icon('check')) : null));
      if (r && item.id === 'mapArea') {
        node.append(h('div', { class: 'result' }, h('span', { class: 'faint' }, 'Test:'),
          h('span', { class: 'val' + (r.found && r.unzoomed ? '' : ' fail') }, r.found
            ? t('dein Punkt bei {x} % / {y} %', { x: Math.round(r.x * 100), y: Math.round(r.y * 100) }) : 'kein gelber Punkt gefunden'),
          h('span', { class: 'raw' }, r.unzoomed ? 'roter Rand gefunden' : t('roter Rand nicht gefunden ({edges})', { edges: r.edges.map((e) => e + ' %').join(' · ') }))));
      }
      if (r && item.id === 'nameArea') {
        node.append(h('div', { class: 'result' }, h('span', { class: 'faint' }, 'Test:'),
          h('span', { class: 'val' + (r.name ? '' : ' fail') }, r.name || 'nicht erkannt'),
          h('span', { class: 'raw', title: r.nameRaw }, r.nameRaw ? t('„{raw}“', { raw: r.nameRaw }) : '')));
      }
      list.append(node);
    }
  },

  async testMap(button) {
    button.disabled = true;
    try {
      const m = this.draft.map;
      this.mapResult = await api.call('testOcr', { page: 'map', map: { anchor: m.anchor, mapArea: m.mapArea, nameArea: m.nameArea } });
      this.refreshMapList();
      const r = this.mapResult;
      if (!r.unzoomed) toast('warn', 'Der rote Rand liegt nicht außen am Kartenausschnitt. Zieh den Rahmen genau auf den Rand – und nimm die Karte zu Rundenbeginn auf: ungezoomt und vor dem ersten Clue.');
      else if (!r.found) toast('warn', 'Auf der Karte ist kein gelber Punkt zu finden. Nimm die Karte im Match auf, dann ist dein Punkt zu sehen.');
      else toast('ok', r.name ? t('Karte erkannt: {name}. Dein Punkt ist gefunden – dann speichern.', { name: r.name }) : 'Dein Punkt ist gefunden – dann speichern.');
    } catch (e) {
      toast('error', e.message);
    } finally {
      button.disabled = false;
    }
  },

  /* ---------- step 6: test & save */
  stepTest() {
    const d = this.draft;
    const hasRef = !!state.calibration.referenceUrl;
    const thresholdValue = h('b', {}, d.threshold + ' %');
    const slider = h('input', { type: 'range', min: '50', max: '98', value: String(d.threshold), style: { width: '220px' } });
    slider.addEventListener('input', () => {
      d.threshold = Number(slider.value);
      thresholdValue.textContent = d.threshold + ' %';
      this.markDirty();
      this.updateMeter(this.lastSimilarity);
    });
    this.refs.meterFill = h('div', { class: 'fill', style: { width: '0%' } });
    this.refs.meterMark = h('div', { class: 'mark', style: { left: d.threshold + '%' } });
    this.refs.meterText = h('span', { class: 'small muted' }, state.calibration.hasAnchor ? 'Warte auf Bild …' : 'Nach dem Speichern siehst du hier live, wie sicher die Mission Summary erkannt wird.');

    const extracted = h('input', { class: 'input', value: d.extractedWords.join(', ') });
    const died = h('input', { class: 'input', value: d.diedWords.join(', ') });
    const words = (input) => input.value.split(',').map((w) => w.trim()).filter(Boolean);
    extracted.addEventListener('input', () => { d.extractedWords = words(extracted); this.markDirty(); });
    died.addEventListener('input', () => { d.diedWords = words(died); this.markDirty(); });

    this.refs.dirty = h('span', { class: 'badge gold' + (this.dirty ? '' : ' hidden') }, 'Ungespeicherte Änderungen');
    const testButton = h('button', { class: 'btn', disabled: !hasRef, onclick: () => this.test(testButton, 'summary') }, icon('play'), 'Auslesen testen');

    return h('div', { class: 'card step' + (state.calibration.ready ? ' done' : '') },
      h('div', { class: 'step-head' }, h('div', { class: 'step-num' }, '6'), h('h2', {}, 'Testen & speichern'), h('span', { class: 'spacer' }), this.refs.dirty),
      h('p', { class: 'lead' }, '„Auslesen testen“ liest die markierten Felder auf der aufgenommenen Mission Summary. Stimmt etwas nicht, den Rahmen enger oder etwas größer ziehen und nochmal testen.'),
      h('div', { class: 'row' }, testButton,
        h('button', { class: 'btn primary', disabled: !hasRef, onclick: () => this.save() }, icon('check'), 'Einrichtung speichern'),
        state.calibration.ready && h('button', { class: 'btn', onclick: () => this.finish() }, 'Fertig – zur Übersicht'),
        h('span', { class: 'grow' }),
        (state.app.preset || {}).available && !state.calibration.preset
          && h('button', { class: 'btn ghost small', onclick: () => this.loadPreset() }, 'Fertige Einrichtung laden'),
        h('button', { class: 'btn ghost small', onclick: () => this.reset() }, 'Alles zurücksetzen')),
      h('div', { style: { marginTop: '22px', maxWidth: '640px' } },
        h('div', { class: 'row', style: { marginBottom: '8px' } }, h('span', { class: 'label' }, 'WIEDERERKENNUNG'), h('span', { class: 'grow' }),
          h('span', { class: 'small muted' }, 'Schwelle '), thresholdValue),
        h('div', { class: 'meter' }, this.refs.meterFill, this.refs.meterMark),
        h('div', { class: 'row', style: { marginTop: '8px' } }, this.refs.meterText, h('span', { class: 'grow' }), slider),
        h('p', { class: 'small faint', style: { marginTop: '6px' } }, 'Solange diese Seite offen ist, wird nichts gespeichert. Zeigt die Anzeige bei offener Mission Summary deutlich mehr als die Schwelle und sonst deutlich weniger, passt alles.')),
      h('details', { style: { marginTop: '18px' } },
        h('summary', { class: 'small muted', style: { cursor: 'pointer' } }, 'Wörter für den Status (Extracted / Died)'),
        h('div', { class: 'form-grid', style: { marginTop: '12px', gridTemplateColumns: '1fr 1fr' } },
          h('div', { class: 'field' }, h('label', {}, 'Bedeutet „Extracted“'), extracted),
          h('div', { class: 'field' }, h('label', {}, 'Bedeutet „Died“'), died))));
  },

  lastSimilarity: null,

  updateMeter(similarity) {
    this.lastSimilarity = similarity;
    const fill = this.refs.meterFill;
    if (!fill) return;
    const threshold = this.draft.threshold;
    this.refs.meterMark.style.left = threshold + '%';
    if (similarity === null || similarity === undefined) {
      fill.style.width = '0%';
      return;
    }
    const hit = similarity >= threshold;
    fill.style.width = clamp(similarity, 0, 100) + '%';
    fill.classList.toggle('hit', hit);
    this.refs.meterText.textContent = t(hit ? 'Jetzt: {pct} % – Mission Summary erkannt (gespeichert wird erst außerhalb der Einrichtung)'
      : 'Jetzt: {pct} % – keine Mission Summary', { pct: Math.round(similarity) });
    this.refs.meterText.style.color = hit ? 'var(--ok)' : '';
  },

  payload() {
    const d = this.draft;
    return {
      anchor: d.anchor,
      threshold: d.threshold,
      fields: d.fields.map(fieldPayload),
      team: { anchor: d.team.anchor, fields: d.team.fields.map(fieldPayload) },
      map: { anchor: d.map.anchor, mapArea: d.map.mapArea, nameArea: d.map.nameArea },
      extractedWords: d.extractedWords,
      diedWords: d.diedWords,
    };
  },

  async test(button, page) {
    const team = page === 'team';
    button.disabled = true;
    try {
      const results = await api.call('testOcr', Object.assign(this.payload(), { page: team ? 'team' : 'summary' }));
      if (team) this.teamResults = results;
      else this.results = results;
      this.refreshList(page);
      const fields = (team ? this.draft.team.fields : this.draft.fields).filter((f) => f.area && !f.derived);
      const failed = fields.filter((f) => {
        const r = results[f.id];
        return !r || r.value === null || r.value === undefined || r.value === '';
      });
      if (!fields.length) toast('warn', 'Noch keine Felder markiert.');
      else if (failed.length && team && state.calibration.preset && failed.every((f) => f.type === 'text')) {
        // The sample picture of the ready-made setup has the names made unreadable.
        toast('ok', 'Die MMR wird erkannt. Die Namen sind auf dem Beispielbild unkenntlich gemacht – in deinen Matches liest HuntXtract sie mit.', 7000);
      } else if (failed.length) {
        toast('warn', t('{failed} von {total} Feldern nicht erkannt: {names}',
          { failed: failed.length, total: fields.length, names: failed.map((f) => tr(f.label)).join(', ') }));
      } else toast('ok', t('Alle {n} Felder erkannt. Passen die Werte? Dann speichern.', { n: fields.length }));
    } catch (e) {
      toast('error', e.message);
    } finally {
      button.disabled = false;
    }
  },

  async save() {
    const p = this.payload();
    if (!p.anchor) {
      toast('warn', 'Bitte zuerst den Bereich „Wiedererkennung“ markieren.');
      this.selected = 'anchor';
      this.refreshList('summary');
      return;
    }
    if (p.team.anchor && !p.team.fields.some((f) => f.area)) {
      toast('warn', 'Markiere in den Team Details mindestens ein Feld – oder entferne dort die Wiedererkennung.');
      return;
    }
    if (!p.team.anchor && p.team.fields.some((f) => f.area)) {
      toast('warn', 'In den Team Details fehlt die Wiedererkennung, sonst findet HuntXtract die Seite nicht.');
      this.teamSelected = 'anchor';
      this.refreshList('team');
      return;
    }
    if (!p.fields.some((f) => f.area)) {
      toast('warn', 'Bitte mindestens ein Feld markieren.');
      return;
    }
    if (state.calibration.map && state.calibration.map.referenceUrl && (!p.map.anchor || !p.map.mapArea)) {
      toast('warn', 'Für die Karte fehlt die Wiedererkennung oder der Kartenausschnitt.');
      this.mapSelected = p.map.anchor ? 'mapArea' : 'anchor';
      this.refreshMapList();
      return;
    }
    try {
      const c = await api.call('saveCalibration', p);
      state.calibration = c;
      this.dirty = false;
      if (this.refs.dirty) this.refs.dirty.classList.add('hidden');
      toast('ok', 'Einrichtung gespeichert. Gezählt wird, sobald du die Einrichtung verlässt – z. B. mit „Fertig – zur Übersicht“.', 7000);
      if (c.warning) toast('warn', c.warning, 10000);
      const results = this.results;
      const teamResults = this.teamResults;
      this.resetDraft();
      this.results = results;
      this.teamResults = teamResults;
      rerender();
    } catch (e) {
      toast('error', e.message);
    }
  },

  // Leave the setup page, so detection saves matches again.
  async finish() {
    if (this.dirty) {
      if (!(await confirmDialog('Ungespeicherte Änderungen', 'Deine Änderungen an der Einrichtung sind noch nicht gespeichert. Trotzdem zur Übersicht wechseln? Die Änderungen gehen dann verloren.', 'Verwerfen & wechseln', true))) return;
      this.resetDraft();
    }
    show('dashboard');
  },

  async reset() {
    if (!(await confirmDialog('Einrichtung zurücksetzen?', 'Die aufgenommene Mission Summary und alle markierten Felder werden gelöscht. Deine gespeicherten Matches bleiben erhalten.', 'Zurücksetzen', true))) return;
    try {
      state.calibration = await api.call('resetCalibration');
      this.resetDraft();
      this.selected = 'anchor';
      rerender();
      toast('ok', 'Einrichtung zurückgesetzt');
    } catch (e) {
      toast('error', e.message);
    }
  },

  // The setup that comes with HuntXtract (16:9, in the language of the game) replaces this one.
  async loadPreset() {
    const preset = state.app.preset || {};
    const c = state.calibration;
    const warn = !preset.fits && preset.reason ? ' ' + t('Achtung: {reason} Die Rahmen passen dann vermutlich nicht.', { reason: preset.reason }) : '';
    const replaces = !!(c.ready || c.referenceUrl || (c.team && c.team.referenceUrl) || (c.map && c.map.referenceUrl));
    if (replaces || warn) {
      const text = t(replaces
        ? 'Die fertige Einrichtung für Hunt in 16:9 ersetzt deine Rahmen und die aufgenommenen Bilder. Deine gespeicherten Matches bleiben erhalten.{warn}'
        : 'Die fertige Einrichtung für Hunt in 16:9 setzt alle Rahmen für dich.{warn}', { warn });
      if (!(await confirmDialog('Vorgabe laden?', text, 'Vorgabe laden', replaces))) return;
    }
    try {
      state.calibration = await api.call('applyPreset');
      this.resetDraft();
      rerender();
      toast('ok', 'Fertige Einrichtung geladen. Spiel einfach ein Match – verschieben kannst du die Rahmen jederzeit.', 7000);
    } catch (e) {
      toast('error', e.message);
    }
  },

  onStatus() {
    const line = this.refs.obsLine;
    const st = state.status;
    if (line && st) {
      line.replaceChildren(h('span', { class: 'dot ' + ((OBS_PILL[st.obs] || [''])[0]) }), h('span', { class: 'muted' }, st.obsText));
    }
    this.showPreviewState();
  },
};

// Draw/move/resize rectangles (relative coordinates) on top of the reference image.
function createEditor(options) {
  const canvas = h('div', { class: 'editor-canvas' });
  const img = h('img', { src: options.imageUrl, alt: 'Aufgenommene Mission Summary', draggable: 'false' });
  const layer = h('div', { class: 'editor-layer' });
  canvas.append(img, layer);
  let drag = null;

  const point = (e) => {
    const r = layer.getBoundingClientRect();
    return { x: clamp((e.clientX - r.left) / r.width, 0, 1), y: clamp((e.clientY - r.top) / r.height, 0, 1) };
  };
  const rect = (a, b) => ({ x: Math.min(a.x, b.x), y: Math.min(a.y, b.y), w: Math.abs(a.x - b.x), h: Math.abs(a.y - b.y) });

  function redraw() {
    layer.replaceChildren();
    const selected = options.selected();
    for (const item of options.items()) {
      if (!item.area) continue;
      const a = item.area;
      layer.append(h('div', {
        class: 'box' + (item.id === selected ? ' selected' : ''),
        dataset: { id: item.id },
        vars: { '--c': item.color },
        style: { left: a.x * 100 + '%', top: a.y * 100 + '%', width: a.w * 100 + '%', height: a.h * 100 + '%' },
      },
      h('span', { class: 'tag' }, item.label),
      ['nw', 'ne', 'sw', 'se'].map((c) => h('span', { class: 'handle ' + c, dataset: { handle: c } }))));
    }
  }

  layer.addEventListener('pointerdown', (e) => {
    if (e.button !== 0) return;
    const p = point(e);
    const boxEl = e.target.closest('.box');
    const items = options.items();
    if (boxEl) {
      const item = items.find((i) => i.id === boxEl.dataset.id);
      options.onSelect(item.id);
      drag = { mode: e.target.dataset.handle ? 'resize' : 'move', handle: e.target.dataset.handle, id: item.id, start: p, orig: { ...item.area } };
    } else {
      const item = items.find((i) => i.id === options.selected());
      if (!item) return;
      drag = { mode: 'draw', id: item.id, start: p, before: item.area ? { ...item.area } : null };
    }
    layer.setPointerCapture(e.pointerId);
    e.preventDefault();
    redraw();
  });

  layer.addEventListener('pointermove', (e) => {
    if (!drag) return;
    const p = point(e);
    let area;
    if (drag.mode === 'draw') {
      area = rect(drag.start, p);
    } else if (drag.mode === 'move') {
      const o = drag.orig;
      area = { x: clamp(o.x + p.x - drag.start.x, 0, 1 - o.w), y: clamp(o.y + p.y - drag.start.y, 0, 1 - o.h), w: o.w, h: o.h };
    } else {
      const o = drag.orig;
      let left = o.x, top = o.y, right = o.x + o.w, bottom = o.y + o.h;
      if (drag.handle.includes('w')) left = p.x;
      if (drag.handle.includes('e')) right = p.x;
      if (drag.handle.includes('n')) top = p.y;
      if (drag.handle.includes('s')) bottom = p.y;
      area = rect({ x: left, y: top }, { x: right, y: bottom });
    }
    options.onChange(drag.id, area, false);
    redraw();
  });

  const finish = () => {
    if (!drag) return;
    const item = options.items().find((i) => i.id === drag.id);
    if (item && item.area && (item.area.w < 0.004 || item.area.h < 0.004)) {
      options.onChange(drag.id, drag.mode === 'draw' ? drag.before : drag.orig, false);
    }
    drag = null;
    options.onChange(null, null, true);
    redraw();
  };
  layer.addEventListener('pointerup', finish);
  layer.addEventListener('pointercancel', finish);

  redraw();
  return {
    el: canvas,
    redraw,
    setZoom(z) {
      canvas.style.width = z * 100 + '%';
    },
  };
}

/* ================================================================= overlay */

const OV = () => window.KopfgeldOverlay;

// Small form controls shared by the overlay editor and the design settings.
function segmented(options, value, onChange, extraClass) {
  const box = h('div', { class: 'segmented' + (extraClass ? ' ' + extraClass : '') });
  for (const [v, label] of options) {
    const button = h('button', { class: v === value ? 'active' : '', dataset: { v } }, label);
    button.addEventListener('click', () => {
      box.querySelectorAll('button').forEach((b) => b.classList.toggle('active', b === button));
      onChange(v);
    });
    box.append(button);
  }
  return box;
}

function selectInput(value, entries, onChange) {
  const el = h('select', { class: 'input' }, entries.map(([v, label]) => h('option', { value: v, selected: v === value }, label)));
  el.addEventListener('change', () => onChange(el.value));
  return el;
}

// onInput runs while dragging, onChange once at the end.
function rangeRow(min, max, step, value, format, onInput, onChange) {
  const out = h('span', { class: 'value' }, format(value));
  const input = h('input', { type: 'range', min: String(min), max: String(max), step: String(step), value: String(value) });
  input.addEventListener('input', () => {
    out.textContent = format(Number(input.value));
    if (onInput) onInput(Number(input.value));
  });
  if (onChange) input.addEventListener('change', () => onChange(Number(input.value)));
  return h('div', { class: 'range-row' }, input, out);
}

function colorField(label, value, onInput) {
  const input = h('input', { type: 'color', value });
  input.addEventListener('input', () => onInput(input.value));
  return h('label', { class: 'color-field' }, input, label);
}

function checkRow(label, checked, onChange) {
  const box = h('input', { type: 'checkbox', checked });
  box.addEventListener('change', () => onChange(box.checked));
  return h('label', { class: 'check' }, box, label);
}

// Picture choice from the fan kit: "none" plus a thumbnail per picture.
function picturePicker(current, extra, onPick) {
  const box = h('div', { class: 'pics' });
  for (const [id, label] of extra) {
    box.append(h('button', { class: 'pic plain' + (current === id ? ' active' : ''), onclick: () => onPick(id) }, label));
  }
  for (const img of OV().IMAGES) {
    box.append(h('button', {
      class: 'pic' + (current === img.id ? ' active' : ''),
      title: img.name,
      style: { backgroundImage: 'url("/img/thumbs/' + img.id + '.jpg")' },
      onclick: () => onPick(img.id),
    }, h('span', { class: 'pic-name' }, img.name)));
  }
  return box;
}

const OVERLAY_SCOPES = [['today', 'Heute'], ['last10', 'Letzte 10 Matches (Ø)'], ['all', 'Gesamt'], ['last', 'Letztes Match']];

// Overlays for OBS: several designs, each with its own address. They live in the program
// (Daten\overlays.json) – OBS picks up every change by itself.
const overlayView = {
  profiles: null,
  currentId: null,
  stageBg: store.get('overlayStage', 'checker'),
  saveTimer: null,
  refs: {},

  load() {
    const O = OV();
    this.profiles = (state.overlays || []).map((p) => O.normalize(p, state.stats));
    if (!this.profiles.length) {
      this.profiles = [this.fromFirstVersion() || O.normalize({ name: 'Overlay 1' }, state.stats)];
      this.save(true);
    }
    const remembered = store.get('overlayCurrent', null);
    this.currentId = this.profiles.some((p) => p.id === remembered) ? remembered : this.profiles[0].id;
  },

  // The first version kept one overlay in the browser storage: it becomes "Overlay 1".
  fromFirstVersion() {
    const old = store.get('overlay', null);
    if (!old || typeof old !== 'object') return null;
    const q = new URLSearchParams();
    if (old.scope) q.set('scope', old.scope);
    if (Array.isArray(old.show)) q.set('show', old.show.join(','));
    if (old.layout) q.set('layout', old.layout);
    if (old.bg) q.set('bg', old.bg);
    if (old.accent) q.set('accent', String(old.accent).replace('#', ''));
    if (old.scale) q.set('scale', String(old.scale));
    if (old.values) q.set('values', old.values);
    if (old.title !== null && old.title !== undefined) q.set('title', old.title);
    const profile = OV().fromLegacy('?' + q.toString());
    profile.name = 'Overlay 1';
    profile.preset = 'modern';
    return profile;
  },

  get current() {
    return this.profiles.find((p) => p.id === this.currentId) || this.profiles[0];
  },

  url(p) {
    return state.settings.overlayUrl + '?o=' + p.id;
  },

  save(now) {
    clearTimeout(this.saveTimer);
    const run = async () => {
      try {
        state.overlays = await api.call('saveOverlays', { profiles: this.profiles });
      } catch (e) {
        toast('error', e.message);
      }
    };
    if (now) run();
    else this.saveTimer = setTimeout(run, 350);
  },

  // redraw: 'all' builds the page again (controls that depend on the change), 'content' also
  // updates the template tiles; otherwise only the preview follows.
  change(fn, redraw) {
    fn(this.current);
    this.save();
    if (redraw === 'all') rerender();
    else {
      this.renderPreview();
      if (redraw === 'content') this.renderPresets();
    }
  },

  select(id) {
    this.currentId = id;
    store.set('overlayCurrent', id);
    rerender();
  },

  addProfile() {
    const p = OV().normalize({ name: 'Overlay ' + (this.profiles.length + 1) }, state.stats);
    this.profiles.push(p);
    this.save(true);
    this.select(p.id);
  },

  duplicate() {
    const copy = JSON.parse(JSON.stringify(this.current));
    copy.id = OV().newId();
    copy.name = t('{name} (Kopie)', { name: this.current.name }).slice(0, 40);
    this.profiles.push(OV().normalize(copy, state.stats));
    this.save(true);
    this.select(copy.id);
  },

  async remove() {
    const p = this.current;
    if (this.profiles.length < 2) return;
    if (!(await confirmDialog('Overlay löschen?',
      t('„{name}“ wird gelöscht. Eine OBS-Quelle mit seiner Adresse zeigt danach das erste Overlay.', { name: p.name }), 'Löschen', true))) return;
    this.profiles = this.profiles.filter((x) => x !== p);
    this.save(true);
    this.select(this.profiles[0].id);
  },

  // All values of the period in the saved order; values that are new since then follow, switched off.
  listItems(p) {
    const available = OV().available(state.stats, p.scope);
    const info = Object.fromEntries(available.map((a) => [a.key, a]));
    for (const a of available) if (!p.items.some((i) => i.key === a.key)) p.items.push({ key: a.key, on: false, label: '' });
    return p.items.filter((i) => info[i.key]).map((item) => ({ item, info: info[item.key] }));
  },

  // keys: the shown rows in their new order. Values of other periods keep their place at the end.
  reorder(keys) {
    this.change((p) => {
      const moved = keys.map((k) => p.items.find((i) => i.key === k)).filter(Boolean);
      p.items = moved.concat(p.items.filter((i) => !keys.includes(i.key)));
    }, 'content');
  },

  move(key, step) {
    const rows = [...this.refs.items.children].map((r) => r.dataset.key);
    const from = rows.indexOf(key);
    const to = from + step;
    if (from < 0 || to < 0 || to >= rows.length) return;
    rows.splice(to, 0, rows.splice(from, 1)[0]);
    this.reorder(rows);
    rerender();
  },

  itemRow(entry) {
    const { item, info } = entry;
    const check = h('input', { type: 'checkbox', checked: item.on, title: 'Im Overlay zeigen' });
    const name = h('input', { class: 'name', value: item.label, placeholder: info.label, maxlength: '40', title: 'Anklicken zum Umbenennen – leer lassen für den normalen Namen' });
    const row = h('div', { class: 'ov-item' + (item.on ? '' : ' off'), dataset: { key: item.key } },
      h('span', { class: 'grip', title: 'Ziehen zum Sortieren' }, icon('grip')),
      check,
      name,
      h('span', { class: 'ov-hint' }, info.hint || ''),
      h('span', { class: 'moves' },
        h('button', { class: 'icon-btn', title: 'Nach oben', onclick: () => this.move(item.key, -1) }, icon('up')),
        h('button', { class: 'icon-btn', title: 'Nach unten', onclick: () => this.move(item.key, 1) }, icon('down'))));
    check.addEventListener('change', () => {
      row.classList.toggle('off', !check.checked);
      this.change(() => { item.on = check.checked; }, 'content');
    });
    name.addEventListener('input', () => this.change(() => { item.label = name.value.trim() ? name.value : ''; }, 'content'));
    return row;
  },

  // Drag by the grip: the row follows the pointer through the list.
  sortable(list) {
    let drag = null;
    list.addEventListener('pointerdown', (e) => {
      const grip = e.target.closest('.grip');
      if (!grip) return;
      e.preventDefault();
      grip.setPointerCapture(e.pointerId);
      drag = { row: grip.closest('.ov-item'), id: e.pointerId, moved: false };
      drag.row.classList.add('dragging');
    });
    list.addEventListener('pointermove', (e) => {
      if (!drag || e.pointerId !== drag.id) return;
      let before = null;
      for (const r of list.children) {
        if (r === drag.row) continue;
        const b = r.getBoundingClientRect();
        if (e.clientY < b.top + b.height / 2) {
          before = r;
          break;
        }
      }
      if (before !== drag.row.nextElementSibling && before !== drag.row) {
        list.insertBefore(drag.row, before);
        drag.moved = true;
      }
    });
    const end = (e) => {
      if (!drag || e.pointerId !== drag.id) return;
      drag.row.classList.remove('dragging');
      const moved = drag.moved;
      drag = null;
      if (moved) this.reorder([...list.children].map((r) => r.dataset.key));
    };
    list.addEventListener('pointerup', end);
    list.addEventListener('pointercancel', end);
  },

  renderPreview() {
    if (!this.refs.inner || !OV()) return;
    OV().render(this.refs.inner, overlayData(), this.current);
    requestAnimationFrame(() => this.fit());
  },

  // Wide overlays are shrunk to fit the stage; the numbers below are the real size in OBS.
  fit() {
    const { stage, inner, size } = this.refs;
    if (!stage || !inner.firstElementChild || !stage.isConnected) return;
    const w = inner.offsetWidth, hgt = inner.offsetHeight;
    const f = Math.min(1, (stage.clientWidth - 36) / Math.max(1, w));
    inner.style.transform = f < 1 ? 'scale(' + f + ')' : '';
    stage.style.height = f < 1 ? Math.ceil(hgt * f + 38) + 'px' : '';
    const box = inner.firstElementChild;
    const up = (v) => Math.ceil((v + 32) / 10) * 10;
    size.textContent = t('Größe im Stream: {w} × {h} px', { w: box.offsetWidth, h: box.offsetHeight }) +
      (f < 1 ? t(' (hier auf {pct} % verkleinert)', { pct: Math.round(f * 100) }) : '') +
      t(' · Browserquelle in OBS mindestens {w} × {h}', { w: up(box.offsetWidth), h: up(box.offsetHeight) });
  },

  presetTiles() {
    const box = h('div', { class: 'presets' });
    this.refs.presets = [];
    for (const [key, preset] of Object.entries(OV().PRESETS)) {
      const inner = h('div', { class: 'ov-stage-inner' });
      const tile = h('button', { class: 'preset', title: t('Vorlage „{name}“ übernehmen', { name: tr(preset.name) }), onclick: () => this.applyPreset(key) },
        h('span', { class: 'preset-stage' }, inner),
        h('span', { class: 'preset-name' }, preset.name));
      this.refs.presets.push({ key, inner, tile });
      box.append(tile);
    }
    return box;
  },

  // Each tile shows the template with your values and layout (first three values).
  renderPresets() {
    const p = this.current;
    let shown = 0;
    const items = p.items.map((i) => Object.assign({}, i, { on: i.on && shown++ < 3 }));
    for (const entry of this.refs.presets || []) {
      const cfg = Object.assign({}, p, OV().PRESETS[entry.key], { items, scale: 1, animate: false });
      OV().render(entry.inner, overlayData(), cfg);
      const f = Math.min(1, 186 / Math.max(1, entry.inner.offsetWidth), 70 / Math.max(1, entry.inner.offsetHeight));
      entry.inner.style.transform = 'translate(-50%, -50%) scale(' + f + ')';
      entry.tile.classList.toggle('active', p.preset === entry.key);
    }
  },

  applyPreset(key) {
    const preset = OV().PRESETS[key];
    this.change((p) => {
      for (const k of OV().DESIGN_KEYS) p[k] = preset[k];
      p.preset = key;
    }, 'all');
  },

  render() {
    if (!this.profiles) this.load();
    const p = this.current;
    this.refs = {};
    const root = h('div', { class: 'stack' });
    const st = state.status || {};

    if (!state.settings.overlayEnabled || st.overlayError) {
      root.append(h('div', { class: 'banner' }, icon('alert'),
        h('div', { class: 'grow' }, h('h3', {}, st.overlayError ? 'Overlay-Server läuft nicht' : 'Overlay ist ausgeschaltet'),
          h('p', {}, st.overlayError || 'Schalte es in den Einstellungen ein, damit OBS die Werte abrufen kann.')),
        h('button', { class: 'btn', onclick: () => show('settings') }, 'Zu den Einstellungen')));
    }

    // Which overlay, and its address for OBS.
    const tabs = h('div', { class: 'ov-tabs' },
      this.profiles.map((x) => h('button', { class: 'ov-tab' + (x.id === p.id ? ' active' : ''), onclick: () => this.select(x.id) }, x.name)),
      h('button', { class: 'ov-tab add', onclick: () => this.addProfile() }, '+ Neues Overlay'));
    const name = h('input', { class: 'input', value: p.name, maxlength: '40' });
    name.addEventListener('input', () => {
      p.name = name.value.trim() || 'Overlay';
      tabs.querySelector('.ov-tab.active').textContent = p.name;
      this.save();
    });
    const url = this.url(p);
    root.append(h('div', { class: 'card' },
      h('div', { class: 'card-head' }, h('h3', {}, 'Deine Overlays'), h('span', { class: 'spacer muted small' }, 'jedes mit eigener Adresse und eigenem Aussehen')),
      tabs,
      h('div', { class: 'ov-profile' },
        h('div', { class: 'field' }, h('label', {}, 'Name'), name),
        h('div', { class: 'field' }, h('label', {}, 'Adresse für OBS'),
          h('div', { class: 'url-box' }, h('input', { class: 'input', readonly: true, value: url }),
            h('button', { class: 'btn', onclick: () => api.call('copy', { text: url }).then(() => toast('ok', 'Adresse kopiert')) }, icon('copy'), 'Kopieren'),
            h('button', { class: 'btn ghost', title: 'Im Browser öffnen', onclick: () => api.call('openUrl', { url }) }, icon('external')))),
        h('div', { class: 'row' },
          h('button', { class: 'btn', onclick: () => this.duplicate() }, icon('copy'), 'Duplizieren'),
          this.profiles.length > 1 && h('button', { class: 'btn ghost danger', onclick: () => this.remove() }, icon('trash'), 'Löschen'))),
      h('p', { class: 'small muted', style: { marginTop: '12px' } }, 'In OBS: Quellen → + → ', h('b', {}, 'Browser'), ' → Adresse einfügen. Was du hier einstellst, übernimmt OBS nach einer Sekunde von selbst – die Adresse bleibt gleich. Alte Adressen mit „?scope=…“ laufen weiter, übernehmen aber keine Änderungen von hier.')));

    // Live preview – stays in sight while the settings below scroll.
    this.refs.inner = h('div', { class: 'ov-stage-inner' });
    this.refs.stage = h('div', { class: 'ov-stage ' + this.stageBg }, this.refs.inner);
    this.refs.size = h('div', { class: 'ov-size' });
    const stageChoice = segmented([['checker', 'Karo'], ['scene', 'Spielszene'], ['dark', 'Dunkel'], ['light', 'Hell']], this.stageBg, (v) => {
      this.stageBg = v;
      store.set('overlayStage', v);
      this.refs.stage.className = 'ov-stage ' + v;
    });
    root.append(h('div', { class: 'card ov-preview' },
      h('div', { class: 'card-head' }, h('h3', {}, 'Vorschau'), h('span', { class: 'muted small' }, 'mit deinen echten Werten'), h('span', { class: 'spacer' }), stageChoice),
      this.refs.stage, this.refs.size));

    root.append(h('div', { class: 'card' },
      h('div', { class: 'card-head' }, h('h3', {}, 'Vorlage'), h('span', { class: 'spacer muted small' }, 'setzt Farben, Schrift und Rahmen – Werte und Anordnung bleiben')),
      this.presetTiles()));
    // Two independent columns, so the long list of values leaves no gap next to it.
    root.append(h('div', { class: 'ov-grid' },
      h('div', { class: 'ov-col' }, this.valuesCard(p), this.fontCard(p)),
      h('div', { class: 'ov-col' }, this.layoutCard(p), this.colorsCard(p))));

    if (!this.resizeHooked) {
      this.resizeHooked = true;
      window.addEventListener('resize', () => { if (state.view === 'overlay') this.fit(); });
    }
    requestAnimationFrame(() => {
      this.renderPreview();
      this.renderPresets();
    });
    return root;
  },

  valuesCard(p) {
    const scope = selectInput(p.scope, OVERLAY_SCOPES, (v) => this.change((x) => { x.scope = v; }, 'all'));
    const sums = p.scope === 'today' || p.scope === 'all'
      ? selectInput(p.values, [['sum', 'Summe'], ['avg', 'Ø pro Match']], (v) => this.change((x) => { x.values = v; }, 'content'))
      : null;
    const title = p.titleMode === 'custom' ? h('input', { class: 'input grow', value: p.title, maxlength: '60', placeholder: OV().SCOPES[p.scope] }) : null;
    if (title) title.addEventListener('input', () => this.change((x) => { x.title = title.value; }, 'content'));
    this.refs.items = h('div', { class: 'ov-items' }, this.listItems(p).map((e) => this.itemRow(e)));
    this.sortable(this.refs.items);
    return h('div', { class: 'card' },
      h('div', { class: 'card-head' }, h('h3', {}, 'Werte & Reihenfolge')),
      h('div', { class: 'ov-form' },
        h('div', { class: 'row' },
          h('div', { class: 'field grow' }, h('label', {}, 'Zeitraum'), scope),
          sums && h('div', { class: 'field grow' }, h('label', {}, 'Zahlen als'), sums)),
        h('div', { class: 'field' }, h('label', {}, 'Titel'),
          h('div', { class: 'row' }, segmented([['auto', 'Automatisch'], ['custom', 'Eigener'], ['none', 'Keiner']], p.titleMode, (v) => this.change((x) => { x.titleMode = v; }, 'all')), title)),
        h('div', { class: 'field' }, h('label', {}, 'Werte'), this.refs.items,
          h('p', { class: 'small faint', style: { marginTop: '4px' } }, 'Am Griff ziehen zum Sortieren · Haken = wird gezeigt · Namen anklicken zum Umbenennen'))));
  },

  layoutCard(p) {
    const line = p.layout === 'line';
    return h('div', { class: 'card' },
      h('div', { class: 'card-head' }, h('h3', {}, 'Anordnung')),
      h('div', { class: 'ov-form' },
        h('div', { class: 'field' }, h('label', {}, 'Darstellung'),
          segmented([['bar', 'Leiste'], ['card', 'Karte'], ['grid', 'Raster'], ['line', 'Eine Zeile']], p.layout, (v) => this.change((x) => { x.layout = v; }, 'all'))),
        p.layout === 'grid' && h('div', { class: 'field' }, h('label', {}, 'Spalten'),
          rangeRow(1, 6, 1, p.columns, (v) => String(v), (v) => this.change((x) => { x.columns = v; }))),
        p.layout !== 'card' && p.layout !== 'line' && h('div', { class: 'field' }, h('label', {}, 'Ausrichtung'),
          segmented([['left', 'Links'], ['center', 'Mitte'], ['right', 'Rechts']], p.align, (v) => this.change((x) => { x.align = v; }))),
        p.layout !== 'card' && h('div', { class: 'field' }, h('label', {}, 'Beschriftung'),
          segmented(line ? [['top', 'Vor dem Wert'], ['bottom', 'Nach dem Wert']] : [['top', 'Über dem Wert'], ['bottom', 'Unter dem Wert']], p.labelPos, (v) => this.change((x) => { x.labelPos = v; }))),
        h('div', { class: 'field' }, h('label', {}, 'Größe'),
          rangeRow(40, 300, 5, Math.round(p.scale * 100), (v) => v + ' %', (v) => this.change((x) => { x.scale = v / 100; }))),
        h('div', { class: 'field' }, h('label', {}, 'Abstände'),
          rangeRow(30, 300, 5, Math.round(p.gap * 100), (v) => v + ' %', (v) => this.change((x) => { x.gap = v / 100; }))),
        h('div', { class: 'toggles' },
          !line && checkRow('Unterzeilen zeigen, z. B. „Ø 2,4“', p.showSub, (v) => this.change((x) => { x.showSub = v; })),
          checkRow('Kurz aufleuchten, wenn sich ein Wert ändert', p.animate, (v) => this.change((x) => { x.animate = v; })))));
  },

  colorsCard(p) {
    return h('div', { class: 'card' },
      h('div', { class: 'card-head' }, h('h3', {}, 'Farben & Hintergrund')),
      h('div', { class: 'ov-form' },
        h('div', { class: 'field' }, h('label', {}, 'Hintergrund – Deckkraft 0 % ist ganz durchsichtig'),
          h('div', { class: 'row' },
            h('input', { type: 'color', value: p.bgColor, oninput: (e) => this.change((x) => { x.bgColor = e.target.value; }) }),
            h('div', { class: 'grow' }, rangeRow(0, 100, 1, Math.round(p.bgOpacity * 100), (v) => v + ' %', (v) => this.change((x) => { x.bgOpacity = v / 100; }))))),
        h('div', { class: 'field' }, h('label', {}, 'Bild im Hintergrund'),
          picturePicker(p.bgImage || 'none', [['none', 'Kein Bild']], (id) => this.change((x) => { x.bgImage = id === 'none' ? '' : id; }, 'all')),
          p.bgImage && h('div', { style: { marginTop: '8px' } }, rangeRow(0, 100, 1, Math.round(p.bgImageOpacity * 100), (v) => t('Bild {n} %', { n: v }), (v) => this.change((x) => { x.bgImageOpacity = v / 100; })))),
        h('div', { class: 'field' }, h('label', {}, 'Farben'),
          h('div', { class: 'colors' },
            colorField('Werte', p.text, (v) => this.change((x) => { x.text = v; })),
            colorField('Beschriftung', p.label, (v) => this.change((x) => { x.label = v; })),
            colorField('Akzent', p.accent, (v) => this.change((x) => { x.accent = v; })),
            colorField('Extracted', p.ok, (v) => this.change((x) => { x.ok = v; })),
            colorField('Died', p.bad, (v) => this.change((x) => { x.bad = v; })))),
        h('div', { class: 'toggles' },
          checkRow('Extracted und Died farbig zeigen', p.tone, (v) => this.change((x) => { x.tone = v; })))));
  },

  fontCard(p) {
    const fonts = Object.entries(OV().FONTS).map(([k, f]) => [k, f.name]);
    return h('div', { class: 'card' },
      h('div', { class: 'card-head' }, h('h3', {}, 'Schrift & Rahmen')),
      h('div', { class: 'ov-form' },
        h('div', { class: 'field' }, h('label', {}, 'Schrift'), segmented(fonts, p.font, (v) => this.change((x) => { x.font = v; }), 'font-choice')),
        h('div', { class: 'field' }, h('label', {}, 'Rahmen'),
          segmented([['none', 'Keiner'], ['top', 'Linie oben'], ['left', 'Linie links'], ['frame', 'Rahmen'], ['double', 'Doppelt']], p.border, (v) => this.change((x) => { x.border = v; }))),
        h('div', { class: 'field' }, h('label', {}, 'Ecken'),
          rangeRow(0, 30, 1, p.radius, (v) => v + ' px', (v) => this.change((x) => { x.radius = v; }))),
        h('div', { class: 'toggles' },
          checkRow('Schatten um die Box', p.shadow, (v) => this.change((x) => { x.shadow = v; })),
          checkRow('Schriftschatten – gut lesbar auch ohne Hintergrund', p.textShadow, (v) => this.change((x) => { x.textShadow = v; })))));
  },
};

/* ================================================================= design */

// Designs of the program – colours and lettering live in themes.css. font: the lettering that
// comes with it; backdrop: its picture from the Hunt fan kit.
const THEMES = [
  { id: 'hunt', name: 'Hunt 1896', text: 'Knochenweiß und Messing auf Schwarz, Schrift wie im Spiel.', font: 'serif', backdrop: 'jaeger' },
  { id: 'blutmond', name: 'Blutmond', text: 'Glut und tiefes Rot für lange Nächte.', font: 'serif', backdrop: 'duell' },
  { id: 'bayou', name: 'Bayou', text: 'Moos, Messing und Sumpfnebel.', font: 'serif', backdrop: 'bayou' },
  { id: 'nacht', name: 'Mitternacht', text: 'Kühles Mondlicht, klare Schrift.', font: 'modern', backdrop: 'nacht' },
  { id: 'pergament', name: 'Pergament', text: 'Hell wie ein altes Kontobuch.', font: 'serif', backdrop: 'pergament' },
  { id: 'modern', name: 'Modern', text: 'Das schlichte Design der ersten Version.', font: 'modern', backdrop: 'none' },
];
const UI_FONTS = [['auto', 'Wie Design'], ['serif', 'Hunt'], ['modern', 'Modern'], ['western', 'Western']];

const themeOf = (s) => THEMES.find((theme) => theme.id === s.theme) || THEMES[0];

// Dark or light text on a custom accent colour.
function textOn(hex) {
  const n = parseInt(hex.slice(1), 16);
  const lum = (0.299 * ((n >> 16) & 255) + 0.587 * ((n >> 8) & 255) + 0.114 * (n & 255)) / 255;
  return lum > 0.55 ? '#16110a' : '#fbf6ec';
}

// Puts the design of settings s on the page. Also remembered for the next start (index.html).
function applyLook(s) {
  const theme = themeOf(s);
  const font = s.uiFont && s.uiFont !== 'auto' ? s.uiFont : theme.font;
  const pic = s.backdrop === 'auto' ? theme.backdrop : s.backdrop;
  const image = OV().IMAGES.some((i) => i.id === pic) ? pic : '';
  const strength = image ? clamp(Number(s.backdropStrength) || 0, 0, 100) / 100 : 0;
  const onAccent = s.accent ? textOn(s.accent) : '';
  const root = document.documentElement;
  root.dataset.theme = theme.id;
  root.dataset.font = font;
  if (s.accent) root.style.setProperty('--gold', s.accent);
  else root.style.removeProperty('--gold');
  if (onAccent) root.style.setProperty('--on-accent', onAccent);
  else root.style.removeProperty('--on-accent');
  root.style.setProperty('--bg-image', image ? 'url("/img/' + image + '.jpg")' : 'none');
  root.style.setProperty('--bg-strength', String(strength));
  store.set('look', { theme: theme.id, font, accent: s.accent || '', onAccent, image, strength });
  fitBrand();
  document.fonts.ready.then(fitBrand);
  requestAnimationFrame(reportChrome);
}

// The program name and the line below it in the sidebar get smaller until they fit: the
// designs use fonts of different widths, and a long name must not run into the edge.
function fitBrand() {
  for (const [selector, min] of [['.brand-name', 12], ['.brand-sub', 9]]) {
    const el = document.querySelector(selector);
    if (!el) continue;
    el.style.fontSize = '';
    let size = parseFloat(getComputedStyle(el).fontSize);
    while (el.scrollWidth > el.clientWidth && size > min) {
      size -= 0.5;
      el.style.fontSize = size + 'px';
    }
  }
}

// Title bar, window background and the countdown in the game take the colours of the design.
let chromeSent = '';
function reportChrome() {
  const probe = h('span', { style: { display: 'none' } });
  document.body.append(probe);
  const hex = (expr) => {
    probe.style.color = '';
    probe.style.color = expr;
    const m = getComputedStyle(probe).color.match(/\d+(\.\d+)?/g);
    return m ? m.slice(0, 3).map((v) => Math.round(Number(v)).toString(16).padStart(2, '0')).join('') : '000000';
  };
  const colors = ['var(--bg)', 'var(--panel-2)', 'var(--text)', 'var(--gold)', 'var(--muted)', 'var(--panel-3)'].map(hex);
  probe.remove();
  const dark = getComputedStyle(document.documentElement).colorScheme !== 'light';
  const chrome = colors.join(',') + ',' + (dark ? 1 : 0);
  if (chrome === chromeSent) return;
  chromeSent = chrome;
  api.call('setChrome', { chrome }).catch(() => {});
}

// The accent colour a design brings along (for the colour field while none of your own is set).
function themeAccent(themeId) {
  const probe = h('div', { dataset: { theme: themeId }, style: { display: 'none' } });
  document.body.append(probe);
  const value = getComputedStyle(probe).getPropertyValue('--gold').trim();
  probe.remove();
  return /^#[0-9a-f]{6}$/i.test(value) ? value : '#d4b06a';
}

async function saveLook(partial) {
  applyLook(Object.assign({}, state.settings, partial));
  if (await saveSettings(partial)) {
    applyLook(state.settings);
    if (state.view === 'settings') rerender();
  }
}

function designCard() {
  const s = state.settings;
  const tiles = h('div', { class: 'themes' }, THEMES.map((theme) => h('button', {
    class: 'theme-tile' + (themeOf(s).id === theme.id ? ' active' : ''),
    dataset: { theme: theme.id, font: theme.font },
    onclick: () => saveLook({ theme: theme.id }),
  },
  h('div', { class: 'theme-mock', style: { backgroundImage: theme.backdrop !== 'none' ? 'url("/img/thumbs/' + theme.backdrop + '.jpg")' : 'none' } },
    h('div', { class: 'mock-side' }, h('span'), h('span'), h('span'), h('span')),
    h('div', { class: 'mock-main' },
      h('div', { class: 'mock-card' }, h('span', { class: 'mock-label' }, 'Kills heute'), h('span', { class: 'mock-num' }, '12')),
      h('div', { class: 'mock-card' }, h('span', { class: 'mock-label' }, 'Bounty'), h('span', { class: 'mock-num' }, fmt.int(1250))),
      h('div', { class: 'mock-bar' }))),
  h('div', { class: 'theme-info' }, h('div', { class: 'theme-name' }, theme.name), h('div', { class: 'theme-text' }, theme.text)))));

  const accent = h('input', { type: 'color', value: s.accent || themeAccent(themeOf(s).id) });
  accent.addEventListener('input', () => applyLook(Object.assign({}, state.settings, { accent: accent.value })));
  accent.addEventListener('change', () => saveLook({ accent: accent.value }));

  const row = (title, text, control) => h('div', { class: 'design-row' }, h('div', { class: 'text' }, h('b', {}, title), text && h('span', {}, text)), control);
  const hasPicture = (s.backdrop === 'auto' ? themeOf(s).backdrop : s.backdrop) !== 'none';

  return h('div', { class: 'card' },
    h('div', { class: 'card-head' }, h('h3', {}, 'Design'), h('span', { class: 'spacer muted small' }, 'gilt sofort, auch für das Countdown-Fenster im Spiel')),
    tiles,
    h('div', { class: 'design-rows' },
      row('Hintergrundbild', 'Motive aus dem offiziellen Hunt-Fankit.',
        picturePicker(s.backdrop, [['auto', 'Wie Design'], ['none', 'Kein Bild']], (id) => saveLook({ backdrop: id }))),
      hasPicture && row('Stärke des Bildes', 'Wie deutlich das Bild hinter den Karten zu sehen ist.',
        rangeRow(0, 100, 5, s.backdropStrength, (v) => v + ' %',
          (v) => applyLook(Object.assign({}, state.settings, { backdropStrength: v })),
          (v) => saveLook({ backdropStrength: v }))),
      row('Schrift', 'Hunt: Garamond und Crimson Text wie im Spiel und auf huntshowdown.com.',
        segmented(UI_FONTS, s.uiFont, (v) => saveLook({ uiFont: v }), 'font-choice')),
      row('Akzentfarbe', 'Knöpfe, Markierungen und Hervorhebungen.',
        h('div', { class: 'row' }, accent,
          h('button', { class: 'btn small ghost', disabled: !s.accent, onclick: () => saveLook({ accent: '' }) }, 'Wie Design')))));
}

/* ================================================================= settings */

function settingRow(title, description, control) {
  return h('div', { class: 'setting' }, h('div', { class: 'text' }, h('b', {}, title), description && h('span', {}, description)), h('div', { class: 'control' }, control));
}

function toggle(checked, onChange) {
  const input = h('input', { type: 'checkbox', checked });
  input.addEventListener('change', () => onChange(input.checked));
  return h('label', { class: 'switch' }, input, h('span', { class: 'track' }));
}

function numberInput(value, min, max, onChange) {
  const input = h('input', { class: 'input', type: 'number', min: String(min), max: String(max), value: String(value) });
  input.addEventListener('change', () => {
    const v = clamp(Math.round(Number(input.value)), min, max);
    input.value = String(v);
    onChange(v);
  });
  return input;
}

const hotkeyRecorder = { active: false };

function renderSettings() {
  const s = state.settings;
  const st = state.status || {};
  const app = state.app;
  const root = h('div', { class: 'stack' });
  root.append(designCard());

  const hotkeyKbd = h('span', { class: 'kbd' }, s.hotkey || '—');
  const hotkeyButton = h('button', { class: 'btn small' }, 'Ändern');
  const hotkeyError = h('div', { class: 'error-text' }, st.hotkeyError || '');
  hotkeyButton.addEventListener('click', () => {
    if (hotkeyRecorder.active) return;
    hotkeyRecorder.active = true;
    hotkeyKbd.classList.add('recording');
    hotkeyKbd.textContent = t('Tastenkombination drücken …');
    const onKey = async (e) => {
      e.preventDefault();
      e.stopPropagation();
      if (['Control', 'Alt', 'Shift', 'Meta'].includes(e.key)) return;
      window.removeEventListener('keydown', onKey, true);
      hotkeyRecorder.active = false;
      hotkeyKbd.classList.remove('recording');
      if (e.key === 'Escape') {
        hotkeyKbd.textContent = state.settings.hotkey;
        return;
      }
      let key = null;
      if (/^Key[A-Z]$/.test(e.code)) key = e.code.slice(3);
      else if (/^Digit[0-9]$/.test(e.code)) key = e.code.slice(5);
      else if (/^F([1-9]|1[0-9]|2[0-4])$/.test(e.code)) key = e.code;
      else if (/^Numpad[0-9]$/.test(e.code)) key = 'Num' + e.code.slice(6);
      const mods = [e.ctrlKey && 'Ctrl', e.altKey && 'Alt', e.shiftKey && 'Shift'].filter(Boolean);
      if (!key || (!mods.length && !/^F\d+$/.test(key))) {
        toast('warn', 'Bitte eine Kombination mit Strg/Alt/Umschalt oder eine F-Taste wählen.');
        hotkeyKbd.textContent = state.settings.hotkey;
        return;
      }
      const combo = mods.concat(key).join('+');
      hotkeyKbd.textContent = combo;
      await saveSettings({ hotkey: combo });
    };
    window.addEventListener('keydown', onKey, true);
  });

  root.append(h('div', { class: 'card' }, h('div', { class: 'card-head' }, h('h3', {}, 'Erkennung')),
    h('div', { class: 'settings-list' },
      settingRow('Automatisch erkennen', 'Liest die Mission Summary selbstständig aus, sobald sie erscheint.', toggle(s.autoDetect, (v) => saveSettings({ autoDetect: v }))),
      settingRow('Prüfungen pro Sekunde', 'Wie oft das Bild geprüft wird. 4 reicht; weniger schont den PC und OBS beim Streamen.', numberInput(s.detectFps, 1, 10, (v) => saveSettings({ detectFps: v }))),
      settingRow('Screenshots speichern', 'Hebt zu jedem Match ein Bild der Mission Summary und der Team Details auf – praktisch zum Nachprüfen –, dazu bei der Laufspur die Karte zu jedem Punkt.', toggle(s.saveScreenshots, (v) => saveSettings({ saveScreenshots: v }))),
      settingRow('Screenshot-Breite', 'Pixelbreite der gespeicherten Screenshots.', numberInput(s.screenshotWidth, 640, 3840, (v) => saveSettings({ screenshotWidth: v }))),
      settingRow('Tastenkürzel „Jetzt auslesen“', 'Funktioniert auch, während Hunt im Vordergrund ist – falls die Erkennung mal ein Match verpasst.',
        h('div', { style: { display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '4px' } }, h('div', { class: 'row' }, hotkeyKbd, hotkeyButton), hotkeyError)))));

  const playerName = h('input', { class: 'input', style: { width: '220px' }, value: s.playerName || '', placeholder: 'Dein Steam-Name', maxlength: '40' });
  playerName.addEventListener('change', () => {
    if ((state.settings.playerName || '') !== playerName.value.trim()) saveSettings({ playerName: playerName.value.trim() });
  });
  // The Steam name of the account signed in on this PC – the name Hunt shows.
  const steamName = (state.app && state.app.steamName) || '';
  const nameControl = steamName && steamName !== (s.playerName || '')
    ? h('div', { class: 'row', style: { gap: '8px', justifyContent: 'flex-end' } }, playerName,
      h('button', { class: 'btn small', title: steamName, onclick: () => {
        playerName.value = steamName;
        saveSettings({ playerName: steamName });
      } }, 'Steam-Namen übernehmen'))
    : playerName;

  root.append(h('div', { class: 'card' }, h('div', { class: 'card-head' }, h('h3', {}, 'Programm')),
    h('div', { class: 'settings-list' },
      settingRow('Sprache', 'Sprache der Oberfläche, des Countdowns im Spiel und des Overlays.', languageChoice()),
      settingRow('Tagesbeginn', 'Ab dieser Uhrzeit zählt „Heute“ neu – so landet eine Session nach Mitternacht noch beim Vortag.',
        h('div', { class: 'row' }, numberInput(s.dayStartHour, 0, 12, (v) => saveSettings({ dayStartHour: v })), h('span', { class: 'muted small' }, 'Uhr'))),
      settingRow('Signalton', 'Kurzer Ton, wenn ein Match gespeichert wurde.',
        h('div', { class: 'row' }, h('button', { class: 'btn small ghost', onclick: () => api.call('testSound') }, icon('play'), 'Test'), toggle(s.sound, (v) => saveSettings({ sound: v })))),
      settingRow('In den Infobereich minimieren', 'Beim Minimieren verschwindet das Fenster neben die Uhr und läuft dort weiter.', toggle(s.minimizeToTray, (v) => saveSettings({ minimizeToTray: v }))),
      settingRow('Dein Name in Hunt', 'Damit findet HuntXtract in den Team Details deine Zeile und übernimmt deine MMR-Sterne.', nameControl),
      settingRow('Einführung', 'Die kurze Tour durch das Programm, die beim ersten Start angeboten wird.',
        h('button', { class: 'btn small', onclick: () => tour.start() }, icon('play'), 'Tour starten')))));

  const position = h('select', { class: 'input', style: { width: '150px' } },
    [['top-center', 'Oben Mitte'], ['top-left', 'Oben links'], ['top-right', 'Oben rechts'], ['bottom-center', 'Unten Mitte'], ['bottom-right', 'Unten rechts']]
      .map(([v, label]) => h('option', { value: v, selected: v === s.countdownPosition }, label)));
  position.addEventListener('change', () => saveSettings({ countdownPosition: position.value }));
  const preview = async () => {
    try {
      await api.call('previewCountdown');
    } catch (e) {
      toast('error', e.message);
    }
  };
  root.append(h('div', { class: 'card' }, h('div', { class: 'card-head' }, h('h3', {}, 'Anzeige im Spiel')),
    h('div', { class: 'settings-list' },
      settingRow('Countdown im Spiel einblenden', 'Sobald die Mission Summary erkannt ist, zählt ein kleines Fenster über dem Spiel bis zum Auslesen herunter und bestätigt danach das gespeicherte Match. Man kann durch das Fenster hindurchklicken, und es erscheint nicht im Stream.',
        toggle(s.countdownInGame, (v) => saveSettings({ countdownInGame: v }))),
      settingRow('Position', 'Auf dem Bildschirm, auf dem das Spiel läuft.',
        h('div', { class: 'row' }, position, h('button', { class: 'btn small', onclick: preview }, icon('play'), 'Vorschau'))))));

  root.append(h('div', { class: 'card' }, h('div', { class: 'card-head' }, h('h3', {}, 'Overlay-Server')),
    h('div', { class: 'settings-list' },
      settingRow('Overlay bereitstellen', 'Stellt die Werte unter einer lokalen Adresse für eine OBS-Browserquelle bereit.', toggle(s.overlayEnabled, (v) => saveSettings({ overlayEnabled: v }))),
      settingRow('Port', st.overlayError ? h('span', { class: 'error-text' }, st.overlayError) : t('Adresse: {url}', { url: s.overlayUrl }),
        numberInput(s.overlayPort, 1024, 65535, (v) => saveSettings({ overlayPort: v }))))));

  // Tidying up: which matches stay, and what of the others goes. Before anything is deleted right
  // away, HuntXtract says how much and asks.
  const option = (value, label, current) => h('option', { value, selected: value === current }, label);
  const keepSelect = h('select', { class: 'input', style: { width: '220px' } },
    [['all', 'Alle behalten'], ['30d', 'Die letzten 30 Tage'], ['90d', 'Die letzten 90 Tage'], ['365d', 'Das letzte Jahr'],
      ['100', 'Die letzten 100 Matches'], ['250', 'Die letzten 250 Matches'], ['500', 'Die letzten 500 Matches'], ['1000', 'Die letzten 1000 Matches']]
      .map(([value, label]) => option(value, label, s.keepMatches || 'all')));
  const whatSelect = h('select', { class: 'input', style: { width: '220px' } },
    [['pictures', 'Nur die Bilder'], ['matches', 'Das ganze Match']].map(([value, label]) => option(value, label, s.keepWhat || 'pictures')));
  const applyKeep = async () => {
    const keep = keepSelect.value, what = whatSelect.value;
    try {
      const preview = await api.call('cleanupPreview', { keep, what });
      if (preview.count > 0) {
        const text = what === 'matches'
          ? t('Damit werden jetzt {n} ältere Matches gelöscht – Werte, Laufspur und Bilder ({mb} MB). Das lässt sich nicht rückgängig machen.', { n: fmt.int(preview.count), mb: fmt.dec(preview.megabytes) })
          : t('Damit werden jetzt die Bilder von {n} älteren Matches gelöscht ({mb} MB). Werte und Statistik bleiben.', { n: fmt.int(preview.count), mb: fmt.dec(preview.megabytes) });
        if (!(await confirmDialog('Alte Matches aufräumen', text, 'Aufräumen', true))) {
          keepSelect.value = s.keepMatches || 'all';
          whatSelect.value = s.keepWhat || 'pictures';
          return;
        }
      }
      await saveSettings({ keepMatches: keep, keepWhat: what });
    } catch (e) {
      toast('error', e.message);
    }
  };
  keepSelect.addEventListener('change', applyKeep);
  whatSelect.addEventListener('change', applyKeep);
  const usage = h('span', {}, '…');
  api.call('storageInfo').then((info) => {
    usage.textContent = t('{mb} MB für {n} Matches – ein Ordner pro Tag, darin einer pro Match mit Bildern und stats.txt.', { mb: fmt.dec(info.megabytes), n: fmt.int(info.matches) });
  }).catch(() => {});

  root.append(h('div', { class: 'card' }, h('div', { class: 'card-head' }, h('h3', {}, 'Daten')),
    h('div', { class: 'settings-list' },
      settingRow('Datenordner', app.dataDir, h('div', { class: 'row' },
        h('button', { class: 'btn small', onclick: () => api.call('openDataFolder') }, icon('folder'), 'Öffnen'),
        h('button', { class: 'btn small ghost', onclick: () => api.call('openLog') }, 'Protokoll'))),
      app.dataDirNote && h('div', { class: 'hint', style: { margin: '10px 0' } }, icon('info'), app.dataDirNote),
      settingRow('Matches', usage, h('button', { class: 'btn small', onclick: () => api.call('openMatchesFolder') }, icon('folder'), 'Öffnen')),
      settingRow('Matches aufbewahren', 'Ältere Matches räumt HuntXtract von selbst auf – beim Start und nach jedem Match.', keepSelect),
      settingRow('Beim Aufräumen löschen', 'Nur die Bilder: Werte und Statistik bleiben. Das ganze Match: auch Werte und Laufspur.', whatSelect),
      settingRow('Sicherung', 'HuntXtract legt jeden Tag automatisch eine Kopie deiner Matches im Unterordner „sicherungen“ an (die letzten 14 Tage).', null),
      settingRow('Texterkennung', app.ocrError ? h('span', { class: 'error-text' }, app.ocrError)
        : t('Windows-Texterkennung ({lang}) – läuft komplett auf deinem PC.', { lang: app.ocrLanguage }), null))));

  root.append(h('div', { class: 'card about' },
    h('div', { class: 'about-head' },
      h('div', {},
        h('div', { class: 'about-name' }, app.name + ' ', h('span', { class: 'muted' }, app.version)),
        h('div', { class: 'small muted' }, t('by {author} · Match-Tracker für Hunt: Showdown – kein Eingriff ins Spiel, alle Daten bleiben auf deinem PC.', { author: app.author }))),
      h('button', { class: 'btn', title: app.authorUrl, onclick: () => api.call('openUrl', { url: app.authorUrl }) }, icon('external'), 'Steam-Profil')),
    h('p', { class: 'small faint', style: { marginTop: '10px' } }, 'Fan-Projekt, nicht mit Crytek verbunden. Hintergrundbilder aus dem offiziellen Hunt-Fankit. Schriften: Crimson Text, EB Garamond und Rye unter der SIL Open Font License.')));
  return root;
}

/* ================================================================= language */

// Flag and name of each language; a click switches the whole program at once.
function languageChoice() {
  return h('div', { class: 'lang-choice', role: 'group' }, LANGUAGES.map((lang) => {
    const flag = h('span', { class: 'flag', 'aria-hidden': 'true' });
    flag.innerHTML = lang.flag;
    return h('button', {
      class: 'lang-btn' + (lang.id === I18N.lang ? ' active' : ''),
      lang: lang.id,
      onclick: () => setLanguage(lang.id),
    }, flag, h('span', {}, document.createTextNode(lang.name)));
  }));
}

// Puts the language on everything a view does not draw: the page texts marked with data-i18n,
// the overlay previews and the number formats. Remembered for the next start, so the page
// comes up in the right language before the settings have arrived.
function applyLanguage(lang) {
  I18N.lang = LANGUAGES.some((l) => l.id === lang) ? lang : 'en';
  document.documentElement.lang = I18N.lang;
  store.set('lang', I18N.lang);
  document.querySelectorAll('[data-i18n]').forEach((el) => { el.textContent = tr(el.dataset.i18n); });
  document.querySelectorAll('[data-i18n-title]').forEach((el) => { el.title = tr(el.dataset.i18nTitle); });
  if (OV()) OV().setLanguage(I18N.lang);
}

// Everything on screen once more in the current language.
function redrawTexts() {
  if (state.view) {
    document.getElementById('viewTitle').textContent = t(views[state.view].title);
    rerender();
  }
  renderStatus();
  live.draw();
  fitBrand();
  if (tour.active) tour.show();
}

// From the settings or the tour. The host follows with the countdown in the game, the tray menu
// and the overlay in OBS; texts it wrote before (status lines, hints) are fetched again.
async function setLanguage(lang) {
  if (lang === I18N.lang) return;
  const previous = I18N.lang;
  applyLanguage(lang);
  redrawTexts();
  if (!(await saveSettings({ language: I18N.lang }))) {
    applyLanguage(previous);
    redrawTexts();
    return;
  }
  try {
    const data = await api.call('init');
    state.app = data.app;
    state.stats = data.stats;
    state.matches = data.matches;
    state.status = data.status;
    // Default names of the Team Details fields follow the language – unless the setup is being edited.
    if (!setup.dirty) {
      state.calibration = data.calibration;
      setup.draft = null;
    }
    redrawTexts();
  } catch (e) { /* the texts of the host stay as they were until they change anyway */ }
}

/* ================================================================= guided tour */

// The setup card with the given step number.
const stepCard = (n) => [...document.querySelectorAll('.card.step')].find((c) => {
  const num = c.querySelector('.step-num');
  return num && num.textContent.trim() === String(n);
});

// First start: a short walk through the program. Only the part it talks about stays sharp,
// the rest is dimmed and blurred. "Ich brauche keine Hilfe" ends it for good.
// Steps marked setup explain drawing the frames; with the ready-made setup (16:9) the
// steps marked preset take their place.
const TOUR_STEPS = [
  { languages: true, title: 'Sprache · Language',
    text: 'Wähle die Sprache für HuntXtract. Du kannst sie später in den Einstellungen ändern.' },
  { setup: true, title: 'Willkommen bei HuntXtract',
    text: 'HuntXtract liest nach jedem Match deine Mission Summary aus und führt Buch über Kills, Bounty, Hunt Dollars, Extraction Rate und MMR. Ich zeige dir in ein paar kurzen Schritten, wie du es einrichtest.' },
  { preset: true, title: 'Willkommen bei HuntXtract',
    text: 'HuntXtract liest nach jedem Match deine Mission Summary aus und führt Buch über Kills, Bounty, Hunt Dollars, Extraction Rate und MMR. Eingerichtet ist schon alles – ich zeige dir in ein paar kurzen Schritten, wo du was findest.' },
  { input: 'playerName', title: 'Wie heißt du in Hunt?',
    text: 'Magst du schon deinen Steam-Namen eintragen, so wie er im Spiel steht? Damit findet HuntXtract in den Team Details deine eigene Zeile und schreibt deine MMR mit. Du kannst das auch überspringen und später in den Einstellungen nachholen.' },
  { setup: true, view: 'setup', target: () => document.querySelector('#nav button[data-view="setup"]'), title: 'Hier geht es los',
    text: 'In der Einrichtung zeigst du HuntXtract einmal, wie deine Mission Summary aussieht. Das dauert ein paar Minuten und muss nur einmal sein.' },
  { setup: true, view: 'setup', target: () => stepCard(1), title: 'Schritt 1 · Bildquelle',
    text: '„Bildschirm direkt“ ist schon gewählt – HuntXtract liest das Bild selbst vom Monitor, OBS brauchst du dafür nicht. Aufgenommen wird dabei nur Hunt, solange es im Vordergrund ist – sonst bleibt das Bild schwarz. Den Bildschirm, auf dem Hunt läuft, findet HuntXtract selbst. Hunt sollte im randlosen Fenster laufen.' },
  { setup: true, view: 'setup', target: () => stepCard(2), title: 'Schritt 2 · Mission Summary aufnehmen',
    text: 'Spiel ein Match und bleib auf der Mission Summary, bis alle Punkte vergeben sind. Dann „In 5 Sekunden aufnehmen“ – so bleibt Zeit, zurück ins Spiel zu wechseln.' },
  { setup: true, view: 'setup', target: () => stepCard(3), title: 'Schritt 3 · Felder markieren',
    text: 'Zieh auf dem Bild Rahmen: zuerst um die Überschrift als „Wiedererkennung“, dann um jede Zahl, die gezählt werden soll. „Beispiel ansehen“ zeigt dir, wo die Rahmen ungefähr hingehören. Bei Hunter Level und Hunter Progress „nur bei Extracted“ anhaken – daran erkennt HuntXtract, ob du rausgekommen bist.' },
  { setup: true, view: 'setup', target: () => stepCard(4), title: 'Schritt 4 · Team Details (freiwillig)',
    text: 'Wer auch MMR und Mitspieler mitschreiben will, nimmt hier zusätzlich den Reiter „Team Details“ auf und trägt seinen Hunt-Namen ein, damit HuntXtract die eigene Zeile findet.' },
  { setup: true, view: 'setup', target: () => stepCard(5), title: 'Schritt 5 · Karte (freiwillig)',
    text: 'Nimm hier die Karte auf, die Hunt auf Tab zeigt. Dann merkt sich HuntXtract bei jedem Blick auf die Karte, wo du bist, und zeichnet deine Laufspur zu jedem Match.' },
  { setup: true, view: 'setup', target: () => stepCard(6), title: 'Schritt 6 · Testen & speichern',
    text: '„Auslesen testen“ zeigt dir sofort, was erkannt wird. Passt alles: „Einrichtung speichern“, dann „Fertig – zur Übersicht“. Ab da zählt HuntXtract jedes Match von selbst.' },
  { preset: true, view: 'setup', target: () => document.querySelector('.preset-banner'), title: 'Schon eingerichtet',
    text: () => t('Hunt läuft bei dir in {res}. Dafür bringt HuntXtract eine fertige Einrichtung mit: Die Rahmen für Mission Summary, Team Details und Karte sind schon gesetzt – zeichnen musst du nichts.', { res: presetResolution() }) },
  { preset: true, view: 'setup', target: () => stepCard(1), title: 'Bildquelle',
    text: '„Bildschirm direkt“ ist schon gewählt – HuntXtract liest das Bild selbst vom Monitor, OBS brauchst du dafür nicht. Aufgenommen wird dabei nur Hunt, solange es im Vordergrund ist – sonst bleibt das Bild schwarz. Den Bildschirm, auf dem Hunt läuft, findet HuntXtract selbst.' },
  { preset: true, view: 'setup', target: () => stepCard(6), title: 'Testen & anpassen',
    text: '„Auslesen testen“ zeigt dir, was auf dem Beispielbild erkannt wird. Sitzt bei dir nach dem ersten Match ein Rahmen nicht, verschieb ihn und speichere – dann ist es deine eigene Einrichtung.' },
  { view: 'dashboard', target: () => document.querySelector('.pills'), title: 'Läuft alles?',
    text: 'Hier oben siehst du jederzeit, ob ein Bild ankommt und ob auf die Mission Summary gewartet wird. Wird sie erkannt, zählt ein kleines Fenster im Spiel bis zum Auslesen herunter.' },
  { view: 'dashboard', target: () => document.getElementById('btnExtract'), title: 'Match verpasst?',
    text: '„Jetzt auslesen“ – oder Strg+Alt+K mitten im Spiel – liest den Bildschirm sofort aus. Stehst du dabei auf „Team Details“, ergänzt es das letzte Match.' },
  { view: 'dashboard', target: () => document.getElementById('nav'), title: 'Das war’s',
    text: 'Übersicht, Matches, ein Overlay für deinen Stream und die Einstellungen findest du links. In den Einstellungen wählst du auch das Design und kannst diese Tour jederzeit wieder starten. Viel Erfolg da draußen, Hunter!' },
];

const tour = {
  index: 0,
  active: false,
  refs: null,
  timer: null,
  steps: [],

  start() {
    if (this.active) return;
    if (modal.isOpen) modal.close();
    this.active = true;
    this.index = 0;
    const preset = !!(state.calibration && state.calibration.preset);
    this.steps = TOUR_STEPS.filter((s) => (preset ? !s.setup : !s.preset));
    const root = document.getElementById('tour');
    root.hidden = false;
    this.refs = {
      shades: [0, 1, 2, 3].map(() => h('div', { class: 'tour-shade' })),
      ring: h('div', { class: 'tour-ring' }),
      card: h('div', { class: 'tour-card', role: 'dialog', 'aria-modal': 'true' }),
    };
    root.replaceChildren(...this.refs.shades, this.refs.ring, this.refs.card);
    this.onKey = (e) => {
      if (e.key === 'Escape') {
        this.stop(false);
        return;
      }
      // Typing into a field inside the highlighted part must not flip pages.
      if (e.target && /^(INPUT|SELECT|TEXTAREA)$/.test(e.target.tagName)) return;
      if (e.key === 'ArrowRight' || e.key === 'Enter') this.next();
      else if (e.key === 'ArrowLeft') this.back();
    };
    window.addEventListener('keydown', this.onKey, true);
    // Layout may change while the view settles (images, live data); follow the target.
    this.timer = setInterval(() => this.place(), 300);
    this.show();
  },

  // forever: finished or "Ich brauche keine Hilfe" – do not offer the tour again.
  stop(forever) {
    if (!this.active) return;
    this.active = false;
    clearInterval(this.timer);
    window.removeEventListener('keydown', this.onKey, true);
    const root = document.getElementById('tour');
    root.hidden = true;
    root.replaceChildren();
    if (forever && !state.settings.tourDone) saveSettings({ tourDone: true });
  },

  next() {
    if (this.index >= this.steps.length - 1) this.stop(true);
    else {
      this.index++;
      this.show();
    }
  },

  back() {
    if (this.index === 0) return;
    this.index--;
    this.show();
  },

  show() {
    const step = this.steps[this.index];
    if (step.view && state.view !== step.view) show(step.view);
    this.renderCard(step);
    // Let the view render, then bring the target into sight and put the light on it.
    requestAnimationFrame(() => {
      const target = step.target ? step.target() : null;
      if (target) {
        const tall = target.getBoundingClientRect().height > window.innerHeight * 0.6;
        target.scrollIntoView({ block: tall ? 'start' : 'center', behavior: 'auto' });
      }
      this.place();
    });
  },

  renderCard(step) {
    const last = this.index === this.steps.length - 1;
    const skip = h('input', { type: 'checkbox' });
    skip.addEventListener('change', () => { if (skip.checked) this.stop(true); });
    let primary = h('button', { class: 'btn primary small', onclick: () => this.next() }, last ? 'Los geht’s' : 'Weiter');
    let field = null;
    let later = null;
    // The step that asks for the name in Hunt: save it, or skip it.
    if (step.input === 'playerName') {
      const saved = state.settings.playerName || '';
      // Suggested from Steam: the name of the account signed in on this PC.
      const steam = (state.app && state.app.steamName) || '';
      const input = h('input', { class: 'input', value: saved || steam, placeholder: 'Dein Steam-Name', maxlength: '40', spellcheck: 'false' });
      const save = async () => {
        const name = input.value.trim();
        if (!name) return;
        if (name !== saved && !(await saveSettings({ playerName: name }, t('Name gespeichert: {name}', { name })))) return;
        this.next();
      };
      primary = h('button', { class: 'btn primary small', onclick: save });
      const sync = () => {
        const name = input.value.trim();
        primary.disabled = !name;
        primary.textContent = t(name && name === saved ? 'Weiter' : 'Speichern & weiter');
      };
      input.addEventListener('input', sync);
      input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
          e.preventDefault();
          save();
        }
      });
      sync();
      later = h('button', { class: 'btn ghost small', onclick: () => this.next() }, 'Überspringen');
      field = h('div', { class: 'tour-field' }, input,
        !!steam && (saved || steam) === steam && h('div', { class: 'small muted', style: { marginTop: '6px' } }, 'Von Steam übernommen'));
      requestAnimationFrame(() => input.focus());
    }
    // The very first step: the language, as flags with their names. It applies at once.
    if (step.languages) field = h('div', { class: 'tour-field' }, languageChoice());
    // Only real nodes: replaceChildren would print a missing field as the text "null".
    this.refs.card.replaceChildren(...[
      h('div', { class: 'tour-top' },
        h('span', { class: 'tour-step' }, t('Einführung · {n} von {total}', { n: this.index + 1, total: this.steps.length })),
        h('button', { class: 'icon-btn', title: 'Später (Esc)', onclick: () => this.stop(false) }, icon('x'))),
      h('h3', {}, step.title),
      h('p', {}, typeof step.text === 'function' ? step.text() : step.text),
      field,
      h('div', { class: 'tour-meta' },
        h('div', { class: 'tour-dots' }, this.steps.map((_, i) => h('span', { class: i === this.index ? 'on' : i < this.index ? 'seen' : '' }))),
        h('label', { class: 'tour-skip' }, skip, 'Ich brauche keine Hilfe')),
      h('div', { class: 'tour-foot' },
        this.index > 0 && h('button', { class: 'btn ghost small', onclick: () => this.back() }, 'Zurück'),
        h('span', { class: 'grow' }),
        later,
        primary),
    ].filter(Boolean));
  },

  // Four shades around the target leave a sharp hole. Without a target the hole is a point in
  // the middle of the window: everything is shaded, and the first spotlight opens from there
  // instead of the shades sweeping in from a corner.
  place() {
    if (!this.active) return;
    const step = this.steps[this.index];
    const target = step.target ? step.target() : null;
    const vw = window.innerWidth, vh = window.innerHeight;
    const [top, bottom, left, right] = this.refs.shades;
    const ring = this.refs.ring;
    const card = this.refs.card;
    const put = (el, x, y, w, hgt) => Object.assign(el.style, { left: x + 'px', top: y + 'px', width: Math.max(0, w) + 'px', height: Math.max(0, hgt) + 'px' });

    let x1 = Math.round(vw / 2), y1 = Math.round(vh / 2), x2 = x1, y2 = y1;
    if (target) {
      const pad = 8;
      const r = target.getBoundingClientRect();
      // Only the part that is on screen – tall cards reach beyond the window.
      x1 = Math.max(0, r.left - pad);
      y1 = Math.max(0, r.top - pad);
      x2 = Math.min(vw, r.right + pad);
      y2 = Math.min(vh, r.bottom + pad);
    }
    put(top, 0, 0, vw, y1);
    put(bottom, 0, y2, vw, vh - y2);
    put(left, 0, y1, x1, y2 - y1);
    put(right, x2, y1, vw - x2, y2 - y1);
    put(ring, x1, y1, x2 - x1, y2 - y1);
    ring.style.opacity = target ? '1' : '0';

    const cw = card.offsetWidth, ch = card.offsetHeight, gap = 14;
    if (!target) {
      Object.assign(card.style, { left: (vw - cw) / 2 + 'px', top: Math.max(16, (vh - ch) / 2) + 'px' });
      return;
    }

    // The card goes below the highlight, else above it, else into a free corner on top of it.
    let cx = Math.min(Math.max(16, x1), vw - cw - 16);
    let cy;
    if (y2 + gap + ch <= vh - 16) cy = y2 + gap;
    else if (y1 - gap - ch >= 16) cy = y1 - gap - ch;
    else if (x2 + gap + cw <= vw - 16) { cx = x2 + gap; cy = Math.min(Math.max(16, y1), vh - ch - 16); }
    else if (x1 - gap - cw >= 16) { cx = x1 - gap - cw; cy = Math.min(Math.max(16, y1), vh - ch - 16); }
    else { cx = vw - cw - 24; cy = vh - ch - 24; }
    Object.assign(card.style, { left: cx + 'px', top: cy + 'px' });
  },
};

/* ================================================================= boot */

async function boot() {
  // The language of the last start right away, the setting follows with the init data. Before
  // there was a choice everything was German – a remembered design gives such a user away.
  applyLanguage(store.get('lang', store.get('look', null) ? 'de' : 'en'));
  document.querySelectorAll('#nav button').forEach((b) => b.addEventListener('click', () => show(b.dataset.view)));
  document.getElementById('btnExtract').addEventListener('click', async () => {
    try {
      await api.call('extractNow');
      toast('info', 'Lese den aktuellen Bildschirm aus …');
    } catch (e) {
      toast('error', e.message);
    }
  });
  document.getElementById('modal').addEventListener('mousedown', (e) => {
    if (e.target.id === 'modal') modal.close();
  });
  window.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && modal.isOpen && !hotkeyRecorder.active) modal.close();
  });

  // The search and lobby clock at the top runs on by the second.
  setInterval(() => {
    const hunt = state.status && state.status.hunt;
    if (hunt && (hunt.phase === 'Searching' || hunt.phase === 'Found')) renderStatus();
  }, 1000);
  api.on('status', (st) => {
    state.status = st;
    renderStatus();
    liveTrail.sync(st);
    if (state.view === 'setup') {
      setup.onStatus();
      if (st.similarity !== null && st.similarity !== undefined) setup.updateMeter(st.similarity);
    }
  });
  api.on('preview', (data) => {
    if (state.view === 'setup') setup.onPreview(data);
  });
  api.on('data', (data) => {
    state.stats = data.stats;
    if (data.matches) state.matches = data.matches;
    if (modal.isOpen) state.staleView = true;
    else refreshAfterData();
  });
  api.on('matchSaved', (data) => {
    const m = data.match;
    state.freshUntil = Date.now() + 2500;
    const parts = [];
    const fields = state.stats ? state.stats.fields : [];
    // Outcome derived from the "nur bei Extracted" fields – the loop below only sees a status field.
    if (!statusField() && m.outcome) parts.push(m.outcome === 'extrahiert' ? 'Extracted' : 'Died');
    for (const f of fields) {
      const v = m.values[f.id];
      if (v === null || v === undefined || v === '') continue;
      parts.push(f.type === 'status' ? (v === 'extrahiert' ? 'Extracted' : v === 'gestorben' ? 'Died' : '') : tr(f.label) + ' ' + (isNumberType(f.type) ? fmt.int(v) : v));
    }
    // The same summary read again, now complete: the missing values were added to the match.
    toast('ok', t(data.completed ? 'Match #{id} ergänzt' : 'Match #{id} gespeichert', { id: m.id }) + (parts.length ? ': ' + parts.filter(Boolean).join(' · ') : ''), 7000);
  });
  api.on('notice', (n) => toast(n.kind === 'warn' ? 'warn' : n.kind === 'error' ? 'error' : n.kind === 'ok' ? 'ok' : 'info', n.text));

  try {
    const data = await api.call('init');
    Object.assign(state, data);
    applyLanguage(state.settings.language);
    applyLook(state.settings);
    document.fonts.addEventListener('loadingdone', fitBrand);
    document.getElementById('sidebarFoot').textContent = 'Version ' + data.app.version;
    const author = document.getElementById('brandAuthor');
    author.textContent = 'by ' + data.app.author;
    author.href = data.app.authorUrl;
    renderStatus();
    // A match that is already running shows up with the first drawing of the overview.
    await liveTrail.sync(state.status);
    show(data.calibration.ready ? 'dashboard' : 'setup');
    if (data.app.ocrError) toast('error', data.app.ocrError, 12000);
    // First start (or never finished): offer the guided tour.
    if (!data.settings.tourDone) setTimeout(() => tour.start(), 600);
  } catch (e) {
    document.getElementById('view').append(h('div', { class: 'card empty' }, h('h3', {}, 'HuntXtract konnte nicht starten'), h('p', {}, e.message)));
  }
}

boot();

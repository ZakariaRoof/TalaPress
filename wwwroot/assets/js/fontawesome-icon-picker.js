(function (window) {
  'use strict';

  const STYLE_PREFIX = {
    solid: 'fas',
    regular: 'far',
    brands: 'fab',
    light: 'fal',
    thin: 'fat',
    duotone: 'fad'
  };

  const PREFIX_TO_STYLE = {
    fas: 'solid',
    far: 'regular',
    fab: 'brands',
    fal: 'light',
    fat: 'thin',
    fad: 'duotone',
    'fa-solid': 'solid',
    'fa-regular': 'regular',
    'fa-brands': 'brands',
    'fa-light': 'light',
    'fa-thin': 'thin',
    'fa-duotone': 'duotone'
  };

  const STYLE_TOKENS = new Set([
    'fas', 'far', 'fab', 'fal', 'fat', 'fad',
    'fa-solid', 'fa-regular', 'fa-brands', 'fa-light', 'fa-thin', 'fa-duotone'
  ]);

  const TOP_15 = [
    'fas fa-heart',
    'fas fa-hand-holding-heart',
    'fas fa-star',
    'fas fa-users',
    'fas fa-house',
    'fas fa-graduation-cap',
    'fas fa-hospital',
    'fas fa-folder',
    'fas fa-tags',
    'fas fa-newspaper',
    'fas fa-briefcase',
    'fas fa-calendar-days',
    'fas fa-image',
    'fas fa-globe',
    'fas fa-lightbulb'
  ];

  let catalogPromise = null;
  let catalog = [];

  function stylePrefix(style) {
    return STYLE_PREFIX[style] || 'fas';
  }

  function buildClassName(style, iconName) {
    const name = String(iconName || '').replace(/^fa-/, '');
    return `${stylePrefix(style)} fa-${name}`;
  }

  function normalizeIconClass(icon) {
    let clean = String(icon || '').trim();
    if (!clean) return 'fas fa-circle';

    if (/^bi[\s-]/i.test(clean)) {
      if (clean.startsWith('bi ')) clean = clean.slice(3).trim();
      if (!clean.startsWith('bi-')) clean = 'bi-' + clean.replace(/^-/, '');
      return clean;
    }

    clean = clean.replace(/\s+/g, ' ');
    const longPrefix = clean.match(/^(fa-solid|fa-regular|fa-brands|fa-light|fa-thin|fa-duotone)\s+(fa-[\w-]+)$/i);
    if (longPrefix) {
      const style = PREFIX_TO_STYLE[longPrefix[1].toLowerCase()];
      return buildClassName(style, longPrefix[2]);
    }

    const shortPrefix = clean.match(/^(fas|far|fab|fal|fat|fad)\s+(fa-[\w-]+)$/i);
    if (shortPrefix) {
      return `${shortPrefix[1].toLowerCase()} ${shortPrefix[2].toLowerCase()}`;
    }

    if (/^fa-[\w-]+$/.test(clean)) {
      return `fas ${clean.toLowerCase()}`;
    }

    if (!clean.includes(' ')) {
      return `fas fa-${clean.replace(/^-/, '').toLowerCase()}`;
    }

    return clean.toLowerCase();
  }

  function isBootstrapIcon(iconClass) {
    return /^bi(-|\s)/i.test(String(iconClass || '').trim());
  }

  function iconHtml(iconClass) {
    const cls = normalizeIconClass(iconClass);
    if (isBootstrapIcon(cls)) {
      return `<i class="bi ${cls}"></i>`;
    }
    return `<i class="${cls}"></i>`;
  }

  function updateIconPreviewElement(previewEl, iconClass) {
    if (!previewEl) return;
    const cls = normalizeIconClass(iconClass);
    if (isBootstrapIcon(cls)) {
      previewEl.className = 'bi ' + cls;
    } else {
      previewEl.className = cls;
    }
  }

  function normalizeEntryTerms(entry) {
    const t = entry && entry.t;
    if (!t) return [];
    if (Array.isArray(t)) return t.map(x => String(x).toLowerCase());
    return [String(t).toLowerCase()];
  }

  function loadCatalog() {
    if (catalog.length) return Promise.resolve(catalog);
    if (catalogPromise) return catalogPromise;

    catalogPromise = fetch('/assets/js/fontawesome-icon-catalog.json?v=2')
      .then(r => {
        if (!r.ok) throw new Error('catalog load failed');
        return r.json();
      })
      .then(data => {
        catalog = Array.isArray(data) ? data : [];
        return catalog;
      })
      .catch(err => {
        console.warn('Font Awesome catalog load failed:', err);
        catalog = [];
        return catalog;
      });

    return catalogPromise;
  }

  function parseManualClass(query) {
    const q = query.trim().toLowerCase().replace(/\s+/g, ' ');
    const longMatch = q.match(/^(fa-solid|fa-regular|fa-brands|fa-light|fa-thin|fa-duotone)\s+(fa-[\w-]+)$/);
    if (longMatch) {
      const style = PREFIX_TO_STYLE[longMatch[1]];
      return buildClassName(style, longMatch[2]);
    }
    const shortMatch = q.match(/^(fas|far|fab|fal|fat|fad)\s+(fa-[\w-]+)$/);
    if (shortMatch) {
      return `${shortMatch[1]} ${shortMatch[2]}`;
    }
    return null;
  }

  function extractSearchContext(raw) {
    const manual = parseManualClass(raw);
    let terms = String(raw || '').trim().toLowerCase().split(/\s+/).filter(Boolean);

    terms = terms.filter(t => !STYLE_TOKENS.has(t));
    terms = terms.map(t => (t.startsWith('fa-') ? t.slice(3) : t));

    if (manual) {
      const iconName = manual.split(/\s+/)[1].replace(/^fa-/, '');
      if (iconName && !terms.includes(iconName)) {
        terms.unshift(iconName);
      }
    }

    terms = terms.filter(Boolean);
    return { manual, terms, rawLower: String(raw || '').trim().toLowerCase() };
  }

  function scoreIconEntry(entry, context) {
    const name = entry.n.toLowerCase();
    const labels = normalizeEntryTerms(entry);
    const classes = (entry.s || ['solid']).map(s => buildClassName(s, name).toLowerCase());
    const haystack = [name, ...labels, ...classes].join(' ');
    const nameParts = name.split('-');
    const { terms, rawLower, manual } = context;
    let score = 0;

    if (manual && classes.includes(manual)) score += 1000;
    if (rawLower && classes.some(c => c === rawLower)) score += 900;
    if (terms.length === 1 && name === terms[0]) score += 200;
    if (terms.length === 1 && name.startsWith(terms[0])) score += 160;
    if (terms.every(t => name.includes(t))) score += 120;
    if (terms.every(t => nameParts.some(p => p.includes(t) || p.startsWith(t)))) score += 100;
    if (terms.every(t => haystack.includes(t))) score += 80;
    if (terms.some(t => labels.some(l => l.includes(t)))) score += 60;
    if (terms.length === 1 && terms[0].length >= 3) {
      const t = terms[0];
      if (nameParts.some(p => p.startsWith(t) || p.includes(t))) score += 50;
    }

    return score;
  }

  function searchIcons(query, limit) {
    const max = limit || 15;
    const raw = String(query || '').trim();
    const context = extractSearchContext(raw);
    const results = [];
    const seen = new Set();

    function push(className, label, score) {
      const normalized = normalizeIconClass(className);
      if (seen.has(normalized)) return;
      seen.add(normalized);
      results.push({ className: normalized, label: label || normalized, score: score || 0 });
    }

    if (context.manual) {
      push(context.manual, context.manual, 2000);
    }

    if (!raw) {
      TOP_15.forEach((cls, idx) => push(cls, cls, 500 - idx));
      return results.slice(0, max);
    }

    if (catalog.length) {
      catalog
        .map(entry => ({ entry, score: scoreIconEntry(entry, context) }))
        .filter(x => x.score > 0)
        .sort((a, b) => b.score - a.score)
        .forEach(({ entry, score }) => {
          const style = entry.s && entry.s.length ? entry.s[0] : 'solid';
          push(buildClassName(style, entry.n), buildClassName(style, entry.n), score);
        });
    }

    if (!results.length && context.terms.length === 1 && /^[\w-]+$/.test(context.terms[0])) {
      push(`fas fa-${context.terms[0]}`, `fas fa-${context.terms[0]}`, 10);
    }

    if (!results.length && context.manual) {
      push(context.manual, context.manual, 2000);
    }

    results.sort((a, b) => b.score - a.score);
    return results.slice(0, max);
  }

  function renderIconGrid(gridEl, icons, onSelect) {
    if (!gridEl) return;
    gridEl.innerHTML = '';

    if (!icons.length) {
      gridEl.innerHTML = '<div class="text-muted fs-9 text-center py-2">لا توجد نتائج / No icons found</div>';
      return;
    }

    icons.forEach(item => {
      const box = document.createElement('button');
      box.type = 'button';
      box.className = 'tp-icon-select-box cardlist-icon-select-box';
      box.title = item.className;
      box.innerHTML = `${iconHtml(item.className)}<span class="icon-class-label">${item.label}</span>`;
      box.addEventListener('click', (e) => {
        e.stopPropagation();
        gridEl.querySelectorAll('.tp-icon-select-box').forEach(b => b.classList.remove('selected'));
        box.classList.add('selected');
        onSelect(item.className);
      });
      gridEl.appendChild(box);
    });
  }

  function initPickerPanel(panelEl, options) {
    if (!panelEl) return;

    const grid = panelEl.querySelector('.cardlist-icon-grid');
    const searchInput = panelEl.querySelector('.cardlist-icon-search');
    const meta = panelEl.querySelector('.cardlist-icon-search-meta');
    const iconInput = options.iconInput;
    const previewEl = options.previewEl;
    const onChange = options.onChange || function () {};
    let debounceTimer = null;

    function renderResults(query) {
      const q = String(query || '').trim();
      const icons = searchIcons(q, q ? 40 : 15);
      renderIconGrid(grid, icons, (className) => {
        if (iconInput) iconInput.value = className;
        if (searchInput) searchInput.value = className;
        updateIconPreviewElement(previewEl, className);
        onChange(className);
      });
      if (meta) {
        meta.textContent = q
          ? `نتائج البحث: ${icons.length} أيقونة / ${icons.length} result(s)`
          : 'أفضل 15 أيقونة — ابحث بالاسم أو الكلاس الكامل / Top 15 — search by name or full class';
      }
    }

    function refresh(query) {
      const q = String(query || '').trim();
      if (searchInput && searchInput.value !== q) {
        searchInput.value = q;
      }
      loadCatalog().then(() => renderResults(q)).catch(() => renderResults(q));
    }

    if (!panelEl._faPickerReady) {
      if (searchInput) {
        searchInput.addEventListener('input', () => {
          clearTimeout(debounceTimer);
          debounceTimer = setTimeout(() => refresh(searchInput.value), 120);
        });
        searchInput.addEventListener('click', (e) => e.stopPropagation());
      }
      panelEl._faPickerReady = true;
    }

    panelEl._faRefresh = refresh;
    refresh(iconInput ? iconInput.value : '');
  }

  window.TpFaIconPicker = {
    TOP_15,
    loadCatalog,
    searchIcons,
    normalizeIconClass,
    iconHtml,
    updateIconPreviewElement,
    renderIconGrid,
    initPickerPanel
  };
})(window);

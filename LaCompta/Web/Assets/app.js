const COLORS = {
  farming:  { bg: '#4CAF50', bgAlpha: 'rgba(76,175,80,0.15)',  border: '#4CAF50' },
  foraging: { bg: '#8BC34A', bgAlpha: 'rgba(139,195,74,0.15)', border: '#8BC34A' },
  fishing:  { bg: '#2196F3', bgAlpha: 'rgba(33,150,243,0.15)', border: '#2196F3' },
  mining:   { bg: '#FF9800', bgAlpha: 'rgba(255,152,0,0.15)',  border: '#FF9800' },
  other:    { bg: '#9E9E9E', bgAlpha: 'rgba(158,158,158,0.15)',border: '#9E9E9E' },
};

const CAT_LABELS = ['Farming', 'Foraging', 'Fishing', 'Mining', 'Other'];
const CAT_KEYS = ['farming', 'foraging', 'fishing', 'mining', 'other'];
const CAT_COLORS = CAT_KEYS.map(k => COLORS[k].bg);

const TAGLINES = [
  '"Salut salut, c\'est Val\u00e9rie de la compta..."',
  '"Les poireaux ne vont pas se d\u00e9clarer tout seuls."',
  '"Pierre called. He wants his margins back."',
  '"Another day, another gold coin for Lewis."',
  '"This spreadsheet is brought to you by URSSAF."',
  '"Your farm is basically a hedge fund at this point."',
  '"Joja Corp wishes they had these numbers."',
  '"Farming: like day trading, but with more dirt."',
];

const NAV_ITEMS = [
  { icon: '\uD83D\uDCCA', label: 'Overview', hash: 'overview' },
  { icon: '\u2696\uFE0F', label: 'Season Compare', hash: 'comparison' },
  { icon: '\uD83D\uDCB0', label: 'Profitability', hash: 'profitability' },
  { icon: '\uD83D\uDC1F', label: 'Legendary Fish', hash: 'legendary' },
];

let charts = {};
let seasons = [];
let summaryData = null;
let sortState = { key: 'profit', desc: true };
let currentOverviewSeason = null;
let currentProfitSeason = null;

/* Helpers */
function gold(n) { return n == null ? '...' : n.toLocaleString() + 'g'; }

async function fetchJson(url) {
  try { const r = await fetch(url); if (!r.ok) throw new Error(r.statusText); return await r.json(); }
  catch(e) { console.error('Fetch error:', url, e); return null; }
}

function seasonIcon(s) {
  var icons = { spring: '\uD83C\uDF31', summer: '\u2600\uFE0F', fall: '\uD83C\uDF42', winter: '\u2744\uFE0F' };
  return icons[s] || '\uD83D\uDCC5';
}

function clearChildren(el) { while (el.firstChild) el.removeChild(el.firstChild); }

function createEl(tag, attrs, children) {
  var el = document.createElement(tag);
  if (attrs) {
    Object.keys(attrs).forEach(function(k) {
      var v = attrs[k];
      if (k === 'className') el.className = v;
      else if (k === 'textContent') el.textContent = v;
      else el.setAttribute(k, v);
    });
  }
  if (children) children.forEach(function(c) { if (c) el.appendChild(typeof c === 'string' ? document.createTextNode(c) : c); });
  return el;
}

/**
 * Create a custom styled dropdown (no native select).
 * Returns { el: HTMLElement, getValue: fn, setValue: fn }
 */
function makeCustomDropdown(options, defaultValue, onChangeCb) {
  var wrapper = createEl('div', { className: 'custom-dropdown' });
  var btn = createEl('div', { className: 'custom-dropdown-btn' });
  var arrow = createEl('div', { className: 'custom-dropdown-arrow', textContent: '\u25BC' });
  btn.appendChild(arrow);
  wrapper.appendChild(btn);

  var menu = createEl('div', { className: 'custom-dropdown-menu' });
  wrapper.appendChild(menu);

  var currentValue = defaultValue;

  function render() {
    // Update button text
    var sel = options.find(function(o) { return o.value === currentValue; });
    // Clear btn text nodes but keep arrow
    while (btn.firstChild !== arrow && btn.firstChild) btn.removeChild(btn.firstChild);
    btn.insertBefore(document.createTextNode(sel ? sel.label : ''), arrow);

    // Rebuild menu items
    clearChildren(menu);
    options.forEach(function(opt) {
      var item = createEl('div', {
        className: 'custom-dropdown-item' + (opt.value === currentValue ? ' selected' : ''),
        textContent: opt.label
      });
      item.addEventListener('click', function(e) {
        e.stopPropagation();
        currentValue = opt.value;
        wrapper.classList.remove('open');
        render();
        if (onChangeCb) onChangeCb(currentValue);
      });
      menu.appendChild(item);
    });
  }

  btn.addEventListener('click', function(e) {
    e.stopPropagation();
    // Close all other dropdowns
    document.querySelectorAll('.custom-dropdown.open').forEach(function(d) {
      if (d !== wrapper) d.classList.remove('open');
    });
    wrapper.classList.toggle('open');
  });

  render();

  return {
    el: wrapper,
    getValue: function() { return currentValue; },
    setValue: function(v) { currentValue = v; render(); },
    setOptions: function(newOpts, newDefault) {
      options = newOpts;
      currentValue = newDefault !== undefined ? newDefault : (options.length > 0 ? options[0].value : '');
      render();
    }
  };
}

// Close dropdowns when clicking outside
document.addEventListener('click', function() {
  document.querySelectorAll('.custom-dropdown.open').forEach(function(d) { d.classList.remove('open'); });
});

function makeStatCard(label, value, tooltip, valueClass) {
  var card = createEl('div', { className: 'stat-card' });
  card.appendChild(createEl('div', { className: 'label', textContent: label }));
  var valEl = createEl('div', { className: 'value' + (valueClass ? ' ' + valueClass : ''), textContent: value });
  card.appendChild(valEl);
  if (tooltip) card.appendChild(createEl('div', { className: 'tooltip-text', textContent: tooltip }));
  return card;
}

/**
 * Stock-ticker style card: [arrow] label  value  (pct%)
 * @param {string} label - Card label
 * @param {number} change - The numeric change value
 * @param {string} pct - Percentage string
 * @param {boolean} positiveIsGood - true if positive change = green, false if positive = red (expenses)
 */
function makeStockCard(label, change, pct, positiveIsGood) {
  var isPositive = change >= 0;
  var isGood = positiveIsGood ? isPositive : !isPositive;
  var colorClass = isGood ? 'stock-up' : 'stock-down';

  var card = createEl('div', { className: 'stat-card stock-card' });

  // Top row: label
  card.appendChild(createEl('div', { className: 'label', textContent: label }));

  // Main row: arrow + value + (pct)
  var row = createEl('div', { className: 'stock-row' });

  // Arrow
  var arrow = createEl('div', { className: 'stock-arrow ' + colorClass });
  arrow.textContent = isGood ? '\u25B2' : '\u25BC';
  row.appendChild(arrow);

  // Value
  var sign = change >= 0 ? '+' : '';
  row.appendChild(createEl('div', { className: 'stock-value ' + colorClass, textContent: sign + gold(change) }));

  // Percentage
  var pctSign = change >= 0 ? '+' : '';
  row.appendChild(createEl('div', { className: 'stock-pct ' + colorClass, textContent: '(' + pctSign + pct + '%)' }));

  card.appendChild(row);
  return card;
}

function destroyChart(name) {
  if (charts[name]) { charts[name].destroy(); charts[name] = null; }
}

function seasonKey(s) { return s.season + ' Y' + s.year; }

function tooltipConfig() {
  return { backgroundColor: '#16213e', titleColor: '#e6d9a8', bodyColor: '#a89b6e', borderColor: '#8b7355', borderWidth: 1 };
}

function chartScaleColors() {
  return {
    ticks: { color: '#6b6245', font: { size: 9 } },
    grid: { color: 'rgba(139,115,85,0.1)' }
  };
}

function showEmpty(container, emoji, msg) {
  clearChildren(container);
  var empty = createEl('div', { className: 'empty-state' });
  empty.appendChild(createEl('div', { className: 'emoji', textContent: emoji }));
  empty.appendChild(createEl('div', { className: 'msg', textContent: msg }));
  container.appendChild(empty);
}

function showLoading(container) {
  clearChildren(container);
  container.appendChild(createEl('div', { className: 'loading', textContent: 'Loading...' }));
}

/* Build season tabs into a container, calling cb(season, year, tabEl) on click */
function buildSeasonTabs(container, activeSeason, activeYear, cb) {
  clearChildren(container);
  if (seasons.length === 0) {
    showEmpty(container, '\uD83D\uDD73\uFE0F', 'No data yet... your farm is a financial black hole');
    return;
  }

  // Get unique years
  var years = [];
  seasons.forEach(function(s) { if (years.indexOf(s.year) === -1) years.push(s.year); });

  // Wrapper row: year dropdown + season tabs
  var row = createEl('div', { className: 'season-selector-row' });

  // Custom year dropdown
  var yearOpts = years.map(function(yr) { return { value: String(yr), label: 'Year ' + yr }; });
  var yearDd = makeCustomDropdown(yearOpts, String(activeYear), function(val) {
    activeYear = parseInt(val);
    renderSeasonTabs(activeYear);
  });
  row.appendChild(yearDd.el);

  // Season tabs container
  var tabsWrap = createEl('div', { className: 'season-tabs-inner' });
  row.appendChild(tabsWrap);
  container.appendChild(row);

  function renderSeasonTabs(year) {
    clearChildren(tabsWrap);
    var yearSeasons = seasons.filter(function(s) { return s.year === year; });
    yearSeasons.forEach(function(s) {
      var isActive = s.season === activeSeason && s.year === activeYear;
      var tab = createEl('div', {
        className: 'season-tab ' + s.season + (isActive ? ' active' : ''),
        textContent: seasonIcon(s.season) + ' ' + s.season
      });
      tab.addEventListener('click', function() {
        tabsWrap.querySelectorAll('.season-tab').forEach(function(t) { t.classList.remove('active'); });
        tab.classList.add('active');
        activeSeason = s.season;
        activeYear = s.year;
        cb(s.season, s.year, tab);
      });
      tabsWrap.appendChild(tab);
    });
    var match = yearSeasons.find(function(s) { return s.season === activeSeason; });
    if (!match && yearSeasons.length > 0) {
      match = yearSeasons[yearSeasons.length - 1];
      activeSeason = match.season;
      activeYear = match.year;
    }
    if (match) {
      tabsWrap.querySelectorAll('.season-tab').forEach(function(t) { t.classList.remove('active'); });
      var activeTab = tabsWrap.querySelector('.season-tab.' + match.season);
      if (activeTab) activeTab.classList.add('active');
      cb(match.season, match.year, activeTab);
    }
  }

  renderSeasonTabs(activeYear);
}

/* ========== SIDEBAR & ROUTING ========== */
function buildNav() {
  var list = document.getElementById('nav-list');
  NAV_ITEMS.forEach(function(item) {
    var li = createEl('li', { className: 'nav-item', 'data-hash': item.hash });
    li.appendChild(createEl('span', { className: 'nav-icon', textContent: item.icon }));
    li.appendChild(createEl('span', { className: 'nav-label', textContent: item.label }));
    li.addEventListener('click', function() {
      window.location.hash = '#' + item.hash;
    });
    list.appendChild(li);
  });
}

function navigate(hash) {
  if (!hash || hash === '#') hash = '#overview';
  var page = hash.replace('#', '');
  document.querySelectorAll('.page').forEach(function(p) { p.classList.remove('active'); });
  var target = document.getElementById('page-' + page);
  if (target) target.classList.add('active');
  else {
    var ov = document.getElementById('page-overview');
    if (ov) ov.classList.add('active');
    page = 'overview';
  }
  document.querySelectorAll('.nav-item').forEach(function(n) {
    n.classList.toggle('active', n.getAttribute('data-hash') === page);
  });
  loadPageData(page);
}

var loadedPages = {};

function loadPageData(page) {
  if (page === 'overview') loadOverviewPage();
  else if (page === 'comparison') loadComparisonPage();
  else if (page === 'profitability') loadProfitabilityPage();
  else if (page === 'legendary') loadLegendaryPage();
}

/* Sidebar toggle */
document.getElementById('burger-btn').addEventListener('click', function() {
  var sb = document.getElementById('sidebar');
  if (window.innerWidth <= 900) {
    sb.classList.toggle('expanded');
  } else {
    sb.classList.toggle('collapsed');
  }
});

/* ========== OVERVIEW ========== */
var overviewRange = 'season';

function setupTimeRangeSelector() {
  document.querySelectorAll('.range-btn').forEach(function(btn) {
    btn.addEventListener('click', function() {
      document.querySelectorAll('.range-btn').forEach(function(b) { b.classList.remove('active'); });
      btn.classList.add('active');
      overviewRange = btn.getAttribute('data-range');
      loadOverviewPage();
    });
  });
}

function loadOverviewPage() {
  loadSummaryCards();

  var seasonTabsEl = document.getElementById('overview-season-tabs');
  var chartsRow = document.querySelector('#page-overview .overview-main-row');
  var alltimePanel = document.getElementById('overview-alltime-panel');
  var seasonTablePanel = document.getElementById('overview-season-table-panel');

  if (overviewRange === 'alltime') {
    // Hide season tabs and per-season charts, show all-time charts
    seasonTabsEl.style.display = 'none';
    chartsRow.style.display = 'none';
    alltimePanel.style.display = 'block';
    seasonTablePanel.style.display = 'block';
    loadAlltimeCharts();
  } else if (overviewRange === 'year') {
    // Show season tabs filtered to years, show per-season charts
    seasonTabsEl.style.display = 'flex';
    chartsRow.style.display = 'grid';
    alltimePanel.style.display = 'none';
    seasonTablePanel.style.display = 'none';
    buildYearTabs(seasonTabsEl);
  } else {
    // Per season (default)
    seasonTabsEl.style.display = 'flex';
    chartsRow.style.display = 'grid';
    alltimePanel.style.display = 'none';
    seasonTablePanel.style.display = 'none';
    var lastSeason = seasons.length > 0 ? seasons[seasons.length - 1] : null;
    buildSeasonTabs(
      seasonTabsEl,
      lastSeason ? lastSeason.season : null,
      lastSeason ? lastSeason.year : null,
      function(season, year) { loadOverviewCharts(season, year); }
    );
    if (lastSeason) loadOverviewCharts(lastSeason.season, lastSeason.year);
  }
}

function buildYearTabs(container) {
  clearChildren(container);
  var years = [];
  seasons.forEach(function(s) {
    if (years.indexOf(s.year) === -1) years.push(s.year);
  });
  if (years.length === 0) {
    showEmpty(container, '\uD83D\uDD73\uFE0F', 'No data yet...');
    return;
  }
  years.forEach(function(yr, i) {
    var isLast = i === years.length - 1;
    var tab = createEl('div', {
      className: 'season-tab' + (isLast ? ' active' : ''),
      textContent: '\uD83D\uDCC5 Year ' + yr
    });
    tab.addEventListener('click', function() {
      container.querySelectorAll('.season-tab').forEach(function(t) { t.classList.remove('active'); });
      tab.classList.add('active');
      loadYearCharts(yr);
    });
    container.appendChild(tab);
  });
  loadYearCharts(years[years.length - 1]);
}

function loadYearCharts(year) {
  var yearSeasons = seasons.filter(function(s) { return s.year === year; });
  if (yearSeasons.length === 0) return;

  // Aggregate year data for doughnut
  var totals = { farming: 0, foraging: 0, fishing: 0, mining: 0, other: 0 };
  yearSeasons.forEach(function(s) {
    totals.farming += s.farmingTotal;
    totals.foraging += s.foragingTotal;
    totals.fishing += s.fishingTotal;
    totals.mining += s.miningTotal;
    totals.other += s.otherTotal;
  });

  var values = CAT_KEYS.map(function(k) { return totals[k]; });

  destroyChart('overviewDoughnut');
  charts.overviewDoughnut = new Chart(document.getElementById('overview-doughnut'), {
    type: 'doughnut',
    data: { labels: CAT_LABELS, datasets: [{ data: values, backgroundColor: CAT_COLORS, borderColor: '#1a1a2e', borderWidth: 3 }] },
    options: { responsive: true, maintainAspectRatio: false, cutout: '55%',
      plugins: { legend: { position: 'bottom', labels: { color: '#a89b6e', font: { family: 'Courier New', size: 11 }, padding: 12 } },
        tooltip: Object.assign({}, tooltipConfig(), { callbacks: { label: function(ctx) { var total = ctx.dataset.data.reduce(function(a,b){return a+b;}, 0); var pct = total > 0 ? ((ctx.raw / total) * 100).toFixed(1) : 0; return ctx.label + ': ' + ctx.raw.toLocaleString() + 'g (' + pct + '%)'; } } }) } }
  });

  destroyChart('overviewBar');
  charts.overviewBar = new Chart(document.getElementById('overview-bar'), {
    type: 'bar',
    data: { labels: CAT_LABELS, datasets: [{ label: 'Income', data: values, backgroundColor: CAT_COLORS.map(function(c){return c + '99';}), borderColor: CAT_COLORS, borderWidth: 2 }] },
    options: { responsive: true, maintainAspectRatio: false, indexAxis: 'y',
      plugins: { legend: { display: false }, tooltip: Object.assign({}, tooltipConfig(), { callbacks: { label: function(ctx) { return ctx.raw.toLocaleString() + 'g'; } } }) },
      scales: { x: { ticks: { color: '#6b6245', callback: function(v) { return v.toLocaleString() + 'g'; } }, grid: { color: 'rgba(139,115,85,0.1)' } }, y: { min: 0, ticks: { color: '#a89b6e', font: { family: 'Courier New', size: 12 } }, grid: { display: false } } } }
  });
}

async function loadAlltimeCharts() {
  // Area chart across all seasons
  var allDays = [];
  var allLabels = [];
  for (var i = 0; i < seasons.length; i++) {
    var s = seasons[i];
    var data = await fetchJson('/api/daily?season=' + s.season + '&year=' + s.year);
    if (data) {
      data.forEach(function(d) {
        allDays.push(d);
        allLabels.push(s.season.charAt(0).toUpperCase() + s.season.slice(1) + ' Y' + s.year + ' D' + d.day);
      });
    }
  }

  if (allDays.length === 0) return;

  var mkDs = function(lbl, key, col) {
    return { label: lbl, data: allDays.map(function(d){return d[key];}), borderColor: col.border, backgroundColor: col.bgAlpha, fill: true, tension: 0, pointRadius: 0 };
  };

  destroyChart('overviewAlltimeArea');
  charts.overviewAlltimeArea = new Chart(document.getElementById('overview-alltime-area'), {
    type: 'line',
    data: { labels: allLabels, datasets: [mkDs('Farming','farmingIncome',COLORS.farming), mkDs('Foraging','foragingIncome',COLORS.foraging), mkDs('Fishing','fishingIncome',COLORS.fishing), mkDs('Mining','miningIncome',COLORS.mining), mkDs('Other','otherIncome',COLORS.other)] },
    options: {
      responsive: true, maintainAspectRatio: false,
      interaction: { intersect: false, mode: 'index' },
      plugins: {
        legend: { labels: { color: '#a89b6e', font: { family: 'Courier New', size: 11 } } },
        tooltip: Object.assign({}, tooltipConfig(), { callbacks: { label: function(ctx) { return ctx.dataset.label + ': ' + ctx.raw.toLocaleString() + 'g'; } } }),
        zoom: { zoom: { wheel: { enabled: true }, pinch: { enabled: true }, mode: 'x' }, pan: { enabled: true, mode: 'x' } }
      },
      scales: {
        x: { ticks: { color: '#6b6245', font: { size: 7 }, maxRotation: 45, autoSkip: true, maxTicksLimit: 30 }, grid: { color: 'rgba(139,115,85,0.1)' } },
        y: { min: 0, ticks: { color: '#6b6245', callback: function(v){return v.toLocaleString() + 'g';} }, grid: { color: 'rgba(139,115,85,0.1)' }, stacked: true }
      }
    }
  });

  document.getElementById('overview-reset-zoom').onclick = function() {
    if (charts.overviewAlltimeArea) charts.overviewAlltimeArea.resetZoom();
  };

  // Season performance table
  var tbody = document.getElementById('overview-alltime-tbody');
  clearChildren(tbody);
  seasons.forEach(function(s) {
    var total = s.farmingTotal + s.foragingTotal + s.fishingTotal + s.miningTotal + s.otherTotal;
    var net = total - s.totalExpenses;
    var tr = createEl('tr');
    tr.appendChild(createEl('td', { textContent: seasonIcon(s.season) + ' ' + s.season + ' Y' + s.year }));
    tr.appendChild(createEl('td', { textContent: total.toLocaleString() + 'g' }));
    tr.appendChild(createEl('td', { textContent: s.totalExpenses.toLocaleString() + 'g' }));
    var netTd = createEl('td', { textContent: (net >= 0 ? '+' : '') + net.toLocaleString() + 'g', className: net >= 0 ? 'profit-positive' : 'profit-negative' });
    tr.appendChild(netTd);
    tr.appendChild(createEl('td', { textContent: 'Day ' + s.bestDay + ' (' + s.bestDayIncome.toLocaleString() + 'g)' }));
    tbody.appendChild(tr);
  });
}

/* Money falling effect */
function spawnMoneyParticles(card) {
  var symbols = ['\uD83D\uDCB0', '\uD83D\uDCB5', '\uD83D\uDCB2', '\u2728', '\uD83E\uDE99'];
  for (var i = 0; i < 6; i++) {
    var particle = document.createElement('span');
    particle.className = 'money-particle';
    particle.textContent = symbols[Math.floor(Math.random() * symbols.length)];
    particle.style.left = Math.random() * 80 + 10 + '%';
    particle.style.top = '-10px';
    particle.style.animationDuration = (0.8 + Math.random() * 0.8) + 's';
    particle.style.animationDelay = (Math.random() * 0.3) + 's';
    card.appendChild(particle);
    setTimeout(function(p) { if (p.parentNode) p.parentNode.removeChild(p); }, 2000, particle);
  }
}

function loadSummaryCards() {
  var container = document.getElementById('summary-cards');
  clearChildren(container);
  if (!summaryData) {
    showLoading(container);
    return;
  }
  var net = summaryData.totalIncome - summaryData.totalExpenses;
  var expenseClass = summaryData.totalExpenses > summaryData.totalIncome ? 'negative' : '';

  // Season-over-season differential
  var diffText = '--';
  var diffClass = '';
  var diffTooltip = 'Need 2+ years of data';
  if (seasons.length >= 2) {
    var last = seasons[seasons.length - 1];
    // Find same season from previous year
    var prev = seasons.find(function(s) { return s.season === last.season && s.year === last.year - 1; });
    if (prev) {
      var lastTotal = last.farmingTotal + last.foragingTotal + last.fishingTotal + last.miningTotal + last.otherTotal;
      var prevTotal = prev.farmingTotal + prev.foragingTotal + prev.fishingTotal + prev.miningTotal + prev.otherTotal;
      var diff = lastTotal - prevTotal;
      var pct = prevTotal > 0 ? ((diff / prevTotal) * 100).toFixed(1) : '0';
      diffText = (diff >= 0 ? '+' : '') + gold(diff);
      diffClass = diff >= 0 ? 'positive' : 'negative';
      diffTooltip = (diff >= 0 ? '\u25B2 ' : '\u25BC ') + pct + '% vs ' + prev.season + ' Y' + prev.year;
    }
  }

  var cards = [
    makeStatCard('Total Income', gold(summaryData.totalIncome), null),
    makeStatCard('Total Expenses', gold(summaryData.totalExpenses), null, expenseClass),
    makeStatCard('Net Profit', gold(net), null, net >= 0 ? 'positive' : 'negative'),
    makeStatCard('vs Last Year', diffText, diffTooltip, diffClass)
  ];
  cards.forEach(function(card) {
    card.addEventListener('mouseenter', function() { spawnMoneyParticles(card); });
    container.appendChild(card);
  });
}

/* ========== MULTI-FILTER SYSTEM ========== */
var activeFilters = []; // array of category indices (0=Farming, 1=Foraging, etc.)
var cachedOverviewSummary = null; // current season summary for chart updates

function toggleFilter(catIndex) {
  var pos = activeFilters.indexOf(catIndex);
  if (pos >= 0) {
    activeFilters.splice(pos, 1);
  } else {
    activeFilters.push(catIndex);
  }
  applyFilters();
}

function removeFilter(catIndex) {
  var pos = activeFilters.indexOf(catIndex);
  if (pos >= 0) activeFilters.splice(pos, 1);
  applyFilters();
}

function clearAllFilters() {
  activeFilters = [];
  applyFilters();
}

function applyFilters() {
  renderFilterChips();
  updateTrendForFilters();
  updateDoughnutForFilters();
  updateBarForFilters();
  updateSummaryForFilters();
}

function renderFilterChips() {
  var container = document.getElementById('filter-chips');
  clearChildren(container);
  if (activeFilters.length === 0) return;

  activeFilters.forEach(function(idx) {
    var chip = createEl('div', { className: 'filter-chip ' + CAT_LABELS[idx] });
    chip.appendChild(document.createTextNode(CAT_LABELS[idx]));
    var x = createEl('span', { className: 'chip-x', textContent: '\u2715' });
    x.addEventListener('click', function() { removeFilter(idx); });
    chip.appendChild(x);
    container.appendChild(chip);
  });

  var clearBtn = createEl('button', { className: 'filter-clear-btn', textContent: 'Clear all' });
  clearBtn.addEventListener('click', clearAllFilters);
  container.appendChild(clearBtn);
}

function isFilterActive() { return activeFilters.length > 0; }

function isCatVisible(idx) {
  return !isFilterActive() || activeFilters.indexOf(idx) >= 0;
}

function updateTrendForFilters() {
  if (!charts.overviewTrendLine) return;
  charts.overviewTrendLine.data.datasets.forEach(function(ds, i) {
    ds.hidden = isFilterActive() && !isCatVisible(i);
  });
  charts.overviewTrendLine.update();
}

function updateDoughnutForFilters() {
  if (!charts.overviewDoughnut || !cachedOverviewSummary) return;
  var s = cachedOverviewSummary;
  var allVals = [s.farmingTotal, s.foragingTotal, s.fishingTotal, s.miningTotal, s.otherTotal];
  var ds = charts.overviewDoughnut.data.datasets[0];
  // Keep all data but dim non-active segments
  ds.data = allVals;
  if (isFilterActive()) {
    ds.backgroundColor = CAT_COLORS.map(function(c, i) {
      return isCatVisible(i) ? c : c + '22';
    });
    ds.borderColor = CAT_COLORS.map(function(c, i) {
      return isCatVisible(i) ? '#1a1a2e' : '#1a1a2e';
    });
    ds.borderWidth = CAT_COLORS.map(function(c, i) {
      return isCatVisible(i) ? 4 : 1;
    });
  } else {
    ds.backgroundColor = CAT_COLORS;
    ds.borderColor = '#1a1a2e';
    ds.borderWidth = 3;
  }
  charts.overviewDoughnut.update();
}

function updateBarForFilters() {
  if (!charts.overviewBar || !cachedOverviewSummary) return;
  var s = cachedOverviewSummary;
  var allVals = [s.farmingTotal, s.foragingTotal, s.fishingTotal, s.miningTotal, s.otherTotal];
  var ds = charts.overviewBar.data.datasets[0];
  ds.data = allVals;
  if (isFilterActive()) {
    ds.backgroundColor = CAT_COLORS.map(function(c, i) {
      return isCatVisible(i) ? c + '99' : c + '15';
    });
    ds.borderColor = CAT_COLORS.map(function(c, i) {
      return isCatVisible(i) ? c : c + '33';
    });
  } else {
    ds.backgroundColor = CAT_COLORS.map(function(c) { return c + '99'; });
    ds.borderColor = CAT_COLORS;
  }
  charts.overviewBar.update();
}

function updateSummaryForFilters() {
  var container = document.getElementById('summary-cards');
  if (!container || !summaryData) return;

  var incomeEl = container.querySelector('.stat-card:nth-child(1) .value');
  var expenseEl = container.querySelector('.stat-card:nth-child(2) .value');
  var netEl = container.querySelector('.stat-card:nth-child(3) .value');
  if (!incomeEl || !expenseEl || !netEl) return;

  if (!isFilterActive()) {
    incomeEl.textContent = gold(summaryData.totalIncome);
    expenseEl.textContent = gold(summaryData.totalExpenses);
    var net = summaryData.totalIncome - summaryData.totalExpenses;
    netEl.textContent = gold(net);
    netEl.className = 'value ' + (net >= 0 ? 'positive' : 'negative');
    expenseEl.className = 'value ' + (summaryData.totalExpenses > summaryData.totalIncome ? 'negative' : '');
  } else {
    var catKeys = ['farming', 'foraging', 'fishing', 'mining', 'other'];
    var filteredIncome = 0;
    var filterNames = [];
    activeFilters.forEach(function(idx) {
      filteredIncome += summaryData.categories ? (summaryData.categories[catKeys[idx]] || 0) : 0;
      filterNames.push(CAT_LABELS[idx]);
    });
    incomeEl.textContent = gold(filteredIncome) + ' (' + filterNames.join('+') + ')';
    expenseEl.textContent = 'N/A';
    expenseEl.className = 'value';
    netEl.textContent = 'N/A';
    netEl.className = 'value';
  }
}

function handleChartClick(evt, elements, chart) {
  if (!elements || elements.length === 0) return;
  var idx = elements[0].index;
  toggleFilter(idx);
}

async function loadOverviewCharts(season, year) {
  var summary = seasons.find(function(s) { return s.season === season && s.year === year; });
  if (!summary) return;
  cachedOverviewSummary = summary;
  activeFilters = [];
  renderFilterChips();
  var values = [summary.farmingTotal, summary.foragingTotal, summary.fishingTotal, summary.miningTotal, summary.otherTotal];

  /* Main trend line chart */
  var dailyData = await fetchJson('/api/daily?season=' + season + '&year=' + year);
  if (dailyData && dailyData.length > 0) {
    var labels = dailyData.map(function(d) { return 'Day ' + d.day; });
    var mkDs = function(lbl, key, col) {
      return { label: lbl, data: dailyData.map(function(d){return d[key];}), borderColor: col.border, backgroundColor: col.bgAlpha, fill: false, tension: 0, pointRadius: 2 };
    };
    destroyChart('overviewTrendLine');
    charts.overviewTrendLine = new Chart(document.getElementById('overview-trend-line'), {
      type: 'line',
      data: { labels: labels, datasets: [mkDs('Farming','farmingIncome',COLORS.farming), mkDs('Foraging','foragingIncome',COLORS.foraging), mkDs('Fishing','fishingIncome',COLORS.fishing), mkDs('Mining','miningIncome',COLORS.mining), mkDs('Other','otherIncome',COLORS.other)] },
      options: {
        responsive: true, maintainAspectRatio: false,
        interaction: { intersect: false, mode: 'index' },
        onClick: function(evt, elements) {
          if (!elements || elements.length === 0) return;
          var idx = elements[0].index;
          var chart = charts.overviewTrendLine;
          if (!chart) return;
          // Zoom to clicked day +/- 1
          var minIdx = Math.max(0, idx - 1);
          var maxIdx = Math.min(labels.length - 1, idx + 1);
          chart.options.scales.x.min = minIdx;
          chart.options.scales.x.max = maxIdx;
          chart.update();
        },
        plugins: {
          legend: { labels: { color: '#a89b6e', font: { family: 'Courier New', size: 11 } } },
          tooltip: Object.assign({}, tooltipConfig(), {
            callbacks: {
              label: function(ctx) { return ctx.dataset.label + ': ' + ctx.raw.toLocaleString() + 'g'; },
              footer: function() { return 'Click to zoom on this day'; }
            }
          }),
          zoom: { zoom: { wheel: { enabled: true }, pinch: { enabled: true }, mode: 'x' }, pan: { enabled: true, mode: 'x' } }
        },
        scales: {
          x: {
            ticks: { color: '#6b6245', font: { size: 9 } },
            grid: { color: 'rgba(139,115,85,0.1)' },
            min: Math.max(0, labels.length - 14),
            max: labels.length - 1
          },
          y: { min: 0, ticks: { color: '#6b6245', callback: function(v){return v.toLocaleString()+'g';} }, grid: { color: 'rgba(139,115,85,0.1)' } }
        }
      }
    });
    document.getElementById('overview-trend-reset-zoom').onclick = function() {
      if (charts.overviewTrendLine) charts.overviewTrendLine.resetZoom();
    };
  }

  /* Doughnut — click to filter trend chart */
  activeFilter = -1;
  destroyChart('overviewDoughnut');
  charts.overviewDoughnut = new Chart(document.getElementById('overview-doughnut'), {
    type: 'doughnut',
    data: { labels: CAT_LABELS, datasets: [{ data: values, backgroundColor: CAT_COLORS, borderColor: '#1a1a2e', borderWidth: 3 }] },
    options: {
      responsive: true, maintainAspectRatio: false, cutout: '55%',
      onClick: handleChartClick,
      plugins: {
        legend: { position: 'bottom', labels: { color: '#a89b6e', font: { family: 'Courier New', size: 11 }, padding: 12 } },
        tooltip: Object.assign({}, tooltipConfig(), {
          callbacks: { label: function(ctx) {
            var total = ctx.dataset.data.reduce(function(a,b){return a+b;},0);
            var pct = total > 0 ? ((ctx.raw / total) * 100).toFixed(1) : 0;
            return ctx.label + ': ' + ctx.raw.toLocaleString() + 'g (' + pct + '%) — click to filter';
          }}
        })
      }
    }
  });

  /* Horizontal bar — click to filter trend chart */
  destroyChart('overviewBar');
  charts.overviewBar = new Chart(document.getElementById('overview-bar'), {
    type: 'bar',
    data: { labels: CAT_LABELS, datasets: [{ label: 'Income', data: values, backgroundColor: CAT_COLORS.map(function(c){return c+'99';}), borderColor: CAT_COLORS, borderWidth: 2 }] },
    options: {
      responsive: true, maintainAspectRatio: false, indexAxis: 'y',
      onClick: handleChartClick,
      plugins: {
        legend: { display: false },
        tooltip: Object.assign({}, tooltipConfig(), {
          callbacks: { label: function(ctx) { return ctx.raw.toLocaleString() + 'g — click to filter'; } }
        })
      },
      scales: {
        x: { min: 0, ticks: { color: '#6b6245', callback: function(v){return v.toLocaleString()+'g';} }, grid: { color: 'rgba(139,115,85,0.1)' } },
        y: { min: 0, ticks: { color: '#a89b6e', font: { family: 'Courier New', size: 12 } }, grid: { display: false } }
      }
    }
  });
}

/* Tagline rotation */
function rotateTags() {
  var el = document.getElementById('tagline');
  var i = 0;
  el.textContent = TAGLINES[0];
  setInterval(function() {
    i = (i + 1) % TAGLINES.length;
    el.style.opacity = '0';
    setTimeout(function() { el.textContent = TAGLINES[i]; el.style.opacity = '1'; }, 400);
  }, 5000);
}

/* ========== COMPARISON ========== */
function buildCompSelector(container, label, defaultYear, defaultSeason, onChange) {
  var years = [];
  seasons.forEach(function(s) { if (years.indexOf(s.year) === -1) years.push(s.year); });

  var wrap = createEl('div', { className: 'comp-selector-group' });
  wrap.appendChild(createEl('label', { textContent: label }));

  var currentYear = defaultYear;
  var currentSeason = defaultSeason;

  // Custom year dropdown
  var yearOpts = years.map(function(yr) { return { value: String(yr), label: 'Year ' + yr }; });
  var yearDd = makeCustomDropdown(yearOpts, String(defaultYear), function(val) {
    currentYear = parseInt(val);
    var yearSeasons = seasons.filter(function(s) { return s.year === currentYear; });
    var seasonOpts = yearSeasons.map(function(s) { return { value: s.season, label: seasonIcon(s.season) + ' ' + s.season }; });
    var matchSeason = yearSeasons.find(function(s) { return s.season === currentSeason; });
    var defSeason = matchSeason ? currentSeason : (yearSeasons.length > 0 ? yearSeasons[0].season : '');
    seasonDd.setOptions(seasonOpts, defSeason);
    currentSeason = defSeason;
    onChange(currentYear, currentSeason);
  });

  // Custom season dropdown
  var initYearSeasons = seasons.filter(function(s) { return s.year === defaultYear; });
  var seasonOpts = initYearSeasons.map(function(s) { return { value: s.season, label: seasonIcon(s.season) + ' ' + s.season }; });
  var seasonDd = makeCustomDropdown(seasonOpts, defaultSeason, function(val) {
    currentSeason = val;
    onChange(currentYear, currentSeason);
  });

  wrap.appendChild(yearDd.el);
  wrap.appendChild(seasonDd.el);
  container.appendChild(wrap);

  return { getYear: function() { return currentYear; }, getSeason: function() { return currentSeason; } };
}

function loadComparisonPage() {
  var container = document.getElementById('comparison-selectors');
  clearChildren(container);

  if (seasons.length < 2) {
    showEmpty(container, '\u2696\uFE0F', 'Need at least 2 seasons to compare');
    return;
  }

  // Default: current season vs same season previous year
  var current = seasons[seasons.length - 1];
  var previous = seasons.find(function(s) {
    return s.season === current.season && s.year === current.year - 1;
  });
  // Fallback: if no same season last year, use second-to-last
  if (!previous) previous = seasons.length >= 2 ? seasons[seasons.length - 2] : seasons[0];

  var selectorA, selectorB;

  function doCompare() {
    var a = seasons.find(function(s) { return s.year === selectorA.getYear() && s.season === selectorA.getSeason(); });
    var b = seasons.find(function(s) { return s.year === selectorB.getYear() && s.season === selectorB.getSeason(); });
    if (a && b) renderComparison(a, b);
  }

  selectorA = buildCompSelector(container, 'Season A:', previous.year, previous.season, doCompare);
  selectorB = buildCompSelector(container, 'Season B:', current.year, current.season, doCompare);

  doCompare();
}

function renderComparison(a, b) {
  var titleA = document.getElementById('comp-title-a');
  var titleB = document.getElementById('comp-title-b');
  titleA.textContent = seasonIcon(a.season) + ' ' + a.season + ' Y' + a.year;
  titleB.textContent = seasonIcon(b.season) + ' ' + b.season + ' Y' + b.year;

  var valsA = [a.farmingTotal, a.foragingTotal, a.fishingTotal, a.miningTotal, a.otherTotal];
  var valsB = [b.farmingTotal, b.foragingTotal, b.fishingTotal, b.miningTotal, b.otherTotal];
  var totalA = valsA.reduce(function(x,y){return x+y;},0);
  var totalB = valsB.reduce(function(x,y){return x+y;},0);

  /* Stat cards — stock ticker style */
  var statsContainer = document.getElementById('comparison-stats');
  clearChildren(statsContainer);

  var incomeChange = totalB - totalA;
  var incomePct = totalA > 0 ? ((incomeChange / totalA) * 100).toFixed(1) : '0.0';
  statsContainer.appendChild(makeStockCard('Income', incomeChange, incomePct, true));

  var expChange = b.totalExpenses - a.totalExpenses;
  var expPct = a.totalExpenses > 0 ? ((expChange / a.totalExpenses) * 100).toFixed(1) : '0.0';
  statsContainer.appendChild(makeStockCard('Expenses', expChange, expPct, false));

  var netA = totalA - a.totalExpenses;
  var netB = totalB - b.totalExpenses;
  var netChange = netB - netA;
  var netPct = netA > 0 ? ((netChange / netA) * 100).toFixed(1) : '0.0';
  statsContainer.appendChild(makeStockCard('Net Profit', netChange, netPct, true));

  var bestChange = b.bestDayIncome - a.bestDayIncome;
  var bestPct = a.bestDayIncome > 0 ? ((bestChange / a.bestDayIncome) * 100).toFixed(1) : '0.0';
  statsContainer.appendChild(makeStockCard('Best Day', bestChange, bestPct, true));

  /* Chart A — with difference annotations */
  var diffs = valsA.map(function(v, i) { return valsB[i] - v; });

  destroyChart('compA');
  charts.compA = new Chart(document.getElementById('comp-chart-a'), {
    type: 'bar',
    data: {
      labels: CAT_LABELS,
      datasets: [
        { label: seasonKey(a), data: valsA, backgroundColor: CAT_COLORS.map(function(c){return c+'66';}), borderColor: CAT_COLORS, borderWidth: 2 },
        { label: seasonKey(b), data: valsB, backgroundColor: CAT_COLORS.map(function(c){return c+'cc';}), borderColor: CAT_COLORS, borderWidth: 2 }
      ]
    },
    options: {
      responsive: true, maintainAspectRatio: false,
      plugins: {
        legend: { labels: { color: '#a89b6e', font: { family: 'Courier New', size: 11 } } },
        tooltip: Object.assign({}, tooltipConfig(), {
          callbacks: {
            afterBody: function(items) {
              var idx = items[0].dataIndex;
              var diff = diffs[idx];
              var sign = diff >= 0 ? '+' : '';
              return 'Diff: ' + sign + diff.toLocaleString() + 'g';
            }
          }
        })
      },
      scales: {
        y: { min: 0, ticks: { color: '#6b6245', callback: function(v){return v.toLocaleString()+'g';} }, grid: { color: 'rgba(139,115,85,0.1)' } },
        x: { ticks: { color: '#a89b6e' }, grid: { display: false } }
      }
    }
  });

  /* Chart B — difference chart (B - A) */
  var diffColors = diffs.map(function(d) { return d >= 0 ? 'rgba(46,204,113,0.7)' : 'rgba(231,76,60,0.7)'; });
  var diffBorders = diffs.map(function(d) { return d >= 0 ? '#2ecc71' : '#e74c3c'; });

  destroyChart('compB');
  charts.compB = new Chart(document.getElementById('comp-chart-b'), {
    type: 'bar',
    data: {
      labels: CAT_LABELS,
      datasets: [{ label: 'Difference (B - A)', data: diffs, backgroundColor: diffColors, borderColor: diffBorders, borderWidth: 2 }]
    },
    options: {
      responsive: true, maintainAspectRatio: false,
      plugins: {
        legend: { labels: { color: '#a89b6e', font: { family: 'Courier New', size: 11 } } },
        tooltip: Object.assign({}, tooltipConfig(), {
          callbacks: { label: function(ctx) { var v = ctx.raw; return (v >= 0 ? '+' : '') + v.toLocaleString() + 'g'; } }
        })
      },
      scales: {
        y: { ticks: { color: '#6b6245', callback: function(v){return v.toLocaleString()+'g';} }, grid: { color: 'rgba(139,115,85,0.1)' } },
        x: { ticks: { color: '#a89b6e' }, grid: { display: false } }
      }
    }
  });
}

/* ========== PROFITABILITY ========== */
function loadProfitabilityPage() {
  var defSeason = currentProfitSeason || (seasons[0] && seasons[0].season);
  var defYear = (currentProfitSeason && currentProfitSeason._year) || (seasons[0] && seasons[0].year);
  buildSeasonTabs(
    document.getElementById('profit-season-tabs'),
    defSeason, defYear,
    function(season, year) {
      currentProfitSeason = season;
      currentProfitSeason._year = year;
      loadProfitTable(season, year);
    }
  );
  if (seasons.length > 0) loadProfitTable(seasons[0].season, seasons[0].year);

  /* Sort handler */
  document.querySelectorAll('#profit-table thead th').forEach(function(th) {
    th.onclick = function() {
      var key = th.dataset.sort;
      if (!key) return;
      if (sortState.key === key) sortState.desc = !sortState.desc;
      else { sortState.key = key; sortState.desc = true; }
      var s0 = seasons[0];
      if (s0) loadProfitTable(currentProfitSeason || s0.season, (currentProfitSeason && currentProfitSeason._year) || s0.year);
    };
  });
}

async function loadProfitTable(season, year) {
  var data = await fetchJson('/api/profitability?season=' + season + '&year=' + year + '&limit=50');
  var tbody = document.getElementById('profit-tbody');
  clearChildren(tbody);

  if (!data || !data.length) {
    var tr = createEl('tr');
    var td = createEl('td', { colspan: '6' });
    showEmpty(td, '\uD83D\uDCED', 'The shipping bin is judging you');
    tr.appendChild(td);
    tbody.appendChild(tr);
    return;
  }

  var items = data.map(function(d) { return Object.assign({}, d, { profit: d.totalPrice - d.costBasis }); });
  items.sort(function(a, b) {
    var va = a[sortState.key], vb = b[sortState.key];
    if (typeof va === 'string') return sortState.desc ? vb.localeCompare(va) : va.localeCompare(vb);
    return sortState.desc ? vb - va : va - vb;
  });

  items.forEach(function(d) {
    var tr = createEl('tr');
    tr.appendChild(createEl('td', { textContent: d.itemName }));
    var catTd = createEl('td');
    catTd.appendChild(createEl('span', { className: 'cat-badge ' + d.category, textContent: d.category }));
    tr.appendChild(catTd);
    tr.appendChild(createEl('td', { textContent: String(d.quantity) }));
    tr.appendChild(createEl('td', { textContent: d.totalPrice.toLocaleString() + 'g' }));
    tr.appendChild(createEl('td', { textContent: d.costBasis.toLocaleString() + 'g' }));
    tr.appendChild(createEl('td', {
      className: d.profit >= 0 ? 'profit-positive' : 'profit-negative',
      textContent: (d.profit >= 0 ? '+' : '') + d.profit.toLocaleString() + 'g'
    }));
    tbody.appendChild(tr);
  });

  document.querySelectorAll('#profit-table thead th').forEach(function(th) {
    var key = th.dataset.sort;
    th.classList.toggle('sorted', key === sortState.key);
    var arrow = th.querySelector('.sort-arrow');
    if (arrow) arrow.textContent = key === sortState.key ? (sortState.desc ? '\u25BC' : '\u25B2') : '';
  });
}

/* ========== LEGENDARY ========== */
async function loadLegendaryPage() {
  var grid = document.getElementById('fish-grid');
  showLoading(grid);
  var data = await fetchJson('/api/fish');
  clearChildren(grid);

  if (!data || !data.length) {
    showEmpty(grid, '\uD83D\uDC1F', 'Willy is disappointed in you');
    return;
  }

  /* Sort: legendary first, then by revenue */
  data.sort(function(a, b) {
    if (a.isLegendary && !b.isLegendary) return -1;
    if (!a.isLegendary && b.isLegendary) return 1;
    return b.totalRevenue - a.totalRevenue;
  });

  data.forEach(function(f) {
    var card = createEl('div', { className: 'fish-card' + (f.isLegendary ? ' legendary' : '') });
    if (f.isLegendary) {
      card.appendChild(createEl('div', { className: 'legendary-badge', textContent: '\u2605 LEGENDARY' }));
    }
    var iconText = f.isLegendary ? '\uD83D\uDC51' : '\uD83D\uDC20';
    card.appendChild(createEl('div', { className: 'fish-icon', textContent: iconText }));
    var info = createEl('div', { className: 'fish-info' });
    info.appendChild(createEl('div', { className: 'fish-name', textContent: f.fishName }));
    var details = f.totalRevenue.toLocaleString() + 'g';
    if (f.season) details += ' \u00B7 ' + seasonIcon(f.season) + ' ' + f.season + ' Y' + f.year + ' Day ' + f.day;
    if (f.quantity > 1) details += ' \u00B7 x' + f.quantity;
    info.appendChild(createEl('div', { className: 'fish-details', textContent: details }));
    card.appendChild(info);
    grid.appendChild(card);
  });
}

/* ========== INIT ========== */
async function init() {
  setupTimeRangeSelector();
  buildNav();
  rotateTags();

  summaryData = await fetchJson('/api/summary');
  seasons = await fetchJson('/api/seasons') || [];

  // Load farm name
  var farmInfo = await fetchJson('/api/farminfo');
  if (farmInfo && farmInfo.farmName) {
    document.getElementById('farm-name').textContent = farmInfo.farmName + ' Farm — ' + farmInfo.playerName;
  }

  window.addEventListener('hashchange', function() { navigate(window.location.hash); });
  navigate(window.location.hash);
}

init();

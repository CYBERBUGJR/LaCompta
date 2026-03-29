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
  { icon: '\uD83D\uDCCB', label: 'Sales Ledger', hash: 'sales' },
  { icon: '\uD83D\uDC1F', label: 'Legendary Fish', hash: 'legendary' },
];

let charts = {};
let seasons = [];
let summaryData = null;
let sortState = { key: 'profit', desc: true };
let currentProfitSeason = null;
let currentProfitYear = null;
let currentPlayerId = '';
let players = [];

/* Helpers */
function gold(n) { return n == null ? '...' : n.toLocaleString() + 'g'; }

function apiUrl(path, params) {
  var parts = [];
  if (currentPlayerId) parts.push('playerId=' + encodeURIComponent(currentPlayerId));
  if (params) {
    Object.keys(params).forEach(function(k) { parts.push(k + '=' + encodeURIComponent(params[k])); });
  }
  return path + (parts.length > 0 ? '?' + parts.join('&') : '');
}

async function fetchJson(url) {
  try { const r = await fetch(url); if (!r.ok) throw new Error(r.statusText); return await r.json(); }
  catch(e) { console.error('Fetch error:', url, e); return null; }
}

function seasonIcon(s) {
  var icons = { spring: '\uD83C\uDF31', summer: '\u2600\uFE0F', fall: '\uD83C\uDF42', winter: '\u2744\uFE0F' };
  return icons[s] || '\uD83D\uDCC5';
}

function clearChildren(el) { while (el.firstChild) el.removeChild(el.firstChild); }

function getUniqueYears() {
  var years = [];
  seasons.forEach(function(s) { if (years.indexOf(s.year) === -1) years.push(s.year); });
  return years;
}

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

function makeStatCard(label, value, infoText, valueClass) {
  var card = createEl('div', { className: 'stat-card' });
  if (infoText) {
    var infoIcon = createEl('div', { className: 'stat-info-icon', textContent: 'i' });
    var infoBubble = createEl('div', { className: 'stat-info-bubble', textContent: infoText });
    infoIcon.appendChild(infoBubble);
    card.appendChild(infoIcon);
  }
  card.appendChild(createEl('div', { className: 'label', textContent: label }));
  var valEl = createEl('div', { className: 'value' + (valueClass ? ' ' + valueClass : ''), textContent: value });
  card.appendChild(valEl);
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

  // Arrow: direction matches actual change (up/down), color matches good/bad
  var arrow = createEl('div', { className: 'stock-arrow ' + colorClass });
  arrow.textContent = isPositive ? '\u25B2' : '\u25BC';
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
  var years = getUniqueYears();

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

function loadPageData(page) {
  if (page === 'overview') loadOverviewPage();
  else if (page === 'sales') loadSalesPage();
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

var stackedAreaEnabled = false;

function setupTimeRangeSelector() {
  document.querySelectorAll('.range-btn').forEach(function(btn) {
    btn.addEventListener('click', function() {
      document.querySelectorAll('.range-btn').forEach(function(b) { b.classList.remove('active'); });
      btn.classList.add('active');
      overviewRange = btn.getAttribute('data-range');
      loadOverviewPage();
    });
  });

  var stackToggle = document.getElementById('stacked-area-toggle');
  if (stackToggle) {
    stackToggle.addEventListener('change', function() {
      stackedAreaEnabled = stackToggle.checked;
      if (charts.overviewTrendLine) {
        charts.overviewTrendLine.data.datasets.forEach(function(ds) {
          ds.fill = stackedAreaEnabled;
        });
        charts.overviewTrendLine.options.scales.y.stacked = stackedAreaEnabled;
        charts.overviewTrendLine.update();
      }
    });
  }
}

function loadOverviewPage() {
  var summaryRow = document.getElementById('summary-cards');
  var seasonTabsEl = document.getElementById('overview-season-tabs');
  var chartsRow = document.querySelector('#page-overview .overview-main-row');
  var alltimePanel = document.getElementById('overview-alltime-panel');
  var seasonTablePanel = document.getElementById('overview-season-table-panel');

  loadSummaryCards();

  if (overviewRange === 'alltime') {
    seasonTabsEl.style.display = 'none';
    chartsRow.style.display = 'none';
    alltimePanel.style.display = 'block';
    seasonTablePanel.style.display = 'block';
    loadAlltimeCharts();
  } else if (overviewRange === 'year') {
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

async function loadYearCharts(year) {
  var yearSeasons = seasons.filter(function(s) { return s.year === year; });
  if (yearSeasons.length === 0) return;
  loadSummaryCards(null, year);

  // Fetch daily data for all seasons in this year and build trend chart
  var results = await Promise.all(yearSeasons.map(function(s) {
    return fetchJson(apiUrl('/api/daily', { season: s.season, year: s.year })).then(function(data) {
      return { season: s, data: data };
    });
  }));
  var allDays = [];
  var allLabels = [];
  results.forEach(function(r) {
    if (r.data) {
      r.data.forEach(function(d) {
        allDays.push(d);
        allLabels.push(r.season.season.charAt(0).toUpperCase() + r.season.season.slice(1) + ' D' + d.day);
      });
    }
  });
  if (allDays.length > 0) {
    var mkDs = function(lbl, key, col) {
      return { label: lbl, data: allDays.map(function(d){return d[key];}), borderColor: col.border, backgroundColor: col.bgAlpha, fill: stackedAreaEnabled, tension: 0, pointRadius: 1 };
    };
    destroyChart('overviewTrendLine');
    charts.overviewTrendLine = new Chart(document.getElementById('overview-trend-line'), {
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
          y: { min: 0, stacked: stackedAreaEnabled, ticks: { color: '#6b6245', callback: function(v){return v.toLocaleString() + 'g';} }, grid: { color: 'rgba(139,115,85,0.1)' } }
        }
      }
    });
  }

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
  var results = await Promise.all(seasons.map(function(s) {
    return fetchJson(apiUrl('/api/daily', { season: s.season, year: s.year })).then(function(data) {
      return { season: s, data: data };
    });
  }));
  results.forEach(function(r) {
    if (r.data) {
      r.data.forEach(function(d) {
        allDays.push(d);
        allLabels.push(r.season.season.charAt(0).toUpperCase() + r.season.season.slice(1) + ' Y' + r.season.year + ' D' + d.day);
      });
    }
  });

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

var currentViewSeason = null;
var currentViewYear = null;

function sumSeasonIncome(s) {
  return s.farmingTotal + s.foragingTotal + s.fishingTotal + s.miningTotal + s.otherTotal;
}

function loadSummaryCards(season, year) {
  // Track which season/year the cards reflect
  if (season !== undefined) currentViewSeason = season;
  if (year !== undefined) currentViewYear = year;

  var container = document.getElementById('summary-cards');
  clearChildren(container);
  if (!summaryData || seasons.length === 0) {
    showLoading(container);
    return;
  }

  var income, expenses, net, contextLabel, infoIncome, infoExpenses, infoNet;

  if (overviewRange === 'alltime') {
    // All-time view
    income = seasons.reduce(function(sum, s) { return sum + sumSeasonIncome(s); }, 0);
    expenses = seasons.reduce(function(sum, s) { return sum + s.totalExpenses; }, 0);
    contextLabel = 'All Time';
    infoIncome = 'Total income across all seasons since day 1';
    infoExpenses = 'Total expenses across all seasons since day 1 (Estimated via daily money delta, net spending only, not gross purchases)';
    infoNet = 'Income minus expenses across all seasons';
  } else if (overviewRange === 'year' && currentViewYear) {
    // Per-year view
    var yearSeasons = seasons.filter(function(s) { return s.year === currentViewYear; });
    income = yearSeasons.reduce(function(sum, s) { return sum + sumSeasonIncome(s); }, 0);
    expenses = yearSeasons.reduce(function(sum, s) { return sum + s.totalExpenses; }, 0);
    contextLabel = 'Year ' + currentViewYear;
    infoIncome = 'Total income for year ' + currentViewYear;
    infoExpenses = 'Expenses for year ' + currentViewYear + ' (Estimated via daily money delta, net spending only, not gross purchases)';
    infoNet = 'Net profit for year ' + currentViewYear;
  } else if (currentViewSeason && currentViewYear) {
    // Per-season view
    var match = seasons.find(function(s) { return s.season === currentViewSeason && s.year === currentViewYear; });
    income = match ? sumSeasonIncome(match) : 0;
    expenses = match ? match.totalExpenses : 0;
    contextLabel = currentViewSeason.charAt(0).toUpperCase() + currentViewSeason.slice(1) + ' Y' + currentViewYear;
    infoIncome = 'Income for ' + contextLabel;
    infoExpenses = 'Expenses for ' + contextLabel + ' (Estimated via daily money delta, net spending only, not gross purchases)';
    infoNet = 'Net profit for ' + contextLabel;
  } else {
    income = summaryData.totalIncome || 0;
    expenses = summaryData.totalExpenses || 0;
    contextLabel = 'All Time';
    infoIncome = 'Total income across all seasons';
    infoExpenses = 'Total expenses across all seasons (Estimated via daily money delta, net spending only, not gross purchases)';
    infoNet = 'Income minus expenses';
  }

  net = income - expenses;
  var expenseClass = expenses > income ? 'negative' : '';

  // Season-over-season differential
  var diffText = '--';
  var diffClass = '';
  var diffInfo = 'Need 2+ seasons to compare';
  if (currentViewSeason && currentViewYear) {
    var prev = seasons.find(function(s) { return s.season === currentViewSeason && s.year === currentViewYear - 1; });
    if (prev) {
      var curMatch = seasons.find(function(s) { return s.season === currentViewSeason && s.year === currentViewYear; });
      if (curMatch) {
        var curTotal = sumSeasonIncome(curMatch);
        var prevTotal = sumSeasonIncome(prev);
        var diff = curTotal - prevTotal;
        var pct = prevTotal > 0 ? ((diff / prevTotal) * 100).toFixed(1) : '0';
        diffText = (diff >= 0 ? '+' : '') + gold(diff);
        diffClass = diff >= 0 ? 'positive' : 'negative';
        diffInfo = (diff >= 0 ? 'Up ' : 'Down ') + pct + '% vs ' + prev.season + ' Y' + prev.year;
      }
    }
  }

  var cards = [
    makeStatCard('Income', gold(income), infoIncome),
    makeStatCard('Expenses', gold(expenses), infoExpenses, expenseClass),
    makeStatCard('Net Profit', gold(net), infoNet, net >= 0 ? 'positive' : 'negative'),
    makeStatCard('vs Last Year', diffText, diffInfo, diffClass)
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
  // Reset filters if all 5 categories are selected
  if (activeFilters.length >= 5) {
    activeFilters = [];
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
  var section = document.getElementById('filter-section');
  clearChildren(container);
  if (activeFilters.length === 0) {
    if (section) section.style.display = 'none';
    return;
  }
  if (section) section.style.display = 'block';

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
    ds.borderColor = '#1a1a2e';
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
  if (!isFilterActive()) {
    // Rebuild cards with full context data
    loadSummaryCards(currentViewSeason, currentViewYear);
    return;
  }

  // When filters active, update income card to show filtered total from current season
  var container = document.getElementById('summary-cards');
  if (!container || !cachedOverviewSummary) return;

  var incomeEl = container.querySelector('.stat-card:nth-child(1) .value');
  var expenseEl = container.querySelector('.stat-card:nth-child(2) .value');
  var netEl = container.querySelector('.stat-card:nth-child(3) .value');
  if (!incomeEl || !expenseEl || !netEl) return;

  var catTotals = [
    cachedOverviewSummary.farmingTotal, cachedOverviewSummary.foragingTotal,
    cachedOverviewSummary.fishingTotal, cachedOverviewSummary.miningTotal,
    cachedOverviewSummary.otherTotal
  ];
  var filteredIncome = 0;
  activeFilters.forEach(function(idx) { filteredIncome += catTotals[idx] || 0; });

  incomeEl.textContent = gold(filteredIncome);
  expenseEl.textContent = 'N/A';
  expenseEl.className = 'value';
  netEl.textContent = 'N/A';
  netEl.className = 'value';
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
  loadSummaryCards(season, year);
  var values = [summary.farmingTotal, summary.foragingTotal, summary.fishingTotal, summary.miningTotal, summary.otherTotal];

  /* Main trend line chart */
  var dailyData = await fetchJson(apiUrl('/api/daily', { season: season, year: year }));
  if (dailyData && dailyData.length > 0) {
    var labels = dailyData.map(function(d) { return 'Day ' + d.day; });
    var mkDs = function(lbl, key, col) {
      return { label: lbl, data: dailyData.map(function(d){return d[key];}), borderColor: col.border, backgroundColor: col.bgAlpha, fill: stackedAreaEnabled, tension: 0, pointRadius: 2 };
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
          y: { min: 0, stacked: stackedAreaEnabled, ticks: { color: '#6b6245', callback: function(v){return v.toLocaleString()+'g';} }, grid: { color: 'rgba(139,115,85,0.1)' } }
        }
      }
    });
    document.getElementById('overview-trend-reset-zoom').onclick = function() {
      if (charts.overviewTrendLine) charts.overviewTrendLine.resetZoom();
    };
  }

  /* Doughnut — click to filter trend chart */
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

/* ========== SALES LEDGER ========== */
var salesCurrentSeason = null;
var salesCurrentYear = null;
var salesCurrentDay = 0; // 0 = all days

function loadSalesPage() {
  var lastSeason = seasons.length > 0 ? seasons[seasons.length - 1] : null;
  buildSeasonTabs(
    document.getElementById('sales-season-tabs'),
    lastSeason ? lastSeason.season : null,
    lastSeason ? lastSeason.year : null,
    function(season, year) {
      salesCurrentSeason = season;
      salesCurrentYear = year;
      salesCurrentDay = 0;
      buildDaySelector(season, year);
      loadSalesData(season, year, 0);
    }
  );
}

function buildDaySelector(season, year) {
  var container = document.getElementById('sales-day-selector');
  clearChildren(container);

  container.appendChild(createEl('span', { className: 'day-label', textContent: 'Day:' }));

  // "All" button
  var allBtn = createEl('div', { className: 'day-btn active', textContent: 'All' });
  allBtn.addEventListener('click', function() {
    container.querySelectorAll('.day-btn').forEach(function(b) { b.classList.remove('active'); });
    allBtn.classList.add('active');
    salesCurrentDay = 0;
    loadSalesData(salesCurrentSeason, salesCurrentYear, 0);
  });
  container.appendChild(allBtn);

  // Day 1-28 buttons
  for (var d = 1; d <= 28; d++) {
    (function(day) {
      var btn = createEl('div', { className: 'day-btn', textContent: String(day) });
      btn.addEventListener('click', function() {
        container.querySelectorAll('.day-btn').forEach(function(b) { b.classList.remove('active'); });
        btn.classList.add('active');
        salesCurrentDay = day;
        loadSalesData(salesCurrentSeason, salesCurrentYear, day);
      });
      container.appendChild(btn);
    })(d);
  }
}

async function loadSalesData(season, year, day) {
  var params = { season: season, year: year };
  if (day > 0) params.day = day;
  var data = await fetchJson(apiUrl('/api/transactions', params));
  var container = document.getElementById('sales-table-body');
  clearChildren(container);

  if (!data || !data.length) {
    var tr = createEl('tr');
    var td = createEl('td', { colspan: '7' });
    showEmpty(td, '\uD83D\uDCCB', 'Nothing sold yet... the shipping bin misses you');
    tr.appendChild(td);
    container.appendChild(tr);
    return;
  }

  // Group by category
  var grouped = {};
  CAT_LABELS.forEach(function(cat) { grouped[cat] = []; });
  data.forEach(function(tx) {
    var cat = grouped[tx.category] ? tx.category : 'Other';
    grouped[cat].push(tx);
  });

  // Aggregate by item within each category
  CAT_LABELS.forEach(function(cat) {
    var items = grouped[cat];
    if (items.length === 0) return;

    // Aggregate same items
    var byItem = {};
    items.forEach(function(tx) {
      if (!byItem[tx.itemName]) {
        byItem[tx.itemName] = { itemName: tx.itemName, itemId: tx.itemId, category: cat, quantity: 0, totalPrice: 0, costBasis: 0, count: 0 };
      }
      byItem[tx.itemName].quantity += tx.quantity;
      byItem[tx.itemName].totalPrice += tx.totalPrice;
      byItem[tx.itemName].costBasis += tx.costBasis;
      byItem[tx.itemName].count++;
    });

    var aggregated = Object.values(byItem);
    aggregated.sort(function(a, b) { return b.totalPrice - a.totalPrice; });

    // Category header row
    var catTotal = aggregated.reduce(function(s, i) { return s + i.totalPrice; }, 0);
    var headerTr = createEl('tr', { className: 'sales-cat-header' });
    var headerTd = createEl('td', { colspan: '7' });
    headerTd.appendChild(createEl('span', { className: 'cat-badge ' + cat, textContent: cat }));
    headerTd.appendChild(createEl('span', { className: 'sales-cat-total', textContent: '  ' + catTotal.toLocaleString() + 'g total' }));
    headerTr.appendChild(headerTd);
    container.appendChild(headerTr);

    // Item rows
    aggregated.forEach(function(item) {
      var profit = item.totalPrice - item.costBasis;
      var avgPrice = item.quantity > 0 ? Math.round(item.totalPrice / item.quantity) : 0;
      var tr = createEl('tr');
      var nameTd = createEl('td', { className: 'item-name-cell' });
      var sprite = createEl('img', { className: 'item-sprite', src: '/api/sprite/' + item.itemId, alt: '' });
      sprite.onerror = function() { this.style.display = 'none'; };
      nameTd.appendChild(sprite);
      nameTd.appendChild(document.createTextNode(item.itemName));
      tr.appendChild(nameTd);
      tr.appendChild(createEl('td'));  // category column empty (shown in header)
      tr.appendChild(createEl('td', { textContent: String(item.quantity) }));
      tr.appendChild(createEl('td', { textContent: avgPrice.toLocaleString() + 'g' }));
      tr.appendChild(createEl('td', { textContent: item.totalPrice.toLocaleString() + 'g' }));
      tr.appendChild(createEl('td', { textContent: item.costBasis.toLocaleString() + 'g' }));
      tr.appendChild(createEl('td', {
        className: profit >= 0 ? 'profit-positive' : 'profit-negative',
        textContent: (profit >= 0 ? '+' : '') + profit.toLocaleString() + 'g'
      }));
      container.appendChild(tr);
    });
  });
}

/* ========== COMPARISON ========== */
function buildCompSelector(container, label, defaultYear, defaultSeason, onChange) {
  var years = getUniqueYears();

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
  var defYear = currentProfitYear || (seasons[0] && seasons[0].year);
  buildSeasonTabs(
    document.getElementById('profit-season-tabs'),
    defSeason, defYear,
    function(season, year) {
      currentProfitSeason = season;
      currentProfitYear = year;
      loadProfitTable(season, year);
    }
  );
  if (seasons.length > 0) loadProfitTable(defSeason, defYear);

  /* Sort handler */
  document.querySelectorAll('#profit-table thead th').forEach(function(th) {
    th.onclick = function() {
      var key = th.dataset.sort;
      if (!key) return;
      if (sortState.key === key) sortState.desc = !sortState.desc;
      else { sortState.key = key; sortState.desc = true; }
      if (currentProfitSeason) loadProfitTable(currentProfitSeason, currentProfitYear);
    };
  });
}

async function loadProfitTable(season, year) {
  var data = await fetchJson(apiUrl('/api/profitability', { season: season, year: year, limit: 50 }));
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
  var data = await fetchJson(apiUrl('/api/fish'));
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

/* ========== PLAYER SELECTOR ========== */
function buildPlayerSelector(playerList) {
  var container = document.getElementById('player-selector');
  if (!container) return;

  var opts = playerList.map(function(p) {
    var label = p.name + (p.isCurrentPlayer ? ' (you)' : '');
    return { value: p.playerId, label: label };
  });

  var dd = makeCustomDropdown(opts, currentPlayerId, function(val) {
    currentPlayerId = val;
    reloadAllData();
  });

  container.style.display = 'block';
  clearChildren(container);
  container.appendChild(createEl('span', { className: 'label', textContent: 'Player: ' }));
  container.appendChild(dd.el);
}

async function reloadAllData() {
  var initData = await Promise.all([
    fetchJson(apiUrl('/api/summary')),
    fetchJson(apiUrl('/api/seasons'))
  ]);
  summaryData = initData[0];
  seasons = initData[1] || [];
  navigate(window.location.hash);
}

/* ========== INIT ========== */
async function init() {
  setupTimeRangeSelector();
  buildNav();
  rotateTags();

  // Get player info first, then load data with correct playerId
  var metaData = await Promise.all([
    fetchJson('/api/farminfo'),
    fetchJson('/api/players')
  ]);
  var farmInfo = metaData[0];
  players = metaData[1] || [];

  if (farmInfo && farmInfo.playerId) {
    currentPlayerId = farmInfo.playerId;
  }

  // Show player selector if multiplayer
  if (players.length > 1) {
    buildPlayerSelector(players);
  }

  // Now load data with correct playerId filter
  var gameData = await Promise.all([
    fetchJson(apiUrl('/api/summary')),
    fetchJson(apiUrl('/api/seasons'))
  ]);
  summaryData = gameData[0];
  seasons = gameData[1] || [];
  if (farmInfo && farmInfo.farmName) {
    document.getElementById('farm-name').textContent = farmInfo.farmName + ' Farm — ' + farmInfo.playerName;
  }

  window.addEventListener('hashchange', function() { navigate(window.location.hash); });
  navigate(window.location.hash);
}

init();

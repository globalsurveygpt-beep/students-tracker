(function () {
  'use strict';

  var STATUSES = ['present', 'absent', 'excused', 'late'];
  var ST = {
    present: { label: 'Был', icon: 'i-present' },
    absent:  { label: 'Не был', icon: 'i-absent' },
    excused: { label: 'Уважительная', icon: 'i-excused' },
    late:    { label: 'Опоздал', icon: 'i-late' }
  };
  var ROLE = { admin: 'Администратор', teacher: 'Преподаватель', student: 'Студент' };
  function esc(v) { return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
  function ic(id, cls) { return '<svg class="i' + (cls ? ' ' + cls : '') + '" aria-hidden="true"><use href="#' + id + '"/></svg>'; }
  function byId(list, id) { for (var i = 0; i < list.length; i++) if (list[i].id === id) return list[i]; return null; }
  var WD = ['вс', 'пн', 'вт', 'ср', 'чт', 'пт', 'сб'];
  function fmtDate(d) { var p = d.split('-'); return p[2] + '.' + p[1] + '.' + p[0]; }
  function wd(d) { var p = d.split('-'); return WD[new Date(Number(p[0]), Number(p[1]) - 1, Number(p[2])).getDay()]; }
  function initials(n) { var p = n.split(' '); return (p[0] ? p[0][0] : '') + (p[1] ? p[1][0] : ''); }
  function short(n) { var p = n.split(' '); return p[0] + (p[1] ? ' ' + p[1][0] + '.' : '') + (p[2] ? ' ' + p[2][0] + '.' : ''); }
  function chip(st) { return '<span class="chip ' + st + '">' + ic(ST[st].icon) + ST[st].label + '</span>'; }
  function translit(n) {
    var m = { а: 'a', б: 'b', в: 'v', г: 'g', ғ: 'g', д: 'd', е: 'e', ё: 'yo', ж: 'zh', з: 'z', и: 'i', ӣ: 'i', й: 'y', к: 'k', қ: 'q', л: 'l', м: 'm', н: 'n', о: 'o', п: 'p', р: 'r', с: 's', т: 't', у: 'u', ӯ: 'u', ф: 'f', х: 'kh', ҳ: 'h', ц: 'ts', ч: 'ch', ҷ: 'j', ш: 'sh', щ: 'sh', ъ: '', ы: 'y', ь: '', э: 'e', ю: 'yu', я: 'ya' };
    return (n.split(' ')[0] || '').toLowerCase().split('').map(function (c) { return m[c] != null ? m[c] : (/[a-z0-9]/.test(c) ? c : ''); }).join('');
  }
  /* статистика: процент = (был + опоздал) / (все отметки − уважительные) */
  function calc(records) {
    var c = { present: 0, absent: 0, excused: 0, late: 0, total: records.length };
    records.forEach(function (a) { c[a.status]++; });
    var den = c.total - c.excused;
    c.pct = den > 0 ? Math.round((c.present + c.late) / den * 100) : null;
    return c;
  }
  function pctText(p) { return p == null ? '—' : p + '%'; }
  function bar(p) { return '<div class="bar"><div class="bar-track"><div class="bar-fill" style="width:' + (p || 0) + '%"></div></div><b>' + pctText(p) + '</b></div>'; }
  /* ================= тема ================= */
  var THEME_KEY = 'ds-theme';
  function readSaved() { try { return localStorage.getItem(THEME_KEY); } catch (e) { return null; } }
  function saveTheme(t) { try { localStorage.setItem(THEME_KEY, t); } catch (e) {} }
  function systemTheme() { return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'; }
  function applyTheme(t) { document.documentElement.setAttribute('data-theme', t); }
  function toggleTheme() { var n = document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark'; applyTheme(n); saveTheme(n); }
  applyTheme(readSaved() || systemTheme());
  function themeBtn() { return '<button type="button" class="theme-btn" data-act="theme" aria-label="Переключить тему">' + ic('i-sun', 'i-sun') + ic('i-moon', 'i-moon') + '</button>'; }
  function toast(msg, icon) {
    var el = document.createElement('div');
    el.className = 'toast';
    el.innerHTML = ic(icon || 'i-check') + '<span>' + esc(msg) + '</span>';
    document.getElementById('toasts').appendChild(el);
    setTimeout(function () { el.remove(); }, 2600);
  }

  function closeModal() { var m = document.querySelector('.modal-bg'); if (m) m.remove(); document.removeEventListener('keydown', escClose); }
  function escClose(e) { if (e.key === 'Escape') closeModal(); }
  function info(title, text) { modal({ title: title, body: '<div class="alert warn">' + ic('i-alert') + '<span>' + esc(text) + '</span></div>' }); }
  function confirmBox(title, text, okText, onOk) {
    modal({ title: title, body: '<p>' + esc(text) + '</p>', submit: okText, danger: true, onSubmit: function () { onOk(); return true; } });
  }
  function invalid(form, name, msg) {
    var el = form.elements[name], f = el && el.closest('.field');
    if (f) { f.classList.add('invalid'); var e = f.querySelector('.err'); if (e && msg) e.textContent = msg; }
    return false;
  }
  function fld(label, name, ctl, help) {
    return '<div class="field"><label for="f-' + name + '">' + label + '</label>' + ctl + (help ? '<div class="help">' + help + '</div>' : '') + '<div class="err">Заполните поле</div></div>';
  }
  function inp(name, val, type, extra) { return '<input class="ctl" id="f-' + name + '" name="' + name + '" type="' + (type || 'text') + '" value="' + esc(val == null ? '' : val) + '"' + (extra || '') + '>'; }
  function sel(name, opts, val) {
    return '<select class="ctl" id="f-' + name + '" name="' + name + '">' + opts.map(function (o) { return '<option value="' + esc(o[0]) + '"' + (String(o[0]) === String(val) ? ' selected' : '') + '>' + esc(o[1]) + '</option>'; }).join('') + '</select>';
  }

  /* ================= состояние и вспомогательные ================= */
  var root = document.getElementById('root');
  var state = { user: null, groupId: null, q: '', usersTab: 'users', lessonFilter: 'all', lessonGroup: 'all', cabSubject: 'all', rep: null, dirty: false, navOpen: false };
  var draft = null, cache = {}, renderToken = 0, lastRoute = '';
  var HANDLERS = {};
  function pad(n) { return (n < 10 ? '0' : '') + n; }
  function dateStr(d) { return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()); }
  function today() { return dateStr(new Date()); }
  function daysAgo(n) { var d = new Date(); d.setDate(d.getDate() - n); return dateStr(d); }
  function sortRu(list, key) { return list.slice().sort(function (a, b) { return String(a[key]).localeCompare(String(b[key]), 'ru'); }); }
  function uniqBy(list, key) { var seen = {}; return list.filter(function (x) { if (seen[x[key]]) return false; seen[x[key]] = 1; return true; }); }

  /* ================= API ================= */
  function errText(status, data) {
    if (data && data.errors) { var k = Object.keys(data.errors)[0]; if (k && data.errors[k][0]) return 'Проверьте данные: ' + data.errors[k][0]; }
    if (data && data.title) return data.title;
    return status === 403 ? 'Недостаточно прав' : 'Ошибка запроса (' + status + ')';
  }
  async function api(method, path, body) {
    var opt = { method: method, headers: { 'X-Requested-With': 'fetch' }, credentials: 'same-origin' };
    if (body !== undefined) { opt.headers['Content-Type'] = 'application/json'; opt.body = JSON.stringify(body); }
    var r = await fetch('/api/' + path, opt);
    var text = r.status === 204 ? '' : await r.text(), data = null;
    try { data = text ? JSON.parse(text) : null; } catch (e) { data = null; }
    if (r.ok) return data;
    var err = new Error(errText(r.status, data)); err.status = r.status; throw err;
  }
  function sessionExpired() {
    state.user = null; state.dirty = false; draft = null;
    render().then(function () { var e = document.getElementById('loginErr'); if (e) { e.querySelector('span').textContent = 'Сессия завершена, войдите снова'; e.style.display = 'flex'; } });
  }
  function fail(e, title) { if (e && e.status === 401 && state.user) { sessionExpired(); return; } info(title || 'Не удалось выполнить действие', e && e.message ? e.message : String(e)); }
  /* выполнить действие, показать результат и перерисовать экран */
  async function act(fn, okMsg, okIcon, failTitle) {
    try { await fn(); } catch (e) { fail(e, failTitle); return false; }
    if (okMsg) toast(okMsg, okIcon);
    await render(); return true;
  }

  /* ================= окна ================= */
  function modal(opts) {
    closeModal();
    var bg = document.createElement('div');
    bg.className = 'modal-bg';
    bg.innerHTML = '<div class="modal' + (opts.wide ? ' wide' : '') + '" role="dialog" aria-modal="true" aria-label="' + esc(opts.title) + '">' +
      '<form novalidate><div class="modal-head"><h3>' + esc(opts.title) + '</h3><button type="button" class="icon-btn" data-close aria-label="Закрыть">' + ic('i-x') + '</button></div>' +
      '<div class="modal-body"><div class="alert err" data-err style="display:none">' + ic('i-alert') + '<span></span></div>' + opts.body + '</div>' +
      '<div class="modal-foot">' + (opts.submit ? '<button type="button" class="btn btn-ghost" data-close>Отмена</button><button type="submit" class="btn ' + (opts.danger ? 'btn-danger' : 'btn-primary') + '">' + esc(opts.submit) + '</button>' : '<button type="button" class="btn btn-primary" data-close>Понятно</button>') + '</div></form></div>';
    document.body.appendChild(bg);
    var form = bg.querySelector('form');
    bg.addEventListener('mousedown', function (e) { if (e.target === bg) closeModal(); });
    bg.querySelectorAll('[data-close]').forEach(function (b) { b.addEventListener('click', closeModal); });
    document.addEventListener('keydown', escClose);
    form.addEventListener('submit', async function (e) {
      e.preventDefault();
      if (!opts.onSubmit) { closeModal(); return; }
      form.querySelectorAll('.field').forEach(function (f) { f.classList.remove('invalid'); });
      var box = form.querySelector('[data-err]'), btn = form.querySelector('[type=submit]'), res;
      box.style.display = 'none'; btn.disabled = true;
      try { res = await opts.onSubmit(form); }
      catch (err) { if (err && err.status === 401 && state.user) { sessionExpired(); return; } res = err && err.message ? err.message : String(err); }
      btn.disabled = false;
      if (res === true) { closeModal(); return; }
      if (typeof res === 'string') { box.style.display = 'flex'; box.querySelector('span').textContent = res; }
    });
    if (opts.onOpen) opts.onOpen(bg);
    var first = form.querySelector('input:not([type=checkbox]),select,textarea');
    (first || form.querySelector('[type=submit]') || form.querySelector('[data-close]')).focus();
    return bg;
  }
  function showCredentials(name, login, password, title) {
    modal({ title: title, body: '<p>Передайте данные для входа: <b>' + esc(name) + '</b>. Пароль показывается один раз, после закрытия окна его не увидеть.</p>' +
      '<div class="field"><div class="lbl">Логин</div><div class="secret"><span>' + esc(login) + '</span></div></div>' +
      '<div class="field"><div class="lbl">Временный пароль</div><div class="secret"><span>' + esc(password) + '</span></div></div>' +
      '<div class="alert info">' + ic('i-alert') + '<span>После входа пользователь может сменить пароль в разделе «Смена пароля».</span></div>' });
  }

  /* ================= маршруты ================= */
  var NAV = {
    admin: [['sec', 'Справочники'], ['#/groups', 'Группы и студенты', 'i-users'], ['#/subjects', 'Предметы', 'i-book'], ['#/users', 'Пользователи и назначения', 'i-shield'],
      ['sec', 'Журнал'], ['#/lessons', 'Занятия и отметки', 'i-calendar'], ['#/reports', 'Отчёты', 'i-chart']],
    teacher: [['sec', 'Работа'], ['#/lessons', 'Мои занятия', 'i-calendar'], ['#/reports', 'Отчёты', 'i-chart']],
    student: [['sec', 'Кабинет'], ['#/cabinet', 'Моя посещаемость', 'i-home']]
  };
  var HOME = { admin: '#/groups', teacher: '#/lessons', student: '#/cabinet' };
  var ALLOWED = {
    admin: ['groups', 'subjects', 'users', 'lessons', 'lesson', 'reports', 'password'],
    teacher: ['lessons', 'lesson', 'reports', 'password'],
    student: ['cabinet', 'password']
  };
  function parseRoute() {
    var parts = (location.hash || '').replace(/^#\/?/, '').split('/');
    return { name: parts[0] || '', id: parts[1] ? Number(parts[1]) : null };
  }
  function go(hash) {
    if (state.dirty && hash !== location.hash) {
      confirmBox('Несохранённые отметки', 'Отметки посещаемости не сохранены. Уйти со страницы без сохранения?', 'Уйти без сохранения', function () {
        state.dirty = false; draft = null; location.hash = hash;
      });
      return;
    }
    if (location.hash === hash) render(); else location.hash = hash;
  }

  /* ================= отрисовка ================= */
  async function render() {
    closeModal();
    if (!state.user) { renderToken++; root.innerHTML = loginView(); bindLogin(); return; }
    var r = parseRoute(), token = ++renderToken, role = state.user.role;
    if (r.name !== 'lesson') draft = null;
    if (ALLOWED[role].indexOf(r.name) < 0) { location.replace(HOME[role]); return; }
    var page;
    try { page = await PAGES[r.name](r); }
    catch (e) {
      if (token !== renderToken) return;
      if (e.status === 401) { sessionExpired(); return; }
      page = { title: 'Не удалось загрузить данные', html: '<div class="alert err">' + ic('i-alert') + '<span>' + esc(e.message) + '</span></div>' };
    }
    if (token !== renderToken) return;
    var keepScroll = lastRoute === location.hash, y = window.scrollY;
    lastRoute = location.hash;
    root.innerHTML = shell(r, page);
    state.navOpen = false;
    if (page.after) page.after();
    window.scrollTo(0, keepScroll ? y : 0);
  }
  window.addEventListener('hashchange', function () { if (state.user) render(); });

  function shell(r, page) {
    var u = state.user;
    var nav = NAV[u.role].map(function (n) {
      if (n[0] === 'sec') return '<div class="nav-sec">' + n[1] + '</div>';
      var active = '#/' + r.name === n[0] || (r.name === 'lesson' && n[0] === '#/lessons');
      return '<a data-go="' + n[0] + '" class="' + (active ? 'active' : '') + '" title="' + esc(n[1]) + '">' + ic(n[2]) + '<span>' + n[1] + '</span></a>';
    }).join('');
    nav += '<div class="nav-sec">Профиль</div><a data-go="#/password" class="' + (r.name === 'password' ? 'active' : '') + '" title="Смена пароля">' + ic('i-key') + '<span>Смена пароля</span></a>';
    return '<div class="app" id="app">' +
      '<aside class="side" aria-label="Меню">' +
        '<div class="brand"><div class="brand-mark">' + ic('i-brand') + '</div><div class="brand-name">Учёт посещаемости</div></div>' +
        '<nav class="nav">' + nav + '</nav>' +
        '<div class="side-foot"><div class="me"><div class="avatar">' + esc(initials(u.fullName)) + '</div><div class="me-text"><div class="me-name">' + esc(short(u.fullName)) + '</div><div class="me-role">' + ROLE[u.role] + '</div></div></div>' +
        '<div class="side-actions"><button class="btn btn-ghost btn-sm" data-act="theme" title="Переключить тему">' + ic('i-sun', 'i-sun') + ic('i-moon', 'i-moon') + '<span>Тема</span></button>' +
        '<button class="btn btn-ghost btn-sm" data-act="logout" title="Выйти">' + ic('i-logout') + '<span>Выйти</span></button></div></div>' +
      '</aside><div class="overlay" data-act="close-nav"></div>' +
      '<main class="content"><div class="topbar"><div class="topbar-left"><button class="icon-btn menu-btn" data-act="open-nav" aria-label="Открыть меню">' + ic('i-menu') + '</button><div>' +
        (page.back ? '<button class="back" data-go="' + page.back[0] + '">' + ic('i-chevron-left', 'sm') + page.back[1] + '</button>' : '') +
        '<h1>' + page.title + '</h1>' + (page.sub ? '<div class="sub">' + page.sub + '</div>' : '') + '</div></div>' +
        (page.actions ? '<div class="actions">' + page.actions + '</div>' : '') + '</div>' + page.html + '</main></div>';
  }

  root.addEventListener('click', function (e) {
    var t = e.target.closest('[data-go],[data-act]');
    if (!t) return;
    if (t.hasAttribute('data-go')) { e.preventDefault(); go(t.getAttribute('data-go')); return; }
    var a = t.getAttribute('data-act');
    if (a === 'theme') toggleTheme();
    else if (a === 'logout') {
      var doLogout = async function () {
        try { await api('POST', 'auth/logout'); } catch (err) { /* сессия и так недействительна */ }
        state.user = null; state.dirty = false; draft = null; state.rep = null; state.lessonFilter = 'all'; state.lessonGroup = 'all'; state.cabSubject = 'all'; state.q = ''; state.usersTab = 'users'; state.groupId = null;
        history.replaceState(null, '', location.pathname + location.search); render();
      };
      if (state.dirty) confirmBox('Несохранённые отметки', 'Отметки не сохранены. Выйти без сохранения?', 'Выйти', function () { state.dirty = false; doLogout(); });
      else doLogout();
    }
    else if (a === 'open-nav') document.getElementById('app').classList.add('nav-open');
    else if (a === 'close-nav') document.getElementById('app').classList.remove('nav-open');
    else if (HANDLERS[a]) HANDLERS[a](t, e);
  });
  root.addEventListener('input', function (e) { var t = e.target.closest('[data-input]'); if (t && HANDLERS[t.getAttribute('data-input')]) HANDLERS[t.getAttribute('data-input')](t, e); });
  root.addEventListener('change', function (e) { var t = e.target.closest('[data-change]'); if (t && HANDLERS[t.getAttribute('data-change')]) HANDLERS[t.getAttribute('data-change')](t, e); });

  /* ================= экран: вход ================= */
  function loginView() {
    return '<div class="login"><aside class="l-aside">' +
      '<div class="brand"><div class="brand-mark">' + ic('i-brand') + '</div><div class="brand-name">Учёт посещаемости</div></div>' +
      '<div class="l-main"><h1>Журнал посещаемости студентов в одном месте</h1><p>Преподаватели отмечают занятия, администратор ведёт группы и студентов, студенты видят свою посещаемость.</p>' +
      '<ul class="facts"><li><span class="fact-ic">' + ic('i-clipboard') + '</span><div><b>Отметка за минуту</b><span class="t">Весь список группы на одном экране, статус меняется одним нажатием.</span></div></li>' +
      '<li><span class="fact-ic">' + ic('i-chart') + '</span><div><b>Отчёты по группам</b><span class="t">Процент посещаемости по студенту, группе и предмету за любой период.</span></div></li>' +
      '<li><span class="fact-ic">' + ic('i-user') + '</span><div><b>Личный кабинет студента</b><span class="t">Своя посещаемость с телефона, планшета или компьютера.</span></div></li></ul></div>' +
      '<div class="l-foot">Учебный проект, 2026</div></aside>' +
      '<div class="l-right"><div class="theme-fab">' + themeBtn() + '</div><div class="l-card">' +
      '<h2>Вход в систему</h2><p class="sub">Введите логин и пароль, выданные администратором.</p>' +
      '<form class="l-form" id="loginForm" novalidate>' +
        '<div class="alert err" id="loginErr" role="alert" style="display:none">' + ic('i-alert') + '<span>Неверный логин или пароль</span></div>' +
        '<div class="field"><label for="lg">Логин</label><div class="with-ic">' + ic('i-user') + '<input class="ctl" id="lg" name="login" autocomplete="username" autocapitalize="none" spellcheck="false" placeholder="Ваш логин"></div><div class="err">Введите логин</div></div>' +
        '<div class="field"><label for="pw">Пароль</label><div class="with-ic">' + ic('i-lock') + '<input class="ctl" id="pw" name="password" type="password" autocomplete="current-password" placeholder="Ваш пароль"><button type="button" class="icon-btn" id="pwToggle" aria-label="Показать пароль" aria-pressed="false">' + ic('i-eye') + '</button></div><div class="err">Введите пароль</div></div>' +
        '<button class="btn btn-primary" type="submit" style="width:100%;min-height:46px">Войти</button></form>' +
      '<div class="hint">' + ic('i-alert') + '<span>Учётную запись выдаёт администратор. Если не можете войти, обратитесь к нему.</span></div>' +
      '</div></div></div>';
  }
  function bindLogin() {
    var f = document.getElementById('loginForm'), lg = f.elements.login, pw = f.elements.password, err = document.getElementById('loginErr');
    document.getElementById('pwToggle').addEventListener('click', function () {
      var show = pw.type === 'password'; pw.type = show ? 'text' : 'password';
      this.setAttribute('aria-pressed', show ? 'true' : 'false'); this.setAttribute('aria-label', show ? 'Скрыть пароль' : 'Показать пароль');
    });
    f.addEventListener('submit', async function (e) {
      e.preventDefault();
      f.querySelectorAll('.field').forEach(function (x) { x.classList.remove('invalid'); });
      err.style.display = 'none';
      var l = lg.value.trim().toLowerCase(), p = pw.value, bad = false;
      if (!l) { lg.closest('.field').classList.add('invalid'); bad = true; }
      if (!p) { pw.closest('.field').classList.add('invalid'); bad = true; }
      if (bad) return;
      try { state.user = await api('POST', 'auth/login', { login: l, password: p }); }
      catch (ex) { err.querySelector('span').textContent = ex.status === 401 ? 'Неверный логин или пароль' : ex.message; err.style.display = 'flex'; if (ex.status === 401) pw.select(); return; }
      if (location.hash === HOME[state.user.role]) render(); else location.hash = HOME[state.user.role];
    });
  }

  /* ================= экран: группы и студенты (admin) ================= */
  async function pageGroups() {
    var groups = await api('GET', 'groups');
    if (!groups.some(function (g) { return g.id === state.groupId; })) state.groupId = groups.length ? groups[0].id : null;
    var q = state.q.trim();
    var res = await Promise.all([
      Promise.all(groups.map(function (g) { return api('GET', 'reports/group/' + g.id); })),
      q ? api('GET', 'students?q=' + encodeURIComponent(q)) : (state.groupId ? api('GET', 'students?groupId=' + state.groupId) : Promise.resolve([]))
    ]);
    var pctGroup = {}, pctStudent = {};
    res[0].forEach(function (r) { pctGroup[r.groupId] = r.total.percent; r.students.forEach(function (s) { pctStudent[s.studentId] = s.summary.percent; }); });
    var rows = sortRu(res[1], 'fullName'), g = groups.filter(function (x) { return x.id === state.groupId; })[0] || null;
    cache.groups = groups; cache.students = rows;
    var studentsTotal = groups.reduce(function (n, x) { return n + x.studentCount; }, 0);

    var list = groups.map(function (x) {
      return '<li><button data-act="pick-group" data-id="' + x.id + '" class="' + (x.id === state.groupId && !q ? 'active' : '') + '"><span><span class="gname">' + esc(x.name) + '</span><br><span class="gmeta">Набор ' + x.startYear + ', ' + x.studentCount + ' студ.</span></span><span class="chip accent">' + pctText(pctGroup[x.id]) + '</span></button></li>';
    }).join('');
    var body = rows.map(function (s) {
      return '<tr><td data-label="Студент"><div class="cell-main">' + esc(s.fullName) + '</div>' + (q ? '<div class="cell-sub">' + esc(s.groupName) + '</div>' : '') + '</td>' +
        '<td data-label="Учётная запись">' + (s.userId ? '<span class="mono">' + esc(s.login) + '</span>' + (s.isActive ? '' : ' <span class="chip neutral">отключена</span>') : '<span class="chip neutral">нет</span>') + '</td>' +
        '<td data-label="Посещаемость" class="num">' + bar(pctStudent[s.id]) + '</td>' +
        '<td class="act"><button class="icon-btn" data-act="edit-student" data-id="' + s.id + '" title="Изменить" aria-label="Изменить">' + ic('i-edit') + '</button>' +
        '<button class="icon-btn" data-act="student-account" data-id="' + s.id + '" title="Учётная запись" aria-label="Учётная запись">' + ic('i-key') + '</button>' +
        '<button class="icon-btn danger" data-act="del-student" data-id="' + s.id + '" title="Удалить" aria-label="Удалить">' + ic('i-trash') + '</button></td></tr>';
    }).join('');
    var head = q ? '<h3>Поиск: найдено ' + rows.length + '</h3>' :
      (g ? '<div><h3>' + esc(g.name) + '</h3><div class="cell-sub">Набор ' + g.startYear + ', ' + rows.length + ' студентов</div></div>' +
        '<div class="actions"><button class="btn btn-ghost btn-sm" data-act="edit-group" data-id="' + g.id + '">' + ic('i-edit') + 'Изменить</button><button class="btn btn-ghost btn-sm" data-act="del-group" data-id="' + g.id + '">' + ic('i-trash') + 'Удалить</button></div>' : '<h3>Нет групп</h3>');
    return {
      title: 'Группы и студенты', sub: groups.length + ' групп, ' + studentsTotal + ' студентов',
      actions: '<button class="btn btn-ghost" data-act="add-group">' + ic('i-plus') + 'Группа</button><button class="btn btn-primary" data-act="add-student"' + (groups.length ? '' : ' disabled') + '>' + ic('i-plus') + 'Студент</button>',
      html: '<div class="split"><div class="card"><div class="card-head"><h3>Группы</h3></div><ul class="glist">' + (list || '<li class="empty">Групп нет</li>') + '</ul></div>' +
        '<div class="card"><div class="toolbar"><div class="search grow">' + ic('i-search') + '<input class="ctl" type="search" placeholder="Поиск студента по ФИО во всех группах" value="' + esc(state.q) + '" data-input="search" aria-label="Поиск студента"></div></div>' +
        '<div class="card-head">' + head + '</div>' +
        (rows.length ? '<div class="tbl-wrap"><table class="t resp"><thead><tr><th>Студент</th><th>Учётная запись</th><th class="num">Посещаемость</th><th></th></tr></thead><tbody>' + body + '</tbody></table></div>'
          : '<div class="empty">' + ic('i-users') + '<b>' + (q ? 'Никого не нашли' : 'В группе пока нет студентов') + '</b>' + (q ? 'Проверьте написание ФИО' : 'Добавьте студента кнопкой «Студент»') + '</div>') + '</div></div>'
    };
  }
  HANDLERS['pick-group'] = function (t) { state.groupId = Number(t.getAttribute('data-id')); state.q = ''; render(); };
  var searchTimer;
  HANDLERS.search = function (t) {
    clearTimeout(searchTimer);
    var v = t.value;
    searchTimer = setTimeout(async function () {
      state.q = v; await render();
      var s = root.querySelector('[data-input="search"]'); if (s) { s.focus(); s.setSelectionRange(s.value.length, s.value.length); }
    }, 250);
  };

  function groupForm(g) {
    return '<div class="form-grid">' + fld('Название', 'name', inp('name', g ? g.name : '', 'text', ' placeholder="Например, ИС-24" maxlength="50"')) +
      fld('Год набора', 'start_year', inp('start_year', g ? g.startYear : new Date().getFullYear(), 'number', ' min="2000" max="2100"')) + '</div>';
  }
  async function saveGroup(form, g) {
    var name = form.elements.name.value.trim(), year = Number(form.elements.start_year.value), ok = true;
    if (!name) ok = invalid(form, 'name');
    if (!year || year < 2000 || year > 2100) ok = invalid(form, 'start_year', 'Год от 2000 до 2100');
    if (!ok) return false;
    var body = { name: name, startYear: year };
    if (g) await api('PUT', 'groups/' + g.id, body);
    else state.groupId = (await api('POST', 'groups', body)).id, state.q = '';
    toast(g ? 'Группа сохранена' : 'Группа добавлена'); await render(); return true;
  }
  HANDLERS['add-group'] = function () { modal({ title: 'Новая группа', body: groupForm(null), submit: 'Добавить', onSubmit: function (f) { return saveGroup(f, null); } }); };
  HANDLERS['edit-group'] = function (t) { var g = byId(cache.groups, Number(t.getAttribute('data-id'))); modal({ title: 'Группа ' + g.name, body: groupForm(g), submit: 'Сохранить', onSubmit: function (f) { return saveGroup(f, g); } }); };
  HANDLERS['del-group'] = function (t) {
    var g = byId(cache.groups, Number(t.getAttribute('data-id')));
    confirmBox('Удалить группу?', 'Группа ' + g.name + ' будет удалена без возможности восстановления. Если в ней есть студенты, назначения или занятия, система откажет.', 'Удалить', function () {
      act(function () { return api('DELETE', 'groups/' + g.id); }, 'Группа удалена', 'i-trash', 'Нельзя удалить группу');
    });
  };

  function studentForm(s) {
    var gopts = cache.groups.map(function (g) { return [g.id, g.name]; });
    var html = '<div class="form-grid"><div class="full">' + fld('ФИО', 'full_name', inp('full_name', s ? s.fullName : '', 'text', ' placeholder="Фамилия Имя Отчество" maxlength="200"')) + '</div>' +
      '<div class="full">' + fld('Группа', 'group_id', sel('group_id', gopts, s ? s.groupId : state.groupId)) + '</div></div>';
    if (!s) html += '<label class="check"><input type="checkbox" name="make_account" checked> Сразу создать учётную запись (логин и временный пароль)</label>';
    return html;
  }
  async function createStudentAccount(student) {
    var base = translit(student.fullName) || 'student';
    if (base.length < 3) base += 'student';
    for (var i = 1; i <= 30; i++) {
      var login = i === 1 ? base : base + i;
      try { return await api('POST', 'students/' + student.id + '/account', { login: login }); }
      catch (e) { if (e.status !== 409 || !/логин/i.test(e.message)) throw e; }
    }
    throw new Error('Не удалось подобрать свободный логин');
  }
  HANDLERS['add-student'] = function () {
    modal({ title: 'Новый студент', body: studentForm(null), submit: 'Добавить', onSubmit: async function (f) {
      var n = f.elements.full_name.value.trim().replace(/\s+/g, ' ');
      if (!n) return invalid(f, 'full_name');
      if (n.split(' ').length < 2) return invalid(f, 'full_name', 'Укажите хотя бы фамилию и имя');
      var s = await api('POST', 'students', { fullName: n, groupId: Number(f.elements.group_id.value) });
      state.groupId = s.groupId; state.q = '';
      if (!f.elements.make_account.checked) { toast('Студент добавлен'); await render(); return true; }
      var cred;
      try { cred = await createStudentAccount(s); }
      catch (e) { await render(); return 'Студент добавлен, но учётную запись создать не удалось: ' + e.message; }
      await render(); showCredentials(s.fullName, cred.login, cred.temporaryPassword, 'Учётная запись создана');
    } });
  };
  HANDLERS['edit-student'] = function (t) {
    var s = byId(cache.students, Number(t.getAttribute('data-id')));
    modal({ title: 'Студент', body: studentForm(s), submit: 'Сохранить', onSubmit: async function (f) {
      var n = f.elements.full_name.value.trim().replace(/\s+/g, ' ');
      if (!n) return invalid(f, 'full_name');
      await api('PUT', 'students/' + s.id, { fullName: n, groupId: Number(f.elements.group_id.value) });
      toast('Изменения сохранены'); await render(); return true;
    } });
  };
  HANDLERS['del-student'] = function (t) {
    var s = byId(cache.students, Number(t.getAttribute('data-id')));
    confirmBox('Удалить студента?', s.fullName + (s.userId ? ' будет удалён вместе с учётной записью.' : ' будет удалён.') + ' Если у студента есть отметки посещаемости, система откажет: история не удаляется.', 'Удалить', function () {
      act(function () { return api('DELETE', 'students/' + s.id); }, 'Студент удалён', 'i-trash', 'Нельзя удалить студента');
    });
  };
  HANDLERS['student-account'] = function (t) {
    var s = byId(cache.students, Number(t.getAttribute('data-id')));
    if (!s.userId) {
      modal({ title: 'Учётная запись', body: '<p>У студента <b>' + esc(s.fullName) + '</b> нет учётной записи, он не может войти в кабинет.</p>', submit: 'Создать учётную запись', onSubmit: async function () {
        var cred = await createStudentAccount(s); await render(); showCredentials(s.fullName, cred.login, cred.temporaryPassword, 'Учётная запись создана');
      } });
      return;
    }
    modal({ title: 'Учётная запись', body: '<div class="field"><div class="lbl">Логин</div><div class="secret"><span>' + esc(s.login) + '</span>' + (s.isActive ? '<span class="chip present">активна</span>' : '<span class="chip neutral">отключена</span>') + '</div></div>' +
      '<div class="actions"><button type="button" class="btn btn-ghost" data-m="reset">' + ic('i-key') + 'Сбросить пароль</button><button type="button" class="btn btn-ghost" data-m="toggle">' + ic('i-power') + (s.isActive ? 'Отключить' : 'Включить') + '</button></div>',
      onOpen: function (el) {
        el.querySelector('[data-m="reset"]').addEventListener('click', async function () {
          try { var r = await api('POST', 'users/' + s.userId + '/reset-password'); await render(); showCredentials(s.fullName, s.login, r.temporaryPassword, 'Пароль сброшен'); } catch (e) { fail(e); }
        });
        el.querySelector('[data-m="toggle"]').addEventListener('click', async function () {
          try { await api('PUT', 'users/' + s.userId + '/active', { isActive: !s.isActive }); toast(s.isActive ? 'Учётная запись отключена' : 'Учётная запись включена'); await render(); } catch (e) { fail(e); }
        });
      } });
  };

  /* ================= экран: предметы (admin) ================= */
  async function pageSubjects() {
    var res = await Promise.all([api('GET', 'subjects'), api('GET', 'assignments'), api('GET', 'lessons')]);
    var subjects = sortRu(res[0], 'name'); cache.subjects = subjects;
    var rows = subjects.map(function (s) {
      var as = res[1].filter(function (a) { return a.subjectId === s.id; }).length, ls = res[2].filter(function (l) { return l.subjectId === s.id; }).length;
      return '<tr><td data-label="Предмет"><span class="cell-main">' + esc(s.name) + '</span></td><td data-label="Назначений" class="num">' + as + '</td><td data-label="Занятий" class="num">' + ls + '</td>' +
        '<td class="act"><button class="icon-btn" data-act="edit-subject" data-id="' + s.id + '" aria-label="Изменить" title="Изменить">' + ic('i-edit') + '</button><button class="icon-btn danger" data-act="del-subject" data-id="' + s.id + '" aria-label="Удалить" title="Удалить">' + ic('i-trash') + '</button></td></tr>';
    }).join('');
    return {
      title: 'Предметы', sub: subjects.length + ' предметов',
      actions: '<button class="btn btn-primary" data-act="add-subject">' + ic('i-plus') + 'Предмет</button>',
      html: '<div class="card">' + (rows ? '<div class="tbl-wrap"><table class="t resp"><thead><tr><th>Предмет</th><th class="num">Назначений</th><th class="num">Занятий</th><th></th></tr></thead><tbody>' + rows + '</tbody></table></div>' : '<div class="empty">' + ic('i-book') + '<b>Предметов нет</b>Добавьте первый предмет</div>') + '</div>'
    };
  }
  async function saveSubject(f, s) {
    var n = f.elements.name.value.trim();
    if (!n) return invalid(f, 'name');
    if (s) await api('PUT', 'subjects/' + s.id, { name: n }); else await api('POST', 'subjects', { name: n });
    toast(s ? 'Предмет сохранён' : 'Предмет добавлен'); await render(); return true;
  }
  HANDLERS['add-subject'] = function () { modal({ title: 'Новый предмет', body: fld('Название', 'name', inp('name', '', 'text', ' maxlength="200" placeholder="Например, Операционные системы"')), submit: 'Добавить', onSubmit: function (f) { return saveSubject(f, null); } }); };
  HANDLERS['edit-subject'] = function (t) { var s = byId(cache.subjects, Number(t.getAttribute('data-id'))); modal({ title: 'Предмет', body: fld('Название', 'name', inp('name', s.name, 'text', ' maxlength="200"')), submit: 'Сохранить', onSubmit: function (f) { return saveSubject(f, s); } }); };
  HANDLERS['del-subject'] = function (t) {
    var s = byId(cache.subjects, Number(t.getAttribute('data-id')));
    confirmBox('Удалить предмет?', 'Предмет «' + s.name + '» будет удалён. Если по нему есть назначения или занятия, система откажет.', 'Удалить', function () {
      act(function () { return api('DELETE', 'subjects/' + s.id); }, 'Предмет удалён', 'i-trash', 'Нельзя удалить предмет');
    });
  };

  /* ================= экран: пользователи и назначения (admin) ================= */
  async function pageUsers() {
    var tabs = '<div class="tabs" role="tablist"><button data-act="users-tab" data-tab="users" class="' + (state.usersTab === 'users' ? 'active' : '') + '">Пользователи</button><button data-act="users-tab" data-tab="assign" class="' + (state.usersTab === 'assign' ? 'active' : '') + '">Назначения преподавателей</button></div>';
    var html, actions, sub;
    if (state.usersTab === 'users') {
      var res = await Promise.all([api('GET', 'users'), api('GET', 'students')]);
      var users = res[0], order = { admin: 0, teacher: 1, student: 2 };
      cache.users = users; sub = users.length + ' учётных записей';
      var rows = users.slice().sort(function (a, b) { return order[a.role] - order[b.role] || a.fullName.localeCompare(b.fullName, 'ru'); }).map(function (u) {
        var st = u.role === 'student' ? res[1].filter(function (s) { return s.userId === u.id; })[0] : null;
        return '<tr><td data-label="Пользователь"><div class="cell-main">' + esc(u.fullName) + '</div>' + (st ? '<div class="cell-sub">Группа ' + esc(st.groupName) + '</div>' : '') + '</td>' +
          '<td data-label="Логин"><span class="mono">' + esc(u.login) + '</span></td><td data-label="Роль"><span class="chip ' + (u.role === 'admin' ? 'accent' : 'neutral') + '">' + ROLE[u.role] + '</span></td>' +
          '<td data-label="Статус">' + (u.isActive ? '<span class="chip present">' + ic('i-present') + 'Активен</span>' : '<span class="chip neutral">Отключён</span>') + '</td>' +
          '<td class="act">' + (u.role !== 'student' ? '<button class="icon-btn" data-act="edit-user" data-id="' + u.id + '" title="Изменить" aria-label="Изменить">' + ic('i-edit') + '</button>' : '') +
          '<button class="icon-btn" data-act="reset-user" data-id="' + u.id + '" title="Сбросить пароль" aria-label="Сбросить пароль">' + ic('i-key') + '</button>' +
          '<button class="icon-btn" data-act="toggle-user" data-id="' + u.id + '" title="' + (u.isActive ? 'Отключить' : 'Включить') + '" aria-label="' + (u.isActive ? 'Отключить' : 'Включить') + '">' + ic('i-power') + '</button>' +
          '<button class="icon-btn danger" data-act="del-user" data-id="' + u.id + '" title="Удалить" aria-label="Удалить">' + ic('i-trash') + '</button></td></tr>';
      }).join('');
      actions = '<button class="btn btn-primary" data-act="add-user">' + ic('i-plus') + 'Сотрудник</button>';
      html = '<div class="alert info" style="margin-bottom:var(--sp-4)">' + ic('i-alert') + '<span>Учётные записи студентов создаются в разделе «Группы и студенты». Здесь добавляются администраторы и преподаватели.</span></div>' +
        '<div class="card"><div class="tbl-wrap"><table class="t resp"><thead><tr><th>Пользователь</th><th>Логин</th><th>Роль</th><th>Статус</th><th></th></tr></thead><tbody>' + rows + '</tbody></table></div></div>';
    } else {
      var r2 = await Promise.all([api('GET', 'assignments'), api('GET', 'lessons')]);
      cache.assignments = r2[0]; sub = r2[0].length + ' назначений';
      var arows = r2[0].map(function (a) {
        var ls = r2[1].filter(function (l) { return l.teacherId === a.teacherId && l.groupId === a.groupId && l.subjectId === a.subjectId; }).length;
        return '<tr><td data-label="Преподаватель"><span class="cell-main">' + esc(a.teacherName) + '</span></td><td data-label="Группа">' + esc(a.groupName) + '</td><td data-label="Предмет">' + esc(a.subjectName) + '</td><td data-label="Занятий" class="num">' + ls + '</td>' +
          '<td class="act"><button class="icon-btn danger" data-act="del-assign" data-id="' + a.id + '" title="Удалить" aria-label="Удалить">' + ic('i-trash') + '</button></td></tr>';
      }).join('');
      actions = '<button class="btn btn-primary" data-act="add-assign">' + ic('i-plus') + 'Назначение</button>';
      html = '<div class="alert info" style="margin-bottom:var(--sp-4)">' + ic('i-alert') + '<span>Преподаватель видит только группы и предметы из своих назначений.</span></div>' +
        '<div class="card">' + (arows ? '<div class="tbl-wrap"><table class="t resp"><thead><tr><th>Преподаватель</th><th>Группа</th><th>Предмет</th><th class="num">Занятий</th><th></th></tr></thead><tbody>' + arows + '</tbody></table></div>' : '<div class="empty">' + ic('i-shield') + '<b>Назначений нет</b>Добавьте первое назначение</div>') + '</div>';
    }
    return { title: 'Пользователи и назначения', sub: sub, actions: actions, html: tabs + html };
  }
  HANDLERS['users-tab'] = function (t) { state.usersTab = t.getAttribute('data-tab'); render(); };
  function userForm(u) {
    return '<div class="form-grid"><div class="full">' + fld('ФИО', 'full_name', inp('full_name', u ? u.fullName : '', 'text', ' maxlength="200"')) + '</div>' +
      fld('Логин', 'login', inp('login', u ? u.login : '', 'text', ' maxlength="50" autocapitalize="none"' + (u ? ' disabled' : '')), u ? 'Логин не меняется' : 'Латиница и цифры, от 3 символов') +
      fld('Роль', 'role', sel('role', [['teacher', 'Преподаватель'], ['admin', 'Администратор']], u ? u.role : 'teacher')) + '</div>' +
      (u ? '' : '<div class="alert info">' + ic('i-alert') + '<span>Временный пароль будет создан автоматически и показан после сохранения.</span></div>');
  }
  async function saveUser(f, u) {
    var n = f.elements.full_name.value.trim(), role = f.elements.role.value, ok = true;
    if (!n) ok = invalid(f, 'full_name');
    var l = u ? u.login : f.elements.login.value.trim().toLowerCase();
    if (!/^[a-z0-9._-]{3,50}$/.test(l)) ok = invalid(f, 'login', 'Латиница, цифры, точка, дефис; от 3 символов');
    if (!ok) return false;
    if (u) { await api('PUT', 'users/' + u.id, { fullName: n, role: role }); toast('Изменения сохранены'); await render(); return true; }
    var r = await api('POST', 'users', { login: l, fullName: n, role: role });
    await render(); showCredentials(r.user.fullName, r.user.login, r.temporaryPassword, 'Сотрудник добавлен');
  }
  HANDLERS['add-user'] = function () { modal({ title: 'Новый сотрудник', body: userForm(null), submit: 'Добавить', onSubmit: function (f) { return saveUser(f, null); } }); };
  HANDLERS['edit-user'] = function (t) { var u = byId(cache.users, Number(t.getAttribute('data-id'))); modal({ title: 'Пользователь', body: userForm(u), submit: 'Сохранить', onSubmit: function (f) { return saveUser(f, u); } }); };
  HANDLERS['reset-user'] = function (t) {
    var u = byId(cache.users, Number(t.getAttribute('data-id')));
    confirmBox('Сбросить пароль?', 'Для ' + u.fullName + ' будет создан новый временный пароль. Старый перестанет работать.', 'Сбросить', async function () {
      try { var r = await api('POST', 'users/' + u.id + '/reset-password'); showCredentials(u.fullName, u.login, r.temporaryPassword, 'Пароль сброшен'); } catch (e) { fail(e); }
    });
  };
  HANDLERS['toggle-user'] = function (t) {
    var u = byId(cache.users, Number(t.getAttribute('data-id')));
    act(function () { return api('PUT', 'users/' + u.id + '/active', { isActive: !u.isActive }); }, u.isActive ? 'Учётная запись отключена' : 'Учётная запись включена', null, 'Действие недоступно');
  };
  HANDLERS['del-user'] = function (t) {
    var u = byId(cache.users, Number(t.getAttribute('data-id')));
    confirmBox('Удалить пользователя?', u.fullName + ' (' + u.login + ') будет удалён. Если есть связанные данные, система откажет: отключите учётную запись вместо удаления.', 'Удалить', function () {
      act(function () { return api('DELETE', 'users/' + u.id); }, 'Пользователь удалён', 'i-trash', 'Нельзя удалить пользователя');
    });
  };
  HANDLERS['add-assign'] = async function () {
    try {
      var res = await Promise.all([api('GET', 'users?role=teacher'), api('GET', 'groups'), api('GET', 'subjects')]);
      var teachers = res[0].filter(function (u) { return u.isActive; }).map(function (u) { return [u.id, u.fullName]; });
      if (!teachers.length || !res[1].length || !res[2].length) { info('Не хватает данных', 'Для назначения нужны активный преподаватель, группа и предмет.'); return; }
      modal({ title: 'Новое назначение', body: fld('Преподаватель', 'teacher_id', sel('teacher_id', teachers)) +
        '<div class="form-grid">' + fld('Группа', 'group_id', sel('group_id', res[1].map(function (g) { return [g.id, g.name]; }))) + fld('Предмет', 'subject_id', sel('subject_id', res[2].map(function (s) { return [s.id, s.name]; }))) + '</div>',
        submit: 'Добавить', onSubmit: async function (f) {
          await api('POST', 'assignments', { teacherId: Number(f.elements.teacher_id.value), groupId: Number(f.elements.group_id.value), subjectId: Number(f.elements.subject_id.value) });
          toast('Назначение добавлено'); await render(); return true;
        } });
    } catch (e) { fail(e); }
  };
  HANDLERS['del-assign'] = function (t) {
    var a = byId(cache.assignments, Number(t.getAttribute('data-id')));
    confirmBox('Удалить назначение?', a.teacherName + ': ' + a.groupName + ', ' + a.subjectName + '. Если по нему уже проведены занятия, система откажет.', 'Удалить', function () {
      act(function () { return api('DELETE', 'assignments/' + a.id); }, 'Назначение удалено', 'i-trash', 'Нельзя удалить назначение');
    });
  };

  /* ================= экран: занятия (teacher — свои, admin — все) ================= */
  async function pageLessons() {
    var isAdmin = state.user.role === 'admin';
    var res = await Promise.all([isAdmin ? Promise.resolve([]) : api('GET', 'my/assignments'), api('GET', 'lessons'), api('GET', 'groups')]);
    var my = res[0], lessons = res[1], groups = res[2];
    cache.myAssignments = my; cache.lessons = lessons; cache.groups = groups;
    var size = {}; groups.forEach(function (g) { size[g.id] = g.studentCount; });

    var fa = (!isAdmin && state.lessonFilter !== 'all') ? byId(my, state.lessonFilter) : null;
    if (!isAdmin && state.lessonFilter !== 'all' && !fa) state.lessonFilter = 'all';
    var list = lessons.filter(function (l) {
      if (isAdmin) return state.lessonGroup === 'all' || l.groupId === state.lessonGroup;
      return !fa || (l.groupId === fa.groupId && l.subjectId === fa.subjectId);
    });
    var cards = '';
    if (!isAdmin && my.length) {
      var pcts = await Promise.all(my.map(function (a) { return api('GET', 'reports/group/' + a.groupId + '?subjectId=' + a.subjectId); }));
      cards = my.map(function (a, i) {
        var n = lessons.filter(function (l) { return l.groupId === a.groupId && l.subjectId === a.subjectId; }).length;
        return '<button class="acard' + (state.lessonFilter === a.id ? ' active' : '') + '" data-act="lesson-filter" data-id="' + a.id + '"><div class="g">' + esc(a.groupName) + '</div><div class="s">' + esc(a.subjectName) + '</div><div class="row"><span class="s">' + n + ' занятий</span><span class="pct">' + pctText(pcts[i].total.percent) + '</span></div></button>';
      }).join('');
    }
    var rows = list.map(function (l) {
      var stt = l.markCount ? '<span class="chip present">' + ic('i-check') + 'Отмечено ' + l.markCount + '/' + size[l.groupId] + '</span>' : '<span class="chip late">' + ic('i-alert') + 'Не отмечено</span>';
      var btns = isAdmin
        ? '<button class="btn btn-ghost btn-sm" data-go="#/lesson/' + l.id + '">' + ic('i-clipboard') + 'Исправить</button>'
        : '<button class="btn btn-' + (l.markCount ? 'ghost' : 'primary') + ' btn-sm" data-go="#/lesson/' + l.id + '">' + ic('i-clipboard') + (l.markCount ? 'Изменить' : 'Отметить') + '</button>' +
          '<button class="icon-btn" data-act="edit-lesson" data-id="' + l.id + '" title="Изменить занятие" aria-label="Изменить занятие">' + ic('i-edit') + '</button>' +
          '<button class="icon-btn danger" data-act="del-lesson" data-id="' + l.id + '" title="Удалить занятие" aria-label="Удалить занятие">' + ic('i-trash') + '</button>';
      return '<tr class="click" data-go="#/lesson/' + l.id + '"><td data-label="Дата"><div class="cell-main">' + fmtDate(l.date) + '</div><div class="cell-sub">' + wd(l.date) + ', ' + l.pairNumber + ' пара</div></td>' +
        '<td data-label="Группа">' + esc(l.groupName) + '</td><td data-label="Предмет"><div>' + esc(l.subjectName) + '</div><div class="cell-sub">' + esc(l.topic || 'Тема не указана') + (isAdmin ? ' · ' + esc(short(l.teacherName)) : '') + '</div></td>' +
        '<td data-label="Посещаемость">' + stt + '</td><td class="act">' + btns + '</td></tr>';
    }).join('');
    var unmarked = lessons.filter(function (l) { return !l.markCount; }).length;
    var top;
    if (isAdmin) {
      top = '<div class="card"><div class="toolbar" style="border-bottom:0"><div class="field"><label for="f-lg">Группа</label>' + sel('lg', [['all', 'Все группы']].concat(groups.map(function (g) { return [g.id, g.name]; })), state.lessonGroup).replace('<select', '<select data-change="lesson-group"') + '</div></div></div>';
    } else if (my.length) {
      top = '<div class="acards"><button class="acard' + (state.lessonFilter === 'all' ? ' active' : '') + '" data-act="lesson-filter" data-id="all"><div class="g">Все</div><div class="s">Все группы и предметы</div><div class="row"><span class="s">' + lessons.length + ' занятий</span></div></button>' + cards + '</div>';
    } else {
      top = '<div class="alert warn">' + ic('i-alert') + '<span>У вас нет назначений. Обратитесь к администратору.</span></div>';
    }
    return {
      title: isAdmin ? 'Занятия и отметки' : 'Мои занятия',
      sub: isAdmin ? lessons.length + ' занятий, без отметок: ' + unmarked : my.length + ' назначений' + (unmarked ? ', без отметки: ' + unmarked : ''),
      actions: isAdmin ? '' : '<button class="btn btn-primary" data-act="add-lesson"' + (my.length ? '' : ' disabled') + '>' + ic('i-plus') + 'Занятие</button>',
      html: '<div class="stack">' + top +
        '<div class="card"><div class="card-head"><h3>' + (fa ? esc(fa.groupName + ', ' + fa.subjectName) : 'Все занятия') + '</h3><span class="muted" style="font-size:13px">' + list.length + ' занятий</span></div>' +
        (rows ? '<div class="tbl-wrap"><table class="t resp"><thead><tr><th>Дата</th><th>Группа</th><th>Предмет и тема</th><th>Посещаемость</th><th></th></tr></thead><tbody>' + rows + '</tbody></table></div>' : '<div class="empty">' + ic('i-calendar') + '<b>Занятий пока нет</b>' + (isAdmin ? '' : 'Создайте занятие кнопкой «Занятие»') + '</div>') + '</div></div>'
    };
  }
  HANDLERS['lesson-filter'] = function (t) { var v = t.getAttribute('data-id'); state.lessonFilter = v === 'all' ? 'all' : Number(v); render(); };
  HANDLERS['lesson-group'] = function (t) { state.lessonGroup = t.value === 'all' ? 'all' : Number(t.value); render(); };

  function pairOptions() { var p = []; for (var i = 1; i <= 8; i++) p.push([i, i + ' пара']); return p; }
  function groupChecks(subjectId, preselect) {
    var gs = cache.myAssignments.filter(function (a) { return a.subjectId === Number(subjectId); });
    return gs.map(function (a) { return '<label class="check"><input type="checkbox" name="g" value="' + a.groupId + '"' + (preselect === a.groupId || gs.length === 1 ? ' checked' : '') + '> ' + esc(a.groupName) + '</label>'; }).join('');
  }
  HANDLERS['add-lesson'] = function () {
    var my = cache.myAssignments;
    if (!my.length) { info('Нет назначений', 'Создать занятие можно только по своему назначению. Обратитесь к администратору.'); return; }
    var subjects = uniqBy(my, 'subjectId').map(function (a) { return [a.subjectId, a.subjectName]; });
    var fa = state.lessonFilter !== 'all' ? byId(my, state.lessonFilter) : null;
    var firstSubject = fa ? fa.subjectId : subjects[0][0];
    modal({ title: 'Новое занятие', body: '<div class="form-grid"><div class="full">' + fld('Предмет', 'subject_id', sel('subject_id', subjects, firstSubject)) + '</div>' +
      '<div class="full field"><div class="lbl">Группы (можно выбрать несколько для лекции потоку)</div><div id="gChecks" class="stack" style="gap:var(--sp-2)">' + groupChecks(firstSubject, fa ? fa.groupId : null) + '</div><div class="err" id="gErr">Выберите хотя бы одну группу</div></div>' +
      fld('Дата', 'lesson_date', inp('lesson_date', today(), 'date', ' max="' + today() + '"')) + fld('Пара', 'pair_number', sel('pair_number', pairOptions(), 1)) +
      '<div class="full">' + fld('Тема', 'topic', inp('topic', '', 'text', ' maxlength="500" placeholder="Необязательно"')) + '</div></div>',
      submit: 'Создать и отметить',
      onOpen: function (el) { el.querySelector('[name=subject_id]').addEventListener('change', function () { el.querySelector('#gChecks').innerHTML = groupChecks(this.value, null); }); },
      onSubmit: async function (f) {
        var d = f.elements.lesson_date.value, ids = Array.prototype.map.call(f.querySelectorAll('[name=g]:checked'), function (x) { return Number(x.value); });
        if (!ids.length) { var gf = document.getElementById('gErr').closest('.field'); gf.classList.add('invalid'); return false; }
        if (!d) return invalid(f, 'lesson_date', 'Укажите дату');
        if (d > today()) return invalid(f, 'lesson_date', 'Дата не может быть в будущем');
        var created = await api('POST', 'lessons', { date: d, pairNumber: Number(f.elements.pair_number.value), subjectId: Number(f.elements.subject_id.value), groupIds: ids, topic: f.elements.topic.value.trim() || null });
        closeModal();
        if (created.length === 1) { toast('Занятие создано, отметьте посещаемость'); go('#/lesson/' + created[0].id); }
        else { toast('Создано занятий: ' + created.length); await render(); }
        return true;
      } });
  };
  HANDLERS['edit-lesson'] = function (t, e) {
    e.stopPropagation();
    var l = byId(cache.lessons, Number(t.getAttribute('data-id')));
    var subjects = cache.myAssignments.filter(function (a) { return a.groupId === l.groupId; }).map(function (a) { return [a.subjectId, a.subjectName]; });
    modal({ title: 'Занятие', body: '<div class="form-grid"><div class="full"><div class="lbl" style="font-size:13px;font-weight:600">Группа</div><div class="cell-main">' + esc(l.groupName) + '</div></div>' +
      '<div class="full">' + fld('Предмет', 'subject_id', sel('subject_id', subjects, l.subjectId)) + '</div>' +
      fld('Дата', 'lesson_date', inp('lesson_date', l.date, 'date', ' max="' + today() + '"')) + fld('Пара', 'pair_number', sel('pair_number', pairOptions(), l.pairNumber)) +
      '<div class="full">' + fld('Тема', 'topic', inp('topic', l.topic || '', 'text', ' maxlength="500" placeholder="Необязательно"')) + '</div></div>',
      submit: 'Сохранить', onSubmit: async function (f) {
        var d = f.elements.lesson_date.value;
        if (!d) return invalid(f, 'lesson_date', 'Укажите дату');
        if (d > today()) return invalid(f, 'lesson_date', 'Дата не может быть в будущем');
        await api('PUT', 'lessons/' + l.id, { date: d, pairNumber: Number(f.elements.pair_number.value), subjectId: Number(f.elements.subject_id.value), topic: f.elements.topic.value.trim() || null });
        toast('Занятие сохранено'); await render(); return true;
      } });
  };
  HANDLERS['del-lesson'] = function (t, e) {
    e.stopPropagation();
    var l = byId(cache.lessons, Number(t.getAttribute('data-id')));
    confirmBox('Удалить занятие?', l.groupName + ', ' + l.subjectName + ', ' + fmtDate(l.date) + '. Если по занятию есть отметки, система откажет.', 'Удалить', function () {
      act(function () { return api('DELETE', 'lessons/' + l.id); }, 'Занятие удалено', 'i-trash', 'Нельзя удалить занятие');
    });
  };

  /* ================= экран: отметка посещаемости ================= */
  async function pageLesson(r) {
    var data;
    try { data = await api('GET', 'lessons/' + r.id + '/attendance'); }
    catch (e) {
      if (e.status === 404 || e.status === 403) return { title: 'Занятие не найдено', back: ['#/lessons', 'К занятиям'], html: '<div class="card"><div class="empty">' + ic('i-alert') + '<b>Занятие недоступно</b>Оно удалено или относится к другому преподавателю.</div></div>' };
      throw e;
    }
    var l = data.lesson, studs = sortRu(data.students, 'fullName'), isAdmin = state.user.role === 'admin';
    if (!draft || draft.lessonId !== l.id) {
      draft = { lessonId: l.id, st: {}, note: {} };
      studs.forEach(function (s) { draft.st[s.studentId] = s.status || 'present'; draft.note[s.studentId] = s.note; });
      state.dirty = false;
    }
    var rows = studs.map(function (s, i) {
      return '<div class="mrow"><div class="n">' + (i + 1) + '</div><div class="nm">' + esc(s.fullName) + '</div><div class="seg" role="radiogroup" aria-label="Статус: ' + esc(s.fullName) + '">' +
        STATUSES.map(function (k) { return '<button type="button" role="radio" aria-checked="' + (draft.st[s.studentId] === k) + '" class="' + k + (draft.st[s.studentId] === k ? ' on' : '') + '" data-act="mark" data-s="' + s.studentId + '" data-v="' + k + '" title="' + ST[k].label + '">' + ic(ST[k].icon) + '<span>' + ST[k].label + '</span></button>'; }).join('') + '</div></div>';
    }).join('');
    return {
      title: esc(l.groupName) + ': ' + esc(l.subjectName),
      sub: fmtDate(l.date) + ', ' + wd(l.date) + ', ' + l.pairNumber + ' пара' + (l.topic ? ' · ' + esc(l.topic) : '') + (isAdmin ? ' · ' + esc(short(l.teacherName)) : '') + (l.markCount ? ' · отметки сохранены ранее' : ' · отметок ещё нет'),
      back: ['#/lessons', isAdmin ? 'Занятия и отметки' : 'Мои занятия'],
      html: '<div class="card"><div class="mark-head"><div class="counters" id="counters"></div><div class="actions"><button class="btn btn-ghost btn-sm" data-act="mark-all" data-v="present">' + ic('i-present') + 'Все были</button><button class="btn btn-ghost btn-sm" data-act="mark-all" data-v="absent">' + ic('i-absent') + 'Все отсутствовали</button><button class="btn btn-ghost btn-sm" data-act="mark-prev">' + ic('i-late') + 'Как в прошлом часе</button></div></div>' +
        (studs.length ? rows : '<div class="empty">' + ic('i-users') + '<b>В группе нет студентов</b></div>') +
        '<div class="sticky-save"><span class="muted" id="dirtyNote" style="font-size:13px"></span><div class="actions"><button class="btn btn-ghost" data-go="#/lessons">Отмена</button><button class="btn btn-primary" data-act="save-marks"' + (studs.length ? '' : ' disabled') + '>' + ic('i-save') + 'Сохранить отметки</button></div></div></div>',
      after: updateCounters
    };
  }
  function updateCounters() {
    if (!draft) return;
    var c = { present: 0, absent: 0, excused: 0, late: 0 }, arr = [];
    Object.keys(draft.st).forEach(function (k) { c[draft.st[k]]++; arr.push({ status: draft.st[k] }); });
    var el = document.getElementById('counters'); if (!el) return;
    el.innerHTML = STATUSES.map(function (k) { return '<span class="chip ' + k + '">' + ic(ST[k].icon) + ST[k].label + ': ' + c[k] + '</span>'; }).join('') + '<span class="chip accent">Посещаемость: ' + pctText(calc(arr).pct) + '</span>';
    var dn = document.getElementById('dirtyNote'); if (dn) dn.textContent = state.dirty ? 'Есть несохранённые изменения' : '';
  }
  function paintMarks() {
    root.querySelectorAll('[data-act="mark"]').forEach(function (b) { var on = draft.st[b.getAttribute('data-s')] === b.getAttribute('data-v'); b.classList.toggle('on', on); b.setAttribute('aria-checked', on); });
    updateCounters();
  }
  HANDLERS.mark = function (t) { draft.st[t.getAttribute('data-s')] = t.getAttribute('data-v'); state.dirty = true; paintMarks(); };
  HANDLERS['mark-all'] = function (t) { var v = t.getAttribute('data-v'); Object.keys(draft.st).forEach(function (k) { draft.st[k] = v; }); state.dirty = true; paintMarks(); };
  HANDLERS['mark-prev'] = async function () {
    try {
      var prev = await api('GET', 'lessons/' + draft.lessonId + '/previous-attendance'), n = 0;
      prev.forEach(function (m) { if (draft.st[m.studentId] !== undefined) { draft.st[m.studentId] = m.status; draft.note[m.studentId] = m.note; n++; } });
      state.dirty = true; paintMarks(); toast('Скопированы отметки предыдущего часа: ' + n);
    } catch (e) { if (e.status === 404) toast(e.message, 'i-alert'); else fail(e); }
  };
  HANDLERS['save-marks'] = async function (t) {
    t.disabled = true;
    var items = Object.keys(draft.st).map(function (id) { return { studentId: Number(id), status: draft.st[id], note: draft.note[id] || null }; });
    try { await api('PUT', 'lessons/' + draft.lessonId + '/attendance', { items: items }); }
    catch (e) { t.disabled = false; fail(e, 'Отметки не сохранены'); return; }
    state.dirty = false; draft = null; toast('Отметки сохранены'); go('#/lessons');
  };

  /* ================= экран: отчёты (admin, teacher) ================= */
  async function pageReports() {
    var isAdmin = state.user.role === 'admin', my = null, groups;
    if (isAdmin) groups = (await api('GET', 'groups')).map(function (g) { return { id: g.id, name: g.name }; });
    else { my = await api('GET', 'my/assignments'); groups = uniqBy(my, 'groupId').map(function (a) { return { id: a.groupId, name: a.groupName }; }); }
    if (!groups.length) return { title: 'Отчёты', html: '<div class="card"><div class="empty">' + ic('i-chart') + '<b>Нет данных для отчёта</b>' + (isAdmin ? 'Сначала добавьте группы.' : 'У вас нет назначений.') + '</div></div>' };
    if (!state.rep || !groups.some(function (g) { return g.id === state.rep.group; })) state.rep = { group: groups[0].id, subject: 'all', from: daysAgo(60), to: today() };
    var f = state.rep;
    var subs = uniqBy((isAdmin ? await api('GET', 'assignments?groupId=' + f.group) : my.filter(function (a) { return a.groupId === f.group; })), 'subjectId')
      .map(function (a) { return { id: a.subjectId, name: a.subjectName }; });
    if (f.subject !== 'all' && !subs.some(function (s) { return s.id === f.subject; })) f.subject = 'all';
    var gname = groups.filter(function (g) { return g.id === f.group; })[0].name;
    var filters = '<div class="card"><div class="toolbar" style="border-bottom:0">' +
      '<div class="field"><label for="f-r-g">Группа</label>' + sel('r-g', groups.map(function (g) { return [g.id, g.name]; }), f.group).replace('<select', '<select data-change="rep" data-k="group"') + '</div>' +
      '<div class="field"><label for="f-r-s">Предмет</label>' + sel('r-s', [['all', 'Все предметы']].concat(subs.map(function (s) { return [s.id, s.name]; })), f.subject).replace('<select', '<select data-change="rep" data-k="subject"') + '</div>' +
      '<div class="field"><label for="r-f">С</label><input class="ctl" type="date" id="r-f" value="' + f.from + '" data-change="rep" data-k="from"></div>' +
      '<div class="field"><label for="r-t">По</label><input class="ctl" type="date" id="r-t" value="' + f.to + '" data-change="rep" data-k="to"></div></div></div>';
    var sub = isAdmin ? 'Все группы и предметы' : 'Только ваши группы и предметы';
    if (f.from && f.to && f.from > f.to) return { title: 'Отчёты', sub: sub, html: '<div class="stack">' + filters + '<div class="alert warn">' + ic('i-alert') + '<span>Дата «С» позже даты «По». Измените период.</span></div></div>' };

    var qs = (f.subject !== 'all' ? '&subjectId=' + f.subject : '') + (f.from ? '&from=' + f.from : '') + (f.to ? '&to=' + f.to : '');
    var res = await Promise.all([api('GET', 'reports/group/' + f.group + '?' + qs.slice(1)), api('GET', 'lessons?groupId=' + f.group + qs)]);
    var rep = res[0], tot = rep.total;
    var rows = rep.students.slice().sort(function (a, b) { return (a.summary.percent == null ? 101 : a.summary.percent) - (b.summary.percent == null ? 101 : b.summary.percent); });
    var body = rows.map(function (x) {
      var c = x.summary;
      return '<tr class="click" data-act="student-report" data-id="' + x.studentId + '" data-name="' + esc(x.fullName) + '"><td data-label="Студент"><span class="cell-main">' + esc(x.fullName) + '</span></td>' +
        '<td data-label="Был" class="num">' + c.present + '</td><td data-label="Опоздал" class="num">' + c.late + '</td><td data-label="Не был" class="num">' + c.absent + '</td><td data-label="Уважительная" class="num">' + c.excused + '</td>' +
        '<td data-label="Посещаемость" class="num">' + bar(c.percent) + '</td></tr>';
    }).join('');
    return {
      title: 'Отчёты', sub: sub,
      html: '<div class="stack">' + filters +
        '<div class="kpis"><div class="card kpi"><div class="v accent">' + pctText(tot.percent) + '</div><div class="l">Средняя посещаемость</div></div><div class="card kpi"><div class="v">' + res[1].length + '</div><div class="l">Занятий за период</div></div>' +
        '<div class="card kpi"><div class="v">' + tot.absent + '</div><div class="l">Пропусков без причины</div></div><div class="card kpi"><div class="v">' + tot.late + '</div><div class="l">Опозданий</div></div></div>' +
        '<div class="card"><div class="card-head"><h3>Студенты группы ' + esc(gname) + '</h3><span class="muted" style="font-size:13px">Сначала с низкой посещаемостью. Нажмите на строку для подробностей.</span></div>' +
        (tot.marked ? '<div class="tbl-wrap"><table class="t resp"><thead><tr><th>Студент</th><th class="num">Был</th><th class="num">Опоздал</th><th class="num">Не был</th><th class="num">Уваж.</th><th class="num">Посещаемость</th></tr></thead><tbody>' + body + '</tbody></table></div>'
          : '<div class="empty">' + ic('i-chart') + '<b>За период отметок нет</b>Измените фильтры</div>') +
        '</div><div class="muted" style="font-size:12.5px">Посещаемость = (был + опоздал) / (все отметки − уважительные) × 100.</div></div>'
    };
  }
  HANDLERS.rep = function (t) {
    var k = t.getAttribute('data-k'), v = t.value;
    if (k === 'group') { state.rep.group = Number(v); state.rep.subject = 'all'; }
    else if (k === 'subject') state.rep.subject = v === 'all' ? 'all' : Number(v);
    else if (k === 'from') state.rep.from = v;
    else if (k === 'to') state.rep.to = v;
    render();
  };
  function detailHtml(rep) {
    var cards = rep.bySubject.map(function (s) {
      var c = s.summary;
      return '<div class="card subj"><div class="h"><span class="nm">' + esc(s.subjectName) + '</span><span class="pc">' + pctText(c.percent) + '</span></div><div class="bar-track"><div class="bar-fill" style="width:' + (c.percent || 0) + '%"></div></div>' +
        '<div class="cnt"><span>Был: ' + c.present + '</span><span>Опоздал: ' + c.late + '</span><span>Не был: ' + c.absent + '</span><span>Уваж.: ' + c.excused + '</span></div></div>';
    }).join('');
    var hist = rep.records.map(function (x) { return '<tr><td data-label="Дата">' + fmtDate(x.date) + ', ' + x.pairNumber + ' пара</td><td data-label="Предмет">' + esc(x.subjectName) + '</td><td data-label="Статус">' + chip(x.status) + '</td></tr>'; }).join('');
    return { cards: cards, hist: hist };
  }
  HANDLERS['student-report'] = async function (t) {
    var f = state.rep, id = Number(t.getAttribute('data-id')), name = t.getAttribute('data-name');
    try {
      var rep = await api('GET', 'reports/student/' + id + '?' + [f.subject !== 'all' ? 'subjectId=' + f.subject : '', f.from ? 'from=' + f.from : '', f.to ? 'to=' + f.to : ''].filter(Boolean).join('&'));
      var d = detailHtml(rep);
      modal({ wide: true, title: name, body: '<div class="muted">Группа ' + esc(rep.student.groupName) + ', период ' + fmtDate(f.from) + ' — ' + fmtDate(f.to) + '. Общая посещаемость: <b style="color:var(--accent)">' + pctText(rep.total.percent) + '</b></div>' +
        (d.cards ? '<div class="subj-grid">' + d.cards + '</div>' : '') +
        (d.hist ? '<div class="card"><div class="tbl-wrap"><table class="t resp"><thead><tr><th>Дата</th><th>Предмет</th><th>Статус</th></tr></thead><tbody>' + d.hist + '</tbody></table></div></div>' : '<div class="empty"><b>Отметок за период нет</b></div>') });
    } catch (e) { fail(e, 'Отчёт недоступен'); }
  };

  /* ================= экран: кабинет студента ================= */
  async function pageCabinet() {
    var rep;
    try { rep = await api('GET', 'me/attendance'); }
    catch (e) { if (e.status === 404) return { title: 'Моя посещаемость', html: '<div class="card"><div class="empty">' + ic('i-alert') + '<b>Учётная запись не привязана к студенту</b>Обратитесь к администратору.</div></div>' }; throw e; }
    var d = detailHtml(rep), t = rep.total, first = rep.student.fullName.split(' ')[1] || rep.student.fullName;
    var subs = rep.bySubject;
    if (state.cabSubject !== 'all' && !subs.some(function (s) { return s.subjectId === state.cabSubject; })) state.cabSubject = 'all';
    var shown = detailHtml({ bySubject: [], records: rep.records.filter(function (x) { return state.cabSubject === 'all' || x.subjectId === state.cabSubject; }) });
    return {
      title: 'Моя посещаемость', sub: 'Группа ' + esc(rep.student.groupName),
      html: '<div class="stack"><div class="card hello"><div><h2>Здравствуйте, ' + esc(first) + '</h2><div class="muted">Данные по всем занятиям вашей группы. Отметки ставит преподаватель.</div></div><div style="text-align:right"><div class="big-pct">' + pctText(t.percent) + '</div><div class="muted" style="font-size:13px">общая посещаемость</div></div></div>' +
        '<div class="kpis"><div class="card kpi"><div class="v">' + t.marked + '</div><div class="l">Отмечено занятий</div></div><div class="card kpi"><div class="v">' + t.absent + '</div><div class="l">Пропусков</div></div><div class="card kpi"><div class="v">' + t.late + '</div><div class="l">Опозданий</div></div><div class="card kpi"><div class="v">' + t.excused + '</div><div class="l">По уважительной</div></div></div>' +
        '<div><h3 style="font-size:16px;font-weight:800;margin-bottom:12px">По предметам</h3><div class="subj-grid">' + (d.cards || '<div class="muted">Пока нет данных</div>') + '</div></div>' +
        '<div class="card"><div class="card-head"><h3>История отметок</h3><div class="field" style="min-width:200px">' + sel('cab-s', [['all', 'Все предметы']].concat(subs.map(function (s) { return [s.subjectId, s.subjectName]; })), state.cabSubject).replace('<select', '<select data-change="cab" aria-label="Предмет"') + '</div></div>' +
        (shown.hist ? '<div class="tbl-wrap"><table class="t resp"><thead><tr><th>Дата</th><th>Предмет</th><th>Статус</th></tr></thead><tbody>' + shown.hist + '</tbody></table></div>' : '<div class="empty">' + ic('i-calendar') + '<b>Отметок пока нет</b></div>') + '</div></div>'
    };
  }
  HANDLERS.cab = function (t) { state.cabSubject = t.value === 'all' ? 'all' : Number(t.value); render(); };

  /* ================= экран: смена пароля (все роли) ================= */
  async function pagePassword() {
    return {
      title: 'Смена пароля', sub: 'Учётная запись: ' + esc(state.user.login),
      html: '<div class="card card-pad" style="max-width:520px"><form id="pwForm" class="stack" novalidate>' +
        '<div class="alert ok" id="pwOk" style="display:none">' + ic('i-check') + '<span>Пароль изменён. Используйте его при следующем входе.</span></div>' +
        '<div class="alert err" id="pwBad" style="display:none">' + ic('i-alert') + '<span></span></div>' +
        fld('Текущий пароль', 'cur', inp('cur', '', 'password', ' autocomplete="current-password"')) +
        fld('Новый пароль', 'np', inp('np', '', 'password', ' autocomplete="new-password"'), 'Не короче 8 символов') +
        fld('Повторите новый пароль', 'np2', inp('np2', '', 'password', ' autocomplete="new-password"')) +
        '<div><button class="btn btn-primary" type="submit">' + ic('i-save') + 'Сохранить пароль</button></div></form></div>',
      after: function () {
        var f = document.getElementById('pwForm');
        f.addEventListener('submit', async function (e) {
          e.preventDefault();
          var ok = document.getElementById('pwOk'), bad = document.getElementById('pwBad');
          ok.style.display = 'none'; bad.style.display = 'none';
          f.querySelectorAll('.field').forEach(function (x) { x.classList.remove('invalid'); });
          var c = f.elements.cur.value, n = f.elements.np.value, n2 = f.elements.np2.value, v = true;
          if (!c) v = invalid(f, 'cur');
          if (n.length < 8) v = invalid(f, 'np', 'Не короче 8 символов');
          if (n2 !== n) v = invalid(f, 'np2', 'Пароли не совпадают');
          if (!v) return;
          try { await api('POST', 'auth/change-password', { currentPassword: c, newPassword: n }); }
          catch (ex) { if (ex.status === 401) { sessionExpired(); return; } bad.style.display = 'flex'; bad.querySelector('span').textContent = ex.message; return; }
          f.reset(); ok.style.display = 'flex'; toast('Пароль изменён');
        });
      }
    };
  }

  var PAGES = { groups: pageGroups, subjects: pageSubjects, users: pageUsers, lessons: pageLessons, lesson: pageLesson, reports: pageReports, cabinet: pageCabinet, password: pagePassword };

  /* ================= запуск ================= */
  applyTheme(readSaved() || systemTheme());
  (async function start() {
    try { state.user = await api('GET', 'auth/me'); } catch (e) { state.user = null; }
    if (state.user && !ALLOWED[state.user.role].some(function (n) { return '#/' + n === location.hash.split('/').slice(0, 2).join('/'); })) location.hash = HOME[state.user.role];
    render();
  })();
})();

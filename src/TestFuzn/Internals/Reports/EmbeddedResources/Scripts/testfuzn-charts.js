// TestFuzn report charts: small SVG charts drawn from JSON in the page, with the same look as Fuzn Cockpit.
(function () {
    'use strict';

    var NS = 'http://www.w3.org/2000/svg';
    var hoverGroups = {};

    function el(name, attrs, parent) {
        var node = document.createElementNS(NS, name);
        for (var key in attrs) node.setAttribute(key, attrs[key]);
        if (parent) parent.appendChild(node);
        return node;
    }

    function num(v, digits) {
        return v.toLocaleString('en-US', { maximumFractionDigits: digits });
    }

    function msParts(v) {
        if (v == null) return null;
        if (v > 0 && v < 0.01) return ['<0.01', 'ms'];
        if (v < 1) return [num(v, 2), 'ms'];
        if (v < 10) return [num(v, 1), 'ms'];
        if (v < 1000) return [num(v, 0), 'ms'];
        if (v < 10000) return [num(v / 1000, 2), 's'];
        if (v < 60000) return [num(v / 1000, 1), 's'];
        var m = Math.floor(v / 60000);
        var s = Math.round((v % 60000) / 1000);
        return s === 60 ? [(m + 1) + ':00', 'min'] : [m + ':' + String(s).padStart(2, '0'), 'min'];
    }

    function fmtMs(v) {
        var p = msParts(v);
        return p ? p[0] + ' ' + p[1] : '—';
    }

    function fmtCount(v, digits) {
        return v == null ? '—' : num(v, digits || 0);
    }

    function fmt(v) {
        return v === 0 ? '0' : v >= 100 ? v.toFixed(0) : v >= 10 ? v.toFixed(1) : v.toFixed(2);
    }

    function niceCeil(value) {
        if (value <= 0) return 1;
        var power = Math.pow(10, Math.floor(Math.log10(value)));
        var steps = [1, 2, 2.5, 5, 10];
        for (var i = 0; i < steps.length; i++) if (steps[i] * power >= value) return steps[i] * power;
        return 10 * power;
    }

    function clock(ms) {
        var total = Math.round(ms / 1000);
        var h = Math.floor(total / 3600), m = Math.floor((total % 3600) / 60), s = total % 60;
        return h > 0 ? h + ':' + String(m).padStart(2, '0') + ':' + String(s).padStart(2, '0') : m + ':' + String(s).padStart(2, '0');
    }

    function tip(plot, rows, left, top) {
        var box = plot.querySelector('.tf-tip');
        if (!rows) {
            if (box) box.remove();
            return;
        }
        if (!box) {
            box = document.createElement('div');
            box.className = 'tf-tip';
            box.setAttribute('role', 'status');
            plot.appendChild(box);
        }
        box.innerHTML = '';
        rows.forEach(function (row, i) {
            var line = document.createElement(i === 0 ? 'strong' : 'span');
            line.textContent = row;
            box.appendChild(line);
        });
        box.style.left = left + 'px';
        box.style.top = (top || 0) + 'px';
    }

    function keys(host, items) {
        var wrap = document.createElement('div');
        wrap.className = 'tf-keys';
        items.forEach(function (item) {
            var key = document.createElement('span');
            key.className = 'tf-key' + (item.weight === 'muted' ? ' mutedkey' : '');
            var swatch = document.createElement('span');
            swatch.className = 'tf-swatch ' + item.cls;
            key.appendChild(swatch);
            key.appendChild(document.createTextNode(item.label));
            wrap.appendChild(key);
        });
        host.appendChild(wrap);
    }

    function lineChart(host, data) {
        var height = data.height || 200;
        var pad = { top: 10, right: 12, bottom: 24, left: 58 };
        var series = data.series;
        var all = [];
        series.forEach(function (s) { s.points.forEach(function (p) { all.push(p); }); });
        if (all.length === 0) {
            host.innerHTML = '<div class="tf-empty">No intervals.</div>';
            return;
        }

        if (series.length > 1 || data.legend) {
            keys(host, series.map(function (s) {
                var cls = s.tone + (s.dash ? ' dash' : '') + (s.weight ? ' ' + s.weight : '');
                return { label: s.label, cls: cls, weight: s.weight };
            }));
        }

        var plot = document.createElement('div');
        plot.className = 'tf-plot';
        host.appendChild(plot);

        var maxT = Math.max(1000, Math.max.apply(null, all.map(function (p) { return p[0]; })));
        var top = Math.max(0, Math.max.apply(null, all.map(function (p) { return p[1] == null ? 0 : p[1]; })));
        var maxV = top <= 0 ? 1 : niceCeil(top * 1.1);
        var rank = function (s) { return s.weight === 'muted' ? 0 : s.weight === 'strong' ? 2 : 1; };
        var drawn = series.slice().sort(function (a, b) { return rank(a) - rank(b); });

        function draw() {
            plot.innerHTML = '';
            var width = Math.max(200, plot.clientWidth);
            var innerW = Math.max(10, width - pad.left - pad.right);
            var innerH = height - pad.top - pad.bottom;
            var x = function (t) { return pad.left + (t / maxT) * innerW; };
            var y = function (v) { return pad.top + innerH - (v / maxV) * innerH; };
            var svg = el('svg', { width: width, height: height, role: 'img', 'aria-label': data.title }, plot);

            [0, maxV / 2, maxV].forEach(function (t) {
                el('line', { 'class': 'tf-grid', x1: pad.left, x2: width - pad.right, y1: y(t), y2: y(t) }, svg);
                var label = el('text', { 'class': 'tf-ylab', x: pad.left - 6, y: y(t) + 4 }, svg);
                label.textContent = data.unit === 'ms' ? fmtMs(t) : fmt(t);
            });

            var tickSteps = [1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200].map(function (s) { return s * 1000; });
            var most = Math.max(2, Math.min(7, Math.floor(innerW / 80)));
            var step = tickSteps[tickSteps.length - 1];
            for (var i = 0; i < tickSteps.length; i++) if (maxT / tickSteps[i] <= most) { step = tickSteps[i]; break; }
            for (var t = 0; t <= maxT; t += step) {
                var xl = el('text', { 'class': 'tf-xlab', x: x(t), y: height - 6 }, svg);
                xl.textContent = clock(t);
            }

            drawn.forEach(function (s) {
                var cls = 'tf-line ' + s.tone + (s.dash ? ' dash' : '') + (s.weight ? ' ' + s.weight : '');
                var current = [];
                var flush = function () {
                    if (current.length > 1) el('polyline', { 'class': cls, points: current.join(' ') }, svg);
                    current = [];
                };
                s.points.forEach(function (p) {
                    if (p[1] == null) { flush(); return; }
                    current.push(x(p[0]).toFixed(1) + ',' + y(p[1]).toFixed(1));
                });
                flush();
                if (!s.dash && s.weight !== 'muted' && s.points.length <= 40) {
                    s.points.forEach(function (p) {
                        if (p[1] != null) el('circle', { 'class': 'tf-dot ' + s.tone, cx: x(p[0]), cy: y(p[1]), r: 4 }, svg);
                    });
                }
            });

            var cross = el('line', { 'class': 'tf-cross', y1: pad.top, y2: pad.top + innerH, visibility: 'hidden' }, svg);
            var hit = el('rect', { 'class': 'tf-hit', x: 0, y: 0, width: width, height: height }, svg);

            host.showHover = function (hoverT) {
                if (hoverT == null) {
                    cross.setAttribute('visibility', 'hidden');
                    tip(plot, null);
                    return;
                }
                cross.setAttribute('x1', x(hoverT));
                cross.setAttribute('x2', x(hoverT));
                cross.setAttribute('visibility', 'visible');
                var rows = [clock(hoverT)];
                series.forEach(function (s) {
                    var best = null;
                    s.points.forEach(function (p) { if (!best || Math.abs(p[0] - hoverT) < Math.abs(best[0] - hoverT)) best = p; });
                    var v = best ? best[1] : null;
                    rows.push(s.label + ': ' + (v == null ? '—' : data.unit === 'ms' ? fmtMs(v) : fmt(v) + ' ' + data.unit));
                });
                tip(plot, rows, Math.min(Math.max(x(hoverT), 80), width - 80), 4);
            };

            hit.addEventListener('pointermove', function (e) {
                var box = hit.getBoundingClientRect();
                var hoverT = Math.max(0, Math.min(maxT, ((e.clientX - box.left - pad.left) / innerW) * maxT));
                share(data.sync, host, hoverT);
            });
            hit.addEventListener('pointerleave', function () { share(data.sync, host, null); });
        }

        draw();
        return draw;
    }

    function share(group, host, value) {
        var members = group ? hoverGroups[group] || [host] : [host];
        members.forEach(function (member) { if (member.showHover) member.showHover(value); });
    }

    function pointChart(host, data) {
        var height = data.height || 220;
        var pad = { top: 12, right: 16, bottom: data.xTitle ? 42 : 26, left: 58 };
        var points = data.points;
        var plot = document.createElement('div');
        plot.className = 'tf-plot';
        host.appendChild(plot);
        var maxY = niceCeil(Math.max(0, Math.max.apply(null, points.map(function (p) { return p[1]; }))) * 1.1);
        var maxX = niceCeil(Math.max(0, Math.max.apply(null, points.map(function (p) { return p[0]; }))) * 1.1);

        function draw() {
            plot.innerHTML = '';
            var width = Math.max(200, plot.clientWidth);
            var innerW = Math.max(10, width - pad.left - pad.right);
            var innerH = height - pad.top - pad.bottom;
            var x = function (v) { return pad.left + (v / (maxX || 1)) * innerW; };
            var y = function (v) { return pad.top + innerH - (v / maxY) * innerH; };
            var svg = el('svg', { width: width, height: height, role: 'img', 'aria-label': data.title }, plot);

            [0, maxY / 2, maxY].forEach(function (t) {
                el('line', { 'class': 'tf-grid', x1: pad.left, x2: width - pad.right, y1: y(t), y2: y(t) }, svg);
                el('text', { 'class': 'tf-ylab big', x: pad.left - 8, y: y(t) + 4 }, svg).textContent = fmtMs(t);
            });
            [0, maxX / 4, maxX / 2, (3 * maxX) / 4, maxX].forEach(function (v) {
                el('text', { 'class': 'tf-xlab big', x: x(v), y: pad.top + innerH + 18 }, svg).textContent = fmtCount(v, 1);
            });
            if (data.xTitle) el('text', { 'class': 'tf-xtitle', x: pad.left + innerW / 2, y: height - 4 }, svg).textContent = data.xTitle;

            var dots = points.map(function (p) {
                return el('circle', { 'class': 'tf-pdot', cx: x(p[0]), cy: y(p[1]), r: 4.5 }, svg);
            });
            var hit = el('rect', { 'class': 'tf-hit', x: 0, y: 0, width: width, height: height }, svg);
            hit.addEventListener('pointermove', function (e) {
                var box = hit.getBoundingClientRect();
                var px = e.clientX - box.left, py = e.clientY - box.top;
                var best = -1, bestD = Infinity;
                points.forEach(function (p, i) {
                    var d = Math.hypot(x(p[0]) - px, y(p[1]) - py);
                    if (d < bestD) { bestD = d; best = i; }
                });
                dots.forEach(function (dot, i) { dot.classList.toggle('on', i === best && bestD < 40); });
                if (best < 0 || bestD >= 40) { tip(plot, null); return; }
                var p = points[best];
                tip(plot, [
                    'At ' + clock(p[2]),
                    'Load: ' + fmtCount(p[0], 1) + ' /s',
                    'P95: ' + fmtMs(p[1])
                ], Math.min(Math.max(x(p[0]), 90), width - 90), Math.max(0, y(p[1]) - 64));
            });
            hit.addEventListener('pointerleave', function () {
                dots.forEach(function (dot) { dot.classList.remove('on'); });
                tip(plot, null);
            });
        }

        draw();
        return draw;
    }

    function barChart(host, data) {
        var height = data.height || 220;
        var pad = { top: 22, right: 16, bottom: 26, left: 58 };
        var gap = 2;
        var buckets = data.buckets;
        var anyFailed = buckets.some(function (b) { return b[3] > 0; });
        if (anyFailed) keys(host, [{ label: 'Ok', cls: 'ok box' }, { label: 'Failed', cls: 'bad box' }]);
        var plot = document.createElement('div');
        plot.className = 'tf-plot';
        host.appendChild(plot);
        var fromMs = buckets[0][0], toMs = buckets[buckets.length - 1][1];
        var maxY = niceCeil(Math.max(0, Math.max.apply(null, buckets.map(function (b) { return b[2] + b[3]; }))) * 1.1);
        var total = buckets.reduce(function (n, b) { return n + b[2] + b[3]; }, 0);

        function draw() {
            plot.innerHTML = '';
            var width = Math.max(200, plot.clientWidth);
            var innerW = Math.max(10, width - pad.left - pad.right);
            var innerH = height - pad.top - pad.bottom;
            var x = function (ms) { return pad.left + ((ms - fromMs) / (toMs - fromMs || 1)) * innerW; };
            var y = function (v) { return pad.top + innerH - (v / maxY) * innerH; };
            var base = pad.top + innerH;
            var svg = el('svg', { width: width, height: height, role: 'img', 'aria-label': 'Duration distribution' }, plot);

            [0, maxY / 2, maxY].forEach(function (t) {
                el('line', { 'class': 'tf-grid', x1: pad.left, x2: width - pad.right, y1: y(t), y2: y(t) }, svg);
                el('text', { 'class': 'tf-ylab big', x: pad.left - 8, y: y(t) + 4 }, svg).textContent = fmtCount(t);
            });
            var most = Math.max(2, Math.floor(innerW / 90));
            var stride = Math.max(1, Math.ceil(buckets.length / most));
            buckets.forEach(function (b, i) {
                if (i % stride === 0) el('text', { 'class': 'tf-xlab big', x: x(b[0]), y: height - 6 }, svg).textContent = fmtMs(b[0]);
            });

            var rounded = function (left, top, w, h) {
                var r = Math.min(4, w / 2, h);
                return 'M' + left + ',' + (top + h) + ' V' + (top + r) + ' Q' + left + ',' + top + ' ' + (left + r) + ',' + top +
                    ' H' + (left + w - r) + ' Q' + (left + w) + ',' + top + ' ' + (left + w) + ',' + (top + r) + ' V' + (top + h) + ' Z';
            };
            var bars = buckets.map(function (b) {
                var left = x(b[0]) + gap / 2;
                var w = Math.max(1, x(b[1]) - x(b[0]) - gap);
                var okTop = y(b[2]);
                var allTop = y(b[2] + b[3]);
                var parts = [];
                if (b[2] > 0) {
                    var d = b[3] > 0 ? 'M' + left + ',' + base + ' V' + okTop + ' H' + (left + w) + ' V' + base + ' Z' : rounded(left, okTop, w, base - okTop);
                    parts.push(el('path', { 'class': 'tf-bar ok', d: d }, svg));
                }
                if (b[3] > 0) parts.push(el('path', { 'class': 'tf-bar bad', d: rounded(left, allTop, w, Math.max(1, okTop - allTop - (b[2] > 0 ? gap : 0))) }, svg));
                return parts;
            });

            (data.markers || []).forEach(function (m) {
                if (m.ms < fromMs || m.ms > toMs) return;
                el('line', { 'class': 'tf-marker', x1: x(m.ms), x2: x(m.ms), y1: pad.top - 4, y2: base }, svg);
                el('text', { 'class': 'tf-mlab', x: x(m.ms), y: pad.top - 8 }, svg).textContent = m.label;
            });

            var hit = el('rect', { 'class': 'tf-hit', x: pad.left, y: 0, width: innerW, height: height }, svg);
            hit.addEventListener('pointermove', function (e) {
                var box = hit.getBoundingClientRect();
                var ms = fromMs + ((e.clientX - box.left) / innerW) * (toMs - fromMs);
                var index = -1;
                buckets.forEach(function (b, i) { if (ms >= b[0] && ms < b[1]) index = i; });
                bars.forEach(function (parts, i) { parts.forEach(function (part) { part.classList.toggle('on', i === index); }); });
                if (index < 0) { tip(plot, null); return; }
                var b = buckets[index];
                var rows = [fmtMs(b[0]) + ' – ' + fmtMs(b[1]), 'Ok: ' + fmtCount(b[2])];
                if (anyFailed) rows.push('Failed: ' + fmtCount(b[3]));
                if (total) rows.push(fmtCount((100 * (b[2] + b[3])) / total, 1) + '% of iterations');
                tip(plot, rows, Math.min(Math.max(x((b[0] + b[1]) / 2), 90), width - 90), 0);
            });
            hit.addEventListener('pointerleave', function () {
                bars.forEach(function (parts) { parts.forEach(function (part) { part.classList.remove('on'); }); });
                tip(plot, null);
            });
        }

        draw();
        return draw;
    }

    function start() {
        var redraws = [];
        document.querySelectorAll('.tf-chart').forEach(function (host) {
            var source = host.querySelector('script[type="application/json"]');
            if (!source) return;
            var data = JSON.parse(source.textContent);
            if (data.sync) (hoverGroups[data.sync] = hoverGroups[data.sync] || []).push(host);
            var redraw = data.type === 'points' ? pointChart(host, data) : data.type === 'bars' ? barChart(host, data) : lineChart(host, data);
            if (redraw) redraws.push(redraw);
        });
        var timer = null;
        window.addEventListener('resize', function () {
            clearTimeout(timer);
            timer = setTimeout(function () { redraws.forEach(function (redraw) { redraw(); }); }, 100);
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
    else start();
})();

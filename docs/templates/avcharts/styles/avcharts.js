// The header's two switches: the language of the documentation, and light or dark.
//
// A page in `using/` is Chinese and its twin in `en/using/` is English, so the language switch points at the
// same chapter in the other language when there is one and at that language's guide when there is not (the
// API reference is English only, and the landing pages of the site have no twin either).
//
// The theme is remembered in local storage and applied by a script in the page's head, before anything is
// painted - see `partials/head.tmpl.partial` - so this file only has to wire the button up.

(function () {
    'use strict';

    var THEME_KEY = 'avcharts-theme';

    // ── language ─────────────────────────────────────────────────────────────────────────────────────

    var path = window.location.pathname;
    var marker = '/docs/';
    var at = path.indexOf(marker);
    var root = at >= 0 ? path.slice(0, at + marker.length) : '/';
    var rel = path.slice(root.length);

    var chinese;
    var english;

    if (rel.indexOf('en/using/') === 0) {
        chinese = root + rel.slice('en/'.length);
        english = path;
    } else if (rel.indexOf('using/') === 0) {
        chinese = path;
        english = root + 'en/' + rel;
    } else {
        chinese = root + 'using/index.html';
        english = root + 'en/using/index.html';
    }

    var isChinese = rel.indexOf('en/') !== 0 && rel.indexOf('using/') === 0;

    var links = document.querySelectorAll('[data-avc-lang]');

    for (var index = 0; index < links.length; index++) {
        var link = links[index];
        var wanted = link.getAttribute('data-avc-lang');

        link.setAttribute('href', wanted === 'en' ? english : chinese);

        if ((wanted === 'en' && !isChinese) || (wanted === 'zh' && isChinese)) {
            link.setAttribute('aria-current', 'true');
        }
    }

    // ── light and dark ───────────────────────────────────────────────────────────────────────────────

    function stored() {
        try {
            return window.localStorage.getItem(THEME_KEY);
        } catch (error) {
            return null;
        }
    }

    function preferred() {
        var saved = stored();

        if (saved === 'light' || saved === 'dark') {
            return saved;
        }

        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }

    function wear(theme) {
        document.documentElement.setAttribute('data-theme', theme);

        var button = document.getElementById('avc-theme');

        if (button) {
            button.setAttribute('aria-pressed', theme === 'dark' ? 'true' : 'false');
            button.setAttribute(
                'title',
                theme === 'dark' ? '切换到浅色 / Switch to light' : '切换到深色 / Switch to dark');
        }
    }

    wear(preferred());

    var toggle = document.getElementById('avc-theme');

    if (toggle) {
        toggle.addEventListener('click', function () {
            var next = document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';

            try {
                window.localStorage.setItem(THEME_KEY, next);
            } catch (error) {
                // A browser with no storage: the theme holds for this page and no longer.
            }

            wear(next);
        });
    }

    // Follow the system when the reader has never chosen.
    if (window.matchMedia) {
        var scheme = window.matchMedia('(prefers-color-scheme: dark)');

        var follow = function () {
            if (stored() !== 'light' && stored() !== 'dark') {
                wear(scheme.matches ? 'dark' : 'light');
            }
        };

        if (scheme.addEventListener) {
            scheme.addEventListener('change', follow);
        } else if (scheme.addListener) {
            scheme.addListener(follow);
        }
    }
})();

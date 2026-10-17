// The header's light/dark switch.
//
// The theme itself is applied before the first paint by the short script in each page's head - a theme chosen
// after the page is painted is a flash of the wrong one. This file only has to wire the button up and to
// remember the reader's choice, under the same key the documentation site uses, so one choice carries across
// the whole site.

(function () {
    'use strict';

    var KEY = 'avcharts-theme';
    var root = document.documentElement;
    var button = document.getElementById('theme');

    function stored() {
        try {
            return window.localStorage.getItem(KEY);
        } catch (error) {
            return null;
        }
    }

    function wear(theme) {
        root.setAttribute('data-theme', theme);

        if (button) {
            button.setAttribute('aria-pressed', theme === 'dark' ? 'true' : 'false');
            button.setAttribute(
                'title',
                theme === 'dark' ? '切换到浅色 / Switch to light' : '切换到深色 / Switch to dark');
        }
    }

    if (!root.getAttribute('data-theme')) {
        wear(window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
    } else {
        wear(root.getAttribute('data-theme'));
    }

    if (button) {
        button.addEventListener('click', function () {
            var next = root.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';

            try {
                window.localStorage.setItem(KEY, next);
            } catch (error) {
                // A browser with no storage: the theme holds for this page and no longer.
            }

            wear(next);
        });
    }

    // Follow the system while the reader has not chosen.
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

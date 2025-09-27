(function () {
    const nav = document.getElementById('primary-navigation');
    const toggle = document.querySelector('.nav-toggle');

    if (!nav) {
        return;
    }

    const links = Array.from(nav.querySelectorAll('.nav-link'));

    const normalise = (value) => {
        if (!value) {
            return '';
        }

        return value.trim().replace(/\/index$/i, '').toLowerCase();
    };

    const setActiveLinks = () => {
        const declaredActivePage = normalise(nav.dataset.activePage);
        const currentPath = normalise(window.location.pathname);

        links.forEach((link) => {
            const target = normalise(link.getAttribute('data-target'));
            const hrefPath = normalise(link.getAttribute('href'));
            const isActive = Boolean(
                (target && declaredActivePage && target === declaredActivePage) ||
                (target && !declaredActivePage && currentPath === target) ||
                (!target && hrefPath && currentPath === hrefPath)
            );

            link.setAttribute('data-active', String(isActive));
        });
    };

    const closeMenu = () => {
        if (!toggle) {
            return;
        }

        toggle.setAttribute('aria-expanded', 'false');
        nav.classList.remove('site-nav--open');
    };

    if (toggle) {
        toggle.addEventListener('click', () => {
            const expanded = toggle.getAttribute('aria-expanded') === 'true';
            toggle.setAttribute('aria-expanded', expanded ? 'false' : 'true');
            nav.classList.toggle('site-nav--open', !expanded);
        });

        links.forEach((link) => {
            link.addEventListener('click', closeMenu);
        });
    }

    setActiveLinks();
    window.addEventListener('pageshow', setActiveLinks);
})();

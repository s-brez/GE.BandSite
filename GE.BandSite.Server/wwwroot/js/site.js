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

(function () {
    const heroVideo = document.querySelector('.hero__video-media');
    if (!heroVideo) {
        return;
    }

    const muteControl = document.querySelector('.hero__control--mute');
    const playControl = document.querySelector('.hero__control--play');
    let userPaused = false;
    let autoPaused = false;
    let userMutedPreference = heroVideo.muted;

    const updateMuteUI = () => {
        if (!muteControl) {
            return;
        }

        const muted = heroVideo.muted;
        muteControl.dataset.muted = String(muted);
        muteControl.setAttribute('aria-pressed', String(muted));
        muteControl.setAttribute('aria-label', muted ? 'Unmute audio' : 'Mute audio');
    };

    const updatePlayUI = () => {
        if (!playControl) {
            return;
        }

        const playing = !heroVideo.paused && !heroVideo.ended;
        playControl.dataset.playing = String(playing);
        playControl.setAttribute('aria-pressed', String(playing));
        playControl.setAttribute('aria-label', playing ? 'Pause video' : 'Play video');
    };

    const restoreAudioToPreference = () => {
        if (!userMutedPreference && heroVideo.muted) {
            heroVideo.muted = false;
            updateMuteUI();
        }
    };

    const playVideo = (options) => {
        const { fromUser = false, allowMutedFallback = false } = options ?? {};
        const promise = heroVideo.play();
        if (promise && typeof promise.then === 'function') {
            promise.then(() => {
                if (fromUser) {
                    userPaused = false;
                }
                autoPaused = false;
                restoreAudioToPreference();
                updatePlayUI();
            }).catch(() => {
                if (allowMutedFallback && !heroVideo.muted) {
                    heroVideo.muted = true;
                    updateMuteUI();
                    heroVideo.play().then(() => {
                        if (fromUser) {
                            userPaused = false;
                        }
                        autoPaused = false;
                        restoreAudioToPreference();
                        updatePlayUI();
                    }).catch(() => {
                        updatePlayUI();
                    });
                } else {
                    updatePlayUI();
                }
            });
        }

        if (fromUser) {
            userPaused = false;
        }

        autoPaused = false;
        restoreAudioToPreference();
        updatePlayUI();
    };

    const pauseVideo = (options) => {
        const { fromUser = false } = options ?? {};
        heroVideo.pause();
        if (fromUser) {
            userPaused = true;
        }
        updatePlayUI();
    };

    const toggleMute = () => {
        heroVideo.muted = !heroVideo.muted;
        updateMuteUI();
        userMutedPreference = heroVideo.muted;
        if (!heroVideo.muted && heroVideo.paused && !userPaused) {
            playVideo({ allowMutedFallback: false });
        }
    };

    const togglePlay = () => {
        if (heroVideo.paused) {
            playVideo({ fromUser: true, allowMutedFallback: true });
        } else {
            pauseVideo({ fromUser: true });
        }
    };

    const bindControl = (control, handler) => {
        if (!control) {
            return;
        }

        control.addEventListener('click', handler);
        control.addEventListener('keydown', (event) => {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                handler();
            }
        });
    };

    bindControl(muteControl, toggleMute);
    bindControl(playControl, togglePlay);

    heroVideo.addEventListener('play', updatePlayUI);
    heroVideo.addEventListener('pause', updatePlayUI);
    heroVideo.addEventListener('volumechange', updateMuteUI);

    if ('IntersectionObserver' in window) {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (entry.target !== heroVideo) {
                    return;
                }

                if (!entry.isIntersecting) {
                    if (!heroVideo.paused) {
                        pauseVideo({ fromUser: false });
                        autoPaused = true;
                    }
                } else if (autoPaused && !userPaused) {
                    restoreAudioToPreference();
                    playVideo({ allowMutedFallback: true });
                    autoPaused = false;
                }
            });
        }, { threshold: 0.25 });

        observer.observe(heroVideo);
    }

    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'hidden' && !heroVideo.paused) {
            pauseVideo({ fromUser: false });
            autoPaused = true;
        } else if (document.visibilityState === 'visible' && autoPaused && !userPaused) {
            restoreAudioToPreference();
            playVideo({ allowMutedFallback: true });
            autoPaused = false;
        }
    });

    heroVideo.muted = false;
    userMutedPreference = heroVideo.muted;
    updateMuteUI();
    updatePlayUI();
    playVideo({ allowMutedFallback: true });
})();

(function () {
    const highlightVideo = document.querySelector('.highlight-video__player');
    if (!highlightVideo) {
        return;
    }

    const highlightVideoFrame = highlightVideo.closest('.highlight-video__frame');
    const highlightPlayOverlay = highlightVideoFrame ? highlightVideoFrame.querySelector('.highlight-video__play-overlay') : null;

    const updateHighlightVideoState = () => {
        if (!highlightVideoFrame) {
            return;
        }

        const playing = !highlightVideo.paused && !highlightVideo.ended;
        highlightVideoFrame.dataset.playing = String(playing);
    };

    if (highlightPlayOverlay) {
        highlightPlayOverlay.addEventListener('click', () => {
            const promise = highlightVideo.play();
            if (promise && typeof promise.then === 'function') {
                promise.catch(() => {
                    updateHighlightVideoState();
                });
            }
        });
    }

    highlightVideo.addEventListener('play', updateHighlightVideoState);
    highlightVideo.addEventListener('pause', updateHighlightVideoState);
    highlightVideo.addEventListener('ended', updateHighlightVideoState);

    const pauseHighlightVideo = () => {
        if (!highlightVideo.paused) {
            highlightVideo.pause();
        }
        updateHighlightVideoState();
    };

    if ('IntersectionObserver' in window) {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (entry.target !== highlightVideo) {
                    return;
                }

                if (!entry.isIntersecting) {
                    pauseHighlightVideo();
                }
            });
        }, { threshold: 0.25 });

        observer.observe(highlightVideo);
    }

    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'hidden') {
            pauseHighlightVideo();
        }
    });

    updateHighlightVideoState();
})();

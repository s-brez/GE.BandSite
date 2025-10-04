(function () {
    const cards = Array.from(document.querySelectorAll('.media-card'));
    const lightbox = document.querySelector('.media-lightbox');

    if (!cards.length || !lightbox) {
        return;
    }

    const dialog = lightbox.querySelector('.media-lightbox__dialog');
    const stage = lightbox.querySelector('.media-lightbox__stage');
    const title = lightbox.querySelector('.media-lightbox__title');
    const description = lightbox.querySelector('.media-lightbox__description');
    const closeButtons = Array.from(lightbox.querySelectorAll('[data-lightbox-close]'));
    const backdrop = lightbox.querySelector('.media-lightbox__backdrop');
    let restoreFocusTarget = null;
    let isOpen = false;

    if (!dialog || !stage || !title || !description || closeButtons.length === 0 || !backdrop) {
        return;
    }

    const assignDynamicAspectRatio = () => {
        cards.forEach((card) => {
            const thumb = card.querySelector('.media-card__thumb');
            const image = thumb ? thumb.querySelector('img') : null;

            if (!thumb || !image) {
                return;
            }

            const applyRatio = () => {
                const { naturalWidth, naturalHeight } = image;
                if (!naturalWidth || !naturalHeight) {
                    return;
                }

                thumb.style.setProperty('--media-card-aspect', `${naturalWidth} / ${naturalHeight}`);
            };

            if (image.complete && image.naturalWidth > 0 && image.naturalHeight > 0) {
                applyRatio();
            } else {
                image.addEventListener('load', applyRatio, { once: true });
                image.addEventListener('error', () => {
                    thumb.style.removeProperty('--media-card-aspect');
                }, { once: true });
            }
        });
    };

    assignDynamicAspectRatio();

    const getFocusableElements = () => {
        return Array.from(dialog.querySelectorAll(
            'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
        )).filter((element) => !element.hasAttribute('disabled') && !element.getAttribute('aria-hidden'));
    };

    const resetStage = () => {
        const activeVideo = stage.querySelector('video');
        if (activeVideo) {
            activeVideo.pause();
        }

        stage.innerHTML = '';
    };

    const setDescription = (value) => {
        const hasValue = typeof value === 'string' && value.trim().length > 0;
        description.textContent = hasValue ? value : '';
        description.hidden = !hasValue;
    };

    const setTitle = (value) => {
        const hasValue = typeof value === 'string' && value.trim().length > 0;
        title.textContent = hasValue ? value : '';
        title.hidden = !hasValue;
    };

    const trapFocus = (event) => {
        if (!isOpen || event.key !== 'Tab') {
            return;
        }

        const focusable = getFocusableElements();
        if (!focusable.length) {
            event.preventDefault();
            return;
        }

        const first = focusable[0];
        const last = focusable[focusable.length - 1];

        if (event.shiftKey) {
            if (document.activeElement === first) {
                event.preventDefault();
                last.focus();
            }
        } else if (document.activeElement === last) {
            event.preventDefault();
            first.focus();
        }
    };

    const closeLightbox = () => {
        if (!isOpen) {
            return;
        }

        resetStage();
        lightbox.dataset.active = 'false';
        lightbox.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('media-lightbox-open');
        isOpen = false;

        if (restoreFocusTarget && typeof restoreFocusTarget.focus === 'function') {
            restoreFocusTarget.focus();
        }

        restoreFocusTarget = null;
    };

    const openLightbox = (card) => {
        const { mediaType, mediaSrc, mediaPoster, mediaTitle, mediaDescription } = card.dataset;

        if (!mediaSrc) {
            return;
        }

        resetStage();

        let mediaElement;
        if (mediaType === 'video') {
            mediaElement = document.createElement('video');
            mediaElement.controls = true;
            mediaElement.preload = 'metadata';
            if (mediaPoster) {
                mediaElement.poster = mediaPoster;
            }
            mediaElement.src = mediaSrc;
        } else {
            mediaElement = document.createElement('img');
            mediaElement.alt = mediaTitle ?? '';
            mediaElement.loading = 'lazy';
            mediaElement.src = mediaSrc;
        }

        mediaElement.classList.add('media-lightbox__media');
        stage.appendChild(mediaElement);

        setTitle(mediaTitle ?? '');
        setDescription(mediaDescription ?? '');

        lightbox.dataset.active = 'true';
        lightbox.setAttribute('aria-hidden', 'false');
        document.body.classList.add('media-lightbox-open');
        isOpen = true;

        const trigger = card.querySelector('.media-card__trigger');
        restoreFocusTarget = trigger || card;

        const focusable = getFocusableElements();
        if (focusable.length) {
            focusable[0].focus();
        }
    };

    cards.forEach((card) => {
        const trigger = card.querySelector('.media-card__trigger') || card;

        trigger.addEventListener('click', (event) => {
            event.preventDefault();
            openLightbox(card);
        });

        trigger.addEventListener('keydown', (event) => {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                openLightbox(card);
            }
        });
    });

    closeButtons.forEach((button) => {
        button.addEventListener('click', (event) => {
            event.preventDefault();
            closeLightbox();
        });
    });

    lightbox.addEventListener('click', (event) => {
        if (event.target === lightbox || event.target === backdrop) {
            closeLightbox();
        }
    });

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape' && isOpen) {
            event.preventDefault();
            closeLightbox();
            return;
        }

        trapFocus(event);
    });
})();

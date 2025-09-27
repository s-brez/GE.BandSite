(function () {
    const form = document.getElementById('media-upload-form');
    if (!form) {
        return;
    }

    const assetTypeField = document.getElementById('asset-type');
    const titleField = document.getElementById('asset-title');
    const descriptionField = document.getElementById('asset-description');
    const displayOrderField = document.getElementById('asset-display-order');
    const featuredField = document.getElementById('asset-featured');
    const homeField = document.getElementById('asset-home');
    const publishedField = document.getElementById('asset-published');
    const mediaFileField = document.getElementById('asset-file');
    const posterFieldWrapper = form.querySelector('[data-role="poster-field"]');
    const posterFileField = document.getElementById('asset-poster');
    const statusField = document.getElementById('media-upload-status');

    const setStatus = (message, isError) => {
        if (!statusField) {
            return;
        }

        statusField.textContent = message;
        statusField.dataset.state = isError ? 'error' : 'info';
    };

    const resetStatus = () => setStatus('', false);

    const toBoolean = (checkbox) => checkbox && checkbox.checked;

    const toInteger = (input) => {
        const value = parseInt(input.value, 10);
        return Number.isNaN(value) ? 0 : value;
    };

    const updateFieldsForType = () => {
        if (!assetTypeField || !mediaFileField || !posterFieldWrapper || !posterFileField) {
            return;
        }

        if (assetTypeField.value === 'video') {
            mediaFileField.accept = 'video/mp4,video/quicktime';
            posterFieldWrapper.hidden = false;
        } else {
            mediaFileField.accept = 'image/*';
            posterFieldWrapper.hidden = true;
            posterFileField.value = '';
        }
    };

    const parseProblem = async (response) => {
        try {
            const data = await response.json();
            if (typeof data === 'string') {
                return data;
            }
            if (data && typeof data.message === 'string') {
                return data.message;
            }
        } catch (_) {
            // ignore JSON parse issues
        }

        return `Request failed with status ${response.status}`;
    };

    const requestUpload = async (kind, file) => {
        const response = await fetch('/api/admin/media/uploads', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                upload_kind: kind,
                file_name: file.name,
                content_type: file.type,
                content_length: file.size
            })
        });

        if (!response.ok) {
            throw new Error(await parseProblem(response));
        }

        return response.json();
    };

    const uploadToPresignedUrl = async (upload, file) => {
        const response = await fetch(upload.upload_url, {
            method: 'PUT',
            headers: {
                'Content-Type': upload.content_type
            },
            body: file
        });

        if (!response.ok) {
            throw new Error('Upload to storage failed. Check bucket CORS settings and retry.');
        }

        return upload;
    };

    const createPhotoAsset = async (payload) => {
        const response = await fetch('/api/admin/media/assets/photo', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            throw new Error(await parseProblem(response));
        }
    };

    const createVideoAsset = async (payload) => {
        const response = await fetch('/api/admin/media/assets/video', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            throw new Error(await parseProblem(response));
        }
    };

    if (assetTypeField) {
        assetTypeField.addEventListener('change', updateFieldsForType);
        updateFieldsForType();
    }

    form.addEventListener('submit', async (event) => {
        event.preventDefault();

        if (!titleField || !mediaFileField) {
            return;
        }

        const assetType = assetTypeField ? assetTypeField.value : 'photo';
        const mediaFile = mediaFileField.files && mediaFileField.files[0];

        if (!mediaFile) {
            setStatus('Select a media file before uploading.', true);
            return;
        }

        const title = titleField.value.trim();
        if (!title) {
            setStatus('Provide a title for the asset.', true);
            return;
        }

        setStatus('Preparing upload…', false);

        try {
            const description = descriptionField ? descriptionField.value.trim() : '';
            const displayOrder = displayOrderField ? toInteger(displayOrderField) : 0;
            const isFeatured = featuredField ? toBoolean(featuredField) : false;
            const showOnHome = homeField ? toBoolean(homeField) : false;
            const isPublished = publishedField ? toBoolean(publishedField) : false;

            if (assetType === 'photo') {
                const upload = await requestUpload('Photo', mediaFile).then((result) => uploadToPresignedUrl(result, mediaFile));

                await createPhotoAsset({
                    title,
                    description: description || null,
                    raw_object_key: upload.object_key,
                    content_type: upload.content_type,
                    is_featured: isFeatured,
                    show_on_home: showOnHome,
                    is_published: isPublished,
                    display_order: displayOrder
                });
            } else {
                const videoUpload = await requestUpload('VideoSource', mediaFile).then((result) => uploadToPresignedUrl(result, mediaFile));
                let posterUpload = null;

                if (posterFileField && posterFileField.files && posterFileField.files[0]) {
                    const posterFile = posterFileField.files[0];
                    posterUpload = await requestUpload('Poster', posterFile).then((result) => uploadToPresignedUrl(result, posterFile));
                }

                await createVideoAsset({
                    title,
                    description: description || null,
                    raw_video_key: videoUpload.object_key,
                    video_content_type: videoUpload.content_type,
                    raw_poster_key: posterUpload ? posterUpload.object_key : null,
                    poster_content_type: posterUpload ? posterUpload.content_type : null,
                    is_featured: isFeatured,
                    show_on_home: showOnHome,
                    is_published: isPublished,
                    display_order: displayOrder
                });
            }

            setStatus('Upload complete. Reloading library…', false);
            window.setTimeout(() => window.location.reload(), 600);
        } catch (error) {
            const message = error instanceof Error ? error.message : 'An unexpected error occurred.';
            setStatus(message, true);
        }
    });
})();

(function () {
    const form = document.querySelector('[data-contact-form]');
    if (!form) {
        return;
    }

    const summary = form.querySelector('.validation-summary');
    const submitButton = form.querySelector('button[type="submit"]');
    const intro = document.querySelector('.contact-form__intro');
    const fieldMessages = new Map();

    if (summary && (!summary.textContent || summary.textContent.trim().length === 0)) {
        summary.hidden = true;
    }

    Array.from(form.querySelectorAll('.validation-message')).forEach((element) => {
        const fieldName = element.getAttribute('data-valmsg-for');
        if (fieldName) {
            fieldMessages.set(fieldName, element);
        }
        if (!element.textContent || element.textContent.trim().length === 0) {
            element.hidden = true;
        }
    });

    const eventDateTimeField = form.querySelector('[name="Input.EventDateTime"]');
    const eventTimezoneField = form.querySelector('[name="Input.EventTimezone"]');

    const toMessages = (value) => {
        if (Array.isArray(value)) {
            return value.filter((message) => typeof message === 'string' && message.trim().length > 0);
        }

        return [];
    };

    const validateEventTiming = () => {
        const fieldErrors = {};
        const generalErrors = [];

        const dateValue = eventDateTimeField && typeof eventDateTimeField.value === 'string'
            ? eventDateTimeField.value.trim()
            : '';

        const timezoneValue = eventTimezoneField && typeof eventTimezoneField.value === 'string'
            ? eventTimezoneField.value.trim()
            : '';

        if (!dateValue && !timezoneValue) {
            return null;
        }

        const hasTimezoneOption = timezoneValue && eventTimezoneField
            ? Array.from(eventTimezoneField.options || []).some((option) => option.value === timezoneValue)
            : false;

        if (dateValue && !timezoneValue) {
            fieldErrors["Input.EventTimezone"] = ["Select the timezone for your event."];
            generalErrors.push("Select the timezone for your event.");
        } else if (!dateValue && timezoneValue) {
            fieldErrors["Input.EventTimezone"] = ["Add the event date and time that matches the selected timezone."];
            generalErrors.push("Add the event date and time that matches the selected timezone.");
        } else if (timezoneValue && !hasTimezoneOption) {
            fieldErrors["Input.EventTimezone"] = ["Select a valid timezone."];
            generalErrors.push("Select a valid timezone.");
        }

        if (generalErrors.length === 0) {
            return null;
        }

        return { fieldErrors, generalErrors };
    };

    const setSubmitting = (isSubmitting) => {
        if (!submitButton) {
            return;
        }

        submitButton.disabled = isSubmitting;
        submitButton.dataset.submitting = String(isSubmitting);
    };

    const clearFieldErrors = () => {
        fieldMessages.forEach((element) => {
            element.textContent = '';
            element.hidden = true;
        });
    };

    const clearSummary = () => {
        if (!summary) {
            return;
        }

        summary.innerHTML = '';
        summary.hidden = true;
    };

    const removeSuccessMessage = () => {
        if (!intro) {
            return;
        }

        const existing = intro.querySelector('[data-contact-success]');
        if (existing) {
            existing.remove();
        }
    };

    const showFieldErrors = (fieldErrors) => {
        if (!fieldErrors) {
            return;
        }

        Object.entries(fieldErrors).forEach(([field, messages]) => {
            if (!field || !fieldMessages.has(field)) {
                return;
            }

            const parsedMessages = toMessages(messages);
            if (!parsedMessages.length) {
                return;
            }

            const target = fieldMessages.get(field);
            if (!target) {
                return;
            }

            target.textContent = parsedMessages.join(' ');
            target.hidden = false;
        });
    };

    const showSummary = (messages) => {
        if (!summary) {
            return;
        }

        const parsedMessages = toMessages(messages);
        if (!parsedMessages.length) {
            clearSummary();
            return;
        }

        const list = document.createElement('ul');
        parsedMessages.forEach((message) => {
            const item = document.createElement('li');
            item.textContent = message;
            list.appendChild(item);
        });

        summary.innerHTML = '';
        summary.appendChild(list);
        summary.hidden = false;
    };

    const focusFirstErrorField = (fieldErrors) => {
        if (!fieldErrors) {
            return;
        }

        for (const field of Object.keys(fieldErrors)) {
            if (!field) {
                continue;
            }

            const element = form.elements.namedItem(field);
            if (!element) {
                continue;
            }

            const control = Array.isArray(element) ? element[0] : element;
            if (control && typeof control.focus === 'function') {
                control.focus();
                break;
            }
        }
    };

    const showSuccess = (message) => {
        if (!intro) {
            return;
        }

        removeSuccessMessage();

        const status = document.createElement('div');
        status.className = 'contact-form__success';
        status.setAttribute('role', 'status');
        status.dataset.contactSuccess = 'true';
        status.textContent = message;

        intro.prepend(status);
    };

    const buildRequest = () => {
        const action = form.getAttribute('action');
        const target = action && action.trim().length > 0 ? action : window.location.href;
        const formData = new FormData(form);

        return { target, formData };
    };

    const handleFailure = (payload) => {
        const fieldErrors = payload && typeof payload === 'object' ? payload.field_errors : undefined;
        const generalErrors = payload && typeof payload === 'object' ? payload.errors : undefined;
        const message = payload && typeof payload === 'object' ? payload.message : undefined;

        if (fieldErrors) {
            showFieldErrors(fieldErrors);
            focusFirstErrorField(fieldErrors);
        }

        const summaryMessages = [];
        if (typeof message === 'string' && message.trim().length > 0) {
            summaryMessages.push(message);
        }

        toMessages(generalErrors).forEach((error) => summaryMessages.push(error));

        showSummary(summaryMessages);
    };

    form.addEventListener('submit', async (event) => {
        event.preventDefault();
        removeSuccessMessage();
        clearSummary();
        clearFieldErrors();

        const timingValidation = validateEventTiming();
        if (timingValidation) {
            showFieldErrors(timingValidation.fieldErrors);
            focusFirstErrorField(timingValidation.fieldErrors);
            showSummary(timingValidation.generalErrors);
            return;
        }

        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        const { target, formData } = buildRequest();

        setSubmitting(true);

        try {
            const response = await fetch(target, {
                method: 'POST',
                headers: {
                    'Accept': 'application/json',
                    'X-Requested-With': 'fetch',
                },
                body: formData,
            });

            const payload = await response.json().catch(() => null);

            if (!response.ok || !payload || payload.success !== true) {
                handleFailure(payload);
                return;
            }

            form.reset();
            showSuccess(typeof payload.message === 'string' && payload.message.trim().length > 0
                ? payload.message
                : 'Thank you! Our bookings team will reply within one business day.');
        } catch (error) {
            showSummary(['We could not submit your booking request. Please check your connection and try again.']);
        } finally {
            setSubmitting(false);
        }
    });
})();

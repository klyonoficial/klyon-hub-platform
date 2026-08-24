/**
 * Eventos de funil de conversão Klyon Hub.
 * Envia para dataLayer (GTM) quando disponível; caso contrário, log em desenvolvimento.
 */
(function (window) {
    'use strict';

    function pushEvent(name, params) {
        var payload = Object.assign({ event: name, funnel_ts: new Date().toISOString() }, params || {});
        if (window.dataLayer && Array.isArray(window.dataLayer)) {
            window.dataLayer.push(payload);
        } else if (window.console && window.console.debug) {
            window.console.debug('[KlyonFunnel]', payload);
        }
    }

    function trackPageView() {
        pushEvent('funnel_page_view', {
            page_path: window.location.pathname,
            page_title: document.title
        });
    }

    function initCtaTracking() {
        document.querySelectorAll('[data-funnel]').forEach(function (el) {
            el.addEventListener('click', function () {
                pushEvent('funnel_cta_click', {
                    cta_id: el.getAttribute('data-funnel'),
                    cta_text: (el.textContent || '').trim().slice(0, 120),
                    page_path: window.location.pathname
                });
            });
        });
    }

    function trackFormSubmit(formId, eventName) {
        var form = document.getElementById(formId);
        if (!form) return;
        form.addEventListener('submit', function () {
            pushEvent(eventName || 'funnel_form_submit', { form_id: formId });
        });
    }

    window.KlyonFunnel = {
        push: pushEvent,
        trackPageView: trackPageView,
        trackFormSubmit: trackFormSubmit
    };

    document.addEventListener('DOMContentLoaded', function () {
        trackPageView();
        initCtaTracking();
        trackFormSubmit('fiscalOnboardingForm', 'funnel_onboarding_submit');
        trackFormSubmit('partnerWizardForm', 'funnel_partner_wizard_submit');
    });
})(window);

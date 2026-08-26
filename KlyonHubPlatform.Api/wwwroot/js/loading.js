(function() {
    'use strict';

    var MUTATING_METHODS = { POST: true, PUT: true, PATCH: true, DELETE: true };

    function createLoadingOverlay() {
        var existing = document.getElementById('klyon-loading-overlay');
        if (existing) {
            if (existing.parentElement !== document.body) {
                document.body.appendChild(existing);
            }
            return existing;
        }
        if (!document.body) {
            return null;
        }
        var overlay = document.createElement('div');
        overlay.id = 'klyon-loading-overlay';
        overlay.className = 'klyon-loading-overlay hidden';
        overlay.style.cssText = 'position: fixed !important; top: 0 !important; left: 0 !important; width: 100% !important; height: 100% !important; background-color: rgba(255, 255, 255, 0.9) !important; display: none !important; justify-content: center !important; align-items: center !important; z-index: 99999 !important;';
        overlay.innerHTML =
            '<div style="text-align: center; display: flex; flex-direction: column; align-items: center; justify-content: center;">' +
            '<div class="klyon-loading-spinner"></div>' +
            '<div class="klyon-loading-text">Carregando...</div>' +
            '</div>';
        document.body.appendChild(overlay);
        return overlay;
    }

    function initLoadingOverlay() {
        if (document.body) {
            createLoadingOverlay();
        } else if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', function() {
                createLoadingOverlay();
            });
        } else {
            setTimeout(function() {
                if (document.body) {
                    createLoadingOverlay();
                }
            }, 0);
        }
    }
    initLoadingOverlay();

    function isConfirmModalOpen() {
        var confirmModal = document.getElementById('confirmModal');
        if (!confirmModal) return false;
        if (confirmModal.getAttribute('data-confirm-open') === 'true') {
            return true;
        }
        try {
            var modalInstance = bootstrap.Modal.getInstance(confirmModal);
            if (modalInstance && modalInstance._element === confirmModal) {
                if (modalInstance._isShown === true) {
                    return true;
                }
            }
        } catch (e) {
        }
        var hasShowClass = confirmModal.classList.contains('show');
        if (hasShowClass) {
            var computedStyle = window.getComputedStyle(confirmModal);
            var isDisplayed = computedStyle.display !== 'none' && computedStyle.visibility !== 'hidden';
            if (isDisplayed) {
                var visibleModals = document.querySelectorAll('.modal.show');
                for (var i = 0; i < visibleModals.length; i++) {
                    if (visibleModals[i] === confirmModal) {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    function applyOverlayVisible(overlay) {
        if (!overlay) return;
        if (overlay.parentElement !== document.body) {
            document.body.appendChild(overlay);
        }
        overlay.style.position = 'fixed';
        overlay.style.top = '0';
        overlay.style.left = '0';
        overlay.style.width = '100%';
        overlay.style.height = '100%';
        overlay.style.backgroundColor = 'rgba(255, 255, 255, 0.9)';
        overlay.style.display = 'flex';
        overlay.style.justifyContent = 'center';
        overlay.style.alignItems = 'center';
        overlay.style.zIndex = '1060';
        overlay.style.opacity = '1';
        overlay.style.pointerEvents = 'auto';
        overlay.style.visibility = 'visible';
        overlay.classList.remove('hidden');
        void overlay.offsetHeight;
    }

    var overlayPinned = false;
    var fetchLoadingCount = 0;

    function actuallyShow() {
        if (!document.body) {
            return false;
        }
        var overlay = document.getElementById('klyon-loading-overlay');
        if (!overlay) {
            overlay = createLoadingOverlay();
            if (!overlay) {
                return false;
            }
        }
        applyOverlayVisible(overlay);
        return true;
    }

    function actuallyHide() {
        var overlay = document.getElementById('klyon-loading-overlay');
        if (overlay) {
            overlay.style.display = 'none';
            overlay.style.opacity = '0';
            overlay.style.visibility = 'hidden';
            overlay.style.pointerEvents = 'none';
            overlay.classList.add('hidden');
        }
    }

    function showLoading() {
        if (isConfirmModalOpen()) {
            return;
        }
        overlayPinned = true;
        if (!actuallyShow()) {
            setTimeout(showLoading, 10);
        }
    }

    function hideLoading() {
        overlayPinned = false;
        fetchLoadingCount = 0;
        actuallyHide();
    }

    function showFetchLoading() {
        if (isConfirmModalOpen()) {
            return;
        }
        fetchLoadingCount++;
        actuallyShow();
    }

    function hideFetchLoading() {
        fetchLoadingCount = Math.max(0, fetchLoadingCount - 1);
        if (fetchLoadingCount === 0 && !overlayPinned) {
            actuallyHide();
        }
    }

    window.KlyonLoading = {
        show: showLoading,
        hide: hideLoading
    };
    window.isConfirmModalOpen = isConfirmModalOpen;

    function showLoadingSync() {
        if (isConfirmModalOpen()) {
            return;
        }
        if (!document.body) {
            return;
        }
        var overlay = document.getElementById('klyon-loading-overlay');
        if (!overlay) {
            overlay = createLoadingOverlay();
            if (!overlay) {
                return;
            }
        }
        applyOverlayVisible(overlay);
    }

    function isLandingPage() {
        return !!(document.body && document.body.classList.contains('landing-body'));
    }

    function isMutatingMethod(method) {
        return !!MUTATING_METHODS[String(method || 'GET').toUpperCase()];
    }

    function getFormSubmitMethod(form, submitter) {
        if (submitter && submitter.getAttribute) {
            var formMethod = submitter.getAttribute('formmethod');
            if (formMethod) return formMethod;
        }
        return form.getAttribute('method') || form.method || 'get';
    }

    function shouldShowFormLoading(form, submitter) {
        if (!form || form.tagName !== 'FORM') return false;
        if (form.classList.contains('no-loading') || form.classList.contains('confirm-required')) {
            return false;
        }
        if (form.dataset.validationFailed === 'true') return false;
        if (isConfirmModalOpen()) return false;
        var target = form.getAttribute('target');
        if (target && target !== '_self') return false;
        return isMutatingMethod(getFormSubmitMethod(form, submitter));
    }

    function formPassesConstraintValidation(form) {
        if (form.hasAttribute('novalidate') || form.noValidate) {
            return true;
        }
        if (typeof form.checkValidity !== 'function') {
            return true;
        }
        return form.checkValidity();
    }

    function showFormLoadingIfStillSubmitting(event, form) {
        if (event.defaultPrevented) return;
        if (!form || !form.isConnected) return;
        if (form.dataset.validationFailed === 'true') return;
        if (!shouldShowFormLoading(form, event.submitter)) return;
        if (!formPassesConstraintValidation(form)) return;
        if (isConfirmModalOpen()) return;
        showLoading();
    }

    // Capture: agenda o overlay só depois dos outros handlers (preventDefault / invalid).
    document.addEventListener('submit', function(e) {
        var form = e.target;
        if (!shouldShowFormLoading(form, e.submitter)) {
            return;
        }
        if (!formPassesConstraintValidation(form)) {
            return;
        }
        setTimeout(function() {
            showFormLoadingIfStillSubmitting(e, form);
        }, 0);
    }, true);

    document.addEventListener('invalid', function() {
        hideLoading();
    }, true);

    function isBackendRequest(url) {
        if (!url) return false;
        if (url instanceof Request) {
            url = url.url;
        }
        var urlString = String(url);
        if (urlString.startsWith('data:') ||
            urlString.startsWith('blob:') ||
            urlString.includes('/css/') ||
            urlString.includes('/js/') ||
            urlString.includes('/images/') ||
            urlString.includes('/fonts/') ||
            urlString.includes('/lib/') ||
            urlString.includes('/vendor/') ||
            urlString.includes('/wwwroot/')) {
            return false;
        }
        if (urlString.includes('/api/') ||
            urlString.includes('/negotiate') ||
            urlString.includes('/start?') ||
            urlString.includes('/connect?') ||
            urlString.includes('transport=webSockets') ||
            urlString.includes('clientProtocol=') ||
            urlString.includes('/hubs')) {
            return false;
        }
        if (urlString.includes('browserLink') ||
            urlString.includes('aspnetcore-browser-refresh') ||
            urlString.includes('localhost:61981') ||
            urlString.includes('localhost:44355')) {
            return false;
        }
        var isStaticFile = urlString.match(/\.(css|js|png|jpg|jpeg|gif|svg|ico|woff|woff2|ttf|eot|mp4|mp3)(\?|$)/i);
        var isControllerAction = urlString.match(/\/(Client|Admin|Coworking|Home|Account|Parceiro|Auth)\//);
        if (isStaticFile && !isControllerAction) {
            return false;
        }
        return true;
    }

    function getFetchMethod(args) {
        if (args[0] instanceof Request && args[0].method) {
            return args[0].method;
        }
        var init = args[1];
        if (init && init.method) {
            return init.method;
        }
        return 'GET';
    }

    var activeBackendRequests = 0;
    var originalFetch = window.fetch;
    window.fetch = function() {
        var args = arguments;
        var url = args[0];
        var method = getFetchMethod(args);
        var shouldTrack = isMutatingMethod(method) && isBackendRequest(url) && !isConfirmModalOpen();
        if (shouldTrack) {
            activeBackendRequests++;
            showFetchLoading();
        }
        var fetchPromise = originalFetch.apply(this, args);
        if (shouldTrack) {
            var release = function() {
                activeBackendRequests--;
                if (activeBackendRequests < 0) activeBackendRequests = 0;
                setTimeout(hideFetchLoading, 100);
            };
            fetchPromise.then(release, release);
        }
        return fetchPromise;
    };

    var originalXHROpen = XMLHttpRequest.prototype.open;
    var originalXHRSend = XMLHttpRequest.prototype.send;
    XMLHttpRequest.prototype.open = function() {
        this._klyonLoadingActive = false;
        this._klyonRequestMethod = arguments[0];
        this._klyonRequestUrl = arguments[1];
        return originalXHROpen.apply(this, arguments);
    };
    XMLHttpRequest.prototype.send = function() {
        var url = this._klyonRequestUrl || '';
        var method = this._klyonRequestMethod || 'GET';
        var shouldTrack = isMutatingMethod(method) && isBackendRequest(url) && !isConfirmModalOpen();
        if (shouldTrack) {
            this._klyonLoadingActive = true;
            activeBackendRequests++;
            showFetchLoading();
            this.addEventListener('loadend', function() {
                if (this._klyonLoadingActive) {
                    activeBackendRequests--;
                    if (activeBackendRequests < 0) activeBackendRequests = 0;
                    setTimeout(hideFetchLoading, 100);
                    this._klyonLoadingActive = false;
                }
            }, { once: true });
        }
        return originalXHRSend.apply(this, arguments);
    };

    function isOptInLoadingLink(link) {
        if (!link || link.tagName !== 'A') return false;
        return link.hasAttribute('data-show-loading') || link.classList.contains('js-show-loading');
    }

    document.addEventListener('click', function(e) {
        if (isConfirmModalOpen()) return;
        if (e.defaultPrevented) return;
        if (e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
        var link = e.target.closest && e.target.closest('a');
        if (!isOptInLoadingLink(link)) return;
        if (link.target && link.target !== '_self') return;
        if (link.hasAttribute('download')) return;
        var href = (link.getAttribute('href') || '').trim();
        if (!href || href.charAt(0) === '#' || href.indexOf('javascript:') === 0) return;
        showLoading();
    });

    var initialPageLoad = true;
    var initialPageLoadTimeout = null;

    function scheduleHideInitialLoading() {
        if (!initialPageLoad) return;
        if (initialPageLoadTimeout) {
            clearTimeout(initialPageLoadTimeout);
        }
        initialPageLoadTimeout = setTimeout(function() {
            initialPageLoad = false;
            if (activeBackendRequests <= 0) {
                setTimeout(hideLoading, 200);
            }
        }, 1000);
    }

    if (!isLandingPage()) {
        if (document.readyState === 'loading') {
            setTimeout(function() {
                if (document.body && !isLandingPage()) {
                    showLoadingSync();
                }
            }, 50);
        } else if (document.readyState === 'interactive') {
            if (document.body && !isLandingPage()) {
                showLoadingSync();
            }
        }
    }

    window.addEventListener('load', function() {
        scheduleHideInitialLoading();
    });
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            scheduleHideInitialLoading();
        });
    } else {
        scheduleHideInitialLoading();
    }

    window.addEventListener('beforeunload', function() {
        initialPageLoad = false;
        if (initialPageLoadTimeout) {
            clearTimeout(initialPageLoadTimeout);
            initialPageLoadTimeout = null;
        }
    });

    // bfcache / voltar-avançar: overlay pode ficar preso (ex.: data-show-loading
    // mostrou o loader e a página foi restaurada do cache sem novo load/DOMContentLoaded).
    function clearLoadingOnPageRestore() {
        initialPageLoad = false;
        if (initialPageLoadTimeout) {
            clearTimeout(initialPageLoadTimeout);
            initialPageLoadTimeout = null;
        }
        activeBackendRequests = 0;
        hideLoading();
    }

    window.addEventListener('pageshow', function(e) {
        // Sempre esconde ao restaurar do bfcache; também limpa overlay pinado
        // se o unload não completou (voltar sem persisted em alguns browsers).
        if (e.persisted || overlayPinned) {
            clearLoadingOnPageRestore();
        }
    });
})();

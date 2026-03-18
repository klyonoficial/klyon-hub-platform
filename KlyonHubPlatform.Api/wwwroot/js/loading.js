(function() {
    'use strict';
    function createLoadingOverlay() {
        let existing = document.getElementById('klyon-loading-overlay');
        if (existing) {
            if (existing.parentElement !== document.body) {
                document.body.appendChild(existing);
            }
            return existing;
        }
        if (!document.body) {
            return null;
        }
        const overlay = document.createElement('div');
        overlay.id = 'klyon-loading-overlay';
        overlay.className = 'klyon-loading-overlay hidden';
        overlay.style.cssText = 'position: fixed !important; top: 0 !important; left: 0 !important; width: 100% !important; height: 100% !important; background-color: rgba(255, 255, 255, 0.9) !important; display: none !important; justify-content: center !important; align-items: center !important; z-index: 99999 !important;';
        overlay.innerHTML = `
            <div style="text-align: center; display: flex; flex-direction: column; align-items: center; justify-content: center;">
                <div class="klyon-loading-spinner"></div>
                <div class="klyon-loading-text">Carregando...</div>
            </div>
        `;
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
    let initialPageLoad = true;
    let initialPageLoadTimeout = null;
    if (document.readyState === 'loading') {
        setTimeout(function() {
            if (document.body) {
                showLoadingSync();
            }
        }, 50);
    } else if (document.readyState === 'interactive') {
        if (document.body) {
            showLoadingSync();
        }
    }
    function isConfirmModalOpen() {
        const confirmModal = document.getElementById('confirmModal');
        if (!confirmModal) return false;
        if (confirmModal.getAttribute('data-confirm-open') === 'true') {
            return true;
        }
        try {
            const modalInstance = bootstrap.Modal.getInstance(confirmModal);
            if (modalInstance && modalInstance._element === confirmModal) {
                if (modalInstance._isShown === true) {
                    return true;
                }
            }
        } catch (e) {
        }
        const hasShowClass = confirmModal.classList.contains('show');
        if (hasShowClass) {
            const computedStyle = window.getComputedStyle(confirmModal);
            const isDisplayed = computedStyle.display !== 'none' && computedStyle.visibility !== 'hidden';
            if (isDisplayed) {
                const visibleModals = document.querySelectorAll('.modal.show');
                for (let i = 0; i < visibleModals.length; i++) {
                    if (visibleModals[i] === confirmModal) {
                        return true;
                    }
                }
            }
        }
        return false;
    }
    function showLoading() {
        if (isConfirmModalOpen()) {
            return;
        }
        if (!document.body) {
            return;
        }
        let overlay = document.getElementById('klyon-loading-overlay');
        if (!overlay) {
            overlay = createLoadingOverlay();
            if (!overlay) {
                setTimeout(showLoading, 10);
                return;
            }
        }
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
    function hideLoading() {
        const overlay = document.getElementById('klyon-loading-overlay');
        if (overlay) {
            overlay.style.display = 'none';
            overlay.style.opacity = '0';
            overlay.style.visibility = 'hidden';
            overlay.style.pointerEvents = 'none';
            overlay.classList.add('hidden');
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
        let overlay = document.getElementById('klyon-loading-overlay');
        if (!overlay) {
            overlay = createLoadingOverlay();
            if (!overlay) {
                return;
            }
        }
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
    document.addEventListener('submit', function(e) {
        const form = e.target;
        if (!form || form.tagName !== 'FORM') {
            return;
        }
        if (isConfirmModalOpen()) {
            return;
        }
        if (form.classList.contains('no-loading') ||
            form.classList.contains('confirm-required')) {
            return;
        }
        if (typeof form.checkValidity === 'function' && !form.checkValidity()) {
            return;
        }
        if (form.dataset.validationFailed === 'true') {
            return;
        }
        let overlay = document.getElementById('klyon-loading-overlay');
        if (!overlay && document.body) {
            overlay = createLoadingOverlay();
        }
        if (overlay) {
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
            void overlay.offsetWidth;
            setTimeout(function() {
                if (overlay) {
                    overlay.style.display = 'flex';
                    overlay.style.zIndex = '1060';
                }
            }, 1);
        }
        form.addEventListener('error', function() {
            hideLoading();
        }, { once: true });
    }, true);
    window.addEventListener('error', function(e) {
        setTimeout(function() {
            const overlay = document.getElementById('klyon-loading-overlay');
            if (overlay && !overlay.classList.contains('hidden')) {
                hideLoading();
            }
        }, 1000);
    });
    window.addEventListener('beforeunload', function() {
        const overlay = document.getElementById('klyon-loading-overlay');
        if (overlay && overlay.style.display === 'flex' && !overlay.classList.contains('hidden')) {
            hideLoading();
        }
    });
    function isBackendRequest(url) {
        if (!url) return false;
        if (url instanceof Request) {
            url = url.url;
        }
        const urlString = String(url);
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
        if (urlString.includes('/negotiate') || 
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
        const isStaticFile = urlString.match(/\.(css|js|png|jpg|jpeg|gif|svg|ico|woff|woff2|ttf|eot|mp4|mp3)(\?|$)/i);
        const isControllerAction = urlString.match(/\/(Client|Admin|Coworking|Home|Account|Parceiro)\//);
        if (isStaticFile && !isControllerAction) {
            return false;
        }
        return true;
    }
    let activeBackendRequests = 0;
    const originalFetch = window.fetch;
    window.fetch = function(...args) {
        const url = args[0];
        const isBackend = isBackendRequest(url);
        const formWithAjax = document.querySelector('form[data-ajax-request="true"]');
        const isManualAjax = formWithAjax !== null;
        if (isBackend && !isConfirmModalOpen() && !isManualAjax) {
            activeBackendRequests++;
            showLoading();
        }
        const fetchPromise = originalFetch.apply(this, args);
        if (isBackend && !isManualAjax) {
            fetchPromise
                .then(() => {
                    activeBackendRequests--;
                    if (activeBackendRequests <= 0) {
                        activeBackendRequests = 0;
                        setTimeout(hideLoading, 100);
                    }
                })
                .catch(() => {
                    activeBackendRequests--;
                    if (activeBackendRequests <= 0) {
                        activeBackendRequests = 0;
                        setTimeout(hideLoading, 100);
                    }
                });
        }
        return fetchPromise;
    };
    const originalXHROpen = XMLHttpRequest.prototype.open;
    const originalXHRSend = XMLHttpRequest.prototype.send;
    XMLHttpRequest.prototype.open = function(...args) {
        this._klyonLoadingActive = false;
        this._klyonRequestUrl = args[1];
        return originalXHROpen.apply(this, args);
    };
    XMLHttpRequest.prototype.send = function(...args) {
        const url = this._klyonRequestUrl || '';
        const isBackend = isBackendRequest(url);
        if (isBackend && !isConfirmModalOpen()) {
            this._klyonLoadingActive = true;
            activeBackendRequests++;
            showLoading();
            this.addEventListener('loadend', function() {
                if (this._klyonLoadingActive) {
                    activeBackendRequests--;
                    if (activeBackendRequests <= 0) {
                        activeBackendRequests = 0;
                        setTimeout(hideLoading, 100);
                    }
                    this._klyonLoadingActive = false;
                }
            }, { once: true });
            this.addEventListener('error', function() {
                if (this._klyonLoadingActive) {
                    activeBackendRequests--;
                    if (activeBackendRequests <= 0) {
                        activeBackendRequests = 0;
                        setTimeout(hideLoading, 100);
                    }
                    this._klyonLoadingActive = false;
                }
            }, { once: true });
            this.addEventListener('abort', function() {
                if (this._klyonLoadingActive) {
                    activeBackendRequests--;
                    if (activeBackendRequests <= 0) {
                        activeBackendRequests = 0;
                        setTimeout(hideLoading, 100);
                    }
                    this._klyonLoadingActive = false;
                }
            }, { once: true });
        }
        return originalXHRSend.apply(this, args);
    };
    document.addEventListener('click', function(e) {
        if (isConfirmModalOpen()) {
            return;
        }
        const link = e.target.closest('a');
        if (link && link.href && !link.href.includes('#') && !link.target) {
            const href = link.getAttribute('href');
            if (href && !href.startsWith('javascript:') && !href.startsWith('#')) {
                showLoading();
            }
        }
    });
    window.addEventListener('load', function() {
        if (initialPageLoad) {
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
    });
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            if (initialPageLoad) {
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
        });
    } else {
        if (initialPageLoad) {
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
    }
    window.addEventListener('beforeunload', function() {
        initialPageLoad = false;
        if (initialPageLoadTimeout) {
            clearTimeout(initialPageLoadTimeout);
            initialPageLoadTimeout = null;
        }
    });
})();
// Loading padrão Klyon
(function() {
    'use strict';

    // Criar overlay de loading
    function createLoadingOverlay() {
        const overlay = document.createElement('div');
        overlay.id = 'klyon-loading-overlay';
        overlay.className = 'klyon-loading-overlay hidden';
        overlay.innerHTML = `
            <div class="text-center">
                <div class="klyon-loading-spinner"></div>
                <div class="klyon-loading-text">Carregando...</div>
            </div>
        `;
        document.body.appendChild(overlay);
        return overlay;
    }

    // Mostrar loading
    function showLoading() {
        let overlay = document.getElementById('klyon-loading-overlay');
        if (!overlay) {
            overlay = createLoadingOverlay();
        }
        overlay.classList.remove('hidden');
    }

    // Esconder loading
    function hideLoading() {
        const overlay = document.getElementById('klyon-loading-overlay');
        if (overlay) {
            overlay.classList.add('hidden');
        }
    }

    // Expor funções globalmente
    window.KlyonLoading = {
        show: showLoading,
        hide: hideLoading
    };

    // Mostrar loading em submissões de formulário
    document.addEventListener('submit', function(e) {
        const form = e.target;
        if (form.tagName === 'FORM' && !form.classList.contains('no-loading')) {
            showLoading();
        }
    });

    // Mostrar loading em cliques de links que não são âncoras
    document.addEventListener('click', function(e) {
        const link = e.target.closest('a');
        if (link && link.href && !link.href.includes('#') && !link.target) {
            const href = link.getAttribute('href');
            if (href && !href.startsWith('javascript:') && !href.startsWith('#')) {
                showLoading();
            }
        }
    });

    // Esconder loading quando a página carregar completamente
    window.addEventListener('load', function() {
        hideLoading();
    });

    // Esconder loading quando DOM estiver pronto
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            setTimeout(hideLoading, 300);
        });
    } else {
        setTimeout(hideLoading, 300);
    }
})();

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

    // Verificar se há modal de confirmação aberto
    function isConfirmModalOpen() {
        const confirmModal = document.getElementById('confirmModal');
        if (!confirmModal) return false;
        
        // Verificação mais rápida: atributo de dados
        if (confirmModal.getAttribute('data-confirm-open') === 'true') {
            return true;
        }
        
        // Verificar se o modal está visível usando Bootstrap
        try {
            const modalInstance = bootstrap.Modal.getInstance(confirmModal);
            if (modalInstance) {
                // Verificar propriedade _isShown ou _element (Bootstrap 5)
                if (modalInstance._isShown === true || modalInstance._element === confirmModal) {
                    return true;
                }
            }
        } catch (e) {
            // Se bootstrap não estiver disponível, usar fallback
        }
        
        // Verificar classes e display como fallback
        // Bootstrap adiciona a classe 'show' quando o modal está visível
        const hasShowClass = confirmModal.classList.contains('show');
        const computedStyle = window.getComputedStyle(confirmModal);
        const isDisplayed = computedStyle.display !== 'none' && computedStyle.visibility !== 'hidden';
        
        // Verificar se há backdrop (Bootstrap cria um elemento .modal-backdrop quando o modal está aberto)
        const backdrop = document.querySelector('.modal-backdrop');
        const hasBackdrop = backdrop !== null;
        
        // Verificar se o modal está sendo animado (classe 'fade' com 'show' indica que está abrindo/fechando)
        const isAnimating = confirmModal.classList.contains('fade') && hasShowClass;
        
        // O modal está aberto se:
        // 1. Tem a classe 'show' E não está escondido
        // 2. Tem backdrop (indica que está aberto)
        // 3. Está sendo animado (abrindo)
        return (hasShowClass && isDisplayed) || hasBackdrop || isAnimating;
    }

    // Mostrar loading
    function showLoading() {
        // Não mostrar loading se há um modal de confirmação aberto
        if (isConfirmModalOpen()) {
            return;
        }
        
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
        
        // Não mostrar loading se:
        // 1. O formulário tem a classe 'no-loading'
        // 2. O formulário tem a classe 'confirm-required' (requer confirmação antes - loading será mostrado automaticamente após confirmação)
        // 3. Há um modal de confirmação aberto (a função showLoading já verifica isso, mas verificamos aqui também para evitar chamadas desnecessárias)
        if (form.tagName === 'FORM' && 
            !form.classList.contains('no-loading') &&
            !form.classList.contains('confirm-required') &&
            !isConfirmModalOpen()) {
            showLoading();
        }
        
        // Para formulários que fazem submit após confirmação, garantir que o loading seja fechado após a resposta
        // Isso será feito automaticamente quando a página recarregar ou quando houver erro
        if (form.tagName === 'FORM' && !form.classList.contains('no-loading')) {
            // Se for uma submissão normal (não AJAX), o loading será fechado quando a página recarregar
            // Se for AJAX, o desenvolvedor deve fechar manualmente após a resposta
            // Adicionar listener para erros de formulário
            form.addEventListener('error', function() {
                hideLoading();
            }, { once: true });
        }
    });
    
    // Interceptar erros de requisição para fechar o loading
    window.addEventListener('error', function(e) {
        // Se houver um erro e o loading estiver visível, fechar após um pequeno delay
        setTimeout(function() {
            const overlay = document.getElementById('klyon-loading-overlay');
            if (overlay && !overlay.classList.contains('hidden')) {
                hideLoading();
            }
        }, 1000);
    });
    
    // Fechar loading quando a página for recarregada (indica que a requisição foi concluída)
    window.addEventListener('beforeunload', function() {
        hideLoading();
    });
    
    // Interceptar requisições fetch para fechar o loading após a resposta
    const originalFetch = window.fetch;
    window.fetch = function(...args) {
        const fetchPromise = originalFetch.apply(this, args);
        
        // Verificar se o loading está visível antes de interceptar
        const overlay = document.getElementById('klyon-loading-overlay');
        const isLoadingVisible = overlay && !overlay.classList.contains('hidden');
        
        if (isLoadingVisible) {
            // Fechar loading quando a requisição for concluída (sucesso ou erro)
            fetchPromise
                .then(() => {
                    // Pequeno delay para garantir que a resposta foi processada
                    setTimeout(hideLoading, 100);
                })
                .catch(() => {
                    // Fechar loading mesmo em caso de erro
                    setTimeout(hideLoading, 100);
                });
        }
        
        return fetchPromise;
    };
    
    // Interceptar requisições XMLHttpRequest para fechar o loading após a resposta
    const originalXHROpen = XMLHttpRequest.prototype.open;
    const originalXHRSend = XMLHttpRequest.prototype.send;
    
    XMLHttpRequest.prototype.open = function(...args) {
        this._klyonLoadingActive = false;
        return originalXHROpen.apply(this, args);
    };
    
    XMLHttpRequest.prototype.send = function(...args) {
        const overlay = document.getElementById('klyon-loading-overlay');
        const isLoadingVisible = overlay && !overlay.classList.contains('hidden');
        
        if (isLoadingVisible) {
            this._klyonLoadingActive = true;
            
            this.addEventListener('loadend', function() {
                if (this._klyonLoadingActive) {
                    setTimeout(hideLoading, 100);
                    this._klyonLoadingActive = false;
                }
            }, { once: true });
            
            this.addEventListener('error', function() {
                if (this._klyonLoadingActive) {
                    setTimeout(hideLoading, 100);
                    this._klyonLoadingActive = false;
                }
            }, { once: true });
        }
        
        return originalXHRSend.apply(this, args);
    };

    // Mostrar loading em cliques de links que não são âncoras
    document.addEventListener('click', function(e) {
        // Não mostrar loading se há um modal de confirmação aberto
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

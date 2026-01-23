// Loading padrão Klyon
(function() {
    'use strict';

    // Criar overlay de loading
    function createLoadingOverlay() {
        // Verificar se já existe
        let existing = document.getElementById('klyon-loading-overlay');
        if (existing) {
            // Garantir que está no body, não dentro de um modal
            if (existing.parentElement !== document.body) {
                document.body.appendChild(existing);
            }
            return existing;
        }
        
        // Garantir que o body existe
        if (!document.body) {
            return null;
        }
        
        const overlay = document.createElement('div');
        overlay.id = 'klyon-loading-overlay';
        overlay.className = 'klyon-loading-overlay hidden';
        // Definir estilos inline para garantir que funcionem e fiquem acima dos modais
        overlay.style.cssText = 'position: fixed !important; top: 0 !important; left: 0 !important; width: 100% !important; height: 100% !important; background-color: rgba(255, 255, 255, 0.9) !important; display: none !important; justify-content: center !important; align-items: center !important; z-index: 99999 !important;';
        overlay.innerHTML = `
            <div style="text-align: center; display: flex; flex-direction: column; align-items: center; justify-content: center;">
                <div class="klyon-loading-spinner"></div>
                <div class="klyon-loading-text">Carregando...</div>
            </div>
        `;
        // Garantir que o overlay seja adicionado ao body, não dentro de um modal
        document.body.appendChild(overlay);
        return overlay;
    }
    
    // Criar o overlay quando o DOM estiver pronto
    function initLoadingOverlay() {
        if (document.body) {
            createLoadingOverlay();
        } else if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', function() {
                createLoadingOverlay();
            });
        } else {
            // Tentar criar imediatamente
            setTimeout(function() {
                if (document.body) {
                    createLoadingOverlay();
                }
            }, 0);
        }
    }
    
    // Inicializar o overlay
    initLoadingOverlay();

    // Flag para rastrear se o loading foi mostrado durante o carregamento inicial da página
    let initialPageLoad = true;
    let initialPageLoadTimeout = null;
    
    // Mostrar loading no início do carregamento da página
    if (document.readyState === 'loading') {
        // Página ainda está carregando, mostrar loading
        setTimeout(function() {
            if (document.body) {
                showLoadingSync();
            }
        }, 50);
    } else if (document.readyState === 'interactive') {
        // DOM está pronto mas recursos ainda podem estar carregando
        if (document.body) {
            showLoadingSync();
        }
    }

    // Verificar se há modal de confirmação aberto
    // Esta função é CRÍTICA: o loading NUNCA deve aparecer quando há um showConfirm ativo
    // IMPORTANTE: Esta função verifica APENAS o modal de confirmação (confirmModal), não outros modais
    function isConfirmModalOpen() {
        const confirmModal = document.getElementById('confirmModal');
        if (!confirmModal) return false;
        
        // Verificação mais rápida e confiável: atributo de dados
        // Este atributo é definido quando showConfirm() é chamado e removido quando o modal fecha
        // Esta é a verificação mais confiável porque é controlada diretamente pelo showConfirm
        if (confirmModal.getAttribute('data-confirm-open') === 'true') {
            return true;
        }
        
        // Verificar se o modal está visível usando Bootstrap
        // Mas APENAS se for o confirmModal específico
        try {
            const modalInstance = bootstrap.Modal.getInstance(confirmModal);
            if (modalInstance && modalInstance._element === confirmModal) {
                // Verificar propriedade _isShown (Bootstrap 5)
                if (modalInstance._isShown === true) {
                    return true;
                }
            }
        } catch (e) {
            // Se bootstrap não estiver disponível, continuar com outras verificações
        }
        
        // Verificar classes e display como fallback
        // Bootstrap adiciona a classe 'show' quando o modal está visível
        // Mas APENAS verificar se é o confirmModal específico
        const hasShowClass = confirmModal.classList.contains('show');
        if (hasShowClass) {
            const computedStyle = window.getComputedStyle(confirmModal);
            const isDisplayed = computedStyle.display !== 'none' && computedStyle.visibility !== 'hidden';
            if (isDisplayed) {
                // Verificar se realmente é o confirmModal que está visível
                // Comparar o elemento para garantir que não é outro modal
                const visibleModals = document.querySelectorAll('.modal.show');
                for (let i = 0; i < visibleModals.length; i++) {
                    if (visibleModals[i] === confirmModal) {
                        return true;
                    }
                }
            }
        }
        
        // NÃO verificar backdrop genérico, pois pode ser de qualquer modal
        // Apenas confiar no atributo data-confirm-open e nas verificações específicas acima
        
        return false;
    }

    // Mostrar loading
    function showLoading() {
        // REGRA CRÍTICA: NUNCA mostrar loading se há um modal de confirmação (showConfirm) aberto
        // O loading só deve aparecer APÓS o usuário clicar em "Confirmar" dentro do showConfirm
        if (isConfirmModalOpen()) {
            return;
        }
        
        // Garantir que o body existe
        if (!document.body) {
            return;
        }
        
        let overlay = document.getElementById('klyon-loading-overlay');
        if (!overlay) {
            overlay = createLoadingOverlay();
            if (!overlay) {
                // Se ainda não foi criado, tentar novamente
                setTimeout(showLoading, 10);
                return;
            }
        }
        
        // Garantir que o overlay está no body, não dentro de um modal
        if (overlay.parentElement !== document.body) {
            document.body.appendChild(overlay);
        }
        
        // Aplicar estilos inline diretamente para garantir que apareça acima dos modais normais
        // Mas ABAIXO do modal de confirmação (z-index: 10550)
        overlay.style.position = 'fixed';
        overlay.style.top = '0';
        overlay.style.left = '0';
        overlay.style.width = '100%';
        overlay.style.height = '100%';
        overlay.style.backgroundColor = 'rgba(255, 255, 255, 0.9)';
        overlay.style.display = 'flex';
        overlay.style.justifyContent = 'center';
        overlay.style.alignItems = 'center';
        overlay.style.zIndex = '1060'; // Acima dos modais normais (1050), mas abaixo do modal de confirmação (10550)
        overlay.style.opacity = '1';
        overlay.style.pointerEvents = 'auto';
        overlay.style.visibility = 'visible';
        overlay.classList.remove('hidden');
        
        // Forçar reflow para garantir que o navegador renderize
        void overlay.offsetHeight;
    }

    // Esconder loading
    function hideLoading() {
        const overlay = document.getElementById('klyon-loading-overlay');
        if (overlay) {
            // Remover estilos inline que foram aplicados para mostrar
            overlay.style.display = 'none';
            overlay.style.opacity = '0';
            overlay.style.visibility = 'hidden';
            overlay.style.pointerEvents = 'none';
            overlay.classList.add('hidden');
        }
    }

    // Expor funções globalmente
    window.KlyonLoading = {
        show: showLoading,
        hide: hideLoading
    };
    
    // Expor função de verificação de modal de confirmação para uso externo
    window.isConfirmModalOpen = isConfirmModalOpen;

    // Função auxiliar para mostrar loading de forma síncrona (sem delay)
    function showLoadingSync() {
        // REGRA CRÍTICA: NUNCA mostrar loading se há um modal de confirmação (showConfirm) aberto
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
        
        // Garantir que o overlay está no body, não dentro de um modal
        if (overlay.parentElement !== document.body) {
            document.body.appendChild(overlay);
        }
        
        // Mostrar de forma síncrona (sem requestAnimationFrame) usando estilos inline
        // Z-index abaixo do modal de confirmação (10550), mas acima dos modais normais (1050)
        overlay.style.position = 'fixed';
        overlay.style.top = '0';
        overlay.style.left = '0';
        overlay.style.width = '100%';
        overlay.style.height = '100%';
        overlay.style.backgroundColor = 'rgba(255, 255, 255, 0.9)';
        overlay.style.display = 'flex';
        overlay.style.justifyContent = 'center';
        overlay.style.alignItems = 'center';
        overlay.style.zIndex = '1060'; // Acima dos modais normais (1050), mas abaixo do modal de confirmação (10550)
        overlay.style.opacity = '1';
        overlay.style.pointerEvents = 'auto';
        overlay.style.visibility = 'visible';
        overlay.classList.remove('hidden');
        
        // Forçar reflow imediatamente para garantir renderização
        void overlay.offsetHeight;
    }

    // Mostrar loading em submissões de formulário
    // Usar capture phase para garantir que capturamos o evento antes de qualquer outro handler
    document.addEventListener('submit', function(e) {
        const form = e.target;
        
        // Verificar se é realmente um formulário
        if (!form || form.tagName !== 'FORM') {
            return;
        }
        
        // REGRA CRÍTICA: NUNCA mostrar loading se há um modal de confirmação (showConfirm) aberto
        // O loading só deve aparecer APÓS o usuário clicar em "Confirmar" dentro do showConfirm
        if (isConfirmModalOpen()) {
            return;
        }
        
        // Não mostrar loading se:
        // 1. O formulário tem a classe 'no-loading'
        // 2. O formulário tem a classe 'confirm-required' (requer confirmação antes - loading será mostrado automaticamente após confirmação)
        if (form.classList.contains('no-loading') ||
            form.classList.contains('confirm-required')) {
            return;
        }
        
        // Garantir que o overlay existe antes de mostrar
        let overlay = document.getElementById('klyon-loading-overlay');
        if (!overlay && document.body) {
            overlay = createLoadingOverlay();
        }
        
        // Mostrar loading de forma síncrona ANTES de permitir o submit
        // Isso garante que o loading apareça antes da navegação
        if (overlay) {
            // Garantir que o overlay está no body, não dentro de um modal
            if (overlay.parentElement !== document.body) {
                document.body.appendChild(overlay);
            }
            
            // Aplicar estilos inline diretamente para garantir que apareça IMEDIATAMENTE
            overlay.style.position = 'fixed';
            overlay.style.top = '0';
            overlay.style.left = '0';
            overlay.style.width = '100%';
            overlay.style.height = '100%';
            overlay.style.backgroundColor = 'rgba(255, 255, 255, 0.9)';
            overlay.style.display = 'flex';
            overlay.style.justifyContent = 'center';
            overlay.style.alignItems = 'center';
            overlay.style.zIndex = '1060'; // Acima dos modais normais (1050), mas abaixo do modal de confirmação (10550)
            overlay.style.opacity = '1';
            overlay.style.pointerEvents = 'auto';
            overlay.style.visibility = 'visible';
            overlay.classList.remove('hidden');
            
            // Forçar reflow múltiplas vezes para garantir renderização
            void overlay.offsetHeight;
            void overlay.offsetWidth;
            
            // Adicionar um micro-delay para garantir que o navegador renderize antes da navegação
            // Isso é especialmente importante em navegadores que navegam muito rápido
            setTimeout(function() {
                // Garantir que ainda está visível
                if (overlay) {
                    overlay.style.display = 'flex';
                    overlay.style.zIndex = '1060'; // Acima dos modais normais (1050), mas abaixo do modal de confirmação (10550)
                }
            }, 1);
        }
        
        // Para formulários que fazem submit após confirmação, garantir que o loading seja fechado após a resposta
        // Isso será feito automaticamente quando a página recarregar ou quando houver erro
        form.addEventListener('error', function() {
            hideLoading();
        }, { once: true });
    }, true); // Usar capture phase
    
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
    // Mas apenas se o loading não foi mostrado recentemente (para evitar esconder antes de mostrar)
    window.addEventListener('beforeunload', function() {
        const overlay = document.getElementById('klyon-loading-overlay');
        if (overlay && overlay.style.display === 'flex' && !overlay.classList.contains('hidden')) {
            // Só esconder se o loading estiver realmente visível
            // Isso evita esconder o loading antes mesmo dele aparecer
            hideLoading();
        }
    });
    
    // Função auxiliar para verificar se é uma requisição ao backend
    function isBackendRequest(url) {
        if (!url) return false;
        
        // Se for um objeto Request, pegar a URL
        if (url instanceof Request) {
            url = url.url;
        }
        
        // Converter para string se necessário
        const urlString = String(url);
        
        // Não é requisição ao backend se:
        // - É data URI ou blob URI
        // - É um arquivo estático em pastas específicas (css, js, images, fonts, lib, vendor)
        // - É um arquivo estático com extensão de mídia (mas não se for uma URL de API/Controller)
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
        
        // Ignorar requisições SignalR (negotiate, start, connect)
        if (urlString.includes('/negotiate') || 
            urlString.includes('/start?') || 
            urlString.includes('/connect?') ||
            urlString.includes('transport=webSockets') ||
            urlString.includes('clientProtocol=')) {
            return false;
        }
        
        // Ignorar requisições BrowserLink e AspNetCore Browser Refresh
        if (urlString.includes('browserLink') || 
            urlString.includes('aspnetcore-browser-refresh') ||
            urlString.includes('localhost:61981') ||
            urlString.includes('localhost:44355')) {
            return false;
        }
        
        // Se for um arquivo estático direto (com extensão no final, sem parâmetros de query que indiquem ação)
        // Mas permitir se for uma URL de controller/action (contém /Client/, /Admin/, /Coworking/, etc)
        const isStaticFile = urlString.match(/\.(css|js|png|jpg|jpeg|gif|svg|ico|woff|woff2|ttf|eot|mp4|mp3)(\?|$)/i);
        const isControllerAction = urlString.match(/\/(Client|Admin|Coworking|Home|Account|Parceiro)\//i);
        
        if (isStaticFile && !isControllerAction) {
            return false;
        }
        
        // É uma requisição ao backend se não for um arquivo estático
        return true;
    }

    // Contador de requisições ativas ao backend
    let activeBackendRequests = 0;
    
    // Interceptar requisições fetch para mostrar e fechar o loading
    const originalFetch = window.fetch;
    window.fetch = function(...args) {
        const url = args[0];
        const isBackend = isBackendRequest(url);
        
        // Verificar se há um formulário com data-ajax-request (requisição AJAX manual)
        // Se houver, não gerenciar o loading automaticamente
        const formWithAjax = document.querySelector('form[data-ajax-request="true"]');
        const isManualAjax = formWithAjax !== null;
        
        // Mostrar loading antes da requisição se:
        // 1. For ao backend
        // 2. Não houver modal de confirmação aberto
        // 3. Não for uma requisição AJAX manual
        // (Agora permitimos mostrar loading durante o carregamento inicial também)
        if (isBackend && !isConfirmModalOpen() && !isManualAjax) {
            activeBackendRequests++;
            showLoading();
        }
        
        const fetchPromise = originalFetch.apply(this, args);
        
        // Fechar loading quando a requisição for concluída (sucesso ou erro)
        // Mas APENAS se não for uma requisição AJAX manual
        if (isBackend && !isManualAjax) {
            fetchPromise
                .then(() => {
                    activeBackendRequests--;
                    // Só esconder o loading se não houver mais requisições ativas
                    if (activeBackendRequests <= 0) {
                        activeBackendRequests = 0;
                        // Pequeno delay para garantir que a resposta foi processada
                        setTimeout(hideLoading, 100);
                    }
                })
                .catch(() => {
                    activeBackendRequests--;
                    // Só esconder o loading se não houver mais requisições ativas
                    if (activeBackendRequests <= 0) {
                        activeBackendRequests = 0;
                        // Fechar loading mesmo em caso de erro
                        setTimeout(hideLoading, 100);
                    }
                });
        }
        
        return fetchPromise;
    };
    
    // Interceptar requisições XMLHttpRequest para mostrar e fechar o loading
    const originalXHROpen = XMLHttpRequest.prototype.open;
    const originalXHRSend = XMLHttpRequest.prototype.send;
    
    XMLHttpRequest.prototype.open = function(...args) {
        this._klyonLoadingActive = false;
        this._klyonRequestUrl = args[1]; // URL da requisição
        return originalXHROpen.apply(this, args);
    };
    
    XMLHttpRequest.prototype.send = function(...args) {
        const url = this._klyonRequestUrl || '';
        const isBackend = isBackendRequest(url);
        
        // Mostrar loading antes da requisição se for ao backend
        // (Agora permitimos mostrar loading durante o carregamento inicial também)
        if (isBackend && !isConfirmModalOpen()) {
            this._klyonLoadingActive = true;
            activeBackendRequests++;
            showLoading();
            
            // Fechar loading quando a requisição for concluída
            this.addEventListener('loadend', function() {
                if (this._klyonLoadingActive) {
                    activeBackendRequests--;
                    // Só esconder o loading se não houver mais requisições ativas
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
                    // Só esconder o loading se não houver mais requisições ativas
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
                    // Só esconder o loading se não houver mais requisições ativas
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
        // Se for o carregamento inicial da página, apenas marcar que terminou após um tempo
        // Mas não esconder o loading aqui - deixar que as requisições fetch/XHR controlem
        if (initialPageLoad) {
            // Limpar timeout anterior se existir
            if (initialPageLoadTimeout) {
                clearTimeout(initialPageLoadTimeout);
            }
            
            // Aguardar um tempo para marcar que o carregamento inicial terminou
            // Mas não esconder o loading - deixar que as requisições fetch/XHR controlem
            initialPageLoadTimeout = setTimeout(function() {
                initialPageLoad = false;
                // Se não houver requisições ativas, esconder o loading
                if (activeBackendRequests <= 0) {
                    setTimeout(hideLoading, 200);
                }
            }, 1000);
        }
    });

    // Esconder loading quando DOM estiver pronto (apenas se não houver requisições pendentes)
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            // Se for o carregamento inicial, apenas marcar que terminou após um tempo
            // Mas não esconder o loading aqui - deixar que as requisições controlem
            if (initialPageLoad) {
                // Limpar timeout anterior se existir
                if (initialPageLoadTimeout) {
                    clearTimeout(initialPageLoadTimeout);
                }
                
                // Aguardar um tempo para marcar que o carregamento inicial terminou
                // Mas não esconder o loading - deixar que as requisições fetch/XHR controlem
                initialPageLoadTimeout = setTimeout(function() {
                    initialPageLoad = false;
                    // Se não houver requisições ativas, esconder o loading
                    if (activeBackendRequests <= 0) {
                        setTimeout(hideLoading, 200);
                    }
                }, 1000);
            }
        });
    } else {
        // DOM já está pronto
        if (initialPageLoad) {
            // Limpar timeout anterior se existir
            if (initialPageLoadTimeout) {
                clearTimeout(initialPageLoadTimeout);
            }
            
            // Aguardar um tempo para marcar que o carregamento inicial terminou
            initialPageLoadTimeout = setTimeout(function() {
                initialPageLoad = false;
                // Se não houver requisições ativas, esconder o loading
                if (activeBackendRequests <= 0) {
                    setTimeout(hideLoading, 200);
                }
            }, 1000);
        }
    }
    
    // Marcar que não é mais o carregamento inicial após navegação
    window.addEventListener('beforeunload', function() {
        initialPageLoad = false;
        if (initialPageLoadTimeout) {
            clearTimeout(initialPageLoadTimeout);
            initialPageLoadTimeout = null;
        }
    });
})();

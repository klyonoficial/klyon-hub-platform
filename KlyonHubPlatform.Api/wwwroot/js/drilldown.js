/**
 * Sistema de Drilldown - Navegação em cascata com animações
 * Permite "entrar mais a fundo" ao clicar em elementos do dashboard
 */

(function() {
    'use strict';

    class DrilldownManager {
        constructor() {
            this.history = [];
            this.currentLevel = 0;
            this.container = null;
            this.init();
        }

        init() {
            document.addEventListener('DOMContentLoaded', () => {
                this.container = document.querySelector('.main-content');
                if (!this.container) return;

                this.setupDrilldownCards();
                this.setupBackButton();
            });
        }

        setupDrilldownCards() {
            // Encontrar todos os cartões com atributo data-drilldown
            const cards = document.querySelectorAll('[data-drilldown]');
            
            cards.forEach(card => {
                const url = card.getAttribute('data-drilldown');
                if (url && url !== '#' && url !== '') {
                    card.style.cursor = 'pointer';
                    card.addEventListener('click', (e) => {
                        // Evitar navegação se clicar em um link dentro do card
                        if (e.target.tagName === 'A' || e.target.closest('a')) {
                            return;
                        }

                        this.navigateTo(url, card);
                    });
                }

                // Adicionar efeito hover
                card.addEventListener('mouseenter', () => {
                    card.style.transform = 'scale(1.02)';
                    card.style.transition = 'transform 0.2s ease';
                });

                card.addEventListener('mouseleave', () => {
                    card.style.transform = 'scale(1)';
                });
            });

            // Configurar linhas da tabela com drilldown
            const tableRows = document.querySelectorAll('tr[data-drilldown]');
            tableRows.forEach(row => {
                row.style.cursor = 'pointer';
                row.addEventListener('click', (e) => {
                    if (e.target.tagName === 'A' || e.target.tagName === 'BUTTON' || e.target.closest('a, button')) {
                        return;
                    }

                    const url = row.getAttribute('data-drilldown');
                    if (url) {
                        this.navigateTo(url, row);
                    }
                });

                row.addEventListener('mouseenter', () => {
                    row.style.backgroundColor = 'rgba(0, 123, 255, 0.1)';
                });

                row.addEventListener('mouseleave', () => {
                    row.style.backgroundColor = '';
                });
            });
        }

        navigateTo(url, sourceElement) {
            // Verificar se a URL é válida
            if (!url || url === '#' || url === '') {
                return;
            }

            // Salvar estado atual
            this.saveState();

            // Criar overlay de transição
            const overlay = this.createTransitionOverlay(sourceElement);
            document.body.appendChild(overlay);

            // Animar saída
            this.animateOut(overlay, () => {
                // Navegar para a URL
                window.location.href = url;
            });
        }

        createTransitionOverlay(sourceElement) {
            const overlay = document.createElement('div');
            overlay.className = 'drilldown-overlay';
            
            // Obter posição e tamanho do elemento fonte
            const rect = sourceElement.getBoundingClientRect();
            const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
            const scrollLeft = window.pageXOffset || document.documentElement.scrollLeft;

            overlay.style.position = 'fixed';
            overlay.style.top = (rect.top + scrollTop) + 'px';
            overlay.style.left = (rect.left + scrollLeft) + 'px';
            overlay.style.width = rect.width + 'px';
            overlay.style.height = rect.height + 'px';
            overlay.style.backgroundColor = window.getComputedStyle(sourceElement).backgroundColor;
            overlay.style.borderRadius = window.getComputedStyle(sourceElement).borderRadius;
            overlay.style.boxShadow = window.getComputedStyle(sourceElement).boxShadow;
            overlay.style.zIndex = '9999';
            overlay.style.transition = 'all 0.4s cubic-bezier(0.4, 0, 0.2, 1)';
            overlay.style.overflow = 'hidden';

            // Clonar conteúdo visual do elemento
            const clone = sourceElement.cloneNode(true);
            clone.style.margin = '0';
            clone.style.transform = 'none';
            overlay.appendChild(clone);

            return overlay;
        }

        animateOut(overlay, callback) {
            // Expandir overlay para cobrir toda a tela
            requestAnimationFrame(() => {
                overlay.style.top = '0';
                overlay.style.left = '0';
                overlay.style.width = '100vw';
                overlay.style.height = '100vh';
                overlay.style.borderRadius = '0';
                
                // Adicionar indicador de carregamento
                const loader = document.createElement('div');
                loader.className = 'drilldown-loader';
                loader.innerHTML = '<div class="spinner-border text-light" role="status"><span class="visually-hidden">Carregando...</span></div>';
                overlay.appendChild(loader);

                // Executar callback após animação
                setTimeout(() => {
                    if (callback) callback();
                }, 400);
            });
        }

        saveState() {
            this.history.push({
                url: window.location.href,
                scrollY: window.scrollY,
                timestamp: Date.now()
            });
        }

        setupBackButton() {
            // Adicionar botão de voltar se houver histórico
            if (window.history.length > 1) {
                // O botão de voltar será gerenciado pelo navegador
                // Mas podemos adicionar uma animação de entrada ao voltar
                window.addEventListener('pageshow', (e) => {
                    if (e.persisted) {
                        // Página foi carregada do cache (voltar)
                        this.animateIn();
                    }
                });
            }
        }

        animateIn() {
            const container = this.container;
            if (!container) return;

            container.style.opacity = '0';
            container.style.transform = 'translateX(-20px)';
            container.style.transition = 'opacity 0.3s ease, transform 0.3s ease';

            requestAnimationFrame(() => {
                container.style.opacity = '1';
                container.style.transform = 'translateX(0)';
            });
        }
    }

    // Inicializar quando o DOM estiver pronto
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            window.drilldownManager = new DrilldownManager();
        });
    } else {
        window.drilldownManager = new DrilldownManager();
    }

    // Adicionar animação suave ao carregar a página
    document.addEventListener('DOMContentLoaded', () => {
        const cards = document.querySelectorAll('[data-drilldown]');
        cards.forEach((card, index) => {
            card.style.opacity = '0';
            card.style.transform = 'translateY(20px)';
            card.style.transition = 'opacity 0.4s ease, transform 0.4s ease';

            setTimeout(() => {
                card.style.opacity = '1';
                card.style.transform = 'translateY(0)';
            }, index * 100);
        });
    });

})();

// Sistema de Autocomplete usando Select2 para todos os selects do sistema
(function() {
    'use strict';

    // Função para inicializar Select2 em um select
    function initSelect2(select) {
        // Verificar se já foi inicializado
        if ($(select).hasClass('select2-hidden-accessible')) {
            return;
        }

        // Verificar se o select tem a classe para desabilitar Select2
        if (select.classList.contains('no-select2')) {
            return;
        }

        // Inicializar Select2
        $(select).select2({
            theme: 'bootstrap-5',
            language: {
                noResults: function() {
                    return "Nenhum resultado encontrado";
                },
                searching: function() {
                    return "Buscando...";
                },
                loadingMore: function() {
                    return "Carregando mais resultados...";
                },
                maximumSelected: function(e) {
                    return "Você só pode selecionar " + e.maximum + " item" + (e.maximum > 1 ? "s" : "");
                },
                removeAllItems: function() {
                    return "Remover todos os itens";
                },
                removeItem: function() {
                    return "Remover item";
                },
                search: function() {
                    return "Buscar";
                }
            },
            width: '100%',
            placeholder: select.querySelector('option[value=""]')?.textContent || 'Selecione...',
            allowClear: select.querySelector('option[value=""]') !== null,
            // Configurações de busca
            minimumResultsForSearch: select.options.length > 5 ? 0 : -1, // Sempre mostrar busca se tiver mais de 5 opções
            // Melhorar performance para selects grandes
            closeOnSelect: true,
            // Permitir busca em português
            matcher: function(params, data) {
                // Se não há termo de busca, mostrar todas as opções
                if ($.trim(params.term) === '') {
                    return data;
                }

                // Normalizar texto para busca (remover acentos)
                function normalizeText(text) {
                    return text.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
                }

                const term = normalizeText(params.term);
                const text = normalizeText(data.text);
                const id = normalizeText(String(data.id));

                // Buscar no texto ou no ID
                if (text.indexOf(term) > -1 || id.indexOf(term) > -1) {
                    return data;
                }

                // Se não encontrou, retornar null para ocultar
                return null;
            }
        });

        // Adicionar classe para identificar selects com Select2
        select.classList.add('select2-initialized');
    }

    // Função para inicializar autocomplete em inputs com datalist
    function initInputAutocomplete(input) {
        // Verificar se já tem datalist ou se já foi inicializado
        if (input.list || input.dataset.autocompleteInitialized === 'true') {
            return;
        }

        // Marcar como inicializado
        input.dataset.autocompleteInitialized = 'true';

        // Criar datalist se o input tiver atributo data-autocomplete
        const autocompleteData = input.dataset.autocomplete;
        if (autocompleteData) {
            try {
                const options = JSON.parse(autocompleteData);
                const datalist = document.createElement('datalist');
                datalist.id = 'autocomplete-' + Math.random().toString(36).substr(2, 9);
                
                options.forEach(option => {
                    const optionElement = document.createElement('option');
                    optionElement.value = typeof option === 'string' ? option : option.value;
                    optionElement.textContent = typeof option === 'string' ? option : (option.text || option.value);
                    datalist.appendChild(optionElement);
                });

                input.setAttribute('list', datalist.id);
                input.parentNode.insertBefore(datalist, input.nextSibling);
            } catch (e) {
                console.error('Erro ao processar dados de autocomplete:', e);
            }
        }
    }

    // Inicializar Select2 em todos os selects quando o DOM estiver pronto
    function initAllSelects() {
        // Aguardar jQuery e Select2 estarem disponíveis
        if (typeof $ === 'undefined' || typeof $.fn.select2 === 'undefined') {
            setTimeout(initAllSelects, 100);
            return;
        }

        // Inicializar em todos os selects (exceto os que têm classe no-select2)
        document.querySelectorAll('select.form-select, select.form-control').forEach(select => {
            // Não inicializar em selects muito pequenos (menos de 3 opções) ou que já têm Select2
            if (select.options.length > 2 && !select.classList.contains('no-select2')) {
                try {
                    initSelect2(select);
                } catch (e) {
                    console.error('Erro ao inicializar Select2 no select:', e);
                }
            }
        });

        // Inicializar em inputs com data-autocomplete
        document.querySelectorAll('input[data-autocomplete]').forEach(input => {
            initInputAutocomplete(input);
        });
    }

    // Inicializar quando o DOM estiver pronto
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            // Aguardar um pouco mais para garantir que jQuery e Select2 estão carregados
            setTimeout(initAllSelects, 200);
        });
    } else {
        setTimeout(initAllSelects, 200);
    }

    // Re-inicializar após mudanças dinâmicas no DOM (usando MutationObserver)
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            mutation.addedNodes.forEach(function(node) {
                if (node.nodeType === 1) { // Element node
                    // Verificar se é um select
                    if (node.tagName === 'SELECT' && 
                        (node.classList.contains('form-select') || node.classList.contains('form-control')) &&
                        !node.classList.contains('no-select2') &&
                        node.options.length > 2) {
                        setTimeout(() => {
                            try {
                                initSelect2(node);
                            } catch (e) {
                                console.error('Erro ao inicializar Select2:', e);
                            }
                        }, 100);
                    }
                    // Verificar se contém selects
                    const selects = node.querySelectorAll && node.querySelectorAll('select.form-select, select.form-control');
                    if (selects) {
                        selects.forEach(select => {
                            if (select.options.length > 2 && 
                                !select.classList.contains('no-select2') &&
                                !select.classList.contains('select2-initialized')) {
                                setTimeout(() => {
                                    try {
                                        initSelect2(select);
                                    } catch (e) {
                                        console.error('Erro ao inicializar Select2:', e);
                                    }
                                }, 100);
                            }
                        });
                    }
                    // Verificar inputs com autocomplete
                    const inputs = node.querySelectorAll && node.querySelectorAll('input[data-autocomplete]');
                    if (inputs) {
                        inputs.forEach(input => {
                            initInputAutocomplete(input);
                        });
                    }
                }
            });
        });
    });

    // Observar mudanças no body
    observer.observe(document.body, {
        childList: true,
        subtree: true
    });

    // Expor função global para inicialização manual se necessário
    window.KlyonAutocomplete = {
        init: initAllSelects,
        initSelect: function(select) {
            if (typeof $ !== 'undefined' && typeof $.fn.select2 !== 'undefined') {
                initSelect2(select);
            } else {
                console.warn('jQuery ou Select2 não estão disponíveis');
            }
        },
        initInput: initInputAutocomplete,
        destroy: function(select) {
            if (typeof $ !== 'undefined' && $(select).hasClass('select2-hidden-accessible')) {
                $(select).select2('destroy');
            }
        }
    };
})();

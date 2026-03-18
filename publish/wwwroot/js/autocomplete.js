(function() {
    'use strict';
    function initSelect2(select) {
        if ($(select).hasClass('select2-hidden-accessible')) {
            return;
        }
        if (select.classList.contains('no-select2')) {
            return;
        }
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
            minimumResultsForSearch: select.options.length > 5 ? 0 : -1,
            closeOnSelect: true,
            matcher: function(params, data) {
                if ($.trim(params.term) === '') {
                    return data;
                }
                function normalizeText(text) {
                    return text.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
                }
                const term = normalizeText(params.term);
                const text = normalizeText(data.text);
                const id = normalizeText(String(data.id));
                if (text.indexOf(term) > -1 || id.indexOf(term) > -1) {
                    return data;
                }
                return null;
            }
        });
        select.classList.add('select2-initialized');
    }
    function initInputAutocomplete(input) {
        if (input.list || input.dataset.autocompleteInitialized === 'true') {
            return;
        }
        input.dataset.autocompleteInitialized = 'true';
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
    function initAllSelects() {
        if (typeof $ === 'undefined' || typeof $.fn.select2 === 'undefined') {
            setTimeout(initAllSelects, 100);
            return;
        }
        document.querySelectorAll('select.form-select, select.form-control').forEach(select => {
            if (select.options.length > 2 && !select.classList.contains('no-select2')) {
                try {
                    initSelect2(select);
                } catch (e) {
                    console.error('Erro ao inicializar Select2 no select:', e);
                }
            }
        });
        document.querySelectorAll('input[data-autocomplete]').forEach(input => {
            initInputAutocomplete(input);
        });
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            setTimeout(initAllSelects, 200);
        });
    } else {
        setTimeout(initAllSelects, 200);
    }
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            mutation.addedNodes.forEach(function(node) {
                if (node.nodeType === 1) {
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
    observer.observe(document.body, {
        childList: true,
        subtree: true
    });
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
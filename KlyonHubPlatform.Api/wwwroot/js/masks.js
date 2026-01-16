// Funções de máscara para campos de formulário

// Máscara de CPF (000.000.000-00)
function applyCPFMask(value) {
    if (!value) return '';
    let numbers = value.replace(/\D/g, '');
    // Limitar a 11 dígitos (máximo para CPF)
    if (numbers.length > 11) {
        numbers = numbers.substring(0, 11);
    }
    if (numbers.length <= 11) {
        numbers = numbers.replace(/(\d{3})(\d)/, '$1.$2');
        numbers = numbers.replace(/(\d{3})(\d)/, '$1.$2');
        numbers = numbers.replace(/(\d{3})(\d{1,2})$/, '$1-$2');
    }
    return numbers;
}

// Máscara de CNPJ (00.000.000/0000-00)
function applyCNPJMask(value) {
    if (!value) return '';
    let numbers = value.replace(/\D/g, '');
    // Limitar a 14 dígitos (máximo para CNPJ)
    if (numbers.length > 14) {
        numbers = numbers.substring(0, 14);
    }
    if (numbers.length <= 14) {
        numbers = numbers.replace(/(\d{2})(\d)/, '$1.$2');
        numbers = numbers.replace(/(\d{3})(\d)/, '$1.$2');
        numbers = numbers.replace(/(\d{3})(\d)/, '$1/$2');
        numbers = numbers.replace(/(\d{4})(\d{1,2})$/, '$1-$2');
    }
    return numbers;
}

// Máscara de Telefone ((00) 00000-0000 ou (00) 0000-0000)
function applyPhoneMask(value) {
    if (!value) return '';
    let numbers = value.replace(/\D/g, '');
    if (numbers.length <= 11) {
        if (numbers.length <= 10) {
            // Telefone fixo: (00) 0000-0000
            numbers = numbers.replace(/(\d{2})(\d)/, '($1) $2');
            numbers = numbers.replace(/(\d{4})(\d)/, '$1-$2');
        } else {
            // Celular: (00) 00000-0000
            numbers = numbers.replace(/(\d{2})(\d)/, '($1) $2');
            numbers = numbers.replace(/(\d{5})(\d)/, '$1-$2');
        }
    }
    return numbers;
}

// Máscara de Data de Nascimento (00/00/0000)
function applyDateMask(value) {
    if (!value) return '';
    let numbers = value.replace(/\D/g, '');
    if (numbers.length <= 8) {
        numbers = numbers.replace(/(\d{2})(\d)/, '$1/$2');
        numbers = numbers.replace(/(\d{2})\/(\d{2})(\d)/, '$1/$2/$3');
    }
    return numbers;
}

// Máscara de CEP (00000-000)
function applyCEPMask(value) {
    if (!value) return '';
    let numbers = value.replace(/\D/g, '');
    // Limitar a 8 dígitos (máximo para CEP)
    if (numbers.length > 8) {
        numbers = numbers.substring(0, 8);
    }
    if (numbers.length <= 8) {
        numbers = numbers.replace(/(\d{5})(\d)/, '$1-$2');
    }
    return numbers;
}

// Aplicar máscaras automaticamente quando a página carregar
document.addEventListener('DOMContentLoaded', function() {
    // Máscara para CPF
    document.querySelectorAll('input[id*="CPF"], input[name*="CPF"], input[id*="cpf"], input[name*="cpf"]').forEach(function(input) {
        if (input.type !== 'hidden' && !input.id.includes('BankPixKey')) {
            // Definir maxlength para 14 caracteres (formato: 000.000.000-00)
            if (!input.hasAttribute('maxlength')) {
                input.setAttribute('maxlength', '14');
            }
            // Aplicar máscara no valor existente
            if (input.value) {
                input.value = applyCPFMask(input.value);
            }
            // Aplicar máscara durante a digitação
            input.addEventListener('input', function(e) {
                let value = e.target.value.replace(/\D/g, '');
                // Limitar a 11 dígitos (máximo para CPF)
                if (value.length > 11) {
                    value = value.substring(0, 11);
                }
                if (value.length <= 11) {
                    value = value.replace(/(\d{3})(\d)/, '$1.$2');
                    value = value.replace(/(\d{3})(\d)/, '$1.$2');
                    value = value.replace(/(\d{3})(\d{1,2})$/, '$1-$2');
                    e.target.value = value;
                } else {
                    e.target.value = applyCPFMask(e.target.value);
                }
            });
            // Prevenir colar valores muito longos
            input.addEventListener('paste', function(e) {
                e.preventDefault();
                let paste = (e.clipboardData || window.clipboardData).getData('text');
                let numbers = paste.replace(/\D/g, '');
                if (numbers.length > 11) {
                    numbers = numbers.substring(0, 11);
                }
                e.target.value = applyCPFMask(numbers);
            });
        }
    });

    // Máscara para CNPJ
    document.querySelectorAll('input[id*="CNPJ"], input[name*="CNPJ"], input[id*="cnpj"], input[name*="cnpj"]').forEach(function(input) {
        if (input.type !== 'hidden' && !input.id.includes('BankPixKey')) {
            // Definir maxlength para 18 caracteres (formato: 00.000.000/0000-00)
            if (!input.hasAttribute('maxlength')) {
                input.setAttribute('maxlength', '18');
            }
            // Aplicar máscara no valor existente
            if (input.value) {
                input.value = applyCNPJMask(input.value);
            }
            // Aplicar máscara durante a digitação
            input.addEventListener('input', function(e) {
                let value = e.target.value.replace(/\D/g, '');
                // Limitar a 14 dígitos (máximo para CNPJ)
                if (value.length > 14) {
                    value = value.substring(0, 14);
                }
                if (value.length <= 14) {
                    value = value.replace(/(\d{2})(\d)/, '$1.$2');
                    value = value.replace(/(\d{3})(\d)/, '$1.$2');
                    value = value.replace(/(\d{3})(\d)/, '$1/$2');
                    value = value.replace(/(\d{4})(\d{1,2})$/, '$1-$2');
                    e.target.value = value;
                } else {
                    e.target.value = applyCNPJMask(e.target.value);
                }
            });
            // Prevenir colar valores muito longos
            input.addEventListener('paste', function(e) {
                e.preventDefault();
                let paste = (e.clipboardData || window.clipboardData).getData('text');
                let numbers = paste.replace(/\D/g, '');
                if (numbers.length > 14) {
                    numbers = numbers.substring(0, 14);
                }
                e.target.value = applyCNPJMask(numbers);
            });
        }
    });

    // Máscara para Telefone
    document.querySelectorAll('input[id*="Phone"], input[name*="Phone"], input[id*="phone"], input[name*="phone"], input[id*="Telefone"], input[name*="Telefone"]').forEach(function(input) {
        if (input.type !== 'hidden' && !input.id.includes('BankPixKey')) {
            // Definir maxlength para 15 caracteres (formato: (00) 00000-0000)
            if (!input.hasAttribute('maxlength')) {
                input.setAttribute('maxlength', '15');
            }
            // Aplicar máscara no valor existente
            if (input.value) {
                input.value = applyPhoneMask(input.value);
            }
            // Aplicar máscara durante a digitação
            input.addEventListener('input', function(e) {
                let value = e.target.value.replace(/\D/g, '');
                // Limitar a 11 dígitos (máximo para telefone brasileiro)
                if (value.length > 11) {
                    value = value.substring(0, 11);
                }
                if (value.length <= 11) {
                    if (value.length <= 10) {
                        value = value.replace(/(\d{2})(\d)/, '($1) $2');
                        value = value.replace(/(\d{4})(\d)/, '$1-$2');
                    } else {
                        value = value.replace(/(\d{2})(\d)/, '($1) $2');
                        value = value.replace(/(\d{5})(\d)/, '$1-$2');
                    }
                    e.target.value = value;
                } else {
                    e.target.value = applyPhoneMask(e.target.value);
                }
            });
            // Prevenir colar valores muito longos
            input.addEventListener('paste', function(e) {
                e.preventDefault();
                let paste = (e.clipboardData || window.clipboardData).getData('text');
                let numbers = paste.replace(/\D/g, '');
                if (numbers.length > 11) {
                    numbers = numbers.substring(0, 11);
                }
                e.target.value = applyPhoneMask(numbers);
            });
        }
    });

    // Máscara para Data de Nascimento
    document.querySelectorAll('input[id*="Birth"], input[name*="Birth"], input[id*="birth"], input[name*="birth"], input[id*="Nascimento"], input[name*="Nascimento"], input[id*="nascimento"], input[name*="nascimento"], input[type="date"][id*="Date"], input[type="date"][name*="Date"]').forEach(function(input) {
        if (input.type !== 'hidden' && input.type !== 'date') {
            // Aplicar máscara no valor existente
            if (input.value) {
                input.value = applyDateMask(input.value);
            }
            // Aplicar máscara durante a digitação
            input.addEventListener('input', function(e) {
                let value = e.target.value.replace(/\D/g, '');
                if (value.length <= 8) {
                    value = value.replace(/(\d{2})(\d)/, '$1/$2');
                    value = value.replace(/(\d{2})\/(\d{2})(\d)/, '$1/$2/$3');
                    e.target.value = value;
                } else {
                    e.target.value = applyDateMask(e.target.value);
                }
            });
        }
    });

    // Máscara para CEP
    document.querySelectorAll('input[id*="ZipCode"], input[name*="ZipCode"], input[id*="CEP"], input[name*="CEP"], input[id*="cep"], input[name*="cep"]').forEach(function(input) {
        if (input.type !== 'hidden') {
            // Definir maxlength para 9 caracteres (formato: 00000-000)
            if (!input.hasAttribute('maxlength')) {
                input.setAttribute('maxlength', '9');
            }
            // Aplicar máscara no valor existente
            if (input.value) {
                input.value = applyCEPMask(input.value);
            }
            // Aplicar máscara durante a digitação
            input.addEventListener('input', function(e) {
                let value = e.target.value.replace(/\D/g, '');
                // Limitar a 8 dígitos (máximo para CEP)
                if (value.length > 8) {
                    value = value.substring(0, 8);
                }
                if (value.length <= 8) {
                    value = value.replace(/(\d{5})(\d)/, '$1-$2');
                    e.target.value = value;
                } else {
                    e.target.value = applyCEPMask(e.target.value);
                }
            });
            // Prevenir colar valores muito longos
            input.addEventListener('paste', function(e) {
                e.preventDefault();
                let paste = (e.clipboardData || window.clipboardData).getData('text');
                let numbers = paste.replace(/\D/g, '');
                if (numbers.length > 8) {
                    numbers = numbers.substring(0, 8);
                }
                e.target.value = applyCEPMask(numbers);
            });
        }
    });
});

function applyCPFMask(value) {
    if (!value) return '';
    let numbers = value.replace(/\D/g, '');
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
function applyCNPJMask(value) {
    if (!value) return '';
    let numbers = value.replace(/\D/g, '');
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
function applyPhoneMask(value) {
    if (!value) return '';
    let numbers = value.replace(/\D/g, '');
    if (numbers.length <= 11) {
        if (numbers.length <= 10) {
            numbers = numbers.replace(/(\d{2})(\d)/, '($1) $2');
            numbers = numbers.replace(/(\d{4})(\d)/, '$1-$2');
        } else {
            numbers = numbers.replace(/(\d{2})(\d)/, '($1) $2');
            numbers = numbers.replace(/(\d{5})(\d)/, '$1-$2');
        }
    }
    return numbers;
}
function applyDateMask(value) {
    if (!value) return '';
    let numbers = value.replace(/\D/g, '');
    if (numbers.length <= 8) {
        numbers = numbers.replace(/(\d{2})(\d)/, '$1/$2');
        numbers = numbers.replace(/(\d{2})\/(\d{2})(\d)/, '$1/$2/$3');
    }
    return numbers;
}
function applyCEPMask(value) {
    if (!value) return '';
    let numbers = value.replace(/\D/g, '');
    if (numbers.length > 8) {
        numbers = numbers.substring(0, 8);
    }
    if (numbers.length <= 8) {
        numbers = numbers.replace(/(\d{5})(\d)/, '$1-$2');
    }
    return numbers;
}
document.addEventListener('DOMContentLoaded', function() {
    document.querySelectorAll('input[id*="CPF"], input[name*="CPF"], input[id*="cpf"], input[name*="cpf"]').forEach(function(input) {
        if (input.type !== 'hidden' && !input.id.includes('BankPixKey')) {
            if (!input.hasAttribute('maxlength')) {
                input.setAttribute('maxlength', '14');
            }
            if (input.value) {
                input.value = applyCPFMask(input.value);
            }
            input.addEventListener('input', function(e) {
                let value = e.target.value.replace(/\D/g, '');
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
    document.querySelectorAll('input[id*="CNPJ"], input[name*="CNPJ"], input[id*="cnpj"], input[name*="cnpj"]').forEach(function(input) {
        if (input.type !== 'hidden' && !input.id.includes('BankPixKey')) {
            if (!input.hasAttribute('maxlength')) {
                input.setAttribute('maxlength', '18');
            }
            if (input.value) {
                input.value = applyCNPJMask(input.value);
            }
            input.addEventListener('input', function(e) {
                let value = e.target.value.replace(/\D/g, '');
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
    document.querySelectorAll('input[id*="Phone"], input[name*="Phone"], input[id*="phone"], input[name*="phone"], input[id*="Telefone"], input[name*="Telefone"], input#contactPhone, input[name="contactPhone"]').forEach(function(input) {
        if (input.type !== 'hidden' && !input.id.includes('BankPixKey')) {
            if (!input.hasAttribute('maxlength')) {
                input.setAttribute('maxlength', '15');
            }
            if (input.value) {
                input.value = applyPhoneMask(input.value);
            }
            input.addEventListener('input', function(e) {
                let value = e.target.value.replace(/\D/g, '');
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
    document.querySelectorAll('input[id*="Birth"], input[name*="Birth"], input[id*="birth"], input[name*="birth"], input[id*="Nascimento"], input[name*="Nascimento"], input[id*="nascimento"], input[name*="nascimento"], input[type="date"][id*="Date"], input[type="date"][name*="Date"]').forEach(function(input) {
        if (input.type !== 'hidden' && input.type !== 'date') {
            if (input.value) {
                input.value = applyDateMask(input.value);
            }
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
    document.querySelectorAll('input[id*="ZipCode"], input[name*="ZipCode"], input[id*="CEP"], input[name*="CEP"], input[id*="cep"], input[name*="cep"]').forEach(function(input) {
        if (input.type !== 'hidden') {
            if (!input.hasAttribute('maxlength')) {
                input.setAttribute('maxlength', '9');
            }
            if (input.value) {
                input.value = applyCEPMask(input.value);
            }
            input.addEventListener('input', function(e) {
                let value = e.target.value.replace(/\D/g, '');
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
// Funções de validação de CPF e CNPJ

/**
 * Valida CPF
 * @param {string} cpf - CPF com ou sem formatação
 * @returns {boolean} - true se válido, false caso contrário
 */
function validateCPF(cpf) {
    if (!cpf) return false;
    
    // Remove formatação
    cpf = cpf.replace(/[^\d]/g, '');
    
    // Verifica se tem 11 dígitos
    if (cpf.length !== 11) return false;
    
    // Verifica se todos os dígitos são iguais (ex: 111.111.111-11)
    if (/^(\d)\1{10}$/.test(cpf)) return false;
    
    // Validação dos dígitos verificadores
    let sum = 0;
    let remainder;
    
    // Valida primeiro dígito verificador
    for (let i = 1; i <= 9; i++) {
        sum += parseInt(cpf.substring(i - 1, i)) * (11 - i);
    }
    remainder = (sum * 10) % 11;
    if (remainder === 10 || remainder === 11) remainder = 0;
    if (remainder !== parseInt(cpf.substring(9, 10))) return false;
    
    // Valida segundo dígito verificador
    sum = 0;
    for (let i = 1; i <= 10; i++) {
        sum += parseInt(cpf.substring(i - 1, i)) * (12 - i);
    }
    remainder = (sum * 10) % 11;
    if (remainder === 10 || remainder === 11) remainder = 0;
    if (remainder !== parseInt(cpf.substring(10, 11))) return false;
    
    return true;
}

/**
 * Valida CNPJ
 * @param {string} cnpj - CNPJ com ou sem formatação
 * @returns {boolean} - true se válido, false caso contrário
 */
function validateCNPJ(cnpj) {
    if (!cnpj) return false;
    
    // Remove formatação
    cnpj = cnpj.replace(/[^\d]/g, '');
    
    // Verifica se tem 14 dígitos
    if (cnpj.length !== 14) return false;
    
    // Verifica se todos os dígitos são iguais (ex: 00.000.000/0000-00)
    if (/^(\d)\1{13}$/.test(cnpj)) return false;
    
    // Validação dos dígitos verificadores
    let length = cnpj.length - 2;
    let numbers = cnpj.substring(0, length);
    let digits = cnpj.substring(length);
    let sum = 0;
    let pos = length - 7;
    
    // Valida primeiro dígito verificador
    for (let i = length; i >= 1; i--) {
        sum += numbers.charAt(length - i) * pos--;
        if (pos < 2) pos = 9;
    }
    let result = sum % 11 < 2 ? 0 : 11 - (sum % 11);
    if (result !== parseInt(digits.charAt(0))) return false;
    
    // Valida segundo dígito verificador
    length = length + 1;
    numbers = cnpj.substring(0, length);
    sum = 0;
    pos = length - 7;
    for (let i = length; i >= 1; i--) {
        sum += numbers.charAt(length - i) * pos--;
        if (pos < 2) pos = 9;
    }
    result = sum % 11 < 2 ? 0 : 11 - (sum % 11);
    if (result !== parseInt(digits.charAt(1))) return false;
    
    return true;
}

/**
 * Valida CPF ou CNPJ (verifica qual é e valida)
 * @param {string} value - CPF ou CNPJ com ou sem formatação
 * @returns {object} - { isValid: boolean, type: 'CPF'|'CNPJ'|null, message: string }
 */
function validateCPForCNPJ(value) {
    if (!value) {
        return { isValid: false, type: null, message: 'CPF ou CNPJ não informado.' };
    }
    
    // Remove formatação
    const clean = value.replace(/[^\d]/g, '');
    
    if (clean.length === 11) {
        const isValid = validateCPF(value);
        return {
            isValid: isValid,
            type: 'CPF',
            message: isValid ? 'CPF válido.' : 'CPF inválido. Verifique os dígitos informados.'
        };
    } else if (clean.length === 14) {
        const isValid = validateCNPJ(value);
        return {
            isValid: isValid,
            type: 'CNPJ',
            message: isValid ? 'CNPJ válido.' : 'CNPJ inválido. Verifique os dígitos informados.'
        };
    } else {
        return {
            isValid: false,
            type: null,
            message: 'CPF deve ter 11 dígitos ou CNPJ deve ter 14 dígitos.'
        };
    }
}

/**
 * Obtém o tipo de pessoa do formulário (Fisica ou Juridica)
 * @param {HTMLFormElement} form
 * @returns {string|null} 'Fisica', 'Juridica' ou null se o formulário não tem seletor de tipo
 */
function getPersonTypeFromForm(form) {
    if (!form) return null;
    var radio = form.querySelector('input[name="PersonTypeRadio"]:checked');
    if (radio) return radio.value;
    var hidden = form.querySelector('input[name="PersonType"], input#PersonType');
    if (hidden && hidden.value) return hidden.value;
    return null;
}

// Aplicar validação em tempo real nos campos CPF e CNPJ
document.addEventListener('DOMContentLoaded', function() {
    // Validação para campo CPF - só valida quando tipo de pessoa é Física
    document.querySelectorAll('input[id="CPF"], input[name="CPF"]').forEach(function(input) {
        if (input.type !== 'hidden') {
            let feedbackDiv = null;
            
            // Criar div de feedback se não existir
            if (!input.nextElementSibling || !input.nextElementSibling.classList.contains('invalid-feedback')) {
                feedbackDiv = document.createElement('div');
                feedbackDiv.className = 'invalid-feedback';
                input.parentNode.appendChild(feedbackDiv);
            } else {
                feedbackDiv = input.nextElementSibling;
            }
            
            // Validação ao perder o foco - só valida CPF se tipo for Pessoa Física
            input.addEventListener('blur', function() {
                var form = input.form;
                var personType = getPersonTypeFromForm(form);
                if (personType === 'Juridica') {
                    input.classList.remove('is-invalid', 'is-valid');
                    feedbackDiv.style.display = 'none';
                    return;
                }
                const value = input.value.trim();
                if (value) {
                    const validation = validateCPForCNPJ(value);
                    if (!validation.isValid) {
                        input.classList.add('is-invalid');
                        input.classList.remove('is-valid');
                        feedbackDiv.textContent = validation.message;
                        feedbackDiv.style.display = 'block';
                    } else {
                        input.classList.add('is-valid');
                        input.classList.remove('is-invalid');
                        feedbackDiv.style.display = 'none';
                    }
                } else {
                    input.classList.remove('is-invalid', 'is-valid');
                    feedbackDiv.style.display = 'none';
                }
            });
            
            // Limpar validação ao digitar
            input.addEventListener('input', function() {
                if (input.classList.contains('is-invalid')) {
                    input.classList.remove('is-invalid');
                    feedbackDiv.style.display = 'none';
                }
            });
        }
    });
    
    // Validação para campo CNPJ - só valida quando tipo de pessoa é Jurídica
    document.querySelectorAll('input[id="CNPJ"], input[name="CNPJ"]').forEach(function(input) {
        if (input.type !== 'hidden') {
            let feedbackDiv = null;
            
            // Criar div de feedback se não existir
            if (!input.nextElementSibling || !input.nextElementSibling.classList.contains('invalid-feedback')) {
                feedbackDiv = document.createElement('div');
                feedbackDiv.className = 'invalid-feedback';
                input.parentNode.appendChild(feedbackDiv);
            } else {
                feedbackDiv = input.nextElementSibling;
            }
            
            // Validação ao perder o foco - só valida CNPJ se tipo for Pessoa Jurídica
            input.addEventListener('blur', function() {
                var form = input.form;
                var personType = getPersonTypeFromForm(form);
                if (personType === 'Fisica') {
                    input.classList.remove('is-invalid', 'is-valid');
                    feedbackDiv.style.display = 'none';
                    return;
                }
                const value = input.value.trim();
                if (value) {
                    const validation = validateCPForCNPJ(value);
                    if (!validation.isValid) {
                        input.classList.add('is-invalid');
                        input.classList.remove('is-valid');
                        feedbackDiv.textContent = validation.message;
                        feedbackDiv.style.display = 'block';
                    } else {
                        input.classList.add('is-valid');
                        input.classList.remove('is-invalid');
                        feedbackDiv.style.display = 'none';
                    }
                } else {
                    input.classList.remove('is-invalid', 'is-valid');
                    feedbackDiv.style.display = 'none';
                }
            });
            
            // Limpar validação ao digitar
            input.addEventListener('input', function() {
                if (input.classList.contains('is-invalid')) {
                    input.classList.remove('is-invalid');
                    feedbackDiv.style.display = 'none';
                }
            });
        }
    });
    
    // Validação no submit do formulário
    document.querySelectorAll('form').forEach(function(form) {
        // Verificar se o formulário tem campos CPF ou CNPJ
        const hasCPF = form.querySelector('input[id="CPF"], input[name="CPF"]');
        const hasCNPJ = form.querySelector('input[id="CNPJ"], input[name="CNPJ"]');
        
        if (hasCPF || hasCNPJ) {
            // Usar capture phase para executar ANTES do loading.js
            form.addEventListener('submit', function(e) {
                let isValid = true;
                let errorMessage = '';
                var focusedOnCPF = false;
                var personType = getPersonTypeFromForm(form);
                
                // Se o formulário tem seletor de tipo de pessoa, validar apenas o documento correspondente
                var shouldValidateCPF = !personType || personType === 'Fisica';
                var shouldValidateCNPJ = !personType || personType === 'Juridica';
                
                // Validar CPF apenas se tipo for Pessoa Física
                if (hasCPF && shouldValidateCPF) {
                    const cpfInput = hasCPF;
                    const cpfValue = cpfInput.value.trim();
                    if (cpfValue) {
                        const validation = validateCPForCNPJ(cpfValue);
                        if (!validation.isValid) {
                            isValid = false;
                            errorMessage = validation.message;
                            cpfInput.classList.add('is-invalid');
                            cpfInput.focus();
                            focusedOnCPF = true;
                        }
                    }
                }
                
                // Validar CNPJ apenas se tipo for Pessoa Jurídica
                if (hasCNPJ && shouldValidateCNPJ) {
                    const cnpjInput = hasCNPJ;
                    const cnpjValue = cnpjInput.value.trim();
                    if (cnpjValue) {
                        const validation = validateCPForCNPJ(cnpjValue);
                        if (!validation.isValid) {
                            isValid = false;
                            if (errorMessage) errorMessage += ' ';
                            errorMessage += validation.message;
                            cnpjInput.classList.add('is-invalid');
                            if (!focusedOnCPF) {
                                cnpjInput.focus();
                            }
                        }
                    }
                }
                
                // Verificar se o documento obrigatório está preenchido conforme o tipo
                const cpfValue = hasCPF ? hasCPF.value.trim() : '';
                const cnpjValue = hasCNPJ ? hasCNPJ.value.trim() : '';
                
                if (personType === 'Fisica') {
                    if (!cpfValue) {
                        isValid = false;
                        errorMessage = 'É necessário informar o CPF para Pessoa Física.';
                        if (hasCPF) {
                            hasCPF.classList.add('is-invalid');
                            hasCPF.focus();
                        }
                    }
                } else if (personType === 'Juridica') {
                    if (!cnpjValue) {
                        isValid = false;
                        errorMessage = 'É necessário informar o CNPJ para Pessoa Jurídica.';
                        if (hasCNPJ) {
                            hasCNPJ.classList.add('is-invalid');
                            hasCNPJ.focus();
                        }
                    }
                } else if (!cpfValue && !cnpjValue) {
                    isValid = false;
                    errorMessage = 'É necessário informar CPF ou CNPJ.';
                    if (hasCNPJ) {
                        hasCNPJ.classList.add('is-invalid');
                        hasCNPJ.focus();
                    } else if (hasCPF) {
                        hasCPF.classList.add('is-invalid');
                        hasCPF.focus();
                    }
                }
                
                if (!isValid) {
                    // Marcar formulário como tendo falha de validação (para evitar que loading.js mostre o loading)
                    form.dataset.validationFailed = 'true';
                    
                    e.preventDefault();
                    e.stopPropagation(); // Impedir que o evento continue propagando
                    e.stopImmediatePropagation(); // Impedir que outros listeners sejam executados
                    
                    // Esconder loading se estiver visível (caso tenha sido mostrado antes da validação)
                    if (typeof window.KlyonLoading !== 'undefined' && window.KlyonLoading.hide) {
                        window.KlyonLoading.hide();
                    }
                    
                    // Usar toast em vez de alert
                    if (typeof showToast === 'function') {
                        showToast(errorMessage || 'Por favor, corrija os erros no formulário antes de enviar.', 'error', 6000);
                    } else {
                        // Fallback caso showToast não esteja disponível
                        alert(errorMessage || 'Por favor, corrija os erros no formulário antes de enviar.');
                    }
                    
                    // Remover a flag após um pequeno delay para permitir nova tentativa
                    setTimeout(function() {
                        if (form.dataset) {
                            delete form.dataset.validationFailed;
                        }
                    }, 100);
                    
                    return false;
                } else {
                    // Limpar flag se a validação passou
                    if (form.dataset && form.dataset.validationFailed) {
                        delete form.dataset.validationFailed;
                    }
                }
            }, true); // Usar capture phase para executar ANTES do loading.js
        }
    });
});

function validateCPF(cpf) {
    if (!cpf) return false;
    cpf = cpf.replace(/[^\d]/g, '');
    if (cpf.length !== 11) return false;
    if (/^(\d)\1{10}$/.test(cpf)) return false;
    let sum = 0;
    let remainder;
    for (let i = 1; i <= 9; i++) {
        sum += parseInt(cpf.substring(i - 1, i)) * (11 - i);
    }
    remainder = (sum * 10) % 11;
    if (remainder === 10 || remainder === 11) remainder = 0;
    if (remainder !== parseInt(cpf.substring(9, 10))) return false;
    sum = 0;
    for (let i = 1; i <= 10; i++) {
        sum += parseInt(cpf.substring(i - 1, i)) * (12 - i);
    }
    remainder = (sum * 10) % 11;
    if (remainder === 10 || remainder === 11) remainder = 0;
    if (remainder !== parseInt(cpf.substring(10, 11))) return false;
    return true;
}
function validateCNPJ(cnpj) {
    if (!cnpj) return false;
    cnpj = cnpj.replace(/[^\d]/g, '');
    if (cnpj.length !== 14) return false;
    if (/^(\d)\1{13}$/.test(cnpj)) return false;
    let length = cnpj.length - 2;
    let numbers = cnpj.substring(0, length);
    let digits = cnpj.substring(length);
    let sum = 0;
    let pos = length - 7;
    for (let i = length; i >= 1; i--) {
        sum += numbers.charAt(length - i) * pos--;
        if (pos < 2) pos = 9;
    }
    let result = sum % 11 < 2 ? 0 : 11 - (sum % 11);
    if (result !== parseInt(digits.charAt(0))) return false;
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
function validateCPForCNPJ(value) {
    if (!value) {
        return { isValid: false, type: null, message: 'CPF ou CNPJ não informado.' };
    }
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
function getPersonTypeFromForm(form) {
    if (!form) return null;
    var radio = form.querySelector('input[name="PersonTypeRadio"]:checked');
    if (radio) return radio.value;
    var hidden = form.querySelector('input[name="PersonType"], input#PersonType');
    if (hidden && hidden.value) return hidden.value;
    return null;
}
document.addEventListener('DOMContentLoaded', function() {
    document.querySelectorAll('input[id="CPF"], input[name="CPF"]').forEach(function(input) {
        if (input.type !== 'hidden') {
            let feedbackDiv = null;
            if (!input.nextElementSibling || !input.nextElementSibling.classList.contains('invalid-feedback')) {
                feedbackDiv = document.createElement('div');
                feedbackDiv.className = 'invalid-feedback';
                input.parentNode.appendChild(feedbackDiv);
            } else {
                feedbackDiv = input.nextElementSibling;
            }
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
            input.addEventListener('input', function() {
                if (input.classList.contains('is-invalid')) {
                    input.classList.remove('is-invalid');
                    feedbackDiv.style.display = 'none';
                }
            });
        }
    });
    document.querySelectorAll('input[id="CNPJ"], input[name="CNPJ"]').forEach(function(input) {
        if (input.type !== 'hidden') {
            let feedbackDiv = null;
            if (!input.nextElementSibling || !input.nextElementSibling.classList.contains('invalid-feedback')) {
                feedbackDiv = document.createElement('div');
                feedbackDiv.className = 'invalid-feedback';
                input.parentNode.appendChild(feedbackDiv);
            } else {
                feedbackDiv = input.nextElementSibling;
            }
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
            input.addEventListener('input', function() {
                if (input.classList.contains('is-invalid')) {
                    input.classList.remove('is-invalid');
                    feedbackDiv.style.display = 'none';
                }
            });
        }
    });
    document.querySelectorAll('form').forEach(function(form) {
        const hasCPF = form.querySelector('input[id="CPF"], input[name="CPF"]');
        const hasCNPJ = form.querySelector('input[id="CNPJ"], input[name="CNPJ"]');
        if (hasCPF || hasCNPJ) {
            form.addEventListener('submit', function(e) {
                let isValid = true;
                let errorMessage = '';
                var focusedOnCPF = false;
                var personType = getPersonTypeFromForm(form);
                var shouldValidateCPF = !personType || personType === 'Fisica';
                var shouldValidateCNPJ = !personType || personType === 'Juridica';
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
                    form.dataset.validationFailed = 'true';
                    e.preventDefault();
                    e.stopPropagation();
                    e.stopImmediatePropagation();
                    if (typeof window.KlyonLoading !== 'undefined' && window.KlyonLoading.hide) {
                        window.KlyonLoading.hide();
                    }
                    if (typeof showToast === 'function') {
                        showToast(errorMessage || 'Por favor, corrija os erros no formulário antes de enviar.', 'error', 6000);
                    } else {
                        alert(errorMessage || 'Por favor, corrija os erros no formulário antes de enviar.');
                    }
                    setTimeout(function() {
                        if (form.dataset) {
                            delete form.dataset.validationFailed;
                        }
                    }, 100);
                    return false;
                } else {
                    if (form.dataset && form.dataset.validationFailed) {
                        delete form.dataset.validationFailed;
                    }
                }
            }, true);
        }
    });
});
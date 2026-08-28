(function () {
    'use strict';

    function maskBrazilianCep(value) {
        var d = (value || '').replace(/\D/g, '').substring(0, 8);
        return d.length > 5 ? d.substring(0, 5) + '-' + d.substring(5) : d;
    }

    function applyNoNumberToggle(checkEl, numberEl) {
        if (!checkEl || !numberEl) return;
        numberEl.disabled = checkEl.checked;
        if (checkEl.checked) {
            numberEl.removeAttribute('required');
            numberEl.value = '';
        } else {
            numberEl.setAttribute('required', 'required');
        }
    }

    function getPayModalConfig() {
        var modalEl = document.getElementById('payWithCardModal');
        if (!modalEl) return null;
        return {
            modalEl: modalEl,
            settlementMode: modalEl.getAttribute('data-settlement-mode') === 'true',
            lockedPlan: modalEl.getAttribute('data-locked-plan') || '1x',
            processUrl: modalEl.getAttribute('data-process-url') || '',
            successUrl: modalEl.getAttribute('data-success-url') || ''
        };
    }

    function readCreditCards() {
        var cardsEl = document.getElementById('paymentDetailsCreditCards');
        if (!cardsEl || !cardsEl.textContent) return [];
        try {
            return JSON.parse(cardsEl.textContent.trim());
        } catch (e) {
            return [];
        }
    }

    function payModalNeedsBillingInput() {
        var useSaved = document.getElementById('payModalOptionSaved')?.checked;
        if (!useSaved) return true;
        var savedRadio = document.querySelector('input[name="payModalSavedCardId"]:checked');
        if (!savedRadio) return true;
        return savedRadio.dataset.hasBilling !== '1';
    }

    function updatePayModalBillingVisibility() {
        var billingSection = document.getElementById('payModalBillingSection');
        var hint = document.getElementById('payModalBillingHint');
        if (!billingSection) return;
        var need = payModalNeedsBillingInput();
        billingSection.classList.toggle('d-none', !need);
        if (hint) {
            var useSaved = document.getElementById('payModalOptionSaved')?.checked;
            hint.textContent = useSaved && need
                ? 'Este cartão ainda não tem CEP/número salvos. Informe uma vez para concluir o pagamento.'
                : 'CEP e número do titular, exigidos pelo cartão. PIX e boleto não pedem estes dados.';
        }
        if (need) {
            var savedRadio = document.querySelector('input[name="payModalSavedCardId"]:checked');
            var cepEl = document.getElementById('payCardPostalCode');
            var numEl = document.getElementById('payCardAddressNumber');
            var noNumEl = document.getElementById('payCardNoNumber');
            if (savedRadio && savedRadio.dataset.postalCode && cepEl && !(cepEl.value || '').replace(/\D/g, '')) {
                cepEl.value = savedRadio.dataset.postalCode;
            }
            if (savedRadio && savedRadio.dataset.addressNumber && numEl && noNumEl) {
                var storedNum = savedRadio.dataset.addressNumber;
                if (storedNum.toUpperCase() === 'S/N') {
                    noNumEl.checked = true;
                    applyNoNumberToggle(noNumEl, numEl);
                } else if (!(numEl.value || '').trim()) {
                    noNumEl.checked = false;
                    applyNoNumberToggle(noNumEl, numEl);
                    numEl.value = storedNum;
                }
            }
        }
    }

    function updateModalSavedVsNewCardVisibility() {
        var sectionSaved = document.getElementById('payModalSavedCardsSection');
        var sectionNew = document.getElementById('payModalNewCardSection');
        var payModalRevalidateMsg = document.getElementById('payModalRevalidateMsg');
        var savedCardConfirmMsg = document.getElementById('payModalSavedCardConfirmMsg');
        var newCardIntro = document.getElementById('payModalNewCardIntro');
        if (!sectionSaved || !sectionNew) return;
        var useSaved = document.getElementById('payModalOptionSaved')?.checked;
        if (!useSaved) {
            sectionSaved.classList.add('d-none');
            sectionNew.classList.remove('d-none');
            if (payModalRevalidateMsg) payModalRevalidateMsg.classList.add('d-none');
            if (savedCardConfirmMsg) savedCardConfirmMsg.classList.add('d-none');
            if (newCardIntro) newCardIntro.classList.remove('d-none');
            updatePayModalBillingVisibility();
            return;
        }
        sectionSaved.classList.remove('d-none');
        if (savedCardConfirmMsg) savedCardConfirmMsg.classList.add('d-none');
        var savedRadio = document.querySelector('input[name="payModalSavedCardId"]:checked');
        var needsRevalidate = savedRadio && savedRadio.dataset.hasToken === '0';
        if (needsRevalidate) {
            sectionNew.classList.remove('d-none');
            if (payModalRevalidateMsg) payModalRevalidateMsg.classList.remove('d-none');
            if (newCardIntro) newCardIntro.classList.add('d-none');
            var cards = readCreditCards();
            var cardId = savedRadio ? parseInt(savedRadio.value, 10) : 0;
            var card = cards.find(function (c) { return (c.id || c.Id) === cardId; });
            if (card) {
                var payHolder = document.getElementById('payCardHolderName');
                var payMonth = document.getElementById('payCardExpiryMonth');
                var payYear = document.getElementById('payCardExpiryYear');
                if (payHolder && card.cardHolderName) payHolder.value = card.cardHolderName;
                if (payMonth && card.expiryMonth) payMonth.value = card.expiryMonth;
                if (payYear && card.expiryYear) {
                    payYear.value = card.expiryYear.length === 4 ? card.expiryYear : (card.expiryYear.length === 2 ? '20' + card.expiryYear : card.expiryYear);
                }
            }
        } else {
            sectionNew.classList.add('d-none');
            if (payModalRevalidateMsg) payModalRevalidateMsg.classList.add('d-none');
            if (newCardIntro) newCardIntro.classList.add('d-none');
            if (savedCardConfirmMsg && savedRadio && savedRadio.dataset.hasBilling === '1') {
                savedCardConfirmMsg.classList.remove('d-none');
            }
        }
        updatePayModalBillingVisibility();
    }

    function collectCardHolderBilling(errEl) {
        if (!payModalNeedsBillingInput()) {
            return { skip: true };
        }
        var cepEl = document.getElementById('payCardPostalCode');
        var numEl = document.getElementById('payCardAddressNumber');
        var noNumEl = document.getElementById('payCardNoNumber');
        var cep = ((cepEl && cepEl.value) || '').replace(/\D/g, '').substring(0, 8);
        var noNumber = !!(noNumEl && noNumEl.checked);
        var number = ((numEl && numEl.value) || '').trim();
        if (cep.length !== 8 || /^0+$/.test(cep)) {
            if (errEl) {
                errEl.textContent = 'Informe um CEP válido (8 dígitos). O CEP 00000-000 não é aceito.';
                errEl.classList.remove('d-none');
                errEl.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            }
            if (cepEl) cepEl.focus();
            return null;
        }
        if (!noNumber && !number) {
            if (errEl) {
                errEl.textContent = 'Informe o número do endereço ou marque "sem número".';
                errEl.classList.remove('d-none');
                errEl.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            }
            if (numEl) numEl.focus();
            return null;
        }
        return {
            postalCode: cep,
            addressNumber: noNumber ? 'S/N' : number,
            addressHasNoNumber: noNumber
        };
    }

    function resolveCardPlanOption(config) {
        if (config.settlementMode) {
            var locked = document.getElementById('payCardPlanLocked');
            return (locked && locked.value) ? locked.value : config.lockedPlan;
        }
        var planRadio = document.querySelector('input[name="payCardPlanOption"]:checked');
        return planRadio ? planRadio.value : '';
    }

    function populateSavedCardsOnModalOpen() {
        var cards = readCreditCards();
        var optSaved = document.getElementById('payModalOptionSaved');
        var optNew = document.getElementById('payModalOptionNew');
        var sectionSaved = document.getElementById('payModalSavedCardsSection');
        var sectionNew = document.getElementById('payModalNewCardSection');
        var listSaved = document.getElementById('payModalSavedCardsList');
        if (cards.length > 0) {
            if (optSaved && optSaved.parentElement) optSaved.parentElement.style.display = '';
            if (optSaved) optSaved.checked = true;
            if (optNew) optNew.checked = false;
            if (sectionSaved) sectionSaved.classList.remove('d-none');
            if (sectionNew) sectionNew.classList.add('d-none');
            if (listSaved) {
                listSaved.innerHTML = '';
                var defaultCard = cards.find(function (c) { return c.isDefault; }) || cards[0];
                cards.forEach(function (c) {
                    var id = c.id || c.Id;
                    var item = document.createElement('label');
                    item.className = 'list-group-item list-group-item-action d-flex align-items-center';
                    item.style.cursor = 'pointer';
                    var radio = document.createElement('input');
                    radio.type = 'radio';
                    radio.name = 'payModalSavedCardId';
                    radio.value = id;
                    radio.className = 'form-check-input me-2';
                    radio.dataset.hasToken = c.hasToken ? '1' : '0';
                    radio.dataset.hasBilling = c.hasBilling ? '1' : '0';
                    if (c.postalCode) radio.dataset.postalCode = c.postalCode;
                    if (c.addressNumber) radio.dataset.addressNumber = c.addressNumber;
                    if (id === (defaultCard.id || defaultCard.Id)) radio.checked = true;
                    item.appendChild(radio);
                    var text = document.createElement('span');
                    var displayName = (c.nickname && c.nickname.trim())
                        ? (c.nickname.trim() + ' - ' + (c.brand || 'Cartão') + ' •••• ' + (c.lastFourDigits || ''))
                        : ((c.brand || 'Cartão') + ' •••• ' + (c.lastFourDigits || ''));
                    if (!c.hasToken) displayName += ' <span class="badge bg-warning text-dark ms-1">Revalidar</span>';
                    text.innerHTML = displayName + (c.isDefault ? ' <span class="badge bg-primary ms-1">Padrão</span>' : '');
                    item.appendChild(text);
                    item.addEventListener('click', function () {
                        radio.checked = true;
                        updateModalSavedVsNewCardVisibility();
                    });
                    listSaved.appendChild(item);
                });
            }
        } else {
            if (optSaved && optSaved.parentElement) optSaved.parentElement.style.display = 'none';
            if (optNew) optNew.checked = true;
            if (sectionSaved) sectionSaved.classList.add('d-none');
            if (sectionNew) sectionNew.classList.remove('d-none');
        }
        ['payCardHolderName', 'payCardExpiryMonth', 'payCardExpiryYear', 'payCardNumber', 'payCardCvv'].forEach(function (id) {
            var el = document.getElementById(id);
            if (el) el.value = '';
        });
        var saveCheck = document.getElementById('payModalSaveCard');
        var defaultWrap = document.getElementById('payModalSetDefaultWrap');
        if (saveCheck && defaultWrap) defaultWrap.style.display = saveCheck.checked ? 'block' : 'none';
        updateModalSavedVsNewCardVisibility();
    }

    function openPayWithCardModal() {
        var modalEl = document.getElementById('payWithCardModal');
        if (modalEl && typeof bootstrap !== 'undefined') {
            bootstrap.Modal.getOrCreateInstance(modalEl).show();
        }
    }

    function initPayWithCardModal() {
        var modalEl = document.getElementById('payWithCardModal');
        if (!modalEl) return;
        modalEl.addEventListener('show.bs.modal', populateSavedCardsOnModalOpen);
    }

    function initEditNicknameModal() {
        var editCardNicknameModal = document.getElementById('editCardNicknameModal');
        if (!editCardNicknameModal) return;
        editCardNicknameModal.addEventListener('show.bs.modal', function (event) {
            var button = event.relatedTarget;
            if (!button || !button.classList.contains('edit-card-nickname-btn')) return;
            var cardIdInput = document.getElementById('editCardNicknameCardId');
            var nicknameInput = document.getElementById('editCardNicknameInput');
            var labelEl = document.getElementById('editCardNicknameCardLabel');
            if (cardIdInput) cardIdInput.value = button.getAttribute('data-card-id') || '';
            if (nicknameInput) {
                nicknameInput.value = button.getAttribute('data-card-nickname') || '';
                nicknameInput.setCustomValidity('');
            }
            if (labelEl) {
                var label = button.getAttribute('data-card-label') || '';
                labelEl.textContent = label ? ('Cartão: ' + label) : '';
            }
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        initPayWithCardModal();
        initEditNicknameModal();
        applyNoNumberToggle(document.getElementById('payCardNoNumber'), document.getElementById('payCardAddressNumber'));
        applyNoNumberToggle(document.getElementById('addCardNoNumber'), document.getElementById('addCardAddressNumber'));
    });

    document.body.addEventListener('input', function (e) {
        if (!e.target) return;
        if (e.target.id === 'payCardPostalCode' || e.target.id === 'addCardPostalCode') {
            e.target.value = maskBrazilianCep(e.target.value);
        }
        if (e.target.id === 'payCardNumber' || e.target.id === 'cardNumber') {
            var v = e.target.value.replace(/\D/g, '');
            e.target.value = (v.match(/.{1,4}/g) || []).join(' ').substring(0, 19);
        }
        if (e.target.id === 'payCardCvv' || e.target.id === 'cvv') {
            e.target.value = e.target.value.replace(/\D/g, '');
        }
    });

    document.body.addEventListener('change', function (e) {
        if (!e.target) return;
        if (e.target.id === 'payCardNoNumber') {
            applyNoNumberToggle(e.target, document.getElementById('payCardAddressNumber'));
        }
        if (e.target.id === 'addCardNoNumber') {
            applyNoNumberToggle(e.target, document.getElementById('addCardAddressNumber'));
        }
        if (e.target.name === 'payModalOption' || e.target.name === 'payModalSavedCardId') {
            updateModalSavedVsNewCardVisibility();
        }
        if (e.target.id === 'payModalSaveCard') {
            var wrap = document.getElementById('payModalSetDefaultWrap');
            if (wrap) wrap.style.display = e.target.checked ? 'block' : 'none';
            if (!e.target.checked) {
                var defChk = document.getElementById('payModalSetDefault');
                if (defChk) defChk.checked = false;
            }
        }
    });

    document.body.addEventListener('submit', function (e) {
        var form = e.target;
        if (!form) return;
        if (form.id === 'editCardNicknameForm') {
            var nicknameInput = document.getElementById('editCardNicknameInput');
            if (!nicknameInput) return;
            nicknameInput.setCustomValidity('');
            var value = (nicknameInput.value || '').trim();
            if (!value) {
                e.preventDefault();
                nicknameInput.setCustomValidity('Informe um apelido para o cartão.');
                nicknameInput.reportValidity();
                return;
            }
            if (value.length > 100) {
                e.preventDefault();
                nicknameInput.setCustomValidity('O apelido do cartão deve ter no máximo 100 caracteres.');
                nicknameInput.reportValidity();
            }
            return;
        }
        if (form.id !== 'addCardForm') return;
        var cepEl = document.getElementById('addCardPostalCode');
        var numEl = document.getElementById('addCardAddressNumber');
        var noNumEl = document.getElementById('addCardNoNumber');
        var cep = ((cepEl && cepEl.value) || '').replace(/\D/g, '');
        if (cepEl) cepEl.setCustomValidity('');
        if (numEl) numEl.setCustomValidity('');
        if (cep.length !== 8 || /^0+$/.test(cep)) {
            e.preventDefault();
            if (cepEl) {
                cepEl.setCustomValidity('Informe um CEP válido (8 dígitos). O CEP 00000-000 não é aceito.');
                cepEl.reportValidity();
            }
            return;
        }
        if (!(noNumEl && noNumEl.checked) && !((numEl && numEl.value) || '').trim()) {
            e.preventDefault();
            if (numEl) {
                numEl.setCustomValidity('Informe o número do endereço ou marque "sem número".');
                numEl.reportValidity();
            }
        }
    });

    document.body.addEventListener('click', function (e) {
        var payBtn = e.target.closest('#btnPagarComCartao');
        if (payBtn) {
            e.preventDefault();
            e.stopPropagation();
            var pagePlanWrap = document.getElementById('pageCardPlanOptionWrap');
            var pagePlanRadios = pagePlanWrap ? pagePlanWrap.querySelectorAll('input[name="pageCardPlanOption"]') : [];
            if (pagePlanRadios.length > 0) {
                var pagePlan = document.querySelector('input[name="pageCardPlanOption"]:checked');
                if (!pagePlan || !pagePlan.value) {
                    var errBox = document.getElementById('pageCardPlanOptionError');
                    if (pagePlanWrap) {
                        pagePlanWrap.classList.add('border', 'border-2', 'border-danger', 'rounded', 'p-2', 'bg-danger', 'bg-opacity-10');
                        pagePlanWrap.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    }
                    if (errBox) errBox.classList.remove('d-none');
                    return;
                }
            }
            openPayWithCardModal();
            return;
        }

        var btn = e.target.closest('#payWithCardSubmitBtn');
        if (!btn) return;
        e.preventDefault();
        handlePayWithCardSubmit(btn);
    }, true);

    async function handlePayWithCardSubmit(btn) {
        var config = getPayModalConfig();
        if (!config || !config.processUrl) return;
        var errEl = document.getElementById('payWithCardError');
        var paymentIdEl = document.getElementById('payWithCardPaymentId');
        if (!errEl || !paymentIdEl) return;
        var paymentId = parseInt(paymentIdEl.value, 10);
        if (isNaN(paymentId) || paymentId <= 0) return;
        errEl.classList.add('d-none');
        errEl.textContent = '';

        var cardPlanOption = resolveCardPlanOption(config);
        if (!cardPlanOption) {
            var modalWrap = document.getElementById('payModalPlanOptionWrap');
            var modalErrBox = document.getElementById('payModalPlanOptionError');
            if (modalWrap) {
                modalWrap.classList.add('border', 'border-2', 'border-danger', 'rounded', 'p-2', 'bg-danger', 'bg-opacity-10');
                modalWrap.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            }
            if (modalErrBox) modalErrBox.classList.remove('d-none');
            return;
        }

        var useSaved = document.getElementById('payModalOptionSaved')?.checked;
        var payload = { paymentId: paymentId, installments: 1, cardPlanOption: cardPlanOption };

        if (useSaved) {
            var savedRadio = document.querySelector('input[name="payModalSavedCardId"]:checked');
            if (!savedRadio) {
                errEl.textContent = 'Selecione um cartão cadastrado.';
                errEl.classList.remove('d-none');
                return;
            }
            payload.creditCardId = parseInt(savedRadio.value, 10);
            if (savedRadio.dataset.hasToken === '0') {
                var cardPayload = buildNewCardPayload(errEl);
                if (!cardPayload) return;
                payload.card = cardPayload;
            }
        } else {
            var newCardPayload = buildNewCardPayload(errEl);
            if (!newCardPayload) return;
            payload.card = newCardPayload;
            var saveCardChk = document.getElementById('payModalSaveCard');
            var setDefaultChk = document.getElementById('payModalSetDefault');
            payload.saveCard = !!(saveCardChk && saveCardChk.checked);
            payload.setAsDefault = !!(setDefaultChk && setDefaultChk.checked);
        }

        var billing = collectCardHolderBilling(errEl);
        if (!billing) return;
        if (!billing.skip) {
            payload.postalCode = billing.postalCode;
            payload.addressNumber = billing.addressNumber;
            payload.addressHasNoNumber = billing.addressHasNoNumber;
        }

        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Processando...';
        if (window.KlyonLoading && window.KlyonLoading.show) window.KlyonLoading.show();
        try {
            var response = await fetch(config.processUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'same-origin',
                body: JSON.stringify(payload)
            });
            var text = await response.text();
            var result = null;
            try { result = text ? JSON.parse(text) : null; } catch (_) { }
            if (!result) {
                if (window.KlyonLoading && window.KlyonLoading.hide) window.KlyonLoading.hide();
                errEl.textContent = response.ok ? 'Resposta inválida do servidor. Tente novamente.' : ('Erro ' + response.status + (text && text.length < 200 ? ': ' + text : '. Verifique os dados e tente novamente.'));
                errEl.classList.remove('d-none');
                btn.disabled = false;
                btn.innerHTML = '<i class="bi bi-credit-card"></i> Pagar agora';
                return;
            }
            if (result.success) {
                if (window.KlyonLoading && window.KlyonLoading.hide) window.KlyonLoading.hide();
                var modal = bootstrap.Modal.getInstance(document.getElementById('payWithCardModal'));
                if (modal) modal.hide();
                var targetUrl = result.redirectUrl || config.successUrl;
                window.setTimeout(function () { window.location.href = targetUrl; }, 400);
            } else {
                if (window.KlyonLoading && window.KlyonLoading.hide) window.KlyonLoading.hide();
                var payErrorMsg = result.error || 'Erro ao processar pagamento.';
                errEl.textContent = payErrorMsg;
                errEl.classList.remove('d-none');
                errEl.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                if (typeof showToast === 'function') showToast(payErrorMsg, 'error', 12000);
                btn.disabled = false;
                btn.innerHTML = '<i class="bi bi-credit-card"></i> Pagar agora';
            }
        } catch (ex) {
            if (window.KlyonLoading && window.KlyonLoading.hide) window.KlyonLoading.hide();
            errEl.textContent = 'Erro de conexão: ' + (ex.message || 'Tente novamente.');
            errEl.classList.remove('d-none');
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-credit-card"></i> Pagar agora';
        }
    }

    function buildNewCardPayload(errEl) {
        var numEl = document.getElementById('payCardNumber');
        var nameEl = document.getElementById('payCardHolderName');
        var monthEl = document.getElementById('payCardExpiryMonth');
        var yearEl = document.getElementById('payCardExpiryYear');
        var cvvEl = document.getElementById('payCardCvv');
        if (!numEl || !nameEl || !monthEl || !yearEl || !cvvEl) return null;
        var number = (numEl.value || '').replace(/\s/g, '');
        var holderName = (nameEl.value || '').trim();
        var expMonth = monthEl.value;
        var expYear = yearEl.value;
        var securityCode = (cvvEl.value || '').trim();
        if (number.length < 13 || number.length > 19) {
            errEl.textContent = 'Número do cartão inválido. Informe entre 13 e 19 dígitos.';
            errEl.classList.remove('d-none');
            return null;
        }
        if (!holderName || holderName.length < 2) {
            errEl.textContent = 'Nome no cartão é obrigatório (mínimo 2 caracteres).';
            errEl.classList.remove('d-none');
            return null;
        }
        if (!/[a-zA-Z\u00C0-\u024F]/.test(holderName)) {
            errEl.textContent = 'Nome do portador deve conter pelo menos uma letra.';
            errEl.classList.remove('d-none');
            return null;
        }
        if (!expMonth || !expYear) {
            errEl.textContent = 'Data de validade inválida. Selecione mês e ano.';
            errEl.classList.remove('d-none');
            return null;
        }
        if (securityCode.length < 3 || securityCode.length > 4) {
            errEl.textContent = 'CVV inválido. Informe 3 ou 4 dígitos.';
            errEl.classList.remove('d-none');
            return null;
        }
        var year = expYear.length === 2 ? '20' + expYear : expYear;
        var nicknameEl = document.getElementById('payCardNickname');
        return {
            number: number,
            expirationMonth: expMonth,
            expirationYear: year,
            securityCode: securityCode,
            holderName: holderName,
            nickname: (nicknameEl && nicknameEl.value) ? nicknameEl.value.trim() : null
        };
    }

    window.openPayWithCardModal = openPayWithCardModal;
})();

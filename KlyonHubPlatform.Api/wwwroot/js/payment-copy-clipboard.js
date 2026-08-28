(function () {
    'use strict';

    function copyToClipboardWithButtonFeedback(elementId, ev) {
        var el = document.getElementById(elementId);
        if (!el) return;

        el.focus();
        el.select();
        if (typeof el.setSelectionRange === 'function') {
            el.setSelectionRange(0, el.value.length);
        }

        var copied = false;
        try {
            copied = document.execCommand('copy');
        } catch (e) {
            copied = false;
        }

        if (!copied && navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
            navigator.clipboard.writeText(el.value).catch(function () { /* feedback still shown */ });
        }

        var button = (ev && ev.target) ? ev.target.closest('button') : null;
        if (button) {
            var originalHtml = button.innerHTML;
            button.innerHTML = '<i class="bi bi-check"></i> Copiado!';
            button.classList.add('btn-success');
            button.classList.remove('btn-outline-secondary');
            setTimeout(function () {
                button.innerHTML = originalHtml;
                button.classList.remove('btn-success');
                button.classList.add('btn-outline-secondary');
            }, 2000);
        }
    }

    window.copyToClipboardWithButtonFeedback = copyToClipboardWithButtonFeedback;
    window.copyPixCode = function (ev) {
        copyToClipboardWithButtonFeedback('pixCode', ev);
    };
    window.copyBoletoBarcode = function (ev) {
        copyToClipboardWithButtonFeedback('boletoBarcode', ev);
    };
    window.copyBoletoCode = function (ev) {
        copyToClipboardWithButtonFeedback('boletoCode', ev);
    };
})();

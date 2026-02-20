/**
 * Nomes e valores de headers HTTP usados no frontend (fetch/XHR).
 * Devem ser consistentes com KlyonHubPlatform.Domain.Core.Constants.HttpHeaderNames no backend.
 * Uso: headers: { [window.KlyonHeaders.XRequestedWith]: window.KlyonHeaders.XRequestedWithValue }
 */
(function (global) {
    'use strict';
    global.KlyonHeaders = {
        // Request headers (enviados pelo cliente)
        XRequestedWith: 'X-Requested-With',
        XRequestedWithValue: 'XMLHttpRequest',
        ContentType: 'Content-Type',
        ContentTypeJson: 'application/json',
        Accept: 'Accept',
        AcceptJson: 'application/json'
    };
})(typeof window !== 'undefined' ? window : this);

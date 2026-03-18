(function (global) {
    'use strict';
    global.KlyonHeaders = {
        XRequestedWith: 'X-Requested-With',
        XRequestedWithValue: 'XMLHttpRequest',
        ContentType: 'Content-Type',
        ContentTypeJson: 'application/json',
        Accept: 'Accept',
        AcceptJson: 'application/json'
    };
})(typeof window !== 'undefined' ? window : this);
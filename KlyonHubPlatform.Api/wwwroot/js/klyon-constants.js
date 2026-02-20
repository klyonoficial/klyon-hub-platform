var Klyon = Klyon || {};
Klyon.ToastType = {
    Success: 'success',
    Error: 'error',
    Warning: 'warning',
    Info: 'info'
};
Klyon.BadgeCss = {
    Success: 'success',
    Danger: 'danger',
    Warning: 'warning',
    Info: 'info',
    Primary: 'primary',
    Secondary: 'secondary'
};
Klyon.ToastTypes = {
    success: {
        class: 'bg-success text-white',
        icon: 'bi-check-circle-fill',
        title: 'Sucesso',
        closeClass: 'btn-close btn-close-white'
    },
    error: {
        class: 'bg-danger text-white',
        icon: 'bi-exclamation-triangle-fill',
        title: 'Erro',
        closeClass: 'btn-close btn-close-white'
    },
    warning: {
        class: 'bg-warning text-dark',
        icon: 'bi-exclamation-triangle-fill',
        title: 'Atenção',
        closeClass: 'btn-close'
    },
    info: {
        class: 'bg-info text-white',
        icon: 'bi-info-circle-fill',
        title: 'Informação',
        closeClass: 'btn-close btn-close-white'
    }
};
Klyon.ModalIds = {
    CardPricePendingListModal: 'cardPricePendingListModal',
    CardPricePendingListModalBody: 'cardPricePendingListModalBody',
    CardPriceDetailModal: 'cardPriceDetailModal',
    CardPriceDetailModalBody: 'cardPriceDetailModalBody',
    CardPriceDetailModalLabel: 'cardPriceDetailModalLabel',
    CardPriceRejectReasonModal: 'cardPriceRejectReasonModal',
    RejectCardPriceRequestIdAjax: 'rejectCardPriceRequestIdAjax',
    RejectCardPriceReasonAjax: 'rejectCardPriceReasonAjax',
    CardPriceDetailApproveBtn: 'cardPriceDetailApproveBtn',
    CardPriceDetailRejectBtn: 'cardPriceDetailRejectBtn'
};
Klyon.FormField = {
    Status: 'status',
    Id: 'id',
    Event: 'event'
};
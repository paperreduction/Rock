(function ($) {
    'use strict';
    window.Rock = window.Rock || {};
    Rock.NMI = Rock.NMI || {}
    Rock.NMI.controls = Rock.NMI.controls || {};

    /** JS helper for the gatewayCollectJS */
    Rock.NMI.controls.gatewayCollectJS = (function () {
        var exports = {
            initialize: function (controlId) {
                var $control = $('#' + controlId);

                if ($control.length == 0) {
                    // control hasn't been rendered so skip
                    return;
                }

                var self = this;
                self.$creditCardInput = $('.js-credit-card-input', $control);
                self.$creditCardExpInput = $('.js-credit-card-exp-input', $control);
                self.$creditCardCVVInput = $('.js-credit-card-cvv-input', $control);
                self.$checkAccountNumberInput = $('.js-check-account-number-input', $control);
                self.$checkRoutingNumberInput = $('.js-check-routing-number-input', $control);
                self.$checkFullNameInput = $('.js-check-fullname-input', $control);

                debugger

                self.collectJSSettings = {
                    fields: {
                        ccnumber: {
                            selector: '#' + controlId + ' .js-credit-card-input',
                            title: "Card Number",
                            placeholder: "0000 0000 0000 0000"
                        },
                        ccexp: {
                            selector: '#' + controlId + ' .js-credit-card-exp-input',
                            title: "Card Expiration",
                            placeholder: "00/00"
                        },
                        cvv: {
                            display: "show",
                            selector: '#' + controlId + ' .js-credit-card-cvv-input',
                            title: "CVV Code",
                            placeholder: "***"
                        },
                        checkaccount: {
                            selector: '#' + controlId + ' .js-check-account-number-input',
                            title: "Account Number",
                            placeholder: "0000000000"
                        },
                        checkaba: {
                            selector: '#' + controlId + ' .js-check-routing-number-input',
                            title: "Routing Number",
                            placeholder: "000000000"
                        },
                        checkname: {
                            selector: '#' + controlId + ' .js-check-fullname-input',
                            title: "Name on Checking Account",
                            placeholder: "Ted Decker"
                        }
                    },
                    variant: "inline",
                    validationCallback: function (field, status, message) {
                        debugger
                        console.log(field);
                        console.log(status);
                        console.log(message);
                    },
                    fieldsAvailableCallback: function (a, b, c) {
                        debugger
                    },
                    callback: function (resp) {
                        debugger
                        $('.js-response-token', $control).val(resp.token);
                        $('.js-tokenizer-raw-response', $control).val(JSON.stringify(resp, null, 2));

                        if (postbackScript) {
                            window.location = postbackScript;
                        }
                    }
                };

                CollectJS.configure(self.collectJSSettings);
            },

            // Tells the gatewayTokenizer to submit the entered info so that we can get a token (or error, etc) in the response
            submitPaymentInfo: function (controlId) {
                debugger
                var $control = $('#' + controlId)

                CollectJS.startPaymentRequest() // Use submission callback to deal with response
            }
        }

        return exports;
    }());
}(jQuery));


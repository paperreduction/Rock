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
                self.$paymentButton = $('.js-payment-button', $control);
                self.$paymentValidation = $('.js-payment-input-validation', $control);
                self.$paymentValidation.hide();
                self.validationFieldStatus = {
                    ccnumber: {},
                    ccexp: {},
                    cvv: {
                        status:true
                    }
                }

                var postbackScript = $control.attr('data-postback-script');

                self.$paymentButton.click(function (a) {
                    console.log("Payment button click");
                })

                self.collectJSSettings = {
                    paymentSelector: '#' + controlId + ' .js-payment-button',
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
                    timeoutDuration: 2000,
                    timeoutCallback: function () {
                        console.log("The tokenization didn't respond in the expected timeframe.  This could be due to an invalid or incomplete field or poor connectivity");
                        self.validateInputs();
                    },
                    invalidCss: {
                        "background-color": "red",
                        "color": "white"
                    },
                    validationCallback: function (field, status, message) {
                        // if there is a validation error, keep the message and field that has the error. Then we'll check it before doing the submitPaymentInfo
                        console.log(field + ':' + status + ':' + message);

                        self.validationFieldStatus[field] = {
                            status: status,
                            message: message
                        };
                    },
                    fieldsAvailableCallback: function (a, b, c) {
                        console.log("fieldsAvailableCallback");
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


            submitPaymentInfo: function (controlId) {
                var self = this
                console.log('submitPaymentInfo');
                setTimeout(function () {
                    self.startSubmitPaymentInfo(self, controlId);
                }, 0);
            },

            validateInputs: function () {

                var self = this;
                // according to https://secure.tnbcigateway.com/merchants/resources/integration/integration_portal.php?#cjs_integration_inline3, there will be things with 'CollectJSInvalid' classes if there are any validation errors
                for (var iframeKey in CollectJS.iframes) {
                    var $frameEl = $(CollectJS.iframes[iframeKey]);
                    if ($frameEl.hasClass('CollectJSInvalid') == true) {
                        // if a field has CollectJSInValid, is should be indicated with a red outline, or something similar
                        //return;
                    }
                }

                var validationMessage = '';

                var hasInvalidFields = $('.CollectJSInvalid').length > 0;

                var validationFieldTitle = ''
                for (var validationFieldKey in self.validationFieldStatus) {
                    var validationField = self.validationFieldStatus[validationFieldKey];
                    if (!validationField.status) {
                        hasInvalidFields = true;
                        validationFieldTitle = CollectJS.config.fields[validationFieldKey].title;
                        validationMessage = validationField.message || validationFieldTitle + ' cannot be blank';
                        break;
                    }
                }


                if (hasInvalidFields) {
                    var $validationMessage = self.$paymentValidation.find('.js-validation-message')
                    $validationMessage.text(validationMessage);
                    self.$paymentValidation.show();
                }
                else {
                    self.$paymentValidation.hide();
                }
            },

            // Tells the gatewayTokenizer to submit the entered info so that we can get a token (or error, etc) in the response
            // ToDo, we might want to do wait to make sure validation events are fired
            startSubmitPaymentInfo: function (self, controlId) {

                console.log('startSubmitPaymentInfo');
                debugger

                CollectJS.startPaymentRequest();
                return;


                
            }
        }

        return exports;
    }());
}(jQuery));


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

                self.$paymentInputs = $('.js-nmi-payment-inputs', $control);

                self.$paymentButton = $('.js-payment-button', $control);
                self.$paymentValidation = $('.js-payment-input-validation', $control);
                self.$paymentValidation.hide();
                self.validationFieldStatus = {
                    ccnumber: {},
                    ccexp: {},
                    cvv: {
                        status: true
                    }
                }

                var postbackScript = $control.attr('data-postback-script');
                var enabledPaymentTypes = JSON.parse($('.js-enabled-payment-types', $control).val());

                var $creditCardContainer = $('.js-gateway-creditcard-container', $control);
                var $achContainer = $('.js-gateway-ach-container', $control);

                self.$paymentButton.click(function (a) {
                    console.log("Payment button click");
                })

                var inputStyles = function (style) {
                    return $('.js-input-style-hook').css(style)
                };

                var inputPlaceholderStyles = function (style) {
                    return $('.js-input-style-hook[placeholder]').css(style)
                };

                self.collectJSSettings = {
                    paymentSelector: '#' + controlId + ' .js-payment-button',
                    variant: "inline",

                    /* Placeholder Options*/
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

                    styleSniffer: false, // we probably want to disable this. A fake input is created and gatewayCollect will steal css from that (and will disables all the css options)

                    /* Available CSS options. It can only be 1 level deep */
                    /* Only a limited number of styles are supported. see https://secure.tnbcigateway.com/merchants/resources/integration/integration_portal.php?#cjs_integration_inline2 */
                    customCss: {
                        // applied to all input fields
                        'color': inputStyles('color'),
                        'border-bottom-color': inputStyles('border-bottom-color'),
                        'border-bottom-left-radius': inputStyles('border-bottom-left-radius'),
                        'border-bottom-right-radius': inputStyles('border-bottom-right-radius'),
                        'border-bottom-style': inputStyles('border-bottom-style'),
                        'border-bottom-width': inputStyles('border-bottom-width'),
                        'border-left-color': inputStyles('border-left-color'),
                        'border-left-style': inputStyles('border-left-style'),
                        'border-left-width': inputStyles('border-left-width'),
                        'border-right-color': inputStyles('border-right-color'),
                        'border-right-style': inputStyles('border-right-style'),
                        'border-right-width': inputStyles('border-right-width'),
                        'border-top-color': inputStyles('border-top-color'),
                        'border-top-left-radius': inputStyles('border-top-left-radius'),
                        'border-top-right-radius': inputStyles('border-top-right-radius'),
                        'border-top-style': inputStyles('border-top-style'),
                        'border-top-width': inputStyles('border-top-width'),
                        'border-width': inputStyles('border-width'),
                        'border-style': inputStyles('border-style'),
                        'border-radius': inputStyles('border-radius'),
                        'border-color': inputStyles('border-color'),
                        'margin-top': inputStyles('margin-top'),
                        'margin-bottom': inputStyles('margin-bottom'),
                        'background-color': inputStyles('background-color'),
                        'box-shadow': inputStyles('box-shadow'),
                        'padding': inputStyles('padding'),
                        'font-size': inputStyles('font-size'),
                        'height': inputStyles('height'),
                        'font-family': inputStyles('font-family'),
                    },
                    focusCss: {
                        'border': getComputedStyle(document.documentElement).getPropertyValue('--focus-state-border'),
                        'box-shadow': getComputedStyle(document.documentElement).getPropertyValue('--focus-state-shadow')
                    },
                    invalidCss: {
                        "border-color": "red",
                        //"color": "white"
                    },
                    validCss: {
                        "border-color": "green",
                    },
                    placeholderCss: {
                        'color': inputPlaceholderStyles('color'),
                    },

                    /* Callback options*/
                    timeoutDuration: 4000,
                    timeoutCallback: function () {
                        debugger
                        console.log("The tokenization didn't respond in the expected timeframe.  This could be due to an invalid or incomplete field or poor connectivity");
                        self.validateInputs();
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

                        // undo temporarily moving the inputs off screen to avoid seeing input fields getting created by CollectJS
                        self.$paymentInputs.css({
                            left: '',
                            top: '',
                            position: '',
                        });

                        // show default payment type
                        if (enabledPaymentTypes.includes('card')) {
                            $creditCardContainer.show();
                            $achContainer.hide();
                        }
                        else {
                            $creditCardContainer.hide();
                            $achContainer.show();
                        }
                    },
                    callback: function (resp) {
                        console.log(resp);
                        debugger
                        $('.js-response-token', $control).val(resp.token);
                        $('.js-tokenizer-raw-response', $control).val(JSON.stringify(resp, null, 2));

                        if (postbackScript) {
                            window.location = postbackScript;
                        }
                    }
                };

                // temporarily move the inputs off screen to avoid seeing input fields getting created by CollectJS
                // We can't do any hide/show on the input elements until all the fields are created, otherwise CollectJS will squish/overlap all the input fields.
                self.$paymentInputs.css({
                    left: '-9999px',
                    top: '0px',
                    position: 'absolute',
                });

                CollectJS.configure(self.collectJSSettings);

                /* Payment Selector Stuff*/
                //// Credit Card
                if (enabledPaymentTypes.includes('card')) {
                    var $paymentButtonCreditCard = $control.find('.js-payment-creditcard');

                    $paymentButtonCreditCard.off().on('click', function () {
                        $(this).addClass("active").siblings().removeClass("active");
                        $creditCardContainer.show();
                        $achContainer.hide();
                    });
                };

                //// ACH
                if (enabledPaymentTypes.includes('ach')) {
                    var $paymentButtonACH = $control.find('.js-payment-ach');
                    $paymentButtonACH.off().on('click', function () {
                        $(this).addClass("active").siblings().removeClass("active");
                        $creditCardContainer.hide();
                        $achContainer.show();
                    });
                };

                if (enabledPaymentTypes.includes('card') == false) {
                    $creditCardContainer.remove();
                }

                if (enabledPaymentTypes.includes('ach') == false) {
                    $achContainer.remove();
                }

                var $paymentTypeSelector = $control.find('.js-gateway-paymenttype-selector');
                if (enabledPaymentTypes.length > 1) {

                    $paymentTypeSelector.show();
                }
                else {
                    $paymentTypeSelector.hide();
                }
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


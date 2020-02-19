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
                    },
                    checkaccount: {},
                    checkaba: {},
                    checkname: {}
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
                    // There is a paymentType option, but it only affects which fields get created when in Lightbox mode, so it doesn't help us since we are in inline mode
                    // paymentType: ck,cc...

                    // we are using inline mode (lightbox mode pops up a dialog to prompt for payment info)
                    variant: "inline",

                    /* field configuration */
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
                    },
                    validCss: {
                        // if we want to indicate which fields have passed validation (by CollectJS)...
                        //"border-color": "green",
                    },
                    placeholderCss: {
                        'color': inputPlaceholderStyles('color'),
                    },

                    /* Callback options*/

                    // CollectJS uses timeoutDuration and timeoutCallback to handle either connectivity issues, or invalid input
                    // In other words, if input is invalid, CollectJS just times out (it doesn't tell us)
                    timeoutDuration: 4000,
                    timeoutCallback: function () {

                        // a timeout callback will fire due to a timeout or incomplete input fields (CollectJS doesn't tell us why)
                        console.log("The tokenization didn't respond in the expected timeframe. This could be due to an invalid or incomplete field or poor connectivity");

                        // Since we don't know exactly what happened, lets see if it might be invalid inputs by checking them all manually
                        var inputsAreValid = self.validateInputs();
                        if (inputsAreValid) {

                            // inputs seem to be valid, so show a message to let them know what seems to be happeninng
                            console.log("Timeout happened for unknown reason, probably poor connectivity since we already validated inputs.");
                            debugger
                            var $validationMessage = self.$paymentValidation.find('.js-validation-message')
                            $validationMessage.text('Response from gateway timed out. This could be do to poor connectivity or invalid payment values.');
                            self.$paymentValidation.show();

                        }
                    },

                    // Collect JS will validate inputs when blurring out of fields (and it might take a second for it to fire this callback)
                    validationCallback: function (field, status, message) {
                        // if there is a validation error, keep the message and field that has the error. Then we'll check it before doing the submitPaymentInfo
                        console.log(field + ':' + status + ':' + message);

                        self.validationFieldStatus[field] = {
                            field: field,
                            status: status,
                            message: message
                        };
                    },

                    // After we call CollectJS.configure, we have to wait for CollectJS to create all the iframes for the input fields.
                    // Note that it adds the inputs to the DOM one at a time, and then fires this callback when all of them have been created.
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

                    // this is the callback when the token response comes back. This callback will only happen if all the inputs are valid. To deal with an invalid input response, we have to use timeoutDuraction, timeoutCallback to find that out.
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

                        // have CollectJS clear all the input fields when the PaymentType (ach vs cc) changes. This will prevent us sending both ACH and CC payment info at the same time
                        // CollectJS determines to use ACH vs CC by seeing which inputs have data in it. There isn't a explicit option to indicate which to use.
                        CollectJS.clearInputs();

                        $creditCardContainer.show();
                        $achContainer.hide();
                    });
                };

                //// ACH
                if (enabledPaymentTypes.includes('ach')) {
                    var $paymentButtonACH = $control.find('.js-payment-ach');
                    $paymentButtonACH.off().on('click', function () {
                        $(this).addClass("active").siblings().removeClass("active");

                        // have CollectJS clear all the input fields when the PaymentType (ach vs cc) changes. This will prevent us sending both ACH and CC payment info at the same time
                        // CollectJS determines to use ACH vs CC by seeing which inputs have data in it. There isn't a explicit option to indicate which to use.
                        CollectJS.clearInputs();

                        $creditCardContainer.hide();
                        $achContainer.show();
                    });
                };

                if ((enabledPaymentTypes.includes('card') == false) && $creditCardContainer) {
                    // if the $creditCardContainer was created, but CreditCard isn't enabled, remove it from the DOM
                    $creditCardContainer.remove();
                }

                if ((enabledPaymentTypes.includes('ach') == false) && $achContainer) {
                    // if the $achContainer was created, but ACH isn't enabled, remove it from the DOM
                    $achContainer.remove();
                }

                var $paymentTypeSelector = $control.find('.js-gateway-paymenttype-selector');
                if ($paymentTypeSelector) {
                    if (enabledPaymentTypes.length > 1) {

                        // only show the payment type selector (tabs) if there if both ACH and CC are enabled.
                        $paymentTypeSelector.show();
                    }
                    else {
                        $paymentTypeSelector.hide();
                    }
                }
            },


            // NMIHostedPaymentControl will call this when the 'Next' button is clicked
            // use javascript setTimeout to giving validation a little bit of time to check stuff. If that doesn't stop invalid input, CollectJS will do the 'timeoutCallback' event of CollectJS if it finds invalid input after submitting the payment info.
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
                        // NOTE: we might not need this
                    }
                }

                var validationMessage = '';

                // check for both .CollectJSInvalid and also if the fields weren't validated (they are probably blank)
                var hasInvalidFields = $('.CollectJSInvalid').length > 0;

                for (var validationFieldKey in self.validationFieldStatus) {
                    debugger
                    //if (CollectJS)
                    var validationField = self.validationFieldStatus[validationFieldKey];
                    // first check visibility. If this is an ACH field, but we are in CC mode (and vice versa), don't validate
                    var fieldVisible = $(CollectJS.config.fields[validationFieldKey].selector).is(':visible');
                    if (fieldVisible && !validationField.status) {
                        hasInvalidFields = true;
                        var validationFieldTitle = CollectJS.config.fields[validationFieldKey].title;
                        var isBlank = !validationField.message || validationField.message == 'Field is empty'
                        if (isBlank) {
                            validationMessage = validationFieldTitle + ' cannot be blank';
                        }
                        else {
                            validationMessage = validationField.message || 'unknown validation error';
                        }

                        break;
                    }
                }


                if (hasInvalidFields) {
                    var $validationMessage = self.$paymentValidation.find('.js-validation-message')
                    $validationMessage.text(validationMessage);
                    self.$paymentValidation.show();
                    return false;
                }
                else {
                    self.$paymentValidation.hide();
                    return true;
                }
            },

            // Tells the gatewayTokenizer to submit the entered info so that we can get a token (or error, etc) in the response
            startSubmitPaymentInfo: function (self, controlId) {
                CollectJS.startPaymentRequest();
            }
        }

        return exports;
    }());
}(jQuery));


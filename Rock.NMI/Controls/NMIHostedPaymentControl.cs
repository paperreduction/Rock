// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using Rock.Web.UI;
using Rock.NMI;
using Rock.Web.UI.Controls;
using System.Collections.Generic;

namespace Rock.NMI.Controls
{
    /// <summary>
    /// Control for hosting the NMI Gateway Payment Info HTML and scripts
    /// </summary>
    /// <seealso cref="System.Web.UI.WebControls.CompositeControl" />
    /// <seealso cref="System.Web.UI.INamingContainer" />
    public class NMIHostedPaymentControl : CompositeControl, INamingContainer, Rock.Financial.IHostedGatewayPaymentControlTokenEvent
    {
        #region Controls

        private HiddenFieldWithClass _hfPaymentInfoToken;
        private HiddenFieldWithClass _hfCollectJSRawResponse;
        private HiddenFieldWithClass _hfEnabledPaymentTypesJSON;
        private HiddenFieldWithClass _hfSelectedPaymentType;

        private TextBox _tbCreditCardNumber;
        private TextBox _tbCreditCardExp;
        private TextBox _tbCreditCardCVV;
        private TextBox _tbCheckAccountNumber;
        private TextBox _tbCheckRoutingNumber;
        private TextBox _tbCheckFullName;


        #endregion

        private Rock.NMI.Gateway _nmiGateway;

        #region Rock.Financial.IHostedGatewayPaymentControlTokenEvent

        /// <summary>
        /// Occurs when a payment token is received from the hosted gateway
        /// </summary>
        public event EventHandler<Rock.Financial.HostedGatewayPaymentControlTokenEventArgs> TokenReceived;

        #endregion Rock.Financial.IHostedGatewayPaymentControlTokenEvent

        /// <summary>
        /// Gets or sets the enabled payment types.
        /// </summary>
        /// <value>
        /// The enabled payment types.
        /// </value>
        public NMIPaymentType[] EnabledPaymentTypes
        {
            set
            {
                EnsureChildControls();
                _hfEnabledPaymentTypesJSON.Value = value.Select( a => a.ConvertToString() ).ToJson();
            }
        }

        /// <summary>
        /// Gets or sets the tokenization key.
        /// </summary>
        /// <value>
        /// The tokenization key.
        /// </value>
        public string TokenizationKey { private get; set; }

        /// <summary>
        /// Gets or sets the nmi gateway.
        /// </summary>
        /// <value>
        /// The nmi gateway.
        /// </value>
        public Gateway NMIGateway
        {
            private get
            {
                return _nmiGateway;
            }

            set
            {
                _nmiGateway = value;
            }
        }

        /// <summary>
        /// Gets the payment information token.
        /// </summary>
        /// <value>
        /// The payment information token.
        /// </value>
        public string PaymentInfoToken
        {
            get
            {
                EnsureChildControls();
                return _hfPaymentInfoToken.Value;
            }
        }

        /// <summary>
        /// Gets the payment information token raw.
        /// </summary>
        /// <value>
        /// The payment information token raw.
        /// </value>
        public string PaymentInfoTokenRaw
        {
            get
            {
                EnsureChildControls();
                return _hfCollectJSRawResponse.Value;
            }
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            // Script that lets us use the CollectJS API (see https://secure.tnbcigateway.com/merchants/resources/integration/integration_portal.php?#cjs_methodology)
            var additionalAttributes = new Dictionary<string, string>();
            additionalAttributes.Add( "data-tokenization-key", this.TokenizationKey );
            RockPage.AddScriptSrcToHead( this.Page, "nmiCollectJS", $"https://secure.tnbcigateway.com/token/Collect.js", additionalAttributes );

            // Script that contains the initializeTokenizer scripts for us to use on the client
            ScriptManager.RegisterClientScriptBlock( this, this.GetType(), "nmiGatewayCollectJSBlock", Scripts.gatewayCollectJS, true );

            ScriptManager.RegisterStartupScript( this, this.GetType(), "nmiGatewayCollectJSStartup", $"Rock.NMI.controls.gatewayCollectJS.initialize('{this.ClientID}');", true );

            base.OnInit( e );
        }

        /// <summary>
        /// Writes the <see cref="T:System.Web.UI.WebControls.CompositeControl" /> content to the specified <see cref="T:System.Web.UI.HtmlTextWriter" /> object, for display on the client.
        /// </summary>
        /// <param name="writer">An <see cref="T:System.Web.UI.HtmlTextWriter" /> that represents the output stream to render HTML content on the client.</param>
        protected override void Render( HtmlTextWriter writer )
        {
            if ( TokenReceived != null )
            {
                var updatePanel = this.ParentUpdatePanel();
                string postbackControlId;
                if ( updatePanel != null )
                {
                    postbackControlId = updatePanel.ClientID;
                }
                else
                {
                    postbackControlId = this.ID;
                }

                this.Attributes["data-postback-script"] = $"javascript:__doPostBack('{postbackControlId}', '{this.ID}=TokenizerPostback')";
            }

            base.Render( writer );
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( this.Page.IsPostBack )
            {
                string[] eventArgs = ( this.Page.Request.Form["__EVENTARGUMENT"] ?? string.Empty ).Split( new[] { "=" }, StringSplitOptions.RemoveEmptyEntries );

                if ( eventArgs.Length >= 2 )
                {
                    // gatewayTokenizer will pass back '{this.ID}=TokenizerPostback' in a postback. If so, we know this is a postback from that
                    if ( eventArgs[0] == this.ID && eventArgs[1] == "TokenizerPostback" )
                    {
                        Rock.Financial.HostedGatewayPaymentControlTokenEventArgs hostedGatewayPaymentControlTokenEventArgs = new Financial.HostedGatewayPaymentControlTokenEventArgs();

                        /*var tokenResponse = PaymentInfoTokenRaw.FromJsonOrNull<TokenizerResponse>();

                        if ( tokenResponse?.IsSuccessStatus() != true )
                        {
                            hostedGatewayPaymentControlTokenEventArgs.IsValid = false;

                            if ( tokenResponse.HasValidationError() )
                            {
                                hostedGatewayPaymentControlTokenEventArgs.ErrorMessage = tokenResponse.ValidationMessage;
                            }
                            else
                            {
                                hostedGatewayPaymentControlTokenEventArgs.ErrorMessage = tokenResponse?.Message ?? "null response from GetHostedPaymentInfoToken";
                            }
                        }
                        else
                        {
                            hostedGatewayPaymentControlTokenEventArgs.IsValid = true;
                            hostedGatewayPaymentControlTokenEventArgs.ErrorMessage = null;
                        }*/

                        hostedGatewayPaymentControlTokenEventArgs.Token = _hfPaymentInfoToken.Value;

                        TokenReceived?.Invoke( this, hostedGatewayPaymentControlTokenEventArgs );
                    }
                }
            }
        }

        /// <summary>
        /// Called by the ASP.NET page framework to notify server controls that use composition-based implementation to create any child controls they contain in preparation for posting back or rendering.
        /// </summary>
        protected override void CreateChildControls()
        {
            Controls.Clear();
            _hfPaymentInfoToken = new HiddenFieldWithClass() { ID = "_hfPaymentInfoToken", CssClass = "js-response-token" };
            Controls.Add( _hfPaymentInfoToken );

            _hfCollectJSRawResponse = new HiddenFieldWithClass() { ID = "_hfTokenizerRawResponse", CssClass = "js-tokenizer-raw-response" };
            Controls.Add( _hfCollectJSRawResponse );

            _hfEnabledPaymentTypesJSON = new HiddenFieldWithClass() { ID = "_hfEnabledPaymentTypesJSON", CssClass = "js-enabled-payment-types" };
            Controls.Add( _hfEnabledPaymentTypesJSON );

            _hfSelectedPaymentType = new HiddenFieldWithClass() { ID = "_hfSelectedPaymentType", CssClass = "js-selected-payment-type" };
            Controls.Add( _hfSelectedPaymentType );

            var pnlPaymentInputs = new Panel { ID = "pnlPaymentInputs", CssClass= "js-nmi-payment-inputs nmi-payment-inputs" };
            _tbCreditCardNumber = new TextBox { CssClass = "js-credit-card-input credit-card-input" };
            pnlPaymentInputs.Controls.Add( _tbCreditCardNumber );
            _tbCreditCardExp = new TextBox { CssClass = "js-credit-card-exp-input credit-card-exp-input" };
            pnlPaymentInputs.Controls.Add( _tbCreditCardExp );
            _tbCreditCardCVV = new TextBox { CssClass = "js-credit-card-cvv-input credit-card-cvv-input" };
            pnlPaymentInputs.Controls.Add( _tbCreditCardCVV );
            _tbCheckAccountNumber = new TextBox { CssClass = "js-check-account-number-input check-account-number-input" };
            pnlPaymentInputs.Controls.Add( _tbCheckAccountNumber );
            _tbCheckRoutingNumber = new TextBox { CssClass = "js-check-routing-number-input check-routing-number-input" };
            pnlPaymentInputs.Controls.Add( _tbCheckRoutingNumber );
            _tbCheckFullName = new TextBox { CssClass = "js-check-fullname-input check-fullname-input" };
            pnlPaymentInputs.Controls.Add( _tbCheckFullName );

            Controls.Add( pnlPaymentInputs );
        }
    }
}

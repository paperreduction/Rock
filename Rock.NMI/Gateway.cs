﻿// <copyright>
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
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.UI;
using System.Xml.Linq;

using RestSharp;

using Rock.Attribute;
using Rock.Data;
using Rock.Financial;
using Rock.Model;
using Rock.NMI.Controls;
using Rock.Security;
using Rock.Web.Cache;

namespace Rock.NMI
{
    /// <summary>
    /// NMI Payment Gateway
    /// </summary>
    [Description( "NMI Gateway" )]
    [Export( typeof( GatewayComponent ) )]
    [ExportMetadata( "ComponentName", "NMI Gateway" )]

    [TextField(
        "Security Key",
        Key = AttributeKey.SecurityKey,
        Description = "The API key",
        IsRequired = true,
        Order = 0
        )]

    [TextField(
        "Admin Username",
        Key = AttributeKey.AdminUsername,
        Description = "The username of an NMI user",
        IsRequired = true,
        Order = 1 )]

    [TextField( "Admin Password",
        Key = AttributeKey.AdminPassword,
        Description = "The password of the NMI user",
        IsRequired = true,
        IsPassword = true,
        Order = 2 )]

    [TextField(
        "Three Step API URL",
        Key = AttributeKey.ThreeStepAPIURL,
        Description = "The URL of the NMI Three Step API",
        IsRequired = true,
        DefaultValue = "https://secure.networkmerchants.com/api/v2/three-step",
        Order = 3 )]

    [TextField(
        "Query API URL",
        Key = AttributeKey.QueryUrl,
        Description = "The URL of the NMI Query API",
        IsRequired = true,
        DefaultValue = "https://secure.networkmerchants.com/api/query.php",
        Order = 4 )]

    [TextField( "Direct Post API URL",
        Key = AttributeKey.DirectPostAPIUrl,
        Description = "The URL of the NMI Direct Post Query API",
        IsRequired = true,
        DefaultValue = "https://secure.nmi.com/api/transact.php",
        Order = 5 )]

    [TextField( "Tokenization Key",
        Key = AttributeKey.TokenizationKey,
        Description = "The Public Security Key to use for Tokenization. This is required for when using the NMI Hosted Gateway.",
        IsRequired = false,
        DefaultValue = "",
        Order = 6 )]

    [EnumsField( "Hosted Gateway Modes",
        Key = AttributeKey.HostedGatewayModes,
        Description = "The hosted gateway modes that should be supported",
        EnumSourceType = typeof( HostedGatewayMode ),
        DefaultValue = "0",
        Order = 7 )]

    [BooleanField(
        "Prompt for Name On Card",
        Key = AttributeKey.PromptForName,
        Description = "Should users be prompted to enter name on the card",
        DefaultBooleanValue = false,
        Order = 8 )]

    [BooleanField(
        "Prompt for Billing Address",
        Key = AttributeKey.PromptForAddress,
        Description = "Should users be prompted to enter billing address",
        DefaultBooleanValue = false,
        Order = 9 )]


    public class Gateway : GatewayComponent, IThreeStepGatewayComponent, IHostedGatewayComponent
    {
        #region Attribute Keys

        /// <summary>;
        /// Keys to use for Component Attributes
        /// </summary>
        protected static class AttributeKey
        {
            public const string SecurityKey = "SecurityKey";
            public const string AdminUsername = "AdminUsername";
            public const string AdminPassword = "AdminPassword";

            // NOTE: Lets call this ThreeStepAPIUrl but keep "APIUrl" for backwards compatibility 
            public const string ThreeStepAPIURL = "APIUrl";

            public const string QueryUrl = "QueryUrl";
            public const string DirectPostAPIUrl = "DirectPostAPIUrl";
            public const string TokenizationKey = "TokenizationKey";
            public const string HostedGatewayModes = "EnabledHostedGatewayModes";
            public const string PromptForName = "PromptForName";
            public const string PromptForAddress = "PromptForAddress";
        }

        #endregion Attribute Keys

        #region Gateway Component Implementation

        /// <summary>
        /// Gets the step2 form URL.
        /// </summary>
        /// <value>
        /// The step2 form URL.
        /// </value>
        public string Step2FormUrl
        {
            get
            {
                return string.Format( "~/NMIGatewayStep2.html?timestamp={0}", RockDateTime.Now.Ticks );
            }
        }

        /// <summary>
        /// Gets a value indicating whether the gateway requires the name on card for CC processing
        /// </summary>
        /// <value>
        /// <c>true</c> if [name on card required]; otherwise, <c>false</c>.
        /// </value>
        public override bool PromptForNameOnCard( FinancialGateway financialGateway )
        {
            return GetAttributeValue( financialGateway, AttributeKey.PromptForName ).AsBoolean();
        }

        /// <summary>
        /// Gets a value indicating whether gateway provider needs first and last name on credit card as two distinct fields.
        /// </summary>
        /// <value>
        /// <c>true</c> if [split name on card]; otherwise, <c>false</c>.
        /// </value>
        public override bool SplitNameOnCard
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Prompts for the person name associated with a bank account.
        /// </summary>
        /// <param name="financialGateway">The financial gateway.</param>
        /// <returns></returns>
        public override bool PromptForBankAccountName( FinancialGateway financialGateway )
        {
            return true;
        }

        /// <summary>
        /// Gets a value indicating whether [address required].
        /// </summary>
        /// <value>
        /// <c>true</c> if [address required]; otherwise, <c>false</c>.
        /// </value>
        public override bool PromptForBillingAddress( FinancialGateway financialGateway )
        {
            return GetAttributeValue( financialGateway, AttributeKey.PromptForAddress ).AsBoolean();
        }

        /// <summary>
        /// Gets the supported payment schedules.
        /// </summary>
        /// <value>
        /// The supported payment schedules.
        /// </value>
        public override List<DefinedValueCache> SupportedPaymentSchedules
        {
            get
            {
                var values = new List<DefinedValueCache>();
                values.Add( DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.TRANSACTION_FREQUENCY_ONE_TIME ) );
                values.Add( DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.TRANSACTION_FREQUENCY_WEEKLY ) );
                values.Add( DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.TRANSACTION_FREQUENCY_BIWEEKLY ) );
                values.Add( DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.TRANSACTION_FREQUENCY_MONTHLY ) );
                return values;
            }
        }

        /// <summary>
        /// Returns a boolean value indicating if 'Saved Account' functionality is supported for frequency (i.e. one-time vs repeating )
        /// </summary>
        /// <param name="isRepeating">if set to <c>true</c> [is repeating].</param>
        /// <returns></returns>
        public override bool SupportsSavedAccount( bool isRepeating )
        {
            return !isRepeating;
        }

        /// <summary>
        /// Gets the financial transaction parameters that are passed to step 1
        /// </summary>
        /// <param name="redirectUrl">The redirect URL.</param>
        /// <returns></returns>
        public Dictionary<string, string> GetStep1Parameters( string redirectUrl )
        {
            var parameters = new Dictionary<string, string>();
            parameters.Add( "redirect-url", redirectUrl );
            return parameters;
        }

        /// <summary>
        /// Charges the specified payment info.
        /// </summary>
        /// <param name="financialGateway">The financial gateway.</param>
        /// <param name="paymentInfo">The payment info.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns></returns>
        public override FinancialTransaction Charge( FinancialGateway financialGateway, PaymentInfo paymentInfo, out string errorMessage )
        {
            errorMessage = "The Payment Gateway only supports a three-step charge.";
            return null;
        }

        /// <summary>
        /// Performs the first step of a three-step charge
        /// </summary>
        /// <param name="financialGateway">The financial gateway.</param>
        /// <param name="paymentInfo">The payment information.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>
        /// Url to post the Step2 request to
        /// </returns>
        /// <exception cref="ArgumentNullException">paymentInfo</exception>
        public string ChargeStep1( FinancialGateway financialGateway, PaymentInfo paymentInfo, out string errorMessage )
        {
            errorMessage = string.Empty;

            try
            {
                if ( paymentInfo == null )
                {
                    throw new ArgumentNullException( "paymentInfo" );
                }

                var rootElement = GetRoot( financialGateway, "sale" );

                rootElement.Add(
                    new XElement( "ip-address", paymentInfo.IPAddress ),
                    new XElement( "currency", "USD" ),
                    new XElement( "amount", paymentInfo.Amount.ToString() ),
                    new XElement( "order-description", paymentInfo.Description ),
                    new XElement( "tax-amount", "0.00" ),
                    new XElement( "shipping-amount", "0.00" ) );

                bool isReferencePayment = paymentInfo is ReferencePaymentInfo;
                if ( isReferencePayment )
                {
                    var reference = paymentInfo as ReferencePaymentInfo;
                    rootElement.Add( new XElement( "customer-vault-id", reference.ReferenceNumber ) );
                }

                if ( paymentInfo.AdditionalParameters != null )
                {
                    if ( !isReferencePayment )
                    {
                        rootElement.Add( new XElement( "add-customer" ) );
                    }

                    foreach ( var keyValue in paymentInfo.AdditionalParameters )
                    {
                        XElement xElement = new XElement( keyValue.Key, keyValue.Value );
                        rootElement.Add( xElement );
                    }
                }

                rootElement.Add( GetBilling( paymentInfo ) );

                XDocument xdoc = new XDocument( new XDeclaration( "1.0", "UTF-8", "yes" ), rootElement );
                var result = PostToGatewayThreeStepAPI( financialGateway, xdoc );

                if ( result == null )
                {
                    errorMessage = "Invalid Response from NMI!";
                    return null;
                }

                if ( result.GetValueOrNull( "result" ) != "1" )
                {
                    errorMessage = result.GetValueOrNull( "result-text" );
                    return null;
                }

                return result.GetValueOrNull( "form-url" );
            }
            catch ( WebException webException )
            {
                string message = GetResponseMessage( webException.Response.GetResponseStream() );
                errorMessage = webException.Message + " - " + message;
                return null;
            }
            catch ( Exception ex )
            {
                errorMessage = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Performs the final step of a three-step charge.
        /// </summary>
        /// <param name="financialGateway">The financial gateway.</param>
        /// <param name="resultQueryString">The result query string from step 2.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns></returns>
        public FinancialTransaction ChargeStep3( FinancialGateway financialGateway, string resultQueryString, out string errorMessage )
        {
            errorMessage = string.Empty;

            try
            {
                var rootElement = GetRoot( financialGateway, "complete-action" );
                rootElement.Add( new XElement( "token-id", resultQueryString.Substring( 10 ) ) );
                XDocument xdoc = new XDocument( new XDeclaration( "1.0", "UTF-8", "yes" ), rootElement );
                var result = PostToGatewayThreeStepAPI( financialGateway, xdoc );

                if ( result == null )
                {
                    errorMessage = "Invalid Response from NMI!";
                    return null;
                }

                if ( result.GetValueOrNull( "result" ) != "1" )
                {
                    errorMessage = result.GetValueOrNull( "result-text" );

                    string resultCodeMessage = GetResultCodeMessage( result );
                    if ( resultCodeMessage.IsNotNullOrWhiteSpace() )
                    {
                        errorMessage += string.Format( " ({0})", resultCodeMessage );
                    }

                    // write result error as an exception
                    ExceptionLogService.LogException( new Exception( $"Error processing NMI transaction. Result Code:  {result.GetValueOrNull( "result-code" )} ({resultCodeMessage}). Result text: {result.GetValueOrNull( "result-text" )}. Card Holder Name: {result.GetValueOrNull( "first-name" )} {result.GetValueOrNull( "last-name" )}. Amount: {result.GetValueOrNull( "total-amount" )}. Transaction id: {result.GetValueOrNull( "transaction-id" )}. Descriptor: {result.GetValueOrNull( "descriptor" )}. Order description: {result.GetValueOrNull( "order-description" )}." ) );

                    return null;
                }

                var transaction = new FinancialTransaction();
                transaction.TransactionCode = result.GetValueOrNull( "transaction-id" );
                transaction.ForeignKey = result.GetValueOrNull( "customer-vault-id" );
                transaction.FinancialPaymentDetail = new FinancialPaymentDetail();

                string ccNumber = result.GetValueOrNull( "billing_cc-number" );
                if ( !string.IsNullOrWhiteSpace( ccNumber ) )
                {
                    // cc payment
                    var curType = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CREDIT_CARD );
                    transaction.FinancialPaymentDetail.NameOnCardEncrypted = Encryption.EncryptString( $"{result.GetValueOrNull( "billing_first-name" )} {result.GetValueOrNull( "billing_last-name" )}" );
                    transaction.FinancialPaymentDetail.CurrencyTypeValueId = curType != null ? curType.Id : ( int? ) null;
                    transaction.FinancialPaymentDetail.CreditCardTypeValueId = CreditCardPaymentInfo.GetCreditCardType( ccNumber.Replace( '*', '1' ).AsNumeric() )?.Id;
                    transaction.FinancialPaymentDetail.AccountNumberMasked = ccNumber.Masked( true );

                    string mmyy = result.GetValueOrNull( "billing_cc-exp" );
                    if ( !string.IsNullOrWhiteSpace( mmyy ) && mmyy.Length == 4 )
                    {
                        transaction.FinancialPaymentDetail.ExpirationMonthEncrypted = Encryption.EncryptString( mmyy.Substring( 0, 2 ) );
                        transaction.FinancialPaymentDetail.ExpirationYearEncrypted = Encryption.EncryptString( mmyy.Substring( 2, 2 ) );
                    }
                }
                else
                {
                    // ach payment
                    var curType = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_ACH );
                    transaction.FinancialPaymentDetail.CurrencyTypeValueId = curType != null ? curType.Id : ( int? ) null;
                    transaction.FinancialPaymentDetail.AccountNumberMasked = result.GetValueOrNull( "billing_account-number" ).Masked( true );
                }

                transaction.AdditionalLavaFields = new Dictionary<string, object>();
                foreach ( var keyVal in result )
                {
                    transaction.AdditionalLavaFields.Add( keyVal.Key, keyVal.Value );
                }

                return transaction;
            }
            catch ( WebException webException )
            {
                string message = GetResponseMessage( webException.Response.GetResponseStream() );
                errorMessage = webException.Message + " - " + message;
                return null;
            }
            catch ( Exception ex )
            {
                errorMessage = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Credits (Refunds) the specified transaction.
        /// </summary>
        /// <param name="origTransaction">The original transaction.</param>
        /// <param name="amount">The amount.</param>
        /// <param name="comment">The comment.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns></returns>
        public override FinancialTransaction Credit( FinancialTransaction origTransaction, decimal amount, string comment, out string errorMessage )
        {
            errorMessage = string.Empty;

            if ( origTransaction != null &&
                !string.IsNullOrWhiteSpace( origTransaction.TransactionCode ) &&
                origTransaction.FinancialGateway != null )
            {
                var rootElement = GetRoot( origTransaction.FinancialGateway, "refund" );
                rootElement.Add( new XElement( "transaction-id", origTransaction.TransactionCode ) );
                rootElement.Add( new XElement( "amount", amount.ToString( "0.00" ) ) );

                XDocument xdoc = new XDocument( new XDeclaration( "1.0", "UTF-8", "yes" ), rootElement );
                var result = PostToGatewayThreeStepAPI( origTransaction.FinancialGateway, xdoc );

                if ( result == null )
                {
                    errorMessage = "Invalid Response from NMI!";
                    return null;
                }

                if ( result.GetValueOrNull( "result" ) != "1" )
                {
                    errorMessage = result.GetValueOrNull( "result-text" );
                    return null;
                }

                var transaction = new FinancialTransaction();
                transaction.TransactionCode = result.GetValueOrNull( "transaction-id" );
                return transaction;
            }
            else
            {
                errorMessage = "Invalid original transaction, transaction code, or gateway.";
            }

            return null;
        }

        /// <summary>
        /// Adds the scheduled payment.
        /// </summary>
        /// <param name="financialGateway">The financial gateway.</param>
        /// <param name="schedule">The schedule.</param>
        /// <param name="paymentInfo">The payment info.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns></returns>
        public override FinancialScheduledTransaction AddScheduledPayment( FinancialGateway financialGateway, PaymentSchedule schedule, PaymentInfo paymentInfo, out string errorMessage )
        {
            errorMessage = "The Payment Gateway only supports adding scheduled payment using a three-step process.";
            return null;
        }

        /// <summary>
        /// Performs the first step of adding a new payment schedule
        /// </summary>
        /// <param name="financialGateway">The financial gateway.</param>
        /// <param name="schedule">The schedule.</param>
        /// <param name="paymentInfo">The payment information.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">paymentInfo</exception>
        public string AddScheduledPaymentStep1( FinancialGateway financialGateway, PaymentSchedule schedule, PaymentInfo paymentInfo, out string errorMessage )
        {
            errorMessage = string.Empty;

            try
            {
                if ( paymentInfo == null )
                {
                    throw new ArgumentNullException( "paymentInfo" );
                }

                var rootElement = GetRoot( financialGateway, "add-subscription" );

                rootElement.Add(
                    new XElement( "start-date", schedule.StartDate.ToString( "yyyyMMdd" ) ),
                    new XElement( "order-description", paymentInfo.Description ),
                    new XElement( "currency", "USD" ),
                    new XElement( "tax-amount", "0.00" ) );

                bool isReferencePayment = paymentInfo is ReferencePaymentInfo;
                if ( isReferencePayment )
                {
                    var reference = paymentInfo as ReferencePaymentInfo;
                    rootElement.Add( new XElement( "customer-vault-id", reference.ReferenceNumber ) );
                }

                if ( paymentInfo.AdditionalParameters != null )
                {
                    foreach ( var keyValue in paymentInfo.AdditionalParameters )
                    {
                        XElement xElement = new XElement( keyValue.Key, keyValue.Value );
                        rootElement.Add( xElement );
                    }
                }

                rootElement.Add( GetPlan( schedule, paymentInfo ) );

                rootElement.Add( GetBilling( paymentInfo ) );

                XDocument xdoc = new XDocument( new XDeclaration( "1.0", "UTF-8", "yes" ), rootElement );
                var result = PostToGatewayThreeStepAPI( financialGateway, xdoc );

                if ( result == null )
                {
                    errorMessage = "Invalid Response from NMI!";
                    return null;
                }

                if ( result.GetValueOrNull( "result" ) != "1" )
                {
                    errorMessage = result.GetValueOrNull( "result-text" );
                    return null;
                }

                return result.GetValueOrNull( "form-url" );
            }
            catch ( WebException webException )
            {
                string message = GetResponseMessage( webException.Response.GetResponseStream() );
                errorMessage = webException.Message + " - " + message;
                return null;
            }
            catch ( Exception ex )
            {
                errorMessage = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Performs the third step of adding a new payment schedule
        /// </summary>
        /// <param name="financialGateway">The financial gateway.</param>
        /// <param name="resultQueryString">The result query string from step 2.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">tokenId</exception>
        public FinancialScheduledTransaction AddScheduledPaymentStep3( FinancialGateway financialGateway, string resultQueryString, out string errorMessage )
        {
            errorMessage = string.Empty;

            try
            {
                var rootElement = GetRoot( financialGateway, "complete-action" );
                rootElement.Add( new XElement( "token-id", resultQueryString.Substring( 10 ) ) );
                XDocument xdoc = new XDocument( new XDeclaration( "1.0", "UTF-8", "yes" ), rootElement );
                var result = PostToGatewayThreeStepAPI( financialGateway, xdoc );

                if ( result == null )
                {
                    errorMessage = "Invalid Response from NMI!";
                    return null;
                }

                if ( result.GetValueOrNull( "result" ) != "1" )
                {
                    errorMessage = result.GetValueOrNull( "result-text" );
                    return null;
                }

                var scheduledTransaction = new FinancialScheduledTransaction();
                scheduledTransaction.IsActive = true;
                scheduledTransaction.GatewayScheduleId = result.GetValueOrNull( "subscription-id" );
                scheduledTransaction.FinancialGatewayId = financialGateway.Id;

                scheduledTransaction.FinancialPaymentDetail = new FinancialPaymentDetail();
                string ccNumber = result.GetValueOrNull( "billing_cc-number" );
                if ( !string.IsNullOrWhiteSpace( ccNumber ) )
                {
                    // cc payment
                    var curType = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CREDIT_CARD );
                    scheduledTransaction.FinancialPaymentDetail.CurrencyTypeValueId = curType != null ? curType.Id : ( int? ) null;
                    scheduledTransaction.FinancialPaymentDetail.CreditCardTypeValueId = CreditCardPaymentInfo.GetCreditCardType( ccNumber.Replace( '*', '1' ).AsNumeric() )?.Id;
                    scheduledTransaction.FinancialPaymentDetail.AccountNumberMasked = ccNumber.Masked( true );

                    string mmyy = result.GetValueOrNull( "billing_cc-exp" );
                    if ( !string.IsNullOrWhiteSpace( mmyy ) && mmyy.Length == 4 )
                    {
                        scheduledTransaction.FinancialPaymentDetail.ExpirationMonthEncrypted = Encryption.EncryptString( mmyy.Substring( 0, 2 ) );
                        scheduledTransaction.FinancialPaymentDetail.ExpirationYearEncrypted = Encryption.EncryptString( mmyy.Substring( 2, 2 ) );
                    }
                }
                else
                {
                    // ach payment
                    var curType = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_ACH );
                    scheduledTransaction.FinancialPaymentDetail.CurrencyTypeValueId = curType != null ? curType.Id : ( int? ) null;
                    scheduledTransaction.FinancialPaymentDetail.AccountNumberMasked = result.GetValueOrNull( "billing_account_number" ).Masked( true );
                }

                GetScheduledPaymentStatus( scheduledTransaction, out errorMessage );

                return scheduledTransaction;
            }
            catch ( WebException webException )
            {
                string message = GetResponseMessage( webException.Response.GetResponseStream() );
                errorMessage = webException.Message + " - " + message;
                return null;
            }
            catch ( Exception ex )
            {
                errorMessage = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Flag indicating if gateway supports reactivating a scheduled payment.
        /// </summary>
        /// <returns></returns>
        public override bool ReactivateScheduledPaymentSupported
        {
            get { return false; }
        }

        /// <summary>
        /// Reactivates the scheduled payment.
        /// </summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns></returns>
        public override bool ReactivateScheduledPayment( FinancialScheduledTransaction transaction, out string errorMessage )
        {
            errorMessage = "The payment gateway associated with this scheduled transaction (NMI) does not support reactivating scheduled transactions. A new scheduled transaction should be created instead.";
            return false;
        }

        /// <summary>
        /// Updates the scheduled payment supported.
        /// </summary>
        /// <returns></returns>
        public override bool UpdateScheduledPaymentSupported
        {
            get { return false; }
        }

        /// <summary>
        /// Updates the scheduled payment.
        /// </summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="paymentInfo">The payment info.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns></returns>
        public override bool UpdateScheduledPayment( FinancialScheduledTransaction transaction, PaymentInfo paymentInfo, out string errorMessage )
        {
            errorMessage = "The payment gateway associated with this scheduled transaction (NMI) does not support updating an existing scheduled transaction. A new scheduled transaction should be created instead.";
            return false;
        }

        /// <summary>
        /// Cancels the scheduled payment.
        /// </summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns></returns>
        public override bool CancelScheduledPayment( FinancialScheduledTransaction transaction, out string errorMessage )
        {
            errorMessage = string.Empty;

            if ( transaction != null &&
                !string.IsNullOrWhiteSpace( transaction.GatewayScheduleId ) &&
                transaction.FinancialGateway != null )
            {
                var rootElement = GetRoot( transaction.FinancialGateway, "delete-subscription" );
                rootElement.Add( new XElement( "subscription-id", transaction.GatewayScheduleId ) );

                XDocument xdoc = new XDocument( new XDeclaration( "1.0", "UTF-8", "yes" ), rootElement );
                var result = PostToGatewayThreeStepAPI( transaction.FinancialGateway, xdoc );

                if ( result == null )
                {
                    errorMessage = "Invalid Response from NMI!";
                    return false;
                }

                if ( result.GetValueOrNull( "result" ) != "1" )
                {
                    errorMessage = result.GetValueOrNull( "result-text" );
                    return false;
                }

                transaction.IsActive = false;
                return true;
            }
            else
            {
                errorMessage = "Invalid original transaction, transaction code, or gateway.";
            }

            return false;
        }

        /// <summary>
        /// Gets the scheduled payment status supported.
        /// </summary>
        /// <returns></returns>
        public override bool GetScheduledPaymentStatusSupported
        {
            get { return true; }
        }

        /// <summary>
        /// Gets the scheduled payment status.
        /// </summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns></returns>
        public override bool GetScheduledPaymentStatus( FinancialScheduledTransaction transaction, out string errorMessage )
        {
            errorMessage = string.Empty;
            var financialGateway = new FinancialGatewayService( new RockContext() ).Get( transaction.FinancialGatewayId.Value );

            var restClient = new RestClient( GetAttributeValue( financialGateway, AttributeKey.QueryUrl ) );
            var restRequest = new RestRequest( Method.GET );

            restRequest.AddParameter( "username", GetAttributeValue( financialGateway, AttributeKey.AdminUsername ) );
            restRequest.AddParameter( "password", GetAttributeValue( financialGateway, AttributeKey.AdminPassword ) );
            restRequest.AddParameter( "report_type", "recurring" );
            restRequest.AddParameter( "subscription_id", transaction.GatewayScheduleId );

            var response = restClient.Execute( restRequest );
            if ( response != null )
            {
                if ( response.StatusCode == HttpStatusCode.OK )
                {
                    var xdocResult = GetXmlResponse( response );
                    var subscriptionNode = xdocResult.Root.Element( "subscription" );
                    if ( subscriptionNode == null )
                    {
                        errorMessage = "unable to get subscription node";
                        return false;
                    }

                    var subscription_id = subscriptionNode.Element( "subscription_id" )?.Value;

                    if ( subscription_id == null )
                    {
                        errorMessage = "unable to determine subscription_id from response";
                        return false;
                    }
                    else if ( subscription_id != transaction.GatewayScheduleId )
                    {
                        errorMessage = "subscription_id mismatch in response";
                        return false;
                    }

                    transaction.NextPaymentDate = subscriptionNode.Element( "next_charge_date" )?.Value.AsDateTime();
                    transaction.LastStatusUpdateDateTime = RockDateTime.Now;

                    return true;
                }
            }

            errorMessage = "Unexpected response";
            return false;
        }

        /// <summary>
        /// Gets the payments that have been processed for any scheduled transactions
        /// </summary>
        /// <param name="financialGateway">The financial gateway.</param>
        /// <param name="startDate">The start date.</param>
        /// <param name="endDate">The end date.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns></returns>
        public override List<Payment> GetPayments( FinancialGateway financialGateway, DateTime startDate, DateTime endDate, out string errorMessage )
        {
            errorMessage = string.Empty;

            var txns = new List<Payment>();

            var restClient = new RestClient( GetAttributeValue( financialGateway, AttributeKey.QueryUrl ) );
            var restRequest = new RestRequest( Method.GET );

            restRequest.AddParameter( "username", GetAttributeValue( financialGateway, AttributeKey.AdminUsername ) );
            restRequest.AddParameter( "password", GetAttributeValue( financialGateway, AttributeKey.AdminPassword ) );
            restRequest.AddParameter( "start_date", startDate.ToString( "yyyyMMddHHmmss" ) );
            restRequest.AddParameter( "end_date", endDate.ToString( "yyyyMMddHHmmss" ) );

            try
            {
                var response = restClient.Execute( restRequest );
                if ( response != null )
                {
                    if ( response.StatusCode == HttpStatusCode.OK )
                    {
                        var xdocResult = GetXmlResponse( response );
                        if ( xdocResult != null )
                        {
                            var errorResponse = xdocResult.Root.Element( "error_response" );
                            if ( errorResponse != null )
                            {
                                errorMessage = errorResponse.Value;
                            }
                            else
                            {
                                foreach ( var xTxn in xdocResult.Root.Elements( "transaction" ) )
                                {
                                    Payment payment = new Payment();
                                    payment.TransactionCode = GetXElementValue( xTxn, "transaction_id" );
                                    payment.Status = GetXElementValue( xTxn, "condition" ).FixCase();
                                    payment.IsFailure =
                                        payment.Status == "Failed" ||
                                        payment.Status == "Abandoned" ||
                                        payment.Status == "Canceled";
                                    payment.TransactionCode = GetXElementValue( xTxn, "transaction_id" );
                                    payment.GatewayScheduleId = GetXElementValue( xTxn, "original_transaction_id" ).Trim();

                                    var statusMessage = new StringBuilder();
                                    DateTime? txnDateTime = null;

                                    foreach ( var xAction in xTxn.Elements( "action" ) )
                                    {
                                        DateTime? actionDate = ParseDateValue( GetXElementValue( xAction, "date" ) );
                                        string actionType = GetXElementValue( xAction, "action_type" );
                                        string responseText = GetXElementValue( xAction, "response_text" );

                                        if ( actionDate.HasValue )
                                        {
                                            statusMessage.AppendFormat(
                                                "{0} {1}: {2}; Status: {3}",
                                                actionDate.Value.ToShortDateString(),
                                                actionDate.Value.ToShortTimeString(),
                                                actionType.FixCase(),
                                                responseText );

                                            statusMessage.AppendLine();
                                        }

                                        decimal? txnAmount = GetXElementValue( xAction, "amount" ).AsDecimalOrNull();
                                        if ( txnAmount.HasValue && actionDate.HasValue )
                                        {
                                            payment.Amount = txnAmount.Value;
                                        }

                                        if ( actionType == "sale" )
                                        {
                                            txnDateTime = actionDate.Value;
                                        }

                                        if ( actionType == "settle" )
                                        {
                                            payment.IsSettled = true;
                                            payment.SettledGroupId = GetXElementValue( xAction, "processor_batch_id" ).Trim();
                                            payment.SettledDate = actionDate;
                                            txnDateTime = txnDateTime.HasValue ? txnDateTime.Value : actionDate.Value;
                                        }
                                    }

                                    if ( txnDateTime.HasValue )
                                    {
                                        payment.TransactionDateTime = txnDateTime.Value;
                                        payment.StatusMessage = statusMessage.ToString();
                                        txns.Add( payment );
                                    }
                                }
                            }
                        }
                        else
                        {
                            errorMessage = "Invalid XML Document Returned From Gateway!";
                        }
                    }
                    else
                    {
                        errorMessage = string.Format( "Invalid Response from Gateway: [{0}] {1}", response.StatusCode.ConvertToString(), response.ErrorMessage );
                    }
                }
                else
                {
                    errorMessage = "Null Response From Gateway!";
                }
            }
            catch ( WebException webException )
            {
                string message = GetResponseMessage( webException.Response.GetResponseStream() );
                throw new Exception( webException.Message + " - " + message );
            }

            return txns;
        }

        /// <summary>
        /// Gets an optional reference identifier needed to process future transaction from saved account.
        /// </summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns></returns>
        public override string GetReferenceNumber( FinancialTransaction transaction, out string errorMessage )
        {
            errorMessage = string.Empty;
            return transaction.ForeignKey;
        }

        /// <summary>
        /// Gets an optional reference identifier needed to process future transaction from saved account.
        /// </summary>
        /// <param name="scheduledTransaction">The scheduled transaction.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns></returns>
        public override string GetReferenceNumber( FinancialScheduledTransaction scheduledTransaction, out string errorMessage )
        {
            errorMessage = string.Empty;
            return string.Empty;
        }

        /// <summary>
        /// Gets the root.
        /// </summary>
        /// <param name="financialGateway">The financial gateway.</param>
        /// <param name="elementName">Name of the element.</param>
        /// <returns></returns>
        private XElement GetRoot( FinancialGateway financialGateway, string elementName )
        {
            XElement apiKeyElement = new XElement( "api-key", GetAttributeValue( financialGateway, AttributeKey.SecurityKey ) );
            XElement rootElement = new XElement( elementName, apiKeyElement );

            return rootElement;
        }

        /// <summary>
        /// Creates a billing XML element
        /// </summary>
        /// <param name="paymentInfo">The payment information.</param>
        /// <returns></returns>
        private XElement GetBilling( PaymentInfo paymentInfo )
        {
            // If giving from a Business, FirstName will be blank
            // The Gateway might require a FirstName, so just put '-' if no FirstName was provided
            if ( paymentInfo.FirstName.IsNullOrWhiteSpace() )
            {
                paymentInfo.FirstName = "-";
            }

            XElement billingElement = new XElement(
                "billing",
                new XElement( "first-name", paymentInfo.FirstName ),
                new XElement( "last-name", paymentInfo.LastName ),
                new XElement( "address1", paymentInfo.Street1 ),
                new XElement( "address2", paymentInfo.Street2 ),
                new XElement( "city", paymentInfo.City ),
                new XElement( "state", paymentInfo.State ),
                new XElement( "postal", paymentInfo.PostalCode ),
                new XElement( "country", paymentInfo.Country ),
                new XElement( "phone", paymentInfo.Phone ),
                new XElement( "email", paymentInfo.Email ) );

            if ( paymentInfo is ACHPaymentInfo )
            {
                var ach = paymentInfo as ACHPaymentInfo;
                billingElement.Add( new XElement( "account-type", ach.AccountType == BankAccountType.Savings ? "savings" : "checking" ) );
                billingElement.Add( new XElement( "entity-type", "personal" ) );
            }

            return billingElement;
        }

        /// <summary>
        /// Creates a scheduled transaction plan XML element
        /// </summary>
        /// <param name="schedule">The schedule.</param>
        /// <param name="paymentInfo">The payment information.</param>
        /// <returns></returns>
        private XElement GetPlan( PaymentSchedule schedule, PaymentInfo paymentInfo )
        {
            var selectedFrequencyGuid = schedule.TransactionFrequencyValue.Guid.ToString().ToUpper();

            if ( selectedFrequencyGuid == Rock.SystemGuid.DefinedValue.TRANSACTION_FREQUENCY_ONE_TIME )
            {
                // Make sure number of payments is set to 1 for one-time future payments
                schedule.NumberOfPayments = 1;
            }

            XElement planElement = new XElement(
                "plan",
                new XElement( "payments", schedule.NumberOfPayments.HasValue ? schedule.NumberOfPayments.Value.ToString() : "0" ),
                new XElement( "amount", paymentInfo.Amount.ToString() ) );

            switch ( selectedFrequencyGuid )
            {
                case Rock.SystemGuid.DefinedValue.TRANSACTION_FREQUENCY_ONE_TIME:
                    planElement.Add( new XElement( "month-frequency", "12" ) );
                    planElement.Add( new XElement( "day-of-month", schedule.StartDate.Day.ToString() ) );
                    break;
                case Rock.SystemGuid.DefinedValue.TRANSACTION_FREQUENCY_WEEKLY:
                    planElement.Add( new XElement( "day-frequency", "7" ) );
                    break;
                case Rock.SystemGuid.DefinedValue.TRANSACTION_FREQUENCY_BIWEEKLY:
                    planElement.Add( new XElement( "day-frequency", "14" ) );
                    break;
                case Rock.SystemGuid.DefinedValue.TRANSACTION_FREQUENCY_MONTHLY:
                    planElement.Add( new XElement( "month-frequency", "1" ) );
                    planElement.Add( new XElement( "day-of-month", schedule.StartDate.Day.ToString() ) );
                    break;
            }

            return planElement;
        }

        /// <summary>
        /// Posts to gateway using the 3-Step API.
        /// https://secure.tnbcigateway.com/merchants/resources/integration/integration_portal.php?#3step_methodology
        /// </summary>
        /// <param name="financialGateway">The financial gateway.</param>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private Dictionary<string, string> PostToGatewayThreeStepAPI( FinancialGateway financialGateway, XDocument data )
        {
            var restClient = new RestClient( GetAttributeValue( financialGateway, AttributeKey.ThreeStepAPIURL ) );
            var restRequest = new RestRequest( Method.POST );
            restRequest.RequestFormat = DataFormat.Xml;
            restRequest.AddParameter( "text/xml", data.ToString(), ParameterType.RequestBody );

            try
            {
                var response = restClient.Execute( restRequest );
                var xdocResult = GetXmlResponse( response );

                if ( xdocResult != null )
                {
                    // Convert XML result to a dictionary
                    var result = new Dictionary<string, string>();
                    foreach ( XElement element in xdocResult.Root.Elements() )
                    {
                        if ( element.HasElements )
                        {
                            string prefix = element.Name.LocalName;
                            foreach ( XElement childElement in element.Elements() )
                            {
                                result.AddOrIgnore( prefix + "_" + childElement.Name.LocalName, childElement.Value.Trim() );
                            }
                        }
                        else
                        {
                            result.AddOrIgnore( element.Name.LocalName, element.Value.Trim() );
                        }
                    }

                    return result;
                }
            }
            catch ( WebException webException )
            {
                string message = GetResponseMessage( webException.Response.GetResponseStream() );
                throw new Exception( webException.Message + " - " + message );
            }
            catch ( Exception ex )
            {
                throw ex;
            }

            return null;
        }

        /// <summary>
        /// Gets the response as an XDocument
        /// </summary>
        /// <param name="response">The response.</param>
        /// <returns></returns>
        private XDocument GetXmlResponse( IRestResponse response )
        {
            if ( response.StatusCode == HttpStatusCode.OK &&
                response.Content.Trim().Length > 0 &&
                response.Content.Contains( "<?xml" ) )
            {
                return XDocument.Parse( response.Content );
            }

            return null;
        }

        /// <summary>
        /// Gets the result code message.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns></returns>
        private string GetResultCodeMessage( Dictionary<string, string> result )
        {
            switch ( result.GetValueOrNull( "result-code" ).AsInteger() )
            {
                case 100:
                    {
                        return "Transaction was approved.";
                    }

                case 200:
                    {
                        return "Transaction was declined by processor.";
                    }

                case 201:
                    {
                        return "Do not honor.";
                    }

                case 202:
                    {
                        return "Insufficient funds.";
                    }

                case 203:
                    {
                        return "Over limit.";
                    }

                case 204:
                    {
                        return "Transaction not allowed.";
                    }

                case 220:
                    {
                        return "Incorrect payment information.";
                    }

                case 221:
                    {
                        return "No such card issuer.";
                    }

                case 222:
                    {
                        return "No card number on file with issuer.";
                    }

                case 223:
                    {
                        return "Expired card.";
                    }

                case 224:
                    {
                        return "Invalid expiration date.";
                    }

                case 225:
                    {
                        return "Invalid card security code.";
                    }

                case 240:
                    {
                        return "Call issuer for further information.";
                    }

                case 250: // pickup card
                case 251: // lost card
                case 252: // stolen card
                case 253: // fradulent card
                    {
                        // these are more sensitive declines so sanitize them a bit but provide a code for later lookup
                        return string.Format( "This card was declined (code: {0}).", result.GetValueOrNull( "result-code" ) );
                    }

                case 260:
                    {
                        return string.Format( "Declined with further instructions available. ({0})", result.GetValueOrNull( "result-text" ) );
                    }

                case 261:
                    {
                        return "Declined-Stop all recurring payments.";
                    }

                case 262:
                    {
                        return "Declined-Stop this recurring program.";
                    }

                case 263:
                    {
                        return "Declined-Update cardholder data available.";
                    }

                case 264:
                    {
                        return "Declined-Retry in a few days.";
                    }

                case 300:
                    {
                        return "Transaction was rejected by gateway.";
                    }

                case 400:
                    {
                        return "Transaction error returned by processor.";
                    }

                case 410:
                    {
                        return "Invalid merchant configuration.";
                    }

                case 411:
                    {
                        return "Merchant account is inactive.";
                    }

                case 420:
                    {
                        return "Communication error.";
                    }

                case 421:
                    {
                        return "Communication error with issuer.";
                    }

                case 430:
                    {
                        return "Duplicate transaction at processor.";
                    }

                case 440:
                    {
                        return "Processor format error.";
                    }

                case 441:
                    {
                        return "Invalid transaction information.";
                    }

                case 460:
                    {
                        return "Processor feature not available.";
                    }

                case 461:
                    {
                        return "Unsupported card type.";
                    }
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets the response message.
        /// </summary>
        /// <param name="responseStream">The response stream.</param>
        /// <returns></returns>
        private string GetResponseMessage( Stream responseStream )
        {
            Stream receiveStream = responseStream;
            Encoding encode = System.Text.Encoding.GetEncoding( "utf-8" );
            StreamReader readStream = new StreamReader( receiveStream, encode );

            StringBuilder sb = new StringBuilder();
            char[] read = new char[8192];
            int count = 0;
            do
            {
                count = readStream.Read( read, 0, 8192 );
                string str = new string( read, 0, count );
                sb.Append( str );
            }
            while ( count > 0 );

            return sb.ToString();
        }

        /// <summary>
        /// Gets the x element value.
        /// </summary>
        /// <param name="parentElement">The parent element.</param>
        /// <param name="elementName">Name of the element.</param>
        /// <returns></returns>
        private string GetXElementValue( XElement parentElement, string elementName )
        {
            var x = parentElement.Element( elementName );
            if ( x != null )
            {
                return x.Value;
            }

            return string.Empty;
        }

        /// <summary>
        /// Parses the date value.
        /// </summary>
        /// <param name="dateString">The date string.</param>
        /// <returns></returns>
        private DateTime? ParseDateValue( string dateString )
        {
            if ( !string.IsNullOrWhiteSpace( dateString ) && dateString.Length >= 14 )
            {
                int year = dateString.Substring( 0, 4 ).AsInteger();
                int month = dateString.Substring( 4, 2 ).AsInteger();
                int day = dateString.Substring( 6, 2 ).AsInteger();
                int hour = dateString.Substring( 8, 2 ).AsInteger();
                int min = dateString.Substring( 10, 2 ).AsInteger();
                int sec = dateString.Substring( 12, 2 ).AsInteger();

                return new DateTime( year, month, day, hour, min, sec );
            }

            return DateTime.MinValue;
        }

        #endregion

        #region DirectPost API related

        /// <summary>
        /// Posts to gateway using the Direct Post Api https://secure.tnbcigateway.com/merchants/resources/integration/integration_portal.php?#methodology
        /// </summary>
        /// <param name="financialGateway">The financial gateway.</param>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        private Dictionary<string, string> PostToGatewayDirectPostAPI( FinancialGateway financialGateway, Dictionary<string, string> queryParameters )
        {
            var directPostApiURL = GetAttributeValue( financialGateway, AttributeKey.DirectPostAPIUrl );
            var restClient = new RestClient( directPostApiURL );

            var restRequest = new RestRequest( Method.POST );
            restRequest.RequestFormat = DataFormat.Xml;
            foreach ( var queryParameter in queryParameters )
            {
                restRequest.AddQueryParameter( queryParameter.Key, queryParameter.Value );
            }

            restRequest.AddParameter( "username", GetAttributeValue( financialGateway, AttributeKey.AdminUsername ) );
            restRequest.AddParameter( "password", GetAttributeValue( financialGateway, AttributeKey.AdminPassword ) );

            try
            {
                var response = restClient.Execute( restRequest );

                // deal with either an XML response or QueryString style response
                var xdocResult = GetXmlResponse( response );
                if ( xdocResult != null )
                {
                    // Convert XML result to a dictionary
                    var result = new Dictionary<string, string>();
                    foreach ( XElement element in xdocResult.Root.Elements() )
                    {
                        if ( element.HasElements )
                        {
                            string prefix = element.Name.LocalName;
                            foreach ( XElement childElement in element.Elements() )
                            {
                                result.AddOrIgnore( prefix + "_" + childElement.Name.LocalName, childElement.Value.Trim() );
                            }
                        }
                        else
                        {
                            result.AddOrIgnore( element.Name.LocalName, element.Value.Trim() );
                        }
                    }

                    return result;
                }
                else
                {
                    // response in the form of response=3&responsetext=Plan Payments is required REFID:123456789&authcode=&transactionid=&avsresponse=&cvvresponse=&orderid=&type=&response_code=300&customer_vault_id=
                    // so convert this to a dictionary
                    var result = response?.Content?.Split( '&' ).ToList().Select( s => s.Split( '=' ) ).Where( a => a.Length == 2 ).ToDictionary( k => k[0], v => v[1] );

                    return result;
                }
            }
            catch ( WebException webException )
            {
                string message = GetResponseMessage( webException.Response.GetResponseStream() );
                throw new Exception( webException.Message + " - " + message );
            }
            catch ( Exception ex )
            {
                throw ex;
            }
        }

        #endregion DirectPost API related

        #region IHostedGatewayComponent

        /// <summary>
        /// Gets the URL that the Gateway Information UI will navigate to when they click the 'Configure' link
        /// </summary>
        /// <value>
        /// The configure URL.
        /// </value>
        public string ConfigureURL => "https://www.nmi.com/";

        /// <summary>
        /// Gets the URL that the Gateway Information UI will navigate to when they click the 'Learn More' link
        /// </summary>
        /// <value>
        /// The learn more URL.
        /// </value>
        public string LearnMoreURL => "https://www.nmi.com/";

        /// <summary>
        /// Gets the hosted gateway modes that this gateway has enabled
        /// </summary>
        /// <value>
        /// The hosted gateway modes.
        /// </value>
        public HostedGatewayMode[] GetHostedGatewayModes( FinancialGateway financialGateway )
        {
            var hostedGatewayModes = this.GetAttributeValue( financialGateway, AttributeKey.HostedGatewayModes )?.Split( ',' ).Select( a => a.ConvertToEnumOrNull<HostedGatewayMode>() ).Where( a => a != null ).Select( a => a.Value ).ToList();
            if ( hostedGatewayModes == null )
            {
                return new HostedGatewayMode[1] { HostedGatewayMode.Unhosted };
            }
            else
            {
                return hostedGatewayModes.ToArray();
            }
        }

        /// <summary>
        /// Gets the hosted payment information control which will be used to collect CreditCard, ACH fields
        /// Note: A HostedPaymentInfoControl can optionally implement <seealso cref="T:Rock.Financial.IHostedGatewayPaymentControlTokenEvent" />
        /// </summary>
        /// <param name="financialGateway">The financial gateway.</param>
        /// <param name="controlId">The control identifier.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        public Control GetHostedPaymentInfoControl( FinancialGateway financialGateway, string controlId, HostedPaymentInfoControlOptions options )
        {
            NMIHostedPaymentControl nmiHostedPaymentControl = new NMIHostedPaymentControl { ID = controlId };
            nmiHostedPaymentControl.NMIGateway = this;
            List<NMIPaymentType> enabledPaymentTypes = new List<NMIPaymentType>();
            if ( options?.EnableACH ?? true )
            {
                enabledPaymentTypes.Add( NMIPaymentType.ach );
            }

            if ( options?.EnableCreditCard ?? true )
            {
                enabledPaymentTypes.Add( NMIPaymentType.card );
            }

            nmiHostedPaymentControl.EnabledPaymentTypes = enabledPaymentTypes.ToArray();

            nmiHostedPaymentControl.TokenizationKey = this.GetAttributeValue( financialGateway, AttributeKey.TokenizationKey );

            return nmiHostedPaymentControl;
        }

        /// <summary>
        /// Gets the JavaScript needed to tell the hostedPaymentInfoControl to get send the paymentInfo and get a token.
        /// Have your 'Next' or 'Submit' call this so that the hostedPaymentInfoControl will fetch the token/response
        /// </summary>
        /// <param name="financialGateway">The financial gateway.</param>
        /// <param name="hostedPaymentInfoControl">The hosted payment information control.</param>
        /// <returns></returns>
        public string GetHostPaymentInfoSubmitScript( FinancialGateway financialGateway, Control hostedPaymentInfoControl )
        {
            return $"Rock.NMI.controls.gatewayCollectJS.submitPaymentInfo('{hostedPaymentInfoControl.ClientID}');";
        }

        public void UpdatePaymentInfoFromPaymentControl( FinancialGateway financialGateway, Control hostedPaymentInfoControl, ReferencePaymentInfo referencePaymentInfo, out string errorMessage )
        {
            errorMessage = null;

            /*var tokenResponse = ( hostedPaymentInfoControl as NMIHostedPaymentControl ).PaymentInfoTokenRaw.FromJsonOrNull<TokenizerResponse>();
            if ( tokenResponse?.IsSuccessStatus() != true )
            {
                if ( tokenResponse.HasValidationError() )
                {
                    errorMessage = tokenResponse.ValidationMessage;
                }

                errorMessage = tokenResponse?.Message ?? "null response from GetHostedPaymentInfoToken";
                referencePaymentInfo.ReferenceNumber = ( hostedPaymentInfoControl as NMIHostedPaymentControl ).PaymentInfoToken;
            }
            else
            {
                referencePaymentInfo.ReferenceNumber = ( hostedPaymentInfoControl as NMIHostedPaymentControl ).PaymentInfoToken;
            }
            */
        }

        /// <summary>
        /// Creates the customer account using a token received from the HostedPaymentInfoControl <seealso cref="M:Rock.Financial.IHostedGatewayComponent.GetHostedPaymentInfoControl(Rock.Model.FinancialGateway,System.String,Rock.Financial.HostedPaymentInfoControlOptions)" />
        /// and returns a customer account token that can be used for future transactions.
        /// </summary>
        /// <param name="financialGateway">The financial gateway.</param>
        /// <param name="paymentInfo">The payment information.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns></returns>
        public string CreateCustomerAccount( FinancialGateway financialGateway, ReferencePaymentInfo paymentInfo, out string errorMessage )
        {
            errorMessage = string.Empty;

            // see https://secure.tnbcigateway.com/merchants/resources/integration/integration_portal.php?#cv_variables
            var queryParameters = new Dictionary<string, string>();
            queryParameters.Add( "customer_vault", "add_customer" );
            queryParameters.Add( "payment_token", paymentInfo.GatewayPersonIdentifier );

            var response = PostToGatewayDirectPostAPI( financialGateway, queryParameters );

            // TODO
            return response.ToJson();
        }

        /// <summary>
        /// Gets the earliest scheduled start date that the gateway will accept for the start date, based on the current local time.
        /// </summary>
        /// <param name="financialGateway">The financial gateway.</param>
        /// <returns></returns>
        public DateTime GetEarliestScheduledStartDate( FinancialGateway financialGateway )
        {
            return RockDateTime.Today.AddDays( 1 ).Date;
        }

        #endregion IHostedGatewayComponent
    }
}

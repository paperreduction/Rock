using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Rock.Financial;

namespace Rock.NMI
{

    /// <summary>
    /// 
    /// </summary>
    [JsonConverter( typeof( StringEnumConverter ) )]
    public enum NMIPaymentType
    {
        /// <summary>
        /// The card
        /// </summary>
        card,

        /// <summary>
        /// The ach
        /// </summary>
        ach
    }

    public class BaseResponse
    {
        /// <summary>
        /// Newtonsoft.Json.JsonExtensionData instructs the Newtonsoft.Json.JsonSerializer to deserialize properties with no
        /// matching class member into the specified collection
        /// </summary>
        /// <value>
        /// The other data.
        /// </value>
        [Newtonsoft.Json.JsonExtensionData( ReadData = true, WriteData = false )]
        public IDictionary<string, Newtonsoft.Json.Linq.JToken> _additionalData { get; set; }
    }

    public class TokenizerResponse : BaseResponse
    {
        [JsonProperty( "message" )]
        public string Token { get; set; }

        [JsonProperty( "card" )]
        public CardTokenResponse Card { get; set; }

        [JsonProperty( "check" )]
        public CheckTokenResponse Check { get; set; }

        /* Not Sure if NMI has this stuff */

        public bool IsSuccessStatus() => true;

        public bool HasValidationError() => true;

        public string ValidationMessage { get; set; }

        public string Message { get; set; }
    }

    public class CardTokenResponse
    {
        [JsonProperty( "number" )]
        public string Number { get; set; }

        [JsonProperty( "bin" )]
        public string Bin { get; set; }

        [JsonProperty( "exp" )]
        public string Exp { get; set; }

        [JsonProperty( "hash" )]
        public string Hash { get; set; }

        [JsonProperty( "type" )]
        public string Type { get; set; }
    }

    public class CheckTokenResponse
    {
        [JsonProperty( "name" )]
        public string Name { get; set; }

        [JsonProperty( "account" )]
        public string Account { get; set; }

        [JsonProperty( "hash" )]
        public string Hash { get; set; }

        [JsonProperty( "aba" )]
        public string Aba { get; set; }
    }

    /*
        /// <summary>
        /// 
        /// </summary>
        public class NMITypes
        {



            /// <summary>
            /// 
            /// </summary>
            /// <seealso cref="Rock.NMI.NMITypes.TransactionDetailBase" />
            [Serializable()]
            [XmlType( AnonymousType = true )]
            [XmlRoot( Namespace = "", IsNullable = false, ElementName = "sale" )]
            public class Sale : TransactionDetailBase
            {
            }

            /// <summary>
            /// Transaction Sale
            /// </summary>
            [Serializable()]
            [XmlType( AnonymousType = true )]
            [XmlRoot( Namespace = "", IsNullable = false )]
            public abstract class TransactionDetailBase
            {
                /// <summary>
                /// Gets or sets the API key.
                /// </summary>
                /// <value>
                /// The API key.
                /// </value>
                [XmlElement( "api-key" )]
                public string ApiKey { get; set; }

                /// <summary>
                /// Cardholder's IP address. 
                /// </summary>
                /// <value>
                /// The ip address.
                /// </value>
                [XmlElement( "ip-address" )]
                public string IPAddress { get; set; }

                /// <summary>
                /// Gets or sets the currency (USD)
                /// </summary>
                /// <value>
                /// The currency.
                /// </value>
                [XmlElement( "currency" )]
                public string Currency { get; set; } = "USD";

                /// <summary>
                /// Total amount to be charged (For "validate" actions, amount must be 0.00 or omitted).
                /// </summary>
                /// <value>
                /// The amount.
                /// </value>
                [XmlElement( "amount" )]
                public decimal Amount { get; set; }

                /// <summary>
                /// Gets or sets the order description.
                /// </summary>
                /// <value>
                /// The order description.
                /// </value>
                [XmlElement( "order-description" )]
                public string OrderDescription { get; set; }

                /// <summary>
                /// Gets or sets the tax amount.
                /// </summary>
                /// <value>
                /// The tax amount.
                /// </value>
                [XmlElement( "tax-amount" )]
                public decimal TaxAmount { get; set; }

                /// <summary>
                /// Gets or sets the shipping amount.
                /// </summary>
                /// <value>
                /// The shipping amount.
                /// </value>
                [XmlElement( "shipping-amount" )]
                public decimal ShippingAmount { get; set; }

                [XmlElement( "add-customer" )]
                public AddCustomer AddCustomer { get; set; }

                /// <summary>
                /// A URL on your web server that the gateway will redirect your customer to after sensitive data collection. 
                /// </summary>
                /// <value>
                /// The redirecturl.
                /// </value>
                [XmlElement( "redirect-url" )]
                public string RedirectURL { get; set; }

                /// <summary>
                /// Gets or sets the billing.
                /// </summary>
                /// <value>
                /// The billing.
                /// </value>
                [XmlElement( "billing" )]
                public BillingAddress Billing { get; set; }
            }

            /// <summary>
            /// 
            /// </summary>
            [Serializable()]
            [XmlType( AnonymousType = true )]
            [XmlRoot( Namespace = "", IsNullable = false )]
            public class ResponseBase
            {
                /// <summary>
                /// Gets or sets the result.
                /// </summary>
                /// <value>
                /// The result.
                /// </value>
                [XmlElement( "result" )]
                public int Result { get; set; }


                /// <summary>
                /// Gets or sets the result text.
                /// </summary>
                /// <value>
                /// The result text.
                /// </value>
                [XmlElement( "result-text" )]
                public string ResultText { get; set; }


                /// <summary>
                /// Gets or sets the transaction identifier.
                /// </summary>
                /// <value>
                /// The transaction identifier.
                /// </value>
                [XmlElement( "transaction-id" )]
                public string TransactionId { get; set; }

                /// <summary>
                /// Gets or sets the result code.
                /// </summary>
                /// <value>
                /// The result code.
                /// </value>
                [XmlElement( "result-code" )]
                public int ResultCode { get; set; }

                /// <summary>
                /// Gets or sets the form URL.
                /// </summary>
                /// <value>
                /// The form URL.
                /// </value>
                [XmlElement( "form-url" )]
                public string FormUrl { get; set; }
            }

            /// <summary>
            /// Add/Update Customer XML Request
            /// </summary>
            [Serializable()]
            [XmlType( AnonymousType = true )]
            public class AddCustomer
            {
                /// <summary>
                /// Gets or sets the customer vault identifier.
                /// </summary>
                /// <value>
                /// The customer vault identifier.
                /// </value>
                [XmlElement( "customer-vault-id" )]
                public string CustomerVaultId { get; set; }
            }

            /// <summary>
            /// 
            /// </summary>
            [Serializable()]
            [XmlType( AnonymousType = true )]
            public class BillingAddress
            {
                /// <summary>
                /// Updates from payment information.
                /// </summary>
                /// <param name="paymentInfo">The payment information.</param>
                public void UpdateFromPaymentInfo( PaymentInfo paymentInfo)
                {
                    this.FirstName = paymentInfo.FirstName;
                    this.LastName = paymentInfo.LastName;
                    this.AddressLine1 = paymentInfo.Street1;
                    this.AddressLine2 = paymentInfo.Street2;
                    this.City = paymentInfo.City;
                    this.State = paymentInfo.State;
                    this.PostalCode = paymentInfo.PostalCode;
                    this.Country = paymentInfo.Country;
                    this.Phone = paymentInfo.Phone;
                    this.Email = paymentInfo.Email;
                }

                /// <summary>
                /// Gets or sets the first name.
                /// </summary>
                /// <value>
                /// The first name.
                /// </value>
                [XmlElement( "first-name" )]
                public string FirstName { get; set; }

                /// <summary>
                /// Gets or sets the last name.
                /// </summary>
                /// <value>
                /// The last name.
                /// </value>
                [XmlElement( "last-name" )]
                public string LastName { get; set; }

                /// <summary>
                /// Gets or sets the address line1.
                /// </summary>
                /// <value>
                /// The address line1.
                /// </value>
                [XmlElement( "address1" )]
                public string AddressLine1 { get; set; }

                /// <summary>
                /// Gets or sets the address line2.
                /// </summary>
                /// <value>
                /// The address line2.
                /// </value>
                [XmlElement( "address2" )]
                public string AddressLine2 { get; set; }

                /// <summary>
                /// Gets or sets the city.
                /// </summary>
                /// <value>
                /// The city.
                /// </value>
                [XmlElement( "city" )]
                public string City { get; set; }

                /// <summary>
                /// Gets or sets the state.
                /// </summary>
                /// <value>
                /// The state.
                /// </value>
                [XmlElement( "state" )]
                public string State { get; set; }

                /// <summary>
                /// Gets or sets the postal code.
                /// </summary>
                /// <value>
                /// The postal code.
                /// </value>
                [XmlElement( "postal" )]
                public string PostalCode { get; set; }

                /// <summary>
                /// Gets or sets the country.
                /// </summary>
                /// <value>
                /// The country.
                /// </value>
                [XmlElement( "country" )]
                public string Country { get; set; }

                /// <summary>
                /// Gets or sets the phone.
                /// </summary>
                /// <value>
                /// The phone.
                /// </value>
                [XmlElement( "phone" )]
                public string Phone { get; set; }

                /// <summary>
                /// Gets or sets the email.
                /// </summary>
                /// <value>
                /// The email.
                /// </value>
                [XmlElement( "email" )]
                public string Email { get; set; }
            }


            /// <summary>
            /// 
            /// </summary>
            [Serializable()]
            [XmlType( AnonymousType = true )]
            [XmlRoot( "complete-action", Namespace = "", IsNullable = false )]
            public class CompleteAction
            {
                /// <summary>
                /// Gets or sets the API key.
                /// </summary>
                /// <value>
                /// The API key.
                /// </value>
                [XmlElement( "api-key" )]
                public string ApiKey { get; set; }

                /// <summary>
                /// Gets or sets the token identifier.
                /// </summary>
                /// <value>
                /// The token identifier.
                /// </value>
                [XmlElement( "token-id" )]
                public string TokenId { get; set; }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <seealso cref="Rock.NMI.NMITypes.ResponseBase" />
            [Serializable()]
            [XmlType( AnonymousType = true )]
            [XmlRoot( Namespace = "", IsNullable = false )]
            public partial class CompletionResponse : ResponseBase
            {
                /// <summary>
                /// Gets or sets the authorization code.
                /// </summary>
                /// <value>
                /// The authorization code.
                /// </value>
                [XmlElement( "authorization-code" )]
                public string AuthorizationCode { get; set; }

                /// <summary>
                /// Gets or sets the avs result.
                /// </summary>
                /// <value>
                /// The avs result.
                /// </value>
                [XmlElement( "avs-result" )]
                public string AVSResult { get; set; }

                /// <summary>
                /// Gets or sets the CVV result.
                /// </summary>
                /// <value>
                /// The CVV result.
                /// </value>
                [XmlElement( "cvv-result" )]
                public string CVVResult { get; set; }

                /// <summary>
                /// Gets or sets the amount.
                /// </summary>
                /// <value>
                /// The amount.
                /// </value>
                [XmlElement( "amount" )]
                public decimal Amount { get; set; }

                /// <summary>
                /// Gets or sets the amount authorized.
                /// </summary>
                /// <value>
                /// The amount authorized.
                /// </value>
                [XmlElement( "amount-authorized" )]
                public decimal AmountAuthorized { get; set; }

                /// <summary>
                /// Gets or sets the tip amount.
                /// </summary>
                /// <value>
                /// The tip amount.
                /// </value>
                [XmlElement( "tip-amount" )]
                public decimal TipAmount { get; set; }

                /// <summary>
                /// Gets or sets the surcharge amount.
                /// </summary>
                /// <value>
                /// The surcharge amount.
                /// </value>
                [XmlElement( "surcharge-amount" )]
                public decimal SurchargeAmount { get; set; }

                /// <summary>
                /// Gets or sets the ip address.
                /// </summary>
                /// <value>
                /// The ip address.
                /// </value>
                [XmlElement( "ip-address" )]
                public string IPAddress { get; set; }

                /// <summary>
                /// Gets or sets the industry.
                /// </summary>
                /// <value>
                /// The industry.
                /// </value>
                [XmlElement( "industry" )]
                public string Industry { get; set; }

                /// <summary>
                /// Gets or sets the processor identifier.
                /// </summary>
                /// <value>
                /// The processor identifier.
                /// </value>
                [XmlElement( "processor-id" )]
                public string ProcessorId { get; set; }

                /// <summary>
                /// Gets or sets the currency.
                /// </summary>
                /// <value>
                /// The currency.
                /// </value>
                [XmlElement( "currency" )]
                public string Currency { get; set; }

                /// <summary>
                /// Gets or sets the customer identifier.
                /// </summary>
                /// <value>
                /// The customer identifier.
                /// </value>
                [XmlElement( "customer-id" )]
                public string CustomerId { get; set; }

                /// <summary>
                /// Gets or sets the customer vault identifier.
                /// </summary>
                /// <value>
                /// The customer vault identifier.
                /// </value>
                [XmlElement( "customer-vault-id" )]
                public string CustomerVaultId { get; set; }

                /// <summary>
                /// Gets or sets the tax amount.
                /// </summary>
                /// <value>
                /// The tax amount.
                /// </value>
                [XmlElement( "tax-amount" )]
                public decimal TaxAmount { get; set; }

                /// <summary>
                /// Gets or sets the shipping amount.
                /// </summary>
                /// <value>
                /// The shipping amount.
                /// </value>
                [XmlElement( "shipping-amount" )]
                public decimal ShippingAmount { get; set; }

                /// <summary>
                /// Gets or sets the billing.
                /// </summary>
                /// <value>
                /// The billing.
                /// </value>
                [XmlElement( "billing" )]
                public BillingAddress Billing { get; set; }
            }
        }*/
}

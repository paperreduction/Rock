using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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

    /// <summary>
    /// 
    /// </summary>
    /// <seealso cref="Rock.NMI.BaseResponse" />
    public class TokenizerResponse : BaseResponse
    {
        /// <summary>
        /// Gets or sets the token.
        /// </summary>
        /// <value>
        /// The token.
        /// </value>
        [JsonProperty( "message" )]
        public string Token { get; set; }

        /// <summary>
        /// Gets or sets the card.
        /// </summary>
        /// <value>
        /// The card.
        /// </value>
        [JsonProperty( "card" )]
        public CardTokenResponse Card { get; set; }

        /// <summary>
        /// Gets or sets the check.
        /// </summary>
        /// <value>
        /// The check.
        /// </value>
        [JsonProperty( "check" )]
        public CheckTokenResponse Check { get; set; }

        /* Not Sure if NMI has this stuff */
        public bool IsSuccessStatus() => true;

        public bool HasValidationError() => true;

        public string ValidationMessage { get; set; }

        public string Message { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
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

    /// <summary>
    /// 
    /// </summary>
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
}

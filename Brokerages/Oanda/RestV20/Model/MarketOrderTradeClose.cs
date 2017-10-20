/* 
 * OANDA v20 REST API
 *
 * The full OANDA v20 REST API Specification. This specification defines how to interact with v20 Accounts, Trades, Orders, Pricing and more.
 *
 * OpenAPI spec version: 3.0.15
 * Contact: api@oanda.com
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel.DataAnnotations;

namespace Oanda.RestV20.Model
{
    /// <summary>
    /// A MarketOrderTradeClose specifies the extensions to a Market Order that has been created specifically to close a Trade.
    /// </summary>
    [DataContract]
    public partial class MarketOrderTradeClose :  IEquatable<MarketOrderTradeClose>, IValidatableObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MarketOrderTradeClose" /> class.
        /// </summary>
        /// <param name="TradeID">The ID of the Trade requested to be closed.</param>
        /// <param name="ClientTradeID">The client ID of the Trade requested to be closed.</param>
        /// <param name="Units">Indication of how much of the Trade to close. Either \&quot;ALL\&quot;, or a DecimalNumber reflection a partial close of the Trade..</param>
        public MarketOrderTradeClose(string TradeID = default(string), string ClientTradeID = default(string), string Units = default(string))
        {
            this.TradeID = TradeID;
            this.ClientTradeID = ClientTradeID;
            this.Units = Units;
        }
        
        /// <summary>
        /// The ID of the Trade requested to be closed
        /// </summary>
        /// <value>The ID of the Trade requested to be closed</value>
        [DataMember(Name="tradeID", EmitDefaultValue=false)]
        public string TradeID { get; set; }
        /// <summary>
        /// The client ID of the Trade requested to be closed
        /// </summary>
        /// <value>The client ID of the Trade requested to be closed</value>
        [DataMember(Name="clientTradeID", EmitDefaultValue=false)]
        public string ClientTradeID { get; set; }
        /// <summary>
        /// Indication of how much of the Trade to close. Either \&quot;ALL\&quot;, or a DecimalNumber reflection a partial close of the Trade.
        /// </summary>
        /// <value>Indication of how much of the Trade to close. Either \&quot;ALL\&quot;, or a DecimalNumber reflection a partial close of the Trade.</value>
        [DataMember(Name="units", EmitDefaultValue=false)]
        public string Units { get; set; }
        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("class MarketOrderTradeClose {\n");
            sb.Append("  TradeID: ").Append(TradeID).Append("\n");
            sb.Append("  ClientTradeID: ").Append(ClientTradeID).Append("\n");
            sb.Append("  Units: ").Append(Units).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }
  
        /// <summary>
        /// Returns the JSON string presentation of the object
        /// </summary>
        /// <returns>JSON string presentation of the object</returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// Returns true if objects are equal
        /// </summary>
        /// <param name="obj">Object to be compared</param>
        /// <returns>Boolean</returns>
        public override bool Equals(object obj)
        {
            // credit: http://stackoverflow.com/a/10454552/677735
            return this.Equals(obj as MarketOrderTradeClose);
        }

        /// <summary>
        /// Returns true if MarketOrderTradeClose instances are equal
        /// </summary>
        /// <param name="other">Instance of MarketOrderTradeClose to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(MarketOrderTradeClose other)
        {
            // credit: http://stackoverflow.com/a/10454552/677735
            if (other == null)
                return false;

            return 
                (
                    this.TradeID == other.TradeID ||
                    this.TradeID != null &&
                    this.TradeID.Equals(other.TradeID)
                ) && 
                (
                    this.ClientTradeID == other.ClientTradeID ||
                    this.ClientTradeID != null &&
                    this.ClientTradeID.Equals(other.ClientTradeID)
                ) && 
                (
                    this.Units == other.Units ||
                    this.Units != null &&
                    this.Units.Equals(other.Units)
                );
        }

        /// <summary>
        /// Gets the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            // credit: http://stackoverflow.com/a/263416/677735
            unchecked // Overflow is fine, just wrap
            {
                int hash = 41;
                // Suitable nullity checks etc, of course :)
                if (this.TradeID != null)
                    hash = hash * 59 + this.TradeID.GetHashCode();
                if (this.ClientTradeID != null)
                    hash = hash * 59 + this.ClientTradeID.GetHashCode();
                if (this.Units != null)
                    hash = hash * 59 + this.Units.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// To validate all properties of the instance
        /// </summary>
        /// <param name="validationContext">Validation context</param>
        /// <returns>Validation Result</returns>
        IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        { 
            yield break;
        }
    }

}

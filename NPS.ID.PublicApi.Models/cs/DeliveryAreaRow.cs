//----------------------
// <auto-generated>
//     Generated using the NJsonSchema v9.5.4.0 (http://NJsonSchema.org)
// </auto-generated>
//----------------------

namespace NPS.ID.PublicApi.Models.Generated
{
    #pragma warning disable // Disable all warnings

    /// <summary>TODO: Description</summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "9.5.4.0")]
    public partial class DeliveryAreaRow 
    {
        [Newtonsoft.Json.JsonProperty("deliveryAreaId", Required = Newtonsoft.Json.Required.Always)]
        public int DeliveryAreaId { get; set; }
    
        [Newtonsoft.Json.JsonProperty("eicCode", Required = Newtonsoft.Json.Required.AllowNull)]
        public string EicCode { get; set; }
    
        [Newtonsoft.Json.JsonProperty("currentyCode", Required = Newtonsoft.Json.Required.AllowNull)]
        public string CurrentyCode { get; set; }
    
        [Newtonsoft.Json.JsonProperty("areaCode", Required = Newtonsoft.Json.Required.AllowNull)]
        public string AreaCode { get; set; }
    
        [Newtonsoft.Json.JsonProperty("timeZone", Required = Newtonsoft.Json.Required.AllowNull)]
        public string TimeZone { get; set; }
    
        [Newtonsoft.Json.JsonProperty("countryIsoCode", Required = Newtonsoft.Json.Required.AllowNull)]
        public string CountryIsoCode { get; set; }
    
        [Newtonsoft.Json.JsonProperty("productTypes", Required = Newtonsoft.Json.Required.AllowNull)]
        public System.Collections.Generic.List<string> ProductTypes { get; set; }
    
        [Newtonsoft.Json.JsonProperty("deleted", Required = Newtonsoft.Json.Required.Always)]
        public bool Deleted { get; set; }
    
        [Newtonsoft.Json.JsonProperty("updatedAt", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public System.DateTimeOffset UpdatedAt { get; set; }
    
        public string ToJson() 
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }
        
        public static DeliveryAreaRow FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<DeliveryAreaRow>(data);
        }
    }
}
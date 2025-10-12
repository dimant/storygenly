using System.Text.Json.Serialization;

namespace StoryGenly.Models
{
    public class BookFormats
    {
        [JsonPropertyName("text/html")]
        public string? TextHtml { get; set; }
        
        [JsonPropertyName("application/epub+zip")]
        public string? EpubZip { get; set; }
        
        [JsonPropertyName("application/x-mobipocket-ebook")]
        public string? MobiPocket { get; set; }
        
        [JsonPropertyName("text/plain; charset=us-ascii")]
        public string? TextPlainAscii { get; set; }
        
        [JsonPropertyName("text/plain; charset=utf-8")]
        public string? TextPlainUtf8 { get; set; }
        
        [JsonPropertyName("application/rdf+xml")]
        public string? RdfXml { get; set; }
        
        [JsonPropertyName("image/jpeg")]
        public string? ImageJpeg { get; set; }
        
        [JsonPropertyName("application/octet-stream")]
        public string? OctetStream { get; set; }
    }
}

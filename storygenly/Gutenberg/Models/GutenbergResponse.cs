using System.Collections.Generic;

namespace StoryGenly.Models
{
    public class GutenbergResponse
    {
        public int Count { get; set; }
        public string? Next { get; set; }
        public string? Previous { get; set; }
        public List<Book> Results { get; set; } = new List<Book>();
    }
}

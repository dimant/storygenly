using System.Collections.Generic;

namespace StoryGenly.Models
{
    public class Book
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public List<Author> Authors { get; set; } = new List<Author>();
        public List<string> Summaries { get; set; } = new List<string>();
        public List<Editor> Editors { get; set; } = new List<Editor>();
        public List<Translator> Translators { get; set; } = new List<Translator>();
        public List<string> Subjects { get; set; } = new List<string>();
        public List<string> Bookshelves { get; set; } = new List<string>();
        public List<string> Languages { get; set; } = new List<string>();
        public bool Copyright { get; set; }
        public string MediaType { get; set; } = string.Empty;
        public BookFormats Formats { get; set; } = new BookFormats();
        public int DownloadCount { get; set; }
    }
}

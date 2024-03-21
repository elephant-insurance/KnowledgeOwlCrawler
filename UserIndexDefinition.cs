using Microsoft.Azure.Search;
using System.ComponentModel.DataAnnotations;


namespace KnowledgeOwlCrawler
{
    public class UserIndexDefinition : IIndexDefinition
    {
        public string IndexName { get; set; } = "knowledgeowl-test-web-index";

        // All indexes needs a key. 
        [Key]
        public string IndexKey { get; set; }

        [IsFilterable, IsSearchable]
        public string id { get; set; }

        [IsFilterable, IsSearchable]
        public string content { get; set; }

        [IsFilterable, IsSearchable]
        public string title { get; set; }

        [IsFilterable, IsSearchable]
        public string url { get; set; }
    }
}

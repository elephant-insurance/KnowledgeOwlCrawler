using Microsoft.Azure.Search;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace KnowledgeOwlCrawler
{
    public class AzureIndexManagementContext : IIndexManagementContext
    {
        // Since the index name is stored in the index definition class, but should not
        // become an index field, the index name have been marked as "JSON ignore"
        // and the field indexer should therefore ignore the index name when
        // creating the index fields.
        private class IgnoreJsonIgnoreMarkedPropertiesContractResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                IList<JsonProperty> properties = base.CreateProperties(type, memberSerialization);
                properties = properties.Where(p => !p.Ignored).ToList();
                return properties;
            }
        }

        private readonly ISearchServiceClient _searchServiceClient;

        public AzureIndexManagementContext(string searchServiceName, string adminApiKey)
        {
            _searchServiceClient = new SearchServiceClient(searchServiceName, new SearchCredentials(adminApiKey));
        }

        public void CreateOrUpdateIndex<T>() where T : IIndexDefinition, new()
        {
            string name = new T().IndexName;
            var definition = new Microsoft.Azure.Search.Models.Index
            {
                Name = name,
                Fields = FieldBuilder.BuildForType<T>(new IgnoreJsonIgnoreMarkedPropertiesContractResolver())
            };

            _searchServiceClient.Indexes.CreateOrUpdate(definition);
        }

        public void DeleteIndex<T>() where T : IIndexDefinition, new()
        {
            string name = new T().IndexName;
            _searchServiceClient.Indexes.Delete(name);
        }
    }
}

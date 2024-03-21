
using Newtonsoft.Json;

namespace KnowledgeOwlCrawler
{
    public interface IIndexManagementContext
    {
        /// <summary>
        /// Create or update the index
        /// </summary>
        /// <typeparam name="T">The type of the index definition for the index to create or update</typeparam>
        void CreateOrUpdateIndex<T>() where T : IIndexDefinition, new();

        /// <summary>
        /// Delete the index given by a given index definition. 
        /// </summary>
        /// <typeparam name="T">The type of the index definition for the index to delete</typeparam>
        void DeleteIndex<T>() where T : IIndexDefinition, new();
    }

    public interface IIndexDefinition
    {
        // The name of the index. 
        // Property is ignored when serialized to JSON
        [JsonIgnore]
        string IndexName { get; set; }
    }
}

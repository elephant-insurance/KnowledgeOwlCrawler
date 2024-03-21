using Azure.Core.Serialization;
using Azure;
using Azure.Search.Documents;
using System.Collections.Concurrent;
using static KnowledgeOwlCrawler.AzureSearchIndexer;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using Microsoft.AspNetCore.Mvc;


namespace KnowledgeOwlCrawler.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class KnowledgeOwlController : ControllerBase
    {
        private readonly BlockingCollection<WebPage> _queue = [];
        private readonly IConfiguration _configuration;
        private AISearchConfig aiSearchConfig;
        private KnowledgeOwl koApiConfig;

        private readonly ILogger<KnowledgeOwlController> _logger;

        public KnowledgeOwlController(IConfiguration configuration)
        {
            _configuration = configuration;

            aiSearchConfig = new()
            {
                Url = _configuration.GetValue<string>("Search:Url"),
                ServiceName = _configuration.GetValue<string>("Search:ServiceName"),
                Key = _configuration.GetValue<string>("Search:Key"),
                Index = _configuration.GetValue<string>("Search:Index")
            };

            koApiConfig = new KnowledgeOwl
            {
                Url = _configuration.GetValue<string>("KnowledgeOwl:Url"),
                Username = _configuration.GetValue<string>("KnowledgeOwl:Username"),
                Password = _configuration.GetValue<string>("KnowledgeOwl:Password")
            };
        }

        [HttpGet]
        [Route("IndexKOData")]
        public async Task<int> IndexKOData()
        {
            string svcCredentials = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(koApiConfig.Username + ":" + koApiConfig.Password));

            using HttpClient client = new();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("Authorization", "Basic " + svcCredentials);

            Uri endpoint = new(aiSearchConfig.Url);
            AzureKeyCredential credential = new(aiSearchConfig.Key);

            JsonSerializerOptions serializerOptions = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            SearchClientOptions clientOptions = new()
            {
                Serializer = new JsonObjectSerializer(serializerOptions)
            };


            SearchClient _indexClient = new SearchClient(endpoint, aiSearchConfig.Index, credential, clientOptions);

            var koJsonData = await CallKOAPIAsync(client, "1", koApiConfig);
            UploadData(koJsonData, aiSearchConfig, _indexClient);
            
            //If pages are more than 1, call the API again for the next pages
            if (koJsonData.page_stats.total_pages > 1)
            {
                for (int i = 2; i <= koJsonData.page_stats.total_pages; i++)
                {
                    koJsonData = await CallKOAPIAsync(client, i.ToString(), koApiConfig);
                    UploadData(koJsonData, aiSearchConfig, _indexClient);
                }
            }   
            return 1;
        }

        [HttpGet]
        [Route("ClearIndex")]
        public int ClearIndex()
        {
            Uri endpoint = new(aiSearchConfig.Url);
            AzureKeyCredential credential = new(aiSearchConfig.Key);
            JsonSerializerOptions serializerOptions = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            SearchClientOptions clientOptions = new()
            {
                Serializer = new JsonObjectSerializer(serializerOptions)
            };

            IIndexManagementContext indexManagementContext = new AzureIndexManagementContext(aiSearchConfig.ServiceName, aiSearchConfig.Key);
            IIndexDefinition userIndexDefinition = new UserIndexDefinition();
            //override the index name
            userIndexDefinition.IndexName = aiSearchConfig.Index;

            //delete the index
            var methodInfo = typeof(IIndexManagementContext).GetMethod("DeleteIndex");
            var genericMethod = methodInfo.MakeGenericMethod(userIndexDefinition.GetType());
            genericMethod.Invoke(indexManagementContext, null);

            //create the index
            var methodInfo1 = typeof(IIndexManagementContext).GetMethod("CreateOrUpdateIndex");
            var genericMethod1 = methodInfo1.MakeGenericMethod(userIndexDefinition.GetType());
            genericMethod1.Invoke(indexManagementContext, null);

            return 1;
        }

        static async Task<JsonData> CallKOAPIAsync(HttpClient client, string pageNumber, KnowledgeOwl koApiConfig)
        {
            var query = new Dictionary<string, string>
            {
                ["status"] = "published",
                ["visibility"] = "public",
                ["page"] = pageNumber
            };

            var response = await client.GetAsync(QueryHelpers.AddQueryString(koApiConfig.Url, query), HttpCompletionOption.ResponseHeadersRead);
            var stream = await response.Content.ReadAsStringAsync();

            JsonData koData = JsonSerializer.Deserialize<JsonData>(stream);
            return koData ?? new();
        }

        internal void UploadData(JsonData koJsonData, AISearchConfig aiSearchConfig, SearchClient _indexClient)
        {
            WebPage pg;
            string url = "";
            string content = "";
            int count = koJsonData.data.Count;

            for (int i = 0; i < koJsonData.data.Count; i++)
            {
    
                url = koJsonData.data[i]?.redirect_options?.redirect_url;
                if (url == "" || url == null || url == "https://")
                {
                    url = "https://app.knowledgeowl.com/kb/articles/id/" + koJsonData.data[i].project_id + "/aid/" + koJsonData.data[i].id;
                }
                content = koJsonData.data[i].current_version?.en?.text;

                //replace special HTML content with nothing
                if (content != null && content != "")
                {

                    content = Regex.Replace(content, "<!--.+?-->", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    content = Regex.Replace(content, "{{.+?}}", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    content = Regex.Replace(content, "(<style.+?</style>)|(<script.+?</script>)", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    content = Regex.Replace(content, @"</[^>]+?>", " ");
                    content = Regex.Replace(content, @"<[^>]+?>", "");
                }
                else
                {
                    content = "No content for page";
                }

                pg = new WebPage(url, koJsonData.data[i].searchTitle?.en, content);
                _queue.Add(pg);
                url = "";
                content = "";
            }

            var pages = new List<WebPage>(count);
            for (int i = 0; i < count; i++)
            {
                pages.Add(_queue.Take());
            }
            _indexClient.UploadDocuments(pages);
        }
    }
}

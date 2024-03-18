using Azure.Core.Serialization;
using Azure;
using Azure.Search.Documents;
using System.Collections.Concurrent;
using static KnowledgeOwlCrawler.AzureSearchIndexer;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace KnowledgeOwlCrawler
{
    public class ProcessKOAPIData
    {
        private readonly BlockingCollection<WebPage> _queue = [];
        private readonly SearchClient? _indexClient;
        private readonly IConfiguration ?Configuration;

        public async void Process()
        {
            AISearchConfig aiSearchConfig = new AISearchConfig();

            aiSearchConfig.Url = Configuration.GetSection("Search").GetSection("Url").Value;
            aiSearchConfig.Key = Configuration.GetSection("Search").GetSection("Key").Value;
            aiSearchConfig.Index = Configuration.GetSection("Search").GetSection("Index").Value;

            KnowledgeOwl koApiConfig = new KnowledgeOwl();
            koApiConfig.Url = Configuration.GetSection("Search").GetSection("Url").Value;
            koApiConfig.Username = Configuration.GetSection("Search").GetSection("Username").Value;
            koApiConfig.Password = Configuration.GetSection("Search").GetSection("Password").Value;

            string svcCredentials = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(koApiConfig.Username + ":" + koApiConfig.Password));

            using HttpClient client = new();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("Authorization", "Basic " + svcCredentials);
            var koJsonData = await CallKOAPIAsync(client);

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

            string? url = "";
            WebPage pg;
            string? content = "";
            int? count = koJsonData?.data?.Count;

            for (int i = 0; i < koJsonData?.data?.Count; i++)
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
            SearchClient _indexClient = new SearchClient(endpoint, aiSearchConfig.Index, credential, clientOptions);

            var pages = new List<WebPage>(500);
            for (int i = 0; i < count; i++)
            {
                pages.Add(_queue.Take());
            }
            _indexClient.UploadDocuments(pages);
        }

        static async Task<JsonData> CallKOAPIAsync(HttpClient client)
        {
            var query = new Dictionary<string, string?>
            {
                ["status"] = "published",
                ["visibility"] = "public",
            };

            var url = "https://app.knowledgeowl.com/api/head/article.json";
            var response = await client.GetAsync(QueryHelpers.AddQueryString(url, query), HttpCompletionOption.ResponseHeadersRead);
            var stream = await response.Content.ReadAsStringAsync();

            JsonData? koData = JsonSerializer.Deserialize<JsonData>(stream);
            return koData ?? new();
        }
    }
}

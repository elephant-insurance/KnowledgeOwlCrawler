using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KnowledgeOwlCrawler
{
    class Program
    {
        static void Main(string[] args)
        {
            ProcessKOAPIData objectKoAPI = new ProcessKOAPIData();
            objectKoAPI.Process();
        }
    }
}

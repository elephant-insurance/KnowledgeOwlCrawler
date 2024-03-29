﻿namespace KnowledgeOwlCrawler
{
    public class JsonData
    {
        public List<Data> data { get; set; }
        public PageStats page_stats { get; set; }
    }
    
    public class PageStats {             
        public int total_pages { get; set; }
        public int total_records { get; set; }
    }

    public class Data
    {
        public SearchTitle searchTitle { get; set; }
        public CurrentVersion current_version { get; set; }
        public string id { get; set; }
        public string project_id { get; set; }
        public RedirectOptions redirect_options { get; set; }
    }
    public class SearchTitle
    {
        public string en { get; set; }
    }
    public class CurrentVersion
    {
        public En en { get; set; }
    }
    public class En
    {
        public string title { get; set; }
        public string text { get; set; }
    }
    public class RedirectOptions
    {
        public string? redirect_url { get; set; }
    }
}

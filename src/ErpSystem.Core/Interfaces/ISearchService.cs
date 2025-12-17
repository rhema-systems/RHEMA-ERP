using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ErpSystem.Core.Interfaces
{
    public interface ISearchService
    {
        Task<SearchResult<T>> SearchAsync<T>(SearchQuery query) where T : class;
        Task IndexAsync<T>(T document, string index) where T : class;
        Task DeleteAsync<T>(string id, string index) where T : class;
        Task<bool> IndexExistsAsync(string index);
        Task CreateIndexAsync(string index);
    }

    public class SearchQuery
    {
        public string Term { get; set; } = string.Empty;
        public Dictionary<string, object> Filters { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SortBy { get; set; } = string.Empty;
        public bool SortDescending { get; set; } = false;
        public List<string> Facets { get; set; } = new();
    }

    public class SearchResult<T>
    {
        public List<T> Documents { get; set; } = new();
        public long TotalCount { get; set; }
        public Dictionary<string, List<FacetValue>> Facets { get; set; } = new();
        public TimeSpan SearchTime { get; set; }
    }

    public class FacetValue
    {
        public string Value { get; set; } = string.Empty;
        public long Count { get; set; }
    }
}

using System;

namespace AutoAllegro.Models.HelperModels
{
    public class PaginationView
    {
        public int CurrentPage { get; set; }
        public int PagesCount { get; set; }

        public bool IsFirstPage => CurrentPage == 1;
        public bool IsLastPage => PagesCount == CurrentPage;
        public int StartPage => Math.Max(1, CurrentPage - 2);
        public int EndPage => Math.Min(PagesCount, CurrentPage + 2);
    }
}
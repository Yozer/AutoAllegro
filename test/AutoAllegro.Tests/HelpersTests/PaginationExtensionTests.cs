using System;
using System.Collections.Generic;
using System.Linq;
using AutoAllegro.Helpers.Extensions;
using AutoAllegro.Models.AuctionViewModels;
using AutoAllegro.Models.HelperModels;
using Xunit;

namespace AutoAllegro.Tests.HelpersTests
{
    public class PaginationExtensionTests
    {
        [Fact]
        public void Pagination_PaginatesBiggerList_PageNotSpecified()
        {
            // arrange
            var view = new PaginableView
            {
                Items = Enumerable.Range(1, 55).ToList()
            };

            // act
            view.Paginate(null, 25, t => t.Items);

            // assert
            Assert.Equal(25, view.Items.Count);
            Assert.Equal(Enumerable.Range(1, 25), view.Items);
            Assert.Equal(1, view.PaginationSettings.CurrentPage);
            Assert.Equal(1, view.PaginationSettings.StartPage);
            Assert.Equal(3, view.PaginationSettings.EndPage);
            Assert.Equal(3, view.PaginationSettings.PagesCount);
            Assert.True(view.PaginationSettings.IsFirstPage);
            Assert.False(view.PaginationSettings.IsLastPage);
        }
        [Fact]
        public void Pagination_PaginatesBiggerList_FirstPage()
        {
            // arrange
            var view = new PaginableView
            {
                Items = Enumerable.Range(1, 55).ToList()
            };

            // act
            view.Paginate(1, 25, t => t.Items);

            // assert
            Assert.Equal(25, view.Items.Count);
            Assert.Equal(Enumerable.Range(1, 25), view.Items);
            Assert.Equal(1, view.PaginationSettings.CurrentPage);
            Assert.Equal(1, view.PaginationSettings.StartPage);
            Assert.Equal(3, view.PaginationSettings.EndPage);
            Assert.Equal(3, view.PaginationSettings.PagesCount);
            Assert.True(view.PaginationSettings.IsFirstPage);
            Assert.False(view.PaginationSettings.IsLastPage);
        }

        [Fact]
        public void Pagination_PaginatesBiggerList_SecondPage()
        {
            // arrange
            var view = new PaginableView
            {
                Items = Enumerable.Range(1, 55).ToList()
            };

            // act
            view.Paginate(2, 25, t => t.Items);

            // assert
            Assert.Equal(25, view.Items.Count);
            Assert.Equal(Enumerable.Range(26, 25), view.Items);
            Assert.Equal(2, view.PaginationSettings.CurrentPage);
            Assert.Equal(1, view.PaginationSettings.StartPage);
            Assert.Equal(3, view.PaginationSettings.EndPage);
            Assert.Equal(3, view.PaginationSettings.PagesCount);
            Assert.False(view.PaginationSettings.IsFirstPage);
            Assert.False(view.PaginationSettings.IsLastPage);
        }
        [Fact]
        public void Pagination_PaginatesBiggerList_ThirdPage()
        {
            // arrange
            var view = new PaginableView
            {
                Items = Enumerable.Range(1, 55).ToList()
            };

            // act
            view.Paginate(3, 25, t => t.Items);

            // assert
            Assert.Equal(5, view.Items.Count);
            Assert.Equal(Enumerable.Range(51, 5), view.Items);
            Assert.Equal(3, view.PaginationSettings.CurrentPage);
            Assert.Equal(1, view.PaginationSettings.StartPage);
            Assert.Equal(3, view.PaginationSettings.EndPage);
            Assert.Equal(3, view.PaginationSettings.PagesCount);
            Assert.False(view.PaginationSettings.IsFirstPage);
            Assert.True(view.PaginationSettings.IsLastPage);
        }

        [Fact]
        public void Pagination_Paginate_PageLessThenZero()
        {
            // arrange
            var view = new PaginableView
            {
                Items = Enumerable.Range(1, 55).ToList()
            };

            // act
            view.Paginate(-2, 5, t => t.Items);

            // assert
            Assert.Equal(5, view.Items.Count);
            Assert.Equal(Enumerable.Range(1, 5), view.Items);
            Assert.Equal(1, view.PaginationSettings.CurrentPage);
            Assert.Equal(1, view.PaginationSettings.StartPage);
            Assert.Equal(3, view.PaginationSettings.EndPage);
            Assert.Equal(11, view.PaginationSettings.PagesCount);
            Assert.True(view.PaginationSettings.IsFirstPage);
            Assert.False(view.PaginationSettings.IsLastPage);
        }

        [Fact]
        public void Pagination_Paginate_PageGreaterThanNumberOfPages()
        {
            // arrange
            var view = new PaginableView
            {
                Items = Enumerable.Range(1, 55).ToList()
            };

            // act
            view.Paginate(14, 5, t => t.Items);

            // assert
            Assert.Equal(5, view.Items.Count);
            Assert.Equal(Enumerable.Range(51, 5), view.Items);
            Assert.Equal(11, view.PaginationSettings.CurrentPage);
            Assert.Equal(9, view.PaginationSettings.StartPage);
            Assert.Equal(11, view.PaginationSettings.EndPage);
            Assert.Equal(11, view.PaginationSettings.PagesCount);
            Assert.False(view.PaginationSettings.IsFirstPage);
            Assert.True(view.PaginationSettings.IsLastPage);
        }

        [Fact]
        public void Pagination_Paginate_PageSizeGreaterThanTotalItems_ShouldShowAllItems()
        {
            // arrange
            var view = new PaginableView
            {
                Items = Enumerable.Range(1, 4).ToList()
            };

            // act
            view.Paginate(1, 25, t => t.Items);

            // assert
            Assert.Equal(4, view.Items.Count);
            Assert.Equal(Enumerable.Range(1, 4), view.Items);
            Assert.Equal(1, view.PaginationSettings.CurrentPage);
            Assert.Equal(1, view.PaginationSettings.StartPage);
            Assert.Equal(1, view.PaginationSettings.EndPage);
            Assert.Equal(1, view.PaginationSettings.PagesCount);
            Assert.True(view.PaginationSettings.IsFirstPage);
            Assert.True(view.PaginationSettings.IsLastPage);
        }

        [Fact]
        public void Pagination_PaginateSecondPage_PageSizeGreaterThanTotalItems_ShouldShowAllItems()
        {
            // arrange
            var view = new PaginableView
            {
                Items = Enumerable.Range(1, 4).ToList()
            };

            // act
            view.Paginate(2, 25, t => t.Items);

            // assert
            Assert.Equal(4, view.Items.Count);
            Assert.Equal(Enumerable.Range(1, 4), view.Items);
            Assert.Equal(1, view.PaginationSettings.CurrentPage);
            Assert.Equal(1, view.PaginationSettings.StartPage);
            Assert.Equal(1, view.PaginationSettings.EndPage);
            Assert.Equal(1, view.PaginationSettings.PagesCount);
            Assert.True(view.PaginationSettings.IsFirstPage);
            Assert.True(view.PaginationSettings.IsLastPage);
        }
    }

    class PaginableView : IPaginable
    {
        public IList<int> Items { get; set; }
        public PaginationView PaginationSettings { get; set; }
    }
}

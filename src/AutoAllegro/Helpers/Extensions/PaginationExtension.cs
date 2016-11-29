using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AutoAllegro.Models.AuctionViewModels;
using AutoAllegro.Models.HelperModels;

namespace AutoAllegro.Helpers.Extensions
{
    public static class PaginationExtension
    {
        public static void Paginate<T, E>(this T obj, int? page, int pageSize, Expression<Func<T, IList<E>>> selector)
            where T : IPaginable
        {

            IList<E> list = selector.Compile()(obj);
            int pagesCount = Math.Max(1, (int) Math.Ceiling(list.Count/(decimal) pageSize));

            page = page ?? 1;
            page = Math.Max(1, page.Value);
            page = Math.Min(pagesCount, page.Value);

            --page;
            int from = page.Value*pageSize;

            IList<E> pagedList = list.Skip(from).Take(pageSize).ToList();

            obj.PaginationSettings = new PaginationView
            {
                CurrentPage = page.Value + 1,
                PagesCount = pagesCount
            };

            var prop = (PropertyInfo)((MemberExpression)selector.Body).Member;
            prop.SetValue(obj, pagedList, null);
        }
    }
}

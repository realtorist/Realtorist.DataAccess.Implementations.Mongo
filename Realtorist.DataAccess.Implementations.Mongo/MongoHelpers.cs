using AutoMapper;
using MongoDB.Driver;
using Realtorist.Models.Blog;
using Realtorist.Models.Enums;
using Realtorist.Models.Helpers;
using Realtorist.Models.Page;
using Realtorist.Models.Pagination;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Realtorist.DataAccess.Implementations.Mongo
{
    /// <summary>
    /// Contains useful mongo helpers
    /// </summary>
    public static class MongoHelpers
    {
        /// <summary>
        /// Gets pagination result from  the <see cref="IFindFluent{TDocument, TProjection}"/> cursor
        /// </summary>
        /// <typeparam name="T">Document type</typeparam>
        /// <typeparam name="V">Result type</typeparam>
        /// <param name="cursor">Mongo cursor</param>
        /// <param name="request">Pagination request</param>
        /// <param name="sortProperty">Property to sort by</param>
        /// <param name="sortByOrder">Sort order</param>
        /// <returns>Pagination result</returns>
        public static async Task<PaginationResult<V>> GetPaginationResultAsync<T,V>(
            this IFindFluent<T, V> cursor, 
            PaginationRequest request,
            Expression<Func<T, object>> sortProperty = null,
            SortByOrder sortByOrder = SortByOrder.Asc)
        {
            var result = new PaginationResult<V>
            {
                Limit = request.Limit,
                Offset = request.Offset,
            };

            result.TotalRecords = (int)(await cursor.CountDocumentsAsync());

            var results = cursor;

            if (!request.SortField.IsNullOrEmpty()){
                sortProperty = request.SortField.GetSelectExpression<T>();
                sortByOrder = request.SortOrder;
            }

            if (sortProperty is not null)
            {
                results = sortByOrder switch
                {
                    SortByOrder.Asc => results.SortBy(sortProperty),
                    SortByOrder.Desc => results.SortByDescending(sortProperty),
                    _ => throw new InvalidOperationException($"Unknown sort order: {sortByOrder}")
                };
            }

            result.Results = (await results
                    .Skip(request.Offset)
                    .Limit(result.Limit)
                    .ToListAsync())
                .ToArray();

            return result;
        }

        /// <summary>
        /// Gets pagination result from  the <see cref="IMongoCollection{TDocument}"/> cursor
        /// </summary>
        /// <typeparam name="T">Document type</typeparam>
        /// <param name="collection">Mongo collection</param>
        /// <param name="request">Pagination request</param>
        /// <param name="sortProperty">Property to sort by</param>
        /// <param name="sortByOrder">Sort order</param>
        /// <returns>Pagination result</returns>
        public static async Task<PaginationResult<T>> GetPaginationResultAsync<T>(
            this IMongoCollection<T> collection, 
            PaginationRequest request,
            Expression<Func<T, object>> sortProperty = null,
            SortByOrder sortByOrder = SortByOrder.Asc)
        {
            return await  collection
                .Find(Builders<T>.Filter.Empty)
                .GetPaginationResultAsync(request, sortProperty, sortByOrder);
        }

        /// <summary>
        /// Gets pagination result from the <see cref="IQueryable{T}"/> cursor
        /// </summary>
        /// <typeparam name="T">Document type</typeparam>
        /// <param name="query">Queryable</param>
        /// <param name="request">Pagination request</param>
        /// <param name="sortProperty">Property to sort by</param>
        /// <param name="sortByOrder">Sort order</param>
        /// <returns>Pagination result</returns>
        public static async Task<PaginationResult<T>> GetPaginationResultAsync<T>(
            this IQueryable<T> query, 
            PaginationRequest request,
            Expression<Func<T, object>> sortProperty = null,
            SortByOrder sortByOrder = SortByOrder.Asc)
        {
            var result = new PaginationResult<T>
            {
                Limit = request.Limit,
                Offset = request.Offset,
            };

            result.TotalRecords = query.Count();

            var results = query;

            if (!request.SortField.IsNullOrEmpty()){
                sortProperty = request.SortField.GetSelectExpression<T>();
                sortByOrder = request.SortOrder;
            }

            if (sortProperty is not null)
            {
                results = sortByOrder switch
                {
                    SortByOrder.Asc => results.OrderBy(sortProperty),
                    SortByOrder.Desc => results.OrderByDescending(sortProperty),
                    _ => throw new InvalidOperationException($"Unknown sort order: {sortByOrder}")
                };
            }

            result.Results = results
                .Skip(request.Offset)
                .Take(result.Limit)
                .ToArray();

            return result;
        }

        /// <summary>
        /// Projects result from  the <see cref="IFindFluent{TDocument, TProjection}"/> cursor
        /// If document type and destination type are same, return cursor back with out any operations
        /// </summary>
        /// <typeparam name="TDocument">Document type</typeparam>
        /// <typeparam name="TFrom">Cursor type</typeparam>
        /// <typeparam name="TTo">Result type</typeparam>
        /// <param name="cursor">Mongo cursor</param>
        /// <param name="mapper">Auto mapper</param>
        /// <returns>Project result</returns>
        public static IFindFluent<TDocument, TTo> Project<TDocument, TFrom, TTo>(this IFindFluent<TDocument, TFrom> cursor, IMapper mapper)
        {
            if (typeof(TDocument) == typeof(TTo)) return (IFindFluent<TDocument, TTo>)cursor;
            
            var expression = (Expression<Func<TDocument, TTo>>)mapper.ConfigurationProvider
                .ExpressionBuilder
                .GetMapExpression(typeof(TDocument), typeof(TTo), null, new System.Reflection.MemberInfo[0])[0];

            return cursor.Project<TTo>(new ProjectionDefinitionBuilder<TDocument>().Expression<TTo>(expression));
        }

        /// <summary>
        /// Projects result from  the <see cref="IFindFluent{TDocument, TProjection}"/> cursor
        /// </summary>
        /// <typeparam name="TDocument">Document type</typeparam>
        /// <typeparam name="TTo">Result type</typeparam>
        /// <param name="cursor">Mongo cursor</param>
        /// <param name="mapper">Auto mapper</param>
        /// <returns>Project result</returns>
        public static IFindFluent<TDocument, TTo> Project<TDocument, TTo>(this IFindFluent<TDocument, TDocument> cursor, IMapper mapper)
        {
            return cursor.Project<TDocument, TDocument, TTo>(mapper);
        }

        /// <summary>
        /// Projects result from  the <see cref="IMongoCollection{TDocument}"/> collection
        /// </summary>
        /// <typeparam name="TDocument">Document type</typeparam>
        /// <typeparam name="TTo">Result type</typeparam>
        /// <param name="collection">Mongo collection</param>
        /// <param name="mapper">Auto mapper</param>
        /// <returns>Project result</returns>
        public static IFindFluent<TDocument, TTo> Project<TDocument, TTo>(this IMongoCollection<TDocument> collection, IMapper mapper)
        {
            return collection
                .Find(Builders<TDocument>.Filter.Empty)
                .Project<TDocument, TTo>(mapper);
        }

        /// <summary>
        /// Projects result from  the <see cref="IQueryable{TDocument}"/> cursor
        /// If document type and destination type are same, return cursor back with out any operations
        /// </summary>
        /// <typeparam name="TDocument">Document type</typeparam>
        /// <typeparam name="TTo">Result type</typeparam>
        /// <param name="queryable">Queryabler</param>
        /// <param name="mapper">Auto mapper</param>
        /// <returns>Project result</returns>
        public static IQueryable<TTo> Project<TDocument, TTo>(this IQueryable<TDocument> queryable, IMapper mapper)
        {
            if (typeof(TDocument) == typeof(TTo)) return (IQueryable<TTo>)queryable;
            
            var expression = (Expression<Func<TDocument, TTo>>)mapper.ConfigurationProvider
                .ExpressionBuilder
                .GetMapExpression(typeof(TDocument), typeof(TTo), null, new System.Reflection.MemberInfo[0])[0];

            return queryable.Select(expression);
        }

        /// <summary>
        /// Gets filter for getting blog posts
        /// </summary>
        /// <param name="includeNotPublished">Indicates whether to include posts with date larger than now</param>
        /// <param name="additionalFilter">If not null, it will be applied as AND filter</param>
        /// <returns>Blog posts filters</returns>
        public static FilterDefinition<Post> GetPostsFilter(bool includeNotPublished = false, FilterDefinition<Post> additionalFilter = null)
        {
            var builder = Builders<Post>.Filter;
            var filter = builder.Empty;
            if (!includeNotPublished) filter &= builder.Lte(p => p.PublishDate, DateTime.UtcNow);
            if (additionalFilter != null) filter &= additionalFilter;

            return filter;
        }

        /// <summary>
        /// Gets filter for getting pages
        /// </summary>
        /// <param name="includeNotPublished">Indicates whether to include hidden pages</param>
        /// <param name="additionalFilter">If not null, it will be applied as AND filter</param>
        /// <returns>Pages filters</returns>
        public static FilterDefinition<Page> GetPagesFilter(bool includeNotPublished = false, FilterDefinition<Page> additionalFilter = null)
        {
            var builder = Builders<Page>.Filter;
            var filter = builder.Empty;
            if (!includeNotPublished) filter &= builder.Eq(p => p.UnPublished, false);
            if (additionalFilter != null) filter &= additionalFilter;

            return filter;
        }


        /// <summary>
        /// Gets blog posts
        /// </summary>
        /// <param name="collection">Blog posts collection</param>
        /// <param name="includeNotPublished">Indicates whether to include posts with date larger than now</param>
        /// <param name="additionalFilter">If not null, it will be applied as AND filter</param>
        /// <returns>Blog posts</returns>
        public static IFindFluent<Post, Post> GetPosts(
            this IMongoCollection<Post> collection, 
            bool includeNotPublished,
            FilterDefinition<Post> additionalFilter)
        {
            return collection.Find(GetPostsFilter(includeNotPublished, additionalFilter));
        }

        /// <summary>
        /// Gets blog posts
        /// </summary>
        /// <param name="collection">Blog posts collection</param>
        /// <param name="includeNotPublished">Indicates whether to include posts with date larger than now</param>
        /// <param name="additionalFilter">If not null, it will be applied as AND filter</param>
        /// <returns>Blog posts</returns>
        public static IFindFluent<Post, Post> GetPosts(
            this IMongoCollection<Post> collection, 
            bool includeNotPublished = false,
            Expression<Func<Post, bool>> additionalFilter = null)
        {
            return GetPosts(
                collection, 
                includeNotPublished, 
                additionalFilter != null ? Builders<Post>.Filter.Where(additionalFilter) : null);
        }

        /// <summary>
        /// Projects result from  the <see cref="IQueryable{TDocument}"/> cursor
        /// If document type and destination type are same, return cursor back with out any operations
        /// </summary>
        /// <typeparam name="TDocument">Document type</typeparam>
        /// <param name="queryable">Queryabler</param>
        /// <param name="filter">Filter</param>
        /// <returns>Project result</returns>
        public static IQueryable<TDocument> Filter<TDocument>(this IQueryable<TDocument> queryable, IDictionary<string, string> filter)
        {            
            if (filter.IsNullOrEmpty()) return queryable;
            
            Expression expression = null;

            var parameterExp = Expression.Parameter(typeof(TDocument), "item");

            foreach (var pair in filter)
            {
                var property = (typeof (TDocument)).GetProperties().FirstOrDefault(p => p.Name.ToLowerInvariant() == pair.Key.ToLowerInvariant());
                var propertyType = property.PropertyType;
                if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    propertyType = propertyType.GetGenericArguments()[0];
                }

                var selectExpression = pair.Key.GetPropertyExpression<TDocument>(parameterExp);
                Expression filterExpression;

                if (propertyType == typeof(bool))
                {
                    var expectedValue = bool.Parse(pair.Value);
                    filterExpression = Expression.Equal(selectExpression, Expression.Constant(expectedValue));
                }
                else if (propertyType == typeof(string) || propertyType.IsPrimitive)
                {
                    if (pair.Value.IsNullOrEmpty())
                    {
                        filterExpression = Expression.Equal(selectExpression, Expression.Constant(null));
                    }
                    else
                    {
                        var method = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                        var value = pair.Value.ToLower();

                        var valueExpression = Expression.Constant(value, typeof(string));

                        Expression selectionExpression = selectExpression;
                        if (propertyType != typeof(string))
                        {
                            selectionExpression = Expression.Call(selectExpression, typeof(object).GetMethod(nameof(object.ToString)));
                        }

                        selectionExpression = Expression.Call(selectExpression, typeof(string).GetMethod(nameof(string.ToLower), new Type[0]));

                        var containsExpression = Expression.Call(selectionExpression, method, valueExpression);
                        filterExpression = Expression.Equal(containsExpression, Expression.Constant(true));
                    }
                }
                else if (propertyType.IsEnum)
                {
                    object value;
                    if (int.TryParse(pair.Value, out var intValue))
                    {
                        value = Enum.ToObject(propertyType, intValue);
                    }
                    else if (!Enum.TryParse(propertyType, pair.Value, true, out value))
                    {
                        throw new InvalidOperationException($"Can't convert '{pair.Value} into enum {propertyType.FullName}");
                    }
                    
                    filterExpression = Expression.Equal(selectExpression, Expression.Convert(Expression.Constant(value, propertyType), property.PropertyType));
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported type: {propertyType.FullName}");
                }

                expression = expression == null ? filterExpression : Expression.And(expression, filterExpression);
            }

            if (expression == null) return queryable;
            var resultExpression = Expression.Lambda<Func<TDocument, bool>>(expression, parameterExp);

            return queryable.Where(resultExpression);
        }
    }
}

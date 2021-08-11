using AutoMapper;
using MongoDB.Driver;
using Realtorist.DataAccess.Abstractions;
using Realtorist.DataAccess.Implementations.Mongo.Settings;
using Realtorist.Models.Blog;
using Realtorist.Models.Enums;
using Realtorist.Models.Helpers;
using Realtorist.Models.Pagination;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Realtorist.DataAccess.Implementations.Mongo.DataAccess
{
    public class BlogDataAccess : IBlogDataAccess
    {
        private readonly IMongoCollection<Post> _postsCollection;
        private readonly IMapper _mapper;

        public BlogDataAccess(IDatabaseSettings databaseSettings, IMapper mapper)
        {
            if (databaseSettings == null) throw new ArgumentNullException(nameof(databaseSettings));

            var client = new MongoClient(databaseSettings.ConnectionString);
            var database = client.GetDatabase(databaseSettings.DatabaseName);

            _postsCollection = database.GetCollection<Post>(nameof(Post) + "s");
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<Guid> AddCommentAsync(Guid postId, Comment comment)
        {
            if (comment is null) throw new ArgumentNullException(nameof(comment));

            var update = new UpdateDefinitionBuilder<Post>().AddToSet(post => post.Comments, comment);
            await _postsCollection.UpdateOneAsync(post => post.Id == postId, update);
            return comment.Id;
        }

        public async Task<Guid> AddPostAsync(PostUpdateModel blogPost)
        {
            if (blogPost is null) throw new ArgumentNullException(nameof(blogPost));
            var post = _mapper.Map<Post>(blogPost);

            await _postsCollection.InsertOneAsync(post);
            return post.Id;
        }

        public async Task<Dictionary<string, int>> GetCategoriesAsync(bool includeNotPublished = false)
        {
            var aggregate = _postsCollection.Aggregate();
            if (!includeNotPublished)
            {
                aggregate = aggregate.Match(post => post.PublishDate <= DateTime.UtcNow);
            }

            var categories = await aggregate
                .Group(post => post.Category, x => new { x.Key, Count = x.Count() })
                .ToListAsync();
            return categories.ToDictionary(x => x.Key, x => x.Count);
        }

        public async Task<PaginationResult<Post>> GetCategoryPostsAsync(PaginationRequest request, string category, bool includeNotPublished = false)
        {
            return await GetCategoryPostsAsync<Post>(request, category, includeNotPublished);
        }

        public async Task<PaginationResult<T>> GetCategoryPostsAsync<T>(PaginationRequest request, string category, bool includeNotPublished = false)
        {
            return await _postsCollection
                .GetPosts(includeNotPublished, p => p.Category == category)
                .Project<Post, T>(_mapper)
                .GetPaginationResultAsync(request, post => post.PublishDate, SortByOrder.Desc);
        }

        public async Task<Post> GetPostAsync(Guid postId)
        {
            return await _postsCollection.Find(post => post.Id == postId).FirstAsync();
        }

        public async Task<Post> GetPostAsync(string link)
        {
            return await _postsCollection.Find(post => post.Link == link).FirstAsync();
        }

        public async Task<List<Post>> GetPostsAsync(bool includeNotPublished = false)
        {
            return await GetPostsAsync<Post>(includeNotPublished);
        }

        public async Task<List<T>> GetPostsAsync<T>(bool includeNotPublished = false)
        {
            return await _postsCollection
                .GetPosts(includeNotPublished)
                .SortByDescending(post => post.PublishDate)
                .Project<Post, T>(_mapper)
                .ToListAsync();
        }

        public async Task<PaginationResult<Post>> GetPostsAsync(PaginationRequest request, bool includeNotPublished = false)
        {
            return await GetPostsAsync<Post>(request, includeNotPublished);
        }

        public async Task<PaginationResult<T>> GetPostsAsync<T>(PaginationRequest request, bool includeNotPublished = false)
        {
            return await _postsCollection.GetPosts(includeNotPublished)
                .Project<Post, T>(_mapper)
                .GetPaginationResultAsync(request, post => post.PublishDate, SortByOrder.Desc);
        }

        public async Task<PaginationResult<Post>> GetPostsByTagAsync(PaginationRequest request, string tag, bool includeNotPublished = false)
        {
            return await GetPostsByTagAsync<Post>(request, tag, includeNotPublished);
        }

        public async Task<PaginationResult<T>> GetPostsByTagAsync<T>(PaginationRequest request, string tag, bool includeNotPublished = false)
        {
            var cursor = _postsCollection.GetPosts(includeNotPublished, post => post.Tags.Contains(tag));
            return await cursor
                .Project<Post, T>(_mapper)
                .GetPaginationResultAsync(request, post => post.PublishDate, SortByOrder.Desc);
        }

        public async Task<PaginationResult<Post>> SearchPostsAsync(PaginationRequest request, string query, bool includeNotPublished = false)
        {
            return await SearchPostsAsync<Post>(request, query, includeNotPublished);
        }

        public async Task<PaginationResult<T>> SearchPostsAsync<T>(PaginationRequest request, string query, bool includeNotPublished = false)
        {
            var filter = Builders<Post>.Filter.Text(query);
            var cursor = _postsCollection.Find(filter);

            return await cursor
                .Project<Post, T>(_mapper)
                .GetPaginationResultAsync(request, post => post.PublishDate, SortByOrder.Desc);
        }

        public async Task<List<string>> GetTagsAsync(bool includeNotPublished = false)
        {
            return await _postsCollection
                .Distinct(new StringFieldDefinition<Post, string>(nameof(Post.Tags)), MongoHelpers.GetPostsFilter(includeNotPublished))
                .ToListAsync();
        }

        public async Task RemoveCommentAsync(Guid postId, Guid commentId)
        {
            var update = new UpdateDefinitionBuilder<Post>().PullFilter(post => post.Comments, builder => builder.Id == commentId);
            await _postsCollection.UpdateOneAsync(post => post.Id == postId, update);
        }

        public async Task RemovePostAsync(Guid postId)
        {
            await _postsCollection.DeleteOneAsync(post => post.Id == postId);
        }

        public async Task UpdatePostAsync(Guid postId, PostUpdateModel post)
        {
            if (post is null) throw new ArgumentNullException(nameof(post));
            var update = new UpdateDefinitionBuilder<Post>()
                .Set(p => p.Image, post.Image)
                .Set(p => p.Title, post.Title)
                .Set(p => p.Link, post.Link)
                .Set(p => p.SubTitle, post.SubTitle)
                .Set(p => p.PublishDate, post.PublishDate)
                .Set(p => p.Text, post.Text)
                .Set(p => p.Tags, post.Tags)
                .Set(p => p.Category, post.Category);

            await _postsCollection.UpdateOneAsync(p => p.Id == postId, update);
        }

        public async Task IncrementPostViews(Guid postId)
        {
            var update = new UpdateDefinitionBuilder<Post>().Inc(p => p.Views, 1);

            await _postsCollection.UpdateOneAsync(p => p.Id == postId, update);
        }

        public async Task<PaginationResult<CommentListModel>> GetPostCommentsAsync(Guid postId, PaginationRequest request)
        {
            return await _postsCollection
                .AsQueryable()
                .Where(post => post.Id == postId)
                .SelectMany(
                    post => post.Comments,
                    (post, comment) => new CommentListModel {
                        PostId = post.Id,
                        PostTitle = post.Title,
                        Id = comment.Id,
                        Date = comment.Date,
                        Email = comment.Email,
                        Message = comment.Message,
                        Name = comment.Name
                    })
                .GetPaginationResultAsync(request);
        }

        public async Task<PaginationResult<CommentListModel>> GetCommentsAsync(PaginationRequest request)
        {
            return await _postsCollection
                .AsQueryable()
                .SelectMany(
                    post => post.Comments,
                    (post, comment) => new CommentListModel {
                        PostId = post.Id,
                        PostTitle = post.Title,
                        Id = comment.Id,
                        Date = comment.Date,
                        Email = comment.Email,
                        Message = comment.Message,
                        Name = comment.Name
                    })
                .GetPaginationResultAsync(request);
        }

        public async Task<bool> IsLinkUsed(string link, IEnumerable<Guid> idsToExclude = null)
        {
            if (idsToExclude.IsNullOrEmpty()) idsToExclude = new Guid[0];
            return await _postsCollection.Find(p => p.Link == link && !idsToExclude.Contains(p.Id)).AnyAsync();
        }
    }
}

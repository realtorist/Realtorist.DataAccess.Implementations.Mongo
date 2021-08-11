using AutoMapper;
using MongoDB.Driver;
using Realtorist.DataAccess.Abstractions;
using Realtorist.DataAccess.Implementations.Mongo.Settings;
using Realtorist.Models.Blog;
using Realtorist.Models.Enums;
using Realtorist.Models.Helpers;
using Realtorist.Models.Page;
using Realtorist.Models.Pagination;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Realtorist.DataAccess.Implementations.Mongo.DataAccess
{
    public class PagesDataAccess : IPagesDataAccess
    {
        private readonly IMongoCollection<Page> _pagesCollection;
        private readonly IMapper _mapper;

        public PagesDataAccess(IDatabaseSettings databaseSettings, IMapper mapper)
        {
            if (databaseSettings == null) throw new ArgumentNullException(nameof(databaseSettings));

            var client = new MongoClient(databaseSettings.ConnectionString);
            var database = client.GetDatabase(databaseSettings.DatabaseName);

            _pagesCollection = database.GetCollection<Page>(nameof(Page) + "s");
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<Guid> AddPageAsync(PageUpdateModel page)
        {
            if (page is null) throw new ArgumentNullException(nameof(page));
            var p = _mapper.Map<Page>(page);

            await _pagesCollection.InsertOneAsync(p);
            return p.Id;
        }

        public async Task<Page> GetPageAsync(Guid pageId)
        {
            return await _pagesCollection.Find(p => p.Id == pageId).FirstAsync();
        }

        public async Task<Page> GetPageAsync(string link)
        {
            return await _pagesCollection.Find(p => p.Link == link).FirstAsync();
        }

        public async Task<List<Page>> GetPagesAsync(bool includeNotPublished = false)
        {
            return await _pagesCollection
                .Find(MongoHelpers.GetPagesFilter(includeNotPublished))
                .ToListAsync();
        }

        public async Task<PaginationResult<Page>> GetPagesAsync(PaginationRequest request, bool includeNotPublished = false)
        {
            return await _pagesCollection
                .Find(MongoHelpers.GetPagesFilter(includeNotPublished))
                .GetPaginationResultAsync(request);
        }

        public async Task<PaginationResult<T>> GetPagesAsync<T>(PaginationRequest request, bool includeNotPublished = false)
        {
            return await _pagesCollection
                .Find(MongoHelpers.GetPagesFilter(includeNotPublished))
                .Project<Page, T>(_mapper)
                .GetPaginationResultAsync(request);
        }

        public async Task IncrementPagetViews(Guid pageId)
        {
            var update = new UpdateDefinitionBuilder<Page>().Inc(p => p.Views, 1);

            await _pagesCollection.UpdateOneAsync(p => p.Id == pageId, update);
        }

        public async Task<bool> IsLinkUsed(string link, IEnumerable<Guid> idsToExclude = null)
        {
            if (idsToExclude.IsNullOrEmpty()) idsToExclude = new Guid[0];
            return await _pagesCollection.Find(p => p.Link == link && !idsToExclude.Contains(p.Id)).AnyAsync();
        }

        public async Task RemovePageAsync(Guid pageId)
        {
            await _pagesCollection.DeleteOneAsync(post => post.Id == pageId);
        }

        public async Task UpdatePageAsync(Guid pageId, PageUpdateModel page)
        {
            if (page is null) throw new ArgumentNullException(nameof(page));
            var update = new UpdateDefinitionBuilder<Page>()
                .Set(p => p.Title, page.Title)
                .Set(p => p.Link, page.Link)
                .Set(p => p.UnPublished, page.UnPublished)
                .Set(p => p.Components, page.Components)
                .Set(p => p.AdditionalCss, page.AdditionalCss)
                .Set(p => p.Configuration, page.Configuration)
                .Set(p => p.Keywords, page.Keywords)
                .Set(p => p.Description, page.Description);

            await _pagesCollection.UpdateOneAsync(p => p.Id == pageId, update);
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Yarn;
using Raven.Client;
using Raven.Client.Linq;
using Yarn.Specification;

namespace Yarn.Data.RavenDbProvider
{
    public class Repository : IRepository, IMetaDataProvider
    {
        private IDataContext<IDocumentSession> _context;
        private bool _waitForNonStaleResults;
        private string _prefix;

        public Repository() : this(false, null) { }

        public Repository(bool waitForNonStaleResults = false, string prefix = null)
        {
            _waitForNonStaleResults = waitForNonStaleResults;
            _prefix = prefix;
        }

        public T GetById<T, ID>(ID id) where T : class
        {
            return _context.Session.Load<T>(id.ToString());
        }

        public IEnumerable<T> GetByIdList<T, ID>(IList<ID> ids) where T : class
        {
            return _context.Session.Load<T>(ids.Select(i => i.ToString()));
        }

        public T Find<T>(Expression<Func<T, bool>> criteria) where T : class
        {
            return this.FindAll(criteria).FirstOrDefault();
        }

        public T Find<T>(ISpecification<T> criteria) where T : class
        {
            return FindAll(criteria).FirstOrDefault();
        }

        public IEnumerable<T> FindAll<T>(Expression<Func<T, bool>> criteria, int offset = 0, int limit = 0) where T : class
        {
            var results = this.All<T>().Where(criteria);
            if (offset >= 0)
            {
                results = results.Skip(offset);
            }
            if (limit > 0)
            {
                results = results.Take(limit);
            }
            return results;
        }

        public IEnumerable<T> FindAll<T>(ISpecification<T> criteria, int offset = 0, int limit = 0) where T : class
        {
            var results = criteria.Apply(this.All<T>());
            if (offset >= 0 && limit > 0)
            {
                results = results.Skip(offset).Take(limit);
            }
            return results;
        }

        public T FindOne<T>(Expression<Func<T, bool>> criteria, bool waitForNonStaleResults = false) where T : class
        {
            var foundList = this.FindAll(criteria);
            try
            {
                return foundList.SingleOrDefault();
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException("The query returned more than one result. Please refine your query.");
            }
        }

        public T Add<T>(T entity) where T : class
        {
            _context.Session.Store(entity);
            return entity;
        }

        public T Remove<T>(T entity) where T : class
        {
            _context.Session.Delete<T>(entity);
            return entity;
        }

        public T Remove<T, ID>(ID id) where T : class
        {
            var entity = GetById<T, ID>(id);
            if (entity != null)
            {
                _context.Session.Delete<T>(entity);
            }
            return entity;
        }

        public T Update<T>(T entity) where T : class
        {
            _context.Session.Store(entity);
            return entity;
        }

        public void Attach<T>(T entity) where T : class
        {
            throw new NotImplementedException();
        }

        public void Detach<T>(T entity) where T : class
        {
            _context.Session.Advanced.Evict<T>(entity);
        }

        public IQueryable<T> All<T>() where T : class
        {
            return _context.Session.Query<T>().Customize(q => CustomizeQuery(q, _waitForNonStaleResults));
        }

        public long Count<T>() where T : class
        {
            RavenQueryStatistics stats;
            _context.Session.Query<T>().Statistics(out stats);
            return stats.TotalResults;
        }

        public long Count<T>(Expression<Func<T, bool>> criteria) where T : class
        {
            RavenQueryStatistics stats;
            _context.Session.Query<T>().Where(criteria).Statistics(out stats);
            return stats.TotalResults;
        }

        public long Count<T>(ISpecification<T> criteria) where T : class
        {
            RavenQueryStatistics stats;
            ((IRavenQueryable<T>)criteria.Apply(_context.Session.Query<T>())).Statistics(out stats);
            return stats.TotalResults;
        }

        public IList<T> Execute<T>(string command, ParamList parameters) where T : class
        {
            // Execute is used to query RavenDB index
            var indexQuery = _context.Session.Query<T>(command);
            if (parameters != null && parameters.Count > 0)
            {
                // De-duplicate parameters and oraganize them into a dictionary
                var args = (Dictionary<string, object>)parameters;

                object criteria, offset, limit;

                if (args.TryGetValue("criteria", out criteria))
                {
                    if (criteria is Expression<Func<T, bool>>)
                    {
                        indexQuery = indexQuery.Where((Expression<Func<T, bool>>)criteria);
                    }
                    else if (criteria is Specification<T>)
                    {
                        indexQuery = indexQuery.Where(((Specification<T>)criteria).Predicate);
                    }
                }

                if (args.TryGetValue("offset", out offset) && offset is int && ((int)offset > 0))
                {
                    indexQuery = indexQuery.Skip((int)offset);
                }

                if (args.TryGetValue("limit", out limit) && limit is int && ((int)limit > 0))
                {
                    indexQuery = (IRavenQueryable<T>)indexQuery.Take((int)limit);
                }
            }

            return indexQuery.ToArray();
        }

        protected IDocumentSession DocumentSession
        {
            get
            {
                return ((IDataContext<IDocumentSession>)DataContext).Session;
            }
        }

        public IDataContext DataContext
        {
            get
            {
                if (_context == null)
                {
                    _context = new DataContext(_prefix);
                }
                return _context;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_context != null)
                {
                    _context.Dispose();
                    _context = null;
                }
            }
        }

        private void CustomizeQuery(IDocumentQueryCustomization p, bool waitForNonStaleResults)
        {
            if (waitForNonStaleResults)
            {
                p.WaitForNonStaleResults();
            }
        }

        #region IMetaDataProvider Members

        string[] IMetaDataProvider.GetPrimaryKey<T>()
        {
            return new[] { this.DocumentSession.Advanced.DocumentStore.Conventions.GetIdentityProperty(typeof(T)).Name };
        }

        object[] IMetaDataProvider.GetPrimaryKeyValue<T>(T entity)
        {
            return new[] { this.DocumentSession.Advanced.GetDocumentId(entity) };
        }
        
        #endregion
    }
}

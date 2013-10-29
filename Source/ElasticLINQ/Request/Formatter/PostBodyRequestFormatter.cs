﻿// Copyright (c) Tier 3 Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. 

using ElasticLinq.Request.Criteria;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace ElasticLinq.Request.Formatter
{
    /// <summary>
    /// Formats an ElasticSearchRequest into a JSON POST body to be sent
    /// to ElasticSearch for querying.
    /// </summary>
    internal class PostBodyRequestFormatter : RequestFormatter
    {
        public PostBodyRequestFormatter(ElasticConnection connection, ElasticSearchRequest searchRequest)
            : base(connection, searchRequest)
        {
        }

        protected override void CompleteSearchUri(UriBuilder builder)
        {
        }

        public string Body
        {
            get { return CreateJsonPayload().ToString(); }
        }

        private JObject CreateJsonPayload()
        {
            var root = new JObject();

            if (SearchRequest.Fields.Any())
                root.Add("fields", new JArray(SearchRequest.Fields));

            if (SearchRequest.Query != null)
                root.Add("query", BuildCriteria(SearchRequest.Query));

            if (SearchRequest.Filter != null)
                root.Add("filter", BuildCriteria(SearchRequest.Filter));

            if (SearchRequest.SortOptions.Any())
                root.Add("sort", new JArray(
                    SearchRequest.SortOptions
                        .Select(o => o.Ascending ? (object)o.Name : new JObject(new JProperty(o.Name, "desc")))
                        .ToArray()));

            if (SearchRequest.From > 0)
                root.Add("from", SearchRequest.From);

            if (SearchRequest.Size.HasValue)
                root.Add("size", SearchRequest.Size.Value);

            root.Add("timeout", Format(Connection.Timeout));

            return root;
        }

        private static JObject BuildCriteria(ICriteria criteria)
        {
            if (criteria is RangeCriteria)
                return Create((RangeCriteria)criteria);

            if (criteria is TermCriteria)
                return Create((TermCriteria)criteria);

            if (criteria is NotCriteria)
                return Create((NotCriteria)criteria);

            // Base class formatters using name property

            if (criteria is SingleFieldCriteria)
                return Create((SingleFieldCriteria)criteria);

            if (criteria is CompoundCriteria)
                return Create((CompoundCriteria)criteria);

            throw new InvalidOperationException(String.Format("Unknown filter type {0}", criteria.GetType()));
        }

        private static JObject Create(RangeCriteria criteria)
        {
            // Range filters can be combined by field
            return new JObject(new JProperty(criteria.Name, new JObject(new JProperty(criteria.Field,
                   new JObject(criteria.Specifications.Select(s => new JProperty(s.Name, s.Value)).ToList())))));
        }

        private static JObject Create(TermCriteria criteria)
        {
            // Terms filter with one item is a single term filter
            var value = criteria.Values.Count == 1 ? criteria.Values[0] : new JArray(criteria.Values.ToArray());
            return new JObject(new JProperty(criteria.Name, new JObject(new JProperty(criteria.Field, value))));
        }

        private static JObject Create(SingleFieldCriteria criteria)
        {
            return new JObject(new JProperty(criteria.Name, new JObject(new JProperty("field", criteria.Field))));
        }

        private static JObject Create(NotCriteria criteria)
        {
            return new JObject(new JProperty(criteria.Name, BuildCriteria(criteria.Criteria)));
        }

        private static JObject Create(CompoundCriteria criteria)
        {
            return criteria.Criteria.Count == 1    // A compound filter with one item can be collapsed
                ? BuildCriteria(criteria.Criteria.First())
                : new JObject(new JProperty(criteria.Name, new JArray(criteria.Criteria.Select(BuildCriteria).ToList())));
        }
    }
}
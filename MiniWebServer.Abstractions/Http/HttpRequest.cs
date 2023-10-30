﻿using Microsoft.Extensions.Logging;
using MiniWebServer.Abstractions.Http.Form;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MiniWebServer.Abstractions.Http
{
    public class HttpRequest : IHttpRequest
    {
        private static readonly IRequestForm EmptyForm = new RequestForm();


        public HttpRequest(ulong requestId, 
            HttpMethod method, 
            string url, 
            HttpRequestHeaders headers, 
            string queryString, 
            string hash, 
            HttpParameters queryParameters, 
            string[] segments, 
            HttpCookies cookies, 
            Pipe bodyPipeline, 
            long contentLength, 
            string contentType)
        {
            if (queryParameters is null)
            {
                throw new ArgumentNullException(nameof(queryParameters));
            }

            RequestId = requestId;
            Method = method ?? throw new ArgumentNullException(nameof(method));
            Url = url ?? throw new ArgumentNullException(nameof(url));
            Headers = headers ?? throw new ArgumentNullException(nameof(headers));
            QueryString = queryString ?? string.Empty;
            Hash = hash ?? string.Empty;
            QueryParameters = queryParameters ?? throw new ArgumentNullException(nameof(queryParameters));
            Segments = segments ?? throw new ArgumentNullException(nameof(segments));
            Cookies = cookies ?? throw new ArgumentNullException(nameof(cookies));
            BodyPipeline = bodyPipeline;
            ContentLength = contentLength;
            ContentType = contentType;

            BodyManager = new RequestBodyManager(BodyPipeline.Reader);
        }

        public Pipe BodyPipeline { get; }
        public HttpCookies Cookies { get; }
        public long ContentLength { get; }
        public string ContentType { get; }
        public string Hash { get; }
        public ulong RequestId { get; }
        public HttpMethod Method { get; }
        public HttpRequestHeaders Headers { get; }
        public HttpParameters QueryParameters { get; }
        public string[] Segments { get; }
        public string QueryString { get; }
        public string Url { get; }

        private IRequestForm? form; // a cache object for lazy loading 

        public bool KeepAliveRequested { get 
            {
                return !"close".Equals(Headers.Connection, StringComparison.InvariantCultureIgnoreCase); // it is keep-alive by default
            } 
        }

        public IRequestBodyManager BodyManager { get; }

        public async Task<IRequestForm> ReadFormAsync(ILoggerFactory? loggerFactory = null, CancellationToken cancellationToken = default)
        {
            if (form != null)
                return form;
            else
            {
                if (ContentType == null)
                    return EmptyForm;

                var reader = BodyManager.GetReader();

                if (reader == null)
                {
                    return EmptyForm;
                }

                var formReaderFactory = new DefaultFormReaderFactory(loggerFactory);

                var formReader = formReaderFactory.CreateFormReader(ContentType, ContentLength) ?? throw new InvalidHttpStreamException("Not supported content type");
                var readform = await formReader.ReadAsync(reader, cancellationToken);
                form =  readform ?? throw new InvalidHttpStreamException("Error reading form data");

                return form;
            }
        }
    }
}

// ***********************************************************************
// Assembly         : EveLib.ZKillboard
// Author           : Lars Kristian
// Created          : 06-18-2014
//
// Last Modified By : Lars Kristian
// Last Modified On : 11-02-2014
// ***********************************************************************
// <copyright file="ZkbRequestHandler.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace ThunderED.Zkb {
    /// <summary>
    ///     Class ZkbRequestHandler.
    /// </summary>
    public class ZkbRequestHandler : IRequestHandler {
        /// <summary>
        ///     The _trace
        /// </summary>
        private readonly TraceSource _trace = new TraceSource("EveLib", SourceLevels.All);

        /// <summary>
        ///     Initializes a new instance of the <see cref="ZkbRequestHandler" /> class.
        /// </summary>
        /// <param name="serializer">The serializer.</param>
        /// <param name="cache">The cache.</param>
        public ZkbRequestHandler(ISerializer serializer) {
            Serializer = serializer;
        }

        /// <summary>
        ///     Gets or sets the serializer used to deserialize data
        /// </summary>
        /// <value>The serializer.</value>
        public ISerializer Serializer { get; set; }


        /// <summary>
        ///     Request as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="uri">The URI.</param>
        /// <returns>Task&lt;T&gt;.</returns>
        public async Task<T> RequestAsync<T>(Uri uri)
        {
            _trace.TraceEvent(TraceEventType.Start, 0, "ZkbRequestHandler.RequestAsync(): {0}", uri);
            string data = null;

            DateTime cacheTime;
            int requestCount, maxRequests;
            HttpWebRequest request = HttpRequestHelper.CreateRequest(uri);

            using (
                HttpWebResponse response = await HttpRequestHelper.GetResponseAsync(request).ConfigureAwait(false)) {
                data = await HttpRequestHelper.GetResponseContentAsync(response).ConfigureAwait(false);
                DateTime.TryParse(response.GetResponseHeader("Expires"), out cacheTime);                
                int.TryParse(response.GetResponseHeader("X-Bin-Request-Count"), out requestCount);
                int.TryParse(response.GetResponseHeader("X-Bin-Max-Requests"), out maxRequests);
            }
            _trace.TraceEvent(TraceEventType.Stop, 0, "ZkbRequestHandler.RequestAsync()", uri);
            if (data == "[]") return default(T);

            var result = Serializer.Deserialize<T>(data);
            var zkbResponse = result as ZkbResponse;
            if (zkbResponse != null) {
                zkbResponse.RequestCount = requestCount;
                zkbResponse.MaxRequests = maxRequests;
            }
            return result;
        }

    }
}
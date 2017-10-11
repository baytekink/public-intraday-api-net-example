﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace NPS.public_intraday_api_example.Utilities
{
    public class HttpClientFactory
    {
        /// <summary>
        /// Creates a HttpClient without any default request headers.
        /// </summary>
        /// <returns>HttpClient instance without any default headers</returns>
        public static HttpClient Create()
        {
            return new HttpClient(new HttpClientHandler() {UseDefaultCredentials = false});
        }


        /// <summary>
        /// Creates a HttpClient with Basic-AUTH header value with given user name and password
        /// </summary>
        /// <param name="password">password used in Basic authentication</param>
        /// <param name="username">username used in Basic authentication</param>
        /// <returns>HttpClient with default request headers containting Basic-HTTP-authentication.</returns>
        public static HttpClient CreateWithBasicAuth(string password, string username)
        {
            var client = new HttpClient(new HttpClientHandler() {UseDefaultCredentials = false});
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    $"{username}:{password}")));

            return client;
        }
    }
}
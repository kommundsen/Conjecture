// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

using Conjecture.Core;

using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace Conjecture.OpenApi;

/// <summary>Extension methods on <see cref="Strategy"/> for OpenAPI-driven generation.</summary>
public static class OpenApiStrategyExtensions
{
    private static readonly HttpClient SharedHttpClient = new();

    extension(Strategy)
    {
        /// <summary>Loads an OpenAPI document from <paramref name="filePath"/> and returns a <see cref="OpenApiDocument"/> for building strategies.</summary>
        public static async Task<OpenApiDocument> FromOpenApi(string filePath)
        {
            using FileStream stream = File.OpenRead(filePath);
            OpenApiStreamReader reader = new();
            Microsoft.OpenApi.Models.OpenApiDocument raw = (await reader.ReadAsync(stream)).OpenApiDocument;
            return new(raw);
        }

        /// <summary>Loads an OpenAPI document from <paramref name="file"/> and returns a <see cref="OpenApiDocument"/> for building strategies.</summary>
        public static async Task<OpenApiDocument> FromOpenApi(FileInfo file)
        {
            using FileStream stream = file.OpenRead();
            OpenApiStreamReader reader = new();
            Microsoft.OpenApi.Models.OpenApiDocument raw = (await reader.ReadAsync(stream)).OpenApiDocument;
            return new(raw);
        }

        /// <summary>Loads an OpenAPI document from <paramref name="url"/> and returns a <see cref="OpenApiDocument"/> for building strategies.</summary>
        public static async Task<OpenApiDocument> FromOpenApi(Uri url)
        {
            using Stream stream = url.IsFile
                ? File.OpenRead(url.LocalPath)
                : await SharedHttpClient.GetStreamAsync(url);
            OpenApiStreamReader reader = new();
            Microsoft.OpenApi.Models.OpenApiDocument raw = (await reader.ReadAsync(stream)).OpenApiDocument;
            return new(raw);
        }
    }
}
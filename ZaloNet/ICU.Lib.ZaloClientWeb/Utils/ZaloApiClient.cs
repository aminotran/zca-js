using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ICU.Lib.ZaloClientWeb.Models;

namespace ICU.Lib.ZaloClientWeb.Utils;

/// <summary>
/// Injectable API client for making Zalo API calls.
/// Wraps the static <see cref="ApiMethods"/> with an instance-based, DI-friendly approach.
/// </summary>
public class ZaloApiClient
{
    private readonly ZaloContext _context;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of <see cref="ZaloApiClient"/>.
    /// </summary>
    /// <param name="context">The Zalo context containing session and configuration data.</param>
    /// <param name="httpClient">The HTTP client used for API calls.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="httpClient"/> is null.</exception>
    public ZaloApiClient(ZaloContext context, HttpClient httpClient)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Exposes the underlying <see cref="ZaloContext"/> for advanced scenarios.
    /// </summary>
    public ZaloContext Context => _context;

    /// <summary>
    /// Exposes the underlying <see cref="HttpClient"/> for advanced scenarios.
    /// </summary>
    public HttpClient HttpClient => _httpClient;

    public Task<ZaloApiResponse<JsonElement>> CallGetApiAsync(string endpoint, object? parameters = null)
        => ApiMethods.CallGetApiAsync(_context, _httpClient, endpoint, parameters);

    public Task<ZaloApiResponse<JsonElement>> CallEncryptedGetApiAsync(string endpoint, object? parameters = null)
        => ApiMethods.CallEncryptedGetApiAsync(_context, _httpClient, endpoint, parameters);

    public Task<ZaloApiResponse<JsonElement>> CallPostApiAsync(string endpoint, object? data = null)
        => ApiMethods.CallPostApiAsync(_context, _httpClient, endpoint, data);

    public Task<ZaloApiResponse<JsonElement>> CallEncryptedPostApiAsync(string endpoint, object? data = null)
        => ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, endpoint, data);

    public Task<ZaloApiResponse<JsonElement>> CallCustomApiAsync(string method, string endpoint, object? data = null, bool isGet = true)
        => ApiMethods.CallCustomApiAsync(_context, _httpClient, method, endpoint, data, isGet);
}
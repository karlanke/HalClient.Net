using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HalClient.Net.Parser;

namespace HalClient.Net
{
	internal class HalHttpClient : IHalHttpClient
	{
		private const string ApplicationHalJson = "application/hal+json";
		private readonly IHalJsonParser _parser;
		private HttpClient _httpClient;

		internal HalHttpClient(IHalJsonParser parser, HttpClient httpClient)
		{
			if (parser == null)
				throw new ArgumentNullException(nameof(parser));

			if (httpClient == null)
				throw new ArgumentNullException(nameof(httpClient));

			_parser = parser;
			_httpClient = httpClient;

			HttpClient = new NonParsingHttpClient(httpClient);
			Config = new HalHttpClientConfiguration(httpClient);

			Config.Headers.Accept.Clear();
			Config.Headers.Add("Accept", ApplicationHalJson);
		}

		public IHalHttpClientConfiguration Config { get; }
		
		public async Task<IRootResourceObject> PostAsync<T>(Uri uri, T data)
		{
			var response = await HttpClient.PostAsJsonAsync(uri, data);

			return await ProcessResponseMessage(response);
		}

		public async Task<IRootResourceObject> PutAsync<T>(Uri uri, T data)
		{
			var response = await HttpClient.PutAsJsonAsync(uri, data);

			return await ProcessResponseMessage(response);
		}

		public async Task<IRootResourceObject> GetAsync(Uri uri)
		{
			var response = await HttpClient.GetAsync(uri);

			return await ProcessResponseMessage(response);
		}

		public async Task<IRootResourceObject> DeleteAsync(Uri uri)
		{
			var response = await HttpClient.DeleteAsync(uri);

			return await ProcessResponseMessage(response);
		}

		public async Task<IRootResourceObject> SendAsync(HttpRequestMessage request)
		{
			var response = await HttpClient.SendAsync(request);

			return await ProcessResponseMessage(response);
		}

		public IRootResourceObject CachedApiRootResource { get; set; }

		public INonParsingHttpClient HttpClient { get; }

		private async Task<IRootResourceObject> ProcessResponseMessage(HttpResponseMessage response)
		{
			if ((response.StatusCode == HttpStatusCode.Redirect) ||
				(response.StatusCode == HttpStatusCode.SeeOther) ||
				(response.StatusCode == HttpStatusCode.RedirectMethod))
				return await GetAsync(response.Headers.Location);

			string mediatype = null;
			var isHalResponse = false;

			if (response.Content.Headers.ContentType != null)
			{
				mediatype = response.Content.Headers.ContentType.MediaType;
				isHalResponse = mediatype.Equals(ApplicationHalJson, StringComparison.OrdinalIgnoreCase);
			}

			if (response.IsSuccessStatusCode)
			{
				if (response.StatusCode == HttpStatusCode.NoContent)
					return new RootResourceObject();

				if (string.IsNullOrEmpty(mediatype))
					throw new NotSupportedException("The response is missing the 'Content-Type' header");

				if (!isHalResponse)
					throw new NotSupportedException("The response contains an unsupported 'Content-Type' header value: " + mediatype);

				return await ParseContentAsync(response);
			}

			if (!isHalResponse)
				throw new HalHttpRequestException(response.StatusCode, response.ReasonPhrase);

			var resource = await ParseContentAsync(response);

			throw new HalHttpRequestException(response.StatusCode, response.ReasonPhrase, resource);
		}

		private async Task<IRootResourceObject> ParseContentAsync(HttpResponseMessage response)
		{
			var json = await response.Content.ReadAsStringAsync();
			var result = _parser.Parse(json);

			return new RootResourceObject(result);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposing)
				return;

			if (_httpClient == null)
				return;

			_httpClient.Dispose();
			_httpClient = null;
		}
	}
}
﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Mollie.Api.Extensions;
using Mollie.Api.Framework.Factories;
using Mollie.Api.JsonConverters;
using Mollie.Api.Models.Issuer;
using Mollie.Api.Models.List;
using Mollie.Api.Models.Payment;
using Mollie.Api.Models.Payment.Request;
using Mollie.Api.Models.Payment.Response;
using Mollie.Api.Models.PaymentMethod;
using Mollie.Api.Models.Refund;
using Newtonsoft.Json;
using RestSharp;

namespace Mollie.Api.Client {
    public class MollieClient {
        public const string ApiEndPoint = "https://api.mollie.nl";
        public const string ApiVersion = "v1";

        private readonly string _apiKey;
        private readonly RestClient _restClient;
        private readonly JsonSerializerSettings _defaultJsonSerializerSettings;

        public MollieClient(string apiKey) {
            if (string.IsNullOrEmpty(apiKey)) {
                throw new ArgumentException("Mollie API key cannot be empty");
            }

            this._apiKey = apiKey;
            this._defaultJsonSerializerSettings = this.CreateDefaultJsonSerializerSettings();
            this._restClient = this.CreateRestClient();
        }

        public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest paymentRequest) {
            return await this.PostAsync<PaymentResponse>("payments", paymentRequest);
        }

        public async Task<ListResponse<PaymentResponse>> GetPaymentListAsync(int? offset = null, int? count = null) {
            return await this.GetListAsync<ListResponse<PaymentResponse>>("payments", offset, count);
        }

        public async Task<PaymentResponse> GetPaymentAsync(string paymentId) {
            return await this.GetAsync<PaymentResponse>($"payments/{paymentId}");
        }

        public async Task<ListResponse<PaymentMethodResponse>> GetPaymentMethodListAsync(int? offset = null, int? count = null) {
            return await this.GetListAsync<ListResponse<PaymentMethodResponse>>("methods", offset, count);
        }

        public async Task<PaymentMethodResponse> GetPaymentMethodAsync(PaymentMethod paymentMethod) {
            return await this.GetAsync<PaymentMethodResponse>($"methods/{paymentMethod.ToString().ToLower()}");
        }

        public async Task<ListResponse<IssuerResponse>> GetIssuerListAsync(int? offset = null, int? count = null) {
            return await this.GetListAsync<ListResponse<IssuerResponse>>("issuers", offset, count);
        }

        public async Task<IssuerResponse> GetIssuerAsync(string issuerId) {
            return await this.GetAsync<IssuerResponse>($"issuers/{issuerId}");
        }

        public async Task<RefundResponse> CreateRefundAsync(string paymentId, decimal? amount = null) {
            return await this.PostAsync<RefundResponse>($"payments/{paymentId}/refunds", new { amount = amount });
        }

        public async Task<ListResponse<RefundResponse>> GetRefundListAsync(string paymentId, int? offset = null, int? count = null) {
            return await this.GetListAsync<ListResponse<RefundResponse>>($"payments/{paymentId}/refunds", offset, count);
        }

        public async Task<RefundResponse> GetRefundAsync(string paymentId, string refundId) {
            return await this.GetAsync<RefundResponse>($"payments/{paymentId}/refunds/{refundId}");
        }

        public async Task CancelRefundAsync(string paymentId, string refundId) {
            await this.DeleteAsync($"payments/{paymentId}/refunds/{refundId}");
        }

        private async Task<T> GetAsync<T>(string relativeUri) {
            RestRequest request = new RestRequest(relativeUri, Method.GET);
            return await this.ExecuteRequestAsync<T>(request);
        }

        private async Task<T> GetListAsync<T>(string relativeUri, int? offset, int? count) {
            RestRequest request = new RestRequest(relativeUri, Method.GET);
            if (offset.HasValue) {
                request.AddParameter("offset", offset);
            }
            if (count.HasValue) {
                request.AddParameter("count", count);
            }

            return await this.ExecuteRequestAsync<T>(request);
        }

        private async Task<T> PostAsync<T>(string relativeUri, object data) {
            RestRequest request = new RestRequest(relativeUri, Method.POST);
            request.AddParameter(String.Empty, JsonConvertExtensions.SerializeObjectCamelCase(data), ParameterType.RequestBody);

            return await this.ExecuteRequestAsync<T>(request);
        }

        private async Task DeleteAsync(string relativeUri) {
            RestRequest request = new RestRequest(relativeUri, Method.DELETE);
            await this.ExecuteRequestAsync<object>(request);
        }

        private async Task<T> ExecuteRequestAsync<T>(IRestRequest request) {
            IRestResponse response = await this._restClient.ExecuteTaskAsync(request);
            return this.ProcessHttpResponseMessage<T>(response);
        }

        private T ProcessHttpResponseMessage<T>(IRestResponse response) {
            if (response.IsSuccessful()) {
                return JsonConvert.DeserializeObject<T>(response.Content, this._defaultJsonSerializerSettings);
            }
            else {
                switch (response.StatusCode) {
                    case HttpStatusCode.BadRequest:
                    case HttpStatusCode.Unauthorized:
                    case HttpStatusCode.Forbidden:
                    case HttpStatusCode.NotFound:
                    case HttpStatusCode.MethodNotAllowed:
                    case HttpStatusCode.UnsupportedMediaType:
                    case (HttpStatusCode)422: // Unprocessable entity
                        throw new MollieApiException(response.Content);
                    default:
                        throw new HttpRequestException($"Unknown http exception occured with status code: {(int)response.StatusCode}.");
                }
            }
        }

        /// <summary>
        /// Creates a new rest client for the Mollie API
        /// </summary>
        private RestClient CreateRestClient() {
            RestClient restClient = new RestClient();
            restClient.BaseUrl = this.GetBaseAddress();
            restClient.AddDefaultHeader("Content-Type", "application/json");
            restClient.AddDefaultParameter("Authorization", $"Bearer {this._apiKey}", ParameterType.HttpHeader);

            return restClient;
        }

        /// <summary>
        /// Returns the base address of the Mollie API
        /// </summary>
        /// <returns></returns>
        private Uri GetBaseAddress() => new Uri(ApiEndPoint + "/" + ApiVersion + "/");

        /// <summary>
        /// Creates the default Json serial settings for the JSON.NET parsing.
        /// </summary>
        /// <returns></returns>
        private JsonSerializerSettings CreateDefaultJsonSerializerSettings() {
            return new JsonSerializerSettings {
                NullValueHandling = NullValueHandling.Ignore,
                Converters = new List<JsonConverter>() {
                    // Add a special converter for payment responses, because we need to create specific classes based on the payment method
                    new PaymentResponseConverter(new PaymentResponseFactory())
                }
            };
        }
    }
}

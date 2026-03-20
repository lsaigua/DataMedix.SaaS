using System.Net.Http;
using System.Net.Http.Json;
using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;

namespace DataMedix.Infrastructure.HttpClients;

public class IdentityGatewayClient : IIdentityGatewayClient
{
    private readonly HttpClient _httpClient;

    // 🔥 ESTE CONSTRUCTOR ES OBLIGATORIO PARA TYPED CLIENT
    public IdentityGatewayClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> LoginAsync(string email, string password, Tenant tenant)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenant.id.ToString());
        string nonce = Guid.NewGuid().ToString(); // Genera un nonce único para cada solicitud

        var response = await _httpClient.PostAsJsonAsync("/api/auth/login/identify", new
        {
            email,
            password,
            nonce
        });

        return response.IsSuccessStatusCode;
    }
}
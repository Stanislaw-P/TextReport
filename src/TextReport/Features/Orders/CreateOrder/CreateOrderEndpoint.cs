using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using TextReport.Features.Payments;
using TextReport.Infrastructure.Persistence;

namespace TextReport.Features.Orders.CreateOrder;

internal static class CreateOrderEndpoint
{
    private const int MaxBodySize = 128 * 1024;

    public static void MapCreateOrder(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/orders", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request, TextReportDbContext db, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxBodySize)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        if (!request.HasJsonContentType()
            || !MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType)
            || (contentType.Charset.HasValue
                && !string.Equals(contentType.Charset.Value, "utf-8", StringComparison.OrdinalIgnoreCase)))
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = Math.Min(limit.MaxRequestBodySize ?? MaxBodySize, MaxBodySize);

        Request? input;
        try
        {
            input = await request.ReadFromJsonAsync<Request>(cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Некорректное тело запроса." });
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var email = input?.Email?.Trim() ?? "";
        var text = input?.Text ?? "";
        var errors = new Dictionary<string, string[]>();
        if (email.Length > 254 || email.Any(char.IsControl)
            || !MailAddress.TryCreate(email, out var address)
            || address.Address != email || !address.Host.Contains('.'))
            errors["email"] = ["Укажите корректный email длиной до 254 символов."];
        if (text.Length is < 20 or > 15_000 || string.IsNullOrWhiteSpace(text) || text.Contains('\0'))
            errors["text"] = ["Введите от 20 до 15 000 символов текста без недопустимых знаков."];
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.Orders.Add(new Order
        {
            Id = orderId,
            Email = email,
            InputText = text,
            CreatedAt = now,
            Payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                Amount = 100m,
                Currency = "RUB",
                CreatedAt = now
            }
        });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Json(new { orderId }, statusCode: StatusCodes.Status201Created);
    }

    private sealed record Request(string? Email, string? Text);
}

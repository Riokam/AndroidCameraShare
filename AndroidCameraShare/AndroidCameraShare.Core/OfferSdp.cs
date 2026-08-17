using System.Text.Encodings.Web;
using System.Text.Json;

namespace AndroidCameraShare.Core
{
    /// <summary>
    /// SDP в JSON, как шлёт viewer.html. Без полного SDP в логах — только разбор.
    /// </summary>
    public static class OfferSdp
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static bool TryReadOffer(string? body, out string sdp)
        {
            sdp = string.Empty;
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            try
            {
                OfferDto? dto = JsonSerializer.Deserialize<OfferDto>(body, JsonOptions);
                if (dto is null || string.IsNullOrWhiteSpace(dto.Sdp))
                {
                    return false;
                }

                sdp = dto.Sdp;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static string ToAnswerJson(string sdp)
        {
            return JsonSerializer.Serialize(new OfferDto { Type = "answer", Sdp = sdp }, JsonOptions);
        }

        public static string ToErrorJson(string message)
        {
            return JsonSerializer.Serialize(new ErrorDto { Error = message }, JsonOptions);
        }

        private sealed class OfferDto
        {
            public string? Type { get; set; }

            public string? Sdp { get; set; }
        }

        private sealed class ErrorDto
        {
            public string Error { get; set; } = string.Empty;
        }
    }
}

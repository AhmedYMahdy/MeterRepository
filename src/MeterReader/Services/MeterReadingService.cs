using Grpc.Core;
using MeterReading.gRPC;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using static MeterReading.gRPC.MeterReadingService;
using MeterReadingEntity = MeterReader.Data.Entities.MeterReading;

namespace MeterReader.Services;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MeterReadingService : MeterReadingServiceBase
{
    private readonly IReadingRepository _readingRepository;
    private readonly JwtTokenValidationService _jwtService;
    private readonly ILogger<MeterReadingService> _logger;

    public MeterReadingService(
        IReadingRepository readingRepository,
        JwtTokenValidationService jwtTokenValidationService,
        ILogger<MeterReadingService> logger)
    {
        _readingRepository = readingRepository;
        _jwtService = jwtTokenValidationService;
        _logger = logger;
    }

    public override async Task<StatusMessage> AddReading(ReadingPacket request, ServerCallContext context)
    {
        if (request.Successful == ReadingStatus.Success)
        {
            foreach (var reading in request.Readings)
            {
                var readingVal = new MeterReadingEntity()
                {
                    CustomerId = reading.CustomerId,
                    Value = reading.ReadingValue,
                    ReadingDate = DateTime.Parse(reading.ReadingTime)
                };
                _readingRepository.AddEntity(readingVal);
            }
            if (await _readingRepository.SaveAllAsync())
            {
                return new StatusMessage
                {
                    Message = "Successfully added",
                    Success = ReadingStatus.Success,
                };
            }
        }
        return new StatusMessage
        {
            Message = "Failed",
            Success = ReadingStatus.Failure,
        };
    }
    public override async Task AddReadingStream(
        IAsyncStreamReader<ReadingMessage> requestStream,
        IServerStreamWriter<ErrorMessage> responseStream,
        ServerCallContext context)
    {

        while (await requestStream.MoveNext())
        {

            var msg = requestStream.Current;
            if (msg.ReadingValue < 500)
            {
                await responseStream.WriteAsync(new ErrorMessage
                {
                    Message = $"Value Less than 500 value:{msg.ReadingValue}"
                });
            }

            var readingVal = new MeterReadingEntity()
            {
                CustomerId = msg.CustomerId,
                Value = msg.ReadingValue,
                ReadingDate = DateTime.Parse(msg.ReadingTime)
            };
            _logger.LogInformation($"Adding {msg.ReadingValue} from stream");
            _readingRepository.AddEntity(readingVal);
            await _readingRepository.SaveAllAsync();
        }
    }

    [AllowAnonymous]
    public override async Task<TokenResponse> GenerateToken(TokenRequest request, ServerCallContext context)
    {
        var creds = new CredentialModel
        {
            UserName = request.Username,
            Passcode = request.Password,
        };
        var result = await _jwtService.GenerateTokenModelAsync(creds);

        if (result.Success)
            return new TokenResponse
            {
                Success = true,
                Token = result.Token
            };

        return new TokenResponse
        {
            Success = false,
        };
    }
}
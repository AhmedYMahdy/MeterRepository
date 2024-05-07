using Grpc.Core;
using MeterReading.gRPC;
using MeterReadingEntity = MeterReader.Data.Entities.MeterReading;
using static MeterReading.gRPC.MeterReadingService;
using Google.Protobuf.WellKnownTypes;

namespace MeterReader.Services;

public class MeterReadingService : MeterReadingServiceBase
{
    private readonly IReadingRepository _readingRepository;
    private readonly ILogger<MeterReadingService> _logger;

    public MeterReadingService(
        IReadingRepository readingRepository,
        ILogger<MeterReadingService> logger)
    {
        _readingRepository = readingRepository;
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
}
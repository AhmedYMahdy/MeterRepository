using MeterReading.gRPC;

namespace MeterReadingClient;

public class ReadingGenerator
{
    public Task<ReadingMessage> GenerateAsync(int customerId)
    {
        var reading = new ReadingMessage() {
            CustomerId = customerId,
            ReadingTime = DateTime.UtcNow.ToString(),
            ReadingValue= new Random().Next(10000),
            Notes = $"Notes for customer {customerId}"
        };
        return Task.FromResult(reading);
    }
}

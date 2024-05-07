using Grpc.Net.Client;
using MeterReading.gRPC;
using static MeterReading.gRPC.MeterReadingService;

namespace MeterReadingClient
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ReadingGenerator _generator;
        private readonly int _customerId;
        private readonly string _serviceUrl;

        public Worker(
            ILogger<Worker> logger,
            ReadingGenerator generator,
            IConfiguration config)
        {
            _logger = logger;
            _generator = generator;
            _customerId = config.GetValue<int>("CustomerId");
            _serviceUrl = config["ServiceUrl"];
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var channel = GrpcChannel.ForAddress(_serviceUrl);
                var client = new MeterReadingServiceClient(channel);
                //var packet = new ReadingPacket()
                //{
                //    Successful = ReadingStatus.Success
                //};
                var stream = client.AddReadingStream();

                for (var i = 0; i < 5; i++)
                {
                    var reading = await _generator.GenerateAsync(_customerId);

                    //packet.Readings.Add(reading);
                    await stream.RequestStream.WriteAsync(reading);
                    await Task.Delay(500);
                }
                //var staus = client.AddReading(packet);

                //if (staus.Success == ReadingStatus.Success)
                //    _logger.LogInformation("Successfully Called gRPC");
                //else
                //    _logger.LogInformation("Failed to  Call gRPC");

                await stream.RequestStream.CompleteAsync();
                while (await stream.ResponseStream.MoveNext(new CancellationToken()))
                {
                    _logger.LogWarning($"From Server: {stream.ResponseStream.Current.Message}");
                }

                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}

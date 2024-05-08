using System.IO;
using Grpc.Core;
using Grpc.Net.Client;
using static MeterReading.gRPC.MeterReadingService;

namespace MeterReadingClient
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ReadingGenerator _generator;
        private readonly IConfiguration _config;
        private readonly int _customerId;
        private readonly string _serviceUrl;
        private string _token;

        public Worker(
            ILogger<Worker> logger,
            ReadingGenerator generator,
            IConfiguration config)
        {
            _logger = logger;
            _generator = generator;
            _config = config;
            _customerId = config.GetValue<int>("CustomerId");
            _serviceUrl = config["ServiceUrl"];
        }

        bool NeedsLogin()
        {
            return string.IsNullOrWhiteSpace(_token);
        }
        async Task<bool> RequestToken()
        {
            var result = CreateClient().GenerateToken(
                new MeterReading.gRPC.TokenRequest
                {
                    Password = _config["Settings:Password"],
                    Username = _config["Settings:Username"],
                });
            if (result.Success)
            {
                _token = result.Token;
                return true;
            }

            return false;
        }
        MeterReadingServiceClient CreateClient()
        {

            var channel = GrpcChannel.ForAddress(_serviceUrl);
            var client = new MeterReadingServiceClient(channel);
            return client;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!NeedsLogin() || await RequestToken())
                    {
                        //var packet = new ReadingPacket()
                        //{
                        //    Successful = ReadingStatus.Success
                        //};
                        var header = new Metadata
                        {
                            { "Authorization", $"Bearer {_token}" }
                        };
                        var stream = CreateClient().AddReadingStream(header);

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
                    }
                    else
                        _logger.LogError($"From Server: Failed to get JWT Token");
                }
                catch (RpcException ex)
                {
                    _logger.LogError(ex.Message);
                }

                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}

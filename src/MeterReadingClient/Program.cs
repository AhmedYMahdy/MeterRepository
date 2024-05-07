using MeterReadingClient;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddTransient<ReadingGenerator>();
    })
    .Build();

await host.RunAsync();

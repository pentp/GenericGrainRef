using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Hosting;

public static class Program
{
    public static async Task Main()
    {
        using var host = new HostBuilder()
            .UseOrleans(builder =>
            {
                builder
                    .UseLocalhostClustering()
                    .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(ReproGrain).Assembly))
                    .AddMemoryGrainStorageAsDefault();
            })
            .ConfigureServices(services =>
            {
                services.Configure<ConsoleLifetimeOptions>(o => o.SuppressStatusMessages = true);
            })
            .ConfigureLogging(b => b.AddConsole())
            .UseConsoleLifetime().Build();

        await host.StartAsync();

        var factory = host.Services.GetRequiredService<IGrainFactory>();

        var grain = factory.GetGrain<IReproGrain<Dummy>>(222);
        await grain.SetValue("generic-interface");
        await grain.GetValue();
        await grain.GetValue();

        grain = factory.GetGrain<IReproGrainDummy>(111);
        await grain.SetValue("concrete");
        await grain.GetValue();
        await grain.GetValue();

        await Task.Run(Console.ReadLine);

        await host.StopAsync();
    }
}

public interface IReproGrain<T>:IGrainWithIntegerKey
{
    Task SetValue(string value, bool deactivate = false);
    Task<string> GetValue();
}

public interface IReproGrainDummy:IReproGrain<Dummy> { }

public class Dummy { }

public class ReproState
{
    public string Value;
}

public class ReproGrain:Grain<ReproState>, IReproGrainDummy
{
    private readonly ILogger logger;
    public ReproGrain(ILogger<ReproGrain> logger) => this.logger = logger;

    public Task SetValue(string value, bool deactivate)
    {
        logger.LogWarning($"SetValue='{value}' {GrainReference}");
        State.Value = value;
        return WriteStateAsync();
    }

    public Task<string> GetValue()
    {
        var value = State.Value;
        logger.LogWarning($"GetValue='{value}' {GrainReference}");
        DeactivateOnIdle();
        return Task.FromResult(value);
    }
}
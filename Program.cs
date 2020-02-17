using System;
using System.Diagnostics;
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

        IReproGrain<Dummy> genericRef = factory.GetGrain<IReproGrain<Dummy>>(0);
        IReproGrain<Dummy> concreteRef = factory.GetGrain<IReproGrainDummy>(0);

        await genericRef.SetValue("42");
        Debug.Assert("42" == await genericRef.GetValue());
        Debug.Assert("42" == await concreteRef.GetValue());

        await genericRef.Deactivate();
        Debug.Assert("42" == await genericRef.GetValue());
        Debug.Assert("42" == await concreteRef.GetValue());

        await genericRef.Deactivate();
        Debug.Assert("42" == await concreteRef.GetValue()); // activating through non-generic grain ref fails to load state because Grain.GrainReference is different
        Debug.Assert("42" == await genericRef.GetValue());

        await Task.Run(Console.ReadLine);

        await host.StopAsync();
    }
}

public interface IReproGrain<T>:IGrainWithIntegerKey
{
    Task SetValue(string value);
    Task<string> GetValue();
    Task Deactivate();
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

    public override Task OnActivateAsync()
    {
        logger.LogWarning($"OnActivateAsync {GrainReference} Identity={IdentityString}");
        return base.OnActivateAsync();
    }

    public Task SetValue(string value)
    {
        logger.LogWarning($"SetValue='{value}' {GrainReference} Identity={IdentityString}");
        State.Value = value;
        return WriteStateAsync();
    }

    public Task<string> GetValue()
    {
        var value = State.Value;
        logger.LogWarning($"GetValue='{value}' {GrainReference} Identity={IdentityString}");
        return Task.FromResult(value);
    }

    public Task Deactivate()
    {
        logger.LogWarning($"Deactivate {GrainReference} Identity={IdentityString}");
        DeactivateOnIdle();
        return Task.CompletedTask;
    }
}
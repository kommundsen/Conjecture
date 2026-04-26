<Query Kind="Program">
  <NuGetReference>Conjecture.AspNetCore</NuGetReference>
  <NuGetReference>Conjecture.LinqPad</NuGetReference>
  <NuGetReference>Microsoft.AspNetCore.Mvc.Testing</NuGetReference>
  <Namespace>Conjecture.AspNetCore</Namespace>
  <Namespace>Conjecture.Core</Namespace>
  <Namespace>Conjecture.Http</Namespace>
  <Namespace>Conjecture.Interactions</Namespace>
  <Namespace>Microsoft.AspNetCore.Builder</Namespace>
  <Namespace>Microsoft.AspNetCore.Hosting</Namespace>
  <Namespace>Microsoft.AspNetCore.Http</Namespace>
  <Namespace>Microsoft.AspNetCore.Mvc.Testing</Namespace>
  <Namespace>Microsoft.AspNetCore.Routing</Namespace>
  <Namespace>Microsoft.Extensions.DependencyInjection</Namespace>
  <Namespace>Microsoft.Extensions.Hosting</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Threading</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

// Property-test an ASP.NET Core API in LinqPad.
// Util.KeepRunning() prevents LinqPad from disposing the script's resources before async work completes;
// QueryCancelToken propagates LinqPad's cancel button into Conjecture's runner.

async Task Main()
{
    Util.KeepRunning();

    await using WebApplicationFactory<LinqPadSampleApp> factory = new WebApplicationFactory<LinqPadSampleApp>()
        .WithWebHostBuilder(static webBuilder =>
        {
            webBuilder.Configure(static app =>
            {
                app.UseRouting();
                app.UseEndpoints(static endpoints =>
                {
                    endpoints.MapGet("/orders", static () => Results.Ok("orders"));
                    endpoints.MapGet("/items/{id:int}", static (int id) => Results.Ok($"item {id}"));
                });
            });
        });

    using HttpClient client = factory.CreateClient();
    IHost host = factory.Services.GetRequiredService<IHost>();
    HostHttpTarget target = new(host, client);

    Strategy<HttpInteraction> strategy = Generate
        .AspNetCoreRequests(host, client)
        .ValidRequestsOnly()
        .Build();

    await Property.ForAll(target, strategy, static async (t, request) =>
    {
        HttpResponseMessage response = await request.Response((IHttpTarget)t);
        await Task.FromResult(response).AssertNot5xx();
    }, ct: QueryCancelToken);

    "All examples passed.".Dump();
}

public sealed class LinqPadSampleApp { }

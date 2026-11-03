using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using SigaaMoodle.Bridge.Configuration;
using SigaaMoodle.Bridge.Controllers;
using SigaaMoodle.Bridge.Sync;

namespace SigaaMoodle.Bridge.Tests.Controllers;

public class SyncControllerTests
{
    [Theory]
    [InlineData("invalido")]
    public async Task RunOne_QuandoDominioInvalido_DeveRetornarBadRequestESemAcionarServico(string domain)
    {
        var syncService = Substitute.For<ISyncService>();
        var statusStore = Substitute.For<ISyncStatusStore>();
        var controller = new SyncController(syncService, statusStore,
            Options.Create(new SyncOptions { EnabledDomains = ["Alunos"] }));

        var result = await controller.RunOne(domain, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        await syncService.DidNotReceive().RunAsync(
            Arg.Any<IReadOnlyCollection<SyncDomain>?>(), Arg.Any<CancellationToken>());
    }
}

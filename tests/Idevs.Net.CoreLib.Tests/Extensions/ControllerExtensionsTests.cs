using Idevs.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Idevs.Net.CoreLib.Tests.Extensions;

public class ControllerExtensionsTests
{
    [Fact]
    public void RenderView_WaitsForViewRenderingToComplete()
    {
        var view = Substitute.For<IView>();
        view.RenderAsync(Arg.Any<ViewContext>()).Returns(async call =>
        {
            await Task.Delay(25);
            var context = call.Arg<ViewContext>();
            await context.Writer.WriteAsync("rendered html");
        });

        var viewEngine = Substitute.For<ICompositeViewEngine>();
        viewEngine.GetView("Views", "Report", true)
            .Returns(ViewEngineResult.Found("Report", view));

        var services = new ServiceCollection()
            .AddSingleton(viewEngine)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };

        var controller = new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
                ActionDescriptor = new ControllerActionDescriptor(),
                RouteData = new RouteData()
            },
            TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>())
        };

        var html = controller.RenderView("Views", "Report", new { Name = "Test" });

        Assert.Equal("rendered html", html);
    }

    private sealed class TestController : Controller;
}

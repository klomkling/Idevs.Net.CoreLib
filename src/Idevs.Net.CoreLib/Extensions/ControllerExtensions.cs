using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Idevs.Extensions;

public static class ControllerExtensions
{
    extension(Controller controller)
    {
        public string RenderView<T>(string path, string viewName, T model,
            bool partial = false)
        {
            return controller.RenderViewAsync(path, viewName, model, partial).GetAwaiter().GetResult();
        }

        public async Task<string> RenderViewAsync<T>(string path, string viewName, T model,
            bool partial = false)
        {
            if (string.IsNullOrEmpty(viewName))
            {
                viewName = controller.ControllerContext.ActionDescriptor.ActionName;
            }

            controller.ViewData.Model = model;

            await using var writer = new StringWriter();
            IViewEngine viewEngine =
                controller.HttpContext.RequestServices.GetService(typeof(ICompositeViewEngine)) as ICompositeViewEngine
                ?? throw new InvalidOperationException("A view engine is required.");
            var viewResult = viewEngine.GetView(path, viewName, isMainPage: !partial);
            if (!viewResult.Success)
            {
                throw new InvalidOperationException($"A view with the name {viewName} could not be found");
            }

            var viewContext = new ViewContext(
                controller.ControllerContext,
                viewResult.View,
                controller.ViewData,
                controller.TempData,
                writer,
                new HtmlHelperOptions()
            );

            await viewResult.View.RenderAsync(viewContext);

            return writer.GetStringBuilder().ToString();
        }
    }
}

using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace AutoAllegro.Helpers.TagHelpers
{
    [HtmlTargetElement("menulink", Attributes = "controller-name, action-name, menu-text")]
    public class MenuLinkTagHelper : Microsoft.AspNetCore.Razor.TagHelpers.TagHelper
    {
        public string ControllerName { get; set; }
        public string ActionName { get; set; }
        public string MenuText { get; set; }
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        public IUrlHelper UrlHelper { get; set; }

        public MenuLinkTagHelper(IUrlHelperFactory urlHelperFactory, IActionContextAccessor actionAccessor)
        {
            UrlHelper = urlHelperFactory.GetUrlHelper(actionAccessor.ActionContext);
        }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            string menuUrl = UrlHelper.Action(ActionName, ControllerName);

            output.TagName = "li";

            var tag = new TagBuilder("a");
            tag.MergeAttribute("href", $"{menuUrl}");
            tag.MergeAttribute("title", MenuText);
            tag.InnerHtml.Append(MenuText); 

            var routeData = ViewContext.RouteData.Values;
            var currentController = routeData["controller"];
            var currentAction = routeData["action"];

            if (string.Equals(ActionName, currentAction as string, StringComparison.OrdinalIgnoreCase)
                && string.Equals(ControllerName, currentController as string, StringComparison.OrdinalIgnoreCase))
            {
                output.Attributes.Add("class", "active");
            }

            output.Content.SetHtmlContent(tag);
        }
    }
}

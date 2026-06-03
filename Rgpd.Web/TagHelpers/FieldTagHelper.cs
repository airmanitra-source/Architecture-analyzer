using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Rgpd.Infrastructure.Security;
using Rgpd.Web.Services;

namespace Rgpd.Web.TagHelpers;

[HtmlTargetElement("field", Attributes = "asp-for")]
public sealed class FieldTagHelper : TagHelper
{
    private readonly IDataMaskingService _dataMaskingService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FieldTagHelper(IDataMaskingService dataMaskingService, IHttpContextAccessor httpContextAccessor)
    {
        _dataMaskingService = dataMaskingService;
        _httpContextAccessor = httpContextAccessor;
    }

    [HtmlAttributeName("asp-for")]
    public required ModelExpression AspFor { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";

        var purpose = AspFor.Metadata.ContainerType?
            .GetProperty(AspFor.Name)?
            .GetCustomAttributes(typeof(PersonalDataAttribute), true)
            .OfType<PersonalDataAttribute>()
            .FirstOrDefault()?.Purpose;

        var principal = _httpContextAccessor.HttpContext?.User;
        var value = AspFor.Model;
        var displayValue = string.IsNullOrWhiteSpace(purpose) || principal is null
            ? value?.ToString() ?? string.Empty
            : _dataMaskingService.MaskValue(value, purpose, principal);

        output.Attributes.SetAttribute("class", "rgpd-field");
        output.Content.SetContent(displayValue);
    }
}

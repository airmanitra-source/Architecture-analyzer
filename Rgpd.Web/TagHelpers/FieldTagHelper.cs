using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Rgpd.Infrastructure.Security;
using Rgpd.Web.Services;

namespace Rgpd.Web.TagHelpers;

[HtmlTargetElement("rgpd-field", Attributes = "asp-for, purpose, legal-basis, role")]
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

    [HtmlAttributeName("purpose")]
    public required Purpose Purpose { get; set; }

    [HtmlAttributeName("legal-basis")]
    public required LegalBasis LegalBasis { get; set; }

    [HtmlAttributeName("role")]
    public required AccessRole Role { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";

        var personalDataPolicy = AspFor.Metadata.ContainerType?
            .GetProperty(AspFor.Name)?
            .GetCustomAttributes(typeof(PersonalDataAttribute), true)
            .OfType<PersonalDataAttribute>()
            .FirstOrDefault(policy => policy.Purpose == Purpose
                && policy.LegalBasis == LegalBasis
                && policy.AccessRole == Role);

        var principal = _httpContextAccessor.HttpContext?.User;
        var value = AspFor.Model;
        var accessContext = new PersonalDataAccessContext
        {
            Purpose = Purpose,
            LegalBasis = LegalBasis,
            AccessRole = Role
        };
        var displayValue = personalDataPolicy is null
            ? "****"
            : principal is null
            ? value?.ToString() ?? string.Empty
            : _dataMaskingService.MaskValue(value, accessContext, principal);

        output.Attributes.SetAttribute("class", "rgpd-field");
        output.Content.SetContent(displayValue);
    }
}

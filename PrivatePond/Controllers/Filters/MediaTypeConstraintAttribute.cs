using System;
using Microsoft.AspNetCore.Mvc.ActionConstraints;

namespace PrivatePond.Controllers.Filters
{
    public class MediaTypeConstraintAttribute : Attribute, IActionConstraint
    {
        public MediaTypeConstraintAttribute(string mediaType)
        {
            MediaType = mediaType ?? throw new ArgumentNullException(nameof(mediaType));
        }

        public string MediaType { get; set; }

        public int Order => 100;

        public bool Accept(ActionConstraintContext context)
        {
            var match = context.RouteContext.HttpContext.Request.ContentType?.StartsWith(MediaType,
                StringComparison.Ordinal);
            return match.HasValue && match.Value;
        }
    }
}
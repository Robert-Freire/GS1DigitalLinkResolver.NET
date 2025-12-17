using Microsoft.AspNetCore.Routing;

namespace WebResolverService.Constraints;

/// <summary>
/// Route constraint that matches any path (including slashes)
/// </summary>
public class PathRouteConstraint : IRouteConstraint
{
    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        // Accept any non-empty value
        if (values.TryGetValue(routeKey, out var value) && value != null)
        {
            var stringValue = value.ToString();
            return !string.IsNullOrWhiteSpace(stringValue);
        }

        return false;
    }
}

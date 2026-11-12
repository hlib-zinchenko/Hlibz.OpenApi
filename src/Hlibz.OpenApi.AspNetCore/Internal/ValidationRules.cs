using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Hlibz.OpenApi.Internal;

/// <summary>
/// Tells whether validating a parameter can fail. The validation source generator also registers
/// types with nothing to check (a <c>record struct CityId(int Value)</c> handler parameter), and
/// ASP.NET Core validates those too, but they never answer <c>400</c>.
/// </summary>
internal static class ValidationRules
{
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;

    /// <summary>
    /// Gets whether <paramref name="parameter"/> or anything inside it has a rule: a
    /// <see cref="ValidationAttribute"/> (on the parameter, a type, a property or a record's
    /// constructor parameter) or <see cref="IValidatableObject"/>, searched through properties
    /// and collection items.
    /// </summary>
    /// <param name="parameter">A handler parameter.</param>
    /// <returns>Whether validating it can fail.</returns>
    public static bool Exist(ParameterInfo parameter) =>
        HasAttribute(parameter) || Exist(parameter.ParameterType, []);

    private static bool Exist(Type type, HashSet<Type> seen)
    {
        type = Unwrap(type);
        if (ParameterSchemas.ElementType(type) is { } element)
        {
            type = Unwrap(element);
        }

        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || !seen.Add(type))
        {
            return false;
        }

        return typeof(IValidatableObject).IsAssignableFrom(type)
               || type.GetCustomAttributes<ValidationAttribute>(inherit: true).Any()
               || type.GetConstructors(Instance).SelectMany(constructor => constructor.GetParameters()).Any(HasAttribute)
               || type.GetProperties(Instance).Any(property =>
                   property.GetCustomAttributes<ValidationAttribute>(inherit: true).Any()
                   || Exist(property.PropertyType, seen));
    }

    private static bool HasAttribute(ParameterInfo parameter) =>
        parameter.GetCustomAttributes<ValidationAttribute>(inherit: true).Any();

    private static Type Unwrap(Type type) => Nullable.GetUnderlyingType(type) ?? type;
}

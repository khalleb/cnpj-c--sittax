using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using System.ComponentModel;

namespace Sittax.Cnpj.Utils
{
    public class EnumToPostgresConverter<T> : ValueConverter<T, string> where T : struct, Enum
    {
        public EnumToPostgresConverter()
            : base(enumValue => ConvertToString(enumValue), stringValue => ConvertToEnum(stringValue))
        {
        }

        private static string ConvertToString(T enumValue)
        {
            var field = enumValue.GetType().GetField(enumValue.ToString());
            var attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false).SingleOrDefault() as DescriptionAttribute;
            return attribute?.Description ?? enumValue.ToString().ToLowerInvariant();
        }

        private static T ConvertToEnum(string value)
        {
            foreach (T enumValue in Enum.GetValues<T>())
            {
                var field = typeof(T).GetField(enumValue.ToString());
                var attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false).SingleOrDefault() as DescriptionAttribute;

                if (attribute?.Description == value)
                    return enumValue;
            }

            // Fallback: tentar converter pelo nome
            if (Enum.TryParse<T>(value.Replace("_", ""), true, out var result))
                return result;

            return default(T);
        }
    }
}

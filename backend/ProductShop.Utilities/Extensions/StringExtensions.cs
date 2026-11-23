namespace ProductShop.Utilities.Extensions
{
    public static class StringExtensions
    {
        // " Nafis@Mail.COM " -> "nafis@mail.com"
        // Used everywhere we save or search an email, so login is case-insensitive.
        public static string NormalizeEmail(this string email)
        {
            return email.Trim().ToLowerInvariant();
        }
    }
}

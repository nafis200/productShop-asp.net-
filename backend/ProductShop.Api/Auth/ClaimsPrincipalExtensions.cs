using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ProductShop.Api.Auth
{
    // =====================================================================
    // ClaimsPrincipalExtensions — টোকেন থেকে ইউজারের Id সহজে বের করার জন্য
    // =====================================================================
    //
    // Controller এর ভেতরে "User" নামে একটা প্রপার্টি থাকে।
    // ASP.NET নিজে থেকে JWT টোকেন পড়ে এই "User" তৈরি করে দেয়।
    // এর টাইপ হলো ClaimsPrincipal (মানে: claims সহ লগইন করা ইউজার)।
    //
    // এই ফাইলে একটা "Extension Method" আছে।
    // Extension Method = পুরনো ক্লাসে (ClaimsPrincipal) নতুন মেথড জুড়ে দেওয়া,
    // ক্লাসটা নিজে না বদলিয়েই।
    //
    // ব্যবহার (Controller এ):
    //     Guid userId = User.GetUserId();
    //
    // নিয়ম: Extension Method এর ক্লাস "static" হতে হয়,
    // আর প্রথম প্যারামিটারের আগে "this" লিখতে হয়।
    // =====================================================================
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal principal)
        {
            // ধাপ ১: টোকেন থেকে "sub" claim এর মান পড়ি।
            // (JwtTokenService এ আমরা "sub" এ ইউজারের Id রেখেছিলাম)
            string? userIdText = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

            // ধাপ ২: লেখাটা (string) কে Guid এ রূপান্তরের চেষ্টা করি
            Guid userId;
            bool isValidGuid = Guid.TryParse(userIdText, out userId);

            // ধাপ ৩: রূপান্তর না হলে (sub নেই বা ভুল) error দেই
            if (isValidGuid == false)
            {
                throw new InvalidOperationException("The access token does not contain a valid user id.");
            }

            // ধাপ ৪: সঠিক Id ফেরত দেই
            return userId;
        }
    }
}

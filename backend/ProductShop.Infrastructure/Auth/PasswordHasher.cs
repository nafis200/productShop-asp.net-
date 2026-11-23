using Microsoft.AspNetCore.Identity;
using ProductShop.Application.Interfaces;
using ProductShop.Domain.Entities;

namespace ProductShop.Infrastructure.Auth
{
    // =====================================================================
    // PasswordHasher — পাসওয়ার্ড নিরাপদভাবে সেভ ও চেক করার ক্লাস
    // =====================================================================
    //
    // কেন দরকার?
    //   ডাটাবেসে কখনো আসল পাসওয়ার্ড (যেমন "123456") সেভ করা যাবে না।
    //   ডাটাবেস চুরি হলে সবার পাসওয়ার্ড বের হয়ে যাবে।
    //   তাই পাসওয়ার্ডকে "Hash" বানিয়ে সেভ করি।
    //
    // Hash কী?
    //   "123456"  -->  "AQAAAAIAAYagAAAAE...."  (লম্বা হিজিবিজি লেখা)
    //   এটা ONE-WAY: hash থেকে আসল পাসওয়ার্ড আর ফেরত পাওয়া যায় না।
    //   শুধু চেক করা যায়: "এই পাসওয়ার্ড কি এই hash এর সাথে মিলে?"
    //
    // আমরা নিজে hash এর নিয়ম লিখিনি। ASP.NET Identity এর তৈরি
    // PasswordHasher<User> ব্যবহার করছি (এটা খুবই নিরাপদ, ভেতরে
    // PBKDF2 + random salt + অনেকবার iteration ব্যবহার করে)।
    // =====================================================================
    public class PasswordHasher : IPasswordHasher
    {
        // ASP.NET এর তৈরি hasher — আসল কাজ এটাই করে
        private readonly PasswordHasher<User> _identityHasher;

        // Constructor — ক্লাস তৈরি হওয়ার সময় একবার চলে
        public PasswordHasher()
        {
            _identityHasher = new PasswordHasher<User>();
        }

        // -----------------------------------------------------------------
        // Hash() — নতুন পাসওয়ার্ডকে hash বানায়
        // কখন ব্যবহার হয়? -> Register করার সময়
        // -----------------------------------------------------------------
        public string Hash(User user, string password)
        {
            // ধাপ ১: আসল পাসওয়ার্ড থেকে hash তৈরি করি
            string hashedPassword = _identityHasher.HashPassword(user, password);

            // ধাপ ২: hash ফেরত দেই (এটাই ডাটাবেসে সেভ হবে)
            return hashedPassword;
        }

        // -----------------------------------------------------------------
        // Verify() — ইউজারের দেওয়া পাসওয়ার্ড সঠিক কিনা চেক করে
        // কখন ব্যবহার হয়? -> Login করার সময়
        //
        // passwordHash = ডাটাবেসে সেভ করা hash
        // password     = ইউজার এখন যেটা টাইপ করেছে
        // -----------------------------------------------------------------
        public bool Verify(User user, string passwordHash, string password)
        {
            // ধাপ ১: টাইপ করা পাসওয়ার্ড আর সেভ করা hash মিলিয়ে দেখি
            PasswordVerificationResult result =
                _identityHasher.VerifyHashedPassword(user, passwordHash, password);

            // ধাপ ২: result এর ৩টা মান হতে পারে:
            //   Failed               -> পাসওয়ার্ড ভুল
            //   Success              -> পাসওয়ার্ড সঠিক
            //   SuccessRehashNeeded  -> পাসওয়ার্ড সঠিক (তবে hash পুরনো নিয়মের)
            if (result == PasswordVerificationResult.Failed)
            {
                return false; // ভুল পাসওয়ার্ড
            }

            return true; // সঠিক পাসওয়ার্ড
        }
    }
}

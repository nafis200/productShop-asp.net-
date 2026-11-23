using System.Text;

namespace ProductShop.Infrastructure.Auth
{
    // =====================================================================
    // JwtSettings — JWT টোকেন বানানোর সেটিংস রাখার ক্লাস
    // =====================================================================
    //
    // এই মানগুলো appsettings.json এর "JwtSettings" অংশ থেকে আসে। যেমন:
    //
    //   "JwtSettings": {
    //     "Issuer": "ProductShop.Api",
    //     "Audience": "ProductShop.Client",
    //     "Key": "কমপক্ষে-৩২-অক্ষরের-গোপন-চাবি..........",
    //     "ExpiryTimeInMinutes": 60
    //   }
    //
    // Program.cs / DependencyInjection.cs এ এই অংশ পড়ে এই ক্লাসে বসানো হয়।
    // =====================================================================
    public class JwtSettings
    {
        // appsettings.json এ যে নামে অংশটা আছে
        public const string SectionName = "JwtSettings";

        // Issuer = টোকেন কে বানিয়েছে? (আমাদের API)
        public string Issuer { get; set; } = string.Empty;

        // Audience = টোকেন কার জন্য বানানো? (আমাদের frontend/client)
        public string Audience { get; set; } = string.Empty;

        // Key = গোপন চাবি। এটা দিয়ে টোকেনে "সই" (sign) করা হয়।
        // কেউ টোকেন বদলালে সই মিলবে না, তাই নকল টোকেন ধরা পড়বে।
        // সতর্কতা: এই Key কাউকে দেখানো যাবে না!
        public string Key { get; set; } = string.Empty;

        // টোকেন কত মিনিট পর্যন্ত কাজ করবে (ডিফল্ট ৬০ মিনিট)
        public int ExpiryTimeInMinutes { get; set; } = 60;

        // -----------------------------------------------------------------
        // Validate() — সেটিংস ঠিক আছে কিনা অ্যাপ চালুর সময়ই চেক করে।
        //
        // কেন? ভুল থাকলে অ্যাপ শুরুতেই পরিষ্কার error দিয়ে বন্ধ হবে।
        // নাহলে প্রথম কেউ login করার সময় অদ্ভুত error আসত।
        // -----------------------------------------------------------------
        public void Validate()
        {
            // চেক ১: Issuer খালি থাকলে চলবে না
            if (string.IsNullOrWhiteSpace(Issuer))
            {
                throw new InvalidOperationException(SectionName + ":Issuer is not configured.");
            }

            // চেক ২: Audience খালি থাকলে চলবে না
            if (string.IsNullOrWhiteSpace(Audience))
            {
                throw new InvalidOperationException(SectionName + ":Audience is not configured.");
            }

            // চেক ৩: Key কমপক্ষে ৩২ byte লম্বা হতে হবে।
            // কারণ: HmacSha256 অ্যালগরিদম ২৫৬ bit (= ৩২ byte) এর কম চাবি নেয় না।
            int keyLengthInBytes = Encoding.UTF8.GetByteCount(Key);
            if (keyLengthInBytes < 32)
            {
                throw new InvalidOperationException(SectionName + ":Key must be at least 32 bytes long.");
            }

            // চেক ৪: মেয়াদ ০ বা নেগেটিভ হলে চলবে না
            if (ExpiryTimeInMinutes <= 0)
            {
                throw new InvalidOperationException(SectionName + ":ExpiryTimeInMinutes must be positive.");
            }
        }
    }
}

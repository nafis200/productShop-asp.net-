using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProductShop.Application.Interfaces;
using ProductShop.Domain.Constants;
using ProductShop.Domain.Entities;

namespace ProductShop.Infrastructure.Auth
{
    // =====================================================================
    // JwtTokenService — Login/Register সফল হলে JWT টোকেন বানায়
    // =====================================================================
    //
    // JWT টোকেন কী?
    //   একটা লম্বা string, যেমন: "eyJhbGciOi....eyJzdWIiOi....SflKxwRJ..."
    //   এটা ৩ ভাগে বিভক্ত (ডট দিয়ে আলাদা):
    //     ১. Header    -> কোন অ্যালগরিদমে সই করা
    //     ২. Payload   -> ইউজারের তথ্য (Claims)
    //     ৩. Signature -> গোপন Key দিয়ে করা সই
    //
    // কাজের ধারা:
    //   ইউজার login করে -> সার্ভার টোকেন দেয় -> frontend টোকেন রেখে দেয়
    //   -> পরের প্রতিটা request এ header এ পাঠায়:
    //      Authorization: Bearer <token>
    //   -> সার্ভার টোকেন দেখে বুঝে ইউজার কে।
    // =====================================================================
    public class JwtTokenService : ITokenService
    {
        // appsettings থেকে আসা সেটিংস (Issuer, Audience, Key, মেয়াদ)
        private readonly JwtSettings _jwtSettings;

        // টোকেন তৈরির টুল (Microsoft এর লাইব্রেরি)
        private readonly JsonWebTokenHandler _tokenHandler;

        // Constructor — Dependency Injection নিজে থেকে JwtSettings দিয়ে দেয়
        public JwtTokenService(JwtSettings jwtSettings)
        {
            _jwtSettings = jwtSettings;
            _tokenHandler = new JsonWebTokenHandler();
        }

        // -----------------------------------------------------------------
        // CreateAccessToken() — একজন ইউজারের জন্য টোকেন বানায়
        //
        // user  = যে ইউজার login করেছে
        // roles = ইউজারের রোল সমূহ (যেমন "Admin", "Customer")
        //
        // ফেরত দেয় ২টা জিনিস:
        //   Token     = টোকেন string
        //   ExpiresAt = কখন মেয়াদ শেষ হবে
        // -----------------------------------------------------------------
        public (string Token, DateTime ExpiresAt) CreateAccessToken(User user, IEnumerable<string> roles)
        {
            // ধাপ ১: Claims লিস্ট বানাই
            List<Claim> claims = BuildClaims(user, roles);

            // ধাপ ২: টোকেনের মেয়াদ শেষ হওয়ার সময় ঠিক করি (এখন + X মিনিট)
            DateTime expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryTimeInMinutes);

            // ধাপ ৩: সই করার ক্রেডেনশিয়াল বানাই
            SigningCredentials signingCredentials = BuildSigningCredentials();

            // ধাপ ৪: টোকেনের "বর্ণনা" তৈরি করি — টোকেনে কী কী থাকবে
            SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor();
            tokenDescriptor.Issuer = _jwtSettings.Issuer;          // কে বানাল
            tokenDescriptor.Audience = _jwtSettings.Audience;      // কার জন্য
            tokenDescriptor.Subject = new ClaimsIdentity(claims);  // ইউজারের তথ্য
            tokenDescriptor.Expires = expiresAt;                   // মেয়াদ
            tokenDescriptor.SigningCredentials = signingCredentials; // সই

            // ধাপ ৫: বর্ণনা থেকে আসল টোকেন string বানাই
            string token = _tokenHandler.CreateToken(tokenDescriptor);

            // ধাপ ৬: টোকেন আর মেয়াদ দুটোই ফেরত দেই
            return (token, expiresAt);
        }

        // -----------------------------------------------------------------
        // BuildClaims() — টোকেনের ভেতরে ইউজারের কী কী তথ্য থাকবে
        //
        // Claim = ইউজার সম্পর্কে একটা তথ্য (key = value)
        // যেমন: "email" = "nafis@example.com"
        //
        // সতর্কতা: টোকেন যে কেউ decode করে পড়তে পারে (jwt.io তে)।
        // তাই এখানে কখনো পাসওয়ার্ড বা গোপন তথ্য রাখবে না!
        // -----------------------------------------------------------------
        private List<Claim> BuildClaims(User user, IEnumerable<string> roles)
        {
            List<Claim> claims = new List<Claim>();

            // "sub" = subject = ইউজারের Id (সবচেয়ে গুরুত্বপূর্ণ)
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()));

            // "email" = ইউজারের ইমেইল
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));

            // "given_name" = নামের প্রথম অংশ
            claims.Add(new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName));

            // "family_name" = নামের শেষ অংশ
            claims.Add(new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName));

            // "jti" = প্রতিটা টোকেনের আলাদা ইউনিক Id
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));

            // প্রতিটা রোলের জন্য একটা করে "role" claim যোগ করি
            // যেমন: "role" = "Admin"
            // [Authorize(Roles = "Admin")] এই claim দেখেই সিদ্ধান্ত নেয়।
            foreach (string role in roles)
            {
                claims.Add(new Claim(RoleNames.ClaimType, role));
            }

            return claims;
        }

        // -----------------------------------------------------------------
        // BuildSigningCredentials() — গোপন Key দিয়ে "সই করার যন্ত্র" বানায়
        // -----------------------------------------------------------------
        private SigningCredentials BuildSigningCredentials()
        {
            // ধাপ ১: Key (string) কে byte এ রূপান্তর করি
            byte[] keyBytes = Encoding.UTF8.GetBytes(_jwtSettings.Key);

            // ধাপ ২: byte থেকে security key বানাই
            // Symmetric = একই Key দিয়ে সই করা হয় এবং যাচাইও করা হয়
            SymmetricSecurityKey securityKey = new SymmetricSecurityKey(keyBytes);

            // ধাপ ৩: HmacSha256 অ্যালগরিদম দিয়ে সই করার ক্রেডেনশিয়াল
            SigningCredentials credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            return credentials;
        }
    }
}

using Nouba.Helpers;

namespace Nouba.Tests.Unit;

public class PasswordHasherTests
{
    [Fact]
    public void HashPassword_ProducesNonEmptyHashAndSalt()
    {
        var (hash, salt) = PasswordHasher.HashPassword("testPassword");

        Assert.NotEmpty(hash);
        Assert.NotEmpty(salt);
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var (hash, salt) = PasswordHasher.HashPassword("mysecret");

        Assert.True(PasswordHasher.VerifyPassword("mysecret", hash, salt));
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var (hash, salt) = PasswordHasher.HashPassword("mysecret");

        Assert.False(PasswordHasher.VerifyPassword("wrongpassword", hash, salt));
    }

    [Fact]
    public void VerifyPassword_WrongCase_ReturnsFalse()
    {
        var (hash, salt) = PasswordHasher.HashPassword("Secret");

        Assert.False(PasswordHasher.VerifyPassword("secret", hash, salt));
    }

    [Fact]
    public void HashPassword_SamePassword_ProducesDifferentHashesDueToRandomSalt()
    {
        var (hash1, salt1) = PasswordHasher.HashPassword("password");
        var (hash2, salt2) = PasswordHasher.HashPassword("password");

        Assert.NotEqual(hash1, hash2);
        Assert.NotEqual(salt1, salt2);
    }

    [Fact]
    public void HashPassword_ProducesBase64Strings()
    {
        var (hash, salt) = PasswordHasher.HashPassword("test");

        Assert.NotNull(Convert.FromBase64String(hash));
        Assert.NotNull(Convert.FromBase64String(salt));
    }

    [Theory]
    [InlineData("", "aGFzaA==", "c2FsdA==")]
    [InlineData("password", "", "c2FsdA==")]
    [InlineData("password", "aGFzaA==", "")]
    public void VerifyPassword_EmptyInputs_ReturnsFalse(string password, string hash, string salt)
    {
        Assert.False(PasswordHasher.VerifyPassword(password, hash, salt));
    }
}

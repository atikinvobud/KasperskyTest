
using Backend.Scanning;

namespace Backend.Tests.Scanning;

public class RepositoryUrlValidatorTests
{
    private readonly RepositoryUrlValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-url")]
    [InlineData("github.com/example/project")]
    public async Task IsValidAsync_InvalidUrl_ReturnsFalse(string? url)
    {
        var result = await _validator.IsValidAsync(url);
        Assert.False(result);
    }

    [Theory]
    [InlineData("http://github.com/example/project")]
    [InlineData("https://example.com/project")]
    [InlineData("https://github.com:8080/example/project")]
    [InlineData("https://user:password@github.com/example/project")]
    public async Task IsValidAsync_DisallowedUrl_ReturnsFalse(string url)
    {
        var result = await _validator.IsValidAsync(url);
        Assert.False(result);
    }

    [Theory]
    [InlineData("https://github.com/example/project")]
    [InlineData("https://gitlab.com/example/project")]
    [InlineData("https://bitbucket.org/example/project")]
    public async Task IsValidAsync_AllowedHost_ReturnsTrue(string url)
    {
        var result = await _validator.IsValidAsync(url);
        Assert.True(result);
    }
}

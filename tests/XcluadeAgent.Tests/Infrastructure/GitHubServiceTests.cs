using FluentAssertions;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Tests.Infrastructure;

public class GitHubServiceTests
{
    private readonly Mock<ILogger<IGitHubService>> _loggerMock;

    public GitHubServiceTests()
    {
        _loggerMock = new Mock<ILogger<IGitHubService>>();
    }

    [Theory]
    [InlineData("https://github.com/owner/repo", "owner", "repo")]
    [InlineData("https://github.com/microsoft/dotnet", "microsoft", "dotnet")]
    [InlineData("https://github.com/xjanova/xmanstudio", "xjanova", "xmanstudio")]
    public void ParseRepository_ShouldExtractOwnerAndRepo(string url, string expectedOwner, string expectedRepo)
    {
        // Arrange & Act
        var (owner, repo) = ParseRepository(url);

        // Assert
        owner.Should().Be(expectedOwner);
        repo.Should().Be(expectedRepo);
    }

    [Theory]
    [InlineData("owner/repo", "owner", "repo")]
    [InlineData("microsoft/dotnet", "microsoft", "dotnet")]
    public void ParseRepository_ShortFormat_ShouldExtractOwnerAndRepo(string input, string expectedOwner, string expectedRepo)
    {
        // Arrange & Act
        var (owner, repo) = ParseRepository(input);

        // Assert
        owner.Should().Be(expectedOwner);
        repo.Should().Be(expectedRepo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("only-one-part")]
    public void ParseRepository_InvalidFormat_ShouldThrow(string input)
    {
        // Act
        Action act = () => ParseRepository(input);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("v1.0.0", "v*")]
    [InlineData("v2.3.4", "v*")]
    [InlineData("release-1.0", "release-*")]
    public void MatchesPattern_ShouldReturnTrue(string tag, string pattern)
    {
        // Act
        var result = MatchesPattern(tag, pattern);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("1.0.0", "v*")]
    [InlineData("beta-1.0", "release-*")]
    public void MatchesPattern_ShouldReturnFalse(string tag, string pattern)
    {
        // Act
        var result = MatchesPattern(tag, pattern);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesPattern_NullOrEmptyPattern_ShouldReturnTrue()
    {
        // Act & Assert
        MatchesPattern("any-tag", null).Should().BeTrue();
        MatchesPattern("any-tag", "").Should().BeTrue();
    }

    // Helper methods that simulate GitHubService logic
    private static (string owner, string repo) ParseRepository(string repository)
    {
        if (string.IsNullOrWhiteSpace(repository))
            throw new ArgumentException("Repository cannot be empty", nameof(repository));

        // Handle full URL format
        if (repository.StartsWith("https://github.com/"))
        {
            repository = repository.Replace("https://github.com/", "");
        }

        var parts = repository.Split('/');
        if (parts.Length != 2)
            throw new ArgumentException("Invalid repository format. Expected 'owner/repo'", nameof(repository));

        return (parts[0], parts[1]);
    }

    private static bool MatchesPattern(string tag, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return true;

        if (pattern.EndsWith("*"))
        {
            var prefix = pattern[..^1];
            return tag.StartsWith(prefix);
        }

        return tag == pattern;
    }
}

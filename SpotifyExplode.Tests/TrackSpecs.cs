using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace SpotifyExplode.Tests;

public class TrackSpecs
{
    [Theory]
    [InlineData("0VjIjW4GlUZAMYd2vXMi3b")]
    public async Task I_can_get_a_track(string trackId)
    {
        // Arrange
        var spotify = new SpotifyClient();

        // Act
        var results = await spotify.Tracks.GetAsync(trackId);

        // Assert
        results.Should().NotBeNull();
    }
}

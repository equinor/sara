using api.Services;
using Xunit;

namespace Api.Test.Services;

public class BlobServiceTests
{
    [Theory]
    [InlineData("myF/mySF/myFile.myEnding", ".newFileEnding", "myF/mySF/myFile.newFileEnding")]
    [InlineData("myF/mySF/myFile.oneDot.twoDot.myEnding", ".newFileEnding", "myF/mySF/myFile.oneDot.twoDot.newFileEnding")]
    public void ReplaceFileEnding_ValidFile_ReturnsFilePath(string originalFilePath, string newFileEnding, string expectedFilePath)
    {
        // Act
        var resultingFilePath = BlobService.ReplaceFileEnding(originalFilePath, newFileEnding);

        // Assert
        Assert.Equal(expectedFilePath, resultingFilePath);
    }
}

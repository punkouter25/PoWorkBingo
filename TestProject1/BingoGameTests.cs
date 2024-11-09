using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using PoBingo.Components.Pages;
using System.Text;

namespace TestProject1;

public class TestableHome : HomeBase
{
    public TestableHome(IJSRuntime jsRuntime)
    {
        JSRuntime = jsRuntime;
    }
}

public class BingoGameTests
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly TestableHome _component;

    public BingoGameTests()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>();
        _component = new TestableHome(_jsRuntimeMock.Object);
    }

    [Fact]
    public void ExtractWords_ShouldIgnoreCommonWordsAndShortWords()
    {
        // Arrange
        var content = "The quick brown fox jumps over the lazy dog. The test is running and optimization is needed.";

        // Act
        _component.ExtractWords(content);
        var extractedWords = _component.GetExtractedWords();

        // Assert
        Assert.NotNull(extractedWords);
        Assert.DoesNotContain(extractedWords!, kv => kv.Key == "the");  // Common word should be ignored
        Assert.DoesNotContain(extractedWords!, kv => kv.Key == "is");   // Short word should be ignored
        Assert.Contains(extractedWords!, kv => kv.Key == "quick");      // Valid word should be included
        Assert.Contains(extractedWords!, kv => kv.Key == "optimization"); // Bingo word should be included
    }

    [Fact]
    public void ProcessContent_ShouldIdentifyMatchingPhrases()
    {
        // Arrange
        var content = "The development team completed their smoke test and optimization tasks in azure.";

        // Act
        _component.ProcessContent(content);
        var matchedWords = _component.GetMatchedWords();

        // Assert
        Assert.NotNull(matchedWords);
        Assert.Contains("smoke test", matchedWords!);
        Assert.Contains("optimization", matchedWords!);
        Assert.Contains("azure", matchedWords!);
    }

    [Fact]
    public void CheckForBingo_ShouldDetectWinningCombination()
    {
        // Arrange
        var content = "smoke test that makes sense confused that's fair";  // First row combination

        // Act - Process content first
        _component.ProcessContent(content);
        _component.CheckForBingo();

        // Get results
        var highlightedCells = _component.GetHighlightedCells();
        var showBingoModal = _component.GetShowBingoModal();

        // Assert
        Assert.NotNull(highlightedCells);
        Assert.True(showBingoModal);
        Assert.Contains(0, highlightedCells!); // First cell should be highlighted
        Assert.Contains(1, highlightedCells!); // Second cell should be highlighted
        Assert.Contains(2, highlightedCells!); // Third cell should be highlighted
        Assert.Contains(3, highlightedCells!); // Fourth cell should be highlighted
    }

    [Fact]
    public async Task ProcessFile_ShouldRejectLargeFiles()
    {
        // Arrange
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Size).Returns(6 * 1024 * 1024); // 6MB file (exceeds 5MB limit)
        mockFile.Setup(f => f.Name).Returns("test.txt");

        // Create a real InputFileChangeEventArgs instance
        var browserFile = mockFile.Object;
        var args = new InputFileChangeEventArgs(new[] { browserFile });

        // Setup JSRuntime mock for all InvokeVoidAsync calls
        string? capturedMessage = null;
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                It.IsAny<string>(),
                It.IsAny<object[]>()))
            .Callback<string, object[]>((identifier, parameters) =>
            {
                if (identifier == "alert" && parameters.Length > 0)
                {
                    capturedMessage = parameters[0]?.ToString();
                }
            })
            .Returns(new ValueTask<IJSVoidResult>());

        // Also setup for console.log and console.error
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "console.log",
                It.IsAny<object[]>()))
            .Returns(new ValueTask<IJSVoidResult>());

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "console.error",
                It.IsAny<object[]>()))
            .Returns(new ValueTask<IJSVoidResult>());

        // Act
        await _component.ProcessFile(args);

        // Assert
        Assert.Contains("File size must be less than 5MB", capturedMessage ?? string.Empty);
        _jsRuntimeMock.Verify(x => x.InvokeAsync<IJSVoidResult>(
            "alert",
            It.Is<object[]>(p => p.Length > 0 && p[0].ToString().Contains("File size must be less than 5MB"))), 
            Times.Once);
    }

    [Fact]
    public void CloseModal_ShouldResetModalState()
    {
        // Arrange - Set initial state through a winning combination
        var content = "smoke test that makes sense confused that's fair";
        _component.ProcessContent(content);
        _component.CheckForBingo();
        Assert.True(_component.GetShowBingoModal()); // Verify modal is shown

        // Act
        _component.CloseModal();

        // Assert
        Assert.False(_component.GetShowBingoModal());
    }
}

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

    // Add methods to set and get protected fields for testing
    public void SetPhrases(string[] testPhrases)
    {
        phrases = testPhrases;
    }

    public string[] GetPhrases() => phrases;
    public string? GetLastUploadedPhrasesFile() => lastUploadedPhrasesFile;
    public bool GetPhrasesLoaded() => phrasesLoaded;
    public void SetPhrasesLoaded(bool value) => phrasesLoaded = value;
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
        Assert.True(extractedWords!.ContainsKey("brown")); // Additional word verification
        Assert.True(extractedWords!.ContainsKey("jumps")); // Additional word verification
    }

    [Fact]
    public void ProcessContent_ShouldIdentifyMatchingPhrases()
    {
        // Arrange
        var testPhrases = new string[] { 
            "xkcd123", "yzw456", "abc789", "def012",
            "phrase5", "phrase6", "phrase7", "phrase8",
            "phrase9", "phrase10", "phrase11", "phrase12",
            "phrase13", "phrase14", "phrase15", "phrase16"
        };
        _component.SetPhrases(testPhrases);
        _component.SetPhrasesLoaded(true);

        // Use content with exact matches only
        var content = "Found xkcd123 here. Also yzw456 there. Then abc789 appeared. Finally def012 showed up.";

        // Act
        _component.ProcessContent(content);
        var matchedWords = _component.GetMatchedWords();

        // Assert
        Assert.NotNull(matchedWords);
        Assert.Equal(4, matchedWords!.Count); // Should match exactly 4 phrases
        Assert.Contains("xkcd123", matchedWords!);
        Assert.Contains("yzw456", matchedWords!);
        Assert.Contains("abc789", matchedWords!);
        Assert.Contains("def012", matchedWords!);
    }

    [Fact]
    public void CheckForBingo_ShouldDetectWinningCombination()
    {
        // Arrange - First row combination with completely unique identifiers
        var testPhrases = new string[] { 
            "xkcd123", "yzw456", "abc789", "def012", // First row (winning combination)
            "mnop345", "qrst678", "uvwx901", "ijkl234",
            "efgh567", "ijkl890", "mnop123", "qrst456",
            "uvwx789", "yzab012", "cdef345", "ghij678"
        };
        _component.SetPhrases(testPhrases);
        _component.SetPhrasesLoaded(true);

        // Use content with exact matches only
        var content = "Here is xkcd123. Next is yzw456. Then abc789 appeared. Finally def012 showed up.";

        // Act
        _component.ProcessContent(content);
        _component.CheckForBingo();

        // Get results
        var highlightedCells = _component.GetHighlightedCells();
        var showBingoModal = _component.GetShowBingoModal();
        var matchedWords = _component.GetMatchedWords();

        // Assert
        Assert.NotNull(highlightedCells);
        Assert.True(showBingoModal);
        Assert.Equal(4, highlightedCells!.Count); // Should have exactly 4 highlighted cells
        Assert.Equal(4, matchedWords!.Count); // Should have exactly 4 matched words
        Assert.Equal(new[] { 0, 1, 2, 3 }, highlightedCells!.OrderBy(x => x).ToArray()); // Verify exact cells highlighted
        Assert.Contains("xkcd123", matchedWords!);
        Assert.Contains("yzw456", matchedWords!);
        Assert.Contains("abc789", matchedWords!);
        Assert.Contains("def012", matchedWords!);
    }

    [Fact]
    public async Task ProcessFile_ShouldRejectWhenPhrasesNotLoaded()
    {
        // Arrange
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Size).Returns(1024); // 1KB file
        mockFile.Setup(f => f.Name).Returns("test.txt");

        var browserFile = mockFile.Object;
        var args = new InputFileChangeEventArgs(new[] { browserFile });

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

        // Act
        await _component.ProcessFile(args);

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains("Please upload phrases file first", capturedMessage);
    }

    [Fact]
    public async Task ProcessFile_ShouldRejectLargeFiles()
    {
        // Arrange
        _component.SetPhrasesLoaded(true); // Enable file processing
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Size).Returns(6 * 1024 * 1024); // 6MB file (exceeds 5MB limit)
        mockFile.Setup(f => f.Name).Returns("test.txt");

        var browserFile = mockFile.Object;
        var args = new InputFileChangeEventArgs(new[] { browserFile });

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

        // Act
        await _component.ProcessFile(args);

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains("File size must be less than 5MB", capturedMessage);
    }

    [Fact]
    public async Task ProcessPhrasesFile_ShouldRejectLargeFiles()
    {
        // Arrange
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Size).Returns(6 * 1024 * 1024); // 6MB file (exceeds 5MB limit)
        mockFile.Setup(f => f.Name).Returns("phrases.txt");

        var browserFile = mockFile.Object;
        var args = new InputFileChangeEventArgs(new[] { browserFile });

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

        // Act
        await _component.ProcessPhrasesFile(args);

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains("File size must be less than 5MB", capturedMessage);
    }

    [Fact]
    public void CloseModal_ShouldResetModalState()
    {
        // Arrange - Set initial state through a winning combination
        var testPhrases = new string[] { 
            "xkcd123", "yzw456", "abc789", "def012", // First row (winning combination)
            "mnop345", "qrst678", "uvwx901", "ijkl234",
            "efgh567", "ijkl890", "mnop123", "qrst456",
            "uvwx789", "yzab012", "cdef345", "ghij678"
        };
        _component.SetPhrases(testPhrases);
        _component.SetPhrasesLoaded(true);
        
        var content = "Here is xkcd123. Next is yzw456. Then abc789 appeared. Finally def012 showed up.";
        _component.ProcessContent(content);
        _component.CheckForBingo();
        Assert.True(_component.GetShowBingoModal()); // Verify modal is shown initially

        // Act
        _component.CloseModal();

        // Assert
        Assert.False(_component.GetShowBingoModal());
    }

    [Fact]
    public void ParseUploadedPhrases_ShouldExtractPhrasesCorrectly()
    {
        // Arrange
        var expectedPhrases = new string[] {
            "phrase1", "phrase2", "phrase3", "phrase4",
            "phrase5", "phrase6", "phrase7", "phrase8",
            "phrase9", "phrase10", "phrase11", "phrase12",
            "phrase13", "phrase14", "phrase15", "phrase16"
        };
        var content = string.Join(", ", expectedPhrases);

        // Act
        var phrases = _component.ParseUploadedPhrases(content);

        // Assert
        Assert.NotNull(phrases);
        Assert.Equal(16, phrases.Length);
        Assert.Equal(expectedPhrases, phrases);
    }

    [Fact]
    public void ParseUploadedPhrases_ShouldThrowOnInvalidCount()
    {
        // Arrange
        var content = "phrase1, phrase2, phrase3"; // Only 3 phrases

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => 
            _component.ParseUploadedPhrases(content));
        Assert.Contains("must contain exactly 16 phrases", exception.Message);
    }

    [Fact]
    public async Task ProcessPhrasesFile_ShouldUpdatePhrases()
    {
        // Arrange
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Size).Returns(1024); // 1KB file
        mockFile.Setup(f => f.Name).Returns("phrases.txt");
        
        var content = string.Join(", ", Enumerable.Range(1, 16).Select(i => $"phrase{i}"));
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        mockFile.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(stream);

        var browserFile = mockFile.Object;
        var args = new InputFileChangeEventArgs(new[] { browserFile });

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                It.IsAny<string>(),
                It.IsAny<object[]>()))
            .Returns(new ValueTask<IJSVoidResult>());

        // Act
        await _component.ProcessPhrasesFile(args);

        // Assert
        Assert.Equal("phrases.txt", _component.GetLastUploadedPhrasesFile());
        Assert.True(_component.GetPhrasesLoaded());
        var phrases = _component.GetPhrases();
        Assert.Equal(16, phrases.Length);
        Assert.Contains("phrase1", phrases);
        Assert.Contains("phrase16", phrases);
    }

    [Fact]
    public async Task ProcessPhrasesFile_ShouldResetGameState()
    {
        // Arrange
        // First, set up some existing game state
        _component.SetPhrasesLoaded(true);
        _component.SetPhrases(new string[] { 
            "old1", "old2", "old3", "old4",
            "old5", "old6", "old7", "old8",
            "old9", "old10", "old11", "old12",
            "old13", "old14", "old15", "old16"
        });
        _component.ProcessContent("old1 old2 old3"); // Add some matches

        // Now set up the new phrases file
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Size).Returns(1024);
        mockFile.Setup(f => f.Name).Returns("new_phrases.txt");
        
        var content = string.Join(", ", Enumerable.Range(1, 16).Select(i => $"new{i}"));
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        mockFile.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(stream);

        var browserFile = mockFile.Object;
        var args = new InputFileChangeEventArgs(new[] { browserFile });

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                It.IsAny<string>(),
                It.IsAny<object[]>()))
            .Returns(new ValueTask<IJSVoidResult>());

        // Act
        await _component.ProcessPhrasesFile(args);

        // Assert
        Assert.True(_component.GetPhrasesLoaded());
        Assert.Empty(_component.GetMatchedWords()); // Matches should be cleared
        Assert.Empty(_component.GetHighlightedCells()); // Highlights should be cleared
        Assert.Empty(_component.GetExtractedWords()); // Extracted words should be cleared
        var phrases = _component.GetPhrases();
        Assert.Contains("new1", phrases); // New phrases should be loaded
        Assert.DoesNotContain("old1", phrases); // Old phrases should be gone
    }
}

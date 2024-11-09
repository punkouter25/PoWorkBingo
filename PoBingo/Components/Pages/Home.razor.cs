using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Xunit;

namespace PoBingo.Components.Pages.Tests
{
    public class HomeTests
    {
        private readonly IJSRuntime _jsRuntime;

        public HomeTests()
        {
            _jsRuntime = new JSRuntimeStub();
        }

        [Fact]
        public async Task ProcessFile_ValidFile_ProcessesSuccessfully()
        {
            // Arrange
            var home = new Home { JSRuntime = _jsRuntime };
            var fileContent = "Smoke Test\nThat makes sense\nConfused\nThat's fair";
            var file = new TestFile("test.txt", fileContent);

            // Act
            await home.ProcessFile(new InputFileChangeEventArgs(file));

            // Assert
            Assert.Contains("smoke test", home.MatchedWords);
            Assert.Contains("that makes sense", home.MatchedWords);
            Assert.Contains("confused", home.MatchedWords);
            Assert.Contains("that's fair", home.MatchedWords);
        }

        [Fact]
        public async Task ProcessFile_FileTooLarge_ShowsAlert()
        {
            // Arrange
            var home = new Home { JSRuntime = _jsRuntime };
            var fileContent = new string('a', 6 * 1024 * 1024); // 6MB file
            var file = new TestFile("large.txt", fileContent);

            // Act
            await home.ProcessFile(new InputFileChangeEventArgs(file));

            // Assert
            var jsRuntimeStub = (JSRuntimeStub)_jsRuntime;
            Assert.Contains("File size must be less than 5MB", jsRuntimeStub.Alerts);
        }

        [Fact]
        public async Task ProcessFile_ErrorDuringProcessing_ShowsError()
        {
            // Arrange
            var home = new Home { JSRuntime = _jsRuntime };
            var file = new TestFile("error.txt", null); // Simulate error

            // Act
            await home.ProcessFile(new InputFileChangeEventArgs(file));

            // Assert
            var jsRuntimeStub = (JSRuntimeStub)_jsRuntime;
            Assert.Contains("Error processing file", jsRuntimeStub.Alerts);
        }

        private class TestFile : IBrowserFile
        {
            public TestFile(string name, string content)
            {
                Name = name;
                Content = content;
                Size = content?.Length ?? 0;
            }

            public string Name { get; }
            public DateTimeOffset LastModified => DateTimeOffset.Now;
            public long Size { get; }
            public string ContentType => "text/plain";
            public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            {
                if (Content == null)
                {
                    throw new Exception("Simulated error");
                }

                return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Content));
            }

            private string Content { get; }
        }

        private class JSRuntimeStub : IJSRuntime
        {
            public List<string> Alerts { get; } = new List<string>();

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            {
                if (identifier == "alert" && args?.Length > 0 && args[0] is string message)
                {
                    Alerts.Add(message);
                }

                return new ValueTask<TValue>(default(TValue)!);
            }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                return InvokeAsync<TValue>(identifier, args);
            }
        }
    }
}

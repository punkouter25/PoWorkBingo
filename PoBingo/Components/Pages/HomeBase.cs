using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Text.RegularExpressions;

namespace PoBingo.Components.Pages;

public class HomeBase : ComponentBase
{
    [Inject]
    protected IJSRuntime JSRuntime { get; set; } = default!;

    protected bool showBingoModal = false;
    protected HashSet<string> matchedWords = new();
    protected List<int> highlightedCells = new();
    protected string? lastUploadedFile;
    protected string? lastUploadedPhrasesFile;
    protected string? debugInfo;
    protected Dictionary<string, int> extractedWords = new();
    protected string[] phrases = new string[16];
    protected string[] uploadedPhrases = new string[16];
    protected bool phrasesLoaded = false;

    protected bool IsHighlighted(int index) => highlightedCells.Contains(index);
    
    protected bool IsMatched(string phrase)
    {
        if (string.IsNullOrEmpty(phrase)) return false;
        var cleanPhrase = phrase.ToLower().Replace("?", "");
        return matchedWords.Contains(cleanPhrase);
    }

    public virtual async Task ProcessPhrasesFile(InputFileChangeEventArgs e)
    {
        try
        {
            var file = e.File;
            lastUploadedPhrasesFile = file.Name;
            debugInfo = $"Processing phrases file: {file.Name}\n";

            if (file.Size > 5 * 1024 * 1024) // 5MB limit
            {
                await JSRuntime.InvokeVoidAsync("alert", "File size must be less than 5MB");
                return;
            }

            using var reader = new StreamReader(file.OpenReadStream());
            var content = await reader.ReadToEndAsync();

            try
            {
                // Parse and update phrases
                phrases = ParseUploadedPhrases(content);
                phrasesLoaded = true;
                
                // Reset game state when new phrases are loaded
                matchedWords.Clear();
                highlightedCells.Clear();
                extractedWords.Clear();
                lastUploadedFile = null;
                
                await JSRuntime.InvokeVoidAsync("console.log", "Phrases file processed:", file.Name);
                await JSRuntime.InvokeVoidAsync("console.log", "Loaded phrases:", string.Join(", ", phrases));
            }
            catch (InvalidOperationException ex)
            {
                phrasesLoaded = false;
                await JSRuntime.InvokeVoidAsync("alert", ex.Message);
            }
        }
        catch (Exception ex)
        {
            phrasesLoaded = false;
            debugInfo = $"Error: {ex.Message}\n{ex.StackTrace}";
            await JSRuntime.InvokeVoidAsync("console.error", "Error processing phrases file:", ex.Message);
            await JSRuntime.InvokeVoidAsync("alert", "Error processing phrases file: " + ex.Message);
        }
    }

    public virtual async Task ProcessFile(InputFileChangeEventArgs e)
    {
        if (!phrasesLoaded)
        {
            await JSRuntime.InvokeVoidAsync("alert", "Please upload phrases file first.");
            return;
        }

        try
        {
            var file = e.File;
            lastUploadedFile = file.Name;
            debugInfo = $"Processing file: {file.Name}\n";

            if (file.Size > 5 * 1024 * 1024) // 5MB limit
            {
                await JSRuntime.InvokeVoidAsync("alert", "File size must be less than 5MB");
                return;
            }

            using var reader = new StreamReader(file.OpenReadStream());
            var content = await reader.ReadToEndAsync();

            debugInfo += $"File content length: {content.Length} characters\n\n";

            // Reset match state but keep phrases
            matchedWords.Clear();
            highlightedCells.Clear();
            extractedWords.Clear();

            // Extract and store words
            ExtractWords(content);

            // Process the content and check for matches
            ProcessContent(content);

            await JSRuntime.InvokeVoidAsync("console.log", "File processed:", file.Name);
            await JSRuntime.InvokeVoidAsync("console.log", "Extracted words:", string.Join(", ", extractedWords.Keys));
            await JSRuntime.InvokeVoidAsync("console.log", "Matched words:", string.Join(", ", matchedWords));
        }
        catch (Exception ex)
        {
            debugInfo = $"Error: {ex.Message}\n{ex.StackTrace}";
            await JSRuntime.InvokeVoidAsync("console.error", "Error processing file:", ex.Message);
            await JSRuntime.InvokeVoidAsync("alert", "Error processing file: " + ex.Message);
        }
    }

    public virtual void ExtractWords(string content)
    {
        // List of common words to ignore
        var commonWords = new HashSet<string>
        {
            "i", "me", "my", "myself", "we", "our", "ours", "ourselves", "you", "your", "yours", "yourself", "yourselves",
            "he", "him", "his", "himself", "she", "her", "hers", "herself", "it", "its", "itself", "they", "them", "their",
            "theirs", "themselves", "what", "which", "who", "whom", "this", "that", "these", "those", "am", "is", "are",
            "was", "were", "be", "been", "being", "have", "has", "had", "having", "do", "does", "did", "doing", "a", "an",
            "the", "and", "but", "if", "or", "because", "as", "until", "while", "of", "at", "by", "for", "with", "about",
            "against", "between", "into", "through", "during", "before", "after", "above", "below", "to", "from", "up",
            "down", "in", "out", "on", "off", "over", "under", "again", "further", "then", "once", "here", "there", "when",
            "where", "why", "how", "all", "any", "both", "each", "few", "more", "most", "other", "some", "such", "no",
            "nor", "not", "only", "own", "same", "so", "than", "too", "very", "s", "t", "can", "will", "just", "don",
            "should", "now", "speaker"
        };

        // Split content into words, removing punctuation and extra whitespace
        var words = Regex.Split(content.ToLower(), @"[\p{P}\s]+")
            .Where(w => !string.IsNullOrWhiteSpace(w) && !commonWords.Contains(w) && w.Length > 3 && !Regex.IsMatch(w, @"^\d+$"))
            .GroupBy(w => w)
            .ToDictionary(g => g.Key, g => g.Count());

        extractedWords = words.OrderByDescending(w => w.Value).ToDictionary(w => w.Key, w => w.Value);
        debugInfo += $"Total words extracted: {words.Count}\n\n";
    }

    public virtual void ProcessContent(string content)
    {
        if (string.IsNullOrEmpty(content)) return;
        
        content = content.ToLower();
        debugInfo += "Checking for matches...\n";

        foreach (var phrase in phrases)
        {
            if (string.IsNullOrEmpty(phrase)) continue;
            
            var cleanPhrase = phrase.ToLower().Replace("?", "");
            // Use word boundary regex pattern to ensure exact phrase matches
            var pattern = $@"\b{Regex.Escape(cleanPhrase)}\b";
            if (Regex.IsMatch(content, pattern))
            {
                matchedWords.Add(cleanPhrase);
                debugInfo += $"Found match: {phrase}\n";
            }
        }

        debugInfo += $"\nTotal matches found: {matchedWords.Count}\n";
        CheckForBingo();
    }

    public virtual void CheckForBingo()
    {
        highlightedCells.Clear(); // Clear any previous highlights
        
        var winningCombinations = new List<int[]>
        {
            // Rows
            new[] { 0, 1, 2, 3 },
            new[] { 4, 5, 6, 7 },
            new[] { 8, 9, 10, 11 },
            new[] { 12, 13, 14, 15 },
            // Columns
            new[] { 0, 4, 8, 12 },
            new[] { 1, 5, 9, 13 },
            new[] { 2, 6, 10, 14 },
            new[] { 3, 7, 11, 15 },
            // Diagonals
            new[] { 0, 5, 10, 15 },
            new[] { 3, 6, 9, 12 }
        };

        debugInfo += "\nChecking for winning combinations...\n";

        foreach (var combination in winningCombinations)
        {
            var combinationPhrases = combination.Select(i => phrases[i].ToLower().Replace("?", "")).ToList();
            var allMatched = combinationPhrases.All(phrase => !string.IsNullOrEmpty(phrase) && matchedWords.Contains(phrase));

            if (allMatched)
            {
                // We have a bingo!
                highlightedCells.AddRange(combination);
                showBingoModal = true;
                debugInfo += $"BINGO found! Winning combination: {string.Join(", ", combinationPhrases)}\n";
                break;
            }
        }
    }

    public virtual void CloseModal()
    {
        showBingoModal = false;
    }

    public virtual string[] ParseUploadedPhrases(string content)
    {
        if (string.IsNullOrEmpty(content))
            throw new InvalidOperationException("Content cannot be empty.");
            
        var phrases = content.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(p => p.Trim())
                             .ToArray();

        if (phrases.Length != 16)
        {
            throw new InvalidOperationException("The uploaded file must contain exactly 16 phrases separated by commas.");
        }

        return phrases;
    }

    public virtual async Task LoadDefaultPhrases()
    {
        try
        {
            var defaultPhrases = await File.ReadAllTextAsync("Phrases.txt");
            phrases = ParseUploadedPhrases(defaultPhrases);
            phrasesLoaded = true;

            // Reset game state when default phrases are loaded
            matchedWords.Clear();
            highlightedCells.Clear();
            extractedWords.Clear();
            lastUploadedFile = null;

            await JSRuntime.InvokeVoidAsync("console.log", "Default phrases loaded:", string.Join(", ", phrases));
        }
        catch (Exception ex)
        {
            phrasesLoaded = false;
            debugInfo = $"Error: {ex.Message}\n{ex.StackTrace}";
            await JSRuntime.InvokeVoidAsync("console.error", "Error loading default phrases:", ex.Message);
            await JSRuntime.InvokeVoidAsync("alert", "Error loading default phrases: " + ex.Message);
        }
    }

    // For testing purposes
    public virtual HashSet<string> GetMatchedWords() => matchedWords;
    public virtual List<int> GetHighlightedCells() => highlightedCells;
    public virtual Dictionary<string, int> GetExtractedWords() => extractedWords;
    public virtual bool GetShowBingoModal() => showBingoModal;
}

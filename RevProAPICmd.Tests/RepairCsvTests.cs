using FluentAssertions;

namespace RevProAPICmd.Tests;

public class RepairCsvTests : IDisposable
{
    private readonly string _tempDir;

    public RepairCsvTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"RepairCsvTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string GetRepairedPath(string originalPath)
    {
        return originalPath.Replace(".csv", "_repaired.csv");
    }

    [Fact]
    public void RepairCsvFile_CleanFile_RepairedFileMatchesOriginal()
    {
        // Arrange — valid UTF-8 CSV
        var csvPath = Path.Combine(_tempDir, "clean.csv");
        var content = "Name,Value\nAlice,100\nBob,200\n";
        var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
        File.WriteAllBytes(csvPath, contentBytes);

        // Act
        RevproAPICmd.RevproAPI.RepairCsvFile(csvPath);

        // Assert — repaired file is always created; should be byte-identical for clean input
        var repairedPath = GetRepairedPath(csvPath);
        File.Exists(repairedPath).Should().BeTrue("RepairCsvFile always creates the _repaired.csv file");

        var originalBytes = File.ReadAllBytes(csvPath);
        var repairedBytes = File.ReadAllBytes(repairedPath);
        repairedBytes.Should().Equal(originalBytes, "a clean file should produce an identical repaired copy");
    }

    [Fact]
    public void RepairCsvFile_FileWithInvalidBytes_RemovesInvalidBytesPreservesValid()
    {
        // Arrange — mix valid ASCII with invalid UTF-8 bytes
        var csvPath = Path.Combine(_tempDir, "invalid.csv");

        // Build: "Hello" + 0xFF + 0xFE + ",World\n"
        var hello = System.Text.Encoding.ASCII.GetBytes("Hello");
        var comma = System.Text.Encoding.ASCII.GetBytes(",World\n");
        var bytes = new byte[hello.Length + 2 + comma.Length];
        hello.CopyTo(bytes, 0);
        bytes[hello.Length] = 0xFF;
        bytes[hello.Length + 1] = 0xFE;
        comma.CopyTo(bytes, hello.Length + 2);
        File.WriteAllBytes(csvPath, bytes);

        // Act
        RevproAPICmd.RevproAPI.RepairCsvFile(csvPath);

        // Assert
        var repairedPath = GetRepairedPath(csvPath);
        File.Exists(repairedPath).Should().BeTrue();

        var repairedContent = File.ReadAllBytes(repairedPath);
        // The invalid bytes 0xFF and 0xFE should be stripped; valid ASCII remains
        repairedContent.Should().NotContain(0xFF);
        repairedContent.Should().NotContain(0xFE);

        var repairedText = System.Text.Encoding.UTF8.GetString(repairedContent);
        repairedText.Should().Contain("Hello");
        repairedText.Should().Contain(",World");
    }

    [Fact]
    public void RepairCsvFile_EmptyFile_HandlesGracefully()
    {
        // Arrange
        var csvPath = Path.Combine(_tempDir, "empty.csv");
        File.WriteAllBytes(csvPath, Array.Empty<byte>());

        // Act
        var act = () => RevproAPICmd.RevproAPI.RepairCsvFile(csvPath);

        // Assert — should not throw
        act.Should().NotThrow();

        var repairedPath = GetRepairedPath(csvPath);
        File.Exists(repairedPath).Should().BeTrue();
        File.ReadAllBytes(repairedPath).Should().BeEmpty();
    }

    [Fact]
    public void RepairCsvFile_OnlyInvalidBytes_RepairedFileIsEmpty()
    {
        // Arrange — file with nothing but invalid UTF-8 lead bytes
        var csvPath = Path.Combine(_tempDir, "allinvalid.csv");
        File.WriteAllBytes(csvPath, new byte[] { 0xFF, 0xFF, 0xFF, 0xFE, 0xC0, 0xC1 });

        // Act
        RevproAPICmd.RevproAPI.RepairCsvFile(csvPath);

        // Assert
        var repairedPath = GetRepairedPath(csvPath);
        File.Exists(repairedPath).Should().BeTrue();
        File.ReadAllBytes(repairedPath).Should().BeEmpty("all bytes are invalid and should be stripped");
    }

    [Fact]
    public void RepairCsvFile_MultiByteUtf8_PreservesAccentedCharsAndEmoji()
    {
        // Arrange — CSV with multi-byte UTF-8: accented chars (2-byte), CJK (3-byte), emoji (4-byte)
        var csvPath = Path.Combine(_tempDir, "multibyte.csv");
        var content = "Name,City,Note\ncafé,Zürich,你好\ntest,data,\U0001F680\n";
        File.WriteAllBytes(csvPath, System.Text.Encoding.UTF8.GetBytes(content));

        // Act
        RevproAPICmd.RevproAPI.RepairCsvFile(csvPath);

        // Assert — repaired file should be byte-identical (no valid bytes removed)
        var repairedPath = GetRepairedPath(csvPath);
        File.Exists(repairedPath).Should().BeTrue();

        var originalBytes = File.ReadAllBytes(csvPath);
        var repairedBytes = File.ReadAllBytes(repairedPath);
        repairedBytes.Should().Equal(originalBytes, "valid multi-byte UTF-8 characters must survive repair unchanged");
    }
}

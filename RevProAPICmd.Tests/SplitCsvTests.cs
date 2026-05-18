using FluentAssertions;

namespace RevProAPICmd.Tests;

public class SplitCsvTests : IDisposable
{
    private readonly string _tempDir;

    public SplitCsvTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"SplitCsvTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateCsvFile(string fileName, int dataRowCount)
    {
        var path = Path.Combine(_tempDir, fileName);
        using var sw = new StreamWriter(path);
        sw.WriteLine("Col1,Col2,Col3");
        for (int i = 1; i <= dataRowCount; i++)
        {
            sw.WriteLine($"val{i}_1,val{i}_2,val{i}_3");
        }
        return path;
    }

    private List<string> GetSplitFiles(string originalPath)
    {
        var baseName = Path.GetFileNameWithoutExtension(originalPath);
        return Directory.GetFiles(_tempDir, $"{baseName}_splitPart*.csv")
                        .OrderBy(f => f)
                        .ToList();
    }

    [Fact]
    public void SplitCsv_ValidSplit_Creates10FilesWithAllRows()
    {
        // Arrange
        var csvPath = CreateCsvFile("data100.csv", 100);

        // Act
        RevproAPICmd.RevproAPI.SplitCsv(csvPath, 10, false);

        // Assert
        var splitFiles = GetSplitFiles(csvPath);
        splitFiles.Should().HaveCount(10);

        int totalDataRows = 0;
        foreach (var file in splitFiles)
        {
            var lines = File.ReadAllLines(file);
            lines[0].Should().Be("Col1,Col2,Col3", "every split file should start with the header");
            totalDataRows += lines.Length - 1; // subtract header
        }
        totalDataRows.Should().Be(100);
    }

    [Fact]
    public void SplitCsv_HeaderOnly_CreatesNoSplitFiles()
    {
        // Arrange — CSV with header but zero data rows
        var csvPath = CreateCsvFile("headeronly.csv", 0);

        // Act
        RevproAPICmd.RevproAPI.SplitCsv(csvPath, 10, false);

        // Assert
        var splitFiles = GetSplitFiles(csvPath);
        splitFiles.Should().BeEmpty("a header-only file has nothing to split");
    }

    [Fact]
    public void SplitCsv_MissingFile_DoesNotThrow()
    {
        // Arrange
        var fakePath = Path.Combine(_tempDir, "nonexistent.csv");

        // Act
        var act = () => RevproAPICmd.RevproAPI.SplitCsv(fakePath, 10, false);

        // Assert
        act.Should().NotThrow("missing file should be handled gracefully");
    }

    [Fact]
    public void SplitCsv_SingleDataRow_CreatesOneSplitFile()
    {
        // Arrange
        var csvPath = CreateCsvFile("single.csv", 1);

        // Act
        RevproAPICmd.RevproAPI.SplitCsv(csvPath, 10, false);

        // Assert
        var splitFiles = GetSplitFiles(csvPath);
        splitFiles.Should().HaveCount(1);

        var lines = File.ReadAllLines(splitFiles[0]);
        lines[0].Should().Be("Col1,Col2,Col3");
        lines.Should().HaveCount(2, "header + 1 data row");
    }

    [Fact]
    public void SplitCsv_FewerRowsThanSplits_CreatesOnlyNeededFiles()
    {
        // Arrange — 3 data rows, splitcount=10
        var csvPath = CreateCsvFile("few.csv", 3);

        // Act
        RevproAPICmd.RevproAPI.SplitCsv(csvPath, 10, false);

        // Assert
        var splitFiles = GetSplitFiles(csvPath);
        splitFiles.Should().HaveCount(3, "should create only 3 files for 3 rows");

        int totalDataRows = 0;
        foreach (var file in splitFiles)
        {
            var lines = File.ReadAllLines(file);
            lines[0].Should().Be("Col1,Col2,Col3");
            totalDataRows += lines.Length - 1;
        }
        totalDataRows.Should().Be(3);
    }

    [Fact]
    public void SplitCsv_DeleteAfterSplit_RemovesOriginalFile()
    {
        // Arrange
        var csvPath = CreateCsvFile("todelete.csv", 10);

        // Act
        RevproAPICmd.RevproAPI.SplitCsv(csvPath, 2, deleteaftersplit: true);

        // Assert
        File.Exists(csvPath).Should().BeFalse("original file should be deleted when deleteaftersplit is true");
        var splitFiles = GetSplitFiles(csvPath);
        splitFiles.Should().HaveCount(2);
    }
}

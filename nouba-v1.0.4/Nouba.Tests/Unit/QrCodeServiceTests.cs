using Nouba.Services;

namespace Nouba.Tests.Unit;

public class QrCodeServiceTests
{
    private readonly QrCodeService _svc = new();

    // ── NewPublicId ──────────────────────────────────────────────────

    [Fact]
    public void NewPublicId_DefaultLength_Returns8Chars()
    {
        Assert.Equal(8, QrCodeService.NewPublicId().Length);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    public void NewPublicId_CustomLength_MatchesExpected(int length)
    {
        Assert.Equal(length, QrCodeService.NewPublicId(length).Length);
    }

    [Fact]
    public void NewPublicId_NeverContainsAmbiguousChars()
    {
        // 0/O/1/I/l sont exclus de l'alphabet pour éviter les confusions visuelles
        for (int i = 0; i < 100; i++)
        {
            var id = QrCodeService.NewPublicId();
            Assert.DoesNotContain('0', id);
            Assert.DoesNotContain('O', id);
            Assert.DoesNotContain('1', id);
            Assert.DoesNotContain('I', id);
            Assert.DoesNotContain('l', id);
        }
    }

    [Fact]
    public void NewPublicId_IsUpperCase()
    {
        for (int i = 0; i < 20; i++)
        {
            var id = QrCodeService.NewPublicId();
            Assert.Equal(id, id.ToUpperInvariant());
        }
    }

    [Fact]
    public void NewPublicId_ProducesUniqueValues()
    {
        var ids = Enumerable.Range(0, 50).Select(_ => QrCodeService.NewPublicId()).ToHashSet();
        Assert.True(ids.Count >= 48, "Expected near-total uniqueness over 50 calls");
    }

    // ── GeneratePng ──────────────────────────────────────────────────

    [Fact]
    public void GeneratePng_ValidContent_ReturnsPngBytes()
    {
        var bytes = _svc.GeneratePng("http://localhost:5000/suivi/ABC12345");

        Assert.NotEmpty(bytes);
        // PNG magic bytes: 89 50 4E 47
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x4E, bytes[2]);
        Assert.Equal(0x47, bytes[3]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GeneratePng_EmptyContent_ReturnsEmptyArray(string content)
    {
        Assert.Empty(_svc.GeneratePng(content));
    }

    [Fact]
    public void GeneratePng_LargerModule_ProducesLargerImage()
    {
        var small = _svc.GeneratePng("test", pixelsPerModule: 5);
        var large = _svc.GeneratePng("test", pixelsPerModule: 20);

        Assert.True(large.Length > small.Length);
    }

    // ── GenerateMatrix ───────────────────────────────────────────────

    [Fact]
    public void GenerateMatrix_ValidContent_ReturnsSquareMatrix()
    {
        var matrix = _svc.GenerateMatrix("TEST");

        Assert.True(matrix.GetLength(0) > 0);
        Assert.Equal(matrix.GetLength(0), matrix.GetLength(1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateMatrix_EmptyContent_ReturnsEmptyMatrix(string content)
    {
        var matrix = _svc.GenerateMatrix(content);

        Assert.Equal(0, matrix.GetLength(0));
    }

    [Fact]
    public void GenerateMatrix_ContainsBoolValues()
    {
        var matrix = _svc.GenerateMatrix("Nouba");
        int n = matrix.GetLength(0);
        bool hasTrue = false, hasFalse = false;

        for (int y = 0; y < n && (!hasTrue || !hasFalse); y++)
            for (int x = 0; x < n && (!hasTrue || !hasFalse); x++)
            {
                if (matrix[y, x]) hasTrue = true;
                else hasFalse = true;
            }

        Assert.True(hasTrue, "Matrix should contain black pixels");
        Assert.True(hasFalse, "Matrix should contain white pixels");
    }
}

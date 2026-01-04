using Stardust;
using Xunit;

namespace ClientTest;

public class StarClientDiskStatsTests
{
    [Fact]
    public void ParseDiskStats_VdaFormatWithManyFields_ShouldParseReadsAndWritesAndIoTicks()
    {
        var content = " 253       0 vda 8924704 2893484 282556846 8929408 28010583 29775871 545694282 112373023 0 18426051 121359468 0 0 0 0 1165370 57036 8769609 78935611 0\n" +
                      " 253       1 vda1 57 0 952 53 0 0 0 0 0 67 53 0 0 0 0 0 0 53 0 0\n" +
                      " 253       2 vda2 171 20 9617 183 2 0 9 1 0 155 184 0 0 0 0 0 0 182 1 0\n" +
                      " 253       3 vda3 8924400 2893464 282543429 8929060 28008683 29775871 545694273 112372195 0 18425784 \n";

        var (reads, writes, ioTicks) = StarClient.ParseDiskStats(content);

        Assert.Equal(8924704, reads);
        Assert.Equal(28010583, writes);
        Assert.Equal(18426051, ioTicks);
    }

    [Fact]
    public void ParseDiskStats_VdaFormatWith18Fields_ShouldParseReadsAndWritesAndIoTicks()
    {
        var content = " 253       0 vda 542435 426 38161948 231054 13305384 2179791 404809589 30235516 0 4654387 31595475 0 0 0 0 1022775 1128905\n" +
                      " 253       1 vda1 542351 426 38158652 231037 13305382 2179791 404809589 30235514 0 6767141 30466552 0 0 0 0 0 0\n" +
                      "  11       0 sr0 79 0 628 11 0 0 0 0 0 10 11 0 0 0 0 0 0\n";

        var (reads, writes, ioTicks) = StarClient.ParseDiskStats(content);

        Assert.Equal(542435, reads);
        Assert.Equal(13305384, writes);
        Assert.Equal(4654387, ioTicks);
    }
}

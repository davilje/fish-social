#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Globalization;
using FishSocial.Desktop;
using NUnit.Framework;

namespace FishSocial.Tests.Editor
{
    public sealed class FishSizeFormatTests
    {
        [TestCase(0.02, "2.0cm")]
        [TestCase(0.123, "12.3cm")]
        [TestCase(0.999, "99.9cm")]
        [TestCase(1.0, "1.00m")]
        [TestCase(1.005, "1.01m")]
        [TestCase(1.234, "1.23m")]
        [TestCase(9.994, "9.99m")]
        [TestCase(9.995, "10.0m")]
        [TestCase(10.0, "10.0m")]
        [TestCase(12.35, "12.4m")]
        [TestCase(40.0, "40.0m")]
        public void UsesAdaptiveLengthPrecision(double size, string expected)
        {
            Assert.AreEqual(expected, DesktopGameData.FormatFishSize(size));
        }

        [Test]
        public void DecimalSeparatorDoesNotFollowSystemCulture()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                Assert.AreEqual("12.3cm", DesktopGameData.FormatFishSize(.123f));
                Assert.AreEqual("1.23m", DesktopGameData.FormatFishSize(1.234f));
            }
            finally { CultureInfo.CurrentCulture = original; }
        }
    }
}
#endif

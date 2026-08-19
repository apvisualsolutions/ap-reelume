// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Metadata;

/// <summary>
/// TMDB's terms ask for their logo, not only for their sentence.
/// </summary>
/// <remarks>
/// The attribution wording has been pinned since the legal review; the logo was the last condition
/// left open. Their terms want the mark displayed less prominently than the product's own, so
/// "less prominent" is measured here rather than asserted: the drawn height is compared against the
/// size the product name is actually rendered at.
/// <para>
/// The mark is a trademark, so what the application draws has to be TMDB's own vector and not a
/// lookalike. The versioned file is checked against the digest TMDB itself puts in the asset's URL,
/// and the geometry in the view is checked against that file — an approximation would pass a
/// screenshot review and fail here.
/// </para>
/// </remarks>
public sealed class TmdbLogoTests
{
    /// <summary>
    /// The digest TMDB publishes as part of the asset's own address, so the file can be shown to be
    /// theirs without trusting whoever downloaded it:
    /// <c>/assets/2/v4/logos/v2/blue_short-&lt;digest&gt;.svg</c>.
    /// </summary>
    private const string OfficialDigest =
        "8e7b30f73a4020692ccca9c88bafe5dcb6f8a62a4c6bc55cd9ba82bb2cd95f6c";

    private const string AssetPath = "src/ApSolutions.LocalMedia.Presentation/Assets/tmdb-logo.svg";

    private const string CreditsPath = "src/ApSolutions.LocalMedia.Presentation/About/CreditsView.axaml";

    private const string ShellPath = "src/ApSolutions.LocalMedia.Presentation/Shell/ShellView.axaml";

    private const string TokensPath =
        "src/ApSolutions.LocalMedia.Presentation/Theme/DesignTokens.axaml";

    [Fact]
    public void The_versioned_logo_is_the_file_TMDB_publishes()
    {
        var path = FromRoot(AssetPath);
        Assert.True(File.Exists(path), $"{AssetPath} is missing, so the credits draw a mark from nowhere.");

        var digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
            .ToLower(CultureInfo.InvariantCulture);

        Assert.Equal(OfficialDigest, digest);
    }

    /// <summary>
    /// Avalonia draws no SVG of its own, and pulling in a renderer for one 16-pixel mark would put
    /// half a dozen more packages — and their licences — inside the artifact. The view carries the
    /// geometry instead, and it has to be the geometry of the file above, character for character.
    /// </summary>
    [Fact]
    public void The_credits_draw_the_vector_TMDB_publishes_rather_than_a_lookalike()
    {
        var official = Attribute(File.ReadAllText(FromRoot(AssetPath)), "d");
        var drawn = Attribute(File.ReadAllText(FromRoot(CreditsPath)), "Data");

        Assert.False(string.IsNullOrWhiteSpace(official), "The versioned asset carries no path.");
        Assert.Equal(official, drawn);
    }

    /// <summary>The colours are part of the mark, so the three stops travel with the shape.</summary>
    [Fact]
    public void The_credits_reproduce_the_gradient_of_the_mark()
    {
        var svg = File.ReadAllText(FromRoot(AssetPath));
        var credits = File.ReadAllText(FromRoot(CreditsPath));
        var stops = Regex.Matches(svg, @"stop-color=""(?<colour>#[0-9a-fA-F]{6})""", RegexOptions.None, Timeout)
            .Select(match => match.Groups["colour"].Value)
            .ToArray();

        Assert.Equal(3, stops.Length);
        foreach (var colour in stops)
        {
            Assert.Contains(colour, credits, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// "Less prominent than your own" is the condition, so the mark is drawn smaller than the name of
    /// the product itself. Both numbers are read from the views rather than restated here: a redesign
    /// that grows the logo or shrinks the product name has to come past this.
    /// </summary>
    [Fact]
    public void The_logo_is_drawn_smaller_than_the_product_name()
    {
        var logoHeight = Number(Attribute(File.ReadAllText(FromRoot(CreditsPath)), "Height"));
        var productName = Number(ProductNameFontSize());

        Assert.True(logoHeight > 0, "The logo declares no height, so nothing bounds how large it draws.");
        Assert.True(
            productName > 0,
            "The product name's size read as zero, which means this check stopped being able to read "
                + "it rather than that the name shrank.");
        Assert.True(
            logoHeight < productName,
            $"The TMDB logo is drawn at {logoHeight} against a product name at {productName}, "
                + "which is not less prominent.");
    }

    /// <summary>
    /// A screen reader has to be able to announce it, and it identifies where the data comes from
    /// rather than inviting anybody to go there — so nothing in this view is clickable.
    /// </summary>
    [Fact]
    public void The_logo_is_announced_and_leads_nowhere()
    {
        var credits = File.ReadAllText(FromRoot(CreditsPath));

        Assert.Contains("AboutTmdbLogoAlt", credits, StringComparison.Ordinal);
        foreach (var clickable in new[] { "HyperlinkButton", "<Button", "Command=" })
        {
            Assert.DoesNotContain(clickable, credits, StringComparison.Ordinal);
        }
    }

    /// <summary>The alternative text exists in both languages; half the readers use the other one.</summary>
    [Theory]
    [InlineData("Strings.es.axaml")]
    [InlineData("Strings.en.axaml")]
    public void The_alternative_text_exists_in_both_languages(string resourceFile)
    {
        var resources = File.ReadAllText(FromRoot(
            $"src/ApSolutions.LocalMedia.Presentation/Resources/{resourceFile}"));
        var value = Regex.Match(
            resources,
            @"x:Key=""AboutTmdbLogoAlt"">(?<text>[^<]+)<",
            RegexOptions.None,
            Timeout);

        Assert.True(value.Success, $"{resourceFile} declares no AboutTmdbLogoAlt.");
        Assert.Contains("TMDB", value.Groups["text"].Value, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private static string FromRoot(string relativePath) => Path.Combine(
        RepositoryLayout.Root,
        relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string Attribute(string markup, string name) =>
        Regex.Match(markup, $@"\b{name}=""(?<value>[^""]+)""", RegexOptions.None, Timeout)
            .Groups["value"].Value;

    /// <summary>The size the product's own name is rendered at, read from the view that draws it.</summary>
    private static string ProductNameFontSize()
    {
        var shell = File.ReadAllText(FromRoot(ShellPath));
        var index = shell.IndexOf("ProductDisplayName", StringComparison.Ordinal);
        Assert.True(index > 0, "The shell no longer draws the product name, so there is nothing to compare against.");

        // The TextBlock's own attributes, taken from the element that carries the binding.
        var start = shell.LastIndexOf("<TextBlock", index, StringComparison.Ordinal);
        Assert.True(start >= 0, "The product name is no longer drawn by a TextBlock.");
        return Resolve(Attribute(shell[start..(index + "ProductDisplayName".Length)], "FontSize"));
    }

    /// <summary>
    /// A size the view takes from the type scale, resolved to the number the scale holds.
    /// </summary>
    /// <remarks>
    /// Views stopped writing literal font sizes on 2026-08-19: thirteen distinct literals across
    /// thirty files became five tokens, and the product name went from <c>24</c> to
    /// <c>{DynamicResource FontSizeSubtitle}</c>. Reading only literals, this check parsed that as
    /// zero and went red — which was the right colour for the wrong reason, because 16 is still less
    /// than the 20 the token holds. Following the indirection is what keeps it measuring the drawn
    /// size rather than the spelling of an attribute.
    /// </remarks>
    private static string Resolve(string value)
    {
        var reference = Regex.Match(
            value,
            @"^\{(?:Dynamic|Static)Resource\s+(?<key>[A-Za-z0-9]+)\}$",
            RegexOptions.None,
            Timeout);
        if (!reference.Success)
        {
            return value;
        }

        var key = reference.Groups["key"].Value;
        var declared = Regex.Match(
            File.ReadAllText(FromRoot(TokensPath)),
            $@"x:Key=""{key}"">(?<value>[^<]+)<",
            RegexOptions.None,
            Timeout);
        Assert.True(
            declared.Success,
            $"The product name asks the theme for {key} and the theme does not declare it, so its "
                + "drawn size cannot be known and this comparison would silently measure nothing.");
        return declared.Groups["value"].Value;
    }

    private static double Number(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}

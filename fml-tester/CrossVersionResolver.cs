using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Utility;


namespace Microsoft.Health.Fhir.CodeGen.Tests;

// TODO: @brianpos - there are a lot of similar functions in DefinitionCollectionTx, we should discuss on where we can consolidate
public static class CanonicalExtensions
{
    public static string? Version(this Canonical url)
    {
        int indexOfVersion = url.Value.IndexOf("|");
        if (indexOfVersion == -1)
            return null;
        var result = url.Value.Substring(indexOfVersion + 1);
        int indexOfFragment = result.IndexOf("#");
        if (indexOfFragment != -1)
            return result.Substring(0, indexOfFragment);
        return result;
    }

    public static void Version(this Canonical url, string version)
    {
        if (string.IsNullOrEmpty(version))
            return;

        string newUrl = $"{url.BaseCanonicalUrl()}|{version}";
        string? fragment = url.Fragment();
        int indexOfVersion = url.Value.IndexOf("|");
        if (!string.IsNullOrEmpty(fragment))
            newUrl += $"#{fragment}";
        url.Value = newUrl;
    }

    public static string BaseCanonicalUrl(this Canonical url)
    {
        int indexOfVersion = url.Value.IndexOf("|");
        if (indexOfVersion != -1)
            return url.Value.Substring(0, indexOfVersion);
        int indexOfFragment = url.Value.IndexOf("#");
        if (indexOfFragment != -1)
            return url.Value.Substring(0, indexOfFragment);
        return url.Value;
    }

    public static string? Fragment(this Canonical url)
    {
        int indexOfFragment = url.Value.IndexOf("#");
        if (indexOfFragment != -1)
            return url.Value.Substring(0, indexOfFragment);
        return null;
    }
}

/// <summary>
/// This resolver will take any non version specific question and then resolve with
/// the designated version number
/// </summary>
internal class FixedVersionResolver : IAsyncResourceResolver
{
    public FixedVersionResolver(string version, IAsyncResourceResolver source)
    {
        _fixedVersion = version;
        _source = source;
    }
    private string _fixedVersion;
    private IAsyncResourceResolver _source;

    public async Task<Resource> ResolveByCanonicalUriAsync(string uri)
    {
        Canonical cu = new Canonical(uri);
        if (string.IsNullOrWhiteSpace(cu.Version()))
            cu.Version(_fixedVersion);
        return await _source.ResolveByCanonicalUriAsync(cu);
    }

    public async Task<Resource> ResolveByUriAsync(string uri)
    {
        Canonical cu = new Canonical(uri);
        if (string.IsNullOrWhiteSpace(cu.Version()))
            cu.Version(_fixedVersion);
        return await _source.ResolveByUriAsync(cu);
    }
}

internal class VersionFilterResolver : IAsyncResourceResolver
{
    public VersionFilterResolver(string version, IAsyncResourceResolver source)
    {
        _fixedVersion = version;
        _source = source;
    }
    private string _fixedVersion;
    private IAsyncResourceResolver _source;

    public async Task<Resource> ResolveByCanonicalUriAsync(string uri)
    {
        string convertedUrl = ConvertCanonical(uri);
        Canonical cu = new Canonical(convertedUrl);
        if (!string.IsNullOrWhiteSpace(cu.Version()))
        {
            if (cu.Version() != _fixedVersion)
                return null!;
        }
        var result = await _source.ResolveByCanonicalUriAsync(cu.BaseCanonicalUrl());
        return result;
    }

	private const string fhirBaseCanonical = "http://hl7.org/fhir/";

	public static string ConvertCanonical(string uri)
	{
		if (uri.StartsWith(fhirBaseCanonical))
		{
			var remainder = uri.Substring(fhirBaseCanonical.Length);
			string resourceName;
			if (remainder.StartsWith("StructureDefinition/"))
				remainder = remainder.Substring("StructureDefinition/".Length);
			if (!remainder.Contains("/"))
				return uri;
			resourceName = remainder.Substring(remainder.IndexOf("/") + 1);
			if (resourceName.StartsWith("StructureDefinition/"))
				resourceName = resourceName.Substring("StructureDefinition/".Length);
			remainder = remainder.Substring(0, remainder.IndexOf("/"));

			// convert this from the old format into the versioned format
			// http://hl7.org/fhir/3.0/StructureDefinition/Account
			// =>
			// http://hl7.org/fhir/StructureDefinition/Account|3.0
			// http://hl7.org/fhir/StructureDefinition/Account|3.0.1
			// http://hl7.org/fhir/StructureDefinition/Account|4.0.1
			// i.e. https://github.com/microsoft/fhir-codegen/blob/dev/src/Microsoft.Health.Fhir.SpecManager/Manager/FhirPackageCommon.cs#L513


			// TODO: @brianpos - I would recommend using the functions in FhirReleases that I keep up to date with builds and versions
			string version = remainder;
			switch (version)
			{
				case "1.0":
				case "1.0.2":
				case "DSTU2":
					version = "1.0";
					break;
				case "3.0":
				case "3.0.0":
				case "3.0.1":
				case "3.0.2":
				case "STU3":
					version = "3.0";
					break;

				case "4.0":
				case "4.0.0":
				case "4.0.1":
				case "R4":
					version = "4.0";
					break;

				case "4.3":
				case "4.3.0":
				case "R4B":
					version = "4.3";
					break;

				case "5.0":
				case "5.0.0":
				case "R5":
					version = "5.0";
					break;

				case "6.0":
				case "6.0.0":
				case "R6":
					version = "6.0";
					break;
				default:
					return uri;
			}
			return $"{fhirBaseCanonical}StructureDefinition/{resourceName}|{version}";
		}
		return uri;
	}

	public async Task<Resource> ResolveByUriAsync(string uri)
    {
        string convertedUrl = ConvertCanonical(uri);
        Canonical cu = new Canonical(convertedUrl);
        if (!string.IsNullOrWhiteSpace(cu.Version()))
        {
            if (cu.Version() != _fixedVersion)
                return null!;
        }
        return await _source.ResolveByUriAsync(cu.BaseCanonicalUrl());
    }
}

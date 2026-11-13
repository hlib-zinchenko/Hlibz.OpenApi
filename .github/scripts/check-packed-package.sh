#!/usr/bin/env bash
# Checks the packed package the way consumers get it, which the tests can't: they reference the
# library project and Microsoft.AspNetCore.OpenApi directly. Builds, outside the repo, two
# projects:
#   Shared  a class library holding a documented record (nothing but GenerateDocumentationFile),
#   Api     a web app referencing Shared and only Hlibz.OpenApi.AspNetCore, which serves the
#           record with AddXmlDocumentation() and checks its own document for the record's
#           summaries.
# Api must compile (Microsoft.AspNetCore.OpenApi's generator reaches it through the package, and
# needs the package's buildTransitive targets), then describe Shared's record both from a plain
# build and published as a single file, where assemblies have no location on disk.
#
# Usage: check-packed-package.sh <directory holding the .nupkg files>
set -euo pipefail

artifacts="$(cd "$1" && pwd)"
package="$(ls "$artifacts"/Hlibz.OpenApi.AspNetCore.[0-9]*.nupkg)"
version="$(basename "$package" .nupkg)"
version="${version#Hlibz.OpenApi.AspNetCore.}"

# The physical path: restore drops a project reference reached through a symlink (macOS /var).
work="$(cd "$(mktemp -d)" && pwd -P)"
trap 'rm -rf "$work"' EXIT
# Its own package cache: the packed version must never land in the machine-wide one.
export NUGET_PACKAGES="$work/packages"
# Only warnings and errors from the test app.
export Logging__LogLevel__Default=Warning

# The package only from the fresh pack; Microsoft.AspNetCore.OpenApi and the rest from nuget.org.
cat > "$work/nuget.config" <<EOF
<configuration>
  <packageSources>
    <clear />
    <add key="artifacts" value="$artifacts" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="artifacts">
      <package pattern="Hlibz.OpenApi.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

mkdir "$work/Shared" "$work/Api"

cat > "$work/Shared/Shared.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
EOF

cat > "$work/Shared/Thing.cs" <<'EOF'
namespace Shared;

/// <summary>A thing worth describing.</summary>
/// <param name="Name">The thing's name.</param>
public sealed record Thing(string Name);
EOF

cat > "$work/Api/Api.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>\$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Hlibz.OpenApi.AspNetCore" Version="$version" />
    <ProjectReference Include="../Shared/Shared.csproj" />
  </ItemGroup>
</Project>
EOF

cat > "$work/Api/Program.cs" <<'EOF'
using Hlibz.OpenApi;
using Shared;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddOpenApi(options => options.AddXmlDocumentation());
WebApplication app = builder.Build();
app.MapOpenApi();
app.MapGet("/thing", () => new Thing("pin"));
await app.StartAsync();

using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
string document = await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative));
await app.StopAsync();

bool described = document.Contains("A thing worth describing.", StringComparison.Ordinal)
                 && document.Contains("The thing's name.", StringComparison.Ordinal);
Console.WriteLine(described ? "Thing is described." : $"Thing isn't described:{Environment.NewLine}{document}");
return described ? 0 : 1;
EOF

echo "Checking Hlibz.OpenApi.AspNetCore $version from $artifacts"
echo "Plain build:"
dotnet run --project "$work/Api/Api.csproj"

echo "Single file:"
dotnet publish "$work/Api/Api.csproj" \
  --use-current-runtime \
  --self-contained false \
  -p:PublishSingleFile=true \
  --output "$work/single"
"$work/single/Api"

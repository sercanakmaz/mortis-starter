#!/usr/bin/env bash
# Conformance: the events-project add-on. Queue events live in a contracts project, and a second service builds against the contracts alone.
# Needs: PROJECT NAME LANG_SH WORK EXAMPLE; dotnet.
# Applies when: addons contains events-project
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

CONTRACTS="$PROJECT/src/$NAME.Contracts"
expect_equal "events-project: the contracts project exists and references only the shared kernel" "yes|1|yes" "$([ -d "$CONTRACTS" ] && echo yes || echo no)|$(grep -c '<ProjectReference' "$CONTRACTS/$NAME.Contracts.csproj")|$(grep -q "$NAME.Shared.csproj" "$CONTRACTS/$NAME.Contracts.csproj" && echo yes || echo no)"
expect_equal "events-project: the domain holds no queue event" "0" "$(grep -rlE 'IQueueEvent' "$PROJECT/src/$NAME.Domain" --include='*.cs' | wc -l | tr -d ' ')"

CONSUMER="$WORK/sample-consumer"
rm -rf "$CONSUMER" && mkdir -p "$CONSUMER"
cat > "$CONSUMER/SampleConsumer.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$CONTRACTS/$NAME.Contracts.csproj" />
  </ItemGroup>
</Project>
XML
if [ "${EXAMPLE:-false}" = "true" ]; then
  printf 'using System.Text.Json;\nusing %s.Contracts.Orders;\n\nvar lineAdded = JsonSerializer.Deserialize<LineAdded>("""{"Id":"o-1","LineId":"l-1","Sku":"SKU-1","Quantity":2}""");\nConsole.WriteLine($"{lineAdded!.Sku}|{lineAdded.Quantity}");\n' "$NAME" > "$CONSUMER/Program.cs"
else
  printf 'Console.WriteLine(typeof(%s.Contracts.ContractsMarker).Name);\n' "$NAME" > "$CONSUMER/Program.cs"
fi
expect_equal "events-project: a sample consumer referencing only the contracts builds" "0" "$(cd "$CONSUMER" && dotnet build >"$WORK/events-project.build.log" 2>&1; echo $?)"
if [ "${EXAMPLE:-false}" = "true" ]; then
  expect_equal "events-project: ... and reads a LineAdded event" "SKU-1|2" "$(cd "$CONSUMER" && dotnet run --no-build 2>/dev/null | tail -1)"
fi
expect_equal "events-project: the sample consumer's output holds no domain assembly" "no" "$(ls "$CONSUMER/bin/Debug/net10.0/" 2>/dev/null | grep -q "$NAME.Domain.dll" && echo yes || echo no)"

report
[ "$FAILED" -eq 0 ]
